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
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ILogger<LlmService>> _loggerMock;
    private readonly LlmService _llmService;

    public LlmServiceTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object);
        _configMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<LlmService>>();

        _configMock.Setup(c => c["LLM:ApiKey"]).Returns("test-key");
        _configMock.Setup(c => c["LLM:Model"]).Returns("gpt-4");
        _configMock.Setup(c => c["LLM:BaseUrl"]).Returns("https://api.test.com/");

        _llmService = new LlmService(_httpClient, _configMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsParsedResponse_OnSuccess()
    {
        // 1. Arrange
        var expectedJson = """
        {
          "summary": "The lead is looking for a pool.",
          "facts": {
            "visit_interest": { "value": true, "confidence": 0.95 }
          }
        }
        """;

        var apiResponse = new
        {
            choices = new[]
            {
                new { message = new { content = expectedJson } }
            }
        };

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(apiResponse))
            });

        // 2. Act
        var result = await _llmService.AnalyzeAsync("Context");

        // 3. Assert
        Assert.NotNull(result);
        Assert.Equal("The lead is looking for a pool.", result.Summary);
        Assert.True(result.Facts.ContainsKey("visit_interest"));
        Assert.Equal(true, ((JsonElement)result.Facts["visit_interest"].Value!).GetBoolean());
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsNull_OnHttpError()
    {
        // 1. Arrange
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        // 2. Act
        var result = await _llmService.AnalyzeAsync("Context");

        // 3. Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsNull_WhenApiKeyMissing()
    {
        // 1. Arrange
        _configMock.Setup(c => c["LLM:ApiKey"]).Returns(string.Empty);

        // 2. Act
        var result = await _llmService.AnalyzeAsync("Context");

        // 3. Assert
        Assert.Null(result);
    }
}
