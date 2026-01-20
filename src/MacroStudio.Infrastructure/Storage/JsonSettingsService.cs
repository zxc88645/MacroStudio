using MacroStudio.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MacroStudio.Infrastructure.Storage;

public sealed class JsonSettingsService : ISettingsService
{
    private readonly ILogger<JsonSettingsService> _logger;
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JsonSettingsService(ILogger<JsonSettingsService> logger, string? settingsPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MacroStudio",
            "settings.json");

        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
    }

    public async Task<AppSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return AppSettings.Default();

            var json = await File.ReadAllTextAsync(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _options) ?? AppSettings.Default();
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings; using defaults");
            return AppSettings.Default();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        var json = JsonSerializer.Serialize(settings, _options);
        await File.WriteAllTextAsync(_settingsPath, json);
    }
}

