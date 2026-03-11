using System.Net.Http.Json;
using System.Text.Json;
using Application.DTOs;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Generic OpenAI-compatible LLM service.
/// Configurable for OpenAI, Azure, Mistral, Qwen, etc.
/// </summary>
public class LlmService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<LlmService> _logger;

    public LlmService(HttpClient httpClient, IConfiguration config, ILogger<LlmService> logger)
    {
        _httpClient = httpClient;
        _config     = config;
        _logger     = logger;

        // Configure timeout from appsettings (default 10s as per spec)
        var timeoutSeconds = config.GetValue<int>("LLM:TimeoutSeconds", 10);
        _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    public async Task<LlmResponse?> AnalyzeAsync(string context, CancellationToken ct = default)
    {
        var apiKey  = _config["LLM:ApiKey"];
        var model   = _config["LLM:Model"] ?? "gpt-4o";
        var baseUrl = _config["LLM:BaseUrl"] ?? "https://api.openai.com/v1/";

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("LLM ApiKey is missing. Skipping analysis.");
            return null;
        }

        var requestBody = new
        {
            model          = model,
            messages       = new[]
            {
                new { role = "system", content = GetSystemPrompt() },
                new { role = "user",   content = context }
            },
            temperature     = 0.2,
            response_format = new { type = "json_object" }
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = JsonContent.Create(requestBody);

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var choice = result.GetProperty("choices")[0];
            var message = choice.GetProperty("message");
            var content = message.GetProperty("content").GetString();

            if (string.IsNullOrEmpty(content)) return null;

            return JsonSerializer.Deserialize<LlmResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM analysis failed for context: {Context}", context);
            return null; // Graceful failure as per LLM.md §9
        }
    }

    private static string GetSystemPrompt() =>
        """
        You are an assistant analyzing WhatsApp conversations between real estate brokers and leads.

        Your tasks:
        1. Extract or update structured facts from the latest messages.
        2. Generate an updated one-paragraph summary of the conversation so far.

        Rules:
        - Only update facts that are clearly supported by the messages.
        - Do not invent or assume facts not mentioned.
        - Preserve existing facts if not contradicted.
        - The summary must be concise (1-3 sentences).
        - Respond in JSON format only with keys: "summary" and "facts".
        - Each fact in "facts" must be an object: { "value": any, "confidence": number }.
        """;
}
