using Amazon.DynamoDBv2;
using Api.Endpoints;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NATS.Client.Core;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ──────────────────────────────────────────────────────────
builder.Configuration.AddJsonFile("config.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile("config.local.json", optional: true, reloadOnChange: true);
var config = builder.Configuration;

bool isLocal = args.Contains("--local")
            || Environment.GetEnvironmentVariable("LOCAL_PROTOTYPE") == "true";
bool isTest  = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("INTEGRATION_TEST"));

if (isLocal)
{
    // ── Local prototype: in-memory conversation state, no NATS / DynamoDB ──
    builder.Services.AddSingleton<IConversationStateRepository, InMemoryConversationStateRepository>();
    builder.Services.AddSingleton<IMessagePublisher, NoOpPublisher>();
    builder.Services.AddSingleton<IPersistenceEventPublisher, NoOpPublisher>();

    // Reuse existing SQLite file for EF Core (Bot/User entities used by other endpoints)
    var localDb = config["Database:ConnectionString"] ?? "Data Source=secondbrain.db";
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseSqlite(localDb);
        options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    });
}
else
{
    // ── Production wiring ──────────────────────────────────────────────────
    var natsUrl = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222";
    builder.Services.AddTransient<INatsConnection>(sp => new NatsConnection(new NatsOpts { Url = natsUrl }));

    var dynamoDbEndpoint = Environment.GetEnvironmentVariable("DYNAMODB_ENDPOINT");
    builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
    {
        var cfg = new AmazonDynamoDBConfig();
        if (!string.IsNullOrEmpty(dynamoDbEndpoint))
        {
            cfg.ServiceURL = dynamoDbEndpoint;
            return new AmazonDynamoDBClient(new Amazon.Runtime.AnonymousAWSCredentials(), cfg);
        }
        else
        {
            var accessKey = config["DynamoDB:AccessKey"];
            var secretKey = config["DynamoDB:SecretKey"];
            var region = config["DynamoDB:Region"];
            if (!string.IsNullOrEmpty(region))
                cfg.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
            return new AmazonDynamoDBClient(accessKey, secretKey, cfg);
        }
    });

    if (!isTest)
    {
        var connectionString = config["Database:ConnectionString"] ?? "Data Source=contazap.db";
        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite(connectionString);
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });
    }

    builder.Services.AddScoped<IConversationStateRepository, DynamoDbConversationStateRepository>();
    builder.Services.AddScoped<IMessagePublisher, NatsPublisher>();
    builder.Services.AddScoped<IPersistenceEventPublisher, NatsPublisher>();
}

// ── Repositories (shared) ──────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IBotRepository, BotRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IRealStateRepository, RealStateRepository>();

// ── LLM Service ─────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<ILlmService, LlmService>();

// ── Application services ─────────────────────────────────────────────────────
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<BotService>();
builder.Services.AddScoped<MessageService>();
builder.Services.AddScoped<ConversationStateService>();
builder.Services.AddScoped<RealStateService>();

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtKey = config["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey not configured.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidAudience = config["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// ── CORS ─────────────────────────────────────────────────────────────────────
var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3000", "http://localhost:5173" };
if (allowedOrigins.Length == 0) allowedOrigins = new[] { "http://localhost:3000", "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("ContaZapCors", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────
app.UseCors("ContaZapCors");
app.UseAuthentication();
app.UseAuthorization();

// ── Seed & Migrate ─────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
    }
    await SeedSuperAdmin(db, config);
}

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapGet("/", () => "API is running on port 5050");
app.MapHealthEndpoints();
app.MapAuthEndpoints(config);
app.MapUserEndpoints();
app.MapBotEndpoints();
app.MapWebhookEndpoints(config);
app.MapRealStateEndpoints();
app.MapTestEndpoints();
app.MapChatEndpoints();

app.Run();

// ── Seeder ────────────────────────────────────────────────────────────────────
static async Task SeedSuperAdmin(AppDbContext db, IConfiguration config)
{
    var email = config["SuperAdmin:Email"] ?? "superadmin@test.com";
    var whatsapp = "+5500000000000";
    if (await db.Users.AnyAsync(u => u.Email == email || u.WhatsAppNumber == whatsapp)) return;

    var password = config["SuperAdmin:Password"] ?? "string";
    db.Users.Add(new User
    {
        Name = "Super Admin",
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        WhatsAppNumber = "+5500000000000",
        Role = UserRole.Admin
    });
    await db.SaveChangesAsync();
    Console.WriteLine($"[SEED] Super admin created: {email}");
}
