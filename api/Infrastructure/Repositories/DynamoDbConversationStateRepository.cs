using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Infrastructure.Repositories;

/// <summary>
/// Implementation of IConversationStateRepository using DynamoDB Single Table Design.
/// Table: imobos / crm_memory
/// PrimaryKey = CONV#<id>
/// SortKey = SUM# | EVT#<timestamp>#<type> | FACT#<name> | BUFF# | PART#LSN
/// </summary>
public class DynamoDbConversationStateRepository : IConversationStateRepository
{
    private readonly IAmazonDynamoDB _db;
    private readonly string _tableName;
    private readonly string _primaryKey;
    private readonly string _sortKey;

    public DynamoDbConversationStateRepository(IAmazonDynamoDB db, IConfiguration config)
    {
        _db = db;
        bool isTest = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST"));
        _tableName = isTest ? "crm_memory" : (config["DynamoDB:Table"] ?? "imobos");
        _primaryKey = config["DynamoDB:PrimaryKey"] ?? "PK";
        _sortKey = config["DynamoDB:SortKey"] ?? "SK";
    }

    public async Task<ConversationState?> GetByIdAsync(string conversationId, CancellationToken ct = default)
    {
        var request = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { _primaryKey, new AttributeValue { S = $"CONV#{conversationId}" } },
                { _sortKey, new AttributeValue { S = "SUM#" } }
            }
        };

        var response = await _db.GetItemAsync(request, ct);
        if (!response.IsItemSet) return null;

        var item = response.Item;
        return new ConversationState
        {
            ConversationId = conversationId,
            RollingSummary = item.GetValueOrDefault("rolling_summary")?.S ?? "",
            LastMessageHash = item.GetValueOrDefault("last_hash")?.S ?? "",
            LastMessageTimestamp = item.ContainsKey("last_ts") ? DateTimeOffset.Parse(item["last_ts"].S) : DateTimeOffset.MinValue,
            LastActivityTimestamp = item.GetValueOrDefault("last_activity")?.S ?? string.Empty,
            BufferJson = item.GetValueOrDefault("buffer_json")?.S ?? "[]",
            BufferChars = item.ContainsKey("buffer_chars") ? int.Parse(item["buffer_chars"].N) : 0,
            BrokerId = item.GetValueOrDefault("broker_id")?.S ?? "",
            CustomerId = item.GetValueOrDefault("customer_id")?.S ?? "",
            Mode = item.ContainsKey("mode") ? (ConversationMode)int.Parse(item["mode"].N) : ConversationMode.OnlyListening,
            LastMessageActor = item.GetValueOrDefault("last_actor")?.S ?? "",
            ConsecutiveBrokerMessages = item.ContainsKey("consecutive_broker_msgs") ? int.Parse(item["consecutive_broker_msgs"].N) : 0,
            CreatedAt = item.ContainsKey("created_at") ? DateTimeOffset.Parse(item["created_at"].S) : DateTimeOffset.UtcNow
        };
    }

    public async Task UpsertAsync(ConversationState state, CancellationToken ct = default)
    {
        var request = new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                { _primaryKey, new AttributeValue { S = $"CONV#{state.ConversationId}" } },
                { _sortKey, new AttributeValue { S = "SUM#" } },
                { "rolling_summary", new AttributeValue { S = state.RollingSummary } },
                { "last_hash", new AttributeValue { S = state.LastMessageHash } },
                { "last_ts", new AttributeValue { S = state.LastMessageTimestamp.ToString("O") } },
                { "last_activity", new AttributeValue { S = state.LastActivityTimestamp } },
                { "buffer_json", new AttributeValue { S = state.BufferJson } },
                { "buffer_chars", new AttributeValue { N = state.BufferChars.ToString() } },
                { "broker_id", new AttributeValue { S = state.BrokerId } },
                { "customer_id", new AttributeValue { S = state.CustomerId } },
                { "mode", new AttributeValue { N = ((int)state.Mode).ToString() } },
                { "last_actor", new AttributeValue { S = state.LastMessageActor } },
                { "consecutive_broker_msgs", new AttributeValue { N = state.ConsecutiveBrokerMessages.ToString() } },
                { "created_at", new AttributeValue { S = state.CreatedAt.ToString("O") } }
            }
        };

        await _db.PutItemAsync(request, ct);
    }

    public async Task<IReadOnlyList<ConversationFact>> GetFactsAsync(string conversationId, CancellationToken ct = default)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = $"{_primaryKey} = :pk AND begins_with({_sortKey}, :sk_prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"CONV#{conversationId}" } },
                { ":sk_prefix", new AttributeValue { S = "FACT#" } }
            }
        };

        var response = await _db.QueryAsync(request, ct);
        return response.Items.Select(item => new ConversationFact
        {
            ConversationId = conversationId,
            FactName = item[_sortKey].S.Replace("FACT#", ""),
            Value = item["value"].S,
            Confidence = double.Parse(item["confidence"].N),
            UpdatedAt = DateTimeOffset.Parse(item["updated_at"].S)
        }).ToList();
    }

    public async Task UpsertFactsAsync(string conversationId, IEnumerable<ConversationFact> facts, CancellationToken ct = default)
    {
        foreach (var fact in facts)
        {
            var request = new PutItemRequest
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    { _primaryKey, new AttributeValue { S = $"CONV#{conversationId}" } },
                    { _sortKey, new AttributeValue { S = $"FACT#{fact.FactName}" } },
                    { "value", new AttributeValue { S = fact.Value } },
                    { "confidence", new AttributeValue { N = fact.Confidence.ToString() } },
                    { "updated_at", new AttributeValue { S = fact.UpdatedAt.ToString("O") } }
                }
            };
            await _db.PutItemAsync(request, ct);
        }
    }

    public async Task<IReadOnlyList<NormalizedMessage>> GetMessagesAsync(string conversationId, CancellationToken ct = default)
    {
        // Messages are now flushed to S3. This method would need to be updated 
        // to fetch from S3 if the full history is required in memory.
        // For now, it returns an empty list as we no longer store MSG# records.
        return await Task.FromResult(new List<NormalizedMessage>());
    }

    // Events methods removed during Signals+Tasks migration.

    public async Task<IReadOnlyList<ConversationTask>> GetTasksAsync(string conversationId, CancellationToken ct = default)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = $"{_primaryKey} = :pk AND begins_with({_sortKey}, :sk_prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"CONV#{conversationId}" } },
                { ":sk_prefix", new AttributeValue { S = "TASK#" } }
            }
        };

        var response = await _db.QueryAsync(request, ct);
        return response.Items.Select(item => new ConversationTask
        {
            Id = item.GetValueOrDefault("id")?.S ?? string.Empty,
            ConversationId = item["conversation_id"].S,
            Type = item["type"].S,
            Status = item["status"].S,
            Owner = item["owner"].S,
            Description = item["description"].S,
            MetadataJson = item.GetValueOrDefault("metadata_json")?.S ?? "{}",
            CreatedAt = item.ContainsKey("created_at") ? DateTimeOffset.Parse(item["created_at"].S) : DateTimeOffset.MinValue,
            UpdatedAt = item.ContainsKey("updated_at") ? DateTimeOffset.Parse(item["updated_at"].S) : DateTimeOffset.MinValue
        }).ToList();
    }

    public async Task UpsertTaskAsync(ConversationTask task, CancellationToken ct = default)
    {
        var request = new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                { _primaryKey, new AttributeValue { S = $"CONV#{task.ConversationId}" } },
                { _sortKey, new AttributeValue { S = $"TASK#{task.Type}" } },
                { "id", new AttributeValue { S = task.Id } },
                { "conversation_id", new AttributeValue { S = task.ConversationId } },
                { "type", new AttributeValue { S = task.Type } },
                { "status", new AttributeValue { S = task.Status } },
                { "owner", new AttributeValue { S = task.Owner } },
                { "description", new AttributeValue { S = task.Description } },
                { "metadata_json", new AttributeValue { S = task.MetadataJson } },
                { "created_at", new AttributeValue { S = task.CreatedAt.ToString("O") } },
                { "updated_at", new AttributeValue { S = task.UpdatedAt.ToString("O") } }
            }
        };
        await _db.PutItemAsync(request, ct);
    }

    public async Task<Application.DTOs.LlmSignals?> GetSignalsAsync(string conversationId, CancellationToken ct = default)
    {
        var request = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { _primaryKey, new AttributeValue { S = $"CONV#{conversationId}" } },
                { _sortKey, new AttributeValue { S = "SIGNALS" } }
            }
        };

        var response = await _db.GetItemAsync(request, ct);
        if (!response.IsItemSet) return null;

        var json = response.Item.GetValueOrDefault("payload_json")?.S;
        if (string.IsNullOrEmpty(json)) return null;

        return JsonSerializer.Deserialize<Application.DTOs.LlmSignals>(json);
    }

    public async Task UpsertSignalsAsync(string conversationId, Application.DTOs.LlmSignals signals, CancellationToken ct = default)
    {
        var request = new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                { _primaryKey, new AttributeValue { S = $"CONV#{conversationId}" } },
                { _sortKey, new AttributeValue { S = "SIGNALS" } },
                { "payload_json", new AttributeValue { S = JsonSerializer.Serialize(signals) } },
                { "updated_at", new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") } }
            }
        };
        await _db.PutItemAsync(request, ct);
    }
}
