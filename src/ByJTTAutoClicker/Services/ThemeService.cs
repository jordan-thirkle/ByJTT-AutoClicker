using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using ByJTTAutoClicker.Interop;

namespace ByJTTAutoClicker.Services;

public sealed class ThemeService
{
    public void ApplyTheme(ResourceDictionary resources)
    {
        if (SystemParameters.HighContrast)
        {
            ApplyHighContrastTheme(resources);
            return;
        }

        var dark = IsAppsDarkMode();
        var accent = GetAccentColor() ?? (dark ? Color.FromRgb(0x60, 0xCD, 0xFF) : Color.FromRgb(0x00, 0x5F, 0xB8));

        Color bg, surface, surfaceAlt, border, text, textMuted, subtle, successBg, successFg, dangerBg, dangerFg, warnBg, warnFg, primaryFg;
        if (dark)
        {
            bg = Color.FromRgb(0x1C, 0x1C, 0x1C);
            surface = Color.FromRgb(0x24, 0x24, 0x24);
            surfaceAlt = Color.FromRgb(0x2C, 0x2C, 0x2C);
            border = Color.FromRgb(0x3A, 0x3A, 0x3A);
            text = Color.FromRgb(0xF5, 0xF5, 0xF5);
            textMuted = Color.FromRgb(0xC6, 0xC6, 0xC6);
            subtle = Color.FromRgb(0x20, 0x20, 0x20);
            successBg = Color.FromRgb(0x10, 0x3B, 0x28);
            successFg = Color.FromRgb(0x6E, 0xEB, 0xA0);
            dangerBg = Color.FromRgb(0x44, 0x1C, 0x1C);
            dangerFg = Color.FromRgb(0xFF, 0x99, 0xA4);
            warnBg = Color.FromRgb(0x43, 0x2E, 0x12);
            warnFg = Color.FromRgb(0xFF, 0xD2, 0x8C);
            primaryFg = GetContrastingForeground(accent);
        }
        else
        {
            bg = Color.FromRgb(0xF3, 0xF3, 0xF3);
            surface = Color.FromRgb(0xFF, 0xFF, 0xFF);
            surfaceAlt = Color.FromRgb(0xFA, 0xFA, 0xFA);
            border = Color.FromRgb(0xDD, 0xDD, 0xDD);
            text = Color.FromRgb(0x1A, 0x1A, 0x1A);
            textMuted = Color.FromRgb(0x5C, 0x5C, 0x5C);
            subtle = Color.FromRgb(0xF7, 0xF7, 0xF7);
            successBg = Color.FromRgb(0xDF, 0xF6, 0xE8);
            successFg = Color.FromRgb(0x0F, 0x5C, 0x2E);
            dangerBg = Color.FromRgb(0xFD, 0xE7, 0xE9);
            dangerFg = Color.FromRgb(0x8A, 0x1C, 0x1C);
            warnBg = Color.FromRgb(0xFF, 0xF4, 0xCE);
            warnFg = Color.FromRgb(0x6B, 0x4F, 0x00);
            primaryFg = GetContrastingForeground(accent);
        }

        ApplyPalette(resources, bg, surface, surfaceAlt, border, text, textMuted, subtle, accent, primaryFg, successBg, successFg, dangerBg, dangerFg, warnBg, warnFg, false);
    }

    public void ApplyHighContrastTheme(ResourceDictionary resources)
    {
        // Use system colors to honor the user's selected High Contrast theme.
        resources["App.BackgroundBrush"] = SystemColors.WindowBrush;
        resources["App.SurfaceBrush"] = SystemColors.WindowBrush;
        resources["App.SurfaceAltBrush"] = SystemColors.ControlBrush;
        resources["App.BorderBrush"] = SystemColors.WindowTextBrush;
        resources["App.TextBrush"] = SystemColors.WindowTextBrush;
        resources["App.TextMutedBrush"] = SystemColors.GrayTextBrush;
        resources["App.SubtleBrush"] = SystemColors.ControlBrush;
        resources["App.AccentBrush"] = SystemColors.HighlightBrush;
        resources["App.AccentMutedBrush"] = SystemColors.ControlBrush;
        resources["App.OnAccentBrush"] = SystemColors.HighlightTextBrush;
        resources["App.SuccessBgBrush"] = SystemColors.WindowBrush;
        resources["App.SuccessFgBrush"] = SystemColors.WindowTextBrush;
        resources["App.DangerBgBrush"] = SystemColors.WindowBrush;
        resources["App.DangerFgBrush"] = SystemColors.WindowTextBrush;
        resources["App.WarnBgBrush"] = SystemColors.WindowBrush;
        resources["App.WarnFgBrush"] = SystemColors.WindowTextBrush;
        resources["App.FocusBrush"] = SystemColors.HighlightBrush;
        resources["App.IsDark"] = false;
        resources["App.IsHighContrast"] = true;
    }

    private static void ApplyPalette(ResourceDictionary resources,
        Color bg, Color surface, Color surfaceAlt, Color border, Color text, Color textMuted, Color subtle,
        Color accent, Color onAccent, Color successBg, Color successFg, Color dangerBg, Color dangerFg, Color warnBg, Color warnFg, bool hc)
    {
        SetBrush(resources, "App.BackgroundBrush", bg);
        SetBrush(resources, "App.SurfaceBrush", surface);
        SetBrush(resources, "App.SurfaceAltBrush", surfaceAlt);
        SetBrush(resources, "App.BorderBrush", border);
        SetBrush(resources, "App.TextBrush", text);
        SetBrush(resources, "App.TextMutedBrush", textMuted);
        SetBrush(resources, "App.SubtleBrush", subtle);
        SetBrush(resources, "App.AccentBrush", accent);
        SetBrush(resources, "App.AccentMutedBrush", Blend(accent, bg, 0.20));
        SetBrush(resources, "App.OnAccentBrush", onAccent);
        SetBrush(resources, "App.SuccessBgBrush", successBg);
        SetBrush(resources, "App.SuccessFgBrush", successFg);
        SetBrush(resources, "App.DangerBgBrush", dangerBg);
        SetBrush(resources, "App.DangerFgBrush", dangerFg);
        SetBrush(resources, "App.WarnBgBrush", warnBg);
        SetBrush(resources, "App.WarnFgBrush", warnFg);
        SetBrush(resources, "App.FocusBrush", accent);
        resources["App.IsDark"] = IsAppsDarkMode();
        resources["App.IsHighContrast"] = hc;
    }

    public static bool IsAppsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            if (val is int i) return i == 0;
        }
        catch { }
        return false;
    }

    public static Color? GetAccentColor()
    {
        try
        {
            if (NativeMethods.DwmGetColorizationColor(out var colorization, out _) == 0)
            {
                var a = (byte)((colorization >> 24) & 0xFF);
                var r = (byte)((colorization >> 16) & 0xFF);
                var g = (byte)((colorization >> 8) & 0xFF);
                var b = (byte)(colorization & 0xFF);
                if (a == 0) a = 0xFF;
                return Color.FromArgb(a, r, g, b);
            }
        }
        catch { }
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\\Microsoft\\Windows\\DWM");
            var val = key?.GetValue("AccentColor");
            if (val is int i)
            {
                var bytes = BitConverter.GetBytes(i);
                return Color.FromArgb(0xFF, bytes[0], bytes[1], bytes[2]); // ABGR
            }
        }
        catch { }
        return null;
    }

    private static void SetBrush(ResourceDictionary resources, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze) brush.Freeze();
        resources[key] = brush;
    }

    private static Color Blend(Color c1, Color c2, double amount)
    {
        byte Lerp(byte a, byte b) => (byte)Math.Clamp(a + (b - a) * amount, 0, 255);
        return Color.FromRgb(Lerp(c1.R, c2.R), Lerp(c1.G, c2.G), Lerp(c1.B, c2.B));
    }

    private static Color GetContrastingForeground(Color background)
    {
        double Srgb(double c) { c /= 255.0; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        var l = 0.2126 * Srgb(background.R) + 0.7152 * Srgb(background.G) + 0.0722 * Srgb(background.B);
        return l > 0.45 ? Color.FromRgb(0x10, 0x10, 0x10) : Colors.White;
    }
}
