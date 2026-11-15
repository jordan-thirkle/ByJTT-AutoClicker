# Contributing to ByJTT AutoClicker

Thanks for your interest in improving ByJTT AutoClicker.

## Guidelines

- Keep the app dependency-free (standard library only) unless there is a strong reason.
- Preserve keyboard accessibility, clear labeling, and safe stop/failsafe behavior.
- Do not add telemetry, networking, or data collection.
- Test changes on Windows with keyboard-only interaction and at common DPI scaling settings when possible.

## Development

1. Fork and clone the repository.
2. Create a branch for your change.
3. Run and verify locally:
   - `py -3 -m py_compile autoclicker.py`
   - `py -3 autoclicker.py`
4. Open a pull request with a clear description of the change and how you tested it.

## Reporting issues

Please include your Windows version, Python version, steps to reproduce, expected behavior, and actual behavior.
