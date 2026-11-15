using Microsoft.Win32;
using System.Windows;
using System.Windows.Interop;
using ByJTTAutoClicker.Interop;
using ByJTTAutoClicker.Services;

namespace ByJTTAutoClicker;

public partial class App : Application
{
    public ThemeService ThemeService { get; } = new();

    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            try
            {
                MessageBox.Show(
                    "An unexpected error occurred, but the app will try to continue.\n\n" + e.Exception.Message,
                    "ByJTT AutoClicker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                e.Handled = true;
            }
            catch { }
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplyThemeSafe();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        base.OnExit(e);
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Color)
        {
            Dispatcher.BeginInvoke(ApplyThemeSafe);
        }
    }

    public void ApplyThemeSafe()
    {
        try
        {
            ThemeService.ApplyTheme(Resources);
            foreach (Window w in Windows)
            {
                if (Resources["App.BackgroundBrush"] is System.Windows.Media.Brush bg)
                    w.Background = bg;
                if (Resources["App.TextBrush"] is System.Windows.Media.Brush fg)
                    w.Foreground = fg;
                TryApplySystemTitleBar(w);
            }
        }
        catch
        {
            // Theme application must not crash the app.
        }
    }

    private static void TryApplySystemTitleBar(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                window.SourceInitialized += (_, _) => TryApplySystemTitleBar(window);
                return;
            }

            var highContrast = SystemParameters.HighContrast;
            var dark = !highContrast && ThemeService.IsAppsDarkMode();
            var useDark = dark ? 1 : 0;
            _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDark, sizeof(int));

            // Best-effort Windows 11 system backdrop for a native, modern feel.
            if (!highContrast)
            {
                var backdrop = NativeMethods.DWMSBT_MAINWINDOW;
                _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
                var mica = 1;
                _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_MICA_EFFECT, ref mica, sizeof(int));
            }
        }
        catch
        {
            // Best effort: window chrome theming is non-critical.
        }
    }
}
