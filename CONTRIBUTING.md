# Contributing to ByJTT AutoClicker

Thanks for your interest in improving **ByJTT AutoClicker**.

## Principles

- **Quality & clarity:** keep the UI modern, minimal, and easy to understand
- **Accessibility:** preserve keyboard access, visible labels, logical focus order, and UI Automation properties/names
- **Safety:** maintain user control (clear start/stop, failsafe behavior, no hidden automation)
- **Privacy:** do not add telemetry, analytics, ads, accounts, or network communication
- **Simplicity:** avoid unnecessary dependencies (the app currently uses WPF/.NET BCL only)

## Prerequisites

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Development

1. Fork and clone the repository.
2. Create a branch for your change.
3. Build and run locally:

   ```powershell
   dotnet build src\ByJTTAutoClicker\ByJTTAutoClicker.csproj -c Release
   .\src\ByJTTAutoClicker\bin\Release\net8.0-windows\ByJTTAutoClicker.exe
   ```

4. Verify your changes (as applicable):
   - Keyboard-only operation (Tab/Shift+Tab, Space/Enter, Esc, shortcuts)
   - Light/dark mode and High Contrast behavior
   - DPI/scaling (e.g. 100%, 125%, 150%+)
   - Start/stop, hotkey, failsafe, and settings persistence

5. Open a pull request with a clear description of the change and how you tested it.

## Reporting issues

Please include:

- Windows version (10/11, build) and display scaling/DPI if relevant
- .NET runtime/SDK version
- Steps to reproduce, expected behavior, and actual behavior
- Screenshots or logs if helpful

## Responsible use

Contributions must not promote misuse. Automated clicking features should remain user-initiated, transparent, and easy to stop.
