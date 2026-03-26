using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
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
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly INatsJSContext _js;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, ConversationActivity> _activeConversations = new();
    private readonly object _lock = new();
    private readonly IConfiguration _configuration; // Added IConfiguration field

    private record ConversationActivity(string ConversationId)
    {
        public DateTimeOffset FirstActivity { get; init; } = DateTimeOffset.UtcNow;
        public DateTimeOffset LastActivity { get; set; } = DateTimeOffset.UtcNow;
        public int MessageCount { get; set; } = 1;
    }

    public Worker(ILogger<Worker> logger, INatsConnection connection, IServiceProvider serviceProvider, IConfiguration configuration, INatsJSContext? js = null)
    {
        _logger = logger;
        _js = js ?? new NatsJSContext(connection);
        _serviceProvider = serviceProvider;
        _configuration = configuration; // Initialized IConfiguration
    }

    private const string StreamName = "messages";
    private const string ConsumerName = "message-worker-group";
    private const string Subject = "message.received.>";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message Worker starting...");

        try
        {
            await _js.CreateOrUpdateStreamAsync(new StreamConfig(StreamName, [Subject])
            {
                MaxAge = TimeSpan.FromMinutes(10)
            }, stoppingToken);
            await _js.CreateOrUpdateConsumerAsync(StreamName, new ConsumerConfig(ConsumerName)
            {
                DurableName = ConsumerName,
                MaxDeliver = 5,
                AckWait = TimeSpan.FromSeconds(30),
                DeliverGroup = "message-workers"
            }, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Stream/Consumer creation skipped or failed. NATS might need manual setup. Error: {Error}", ex.Message);
        }

        // 1. Start trigger check loop
        _ = Task.Run(() => CheckTriggersLoopAsync(stoppingToken), stoppingToken);

        // 2. Consume messages
        var consumer = await _js.GetConsumerAsync(StreamName, ConsumerName, stoppingToken);

        await foreach (var msg in consumer.ConsumeAsync<string>(cancellationToken: stoppingToken))
        {
            try
            {
                var json = msg.Data;
                if (string.IsNullOrEmpty(json))
                {
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }

                using var doc = JsonDocument.Parse(json);
                var eventData = doc.RootElement;

                if (eventData.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogWarning("Received non-object message event. ValueKind: {ValueKind}", eventData.ValueKind);
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }

                if (!eventData.TryGetProperty("type", out var typeElement))
                {
                    _logger.LogWarning("Message event missing 'type' property.");
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }

                var type = typeElement.GetString();
                
                if (type != "message.received")
                {
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }

                if (!eventData.TryGetProperty("payload", out var payloadElement))
                {
                    _logger.LogWarning("Message event missing 'payload' property.");
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }

                var normalizedJson = payloadElement.GetRawText();
                var normalized = JsonSerializer.Deserialize<NormalizedMessage>(normalizedJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (normalized == null)
                {
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }

                _logger.LogInformation("Processing message {Hash} for conversation {ConvId}", normalized.MessageHash, normalized.ConversationId);

                // Use a scope for the conversation service which might have scoped deps (like the repo)
                await using var scope = _serviceProvider.CreateAsyncScope();
                var convService = scope.ServiceProvider.GetRequiredService<ConversationStateService>();
                var llmService  = scope.ServiceProvider.GetRequiredService<ILlmService>();

                var result = await convService.ProcessMessageAsync(normalized, stoppingToken);
                
                if (result == null)
                {
                    _logger.LogInformation("Message {Hash} is a duplicate. Acking.", normalized.MessageHash);
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }

                // Track activity for debounced LLM analysis
                lock (_lock)
                {
                    if (_activeConversations.TryGetValue(normalized.ConversationId, out var activity))
                    {
                        activity.LastActivity = DateTimeOffset.UtcNow;
                        activity.MessageCount++;
                    }
                    else
                    {
                        _activeConversations[normalized.ConversationId] = new ConversationActivity(normalized.ConversationId);
                    }
                }

                // If this was a broker message, schedule a delayed engagement check
                if (normalized.SenderType == Domain.Enums.SenderType.Broker)
                {
                    _ = Task.Run(() => ScheduleEngagementCheckAsync(
                        normalized.ConversationId,
                        normalized.BrokerId,
                        normalized.MessageHash,
                        scope.ServiceProvider.GetRequiredService<IRealStateRepository>(),
                        stoppingToken), stoppingToken);
                }

                await msg.AckAsync(cancellationToken: stoppingToken);
                _logger.LogInformation("Successfully processed and Acked message {Hash}", normalized.MessageHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from NATS JetStream.");
                // Depending on error type, you might want to Nack or Terminate the message.
                // For now, we'll ack to prevent reprocessing of potentially bad messages,
                // but a more robust solution might inspect the error.
                await msg.AckAsync(cancellationToken: stoppingToken);
            }
        }
    }
    private async Task CheckTriggersLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(2000, ct); // Check every 2 seconds

                List<ConversationActivity> toTrigger = new();
                lock (_lock)
                {
                    var now = DateTimeOffset.UtcNow;
                    foreach (var kvp in _activeConversations.ToList())
                    {
                        var activity = kvp.Value;
                        var inactiveSeconds = (now - activity.LastActivity).TotalSeconds;
                        var totalWaitSeconds = (now - activity.FirstActivity).TotalSeconds;

                        // Rules:
                        // 1. 10s of inactivity
                        // 2. If 3+ messages, don't wait more than 30s total
                        if (inactiveSeconds >= 10 || (activity.MessageCount >= 3 && totalWaitSeconds >= 30))
                        {
                            toTrigger.Add(activity);
                            _activeConversations.Remove(kvp.Key);
                        }
                    }
                }

                foreach (var activity in toTrigger)
                {
                    _logger.LogInformation("Triggering analysis for {Id} (Inactivity: {S}s)", activity.ConversationId, (DateTimeOffset.UtcNow - activity.LastActivity).TotalSeconds);
                    _ = Task.Run(() => PerformAsyncAnalysisAndReply(activity.ConversationId, ct), ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in trigger check loop");
            }
        }
    }

    private async Task PerformAsyncAnalysisAndReply(string conversationId, CancellationToken ct)
    {
        try
        {
            var debug = _configuration["LOG_DEBUG"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
            if (debug) _logger.LogInformation("[LOUD] Starting analysis/reply flow for {ConvId}", conversationId);
            await using var scope = _serviceProvider.CreateAsyncScope();
            var convService = scope.ServiceProvider.GetRequiredService<ConversationStateService>();
            var llmService  = scope.ServiceProvider.GetRequiredService<ILlmService>();

            // 1. Trigger the analysis of the buffer
            var llmContext = await convService.TriggerAnalysisAsync(conversationId, ct);
            if (!string.IsNullOrEmpty(llmContext))
            {
                if (debug) _logger.LogInformation("[LOUD] Sending context to LLM ({Len} chars)", llmContext.Length);
                var llmResponse = await llmService.AnalyzeAsync(llmContext, ct);
                if (llmResponse != null)
                {
                    if (debug) _logger.LogInformation("[LOUD] LLM Analysis success. Summary: {Summary}", llmResponse.Summary);
                    await convService.ApplyLlmResultAsync(conversationId, llmResponse, ct);
                }
            }

            // 2. Generate an AI reply if in Agent mode
            var state = await convService.GetStateAsync(conversationId, ct);
            if (state != null && state.Mode == Domain.Enums.ConversationMode.AgentAndListening)
            {
                if (debug) _logger.LogInformation("[LOUD] Generating AI reply for {ConvId}", conversationId);
                var welcomePrompt = await GetPromptAsync(Domain.Constants.PromptNames.BrokerSystem);
                var reply = await llmService.ChatAsync(welcomePrompt, llmContext ?? state.RollingSummary, ct);
                if (!string.IsNullOrEmpty(reply))
                {
                    var brokerMsg = new NormalizedMessage(
                        conversationId,
                        state.BrokerId,
                        state.CustomerId,
                        Domain.Enums.SenderType.Broker,
                        reply,
                        DateTimeOffset.UtcNow,
                        Guid.NewGuid().ToString("N")
                    );
                    await convService.ProcessMessageAsync(brokerMsg, ct);
                    if (debug) _logger.LogInformation("[LOUD] AI reply sent: {Reply}", reply);
                }
            }
        }
        catch (Exception ex)
        {
            if (_configuration["LOG_DEBUG"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false) 
                _logger.LogError(ex, "[LOUD] Error in PerformAsyncAnalysisAndReply for {ConvId}", conversationId);
        }
    }

    private async Task<string> GetPromptAsync(string name)
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", name);
            if (File.Exists(path)) return await File.ReadAllTextAsync(path);
            return "You are a professional assistant.";
        }
        catch { return "You are a professional assistant."; }
    }

    /// <summary>
    /// Publishes a delayed engagement check event to NATS so the EngagementWorker
    /// can nudge the customer if they haven't replied within the configured window.
    /// </summary>
    private async Task ScheduleEngagementCheckAsync(
        string conversationId,
        string brokerId,
        string lastMessageHash,
        IRealStateRepository realStateRepo,
        CancellationToken ct)
    {
        try
        {
            // Resolve broker + agency to get the configured timeout
            var brokerAssignment = await realStateRepo.GetAssignmentsByBrokerIdAsync(brokerId, ct);
            var agency           = brokerAssignment?.RealStateAgency;
            var delayMinutes     = NudgeConfigResolver.GetTimeoutMinutes(brokerAssignment, agency);

            var payload = JsonSerializer.Serialize(new
            {
                conversationId,
                lastMessageHash,
                brokerId,
                scheduledAt = DateTimeOffset.UtcNow
            });

            // Publish to the engagement stream with a delay header
            var headers = new NatsHeaders
            {
                // NATS JetStream Delayed Delivery support
                ["Nats-Delivery-Delay"] = $"{(int)TimeSpan.FromMinutes(delayMinutes).TotalSeconds}s"
            };

            await _js.PublishAsync(
                subject: "conversation.engagement.check",
                data: payload,
                headers: headers,
                cancellationToken: ct);

            _logger.LogInformation(
                "Scheduled engagement check for {Id} in {Min} minutes.",
                conversationId, delayMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to schedule engagement check for {Id}", conversationId);
        }
    }
}
