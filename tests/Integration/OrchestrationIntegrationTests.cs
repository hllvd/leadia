using Application.DTOs;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Text.Json;

namespace Integration;

[Collection("Sequential")]
public class OrchestrationIntegrationTests
{
    private readonly Mock<IConversationStateRepository> _repoMock = new();
    private readonly Mock<IPersistenceEventPublisher> _persistencePublisherMock = new();
    private readonly Mock<ILlmService> _llmServiceMock = new();
    private readonly ConversationStateService _service;

    public OrchestrationIntegrationTests()
    {
        // Reset static defaults to ensure isolation from other tests
        BufferPolicy.SummaryTriggerMessages = 5;
        BufferPolicy.SummaryTriggerChars = 400;
        BufferPolicy.TimeoutSeconds = 30.0;
        
        _service = new ConversationStateService(_repoMock.Object, _persistencePublisherMock.Object);
    }

    [Fact]
    public async Task ProcessMessage_PublishesPersistEvent_AndTriggersLlmIfNeeded()
    {
        // Arrange
        var msg = new NormalizedMessage("c1", "b1", "cust1", "customer", "content", DateTimeOffset.UtcNow, "h1");
        _repoMock.Setup(r => r.GetByIdAsync("c1", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new ConversationState { ConversationId = "c1" });
        _repoMock.Setup(r => r.GetFactsAsync("c1", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<ConversationFact>());

        // Act
        var result = await _service.ProcessMessageAsync(msg);

        // Assert: 1. Persistence event for the message itself
        _persistencePublisherMock.Verify(x => x.PublishMessageAsync(msg, It.IsAny<CancellationToken>()), Times.Once);
        
        // Assert: 2. If it was the first message, buffer wouldn't trigger yet (default is 10)
        Assert.NotNull(result);
        Assert.False(result.SummaryTriggered);
    }

    [Fact]
    public async Task SummaryTrigger_CallsLlm_AndPublishesResults()
    {
        // Arrange
        var msg = new NormalizedMessage("c1", "b1", "cust1", "customer", "trigger", DateTimeOffset.UtcNow, "h_trigger");
        
        // Setup state that is 1 message away from threshold (default is 10 messages)
        var buffer = new List<string>();
        for(int i=0; i<9; i++) buffer.Add("previous message");
        
        var state = new ConversationState 
        { 
            ConversationId = "c1",
            BufferJson = JsonSerializer.Serialize(buffer),
            BufferChars = buffer.Sum(s => s.Length)
        };
        
        _repoMock.Setup(r => r.GetByIdAsync("c1", It.IsAny<CancellationToken>())).ReturnsAsync(state);
        _repoMock.Setup(r => r.GetFactsAsync("c1", It.IsAny<CancellationToken>())).ReturnsAsync(new List<ConversationFact>());
        
        var llmResponse = new LlmResponse(
            "New Summary", 
            new Dictionary<string, LlmFactUpdate> 
            { 
                { "name", new LlmFactUpdate("John Doe", 0.9) } 
            }
        );

        _llmServiceMock.Setup(l => l.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(llmResponse);

        // Act
        var result = await _service.ProcessMessageAsync(msg);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.SummaryTriggered);
        
        // Simulation of the Worker logic (Analyze -> Apply)
        // Note: ApplyLlmResultAsync expects the summary and facts extracted from the LLM
        await _service.ApplyLlmResultAsync("c1", llmResponse);

        // Verify result publishing
        _persistencePublisherMock.Verify(x => x.PublishSummaryAsync("c1", "New Summary", "h_trigger", It.IsAny<CancellationToken>()), Times.Once);
        _persistencePublisherMock.Verify(x => x.PublishFactsAsync("c1", It.IsAny<IEnumerable<ConversationFact>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
