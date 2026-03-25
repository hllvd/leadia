using Application.DTOs;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Unit;

public class TaskMappingTests
{
    private readonly Mock<IConversationStateRepository> _repoMock;
    private readonly Mock<IRealStateRepository> _realStateRepoMock;
    private readonly Mock<IPersistenceEventPublisher> _publisherMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly ConversationStateService _service;

    public TaskMappingTests()
    {
        _repoMock = new Mock<IConversationStateRepository>();
        _realStateRepoMock = new Mock<IRealStateRepository>();
        _publisherMock = new Mock<IPersistenceEventPublisher>();
        _configMock = new Mock<IConfiguration>();

        _service = new ConversationStateService(_repoMock.Object, _realStateRepoMock.Object, _publisherMock.Object, _configMock.Object);
    }

    [Fact]
    public async Task ApplyLlmResult_HasNewQuestion_CreatesOpenQuestionTask_ForBroker()
    {
        var convId = "c1";
        
        _repoMock.Setup(r => r.GetByIdAsync(convId, default)).ReturnsAsync(new ConversationState { ConversationId = convId });
        _repoMock.Setup(r => r.GetFactsAsync(convId, default)).ReturnsAsync([]);
        _repoMock.Setup(r => r.GetTasksAsync(convId, default)).ReturnsAsync([]);

        var response = new LlmResponse("Test summary", null, 
            new LlmSignals { HasNewQuestion = true },
            new LlmContext(new LlmLastAction("msg", "customer", ""), null, null, null)
        );

        await _service.ApplyLlmResultAsync(convId, response);

        _repoMock.Verify(r => r.UpsertTaskAsync(It.Is<ConversationTask>(t => 
            t.Type == "question" && 
            t.Status == "open" && 
            t.Owner == "broker"
        ), default), Times.Once);
    }

    [Fact]
    public async Task ApplyLlmResult_HasUnansweredQuestionFalse_CompletesQuestionTask()
    {
        var convId = "c1";
        
        _repoMock.Setup(r => r.GetByIdAsync(convId, default)).ReturnsAsync(new ConversationState { ConversationId = convId });
        _repoMock.Setup(r => r.GetFactsAsync(convId, default)).ReturnsAsync([]);
        _repoMock.Setup(r => r.GetTasksAsync(convId, default)).ReturnsAsync([
            new ConversationTask { Type = "question", Status = "open", Owner = "broker" }
        ]);

        var response = new LlmResponse("Test summary", null, 
            new LlmSignals { HasUnansweredQuestion = false }, // it defaults to false, simulating broker answered
            new LlmContext(new LlmLastAction("msg", "broker", ""), null, null, null)
        );

        await _service.ApplyLlmResultAsync(convId, response);

        _repoMock.Verify(r => r.UpsertTaskAsync(It.Is<ConversationTask>(t => 
            t.Type == "question" && 
            t.Status == "completed"
        ), default), Times.Once);
    }

    [Fact]
    public async Task ApplyLlmResult_VisitConfirmed_CompletesVisitTask()
    {
        var convId = "c1";
        
        _repoMock.Setup(r => r.GetByIdAsync(convId, default)).ReturnsAsync(new ConversationState { ConversationId = convId });
        _repoMock.Setup(r => r.GetFactsAsync(convId, default)).ReturnsAsync([]);
        _repoMock.Setup(r => r.GetTasksAsync(convId, default)).ReturnsAsync([
            new ConversationTask { Type = "visit", Status = "open" }
        ]);

        var response = new LlmResponse("Test summary", null, 
            new LlmSignals { VisitConfirmed = true }, 
            null
        );

        await _service.ApplyLlmResultAsync(convId, response);

        _repoMock.Verify(r => r.UpsertTaskAsync(It.Is<ConversationTask>(t => 
            t.Type == "visit" && 
            t.Status == "completed"
        ), default), Times.Once);
    }
    
    [Fact]
    public async Task ApplyLlmResult_Idempotency_NoChange_DoesNotUpsert()
    {
        var convId = "c1";
        var existingTask = new ConversationTask 
        { 
            Id = "uuid1", Type = "followup", Status = "completed", Owner = "broker", Description = "Follow up with customer", MetadataJson = "{}"
        };

        _repoMock.Setup(r => r.GetByIdAsync(convId, default)).ReturnsAsync(new ConversationState { ConversationId = convId });
        _repoMock.Setup(r => r.GetFactsAsync(convId, default)).ReturnsAsync([]);
        _repoMock.Setup(r => r.GetTasksAsync(convId, default)).ReturnsAsync([ existingTask ]);

        var response = new LlmResponse("Test summary", null, 
            new LlmSignals { NeedsFollowup = false }, 
            null
        );

        await _service.ApplyLlmResultAsync(convId, response);

        // Verify UpsertTaskAsync was never called for followup task because status remains the same
        _repoMock.Verify(r => r.UpsertTaskAsync(It.Is<ConversationTask>(t => t.Type == "followup"), default), Times.Never);
    }

    [Fact]
    public async Task ApplyLlmResult_HasNewQuestion_IncludesUserQuestionInMetadata()
    {
        var convId = "c1";
        var questionText = "Posso levar meus filhos?";
        
        _repoMock.Setup(r => r.GetByIdAsync(convId, default)).ReturnsAsync(new ConversationState { ConversationId = convId });
        _repoMock.Setup(r => r.GetFactsAsync(convId, default)).ReturnsAsync([]);
        _repoMock.Setup(r => r.GetTasksAsync(convId, default)).ReturnsAsync([]);

        var response = new LlmResponse("Test summary", null, 
            new LlmSignals { HasNewQuestion = true },
            new LlmContext(new LlmLastAction("question", "customer", questionText), null, null, null)
        );

        await _service.ApplyLlmResultAsync(convId, response);

        _repoMock.Verify(r => r.UpsertTaskAsync(It.Is<ConversationTask>(t => 
            t.Type == "question" && 
            t.Metadata.ContainsKey("user_question") && 
            t.Metadata["user_question"] == questionText
        ), default), Times.Once);
    }

    [Fact]
    public async Task ApplyLlmResult_CallSuggested_CreatesOpenCallTask()
    {
        var convId = "c1";
        
        _repoMock.Setup(r => r.GetByIdAsync(convId, default)).ReturnsAsync(new ConversationState { ConversationId = convId });
        _repoMock.Setup(r => r.GetFactsAsync(convId, default)).ReturnsAsync([]);
        _repoMock.Setup(r => r.GetTasksAsync(convId, default)).ReturnsAsync([]);

        var response = new LlmResponse("Test summary", null, 
            new LlmSignals { CallSuggested = true }, 
            new LlmContext(null, null, new LlmCallContext("hoje", "15h", "video"), null)
        );

        await _service.ApplyLlmResultAsync(convId, response);

        _repoMock.Verify(r => r.UpsertTaskAsync(It.Is<ConversationTask>(t => 
            t.Type == "call" && 
            t.Status == "open" &&
            t.Metadata.ContainsKey("proposed_time") &&
            t.Metadata["proposed_time"] == "15h" &&
            t.Metadata["type"] == "video"
        ), default), Times.Once);
    }

    [Fact]
    public async Task ApplyLlmResult_CallConfirmed_CompletesCallTask()
    {
        var convId = "c1";
        
        _repoMock.Setup(r => r.GetByIdAsync(convId, default)).ReturnsAsync(new ConversationState { ConversationId = convId });
        _repoMock.Setup(r => r.GetFactsAsync(convId, default)).ReturnsAsync([]);
        _repoMock.Setup(r => r.GetTasksAsync(convId, default)).ReturnsAsync([
            new ConversationTask { Type = "call", Status = "open" }
        ]);

        var response = new LlmResponse("Test summary", null, 
            new LlmSignals { CallConfirmed = true }, 
            null
        );

        await _service.ApplyLlmResultAsync(convId, response);

        _repoMock.Verify(r => r.UpsertTaskAsync(It.Is<ConversationTask>(t => 
            t.Type == "call" && 
            t.Status == "completed"
        ), default), Times.Once);
    }
}
