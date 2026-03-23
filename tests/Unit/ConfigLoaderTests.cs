using Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using System.IO;
using Xunit;

namespace Unit;

public class ConfigLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void Apply_FindsConfigsInParentDirectory_AndMergesThem()
    {
        // Arrange
        var rootDir = Path.Combine(_tempDir, "root");
        var subDir = Path.Combine(rootDir, "sub", "project");
        Directory.CreateDirectory(subDir);

        File.WriteAllText(Path.Combine(rootDir, "config.json"), "{\"App\":{\"Name\":\"BaseApp\", \"Version\":\"1.0\"}}");
        File.WriteAllText(Path.Combine(rootDir, "config.local.json"), "{\"App\":{\"Name\":\"LocalApp\"}}");

        var builder = new ConfigurationBuilder();

        // Act
        ConfigLoader.Apply(builder, subDir);
        var config = builder.Build();

        // Assert
        // The local should override the base configuration for Name
        Assert.Equal("LocalApp", config["App:Name"]);
        // The base properties not overriden should still exist
        Assert.Equal("1.0", config["App:Version"]);
    }

    [Fact]
    public void Apply_GracefullyHandlesMissingLocalJson()
    {
        // Arrange
        var rootDir = Path.Combine(_tempDir, "root2");
        Directory.CreateDirectory(rootDir);

        File.WriteAllText(Path.Combine(rootDir, "config.json"), "{\"App\":{\"Name\":\"BaseApp\"}}");

        var builder = new ConfigurationBuilder();

        // Act
        ConfigLoader.Apply(builder, rootDir);
        var config = builder.Build();

        // Assert
        Assert.Equal("BaseApp", config["App:Name"]);
    }
}
