using System.Net.Http.Json;
using System.Text.Json;
using Application.DTOs;
using Application.Interfaces;
using Domain.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;

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

        // Log the prompt to a file
        if (Environment.GetEnvironmentVariable("DEBUG") == "true")
        {
            File.WriteAllText("/app/logs/llm_prompt.txt", JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { WriteIndented = true }));
        }

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
            
            if (_config["LOG_DEBUG"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false)
            {
                Console.WriteLine($"[DEBUG] LLM Raw Response: {content}");
            }

            if (string.IsNullOrEmpty(content)) return null;

            // Handle potential markdown backticks from LLM
            if (content.Contains("```"))
            {
                var lines = content.Split('\n');
                var cleaned = string.Join("\n", lines.Where(l => !l.Trim().StartsWith("```")));
                content = cleaned.Trim();
            }

            return JsonSerializer.Deserialize<LlmResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM analysis failed. URL: {Url}", $"{baseUrl.TrimEnd('/')}/chat/completions");
            return null; // Graceful failure as per LLM.md §9
        }
    }

    public async Task<string?> ChatAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        var apiKey  = _config["LLM:ApiKey"];
        var model   = _config["LLM:Model"] ?? "gpt-4o";
        var baseUrl = _config["LLM:BaseUrl"] ?? "https://api.openai.com/v1/";

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("LLM ApiKey is missing. Skipping chat.");
            return null;
        }

        var requestBody = new
        {
            model    = model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userMessage }
            },
            temperature = 0.7
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = JsonContent.Create(requestBody);

            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("LLM chat failed with status {Status}. Body: {Body}", response.StatusCode, errorBody);
                return null;
            }

            var result  = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var content = result.GetProperty("choices")[0]
                               .GetProperty("message")
                               .GetProperty("content")
                               .GetString();

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM chat failed. URL: {Url}", $"{baseUrl.TrimEnd('/')}/chat/completions");
            return null;
        }
    }

    private string GetSystemPrompt()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", PromptNames.AnalysisSystem);
            if (File.Exists(path)) return File.ReadAllText(path);

            return """
                Você é um assistente profissional analisando conversas de WhatsApp entre corretores imobiliários e clientes.
                Suas tarefas:
                1. Extrair ou atualizar fatos estruturados a partir das mensagens recentes.
                2. Gerar um resumo atualizado da conversa (1 parágrafo).
                Regras:
                - Responda apenas em JSON.
                - O JSON deve ter duas chaves: "summary" (o resumo em PT-BR) e "facts" (um dicionário simples chave: valor, tudo em PT-BR).
                - IMPORTANTE: Se um fato não foi mencionado, NÃO O INCLUA no JSON. Não use "Não especificado". Apenas omita a chave!
                """;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load analysis system prompt from file. Using hardcoded fallback.");
            return """
                Você é um assistente profissional analisando conversas de WhatsApp entre corretores imobiliários e clientes.
                Suas tarefas:
                1. Extrair ou atualizar fatos estruturados a partir das mensagens recentes.
                2. Gerar um resumo atualizado da conversa (1 parágrafo).
                Regras:
                - Responda apenas em JSON.
                - O JSON deve ter duas chaves: "summary" (o resumo em PT-BR) e "facts" (um dicionário simples chave: valor, tudo em PT-BR).
                - IMPORTANTE: Se um fato não foi mencionado, NÃO O INCLUA no JSON. Não use "Não especificado". Apenas omita a chave!
                """;
        }
    }
}
