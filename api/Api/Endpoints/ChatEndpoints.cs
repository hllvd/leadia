using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class ChatEndpoints
{
    private static string GetBrokerSystemPrompt()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", PromptNames.BrokerSystem);
            if (File.Exists(path)) return File.ReadAllText(path);

            return "You are a professional real estate broker assistant.";
        }
        catch
        {
            return "You are a professional real estate broker assistant.";
        }
    }

    // Scripted conversation — 8 turns that exercise the full pipeline
    private static readonly (string From, string Message)[] ExampleConversation =
    [
        ("customer-demo", "Oi! Estou procurando um apartamento para comprar."),
        ("customer-demo", "Quero algo com 3 quartos, de preferência no Leblon ou Ipanema."),
        ("customer-demo", "Meu orçamento é de até R$ 2 milhões."),
        ("customer-demo", "Precisa ter vaga de garagem e pode ser em andar alto."),
        ("customer-demo", "Tenho financiamento pré-aprovado pela Caixa."),
        ("customer-demo", "Gostaria de visitar o imóvel ainda essa semana se possível."),
        ("customer-demo", "Você tem algum imóvel com vista para o mar?"),
        ("customer-demo", "Perfeito. Pode me enviar as fotos e o valor do condomínio?"),
    ];

    public static void MapChatEndpoints(this WebApplication app)
    {
        app.MapGet("/api/chat/example", async (
            ConversationStateService convService,
            ILlmService llmService,
            CancellationToken ct) =>
        {
            var turns = new List<object>();

            foreach (var (from, message) in ExampleConversation)
            {
                var timestamp = DateTimeOffset.UtcNow;
                var text      = MessageNormalizer.Normalize(message);

                var normalized = new NormalizedMessage(
                    ConversationId: MessageNormalizer.BuildConversationId("local-broker", from),
                    BrokerId:       "local-broker",
                    CustomerId:     from,
                    SenderType:     SenderType.Customer,
                    Text:           text,
                    Timestamp:      timestamp,
                    MessageHash:    MessageNormalizer.ComputeHash(timestamp.ToString("O"), "local-broker", from, text));

                var result = await convService.ProcessMessageAsync(normalized, ct);
                if (result is null) continue;

                // Note: The example endpoint is now partially broken because it expects immediate results.
                // We'll just show the message for now.
                var userMessage = text;

                var reply = await llmService.ChatAsync(GetBrokerSystemPrompt(), userMessage, ct)
                            ?? "...";

                turns.Add(new
                {
                    customer = message,
                    broker   = reply
                });

                // Small delay so timestamps differ and dedup hash stays unique
                await Task.Delay(10, ct);
            }

            return Results.Ok(new { conversation = turns });
        });

        app.MapPost("/api/chat", async (
            ChatRequest req,
            ConversationStateService convService,
            ILlmService llmService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.BrokerNumber) || string.IsNullOrWhiteSpace(req.CustomerNumber))
                return Results.BadRequest(new { error = "Fields 'broker_number' and 'customer_number' are required." });

            if (string.IsNullOrWhiteSpace(req.Message))
                return Results.BadRequest(new { error = "Field 'message' is required." });

            var senderType = req.Type?.ToLower() == "broker" ? SenderType.Broker : SenderType.Customer;
            var timestamp = DateTimeOffset.UtcNow;
            var text      = MessageNormalizer.Normalize(req.Message);

            var normalized = new NormalizedMessage(
                ConversationId: MessageNormalizer.BuildConversationId(req.BrokerNumber, req.CustomerNumber),
                BrokerId:       req.BrokerNumber,
                CustomerId:     req.CustomerNumber,
                SenderType:     senderType,
                Text:           text,
                Timestamp:      timestamp,
                MessageHash:    MessageNormalizer.ComputeHash(timestamp.ToString("O"), req.BrokerNumber, req.CustomerNumber, text));

            // Full pipeline: dedup → buffer
            var result = await convService.ProcessMessageAsync(normalized, ct);

            if (result is null)
                return Results.Ok(new { reply = "(duplicate message — ignored)" });

            // We no longer trigger LLM here. The MessageWorker will handle it asynchronously.
            // The API returns immediately.
            return Results.Accepted($"/api/chat/{normalized.ConversationId}/history", new
            {
                status = "queued",
                conversationId = normalized.ConversationId
            });
        });

        app.MapGet("/api/chat/{conversationId}/history", async (
            string conversationId,
            ConversationStateService convService,
            CancellationToken ct) =>
        {
            var facts    = await convService.GetFactsAsync(conversationId, ct);
            var summary  = await convService.GetSummaryAsync(conversationId, ct);
            var messages = await convService.GetMessagesAsync(conversationId, ct);
            
            return Results.Ok(new
            {
                summary,
                facts = facts.Select(f => new { name = f.FactName, value = f.Value, confidence = f.Confidence }),
                messages = messages.Select(m => new { 
                    sender = m.SenderType.ToString().ToLower(), 
                    text = m.Text, 
                    timestamp = m.Timestamp 
                })
            });
        });

        app.MapGet("/api/chat/fact-metadata", () =>
        {
            return Results.Ok(new
            {
                keys = FactKeys.All,
                labels = FactKeys.Labels
            });
        });



    }

    private record ChatRequest(string BrokerNumber, string CustomerNumber, string Message, string? Type);
}
