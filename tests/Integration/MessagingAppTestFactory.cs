using Application.Interfaces;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NATS.Client.Core;

namespace Integration;

public class MessagingAppTestFactory : WebApplicationFactory<Program>
{
    public Mock<IMessagePublisher> MessagePublisherMock { get; } = new();
    public Mock<INatsConnection> NatsConnMock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("INTEGRATION_TEST", "true");
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove existing DB context
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // Add InMemory DB
            services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("TestDb"));

            // Remove real NATS
            var natsDesc = services.SingleOrDefault(d => d.ServiceType == typeof(INatsConnection));
            if (natsDesc != null) services.Remove(natsDesc);
            services.AddSingleton(NatsConnMock.Object);

            // Mock Publisher
            var pubDesc = services.SingleOrDefault(d => d.ServiceType == typeof(IMessagePublisher));
            if (pubDesc != null) services.Remove(pubDesc);
            services.AddSingleton(MessagePublisherMock.Object);
        });
    }

    public async Task SeedBotAsync(string botNumber)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.Bots.AnyAsync(b => b.BotNumber == botNumber))
        {
            db.Bots.Add(new Domain.Entities.Bot { Id = Guid.NewGuid().ToString(), BotNumber = botNumber, BotName = "Test Bot", BotType = BotType.GenericAi, IsActive = true });
            await db.SaveChangesAsync();
        }
    }
}
