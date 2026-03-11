using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using System.Text.Json;

namespace MessageWorker;

/// <summary>
/// Background worker that consumes 'message.received' events from NATS JetStream.
/// Orchestrates conversation logic and LLM triggers.
/// </summary>
public class Worker(
    ILogger<Worker> logger,
    INatsJSContext js,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    private const string StreamName = "messages";
    private const string ConsumerName = "message-worker-group";
    private const string Subject = "message.received";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Message Worker starting...");

        // 1. Ensure Stream and Consumer exist (Matching QUEUE.md setup)
        try
        {
            await js.CreateOrUpdateStreamAsync(new StreamConfig(StreamName, [Subject]), stoppingToken);
            await js.CreateOrUpdateConsumerAsync(StreamName, new ConsumerConfig(ConsumerName)
            {
                DurableName = ConsumerName,
                DeliverPolicy = ConsumerDeliverPolicy.All,
                AckPolicy = ConsumerAckPolicy.Explicit,
                MaxDeliver = 5,
                AckWait = TimeSpan.FromSeconds(30),
                DeliverGroup = "message-workers"
            }, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Stream/Consumer creation skipped or failed. NATS might need manual setup. Error: {Error}", ex.Message);
        }

        // 2. Consume messages
        var consumer = await js.GetConsumerAsync(StreamName, ConsumerName, stoppingToken);

        await foreach (var msg in consumer.ConsumeAsync<JsonElement>(cancellationToken: stoppingToken))
        {
            try
            {
                var eventData = msg.Data;
                var type = eventData.GetProperty("type").GetString();
                
                if (type != Subject)
                {
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }

                var normalizedJson = eventData.GetProperty("payload").GetRawText();
                var normalized = JsonSerializer.Deserialize<NormalizedMessage>(normalizedJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (normalized == null)
                {
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }

                logger.LogInformation("Processing message {Hash} for conversation {ConvId}", normalized.MessageHash, normalized.ConversationId);

                // Use a scope for the conversation service which might have scoped deps (like the repo)
                using var scope = scopeFactory.CreateScope();
                var convService = scope.ServiceProvider.GetRequiredService<ConversationStateService>();
                var llmService  = scope.ServiceProvider.GetRequiredService<ILlmService>();

                var result = await convService.ProcessMessageAsync(normalized, stoppingToken);
                
                if (result == null)
                {
                    logger.LogInformation("Message {Hash} is a duplicate. Acking.", normalized.MessageHash);
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }

                // Handle LLM trigger
                if (result.SummaryTriggered && !string.IsNullOrEmpty(result.LlmContext))
                {
                    logger.LogInformation("Buffer triggered LLM analysis for {ConvId}", normalized.ConversationId);
                    var llmResponse = await llmService.AnalyzeAsync(result.LlmContext, stoppingToken);
                    if (llmResponse != null)
                    {
                        await convService.ApplyLlmResultAsync(normalized.ConversationId, llmResponse, stoppingToken);
                    }
                }

                await msg.AckAsync(cancellationToken: stoppingToken);
                logger.LogInformation("Successfully processed and Acked message {Hash}", normalized.MessageHash);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process NATS message.");
                // NATS will re-deliver after AckWait (30s)
            }
        }
    }
}
