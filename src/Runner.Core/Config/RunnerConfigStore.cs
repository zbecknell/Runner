using System.Text.Json;
using System.Text.Json.Serialization;
using Runner.Core.Runners;

namespace Runner.Core.Config;

public sealed class RunnerConfigStore
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public RunnerConfigStore(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }

    public static string GetDefaultConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config");
        }

        return Path.Combine(appData, "Runner", "runner-settings.json");
    }

    public async Task<RunnerConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            return new RunnerConfig();
        }

        await using var stream = File.OpenRead(FilePath);
        var config = await JsonSerializer.DeserializeAsync<RunnerConfig>(stream, _jsonOptions, cancellationToken);

        config ??= new RunnerConfig();
        config.Runners ??= [];

        foreach (var runner in config.Runners)
        {
            runner.EnsureId();
            runner.CustomCommands ??= new RunnerCommandSet();
        }

        return config;
    }

    public async Task SaveAsync(RunnerConfig config, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(FilePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        foreach (var runner in config.Runners)
        {
            runner.EnsureId();
            runner.CustomCommands ??= new RunnerCommandSet();
        }

        var tempPath = $"{FilePath}.{Guid.NewGuid():N}.tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, config, _jsonOptions, cancellationToken);
        }

        File.Move(tempPath, FilePath, overwrite: true);
    }
}
