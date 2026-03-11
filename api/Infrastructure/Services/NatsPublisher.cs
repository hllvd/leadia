using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using NATS.Client.Core;
using NATS.Client.JetStream;
using System.Text.Json;

namespace Infrastructure.Services;

/// <summary>
/// NATS JetStream implementation of message and persistence publishers.
/// Uses the NATS.Net library.
/// </summary>
public class NatsPublisher : IMessagePublisher, IPersistenceEventPublisher, IAsyncDisposable
{
    private readonly INatsConnection _connection;
    private readonly INatsJSContext _js;
    private readonly string _messagesStream;
    private readonly string _persistenceStream;

    public NatsPublisher(INatsConnection connection, IConfiguration config)
    {
        _connection = connection;
        _js = _connection.CreateJetStreamContext();
        _messagesStream = config["NATS:StreamMessages"] ?? "messages";
        _persistenceStream = config["NATS:StreamPersistence"] ?? "persistence_events";
    }

    // ── IMessagePublisher (API Gateway side) ────────────────────────────────

    public async Task PublishAsync(NormalizedMessage message, CancellationToken ct = default)
    {
        var payload = new { type = "message.received", payload = message };
        var json = JsonSerializer.Serialize(payload);
        await _js.PublishAsync("message.received", json, cancellationToken: ct);
    }

    // ── IPersistenceEventPublisher (Worker side) ─────────────────────────────

    public async Task PublishMessageAsync(NormalizedMessage message, CancellationToken ct = default)
    {
        var payload = new
        {
            type = "persist.message",
            payload = new
            {
                conversation_id = message.ConversationId,
                timestamp = message.Timestamp.ToString("O"),
                sender_type = message.SenderType,
                text = message.Text,
                hash = message.MessageHash
            }
        };
        var json = JsonSerializer.Serialize(payload);
        await _js.PublishAsync("persist.message", json, cancellationToken: ct);
    }

    public async Task PublishSummaryAsync(string conversationId, string summary, string lastHash, CancellationToken ct = default)
    {
        var payload = new
        {
            type = "persist.summary",
            payload = new
            {
                conversation_id = conversationId,
                rolling_summary = summary,
                last_message_hash = lastHash,
                updated_at = DateTimeOffset.UtcNow.ToString("O")
            }
        };
        var json = JsonSerializer.Serialize(payload);
        await _js.PublishAsync("persist.summary", json, cancellationToken: ct);
    }

    public async Task PublishFactsAsync(string conversationId, IEnumerable<ConversationFact> facts, CancellationToken ct = default)
    {
        var payload = new
        {
            type = "persist.facts",
            payload = new
            {
                conversation_id = conversationId,
                facts = facts.Select(f => new { name = f.FactName, value = f.Value, confidence = f.Confidence }),
                updated_at = DateTimeOffset.UtcNow.ToString("O")
            }
        };
        var json = JsonSerializer.Serialize(payload);
        await _js.PublishAsync("persist.facts", json, cancellationToken: ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
