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
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly INatsJSContext _js;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, ConversationActivity> _activeConversations = new();
    private readonly object _lock = new();

    private record ConversationActivity(string ConversationId)
    {
        public DateTimeOffset FirstActivity { get; init; } = DateTimeOffset.UtcNow;
        public DateTimeOffset LastActivity { get; set; } = DateTimeOffset.UtcNow;
        public int MessageCount { get; set; } = 1;
    }

    public Worker(ILogger<Worker> logger, INatsConnection connection, IServiceProvider serviceProvider, INatsJSContext? js = null)
    {
        _logger = logger;
        _js = js ?? new NatsJSContext(connection);
        _serviceProvider = serviceProvider;
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

        await foreach (var msg in consumer.ConsumeAsync<JsonElement>(cancellationToken: stoppingToken))
        {
            try
            {
                var eventData = msg.Data;

                // If the message was received as a string (value kind String), parse it as a JSON object
                if (eventData.ValueKind == JsonValueKind.String)
                {
                    var rawString = eventData.GetString();
                    if (!string.IsNullOrEmpty(rawString))
                    {
                        using var doc = JsonDocument.Parse(rawString);
                        eventData = doc.RootElement.Clone();
                    }
                }

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
                using var scope = _serviceProvider.CreateScope();
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

                // The background loop (CheckTriggersLoopAsync) will handle the LLM trigger
                // based on inactivity or message count.

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
                    await PerformAsyncAnalysisAndReply(activity.ConversationId, ct);
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
            using var scope = _serviceProvider.CreateScope();
            var convService = scope.ServiceProvider.GetRequiredService<ConversationStateService>();
            var llmService = scope.ServiceProvider.GetRequiredService<ILlmService>();

            _logger.LogInformation("Performing async analysis/reply for {ConvId}", conversationId);

            // 1. Trigger the analysis of the buffer
            var llmContext = await convService.TriggerAnalysisAsync(conversationId, ct);
            if (!string.IsNullOrEmpty(llmContext))
            {
                var llmResponse = await llmService.AnalyzeAsync(llmContext, ct);
                if (llmResponse != null)
                {
                    await convService.ApplyLlmResultAsync(conversationId, llmResponse, ct);
                }
            }

            // 2. Generate an AI reply if in Agent mode
            var state = await convService.GetStateAsync(conversationId, ct);
            if (state != null && state.Mode == Domain.Enums.ConversationMode.AgentAndListening)
            {
                // We typically reply if the last message was from the customer.
                // For simplicity, we'll check the current context.
                var welcomePrompt = await GetPromptAsync(Domain.Constants.PromptNames.BrokerSystem);
                var reply = await llmService.ChatAsync(welcomePrompt, llmContext ?? state.RollingSummary, ct);
                
                if (!string.IsNullOrEmpty(reply))
                {
                    _logger.LogInformation("Async AI reply generated for {ConvId}", conversationId);
                    // In a real app, you'd send this back to NATS as a reply event.
                    // For now, let's just log it or persist it as a broker message.
                    var brokerMsg = new NormalizedMessage(
                        conversationId,
                        state.BrokerId,
                        state.CustomerId,
                        Domain.Enums.SenderType.Broker,
                        reply,
                        DateTimeOffset.UtcNow,
                        Guid.NewGuid().ToString("N") // Dummy hash for reply
                    );
                    await convService.ProcessMessageAsync(brokerMsg, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during async analysis for {ConvId}", conversationId);
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
}
