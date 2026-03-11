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
            IMessagePublisher publisher) =>
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

            // ── 4. Identify bot (Fast check) ──────────────────────────────────
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

            // ── 6. Offload to NATS (Event Driven) ─────────────────────────────
            await publisher.PublishAsync(normalized);

            // ── 7. Immediate return ───────────────────────────────────────────
            return Results.Ok(new { 
                status = "received", 
                conversationId, 
                messageHash 
            });
        });
    }

    private static async Task<byte[]> ReadRawBodyAsync(HttpRequest request)
    {
        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms);
        return ms.ToArray();
    }
}
