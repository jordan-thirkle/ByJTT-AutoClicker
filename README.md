# ByJTT AutoClicker

A modern, minimal Windows auto-clicker by **ByJTT** — built with **WPF on .NET 8** to feel fast, clear, and native on Windows 10/11. Simple controls, safe defaults, and polished feedback without clutter.

> **Responsible use:** Automated clicking is for legitimate convenience and accessibility-related use where it is permitted. Do not use this tool to violate software terms, game rules, workplace policies, or applicable laws.

## Highlights

- **Modern, minimal UI:** clean card-based layout, clear typography, and system-aware light/dark styling with accent integration, best-effort Windows 11 Mica/title-bar integration, and rounded corners on supported systems
- **Clear control & feedback:** explicit `Stopped` / `Starting` / `Running` status, live click count and elapsed time, estimated rate, progress for fixed runs, and descriptive status messages
- **Flexible clicking:** left/right/middle button, single or double click, interval in ms with optional random jitter (± ms), and a **Click once** test action
- **Repeat & timing:** run until stopped or for a fixed number of clicks, with optional start-delay countdown
- **Precise targeting:** follow the current cursor or click a fixed X/Y screen position, with **Pick point (3s)** and **Use cursor** capture
- **Safety first:** start confirmation (optional), prominent stop controls, configurable global hotkey (default **F6**), **Esc** to stop when focused, and optional failsafe (move cursor to top-left `0,0` to stop instantly)
- **Keyboard-friendly & accessible:** labeled controls, logical tab order, access keys, shortcuts, UI Automation names/help/live status for assistive technologies, and Windows High Contrast theme support
- **Private & local:** settings stored locally per user, no telemetry, no analytics, no ads, no accounts, and no network communication

## Requirements

- **Windows 10/11** (desktop)
- **.NET 8 Desktop Runtime** to run a prebuilt app, or **.NET 8 SDK** to build from source

No third-party runtime dependencies.

## Run

If you have a built executable:

- Double-click **`Start ByJTT AutoClicker.cmd`** (launches the Release build if present), or run:
  - `src\ByJTTAutoClicker\bin\Release\net8.0-windows\ByJTTAutoClicker.exe`

## Build from source

```powershell
dotnet build src\ByJTTAutoClicker\ByJTTAutoClicker.csproj -c Release
```

The output executable is written to:

`src\ByJTTAutoClicker\bin\Release\net8.0-windows\ByJTTAutoClicker.exe`

## Quick start

1. Set **interval**, optional **jitter**, **button**, and **click type**.
2. Choose **repeat until stopped** or a **fixed number of clicks**.
3. Choose **follow cursor** or **fixed screen position** (use **Pick point (3s)** or **Use cursor**).
4. Optionally set **start delay**, **global hotkey**, **failsafe**, and **start confirmation**.
5. Press **Start** (or the global hotkey). Stop anytime with the hotkey, **Stop**, **Esc** (when focused), or the failsafe corner.

## Keyboard shortcuts

- **Global hotkey** (default **F6**, configurable F1–F12): start/stop clicking
- **Alt+S**: start/stop
- **Esc** (window focused): stop
- **Ctrl+S**: save settings
- **F1**: show shortcuts & tips
- **Tab / Shift+Tab**: move between controls
- **Space / Enter**: activate focused control

## Settings & privacy

- Settings are stored locally at: `%APPDATA%\ByJTT\AutoClicker\settings.json`
- Legacy settings next to an older script-based build are migrated on first load when found
- No telemetry, no analytics, no crash reporting, no accounts, and no network features are implemented

## Windows design & quality alignment

ByJTT AutoClicker is designed to align with applicable Microsoft Windows desktop design and quality guidance (as of 2026) for a **classic, unpackaged WPF** app, including:

- **System integration:** Per-Monitor V2 DPI awareness, system fonts, light/dark mode and accent awareness, High Contrast support via system colors, and best-effort Windows 11 integration (dark title bar, Mica backdrop, and rounded corners where supported)
- **Accessibility:** keyboard-operable UI, visible labels and logical focus order, UI Automation names/help/live status for assistive technologies, and non-color-dependent status indication
- **Safety & trust:** user-initiated actions, confirmation, multiple stop mechanisms/failsafe, standard user privileges (`asInvoker`), and local-only data handling

**Scope note (important):** This repository is a classic desktop app (not an MSIX-packaged WinUI 3/Windows App SDK Microsoft Store app). It is **not** possible to guarantee or certify compliance with *every* Microsoft guideline, certification, or Store policy from source alone—especially those that are packaging/distribution-specific or require independent/manual validation. Store submission, MSIX identity/signing, formal accessibility conformance, and security/product-safety sign-off depend on your target distribution and testing in your environment. See [`docs/COMPLIANCE.md`](docs/COMPLIANCE.md) for implemented practices and recommended validation steps.

## Project structure

- `src/ByJTTAutoClicker/` — WPF application source
- `docs/COMPLIANCE.md` — Windows alignment and validation notes
- `.github/workflows/build.yml` — CI build (Windows, .NET 8)
- `Start ByJTT AutoClicker.cmd` — launcher for local builds
- `legacy/` — earlier Python prototype (kept for reference)
- `CONTRIBUTING.md`, `SECURITY.md`, `LICENSE`

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Security

See [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE) © 2026 ByJTT
