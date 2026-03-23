using Microsoft.Extensions.Configuration;
using System.IO;

namespace Infrastructure.Configuration;

public static class ConfigLoader
{
    /// <summary>
    /// Pure function that builds the configuration by mixing config.json and config.local.json.
    /// It searches up the directory tree to find these files.
    /// </summary>
    public static IConfigurationBuilder Apply(IConfigurationBuilder builder, string? basePath = null)
    {
        var directory = basePath ?? Directory.GetCurrentDirectory();
        var configDir = FindNearestConfigDirectory(directory);

        if (configDir != null)
        {
            builder.SetBasePath(configDir)
                   .AddJsonFile("config.json", optional: false, reloadOnChange: true)
                   .AddJsonFile("config.local.json", optional: true, reloadOnChange: true);
        }
        else
        {
            // Fallback for tests or environments where it truly doesn't exist up the tree
            builder.SetBasePath(directory)
                   .AddJsonFile("config.json", optional: true, reloadOnChange: true)
                   .AddJsonFile("config.local.json", optional: true, reloadOnChange: true);
        }

        // Always load environment variables last so they have highest precedence
        builder.AddEnvironmentVariables();
        
        return builder;
    }

    private static string? FindNearestConfigDirectory(string startingPath)
    {
        var currentDir = new DirectoryInfo(startingPath);
        
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "config.json")))
            {
                return currentDir.FullName;
            }
            currentDir = currentDir.Parent;
        }

        return null;
    }
}
