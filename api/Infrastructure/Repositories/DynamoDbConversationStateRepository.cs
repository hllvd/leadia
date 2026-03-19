using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Infrastructure.Repositories;

/// <summary>
/// Implementation of IConversationStateRepository using DynamoDB Single Table Design.
/// Table: imobos / crm_memory
/// PrimaryKey = CONV#<id>
/// SortKey = SUM# | LAST#<hash> | FACTS#<name>
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
            LastActivityTimestamp = item.ContainsKey("last_activity") ? DateTimeOffset.Parse(item["last_activity"].S) : DateTimeOffset.MinValue,
            BufferJson = item.GetValueOrDefault("buffer_json")?.S ?? "[]",
            BufferChars = item.ContainsKey("buffer_chars") ? int.Parse(item["buffer_chars"].N) : 0,
            BrokerId = item.GetValueOrDefault("broker_id")?.S ?? "",
            CustomerId = item.GetValueOrDefault("customer_id")?.S ?? ""
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
                { "last_activity", new AttributeValue { S = state.LastActivityTimestamp.ToString("O") } },
                { "buffer_json", new AttributeValue { S = state.BufferJson } },
                { "buffer_chars", new AttributeValue { N = state.BufferChars.ToString() } },
                { "broker_id", new AttributeValue { S = state.BrokerId } },
                { "customer_id", new AttributeValue { S = state.CustomerId } }
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
                { ":sk_prefix", new AttributeValue { S = "FACTS#" } }
            }
        };

        var response = await _db.QueryAsync(request, ct);
        return response.Items.Select(item => new ConversationFact
        {
            ConversationId = conversationId,
            FactName = item[_sortKey].S.Replace("FACTS#", ""),
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
                    { _sortKey, new AttributeValue { S = $"FACTS#{fact.FactName}" } },
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
        var request = new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = $"{_primaryKey} = :pk AND begins_with({_sortKey}, :sk_prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"CONV#{conversationId}" } },
                { ":sk_prefix", new AttributeValue { S = "LAST#" } }
            }
        };

        var response = await _db.QueryAsync(request, ct);
        return response.Items.Select(item => new NormalizedMessage(
            conversationId,
            item.GetValueOrDefault("broker_id")?.S ?? "",
            item.GetValueOrDefault("customer_id")?.S ?? "",
            Enum.Parse<Domain.Enums.SenderType>(item["sender"].S),
            item["text"].S,
            DateTimeOffset.Parse(item["timestamp"].S),
            item[_sortKey].S.Replace("LAST#", "")
        )).OrderBy(m => m.Timestamp).ToList();
    }
}
