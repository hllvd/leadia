using Amazon.DynamoDBv2;
using PersistenceWorker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NATS.Client.Core;

var builder = Host.CreateApplicationBuilder(args);

// ── Shared Infrastructure ───────────────────────────────────────────────────
builder.Services.AddSingleton<INatsConnection>(sp => new NatsConnection());
builder.Services.AddSingleton<IAmazonDynamoDB>(sp => new AmazonDynamoDBClient());

// ── Background Worker ────────────────────────────────────────────────────────
builder.Services.AddHostedService<Worker>();

using IHost host = builder.Build();
host.Run();
