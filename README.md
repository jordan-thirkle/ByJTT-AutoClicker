# ByJTT AutoClicker

A simple, dependency-free Windows auto-clicker by **ByJTT**, built with Python and tkinter.

> **Responsible use:** This tool is intended for legitimate automation and accessibility-related convenience where automated clicking is permitted. Do not use it to violate software terms, game rules, workplace policies, or applicable laws.

## Features

- **Clear, keyboard-friendly UI:** card-based layout, labeled controls, menu bar, status badge (`RUNNING` / `STARTING` / `STOPPED`), live cursor readout, click/elapsed stats, and progress for fixed-count runs
- **Click controls:** Left / Right / Middle button, Single or Double click, configurable interval (ms), and optional random jitter (± ms) with CPS estimate
- **Repeat & timing:** run until stopped or for a fixed number of clicks, with optional start delay countdown
- **Click location:** follow the current cursor or use a fixed X/Y screen position
  - **Pick location (3s)** to capture the target
  - **Use current cursor** to copy the live position instantly
- **Control & safety:** configurable global hotkey (**F1–F12**, default **F6**) to start/stop, start confirmation summary, **Esc** to stop when focused, optional failsafe (move mouse to top-left corner `0,0` to stop), always-on-top, and **Click once (test)**
- **Local settings:** preferences and window geometry are saved locally (per-user app data) and restored on next launch
- **Privacy:** no telemetry, no network features, and no accounts
- **Reliable input:** uses Windows `SendInput` (with fallback) for mouse events

## Requirements

- Windows 10/11 (desktop)
- Python 3.10+ with `tkinter` (included with standard Windows Python installs)
- No third-party packages required

## Run

From PowerShell in this folder:

```powershell
py -3 .\autoclicker.py
# or
python .\autoclicker.py
```

Run without a console window:

```powershell
pyw -3 .\autoclicker.py
# or
pythonw .\autoclicker.py
```

Or double-click `launch_autoclicker.bat`.

## Quick start

1. Set **interval**, optional **jitter**, **mouse button**, and **click type**.
2. Choose **repeat until stopped** or a **fixed number of clicks**.
3. Choose **follow cursor** or **fixed screen position** (use **Pick location (3s)** / **Use current cursor**).
4. Optionally set **start delay**, **global hotkey**, **always on top**, and **failsafe**.
5. Click **Start** (or press the global hotkey) and review the confirmation summary to begin. Stop with the hotkey, **Stop**, **Esc** (when focused), or the failsafe corner.

## Keyboard shortcuts

- **Global hotkey** (default **F6**): start/stop clicking
- **Esc** (window focused): stop
- **Ctrl+S**: save settings
- **Alt+S**: start
- **Alt+T**: stop
- **F1**: show shortcuts
- **Tab / Shift+Tab**: move between controls
- **Space / Enter**: activate focused controls

## Windows guidelines, accessibility, and compliance notes

This is a **classic Win32 desktop utility** implemented with Python/tkinter (not a packaged WinUI 3/UWP/MSIX Microsoft Store app). It is designed to align with applicable Windows desktop app guidance and 2026 best practices where the toolkit and app model allow, including:

- **Keyboard access:** keyboard-operable controls with logical tab order, menu/shortcuts, labeled inputs, and clear stop mechanisms (hotkey, Esc, Stop button)
- **Clarity & feedback:** explicit status (`RUNNING`/`STARTING`/`STOPPED`) with text labels, start-confirmation summary, and descriptive status/error messages (state is not conveyed by color alone)
- **Accessibility-minded UI:** visible labels, high-DPI awareness, readable typography/spacing, resizable/scrollable layout, and contrast-conscious colors
- **Safety & user control:** confirmation before starting, prominent stop actions, optional failsafe (top-left corner), and no hidden/background automation beyond what the user starts
- **Privacy & security:** local-only settings storage (per-user app data), no telemetry, no network communication, no accounts, and no collection/transmission of personal data

**Important:** Because this project is not built with Windows App SDK/WinUI and is not submitted as a packaged Microsoft Store product, it cannot claim blanket certification against every Microsoft Store policy or every Windows app SDK-specific guideline. If you package or distribute it via the Microsoft Store, review the current [Microsoft Store Policies](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies) (including 10.2 security/product safety) and perform full accessibility validation (keyboard-only, Narrator/screen reader, high contrast, and DPI/scaling) for your target environment.

## Settings storage

- Settings are stored locally at: `%APPDATA%\ByJTT\AutoClicker\settings.json`
- A legacy `settings.json` next to the script is read for migration if present (local app data takes precedence once saved)

## Files

- `autoclicker.py` — main application
- `launch_autoclicker.bat` — Windows launcher
- `README.md` — documentation
- `LICENSE` — MIT license
- `CONTRIBUTING.md` — contribution notes
- `SECURITY.md` — security/responsible disclosure notes
- `.gitignore` — excludes local/user-specific and build files (including `settings.json`)

## Development

1. Clone the repository.
2. Run `py -3 autoclicker.py`.
3. Syntax check: `py -3 -m py_compile autoclicker.py`

## License

[MIT](LICENSE)
