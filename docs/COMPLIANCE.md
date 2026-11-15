# Windows alignment & compliance notes

**ByJTT AutoClicker** (v4.2) is a classic Win32 desktop application built with **WPF on .NET 8**. This document describes how the app is designed to align with applicable Microsoft Windows desktop design and quality guidance (as of 2026), and what requires packaging-specific or manual validation.

> **Important:** No single source repository or automated check can certify an app against *every* Microsoft guideline, certification, or policy. Store certification, MSIX packaging/identity rules, formal accessibility conformance (e.g. WCAG/Section 508), and security/product-safety sign-off depend on your target distribution, packaging, and validation in your environment. This project aims to follow applicable guidance in code and design, and to be transparent about scope and limitations.

## App model and scope

- **Type:** Classic desktop app (WPF, `net8.0-windows`, WinExe), not a packaged WinUI 3 / Windows App SDK / UWP app and not submitted to the Microsoft Store in this repository.
- **Implication:** Windows App SDK-specific APIs, MSIX identity/packaging/signing requirements, and Microsoft Store policy/certification requirements are not all directly applicable as-is. Where relevant, this document notes what to do if you package for the Store (MSIX) or target those technologies.

## Design & user experience (Fluent-aligned, desktop)

- **Modern, minimal UI:** Clear visual hierarchy, card-based layout, consistent spacing/typography, rounded surfaces, and restrained color use for a calm, native feel.
- **System integration:** Uses system fonts (Segoe UI Variable / Segoe UI), respects Windows light/dark app theme and accent color (where available), and applies best-effort Windows 11 integration (dark/light title bar, Mica backdrop, and rounded window corners via DWM attributes) when supported. In High Contrast mode, system backdrop effects are not forced and system colors are used.
- **Clarity:** Primary actions are prominent; status is explicit (`Stopped` / `Starting` / `Running`) with text labels and detail messages (state is not conveyed by color alone). Start confirmation summarizes the action before automation begins.
- **DPI & rendering:** Per-Monitor V2 DPI awareness declared in the application manifest; layout uses device-independent units with `UseLayoutRounding`/`SnapsToDevicePixels` for crisp rendering.

## Accessibility (implemented practices)

- **Keyboard access:** All functionality is operable by keyboard. Controls have logical tab order, visible labels, access keys/mnemonics, and standard activation (Tab/Shift+Tab, Space/Enter). Global hotkey (default F6) and in-app shortcuts (Esc to stop when focused, Ctrl+S save, Alt+S start/stop, F1 help) are provided.
- **Assistive technology support:** UI elements expose names/help text and live status updates via WPF automation properties (`AutomationProperties.Name`, `HelpText`, `LiveSetting`, headings) to support UI Automation-based screen readers such as Narrator.
- **High Contrast:** When Windows High Contrast is enabled, the app applies a theme based on system colors (`SystemColors`) so user-selected contrast themes are honored.
- **Visible focus & affordances:** Focus visuals are defined for interactive controls; buttons and inputs have clear labels and minimum target sizes appropriate for pointer and keyboard use.
- **Motion/time:** Clicking cadence is user-controlled; an optional start-delay countdown provides preparation time. No decorative or flashing animations are used for critical information.

**Manual validation recommended:** Test with [Accessibility Insights for Windows](https://accessibilityinsights.io/docs/windows/overview/), Narrator (keyboard-only), Windows High Contrast themes, multiple display scales (100%–200%+), and a contrast analyzer to confirm results in your environment.

## Safety & user control

- **Explicit control:** Clicking runs only after user initiation and (by default) a confirmation summary. Clear stop mechanisms: Stop button, configurable global hotkey, Esc (when focused), and optional failsafe (move cursor to screen corner 0,0).
- **No hidden behavior:** No background automation, no silent persistence of clicking state, and no actions beyond the configured click behavior. The app runs with standard user privileges (`asInvoker`).
- **Responsible use:** UI and documentation state that automated clicking must be used only where permitted.

## Privacy & security

- **Local-only:** Settings are stored locally per user at `%APPDATA%\ByJTT\AutoClicker\settings.json`. No telemetry, analytics, crash reporting, ads, accounts, or network communication are implemented.
- **Least privilege:** The app manifest requests `asInvoker` (no elevation). Input injection uses standard Windows APIs (`SendInput`) only for the user-initiated automation feature.
- **Supply chain:** No third-party runtime dependencies (WPF/.NET BCL only). CI builds from source.

## Reliability & engineering

- **Input method:** Uses Windows `SendInput` for mouse events.
- **Resilience:** Theme/system-preference updates and settings load/save are handled defensively so non-critical failures do not crash the app; unhandled UI exceptions are surfaced with a user-facing error dialog.
- **Build:** Reproducible source build via `dotnet build` (see CI workflow). Versioning and application metadata are defined in the project file and manifest.

## Distribution notes (if you package for Microsoft Store / MSIX)

If you intend to distribute via the Microsoft Store or as an MSIX package, review and validate against the **current** [Microsoft Store Policies](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies) (including product safety, security, and responsible behavior) and Windows packaging/signing/identity requirements. You should also:

- Run full accessibility validation (Narrator, Accessibility Insights, keyboard-only, High Contrast, contrast ratios, DPI/scaling).
- Perform security review appropriate for an input-automation tool and your target audience.
- Test on supported Windows 10/11 versions and configurations you claim to support.

## Summary

ByJTT AutoClicker is implemented to be a modern, minimal, accessible, privacy-respecting, and user-controlled Windows desktop utility that aligns with applicable Windows design and quality guidance for its app model. Items that cannot be "certified" from source alone (Store policy certification, MSIX/Store requirements, formal accessibility conformance, and environment-specific validation) are identified above for manual verification.
