using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ByJTTAutoClicker.Interop;
using ByJTTAutoClicker.Models;
using ByJTTAutoClicker.Services;

namespace ByJTTAutoClicker;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly HotkeyService _hotkeyService = new();
    private readonly ClickerEngine _engine;
    private readonly DispatcherTimer _cursorTimer;
    private HwndSource? _hwndSource;

    private AppSettings _settings = new();
    private bool _isPickingLocation;
    private bool _suppressHotkeyChange;
    private bool _loaded;

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand HelpCommand { get; }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _engine = new ClickerEngine(Dispatcher);
        _engine.StatusChanged += (_, status) => UpdateStatus(status);
        _engine.Stopped += (_, info) => Dispatcher.BeginInvoke(() => OnStopped(info.Reason, info.Message));

        StartCommand = new RelayCommand(_ => StartStop_Click(this, new RoutedEventArgs()));
        StopCommand = new RelayCommand(_ => StopIfRunning(), _ => _engine.IsBusy);
        SaveCommand = new RelayCommand(_ => SaveSettings(showFeedback: true));
        HelpCommand = new RelayCommand(_ => ShowShortcutsHelp());

        InputBindings.Add(new KeyBinding(StartCommand, Key.S, ModifierKeys.Alt));

        _cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _cursorTimer.Tick += (_, _) => UpdateCursorLabel();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        SourceInitialized += MainWindow_SourceInitialized;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var helper = new WindowInteropHelper(this);
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource?.AddHook(WndProc);
            _hotkeyService.Attach(helper.Handle);
            ApplyHotkeyFromUi(showErrors: true);
        }
        catch
        {
            SetStatusDetail("Could not initialize global hotkey support.");
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            PopulateHotkeys();
            _settings = _settingsService.Load();
            ApplySettingsToUi(_settings);
            ApplyAlwaysOnTop();
            UpdateRateEstimate();
            UpdateModeStates();
            UpdateStatus(new ClickerStatus(ClickerState.Stopped, 0, 0, 0, "Ready. Configure your settings, then press Start or use the hotkey."));
            _cursorTimer.Start();
            _loaded = true;
            IntervalBox.TextChanged += (_, _) => UpdateRateEstimate();
            JitterBox.TextChanged += (_, _) => UpdateRateEstimate();
            // Re-apply theme in case window opened before system brush propagation
            (Application.Current as App)?.ApplyThemeSafe();
        }
        catch (Exception ex)
        {
            SetStatusDetail("Could not load settings: " + ex.Message);
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            _cursorTimer.Stop();
            if (_engine.IsBusy) _engine.Stop(StopReason.User, "Stopped (app closing).");
            SaveSettings(showFeedback: false);
            _hotkeyService.Dispose();
            _hwndSource?.RemoveHook(WndProc);
        }
        catch { }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyService.HotkeyId)
        {
            Dispatcher.BeginInvoke(() => StartStop_Click(this, new RoutedEventArgs()));
            handled = true;
        }
        else if (msg == NativeMethods.WM_SETTINGCHANGE || msg == NativeMethods.WM_THEMECHANGED || msg == NativeMethods.WM_DWMCOLORIZATIONCOLORCHANGED)
        {
            Dispatcher.BeginInvoke(() => (Application.Current as App)?.ApplyThemeSafe());
        }
        return IntPtr.Zero;
    }

    private void PopulateHotkeys()
    {
        _suppressHotkeyChange = true;
        HotkeyCombo.Items.Clear();
        foreach (var hk in HotkeyService.SupportedHotkeys)
            HotkeyCombo.Items.Add(hk);
        HotkeyCombo.SelectedItem = "F6";
        _suppressHotkeyChange = false;
    }

    private void ApplySettingsToUi(AppSettings s)
    {
        IntervalBox.Text = s.IntervalMs.ToString(CultureInfo.InvariantCulture);
        JitterBox.Text = s.JitterMs.ToString(CultureInfo.InvariantCulture);
        CountBox.Text = s.Count.ToString(CultureInfo.InvariantCulture);
        DelayBox.Text = s.StartDelaySeconds.ToString(CultureInfo.InvariantCulture);
        XBox.Text = s.FixedX.ToString(CultureInfo.InvariantCulture);
        YBox.Text = s.FixedY.ToString(CultureInfo.InvariantCulture);
        SelectComboByText(ButtonCombo, s.Button.ToString());
        SelectComboByText(ClickTypeCombo, s.ClickType.ToString());
        RepeatUntilStoppedRadio.IsChecked = s.RepeatMode == RepeatMode.UntilStopped;
        RepeatCountRadio.IsChecked = s.RepeatMode == RepeatMode.Count;
        TargetCursorRadio.IsChecked = s.PositionMode == PositionMode.Cursor;
        TargetFixedRadio.IsChecked = s.PositionMode == PositionMode.Fixed;
        _suppressHotkeyChange = true;
        HotkeyCombo.SelectedItem = HotkeyService.IsSupportedHotkey(s.Hotkey) ? s.Hotkey.ToUpperInvariant() : "F6";
        _suppressHotkeyChange = false;
        AlwaysOnTopCheck.IsChecked = s.AlwaysOnTop;
        FailsafeCheck.IsChecked = s.FailsafeEnabled;
        ConfirmCheck.IsChecked = s.ShowStartConfirmation;

        if (!string.IsNullOrWhiteSpace(s.WindowBounds))
        {
            try
            {
                var parts = s.WindowBounds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 4 &&
                    double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var left) &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var top) &&
                    double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w) &&
                    double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
                {
                    if (w >= MinWidth && h >= MinHeight)
                    {
                        Left = Math.Max(0, left);
                        Top = Math.Max(0, top);
                        Width = w;
                        Height = h;
                    }
                }
            }
            catch { }
        }
    }

    private static void SelectComboByText(ComboBox combo, string text)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem cbi && string.Equals(cbi.Content?.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = cbi;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private static string GetComboText(ComboBox combo)
        => (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? combo.Text ?? "";

    private AppSettings CollectSettingsFromUi()
    {
        var s = new AppSettings
        {
            IntervalMs = ParseIntOr(IntervalBox.Text, 100),
            JitterMs = ParseIntOr(JitterBox.Text, 0),
            Count = ParseIntOr(CountBox.Text, 100),
            StartDelaySeconds = ParseDoubleOr(DelayBox.Text, 0),
            FixedX = ParseIntOr(XBox.Text, 0),
            FixedY = ParseIntOr(YBox.Text, 0),
            Button = Enum.TryParse<ClickButton>(GetComboText(ButtonCombo), true, out var b) ? b : ClickButton.Left,
            ClickType = Enum.TryParse<ClickType>(GetComboText(ClickTypeCombo), true, out var ct) ? ct : ClickType.Single,
            RepeatMode = RepeatCountRadio.IsChecked == true ? RepeatMode.Count : RepeatMode.UntilStopped,
            PositionMode = TargetFixedRadio.IsChecked == true ? PositionMode.Fixed : PositionMode.Cursor,
            Hotkey = (HotkeyCombo.SelectedItem?.ToString() ?? "F6").ToUpperInvariant(),
            AlwaysOnTop = AlwaysOnTopCheck.IsChecked == true,
            FailsafeEnabled = FailsafeCheck.IsChecked == true,
            ShowStartConfirmation = ConfirmCheck.IsChecked == true,
            WindowBounds = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", Left, Top, Width, Height)
        };
        return SettingsService.Normalize(s);
    }

    private bool TryBuildConfig(out ClickConfiguration? config, out string? error)
    {
        config = null;
        error = null;

        if (!int.TryParse(IntervalBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval) || interval < 1 || interval > 3_600_000)
        { error = "Enter an interval between 1 and 3,600,000 ms."; return false; }
        if (!int.TryParse(JitterBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var jitter) || jitter < 0 || jitter > 3_600_000)
        { error = "Enter a jitter value between 0 and 3,600,000 ms."; return false; }
        if (jitter > interval * 10) { error = "Jitter is too large compared to the interval."; return false; }
        if (!double.TryParse(DelayBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var delay) || delay < 0 || delay > 60)
        { error = "Enter a start delay between 0 and 60 seconds."; return false; }

        var repeatMode = RepeatCountRadio.IsChecked == true ? RepeatMode.Count : RepeatMode.UntilStopped;
        var maxClicks = 0;
        if (repeatMode == RepeatMode.Count)
        {
            if (!int.TryParse(CountBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out maxClicks) || maxClicks < 1 || maxClicks > 100_000_000)
            { error = "Enter a click count between 1 and 100,000,000."; return false; }
        }

        var posMode = TargetFixedRadio.IsChecked == true ? PositionMode.Fixed : PositionMode.Cursor;
        var fx = 0; var fy = 0;
        if (posMode == PositionMode.Fixed)
        {
            if (!int.TryParse(XBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out fx) || fx < -100_000 || fx > 100_000)
            { error = "Enter a valid X coordinate (-100,000 to 100,000)."; return false; }
            if (!int.TryParse(YBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out fy) || fy < -100_000 || fy > 100_000)
            { error = "Enter a valid Y coordinate (-100,000 to 100,000)."; return false; }
        }

        config = new ClickConfiguration
        {
            IntervalMs = interval,
            JitterMs = jitter,
            Button = Enum.TryParse<ClickButton>(GetComboText(ButtonCombo), true, out var b) ? b : ClickButton.Left,
            ClickType = Enum.TryParse<ClickType>(GetComboText(ClickTypeCombo), true, out var ct) ? ct : ClickType.Single,
            RepeatMode = repeatMode,
            MaxClicks = maxClicks,
            StartDelaySeconds = delay,
            PositionMode = posMode,
            FixedX = fx,
            FixedY = fy,
            FailsafeEnabled = FailsafeCheck.IsChecked == true
        };
        return true;
    }

    private void StartStop_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_engine.IsBusy)
            {
                _engine.Stop(StopReason.User);
                return;
            }

            if (_isPickingLocation)
            {
                SetStatusDetail("Finish or wait for location picking before starting.");
                return;
            }

            if (!TryBuildConfig(out var config, out var error) || config is null)
            {
                SetStatusDetail(error ?? "Invalid settings.");
                ShowInfo("Check settings", error ?? "Invalid settings.", MessageBoxImage.Warning);
                return;
            }

            var hotkey = HotkeyCombo.SelectedItem?.ToString() ?? "F6";
            if (ConfirmCheck.IsChecked == true)
            {
                var summary = DescribeConfig(config, hotkey);
                var result = MessageBox.Show(this,
                    summary + "\n\nStart clicking now?",
                    "Start auto-clicking",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No);
                if (result != MessageBoxResult.Yes)
                {
                    SetStatusDetail("Start cancelled.");
                    return;
                }
            }

            SaveSettings(showFeedback: false);
            if (!_engine.Start(config, out var startError))
            {
                SetStatusDetail(startError ?? "Could not start.");
                ShowInfo("Could not start", startError ?? "Could not start.", MessageBoxImage.Warning);
            }
            else
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
        catch (Exception ex)
        {
            SetStatusDetail("Error: " + ex.Message);
        }
    }

    private static string DescribeConfig(ClickConfiguration c, string hotkey)
    {
        var where = c.PositionMode == PositionMode.Cursor ? "current cursor position" : $"fixed position ({c.FixedX}, {c.FixedY})";
        var repeat = c.RepeatMode == RepeatMode.UntilStopped ? "until stopped" : $"{c.MaxClicks} click(s)";
        var jitter = c.JitterMs > 0 ? $" ±{c.JitterMs} ms jitter" : "";
        var delay = c.StartDelaySeconds > 0 ? $"\nStart delay: {c.StartDelaySeconds:0.##} s" : "";
        var failsafe = c.FailsafeEnabled ? "On (top-left corner)" : "Off";
        return $"Action: {c.ClickType} {c.Button.ToString().ToLowerInvariant()} click\n" +
               $"Location: {where}\n" +
               $"Interval: {c.IntervalMs} ms{jitter}\n" +
               $"Repeat: {repeat}{delay}\n" +
               $"Hotkey: {hotkey}\n" +
               $"Failsafe: {failsafe}";
    }

    private void StopIfRunning()
    {
        if (_engine.IsBusy) _engine.Stop(StopReason.User);
    }

    private void OnStopped(StopReason reason, string message)
    {
        UpdateStatus(new ClickerStatus(ClickerState.Stopped, GetDisplayedClicks(), GetTargetClicks(), GetDisplayedElapsed(), message));
        UpdateModeStates();
        CommandManager.InvalidateRequerySuggested();
    }

    private long GetDisplayedClicks() => long.TryParse(ClicksStatText.Text, out var v) ? v : 0;
    private long GetTargetClicks() => RepeatCountRadio.IsChecked == true && int.TryParse(CountBox.Text, out var c) ? c : 0;
    private double GetDisplayedElapsed()
    {
        var t = (ElapsedStatText.Text ?? "").Replace(" s", "", StringComparison.OrdinalIgnoreCase).Trim();
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private void UpdateStatus(ClickerStatus status)
    {
        try
        {
            ClicksStatText.Text = status.ClicksCompleted.ToString(CultureInfo.InvariantCulture);
            ElapsedStatText.Text = status.ElapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
            HotkeyStatText.Text = HotkeyCombo.SelectedItem?.ToString() ?? "F6";
            StatusDetailText.Text = status.Message;

            var stateText = status.State switch
            {
                ClickerState.Running => "Running",
                ClickerState.Starting => "Starting",
                _ => "Stopped"
            };
            StatusPillText.Text = stateText;
            AutomationProperties.SetName(StatusPill, $"Status: {stateText}. {status.Message}");

            Brush? bg = null; Brush? fg = null;
            if (status.State == ClickerState.Running)
            {
                bg = TryBrush("App.SuccessBgBrush");
                fg = TryBrush("App.SuccessFgBrush");
            }
            else if (status.State == ClickerState.Starting)
            {
                bg = TryBrush("App.WarnBgBrush");
                fg = TryBrush("App.WarnFgBrush");
            }
            else
            {
                bg = TryBrush("App.DangerBgBrush");
                fg = TryBrush("App.DangerFgBrush");
            }
            if (bg is not null) StatusPill.Background = bg;
            if (fg is not null)
            {
                StatusPill.BorderBrush = fg;
                StatusPillText.Foreground = fg;
                StatusDot.Fill = fg;
            }

            if (status.TargetClicks > 0)
            {
                RunProgress.IsIndeterminate = false;
                RunProgress.Maximum = status.TargetClicks;
                RunProgress.Value = Math.Min(status.ClicksCompleted, status.TargetClicks);
            }
            else
            {
                RunProgress.IsIndeterminate = status.State == ClickerState.Running;
                if (status.State == ClickerState.Stopped) { RunProgress.IsIndeterminate = false; RunProgress.Value = 0; }
            }

            var hotkey = HotkeyCombo.SelectedItem?.ToString() ?? "F6";
            if (status.State == ClickerState.Stopped)
            {
                StartStopButton.Content = $"_Start ({hotkey})";
                StartStopButton.Style = (Style)FindResource("PrimaryButton");
                AutomationProperties.SetName(StartStopButton, "Start auto-clicking");
            }
            else
            {
                StartStopButton.Content = $"_Stop ({hotkey})";
                StartStopButton.Style = (Style)FindResource("StopButton");
                AutomationProperties.SetName(StartStopButton, "Stop auto-clicking");
            }

            UpdateModeStates();
        }
        catch { }
    }

    private Brush? TryBrush(string key) => TryFindResource(key) as Brush;

    private void SetStatusDetail(string message)
    {
        StatusDetailText.Text = message;
    }

    private void UpdateModeStates()
    {
        var busy = _engine.IsBusy || _isPickingLocation;
        CountBox.IsEnabled = RepeatCountRadio.IsChecked == true && !_engine.IsBusy;
        var isFixedTarget = TargetFixedRadio.IsChecked == true;
        XBox.IsEnabled = isFixedTarget && !_engine.IsBusy;
        YBox.IsEnabled = isFixedTarget && !_engine.IsBusy;
        PickLocationButton.IsEnabled = isFixedTarget && !busy;
        UseCurrentButton.IsEnabled = isFixedTarget && !busy;

        IntervalBox.IsEnabled = !_engine.IsBusy;
        JitterBox.IsEnabled = !_engine.IsBusy;
        DelayBox.IsEnabled = !_engine.IsBusy;
        ButtonCombo.IsEnabled = !_engine.IsBusy;
        ClickTypeCombo.IsEnabled = !_engine.IsBusy;
        RepeatUntilStoppedRadio.IsEnabled = !_engine.IsBusy;
        RepeatCountRadio.IsEnabled = !_engine.IsBusy;
        TargetCursorRadio.IsEnabled = !_engine.IsBusy;
        TargetFixedRadio.IsEnabled = !_engine.IsBusy;
        HotkeyCombo.IsEnabled = !_engine.IsBusy;
        FailsafeCheck.IsEnabled = !_engine.IsBusy;
        ConfirmCheck.IsEnabled = !_engine.IsBusy;
        AlwaysOnTopCheck.IsEnabled = true;
        TestClickButton.IsEnabled = !_engine.IsBusy && !_isPickingLocation;
        ResetButton.IsEnabled = !_engine.IsBusy && !_isPickingLocation;
        SaveButton.IsEnabled = !_engine.IsBusy;
    }

    private void UpdateCursorLabel()
    {
        try
        {
            if (ClickerEngine.GetCursorPosition(out var x, out var y))
                CursorPositionText.Text = $"Cursor: {x}, {y}";
            else
                CursorPositionText.Text = "Cursor: unavailable";
        }
        catch { CursorPositionText.Text = "Cursor: unavailable"; }
    }

    private void UpdateRateEstimate()
    {
        try
        {
            if (!int.TryParse(IntervalBox.Text.Trim(), out var interval) || interval <= 0)
            { RateText.Text = "—"; return; }
            _ = int.TryParse(JitterBox.Text.Trim(), out var jitter);
            if (jitter < 0) jitter = 0;
            if (jitter <= 0)
            {
                var cps = 1000.0 / interval;
                RateText.Text = $"~{cps:0.##} /s";
            }
            else
            {
                var minInterval = Math.Max(1, interval - jitter);
                var maxInterval = interval + jitter;
                RateText.Text = $"~{1000.0 / maxInterval:0.##}–{1000.0 / minInterval:0.##} /s";
            }
        }
        catch { RateText.Text = ""; }
    }

    private void RepeatModeChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateModeStates();
    }

    private void TargetModeChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateModeStates();
    }

    private void AlwaysOnTopChanged(object sender, RoutedEventArgs e)
    {
        ApplyAlwaysOnTop();
    }

    private void ApplyAlwaysOnTop()
    {
        Topmost = AlwaysOnTopCheck.IsChecked == true;
    }

    private void HotkeyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressHotkeyChange || !_loaded) return;
        ApplyHotkeyFromUi(showErrors: true);
        var hk = HotkeyCombo.SelectedItem?.ToString() ?? "F6";
        HotkeyStatText.Text = hk;
        if (!_engine.IsBusy) StartStopButton.Content = $"_Start ({hk})";
    }

    private void ApplyHotkeyFromUi(bool showErrors)
    {
        var hk = HotkeyCombo.SelectedItem?.ToString() ?? "F6";
        if (!_hotkeyService.Register(hk, out var error))
        {
            if (showErrors && !string.IsNullOrWhiteSpace(error))
            {
                SetStatusDetail(error);
            }
        }
        else
        {
            if (_loaded && !_engine.IsBusy) SetStatusDetail($"Global hotkey set to {hk}.");
        }
    }

    private void TestClick_Click(object sender, RoutedEventArgs e)
    {
        if (_engine.IsBusy || _isPickingLocation) return;
        if (!TryBuildConfig(out var config, out var error) || config is null)
        {
            SetStatusDetail(error ?? "Invalid settings.");
            ShowInfo("Check settings", error ?? "Invalid settings.", MessageBoxImage.Warning);
            return;
        }
        try
        {
            var ok = ClickerEngine.PerformSingleTestClick(config);
            SetStatusDetail(ok ? "Performed 1 test click with the current settings." : "Could not perform the test click.");
            if (!ok) ShowInfo("Test click", "Could not perform the test click. Check permissions and settings.", MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            SetStatusDetail("Test click failed: " + ex.Message);
        }
    }

    private async void PickLocation_Click(object sender, RoutedEventArgs e)
    {
        if (_engine.IsBusy || _isPickingLocation) return;
        _isPickingLocation = true;
        TargetFixedRadio.IsChecked = true;
        UpdateModeStates();
        try
        {
            for (var i = 3; i >= 1; i--)
            {
                SetStatusDetail($"Move cursor to the target... capturing in {i}s");
                await Task.Delay(1000);
            }
            if (ClickerEngine.GetCursorPosition(out var x, out var y))
            {
                XBox.Text = x.ToString(CultureInfo.InvariantCulture);
                YBox.Text = y.ToString(CultureInfo.InvariantCulture);
                SetStatusDetail($"Captured fixed position: {x}, {y}");
            }
            else
            {
                SetStatusDetail("Could not capture the cursor position.");
            }
        }
        catch (Exception ex)
        {
            SetStatusDetail("Could not pick location: " + ex.Message);
        }
        finally
        {
            _isPickingLocation = false;
            UpdateModeStates();
        }
    }

    private void UseCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (_engine.IsBusy || _isPickingLocation) return;
        if (ClickerEngine.GetCursorPosition(out var x, out var y))
        {
            TargetFixedRadio.IsChecked = true;
            XBox.Text = x.ToString(CultureInfo.InvariantCulture);
            YBox.Text = y.ToString(CultureInfo.InvariantCulture);
            SetStatusDetail($"Fixed position set to current cursor: {x}, {y}");
            UpdateModeStates();
        }
        else
        {
            SetStatusDetail("Could not read the cursor position.");
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => SaveSettings(showFeedback: true);

    private void SaveSettings(bool showFeedback)
    {
        try
        {
            var s = CollectSettingsFromUi();
            _settings = s;
            _settingsService.Save(s);
            if (showFeedback) SetStatusDetail("Settings saved.");
        }
        catch (Exception ex)
        {
            if (showFeedback) SetStatusDetail("Could not save settings: " + ex.Message);
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (_engine.IsBusy || _isPickingLocation) return;
        var defaults = SettingsService.Normalize(new AppSettings());
        ApplySettingsToUi(defaults);
        ApplyAlwaysOnTop();
        UpdateRateEstimate();
        UpdateModeStates();
        ApplyHotkeyFromUi(showErrors: false);
        SetStatusDetail("Default settings restored.");
    }

    private void ShowShortcutsHelp()
    {
        const string text = "Keyboard shortcuts\n\n" +
                            "• Global hotkey (default F6): Start or stop clicking\n" +
                            "• Esc: Stop (when this window is focused)\n" +
                            "• Alt+S: Start or stop\n" +
                            "• Ctrl+S: Save settings\n" +
                            "• F1: Show this help\n" +
                            "• Tab / Shift+Tab: Move between controls\n" +
                            "• Space / Enter: Activate the focused control\n\n" +
                            "Safety\n" +
                            "• Failsafe (recommended): move the mouse to the top-left corner (0,0) to stop instantly\n" +
                            "• Use “Click once” to test settings before starting\n\n" +
                            "Use automated clicking only where it is permitted.";
        MessageBox.Show(this, text, "Shortcuts & tips — ByJTT AutoClicker", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowInfo(string title, string message, MessageBoxImage icon)
    {
        try { MessageBox.Show(this, message, title, MessageBoxButton.OK, icon); } catch { }
    }

    private static int ParseIntOr(string s, int fallback) => int.TryParse((s ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    private static double ParseDoubleOr(string s, double fallback) => double.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
}

internal sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    { _execute = execute; _canExecute = canExecute; }
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
}
