using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace Unit;

public class NatsPublisherTests
{
    private readonly Mock<INatsConnection> _connMock;
    private readonly Mock<INatsJSContext> _jsMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly NatsPublisher _publisher;

    public NatsPublisherTests()
    {
        _connMock = new Mock<INatsConnection>();
        _jsMock = new Mock<INatsJSContext>();
        _configMock = new Mock<IConfiguration>();

        _publisher = new NatsPublisher(_connMock.Object, _configMock.Object, _jsMock.Object);
    }

    [Fact]
    public async Task PublishAsync_SendsCorrectSubject()
    {
        // Arrange
        var msg = new NormalizedMessage("conv1", "b1", "c1", SenderType.Customer, "hi", DateTimeOffset.UtcNow, "hash1");

        // Act
        await _publisher.PublishAsync(msg);

        // Assert
        _jsMock.Verify(x => x.PublishAsync(
            It.Is<string>(s => s == "message.received"), 
            It.IsAny<string>(), 
            default, default, default), Times.Once);
    }

    [Fact]
    public async Task PublishSummaryAsync_SendsPersistSubject()
    {
        // Act
        await _publisher.PublishSummaryAsync("conv1", "summary", "hash1");

        // Assert
        _jsMock.Verify(x => x.PublishAsync(
            It.Is<string>(s => s == "persist.summary"), 
            It.IsAny<string>(), 
            default, default, default), Times.Once);
    }
}
