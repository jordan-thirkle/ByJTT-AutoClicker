using System.Text.Json.Serialization;

namespace ByJTTAutoClicker.Models;

public enum ClickButton
{
    Left,
    Right,
    Middle
}

public enum ClickType
{
    Single,
    Double
}

public enum RepeatMode
{
    UntilStopped,
    Count
}

public enum PositionMode
{
    Cursor,
    Fixed
}

public sealed class AppSettings
{
    public int IntervalMs { get; set; } = 100;
    public int JitterMs { get; set; } = 0;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ClickButton Button { get; set; } = ClickButton.Left;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ClickType ClickType { get; set; } = ClickType.Single;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RepeatMode RepeatMode { get; set; } = RepeatMode.UntilStopped;
    public int Count { get; set; } = 100;
    public double StartDelaySeconds { get; set; } = 0;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PositionMode PositionMode { get; set; } = PositionMode.Cursor;
    public int FixedX { get; set; } = 0;
    public int FixedY { get; set; } = 0;
    public string Hotkey { get; set; } = "F6";
    public bool AlwaysOnTop { get; set; } = false;
    public bool FailsafeEnabled { get; set; } = true;
    public bool ShowStartConfirmation { get; set; } = true;
    public string? WindowBounds { get; set; }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}

public sealed class ClickConfiguration
{
    public int IntervalMs { get; init; }
    public int JitterMs { get; init; }
    public ClickButton Button { get; init; }
    public ClickType ClickType { get; init; }
    public RepeatMode RepeatMode { get; init; }
    public int MaxClicks { get; init; }
    public double StartDelaySeconds { get; init; }
    public PositionMode PositionMode { get; init; }
    public int FixedX { get; init; }
    public int FixedY { get; init; }
    public bool FailsafeEnabled { get; init; }
}

public enum ClickerState
{
    Stopped,
    Starting,
    Running
}

public sealed record ClickerStatus(
    ClickerState State,
    long ClicksCompleted,
    long TargetClicks,
    double ElapsedSeconds,
    string Message);

public enum StopReason
{
    User,
    Completed,
    Failsafe,
    HotkeyConflict,
    Error,
    Cancelled
}
