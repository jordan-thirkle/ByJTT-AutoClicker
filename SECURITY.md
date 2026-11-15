# Security Policy

## Supported versions

Security fixes are provided on a best-effort basis for the latest version on the default branch.

## Reporting a vulnerability

If you discover a security issue, please avoid posting exploit details publicly before a fix is available.

Report it privately to the maintainer (for example, via a GitHub private security advisory for this repository, if enabled) and include:

- A clear description of the issue and potential impact
- Steps to reproduce or a proof of concept
- Affected version/commit and environment details (Windows version, .NET version)
- Any suggested mitigation, if known

I'll aim to acknowledge reports and work toward a fix as soon as practical.

## Scope notes

ByJTT AutoClicker is a local Windows desktop utility that simulates mouse input at the user's request.

- **Local-only:** no network services, telemetry, analytics, accounts, or remote control features are implemented
- **Privileges:** runs as a standard user (`asInvoker`); it does not require elevation
- **User control:** actions are user-initiated with confirmation options, stop controls, and an optional failsafe

## Responsible use

Use automated clicking only where it is permitted and in compliance with applicable laws and third-party terms. Do not use or modify this software to bypass security controls or to enable abuse.
