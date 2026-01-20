using MacroStudio.Domain.ValueObjects;

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

    // Recording control hotkeys (global). Defaults: F9 / F11 / F12.
    public HotkeyDefinition? RecordingStartHotkey { get; set; }
    public HotkeyDefinition? RecordingPauseHotkey { get; set; }
    public HotkeyDefinition? RecordingStopHotkey { get; set; }

    public static AppSettings Default()
    {
        var s = new AppSettings();
        s.EnsureDefaults();
        return s;
    }

    /// <summary>
    /// Ensures any missing settings values are populated with sensible defaults.
    /// This provides backward compatibility for older settings.json versions.
    /// </summary>
    public void EnsureDefaults()
    {
        // Legacy settings.json may not have these fields.
        RecordingStartHotkey ??= HotkeyDefinition.Create("Recording Start", HotkeyModifiers.None, VirtualKey.VK_F9, HotkeyTriggerMode.Once);
        RecordingPauseHotkey ??= HotkeyDefinition.Create("Recording Pause", HotkeyModifiers.None, VirtualKey.VK_F11, HotkeyTriggerMode.Once);
        RecordingStopHotkey ??= HotkeyDefinition.Create("Recording Stop", HotkeyModifiers.None, VirtualKey.VK_F12, HotkeyTriggerMode.Once);

        ExecutionLimits ??= ExecutionLimits.Default();
        if (DefaultSpeedMultiplier <= 0) DefaultSpeedMultiplier = 1.0;
        if (CountdownSeconds <= 0) CountdownSeconds = 3.0;
    }
}

