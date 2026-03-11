using Application.DTOs;
using Application.Interfaces;
using Application.Services;

namespace Api.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this WebApplication app, IConfiguration config)
    {
        app.MapPost("/api/webhook/{botNumber}", async (
            string botNumber,
            HttpContext httpContext,
            BotService botService,
            UserService userService,
            MessageService messageService,
            IUserRepository userRepository,
            IBotStrategyFactory strategyFactory,
            ConversationStateService conversationService) =>
        {
            // ── 1. Read raw body for HMAC verification ───────────────────────
            httpContext.Request.EnableBuffering();
            var rawBody = await ReadRawBodyAsync(httpContext.Request);
            httpContext.Request.Body.Position = 0;

            // ── 2. Validate HMAC-SHA256 signature ────────────────────────────
            var webhookSecret = config["Webhook:Secret"] ?? string.Empty;
            if (!string.IsNullOrEmpty(webhookSecret))
            {
                var signature = httpContext.Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
                if (!WebhookSignatureValidator.Validate(rawBody, signature, webhookSecret))
                    return Results.Unauthorized();
            }

            // ── 3. Parse body ─────────────────────────────────────────────────
            WebhookRequestDto? dto;
            try
            {
                dto = await httpContext.Request.ReadFromJsonAsync<WebhookRequestDto>();
            }
            catch
            {
                return Results.BadRequest(new { error = "Invalid JSON payload." });
            }

            if (dto is null || string.IsNullOrWhiteSpace(dto.From) || string.IsNullOrWhiteSpace(dto.Message))
                return Results.BadRequest(new { error = "Fields 'from' and 'message' are required." });

            // ── 4. Identify bot ───────────────────────────────────────────────
            var bot = await botService.GetByNumberAsync(botNumber);
            if (bot is null || !bot.IsActive)
                return Results.NotFound(new { error = "Bot not found or inactive." });

            // ── 5. Normalize message ──────────────────────────────────────────
            var normalizedText = MessageNormalizer.Normalize(dto.Message);
            var conversationId = MessageNormalizer.BuildConversationId(botNumber, dto.From);
            var timestamp      = DateTimeOffset.UtcNow;
            var messageHash    = MessageNormalizer.ComputeHash(
                                    timestamp.ToString("O"), bot.Id, dto.From, normalizedText);

            var normalized = new NormalizedMessage(
                ConversationId: conversationId,
                BrokerId:       bot.Id,
                CustomerId:     dto.From,
                SenderType:     "customer",
                Text:           normalizedText,
                Timestamp:      timestamp,
                MessageHash:    messageHash);

            // ── 6. Process through conversation state service ─────────────────
            var result = await conversationService.ProcessMessageAsync(normalized);
            if (result is null)
                return Results.Conflict(new { error = "Duplicate message." });   // HTTP 409

            // ── 7. Identify or auto-create user ───────────────────────────────
            var user = await userRepository.GetByWhatsAppNumberAsync(dto.From);
            if (user is null)
            {
                try
                {
                    await userService.CreateAsync(new CreateUserDto(
                        Name: dto.From,
                        Email: $"{dto.From.TrimStart('+')}@messaging.local",
                        Password: Guid.NewGuid().ToString(),
                        WhatsAppNumber: dto.From,
                        BotId: bot.Id));
                    user = await userRepository.GetByWhatsAppNumberAsync(dto.From);
                }
                catch
                {
                    return Results.Problem("Could not register user.");
                }
            }

            if (user is null)
                return Results.Problem("User lookup failed.");

            // ── 8. Store incoming message ─────────────────────────────────────
            await messageService.StoreAsync(user.Id, bot.Id, "customer", normalizedText);

            // ── 9. Resolve strategy and process ──────────────────────────────
            string reply;
            try
            {
                var strategy = strategyFactory.Resolve(user.BotType);
                reply = await strategy.ProcessMessageAsync(user, normalizedText);
            }
            catch (NotSupportedException)
            {
                reply = "Bot type not supported yet.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Bot processing failed: {ex.Message}");
                reply = "Sorry, an error occurred. Please try again.";
            }

            // ── 10. Store bot reply ───────────────────────────────────────────
            await messageService.StoreAsync(user.Id, bot.Id, "bot", reply);

            // ── 11. Background LLM processing ─────────────────────────────────
            if (result.SummaryTriggered && !string.IsNullOrEmpty(result.LlmContext))
            {
                // In a production app, this would be a message to a queue (NATS/RabbitMQ).
                // Here we use fire-and-forget to keep the webhook response fast.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var llmService = httpContext.RequestServices.GetRequiredService<ILlmService>();
                        var llmResponse = await llmService.AnalyzeAsync(result.LlmContext);
                        if (llmResponse != null)
                        {
                            await conversationService.ApplyLlmResultAsync(result.UpdatedState.ConversationId, llmResponse);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Background LLM processing failed: {ex.Message}");
                    }
                });
            }

            return Results.Ok(new { reply, conversationId, llmContextBuilt = result.SummaryTriggered });
        });
    }

    private static async Task<byte[]> ReadRawBodyAsync(HttpRequest request)
    {
        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms);
        return ms.ToArray();
    }
}
