using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NATS.Client.Core;
using NATS.Client.JetStream;
using PersistenceWorker;
using System.Text.Json;

namespace Unit;

public class PersistenceWorkerTests
{
    private readonly Mock<ILogger<Worker>> _loggerMock = new();
    private readonly Mock<INatsJSContext> _jsMock = new();
    private readonly Mock<IAmazonDynamoDB> _dbMock = new();
    private readonly Mock<IConfiguration> _configMock = new();

    public PersistenceWorkerTests()
    {
        _configMock.Setup(c => c["DynamoDB:Table"]).Returns("crm_memory");
    }

    [Fact]
    public async Task HandlePersistMessageAsync_MapsCorrectPayload()
    {
        var connMock = new Mock<INatsConnection>();
        var worker = new TestablePersistenceWorker(_loggerMock.Object, connMock.Object, _dbMock.Object, _configMock.Object, _jsMock.Object);
        var payloadJson = "{\"conversation_id\":\"c1\", \"timestamp\":\"2024-03-11T12:00:00Z\", \"sender_type\":\"customer\", \"text\":\"hello\", \"hash\":\"h1\"}";
        var payload = JsonDocument.Parse(payloadJson).RootElement;

        await worker.ExposeHandlePersistMessageAsync(payload, default);

        _dbMock.Verify(x => x.PutItemAsync(It.Is<PutItemRequest>(r => 
            r.TableName == "crm_memory" &&
            r.Item["PK"].S == "CONV#c1" &&
            r.Item["SK"].S == "MSG#2024-03-11T12:00:00Z" &&
            r.Item["text"].S == "hello" &&
            r.Item["hash"].S == "h1"
        ), default), Times.Once);
    }

    [Fact]
    public async Task HandlePersistSummaryAsync_MapsCorrectPayload()
    {
        var connMock = new Mock<INatsConnection>();
        var worker = new TestablePersistenceWorker(_loggerMock.Object, connMock.Object, _dbMock.Object, _configMock.Object, _jsMock.Object);
        var payloadJson = "{\"conversation_id\":\"c1\", \"rolling_summary\":\"new summary\", \"last_message_hash\":\"h1\", \"updated_at\":\"2024-03-11T12:05:00Z\"}";
        var payload = JsonDocument.Parse(payloadJson).RootElement;

        await worker.ExposeHandlePersistSummaryAsync(payload, default);

        _dbMock.Verify(x => x.UpdateItemAsync(It.Is<UpdateItemRequest>(r => 
            r.TableName == "crm_memory" &&
            r.Key["PK"].S == "CONV#c1" &&
            r.Key["SK"].S == "META" &&
            r.AttributeUpdates["rolling_summary"].Value.S == "new summary"
        ), default), Times.Once);
    }

    [Fact]
    public async Task HandlePersistFactsAsync_MapsCorrectPayload()
    {
        var connMock = new Mock<INatsConnection>();
        var worker = new TestablePersistenceWorker(_loggerMock.Object, connMock.Object, _dbMock.Object, _configMock.Object, _jsMock.Object);
        var payloadJson = "{\"conversation_id\":\"c1\", \"facts\": [{\"name\":\"f1\", \"value\":\"v1\", \"confidence\": 0.9}], \"updated_at\":\"2024-03-11T12:00:00Z\"}";
        var payload = JsonDocument.Parse(payloadJson).RootElement;

        await worker.ExposeHandlePersistFactsAsync(payload, default);

        _dbMock.Verify(x => x.PutItemAsync(It.Is<PutItemRequest>(r => 
            r.TableName == "crm_memory" &&
            r.Item["PK"].S == "CONV#c1" &&
            r.Item["SK"].S == "FACT#f1" &&
            r.Item["confidence"].N == "0.9"
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    private class TestablePersistenceWorker : Worker
    {
        public TestablePersistenceWorker(ILogger<Worker> logger, INatsConnection connection, IAmazonDynamoDB db, IConfiguration config, INatsJSContext js) 
            : base(logger, connection, db, config, js) { }

        public Task ExposeHandlePersistMessageAsync(JsonElement payload, CancellationToken ct) 
            => HandlePersistMessageAsync(payload, ct);

        public Task ExposeHandlePersistSummaryAsync(JsonElement payload, CancellationToken ct) 
            => HandlePersistSummaryAsync(payload, ct);

        public Task ExposeHandlePersistFactsAsync(JsonElement payload, CancellationToken ct) 
            => HandlePersistFactsAsync(payload, ct);
    }
}
