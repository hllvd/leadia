using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Infrastructure.Repositories;

/// <summary>
/// Implementation of IConversationStateRepository using DynamoDB Single Table Design.
/// Table: crm_memory
/// PK = CONV#<id>
/// SK = META | MSG#<ts> | FACT#<name>
/// </summary>
public class DynamoDbConversationStateRepository : IConversationStateRepository
{
    private readonly IAmazonDynamoDB _db;
    private readonly string _tableName;

    public DynamoDbConversationStateRepository(IAmazonDynamoDB db, IConfiguration config)
    {
        _db = db;
        _tableName = config["DynamoDB:Table"] ?? "crm_memory";
    }

    public async Task<ConversationState?> GetByIdAsync(string conversationId, CancellationToken ct = default)
    {
        var request = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"CONV#{conversationId}" } },
                { "SK", new AttributeValue { S = "META" } }
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
                { "PK", new AttributeValue { S = $"CONV#{state.ConversationId}" } },
                { "SK", new AttributeValue { S = "META" } },
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
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk_prefix)",
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
            FactName = item["SK"].S.Replace("FACT#", ""),
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
                    { "PK", new AttributeValue { S = $"CONV#{conversationId}" } },
                    { "SK", new AttributeValue { S = $"FACT#{fact.FactName}" } },
                    { "value", new AttributeValue { S = fact.Value } },
                    { "confidence", new AttributeValue { N = fact.Confidence.ToString() } },
                    { "updated_at", new AttributeValue { S = fact.UpdatedAt.ToString("O") } }
                }
            };
            await _db.PutItemAsync(request, ct);
        }
    }
}
