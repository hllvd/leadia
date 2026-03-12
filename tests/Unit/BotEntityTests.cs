using Application.DTOs;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Moq;
using Xunit;

namespace Unit;

public class BotEntityTests
{
    [Fact]
    public void Bot_CanBeInitialized_WithNewFields()
    {
        var bot = new Bot
        {
            BotName = "Test Bot",
            BotNumber = "+5511999990000",
            Prompt = "You are a helpful assistant.",
            Soul = "Friendly and concise.",
            IsAgent = true,
            Description = "A test agent bot."
        };

        Assert.Equal("Test Bot", bot.BotName);
        Assert.Equal("+5511999990000", bot.BotNumber);
        Assert.Equal("You are a helpful assistant.", bot.Prompt);
        Assert.Equal("Friendly and concise.", bot.Soul);
        Assert.True(bot.IsAgent);
        Assert.Equal("A test agent bot.", bot.Description);
        Assert.True(bot.IsActive);
    }

    [Fact]
    public async Task BotService_CreateAsync_MapsCorrectly()
    {
        var repoMock = new Mock<IBotRepository>();
        var service = new BotService(repoMock.Object);
        var dto = new CreateBotDto(
            "+5511999991111",
            "Service Bot",
            "Primary prompt",
            "Helpful soul",
            true,
            "Service description"
        );

        var result = await service.CreateAsync(dto);

        Assert.Equal(dto.BotName, result.BotName);
        Assert.Equal(dto.BotNumber, result.BotNumber);
        Assert.Equal(dto.Prompt, result.Prompt);
        Assert.Equal(dto.Soul, result.Soul);
        Assert.Equal(dto.IsAgent, result.IsAgent);
        Assert.Equal(dto.Description, result.Description);
        
        repoMock.Verify(r => r.AddAsync(It.Is<Bot>(b => 
            b.BotName == dto.BotName && 
            b.Prompt == dto.Prompt &&
            b.IsAgent == dto.IsAgent
        ), It.IsAny<CancellationToken>()), Times.Once);
    }
}
