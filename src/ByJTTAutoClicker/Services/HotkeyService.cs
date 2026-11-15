using ByJTTAutoClicker.Interop;

namespace ByJTTAutoClicker.Services;

public sealed class HotkeyService : IDisposable
{
    public const int HotkeyId = 0xB100;

    private static readonly Dictionary<string, uint> HotkeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["F1"] = 0x70, ["F2"] = 0x71, ["F3"] = 0x72, ["F4"] = 0x73,
        ["F5"] = 0x74, ["F6"] = 0x75, ["F7"] = 0x76, ["F8"] = 0x77,
        ["F9"] = 0x78, ["F10"] = 0x79, ["F11"] = 0x7A, ["F12"] = 0x7B
    };

    public static IReadOnlyList<string> SupportedHotkeys { get; } = new[] { "F1","F2","F3","F4","F5","F6","F7","F8","F9","F10","F11","F12" };

    private IntPtr _hwnd;
    private bool _registered;

    public string? CurrentHotkey { get; private set; }

    public void Attach(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    public static bool IsSupportedHotkey(string? hotkey)
        => !string.IsNullOrWhiteSpace(hotkey) && HotkeyMap.ContainsKey(hotkey.Trim());

    public static uint GetVirtualKey(string hotkey)
        => HotkeyMap[hotkey.Trim().ToUpperInvariant()];

    public bool Register(string hotkey, out string? error)
    {
        error = null;
        hotkey = (hotkey ?? "F6").Trim().ToUpperInvariant();
        if (!IsSupportedHotkey(hotkey))
        {
            error = "Unsupported hotkey.";
            return false;
        }

        Unregister();
        if (_hwnd == IntPtr.Zero)
        {
            error = "Window handle is not ready.";
            return false;
        }

        if (!NativeMethods.RegisterHotKey(_hwnd, HotkeyId, NativeMethods.MOD_NOREPEAT, GetVirtualKey(hotkey)))
        {
            error = $"Could not register {hotkey} as a global hotkey. It may be in use by another app.";
            _registered = false;
            CurrentHotkey = null;
            return false;
        }

        _registered = true;
        CurrentHotkey = hotkey;
        return true;
    }

    public void Unregister()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            try { NativeMethods.UnregisterHotKey(_hwnd, HotkeyId); } catch { }
        }
        _registered = false;
        CurrentHotkey = null;
    }

    public void Dispose() => Unregister();
}
