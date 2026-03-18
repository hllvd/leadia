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

// ── Configuration ──────────────────────────────────────────────────────────
builder.Configuration.AddJsonFile("config.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile("config.local.json", optional: true, reloadOnChange: true);
var config = builder.Configuration;

// ── Shared Infrastructure ───────────────────────────────────────────────────
var natsUrl = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222";
builder.Services.AddTransient<INatsConnection>(sp => new NatsConnection(new NatsOpts { Url = natsUrl }));

var dynamoDbEndpoint = Environment.GetEnvironmentVariable("DYNAMODB_ENDPOINT");
var isTest = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST"));
builder.Services.AddSingleton<IAmazonDynamoDB>(sp => 
{
    if (isTest)
    {
        var cfg = new AmazonDynamoDBConfig();
        cfg.ServiceURL = dynamoDbEndpoint ?? "http://dynamodb-local:8000";
        return new AmazonDynamoDBClient(new Amazon.Runtime.AnonymousAWSCredentials(), cfg);
    }
    else
    {
        var accessKey = config["DynamoDB:AccessKey"];
        var secretKey = config["DynamoDB:SecretKey"];
        var region = config["DynamoDB:Region"];
        var cfg = new AmazonDynamoDBConfig();
        if (!string.IsNullOrEmpty(region))
            cfg.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
        return new AmazonDynamoDBClient(accessKey, secretKey, cfg);
    }
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
