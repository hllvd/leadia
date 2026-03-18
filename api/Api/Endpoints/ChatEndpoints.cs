using Application.Interfaces;
using Application.Services;
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
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "prompts", PromptNames.BrokerSystem);
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

                if (result.SummaryTriggered && result.LlmContext is not null)
                {
                    var llmResult = await llmService.AnalyzeAsync(result.LlmContext, ct);
                    if (llmResult is not null)
                        await convService.ApplyLlmResultAsync(normalized.ConversationId, llmResult, ct);
                }

                var userMessage = result.LlmContext is not null
                    ? $"{result.LlmContext}\n\nNow write your reply to the customer:"
                    : text;

                var reply = await llmService.ChatAsync(GetBrokerSystemPrompt(), userMessage, ct)
                            ?? "...";

                turns.Add(new
                {
                    customer = message,
                    broker   = reply,
                    summaryTriggered = result.SummaryTriggered
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
            if (string.IsNullOrWhiteSpace(req.From) || string.IsNullOrWhiteSpace(req.Message))
                return Results.BadRequest(new { error = "Fields 'from' and 'message' are required." });

            var senderType = req.Type?.ToLower() == "broker" ? SenderType.Broker : SenderType.Customer;
            var timestamp = DateTimeOffset.UtcNow;
            var text      = MessageNormalizer.Normalize(req.Message);

            var normalized = new NormalizedMessage(
                ConversationId: MessageNormalizer.BuildConversationId("local-broker", req.From),
                BrokerId:       "local-broker",
                CustomerId:     req.From,
                SenderType:     senderType,
                Text:           text,
                Timestamp:      timestamp,
                MessageHash:    MessageNormalizer.ComputeHash(timestamp.ToString("O"), "local-broker", req.From, text));

            // Full pipeline: dedup → buffer → optional summary + fact extraction
            var result = await convService.ProcessMessageAsync(normalized, ct);

            if (result is null)
                return Results.Ok(new { reply = "(duplicate message — ignored)" });

            // If buffer threshold was met, update summary + facts via LLM
            if (result.SummaryTriggered && result.LlmContext is not null)
            {
                var llmResult = await llmService.AnalyzeAsync(result.LlmContext, ct);
                if (llmResult is not null)
                    await convService.ApplyLlmResultAsync(normalized.ConversationId, llmResult, ct);
            }

            string? reply = null;

            // Only generate an AI reply if the customer sent the message and the chat is in Agent mode
            if (senderType == SenderType.Customer && result.UpdatedState.Mode == ConversationMode.AgentAndListening)
            {
                // Build user message for the broker reply: include context when available
                var userMessage = result.LlmContext is not null
                    ? $"{result.LlmContext}\n\nNow write your reply to the customer:"
                    : text;

                reply = await llmService.ChatAsync(GetBrokerSystemPrompt(), userMessage, ct)
                            ?? "I'm sorry, I couldn't process your message right now. Please try again.";
            }
            else if (senderType == SenderType.Customer && result.UpdatedState.Mode == ConversationMode.OnlyListening)
            {
                reply = null; // No AI reply in OnlyListening mode
            }

            // Load updated facts + summary to return to the caller
            var facts   = await convService.GetFactsAsync(normalized.ConversationId, ct);
            var summary = await convService.GetSummaryAsync(normalized.ConversationId, ct);

            return Results.Ok(new
            {
                reply,
                summary,
                facts = facts.Select(f => new { name = f.FactName, value = f.Value, confidence = f.Confidence })
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

    private record ChatRequest(string From, string Message, string? Type);
}
