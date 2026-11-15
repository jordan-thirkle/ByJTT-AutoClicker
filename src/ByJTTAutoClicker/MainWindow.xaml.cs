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
    private bool _suppressPresetChange;
    private bool _suppressUiEvents;
    private bool _loaded;
    private StopReason? _lastStopReason;

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
            SetStatusDetail("Could not set up the global stop/start key. You can still use the buttons in this window.");
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            PopulateHotkeys();
            _settings = _settingsService.Load();
            _suppressUiEvents = true;
            ApplySettingsToUi(_settings);
            _suppressUiEvents = false;

            ApplyAlwaysOnTop();
            SyncSpeedPresetFromInterval();
            UpdateRateEstimate();
            UpdateModeStates();
            UpdatePlanSummary();
            _lastStopReason = null;
            UpdateStatus(new ClickerStatus(ClickerState.Stopped, 0, 0, 0,
                "Ready. Choose your settings, then press Start clicking."));

            IntervalBox.TextChanged += (_, _) => { if (_loaded && !_suppressUiEvents) { SyncSpeedPresetFromInterval(); UpdateRateEstimate(); UpdatePlanSummary(); } };
            JitterBox.TextChanged += (_, _) => { if (_loaded && !_suppressUiEvents) { UpdateRateEstimate(); UpdatePlanSummary(); } };
            DelayBox.TextChanged += (_, _) => { if (_loaded && !_suppressUiEvents) UpdatePlanSummary(); };
            CountBox.TextChanged += (_, _) => { if (_loaded && !_suppressUiEvents) UpdatePlanSummary(); };
            XBox.TextChanged += (_, _) => { if (_loaded && !_suppressUiEvents) UpdatePlanSummary(); };
            YBox.TextChanged += (_, _) => { if (_loaded && !_suppressUiEvents) UpdatePlanSummary(); };
            ButtonCombo.SelectionChanged += (_, _) => { if (_loaded && !_suppressUiEvents) UpdatePlanSummary(); };
            ClickTypeCombo.SelectionChanged += (_, _) => { if (_loaded && !_suppressUiEvents) UpdatePlanSummary(); };

            _cursorTimer.Start();
            _loaded = true;
            (Application.Current as App)?.ApplyThemeSafe();
            StartStopButton.Focus();
        }
        catch (Exception ex)
        {
            SetStatusDetail("Could not load your saved settings. Using simple defaults. (" + ex.Message + ")");
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            _cursorTimer.Stop();
            if (_engine.IsBusy) _engine.Stop(StopReason.User, "Stopped because the app was closed.");
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
        { error = "Please enter a time between clicks from 1 to 3,600,000 ms (milliseconds)."; return false; }
        if (!int.TryParse(JitterBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var jitter) || jitter < 0 || jitter > 3_600_000)
        { error = "Please enter a random variation from 0 to 3,600,000 ms (or 0 for no variation)."; return false; }
        if (jitter > interval * 10) { error = "Random variation is too large for this speed. Try a smaller number."; return false; }
        if (!double.TryParse(DelayBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var delay) || delay < 0 || delay > 60)
        { error = "Please enter a start delay from 0 to 60 seconds."; return false; }

        var repeatMode = RepeatCountRadio.IsChecked == true ? RepeatMode.Count : RepeatMode.UntilStopped;
        var maxClicks = 0;
        if (repeatMode == RepeatMode.Count)
        {
            if (!int.TryParse(CountBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out maxClicks) || maxClicks < 1 || maxClicks > 100_000_000)
            { error = "Please enter how many clicks to do (from 1 to 100,000,000)."; return false; }
        }

        var posMode = TargetFixedRadio.IsChecked == true ? PositionMode.Fixed : PositionMode.Cursor;
        var fx = 0; var fy = 0;
        if (posMode == PositionMode.Fixed)
        {
            if (!int.TryParse(XBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out fx) || fx < -100_000 || fx > 100_000)
            { error = "Please enter a valid Left/Right (X) position number."; return false; }
            if (!int.TryParse(YBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out fy) || fy < -100_000 || fy > 100_000)
            { error = "Please enter a valid Up/Down (Y) position number."; return false; }
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
                SetStatusDetail("Please wait while I pick the spot on the screen, then try again.");
                return;
            }

            if (!TryBuildConfig(out var config, out var error) || config is null)
            {
                SetStatusDetail(error ?? "Please check your settings.");
                ShowInfo("Check your settings", error ?? "Some settings need to be fixed before starting.", MessageBoxImage.Warning);
                return;
            }

            var hotkey = HotkeyCombo.SelectedItem?.ToString() ?? "F6";
            if (ConfirmCheck.IsChecked == true)
            {
                var summary = DescribeConfig(config, hotkey);
                var result = MessageBox.Show(this,
                    summary + "\n\nStart clicking now?",
                    "Start clicking — ByJTT AutoClicker",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No);
                if (result != MessageBoxResult.Yes)
                {
                    SetStatusDetail("Start cancelled. Nothing was clicked.");
                    return;
                }
            }

            _lastStopReason = null;
            SaveSettings(showFeedback: false);
            if (!_engine.Start(config, out var startError))
            {
                SetStatusDetail(startError ?? "Could not start clicking.");
                ShowInfo("Could not start", startError ?? "Could not start clicking.", MessageBoxImage.Warning);
            }
            else
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
        catch (Exception ex)
        {
            SetStatusDetail("Something went wrong: " + ex.Message);
        }
    }

    private static string DescribeConfig(ClickConfiguration c, string hotkey)
    {
        var clickName = c.ClickType == ClickType.Double ? "Double click" : "Single click";
        var buttonName = c.Button switch { ClickButton.Right => "right mouse button", ClickButton.Middle => "middle mouse button", _ => "left mouse button" };
        var where = c.PositionMode == PositionMode.Cursor ? "At your mouse pointer" : $"At a fixed spot on screen (X {c.FixedX}, Y {c.FixedY})";
        var repeat = c.RepeatMode == RepeatMode.UntilStopped ? "Until you stop it" : $"{c.MaxClicks:N0} time(s), then stop";
        var speed = c.JitterMs > 0
            ? $"About every {c.IntervalMs} ms (with up to {c.JitterMs} ms variation)"
            : $"About every {c.IntervalMs} ms";
        var delay = c.StartDelaySeconds > 0 ? $"\n• Start delay: {c.StartDelaySeconds:0.##} second(s)" : "";
        var failsafe = c.FailsafeEnabled ? "On (move mouse to top-left corner to stop)" : "Off";

        return "Ready to start clicking:\n" +
               $"• What: {clickName} with the {buttonName}\n" +
               $"• Where: {where}\n" +
               $"• Speed: {speed}\n" +
               $"• How long: {repeat}{delay}\n" +
               $"• Stop/start key: {hotkey}\n" +
               $"• Safety stop: {failsafe}";
    }

    private void StopIfRunning()
    {
        if (_engine.IsBusy) _engine.Stop(StopReason.User);
    }

    private void OnStopped(StopReason reason, string message)
    {
        _lastStopReason = reason;
        UpdateStatus(new ClickerStatus(ClickerState.Stopped, GetDisplayedClicks(), GetTargetClicks(), GetDisplayedElapsed(), message));
        UpdateModeStates();
        CommandManager.InvalidateRequerySuggested();
    }

    private long GetDisplayedClicks() => long.TryParse((ClicksStatText.Text ?? "0").Replace(",", "").Replace(" ", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    private long GetTargetClicks() => RepeatCountRadio.IsChecked == true && int.TryParse((CountBox.Text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var c) ? c : 0;
    private double GetDisplayedElapsed()
    {
        var t = (ElapsedStatText.Text ?? "").Replace(" s", "", StringComparison.OrdinalIgnoreCase).Trim();
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private void UpdateStatus(ClickerStatus status)
    {
        try
        {
            ClicksStatText.Text = status.ClicksCompleted.ToString("N0", CultureInfo.InvariantCulture);
            ElapsedStatText.Text = status.ElapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
            var hotkey = HotkeyCombo.SelectedItem?.ToString() ?? "F6";
            HotkeyStatText.Text = hotkey;
            StatusDetailText.Text = status.Message;

            string stateText;
            string heroText;
            if (status.State == ClickerState.Running)
            {
                stateText = "Running";
                heroText = "Clicking now";
            }
            else if (status.State == ClickerState.Starting)
            {
                stateText = "Starting";
                heroText = "Starting soon…";
            }
            else
            {
                stateText = "Stopped";
                heroText = _lastStopReason switch
                {
                    StopReason.Completed => "All done",
                    StopReason.Failsafe => "Stopped for safety",
                    StopReason.Error => "Stopped (problem)",
                    StopReason.HotkeyConflict => "Stopped (key conflict)",
                    StopReason.User => status.ClicksCompleted > 0 ? "Stopped" : "Ready to click",
                    StopReason.Cancelled => "Ready to click",
                    _ => status.ClicksCompleted > 0 ? "Stopped" : "Ready to click"
                };
            }

            StatusPillText.Text = stateText;
            HeroStateText.Text = heroText;
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
                if (status.State != ClickerState.Running) { RunProgress.IsIndeterminate = false; RunProgress.Value = 0; }
            }

            if (status.State == ClickerState.Stopped)
            {
                StartStopButton.Content = $"_Start clicking ({hotkey})";
                StartStopButton.Style = (Style)FindResource("PrimaryButton");
                AutomationProperties.SetName(StartStopButton, "Start clicking");
            }
            else
            {
                StartStopButton.Content = $"_Stop clicking ({hotkey})";
                StartStopButton.Style = (Style)FindResource("StopButton");
                AutomationProperties.SetName(StartStopButton, "Stop clicking");
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
        var busy = _engine.IsBusy;
        var interacting = busy || _isPickingLocation;

        var fixedTarget = TargetFixedRadio.IsChecked == true;
        var countMode = RepeatCountRadio.IsChecked == true;

        FixedTargetPanel.Visibility = fixedTarget ? Visibility.Visible : Visibility.Collapsed;
        CountPanel.Visibility = countMode ? Visibility.Visible : Visibility.Collapsed;

        CountBox.IsEnabled = countMode && !busy;
        XBox.IsEnabled = fixedTarget && !busy;
        YBox.IsEnabled = fixedTarget && !busy;
        PickLocationButton.IsEnabled = fixedTarget && !interacting;
        UseCurrentButton.IsEnabled = fixedTarget && !interacting;

        IntervalBox.IsEnabled = !busy;
        JitterBox.IsEnabled = !busy;
        DelayBox.IsEnabled = !busy;
        ButtonCombo.IsEnabled = !busy;
        ClickTypeCombo.IsEnabled = !busy;
        SpeedPresetCombo.IsEnabled = !busy;
        RepeatUntilStoppedRadio.IsEnabled = !busy;
        RepeatCountRadio.IsEnabled = !busy;
        TargetCursorRadio.IsEnabled = !busy;
        TargetFixedRadio.IsEnabled = !busy;
        HotkeyCombo.IsEnabled = !busy;
        FailsafeCheck.IsEnabled = !busy;
        ConfirmCheck.IsEnabled = !busy;
        AlwaysOnTopCheck.IsEnabled = true;

        TestClickButton.IsEnabled = !interacting;
        ResetButton.IsEnabled = !interacting;
        SaveButton.IsEnabled = !busy;
    }

    private void UpdateCursorLabel()
    {
        try
        {
            if (ClickerEngine.GetCursorPosition(out var x, out var y))
                CursorPositionText.Text = $"Mouse pointer now: X {x}, Y {y}";
            else
                CursorPositionText.Text = "Mouse pointer now: unavailable";
        }
        catch { CursorPositionText.Text = "Mouse pointer now: unavailable"; }
    }

    private void UpdateRateEstimate()
    {
        try
        {
            if (!int.TryParse((IntervalBox.Text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval) || interval <= 0)
            { RateText.Text = "Enter a valid time between clicks to see the speed."; return; }
            _ = int.TryParse((JitterBox.Text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var jitter);
            if (jitter < 0) jitter = 0;

            if (jitter <= 0)
            {
                var cps = 1000.0 / interval;
                RateText.Text = $"About {FormatRate(cps)} click(s) every second";
            }
            else
            {
                var minInterval = Math.Max(1, interval - jitter);
                var maxInterval = interval + jitter;
                var fast = 1000.0 / minInterval;
                var slow = 1000.0 / maxInterval;
                RateText.Text = $"About {FormatRate(slow)} to {FormatRate(fast)} click(s) every second";
            }
        }
        catch { RateText.Text = ""; }
    }

    private static string FormatRate(double cps)
    {
        if (double.IsNaN(cps) || double.IsInfinity(cps)) return "—";
        if (Math.Abs(cps - Math.Round(cps)) < 0.05) return Math.Round(cps).ToString("0", CultureInfo.InvariantCulture);
        return cps.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private void UpdatePlanSummary()
    {
        try
        {
            var button = GetComboText(ButtonCombo);
            var clickType = GetComboText(ClickTypeCombo);
            var clickDesc = string.Equals(clickType, "Double", StringComparison.OrdinalIgnoreCase)
                ? $"Double {button.ToLowerInvariant()} click"
                : $"{button} click";

            var interval = ParseIntOr(IntervalBox.Text, 100);
            var where = TargetFixedRadio.IsChecked == true
                ? $"at one fixed spot (X {ParseIntOr(XBox.Text, 0)}, Y {ParseIntOr(YBox.Text, 0)})"
                : "at your mouse pointer";
            var howLong = RepeatCountRadio.IsChecked == true
                ? $"{ParseIntOr(CountBox.Text, 100):N0} time(s)"
                : "until you stop";

            PlanSummaryText.Text = $"{clickDesc} every {interval:N0} ms {where}, {howLong}.";
        }
        catch { PlanSummaryText.Text = ""; }
    }

    private void SyncSpeedPresetFromInterval()
    {
        if (SpeedPresetCombo == null) return;
        var interval = ParseIntOr(IntervalBox.Text, -1);
        string targetTag = "custom";
        if (interval == 1000) targetTag = "1000";
        else if (interval == 500) targetTag = "500";
        else if (interval == 200) targetTag = "200";
        else if (interval == 100) targetTag = "100";
        else if (interval == 50) targetTag = "50";

        _suppressPresetChange = true;
        try
        {
            foreach (var item in SpeedPresetCombo.Items)
            {
                if (item is ComboBoxItem cbi && string.Equals(cbi.Tag?.ToString(), targetTag, StringComparison.OrdinalIgnoreCase))
                {
                    SpeedPresetCombo.SelectedItem = cbi;
                    return;
                }
            }
            // fallback to Custom
            foreach (var item in SpeedPresetCombo.Items)
            {
                if (item is ComboBoxItem cbi && string.Equals(cbi.Tag?.ToString(), "custom", StringComparison.OrdinalIgnoreCase))
                { SpeedPresetCombo.SelectedItem = cbi; return; }
            }
        }
        finally { _suppressPresetChange = false; }
    }

    private void SpeedPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressPresetChange || !_loaded) return;
        if (SpeedPresetCombo.SelectedItem is not ComboBoxItem cbi) return;
        var tag = cbi.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(tag) || string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase)) return;
        if (int.TryParse(tag, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
        {
            _suppressUiEvents = true;
            IntervalBox.Text = ms.ToString(CultureInfo.InvariantCulture);
            _suppressUiEvents = false;
            UpdateRateEstimate();
            UpdatePlanSummary();
        }
    }

    private void RepeatModeChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateModeStates();
        UpdatePlanSummary();
    }

    private void TargetModeChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateModeStates();
        UpdatePlanSummary();
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
        if (!_engine.IsBusy)
        {
            StartStopButton.Content = $"_Start clicking ({hk})";
            UpdatePlanSummary();
        }
    }

    private void ApplyHotkeyFromUi(bool showErrors)
    {
        var hk = HotkeyCombo.SelectedItem?.ToString() ?? "F6";
        if (!_hotkeyService.Register(hk, out var error))
        {
            if (showErrors && !string.IsNullOrWhiteSpace(error))
            {
                SetStatusDetail(error + " You can still start and stop with the buttons in this window.");
            }
        }
        else
        {
            if (_loaded && !_engine.IsBusy)
                SetStatusDetail($"Stop/start key set to {hk}. You can press it from anywhere to start or stop.");
        }
    }

    private void TestClick_Click(object sender, RoutedEventArgs e)
    {
        if (_engine.IsBusy || _isPickingLocation) return;
        if (!TryBuildConfig(out var config, out var error) || config is null)
        {
            SetStatusDetail(error ?? "Please check your settings.");
            ShowInfo("Check your settings", error ?? "Some settings need to be fixed before trying a click.", MessageBoxImage.Warning);
            return;
        }
        try
        {
            var ok = ClickerEngine.PerformSingleTestClick(config);
            if (ok)
                SetStatusDetail("Did 1 test click using your current settings.");
            else
            {
                SetStatusDetail("The test click did not work. Please check your settings and try again.");
                ShowInfo("Test click", "Could not do the test click. If another app is blocking input, try running as usual (no special permissions are required).", MessageBoxImage.Warning);
            }
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
                SetStatusDetail($"Move your mouse to the spot you want, then hold still. Picking in {i}…");
                await Task.Delay(1000);
            }
            if (ClickerEngine.GetCursorPosition(out var x, out var y))
            {
                XBox.Text = x.ToString(CultureInfo.InvariantCulture);
                YBox.Text = y.ToString(CultureInfo.InvariantCulture);
                SetStatusDetail($"Got it. I’ll click at X {x}, Y {y}.");
            }
            else
            {
                SetStatusDetail("Could not read the mouse position. Please try again.");
            }
        }
        catch (Exception ex)
        {
            SetStatusDetail("Could not pick a spot: " + ex.Message);
        }
        finally
        {
            _isPickingLocation = false;
            UpdateModeStates();
            UpdatePlanSummary();
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
            SetStatusDetail($"I’ll click at your current mouse position: X {x}, Y {y}.");
            UpdateModeStates();
            UpdatePlanSummary();
        }
        else
        {
            SetStatusDetail("Could not read the mouse position.");
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
            if (showFeedback) SetStatusDetail("Settings saved on this device.");
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
        _suppressUiEvents = true;
        ApplySettingsToUi(defaults);
        _suppressUiEvents = false;
        ApplyAlwaysOnTop();
        ApplyHotkeyFromUi(showErrors: false);
        SyncSpeedPresetFromInterval();
        UpdateRateEstimate();
        UpdateModeStates();
        UpdatePlanSummary();
        SetStatusDetail("Back to simple defaults.");
    }

    private void Help_Click(object sender, RoutedEventArgs e) => ShowShortcutsHelp();

    private void ShowShortcutsHelp()
    {
        const string text = "ByJTT AutoClicker v4.3.0\n\n" +
                            "A simple, modern auto-clicker for Windows — made to be easy for everyone.\n\n" +
                            "How to use\n" +
                            "1. Choose how fast to click and where to click.\n" +
                            "2. Press “Start clicking” (or your stop/start key).\n" +
                            "3. To stop, press the stop/start key, press Esc in this window, click Stop, or move your mouse to the top-left corner of the screen (if safety stop is on).\n\n" +
                            "Keyboard shortcuts\n" +
                            "• Stop/start key (default F6): Start or stop from anywhere\n" +
                            "• Esc: Stop (when this window is selected)\n" +
                            "• Alt+S: Start or stop\n" +
                            "• Ctrl+S: Save settings\n" +
                            "• F1: Show this help\n" +
                            "• Tab / Shift+Tab: Move between controls\n" +
                            "• Space / Enter: Use the selected control\n\n" +
                            "Safety & privacy\n" +
                            "• Safety stop (recommended): move the mouse to the very top-left corner (0,0) to stop instantly.\n" +
                            "• “Try one click” lets you test safely before starting.\n" +
                            "• Settings stay on this device only. No accounts, no ads, no internet connection.\n\n" +
                            "Use automated clicking only where it is allowed and appropriate.";
        MessageBox.Show(this, text, "Help & shortcuts — ByJTT AutoClicker", MessageBoxButton.OK, MessageBoxImage.Information);
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
