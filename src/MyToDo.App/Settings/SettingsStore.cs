using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MyToDo.App.Settings;

public sealed class SettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.Strict
    };

    public SettingsStore(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath)) throw new ArgumentException("Settings directory is required.", nameof(directoryPath));
        var directory = Path.GetFullPath(directoryPath);
        Directory.CreateDirectory(directory);
        FilePath = Path.Combine(directory, "settings.json");
    }

    public string FilePath { get; }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath)) return AppSettings.Default;
        try
        {
            await using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            return (settings ?? AppSettings.Default).Normalize();
        }
        catch (JsonException) { return AppSettings.Default; }
        catch (IOException) { return AppSettings.Default; }
        catch (UnauthorizedAccessException) { return AppSettings.Default; }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = settings.Normalize();
        var temporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }

            if (File.Exists(FilePath))
            {
                File.Replace(temporaryPath, FilePath, null);
            }
            else
            {
                File.Move(temporaryPath, FilePath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
