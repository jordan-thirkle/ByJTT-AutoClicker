using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using ByJTTAutoClicker.Interop;
using ByJTTAutoClicker.Models;

namespace ByJTTAutoClicker.Services;

public sealed class ClickerEngine
{
    private readonly Dispatcher _dispatcher;
    private readonly Random _random = new();
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private ClickConfiguration? _config;
    private readonly Stopwatch _elapsed = new();

    public event EventHandler<ClickerStatus>? StatusChanged;
    public event EventHandler<(StopReason Reason, string Message)>? Stopped;

    public ClickerState State { get; private set; } = ClickerState.Stopped;
    public bool IsBusy => State != ClickerState.Stopped;

    public ClickerEngine(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public bool Start(ClickConfiguration config, out string? error)
    {
        error = null;
        if (IsBusy)
        {
            error = "Clicking is already running.";
            return false;
        }

        _config = config;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        State = config.StartDelaySeconds > 0 ? ClickerState.Starting : ClickerState.Running;
        RaiseStatus(0, 0, config.StartDelaySeconds > 0 ? "Starting soon…" : "Clicking now…");
        _worker = Task.Run(() => RunAsync(config, token));
        return true;
    }

    public void Stop(StopReason reason = StopReason.User, string? message = null)
    {
        if (!IsBusy) return;
        _pendingStopReason = reason;
        _pendingStopMessage = message ?? reason switch
        {
            StopReason.Completed => "Finished.",
            StopReason.Failsafe => "Stopped for safety because the mouse moved to the top-left corner of the screen.",
            StopReason.HotkeyConflict => "Stopped because the stop/start key could not be used.",
            StopReason.Error => "Stopped because something went wrong.",
            StopReason.Cancelled => "Cancelled.",
            _ => "Stopped."
        };
        try { _cts?.Cancel(); } catch { }
    }

    private StopReason _pendingStopReason = StopReason.User;
    private string _pendingStopMessage = "Stopped.";
    private long _finalClicks;

    private async Task RunAsync(ClickConfiguration config, CancellationToken token)
    {
        _finalClicks = 0;
        _pendingStopReason = StopReason.User;
        _pendingStopMessage = "Stopped.";
        _elapsed.Restart();
        try
        {
            if (config.StartDelaySeconds > 0)
            {
                var remaining = config.StartDelaySeconds;
                while (remaining > 0)
                {
                    token.ThrowIfCancellationRequested();
                    var show = remaining >= 10 ? remaining.ToString("0") : remaining.ToString("0.0");
                    RaiseStatus(0, _elapsed.Elapsed.TotalSeconds, $"Starting in {show} second(s). Get ready.");
                    var step = Math.Min(0.1, remaining);
                    await Task.Delay(TimeSpan.FromSeconds(step), token).ConfigureAwait(false);
                    remaining -= step;
                }
                State = ClickerState.Running;
                RaiseStatus(0, _elapsed.Elapsed.TotalSeconds, "Clicking now…");
            }

            long clicks = 0;
            var target = config.RepeatMode == RepeatMode.Count ? config.MaxClicks : 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();

                if (config.FailsafeEnabled && IsFailsafeTriggered())
                {
                    _finalClicks = clicks;
                    Complete(StopReason.Failsafe, "Stopped for safety because the mouse moved to the top-left corner of the screen.", clicks);
                    return;
                }

                if (!PerformClick(config))
                {
                    _finalClicks = clicks;
                    Complete(StopReason.Error, "Could not click. Your computer did not accept the mouse action.", clicks);
                    return;
                }

                clicks++;
                _finalClicks = clicks;
                var msg = config.RepeatMode == RepeatMode.Count
                    ? $"Clicking now… {Math.Max(0, target - clicks):N0} click(s) left."
                    : "Clicking now… Press Stop or your stop/start key to end.";
                RaiseStatus(clicks, _elapsed.Elapsed.TotalSeconds, msg);

                if (config.RepeatMode == RepeatMode.Count && clicks >= target)
                {
                    Complete(StopReason.Completed, $"Finished. Did {clicks:N0} click(s).", clicks);
                    return;
                }

                var interval = GetNextIntervalSeconds(config.IntervalMs, config.JitterMs);
                await DelayInterruptible(interval, token, config.FailsafeEnabled).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            var reason = _pendingStopReason;
            var msg = _pendingStopMessage;
            if (string.IsNullOrWhiteSpace(msg))
            {
                msg = reason == StopReason.User ? "Stopped." : "Cancelled.";
            }
            if (reason == StopReason.User)
            {
                msg = string.Equals(_pendingStopMessage, "Stopped.", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(_pendingStopMessage)
                    ? (_finalClicks > 0 ? $"Stopped. {_finalClicks:N0} click(s) done." : "Stopped.")
                    : _pendingStopMessage;
            }
            Complete(reason, msg, _finalClicks);
        }
        catch
        {
            Complete(StopReason.Error, "Something went wrong while clicking. Please try again.", _finalClicks);
        }
        finally
        {
            _elapsed.Stop();
        }
    }

    private async Task DelayInterruptible(double seconds, CancellationToken token, bool failsafe)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            token.ThrowIfCancellationRequested();
            if (failsafe && IsFailsafeTriggered())
            {
                _pendingStopReason = StopReason.Failsafe;
                _pendingStopMessage = "Stopped for safety because the mouse moved to the top-left corner of the screen.";
                throw new OperationCanceledException(token);
            }
            var remaining = seconds - sw.Elapsed.TotalSeconds;
            if (remaining <= 0) break;
            var slice = Math.Min(0.02, remaining);
            await Task.Delay(TimeSpan.FromSeconds(slice), token).ConfigureAwait(false);
        }
    }

    private void Complete(StopReason reason, string message, long clicks)
    {
        State = ClickerState.Stopped;
        var elapsed = _elapsed.Elapsed.TotalSeconds;
        _dispatcher.BeginInvoke(() =>
        {
            Stopped?.Invoke(this, (reason, message));
            StatusChanged?.Invoke(this, new ClickerStatus(ClickerState.Stopped, clicks, _config?.RepeatMode == RepeatMode.Count ? _config.MaxClicks : 0, elapsed, message));
        });
        try { _cts?.Dispose(); } catch { }
        _cts = null;
    }

    private void RaiseStatus(long clicks, double elapsedSeconds, string message)
    {
        var target = _config?.RepeatMode == RepeatMode.Count ? _config!.MaxClicks : 0;
        var status = new ClickerStatus(State, clicks, target, elapsedSeconds, message);
        _dispatcher.BeginInvoke(() => StatusChanged?.Invoke(this, status));
    }

    private double GetNextIntervalSeconds(int baseMs, int jitterMs)
    {
        if (jitterMs <= 0) return Math.Max(0.001, baseMs / 1000.0);
        var low = Math.Max(1, baseMs - jitterMs);
        var high = baseMs + jitterMs;
        lock (_random)
        {
            var val = low + _random.NextDouble() * (high - low);
            return Math.Max(0.001, val / 1000.0);
        }
    }

    private static bool IsFailsafeTriggered()
    {
        return NativeMethods.GetCursorPos(out var p) && p.X == 0 && p.Y == 0;
    }

    public static bool GetCursorPosition(out int x, out int y)
    {
        if (NativeMethods.GetCursorPos(out var p))
        {
            x = p.X; y = p.Y; return true;
        }
        x = 0; y = 0; return false;
    }

    public static bool PerformSingleTestClick(ClickConfiguration config)
    {
        return PerformClick(config);
    }

    private static bool PerformClick(ClickConfiguration config)
    {
        if (config.PositionMode == PositionMode.Fixed)
        {
            if (!NativeMethods.SetCursorPos(config.FixedX, config.FixedY))
                return false;
        }

        var (down, up) = config.Button switch
        {
            ClickButton.Right => (NativeMethods.MOUSEEVENTF_RIGHTDOWN, NativeMethods.MOUSEEVENTF_RIGHTUP),
            ClickButton.Middle => (NativeMethods.MOUSEEVENTF_MIDDLEDOWN, NativeMethods.MOUSEEVENTF_MIDDLEUP),
            _ => (NativeMethods.MOUSEEVENTF_LEFTDOWN, NativeMethods.MOUSEEVENTF_LEFTUP)
        };

        if (!SendMouseButton(down, up)) return false;
        if (config.ClickType == ClickType.Double)
        {
            Thread.Sleep(45);
            if (!SendMouseButton(down, up)) return false;
        }
        return true;
    }

    private static bool SendMouseButton(uint down, uint up)
    {
        var inputs = new NativeMethods.INPUT[2];
        inputs[0] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            mi = new NativeMethods.MOUSEINPUT { dx = 0, dy = 0, mouseData = 0, dwFlags = down, time = 0, dwExtraInfo = 0 }
        };
        inputs[1] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            mi = new NativeMethods.MOUSEINPUT { dx = 0, dy = 0, mouseData = 0, dwFlags = up, time = 0, dwExtraInfo = 0 }
        };

        var size = Marshal.SizeOf<NativeMethods.INPUT>();
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, size);
        return sent == (uint)inputs.Length;
    }
}
