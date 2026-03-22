using Amazon.S3;
using Amazon.S3.Model;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Infrastructure.Services;

public class S3MessageStorage : IMessageStorage
{
    private readonly IAmazonS3 _s3;
    private readonly ILogger<S3MessageStorage> _logger;
    private readonly string _bucket;
    private readonly bool _enabled;

    public S3MessageStorage(ILogger<S3MessageStorage> logger, IConfiguration config, IAmazonS3? s3 = null)
    {
        _logger = logger;
        var s3Config = config.GetSection("S3");
        
        _bucket = s3Config["Bucket"] ?? "";
        _enabled = !string.IsNullOrEmpty(_bucket) && !string.IsNullOrEmpty(s3Config["AccessKey"]);

        if (!_enabled)
        {
            _logger.LogWarning("S3 Message Storage is DISABLED. Missing configuration in config.json.");
            _s3 = s3 ?? new AmazonS3Client(); // Dummy/default if needed, but won't be used
            return;
        }

        if (s3 != null)
        {
            _s3 = s3;
        }
        else
        {
            var options = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(s3Config["Region"] ?? "us-east-1")
            };
            
            _s3 = new AmazonS3Client(
                s3Config["AccessKey"],
                s3Config["SecretKey"],
                options);
        }
    }

    public async Task StoreMessagesAsync(string conversationId, int part, IEnumerable<NormalizedMessage> messages, CancellationToken ct = default)
    {
        if (!_enabled)
        {
            _logger.LogWarning("Skipping S3 storage for conversation {ConversationId} (Part {Part}) because S3 is disabled.", conversationId, part);
            return;
        }

        var fileName = $"{conversationId}+part-{part}.json";
        var json = JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true });

        try
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucket,
                Key = fileName,
                ContentBody = json,
                ContentType = "application/json"
            };

            await _s3.PutObjectAsync(request, ct);
            _logger.LogInformation("Successfully uploaded {FileName} to S3 bucket {Bucket}.", fileName, _bucket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload {FileName} to S3.", fileName);
            throw;
        }
    }
}
