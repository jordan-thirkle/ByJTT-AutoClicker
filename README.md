# ByJTT AutoClicker

A simple, modern Windows auto-clicker by **ByJTT** — built with **WPF on .NET 8** for a clear, calm, native feel on Windows 10/11. Designed to be easy to use for everyone (all ages and experience levels), with safe defaults and plain-language controls.

> **Responsible use:** Use automated clicking only where it is permitted and appropriate. Do not use this tool to violate software terms, game rules, workplace policies, or applicable laws.

## Why this version is easier to use

- **Simple by default:** A quick setup flow with just the essentials — click speed, where to click, and how long to click — plus a live summary of what will happen.
- **Progressive disclosure:** Advanced options (button/click style, variation, start delay, hotkey, safety/confirmation, always-on-top) are tucked under **More options** so the screen stays uncluttered.
- **Plain language & clear feedback:** Human-friendly labels and messages, an obvious status (“Ready to click / Starting soon / Clicking now / Stopped”), and a clear progress/stats view.
- **One big action:** A prominent **Start/Stop** button and a **Try one click** test button for safe checking.
- **Comfortable to read and use:** Larger text, generous spacing/click targets, and a minimal 2026-inspired layout that works in light/dark modes.

## Highlights

- **Flexible clicking:** left/right/middle button, single or double click, easy speed presets or custom interval (ms) with optional random variation
- **Repeat & timing:** keep clicking until stopped or for a set number of clicks, with optional start-delay countdown
- **Precise targeting:** follow your mouse pointer or click a fixed spot on screen — with **Pick a spot on screen (3 seconds)** and **Use my mouse position now**
- **Safety first:** confirmation before starting (optional), prominent stop controls, configurable global stop/start key (default **F6**), **Esc** to stop when focused, and a safety stop (move mouse to top-left `0,0` to stop instantly)
- **Keyboard-friendly & assistive-tech aware:** labeled controls, logical tab/focus order, access keys, shortcuts, UI Automation names/help/live status, and Windows High Contrast theme support
- **System-aware & polished:** Per-Monitor V2 DPI awareness, system fonts, light/dark and accent awareness, and best-effort Windows 11 integration (title bar/Mica/rounded corners where supported)
- **Private & local:** settings stored locally on your device — no telemetry, analytics, ads, accounts, or network communication

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

1. Choose a **click speed** (or pick a custom time between clicks).
2. Choose **where to click** — follow your mouse pointer, or pick a fixed spot on the screen.
3. Choose **how long to click** — until you stop, or a specific number of times.
4. (Optional) Open **More options** for click type/button, start delay, safety/confirmation, and the stop/start key.
5. Press **Try one click** to test, then press **Start clicking** (or your stop/start key). Stop anytime with the key, **Stop**, **Esc** (when focused), or the safety stop corner.

## Keyboard shortcuts

- **Stop/start key** (default **F6**, configurable F1–F12): start or stop clicking from anywhere
- **Alt+S**: start or stop
- **Esc** (window focused): stop
- **Ctrl+S**: save settings
- **F1**: show help & shortcuts
- **Tab / Shift+Tab**: move between controls
- **Space / Enter**: activate the focused control

## Settings & privacy

- Settings are stored locally at: `%APPDATA%\ByJTT\AutoClicker\settings.json`
- Legacy settings from older builds are migrated on first load when found
- No telemetry, no analytics, no crash reporting, no accounts, and no network features are implemented

## Windows design & quality alignment

ByJTT AutoClicker is designed to align with applicable Microsoft Windows desktop design and quality guidance (as of 2026) for a **classic, unpackaged WPF** app, including:

- **System integration:** Per-Monitor V2 DPI awareness, system fonts, light/dark and accent awareness, High Contrast support via system colors, and best-effort Windows 11 integration (title bar, Mica backdrop, and rounded corners where supported)
- **Accessibility-minded design:** keyboard-operable UI, visible labels and logical focus order, plain-language feedback, UI Automation names/help/live status, and non-color-dependent status indication
- **Safety & trust:** user-initiated actions, confirmation, multiple stop mechanisms/safety stop, standard user privileges (`asInvoker`), and local-only data handling

**Scope note (important):** This repository is a classic desktop app (not an MSIX-packaged WinUI 3/Windows App SDK Microsoft Store app). I cannot guarantee or certify compliance with *every* Microsoft guideline, certification, or Store policy from source alone—some are packaging/distribution-specific or require independent/manual validation (e.g. Narrator + Accessibility Insights passes, contrast checks across themes/scales, security/product-safety review, and Store submission/certification). See [`docs/COMPLIANCE.md`](docs/COMPLIANCE.md) for implemented practices and recommended validation steps.

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
