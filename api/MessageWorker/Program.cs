using Amazon.DynamoDBv2;
using Application.Interfaces;
using Application.Services;
using Infrastructure.Repositories;
using Infrastructure.Services;
using MessageWorker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using NATS.Client.Core;

var builder = Host.CreateApplicationBuilder(args);

// ── Shared Infrastructure ───────────────────────────────────────────────────
var natsUrl = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222";
builder.Services.AddSingleton<INatsConnection>(sp => new NatsConnection(new NatsOpts { Url = natsUrl }));

var dynamoDbEndpoint = Environment.GetEnvironmentVariable("DYNAMODB_ENDPOINT");
builder.Services.AddSingleton<IAmazonDynamoDB>(sp => 
{
    var config = new AmazonDynamoDBConfig();
    if (!string.IsNullOrEmpty(dynamoDbEndpoint))
    {
        config.ServiceURL = dynamoDbEndpoint;
    }
    return new AmazonDynamoDBClient(config);
});

// ── Repositories & Publishers ────────────────────────────────────────────────
builder.Services.AddScoped<IConversationStateRepository, DynamoDbConversationStateRepository>();
builder.Services.AddScoped<IPersistenceEventPublisher, NatsPublisher>();
builder.Services.AddScoped<IMessagePublisher, NatsPublisher>();

// ── Application Services ─────────────────────────────────────────────────────
builder.Services.AddScoped<ConversationStateService>();
builder.Services.AddHttpClient<ILlmService, LlmService>();

// ── Background Worker ────────────────────────────────────────────────────────
builder.Services.AddHostedService<Worker>();

using IHost host = builder.Build();
host.Run();
