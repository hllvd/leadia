using System.Net;
using System.Text.Json;
using Application.DTOs;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace Unit;

public class LlmServiceTests
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly Mock<ILogger<LlmService>> _loggerMock;
    private readonly LlmService _llmService;

    public LlmServiceTests()
    {
        _httpClient = new HttpClient();
        _loggerMock = new Mock<ILogger<LlmService>>();

        var inMemoryConfig = new Dictionary<string, string?> {
            {"LLM:ApiKey", "test-key"},
            {"LLM:Model", "gpt-4"},
            {"LLM:BaseUrl", "https://api.test.com/"},
            {"LLM:TimeoutSeconds", "5"}
        };

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        _llmService = new LlmService(_httpClient, _config, _loggerMock.Object);
    }


    [Fact]
    public async Task AnalyzeAsync_ReturnsNull_WhenApiKeyMissing()
    {
        // 1. Arrange - create a service with missing key
        var emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { {"LLM:ApiKey", ""} })
            .Build();
        var llmWithNoKey = new LlmService(_httpClient, emptyConfig, _loggerMock.Object);

        // 2. Act
        var result = await llmWithNoKey.AnalyzeAsync("Context");

        // 3. Assert
        Assert.Null(result);
    }
}
