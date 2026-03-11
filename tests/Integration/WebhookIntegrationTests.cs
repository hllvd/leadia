using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace Integration;

/// <summary>
/// Messaging-agnostic test factory that replaces SQLite with InMemory EF Core.
/// </summary>
public class MessagingAppTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services
                .SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(opt =>
                opt.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        });

        builder.UseEnvironment("Test");
    }

    public async Task SeedBotAsync(string botNumber)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        if (!await db.Bots.AnyAsync(b => b.BotNumber == botNumber))
        {
            db.Bots.Add(new Bot
            {
                Id = Guid.NewGuid().ToString(),
                BotNumber = botNumber,
                BotName = "Test Bot",
                BotType = BotType.PersonalFinance,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }
    }
}

public record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);
public record WebhookTestRequest(string From, string Message);

public class WebhookIntegrationTests(MessagingAppTestFactory factory)
    : IClassFixture<MessagingAppTestFactory>
{
    private const string WebhookSecret = "CHANGE_THIS_WEBHOOK_SECRET";
    private const string TestBotNumber = "4798913312";

    private static string ComputeSignature(string body, string secret)
    {
        var key   = Encoding.UTF8.GetBytes(secret);
        var data  = Encoding.UTF8.GetBytes(body);
        var hash  = HMACSHA256.HashData(key, data);
        return "sha256=" + Convert.ToHexStringLower(hash);
    }

    [Fact]
    public async Task Webhook_WithValidSignature_ReturnsOk()
    {
        await factory.SeedBotAsync(TestBotNumber);
        var client  = factory.CreateClient();
        var payload = new WebhookTestRequest("47839948", "Hello bot");
        var json    = System.Text.Json.JsonSerializer.Serialize(payload);
        var sig     = ComputeSignature(json, WebhookSecret);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/webhook/{TestBotNumber}");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Hub-Signature-256", sig);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_WithInvalidSignature_ReturnsUnauthorized()
    {
        await factory.SeedBotAsync(TestBotNumber);
        var client  = factory.CreateClient();
        var payload = new WebhookTestRequest("47839948", "Hello bot");
        var json    = System.Text.Json.JsonSerializer.Serialize(payload);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/webhook/{TestBotNumber}");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Hub-Signature-256", "sha256=invalid");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_DuplicateMessage_ReturnsConflict()
    {
        await factory.SeedBotAsync(TestBotNumber);
        var client  = factory.CreateClient();
        var payload = new WebhookTestRequest("47839948", "Same message");
        var json    = System.Text.Json.JsonSerializer.Serialize(payload);
        var sig     = ComputeSignature(json, WebhookSecret);

        // First attempt
        var req1 = new HttpRequestMessage(HttpMethod.Post, $"/api/webhook/{TestBotNumber}");
        req1.Content = new StringContent(json, Encoding.UTF8, "application/json");
        req1.Headers.Add("X-Hub-Signature-256", sig);
        var res1 = await client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

        // Second attempt (duplicate hash)
        var req2 = new HttpRequestMessage(HttpMethod.Post, $"/api/webhook/{TestBotNumber}");
        req2.Content = new StringContent(json, Encoding.UTF8, "application/json");
        req2.Headers.Add("X-Hub-Signature-256", sig);
        var res2 = await client.SendAsync(req2);

        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }
}
