using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
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
    private readonly IMessageStorage _s3Storage;
    private readonly string _tableName;
    private readonly string _primaryKey;
    private readonly string _sortKey;
    private readonly int _flushIntervalMinutes;

    private const string StreamName = "persistence_events";
    private const string ConsumerName = "persistence-worker-group";

    public Worker(ILogger<Worker> logger, INatsConnection connection, IAmazonDynamoDB db, IMessageStorage s3Storage, IConfiguration config, INatsJSContext? js = null)
    {
        _logger = logger;
        _js = js ?? new NatsJSContext(connection);
        _db = db;
        _s3Storage = s3Storage;
        bool isTest = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST"));
        _tableName = isTest ? "crm_memory" : (config["DynamoDB:Table"] ?? "imobos");
        _primaryKey = config["DynamoDB:PrimaryKey"] ?? "PK";
        _sortKey = config["DynamoDB:SortKey"] ?? "SK";
        _flushIntervalMinutes = int.TryParse(config["S3:FlushIntervalMinutes"], out var val) ? val : 10;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Persistence Worker starting...");

        // 1. Ensure Stream and Consumer exist (Matching QUEUE.md setup)
        try
        {
            await _js.CreateOrUpdateStreamAsync(new StreamConfig(StreamName, ["persist.>"]), stoppingToken);
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

        await foreach (var msg in consumer.ConsumeAsync<string>(cancellationToken: stoppingToken))
        {
            try
            {
                var json = msg.Data;
                if (string.IsNullOrEmpty(json))
                {
                    _logger.LogWarning("Received empty message from NATS.");
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }

                using var doc = JsonDocument.Parse(json);
                var eventData = doc.RootElement;
                
                if (eventData.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogWarning("Received non-object persistence event. ValueKind: {ValueKind}", eventData.ValueKind);
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }

                if (!eventData.TryGetProperty("type", out var typeElement))
                {
                    _logger.LogWarning("Persistence event missing 'type' property.");
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }

                var type = typeElement.GetString() ?? "";
                var payload = eventData.TryGetProperty("payload", out var payloadElement) ? payloadElement : default;

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
                    case "persist.event":
                        await HandlePersistEventAsync(payload, stoppingToken);
                        break;
                    case "flush.messages":
                        await HandleFlushMessagesAsync(payload, stoppingToken);
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

    protected async Task HandlePersistEventAsync(JsonElement payload, CancellationToken ct)
    {
        var convId = payload.GetProperty("conversation_id").GetString();
        var type = payload.GetProperty("type").GetString();
        var actor = payload.GetProperty("actor").GetString();
        var description = payload.GetProperty("description").GetString();
        var timestamp = payload.GetProperty("timestamp").GetString();

        var request = new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                { _primaryKey, new AttributeValue { S = $"CONV#{convId}" } },
                { _sortKey, new AttributeValue { S = $"EVT#{timestamp}#{type}" } },
                { "type", new AttributeValue { S = type ?? "" } },
                { "actor", new AttributeValue { S = actor ?? "" } },
                { "description", new AttributeValue { S = description ?? "" } },
                { "timestamp", new AttributeValue { S = timestamp ?? "" } },
                { "conversation_id", new AttributeValue { S = convId ?? "" } }
            }
        };

        await _db.PutItemAsync(request, ct);
    }

    protected async Task HandlePersistMessageAsync(JsonElement payload, CancellationToken ct)
    {
        var convId = payload.GetProperty("conversation_id").GetString();
        if (string.IsNullOrEmpty(convId)) return;

        var ts = payload.GetProperty("timestamp").GetString();
        var senderRaw = payload.GetProperty("sender_type");
        var senderStr = senderRaw.ValueKind == JsonValueKind.String ? senderRaw.GetString() : senderRaw.GetInt32().ToString();
        var text = payload.GetProperty("text").GetString() ?? "";
        var hash = payload.GetProperty("hash").GetString() ?? "";

        var newMessage = new 
        {
            conversation_id = convId,
            timestamp = ts,
            sender = senderStr,
            text = text,
            hash = hash
        };

        // 1. Append message to BUFF# JSON array in DynamoDB using UpdateItem
        var buffRequest = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { _primaryKey, new AttributeValue { S = $"CONV#{convId}" } },
                { _sortKey, new AttributeValue { S = "BUFF#" } }
            },
            UpdateExpression = "SET messages = list_append(if_not_exists(messages, :empty_list), :msg), is_buffering = :true",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":msg", new AttributeValue { L = new List<AttributeValue> { new AttributeValue { S = JsonSerializer.Serialize(newMessage) } } } },
                { ":empty_list", new AttributeValue { L = new List<AttributeValue> { new AttributeValue { S = "__EMPTY_BUFFER__" } } } },
                { ":true", new AttributeValue { BOOL = true } }
            },
            ReturnValues = ReturnValue.UPDATED_OLD
        };

        var buffResponse = await _db.UpdateItemAsync(buffRequest, ct);

        // If this was the first message in the buffer (is_buffering was not true before)
        bool wasBuffering = buffResponse.Attributes.TryGetValue("is_buffering", out var isBufferingAttr) && isBufferingAttr.BOOL;
        if (!wasBuffering)
        {
            // Publish delayed NATS event
            var flushPayload = new { type = "flush.messages", payload = new { conversation_id = convId } };
            var natsDelay = $"{_flushIntervalMinutes}m";
            
            var headers = new NatsHeaders { { "Nats-Delay", natsDelay } };
            var json = JsonSerializer.Serialize(flushPayload);
            
            // We need to publish to JetStream. Since we only injected INatsJSContext, we can use it.
            await _js.PublishAsync("persist.flush", json, headers: headers, cancellationToken: ct);
            _logger.LogInformation("Started {Delay} delay before flushing messages for conversation {ConversationId}", natsDelay, convId);
        }

        // 2. Update metadata (SUM#) row with last_message_id and last_updated
        // Also set first_message_id if it doesn't exist
        var updateRequest = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { _primaryKey, new AttributeValue { S = $"CONV#{convId}" } },
                { _sortKey, new AttributeValue { S = "SUM#" } }
            },
            UpdateExpression = "SET last_message_id = :hash, last_updated = :ts, first_message_id = if_not_exists(first_message_id, :hash)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":hash", new AttributeValue { S = hash } },
                { ":ts", new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") } }
            }
        };
        await _db.UpdateItemAsync(updateRequest, ct);
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
                { _primaryKey, new AttributeValue { S = $"CONV#{convId}" } },
                { _sortKey, new AttributeValue { S = "SUM#" } }
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
            var value = fact.GetProperty("value").GetString();
            var confidence = fact.GetProperty("confidence").GetDouble();

            var request = new PutItemRequest
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    { _primaryKey, new AttributeValue { S = $"CONV#{convId}" } },
                    { _sortKey, new AttributeValue { S = $"FACT#{name}" } },
                    { "value", new AttributeValue { S = value } },
                    { "confidence", new AttributeValue { N = confidence.ToString(System.Globalization.CultureInfo.InvariantCulture) } },
                    { "updated_at", new AttributeValue { S = updatedAt ?? "" } }
                }
            };
            await _db.PutItemAsync(request, ct);
        }
    }

    protected async Task HandleFlushMessagesAsync(JsonElement payload, CancellationToken ct)
    {
        var convId = payload.GetProperty("conversation_id").GetString();
        if (string.IsNullOrEmpty(convId)) return;

        // 1. Get the BUFF# record
        var getBuffReq = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { _primaryKey, new AttributeValue { S = $"CONV#{convId}" } },
                { _sortKey, new AttributeValue { S = "BUFF#" } }
            }
        };
        var buffResp = await _db.GetItemAsync(getBuffReq, ct);
        
        if (!buffResp.IsItemSet || !buffResp.Item.TryGetValue("messages", out var messagesAttr) || messagesAttr.L.Count == 0)
        {
            _logger.LogInformation("No messages to flush for conversation {ConversationId}", convId);
            return;
        }

        // 2. Parse messages
        var messages = new List<NormalizedMessage>();
        foreach (var attr in messagesAttr.L)
        {
            if (attr.S == "__EMPTY_BUFFER__") continue;

            try 
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(attr.S);
                if (dict == null) continue;

                // Support both string ("Customer") and numeric ("1") sender representation
                var senderStr = dict["sender"];
                var senderEnum = int.TryParse(senderStr, out var senderInt) 
                                 ? (SenderType)senderInt 
                                 : Enum.Parse<SenderType>(senderStr, ignoreCase: true);

                messages.Add(new NormalizedMessage(
                    dict["conversation_id"],
                    "", // broker_id
                    "", // customer_id
                    senderEnum,
                    dict["text"],
                    DateTimeOffset.Parse(dict["timestamp"]),
                    dict["hash"]
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing buffered message for conversation {ConversationId}", convId);
            }
        }

        if (messages.Count == 0) return;

        // 3. Get / Increment PART#LSN
        var getPartReq = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { _primaryKey, new AttributeValue { S = $"CONV#{convId}" } },
                { _sortKey, new AttributeValue { S = "PART#LSN" } }
            }
        };
        var partResp = await _db.GetItemAsync(getPartReq, ct);
        int currentPart = partResp.IsItemSet && partResp.Item.ContainsKey("value") 
                          ? int.Parse(partResp.Item["value"].N) 
                          : 1;

        // 4. Upload to S3
        await _s3Storage.StoreMessagesAsync(convId, currentPart, messages, ct);

        // 5. Delete BUFF# securely and Update PART#LSN
        var batchWrite = new TransactWriteItemsRequest
        {
            TransactItems = new List<TransactWriteItem>
            {
                new TransactWriteItem
                {
                    Delete = new Delete
                    {
                        TableName = _tableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            { _primaryKey, new AttributeValue { S = $"CONV#{convId}" } },
                            { _sortKey, new AttributeValue { S = "BUFF#" } }
                        }
                    }
                },
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = _tableName,
                        Item = new Dictionary<string, AttributeValue>
                        {
                            { _primaryKey, new AttributeValue { S = $"CONV#{convId}" } },
                            { _sortKey, new AttributeValue { S = "PART#LSN" } },
                            { "value", new AttributeValue { N = (currentPart + 1).ToString() } }
                        }
                    }
                }
            }
        };
        await _db.TransactWriteItemsAsync(batchWrite, ct);

        _logger.LogInformation("Successfully flushed {Count} messages to S3 for conversation {ConversationId} as part {Part}", messages.Count, convId, currentPart);
    }
}
