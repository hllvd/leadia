using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.BotEngine;

/// <summary>
/// A bot strategy that uses AI-driven memory (facts/summary) to provide more contextual replies.
/// </summary>
public class AiBotStrategy(ILlmService llmService, ConversationStateService conversationService) : IBotStrategy
{
    private readonly ILlmService _llmService = llmService;
    private readonly ConversationStateService _conversationService = conversationService;
    public BotType SupportedType => BotType.GenericAi;

    public async Task<string> ProcessMessageAsync(User user, string message, CancellationToken ct = default)
    {
        // 1. All bots now go through the same state orchestration via WebhookEndpoints -> ConversationStateService.
        // 2. Here, we can leverage the facts and summary if they exist.
        
        // This is a placeholder for a more advanced AI-driven reply logic.
        // For now, it simply acknowledges the context if an LLM summary was just built.
        
        return "🤖 Olá! Estou processando sua mensagem com auxílio de IA. Em breve terei mais detalhes sobre seu perfil.";
    }
}
