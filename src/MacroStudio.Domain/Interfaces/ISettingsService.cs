namespace MacroStudio.Domain.Interfaces;

/// <summary>
/// Domain service interface for application configuration persistence.
/// </summary>
public interface ISettingsService
{
    Task<AppSettings> LoadAsync();
    Task SaveAsync(AppSettings settings);
}

/// <summary>
/// Application settings persisted to disk.
/// </summary>
public class AppSettings
{
    public double DefaultSpeedMultiplier { get; set; } = 1.0;
    public bool ShowCountdown { get; set; } = true;
    public double CountdownSeconds { get; set; } = 3.0;

    public ExecutionLimits ExecutionLimits { get; set; } = ExecutionLimits.Default();

    public static AppSettings Default() => new();
}

