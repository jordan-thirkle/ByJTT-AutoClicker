using System.IO;
using System.Text.Json;
using ByJTTAutoClicker.Models;

namespace ByJTTAutoClicker.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string SettingsDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ByJTT", "AutoClicker");

    public string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public AppSettings Load()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    return Normalize(loaded);
                }
            }
        }
        catch
        {
            // Fall through to defaults.
        }

        // Migration from legacy app-local settings file (v3 and earlier).
        try
        {
            var legacy = Path.Combine(AppContext.BaseDirectory, "settings.json");
            if (File.Exists(legacy))
            {
                var json = File.ReadAllText(legacy);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    var normalized = Normalize(loaded);
                    Save(normalized);
                    return normalized;
                }
            }
        }
        catch
        {
            // Ignore migration errors.
        }

        return Normalize(new AppSettings());
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(Normalize(settings), JsonOptions);
            var tmp = SettingsPath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, SettingsPath, overwrite: true);
        }
        catch
        {
            // Persistence errors should not crash the app.
        }
    }

    public static AppSettings Normalize(AppSettings s)
    {
        s ??= new AppSettings();
        s.IntervalMs = Math.Clamp(s.IntervalMs <= 0 ? 100 : s.IntervalMs, 1, 3_600_000);
        s.JitterMs = Math.Clamp(s.JitterMs < 0 ? 0 : s.JitterMs, 0, 3_600_000);
        s.Count = Math.Clamp(s.Count <= 0 ? 100 : s.Count, 1, 100_000_000);
        s.StartDelaySeconds = Math.Clamp(double.IsFinite(s.StartDelaySeconds) ? s.StartDelaySeconds : 0, 0, 60);
        s.FixedX = Math.Clamp(s.FixedX, -100_000, 100_000);
        s.FixedY = Math.Clamp(s.FixedY, -100_000, 100_000);
        if (string.IsNullOrWhiteSpace(s.Hotkey)) s.Hotkey = "F6";
        s.Hotkey = s.Hotkey.Trim().ToUpperInvariant();
        if (!HotkeyService.IsSupportedHotkey(s.Hotkey)) s.Hotkey = "F6";
        if (!Enum.IsDefined(s.Button)) s.Button = ClickButton.Left;
        if (!Enum.IsDefined(s.ClickType)) s.ClickType = ClickType.Single;
        if (!Enum.IsDefined(s.RepeatMode)) s.RepeatMode = RepeatMode.UntilStopped;
        if (!Enum.IsDefined(s.PositionMode)) s.PositionMode = PositionMode.Cursor;
        return s;
    }

    public static string? Validate(AppSettings s)
    {
        if (s.IntervalMs < 1 || s.IntervalMs > 3_600_000) return "Interval must be between 1 and 3,600,000 ms.";
        if (s.JitterMs < 0 || s.JitterMs > 3_600_000) return "Jitter must be between 0 and 3,600,000 ms.";
        if (s.JitterMs > s.IntervalMs * 10) return "Jitter is too large compared to the interval.";
        if (s.StartDelaySeconds < 0 || s.StartDelaySeconds > 60) return "Start delay must be between 0 and 60 seconds.";
        if (s.RepeatMode == RepeatMode.Count && (s.Count < 1 || s.Count > 100_000_000)) return "Click count must be between 1 and 100,000,000.";
        if (s.PositionMode == PositionMode.Fixed && (s.FixedX < -100_000 || s.FixedX > 100_000 || s.FixedY < -100_000 || s.FixedY > 100_000))
            return "Fixed coordinates must be between -100,000 and 100,000.";
        if (!HotkeyService.IsSupportedHotkey(s.Hotkey)) return "Choose a supported hotkey (F1-F12).";
        return null;
    }
}
