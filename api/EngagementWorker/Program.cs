using System;
using Amazon.DynamoDBv2;
using Application.Interfaces;
using Application.Services;
using EngagementWorker;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NATS.Client.Core;
using Amazon;
using Amazon.Runtime;

var builder = Host.CreateApplicationBuilder(args);

// ── Configuration ──────────────────────────────────────────────────────────
Infrastructure.Configuration.ConfigLoader.Apply(builder.Configuration);
var config = builder.Configuration;

// ── NATS Connection ─────────────────────────────────────────────────────────
var natsUrl = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222";
builder.Services.AddTransient<INatsConnection>(sp => new NatsConnection(new NatsOpts { Url = natsUrl }));

// ── DynamoDB ────────────────────────────────────────────────────────────────
var dynamoDbEndpoint = Environment.GetEnvironmentVariable("DYNAMODB_ENDPOINT");
var isTest = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST"));
builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    if (isTest)
    {
        var cfg = new AmazonDynamoDBConfig { ServiceURL = dynamoDbEndpoint ?? "http://dynamodb-local:8000" };
        return new AmazonDynamoDBClient(new Amazon.Runtime.AnonymousAWSCredentials(), cfg);
    }
    var accessKey = config["DynamoDB:AccessKey"];
    var secretKey = config["DynamoDB:SecretKey"];
    var region    = config["DynamoDB:Region"];
    var dbCfg = new AmazonDynamoDBConfig();
    if (!string.IsNullOrEmpty(region))
        dbCfg.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
    return new AmazonDynamoDBClient(accessKey, secretKey, dbCfg);
});

// ── Repositories & Publishers ────────────────────────────────────────────────
builder.Services.AddScoped<IConversationStateRepository, DynamoDbConversationStateRepository>();
builder.Services.AddScoped<IPersistenceEventPublisher, NatsPublisher>();
builder.Services.AddScoped<IMessagePublisher, NatsPublisher>();

// ── Database (EF Core / SQLite) ───────────────────────────────────────────────
var connectionString = config["Database:ConnectionString"] ?? "Data Source=/app/data/contazap.db";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<IRealStateRepository, RealStateRepository>();

// ── Application Services ─────────────────────────────────────────────────────
builder.Services.AddScoped<ConversationStateService>();
builder.Services.AddHttpClient<ILlmService, LlmService>();

// ── Background Worker ────────────────────────────────────────────────────────
builder.Services.AddHostedService<Worker>();

using IHost host = builder.Build();
host.Run();
