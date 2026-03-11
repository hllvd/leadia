using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using System.Text.Json;

namespace PersistenceWorker;

/// <summary>
/// Background worker that consumes 'persist.*' events from NATS JetStream.
/// Implements Single Table Design writes to DynamoDB.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly INatsJSContext _js;
    private readonly IAmazonDynamoDB _db;
    private readonly string _tableName;

    private const string StreamName = "persistence_events";
    private const string ConsumerName = "persistence-worker-group";

    public Worker(ILogger<Worker> logger, INatsConnection connection, IAmazonDynamoDB db, IConfiguration config, INatsJSContext? js = null)
    {
        _logger = logger;
        _js = js ?? new NatsJSContext(connection);
        _db = db;
        _tableName = config["DynamoDB:Table"] ?? "crm_memory";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Persistence Worker starting...");

        // 1. Ensure Stream and Consumer exist (Matching QUEUE.md setup)
        try
        {
            await _js.CreateOrUpdateStreamAsync(new StreamConfig(StreamName, ["persist.*"]), stoppingToken);
            await _js.CreateOrUpdateConsumerAsync(StreamName, new ConsumerConfig(ConsumerName)
            {
                DurableName = ConsumerName,
                MaxDeliver = 10,
                AckWait = TimeSpan.FromSeconds(60),
                DeliverGroup = "persistence-workers"
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
                var type = eventData.GetProperty("type").GetString() ?? "";
                var payload = eventData.GetProperty("payload");

                _logger.LogInformation("Received persistence event: {Type}", type);

                switch (type)
                {
                    case "persist.message":
                        await HandlePersistMessageAsync(payload, stoppingToken);
                        break;
                    case "persist.summary":
                        await HandlePersistSummaryAsync(payload, stoppingToken);
                        break;
                    case "persist.facts":
                        await HandlePersistFactsAsync(payload, stoppingToken);
                        break;
                    default:
                        _logger.LogWarning("Unknown persistence event type: {Type}", type);
                        break;
                }

                await msg.AckAsync(cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist event to DynamoDB.");
                // NATS will re-deliver after AckWait (60s)
            }
        }
    }

    protected async Task HandlePersistMessageAsync(JsonElement payload, CancellationToken ct)
    {
        var convId = payload.GetProperty("conversation_id").GetString();
        var ts = payload.GetProperty("timestamp").GetString();
        var sender = payload.GetProperty("sender_type").GetString();
        var text = payload.GetProperty("text").GetString();

        var request = new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"CONV#{convId}" } },
                { "SK", new AttributeValue { S = $"MSG#{ts}" } },
                { "sender", new AttributeValue { S = sender } },
                { "text", new AttributeValue { S = text ?? "" } },
                { "hash", new AttributeValue { S = payload.GetProperty("hash").GetString() ?? "" } }
            }
        };

        await _db.PutItemAsync(request, ct);
    }

    protected async Task HandlePersistSummaryAsync(JsonElement payload, CancellationToken ct)
    {
        var convId = payload.GetProperty("conversation_id").GetString();
        var summary = payload.GetProperty("rolling_summary").GetString();
        var lastHash = payload.GetProperty("last_message_hash").GetString();
        var updatedAt = payload.GetProperty("updated_at").GetString();

        var request = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"CONV#{convId}" } },
                { "SK", new AttributeValue { S = "META" } }
            },
            AttributeUpdates = new Dictionary<string, AttributeValueUpdate>
            {
                { "rolling_summary", new AttributeValueUpdate { Action = AttributeAction.PUT, Value = new AttributeValue { S = summary ?? "" } } },
                { "last_hash", new AttributeValueUpdate { Action = AttributeAction.PUT, Value = new AttributeValue { S = lastHash ?? "" } } },
                { "last_updated", new AttributeValueUpdate { Action = AttributeAction.PUT, Value = new AttributeValue { S = updatedAt ?? "" } } }
            }
        };

        await _db.UpdateItemAsync(request, ct);
    }

    protected async Task HandlePersistFactsAsync(JsonElement payload, CancellationToken ct)
    {
        var convId = payload.GetProperty("conversation_id").GetString();
        var facts = payload.GetProperty("facts").EnumerateArray();
        var updatedAt = payload.GetProperty("updated_at").GetString();

        foreach (var fact in facts)
        {
            var name = fact.GetProperty("name").GetString();
            var value = fact.GetProperty("value").GetRawText(); // Handle object/string generically
            var confidence = fact.GetProperty("confidence").GetDouble();

            var request = new PutItemRequest
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"CONV#{convId}" } },
                    { "SK", new AttributeValue { S = $"FACT#{name}" } },
                    { "value", new AttributeValue { S = value } },
                    { "confidence", new AttributeValue { N = confidence.ToString(System.Globalization.CultureInfo.InvariantCulture) } },
                    { "updated_at", new AttributeValue { S = updatedAt ?? "" } }
                }
            };
            await _db.PutItemAsync(request, ct);
        }
    }
}
