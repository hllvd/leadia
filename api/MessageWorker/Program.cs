using Amazon.DynamoDBv2;
using Application.Interfaces;
using Application.Services;
using Infrastructure.BotEngine;
using Infrastructure.Repositories;
using Infrastructure.Services;
using MessageWorker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NATS.Client.Core;

var builder = Host.CreateApplicationBuilder(args);

// ── Shared Infrastructure ───────────────────────────────────────────────────
builder.Services.AddSingleton<INatsConnection>(sp => new NatsConnection());
builder.Services.AddSingleton<IAmazonDynamoDB>(sp => new AmazonDynamoDBClient());

// ── Repositories & Publishers ────────────────────────────────────────────────
builder.Services.AddScoped<IConversationStateRepository, DynamoDbConversationStateRepository>();
builder.Services.AddScoped<IPersistenceEventPublisher, NatsPublisher>();
builder.Services.AddScoped<IMessagePublisher, NatsPublisher>();

// ── Application Services ─────────────────────────────────────────────────────
builder.Services.AddScoped<ConversationStateService>();
builder.Services.AddHttpClient<ILlmService, LlmService>();

// ── Bot Strategies ──────────────────────────────────────────────────────────
builder.Services.AddScoped<IBotStrategy, PersonalFinanceBotStrategy>();
builder.Services.AddScoped<IBotStrategy, MeiBotStrategy>();
builder.Services.AddScoped<IBotStrategy, AiBotStrategy>();
builder.Services.AddScoped<IBotStrategyFactory, BotStrategyFactory>();

// ── Background Worker ────────────────────────────────────────────────────────
builder.Services.AddHostedService<Worker>();

using IHost host = builder.Build();
host.Run();
