using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using System.Text.Json;

namespace EngagementWorker;

/// <summary>
/// Consumes "conversation.engagement.check" events published (with a delay) by MessageWorker.
/// If the conversation is still stale (same last message hash, actor = broker), generates a nudge task.
/// </summary>
public class Worker(
    ILogger<Worker> logger,
    INatsConnection connection,
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    INatsJSContext? js = null) : BackgroundService
{
    private readonly INatsJSContext _js = js ?? new NatsJSContext(connection);

    private const string StreamName    = "engagement";
    private const string ConsumerName  = "engagement-worker-group";
    private const string Subject       = "conversation.engagement.check";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Engagement Worker starting...");

        try
        {
            await _js.CreateOrUpdateStreamAsync(new StreamConfig(StreamName, [Subject])
            {
                MaxAge = TimeSpan.FromHours(24)
            }, stoppingToken);

            await _js.CreateOrUpdateConsumerAsync(StreamName, new ConsumerConfig(ConsumerName)
            {
                DurableName  = ConsumerName,
                MaxDeliver   = 3,
                AckWait      = TimeSpan.FromSeconds(30),
                DeliverGroup = "engagement-workers"
            }, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Stream/Consumer creation skipped or failed. Error: {Error}", ex.Message);
        }

        var consumer = await _js.GetConsumerAsync(StreamName, ConsumerName, stoppingToken);

        await foreach (var msg in consumer.ConsumeAsync<string>(cancellationToken: stoppingToken))
        {
            try
            {
                if (string.IsNullOrEmpty(msg.Data))
                {
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }

                var payload = JsonSerializer.Deserialize<EngagementCheckPayload>(msg.Data, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (payload is null)
                {
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }

                await HandleEngagementCheckAsync(payload, stoppingToken);
                await msg.AckAsync(cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing engagement check event.");
                await msg.AckAsync(cancellationToken: stoppingToken);
            }
        }
    }

    private async Task HandleEngagementCheckAsync(EngagementCheckPayload payload, CancellationToken ct)
    {
        await using var scope    = serviceProvider.CreateAsyncScope();
        var convService          = scope.ServiceProvider.GetRequiredService<ConversationStateService>();
        var realStateRepository  = scope.ServiceProvider.GetRequiredService<IRealStateRepository>();
        var taskRepository       = scope.ServiceProvider.GetRequiredService<IConversationStateRepository>();
        var llmService           = scope.ServiceProvider.GetRequiredService<ILlmService>();

        var state = await convService.GetStateAsync(payload.ConversationId, ct);
        if (state is null) return;

        // Only nudge if the last actor is still the broker AND the hash hasn't changed
        // (meaning no customer reply arrived since the event was scheduled)
        if (state.LastMessageActor != "broker" || state.LastMessageHash != payload.LastMessageHash)
        {
            logger.LogInformation("Conversation {Id} has changed since check was scheduled. Skipping nudge.", payload.ConversationId);
            return;
        }

        // Check threshold
        var brokerAssignment = await realStateRepository.GetAssignmentsByBrokerIdAsync(payload.BrokerId, ct);
        var agency           = brokerAssignment?.RealStateAgency;
        var threshold        = NudgeConfigResolver.GetAfterMessages(brokerAssignment, agency);

        if (state.ConsecutiveBrokerMessages < threshold)
        {
            logger.LogInformation("Conversation {Id} hasn't reached nudge threshold ({Count}/{Threshold}).", 
                payload.ConversationId, state.ConsecutiveBrokerMessages, threshold);
            return;
        }

        logger.LogInformation("Conversation {Id} is still stale and reached threshold. Generating nudge task.", payload.ConversationId);

        // Generate the nudge text using the LLM nudge prompt
        var nudgePrompt = await GetPromptAsync("nudge_system.md");
        var llmContext  = LlmContextBuilder.Build(state.RollingSummary, [], [], string.Empty, DateTimeOffset.UtcNow);
        var nudgeText   = await llmService.ChatAsync(nudgePrompt, llmContext, ct);

        if (string.IsNullOrWhiteSpace(nudgeText)) return;

        // Create or update a nudge task (queue for broker review — not sent automatically)
        var existing = (await taskRepository.GetTasksAsync(payload.ConversationId, ct))
            .FirstOrDefault(t => t.Type == "nudge" && t.Status == "pending");

        if (existing is null)
        {
            var nudgeTask = new ConversationTask
            {
                Id             = Guid.NewGuid().ToString("N"),
                ConversationId = payload.ConversationId,
                Type           = "nudge",
                Status         = "pending",
                Owner          = "broker",
                Description    = "Sistema sugeriu: " + nudgeText,
                Metadata       = new Dictionary<string, string>
                {
                    ["suggested_nudge"] = nudgeText,
                    ["reason"]          = "customer_inactivity"
                }
            };
            await taskRepository.UpsertTaskAsync(nudgeTask, ct);
            logger.LogInformation("Nudge task created for {Id}: {Nudge}", payload.ConversationId, nudgeText);
        }
    }

    private static async Task<string> GetPromptAsync(string name)
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, "Prompts", name);
            
            // Search up to 3 levels up if not found (helps during development)
            int levels = 0;
            while (!File.Exists(path) && levels < 3 && baseDir != null)
            {
                baseDir = Path.GetDirectoryName(baseDir);
                if (baseDir != null) path = Path.Combine(baseDir, "Prompts", name);
                levels++;
            }

            return File.Exists(path) ? await File.ReadAllTextAsync(path) : "You are a professional assistant.";
        }
        catch { return "You are a professional assistant."; }
    }
}

/// <summary>Payload for a delayed engagement check event.</summary>
public record EngagementCheckPayload(
    string ConversationId,
    string LastMessageHash,
    string BrokerId);
