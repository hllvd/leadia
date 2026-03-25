using Application.DTOs;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Integration;

[Collection("Sequential")]
public class FlowIntegrationTests
{
    private readonly Mock<IConversationStateRepository> _repoMock;
    private readonly Mock<IRealStateRepository> _realStateRepoMock;
    private readonly Mock<IPersistenceEventPublisher> _publisherMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly ConversationStateService _service;

    public FlowIntegrationTests()
    {
        _repoMock = new Mock<IConversationStateRepository>();
        _realStateRepoMock = new Mock<IRealStateRepository>();
        _publisherMock = new Mock<IPersistenceEventPublisher>();
        _configMock = new Mock<IConfiguration>();

        _service = new ConversationStateService(_repoMock.Object, _realStateRepoMock.Object, _publisherMock.Object, _configMock.Object);
    }

    [Fact]
    public async Task QuestionFlow_IntegrationTest()
    {
        var convId = "c1";
        
        _repoMock.Setup(r => r.GetByIdAsync(convId, default)).ReturnsAsync(new ConversationState { ConversationId = convId });
        _repoMock.Setup(r => r.GetFactsAsync(convId, default)).ReturnsAsync([]);
        _repoMock.Setup(r => r.GetTasksAsync(convId, default)).ReturnsAsync([]);

        // Phase 1: Customer asks a new question
        var response1 = new LlmResponse("Customer asked a question", null, 
            new LlmSignals { HasNewQuestion = true, HasUnansweredQuestion = true },
            new LlmContext(new LlmLastAction("msg", "customer", ""), null, null, null)
        );

        await _service.ApplyLlmResultAsync(convId, response1);

        _repoMock.Verify(r => r.UpsertTaskAsync(It.Is<ConversationTask>(t => 
            t.Type == "question" && t.Status == "open" && t.Owner == "broker"
        ), default), Times.Once);

        // Verify task transition notification fired
        _publisherMock.Verify(p => p.PublishNotificationAsync(convId, "task_state", It.IsAny<object>(), default), Times.AtLeastOnce());
    }

    [Fact]
    public async Task VisitFlow_IntegrationTest()
    {
        var convId = "c2";
        _repoMock.Setup(r => r.GetByIdAsync(convId, default)).ReturnsAsync(new ConversationState { ConversationId = convId });
        _repoMock.Setup(r => r.GetFactsAsync(convId, default)).ReturnsAsync([]);
        _repoMock.Setup(r => r.GetTasksAsync(convId, default)).ReturnsAsync([]);

        // Customer requests a visit
        var response = new LlmResponse("Customer requested visit", null, 
            new LlmSignals { VisitSuggested = true },
            new LlmContext(new LlmLastAction("msg", "customer", ""), new LlmVisitContext("amanhã", null), null, null)
        );

        await _service.ApplyLlmResultAsync(convId, response);

        _repoMock.Verify(r => r.UpsertTaskAsync(It.Is<ConversationTask>(t => 
            t.Type == "visit" && t.Status == "open" && t.Metadata.Values.Any(v => v.Contains("amanhã"))
        ), default), Times.Once);

        _publisherMock.Verify(p => p.PublishNotificationAsync(convId, "task_state", It.IsAny<object>(), default), Times.AtLeastOnce());
    }

    [Fact]
    public async Task DocumentsFlow_IntegrationTest()
    {
        var convId = "c3";
        _repoMock.Setup(r => r.GetByIdAsync(convId, default)).ReturnsAsync(new ConversationState { ConversationId = convId });
        _repoMock.Setup(r => r.GetFactsAsync(convId, default)).ReturnsAsync([]);
        _repoMock.Setup(r => r.GetTasksAsync(convId, default)).ReturnsAsync([]);

        // Broker requests documents
        var response = new LlmResponse("Broker requested documents", null, 
            new LlmSignals { HasPendingDocuments = true },
            new LlmContext(new LlmLastAction("msg", "broker", ""), null, null, new LlmDocumentsContext(true, "CNH and Proof of address"))
        );

        await _service.ApplyLlmResultAsync(convId, response);

        _repoMock.Verify(r => r.UpsertTaskAsync(It.Is<ConversationTask>(t => 
            t.Type == "documents" && t.Status == "open" && t.Owner == "customer" && t.Metadata.Values.Any(v => v.Contains("CNH"))
        ), default), Times.Once);

        _publisherMock.Verify(p => p.PublishNotificationAsync(convId, "task_state", It.IsAny<object>(), default), Times.AtLeastOnce());
    }

    [Fact]
    public async Task FollowUpFlow_IntegrationTest()
    {
        var convId = "c4";
        _repoMock.Setup(r => r.GetByIdAsync(convId, default)).ReturnsAsync(new ConversationState { ConversationId = convId });
        _repoMock.Setup(r => r.GetFactsAsync(convId, default)).ReturnsAsync([]);
        _repoMock.Setup(r => r.GetTasksAsync(convId, default)).ReturnsAsync([]);

        var response = new LlmResponse("Needs follow up", null, 
            new LlmSignals { NeedsFollowup = true },
            new LlmContext(new LlmLastAction("msg", "customer", ""), null, null, null)
        );

        await _service.ApplyLlmResultAsync(convId, response);

        _repoMock.Verify(r => r.UpsertTaskAsync(It.Is<ConversationTask>(t => 
            t.Type == "followup" && t.Status == "pending" && t.Owner == "broker"
        ), default), Times.Once);

        _publisherMock.Verify(p => p.PublishNotificationAsync(convId, "task_state", It.IsAny<object>(), default), Times.AtLeastOnce());
    }

    [Fact]
    public async Task UnresponsiveCustomer_IntegrationTest()
    {
        var convId = "c5";
        _repoMock.Setup(r => r.GetByIdAsync(convId, default)).ReturnsAsync(new ConversationState { ConversationId = convId });
        _repoMock.Setup(r => r.GetFactsAsync(convId, default)).ReturnsAsync([]);
        _repoMock.Setup(r => r.GetTasksAsync(convId, default)).ReturnsAsync([]);

        var response = new LlmResponse("Customer is ignoring texts", null, 
            new LlmSignals { CustomerUnresponsive = true },
            null
        );

        await _service.ApplyLlmResultAsync(convId, response);

        _publisherMock.Verify(p => p.PublishNotificationAsync(convId, "unresponsive", It.IsAny<object>(), default), Times.Once);
    }
}
