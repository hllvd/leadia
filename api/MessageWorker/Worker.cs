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

    public Worker(ILogger<Worker> logger, INatsConnection connection, IServiceProvider serviceProvider, INatsJSContext? js = null)
    {
        _logger = logger;
        _js = js ?? new NatsJSContext(connection);
        _serviceProvider = serviceProvider;
    }

    private const string StreamName = "messages";
    private const string ConsumerName = "message-worker-group";
    private const string Subject = "message.received";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message Worker starting...");

        try
        {
            await _js.CreateOrUpdateStreamAsync(new StreamConfig(StreamName, [Subject]), stoppingToken);
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
                
                if (type != Subject)
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

                // Handle LLM trigger
                if (result.SummaryTriggered && !string.IsNullOrEmpty(result.LlmContext))
                {
                    _logger.LogInformation("Buffer triggered LLM analysis for {ConvId}", normalized.ConversationId);
                    var llmResponse = await llmService.AnalyzeAsync(result.LlmContext, stoppingToken);
                    if (llmResponse != null)
                    {
                        await convService.ApplyLlmResultAsync(normalized.ConversationId, llmResponse, stoppingToken);
                    }
                }

                await msg.AckAsync(cancellationToken: stoppingToken);
                _logger.LogInformation("Successfully processed and Acked message {Hash}", normalized.MessageHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process NATS message.");
                // NATS will re-deliver after AckWait (30s)
            }
        }
    }
}
