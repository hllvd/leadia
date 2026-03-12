using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NATS.Client.Core;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace Integration;

public record WebhookTestRequest(string From, string Message);

public class WebhookIntegrationTests(MessagingAppTestFactory factory) : IClassFixture<MessagingAppTestFactory>
{
    private const string WebhookSecret = "CHANGE_THIS_WEBHOOK_SECRET";
    private const string TestBotNumber = "4798913312";

    private static string ComputeSignature(string body, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(body);
        var hash = HMACSHA256.HashData(key, data);
        return "sha256=" + Convert.ToHexStringLower(hash);
    }

    [Fact]
    public async Task Webhook_PublishesToNats_AndReturnsOk()
    {
        // Arrange
        await factory.SeedBotAsync(TestBotNumber);
        factory.MessagePublisherMock.Reset();
        
        var client = factory.CreateClient();
        var payload = new WebhookTestRequest("47839948", "Hello bot");
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var sig = ComputeSignature(json, WebhookSecret);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/webhook/{TestBotNumber}");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Hub-Signature-256", sig);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        factory.MessagePublisherMock.Verify(x => x.PublishAsync(
            It.Is<NormalizedMessage>(m => m.CustomerId == "47839948" && m.Text == "Hello bot"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_InvalidSignature_ReturnsUnauthorized()
    {
        await factory.SeedBotAsync(TestBotNumber);
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"api/webhook/{TestBotNumber}")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Hub-Signature-256", "sha256=wrong");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
