# Security Policy

## Supported versions

Security fixes apply to the latest GitHub Release of FusionRpg. Older player zips may not receive backports.

## Reporting a vulnerability

Please **do not** open a public issue for security problems.

1. Prefer [GitHub Security Advisories](https://github.com/letuhao/plant-vs-zombie-rise-of-summoner/security/advisories/new) on this repository.
2. If that is unavailable, contact the maintainer privately through GitHub (see profile for `@letuhao`).

Include: FusionRpg version, OS, steps to reproduce, and impact. We will acknowledge when we can and coordinate disclosure.

## Scope notes

- FusionRpg is a **localhost** overlay (server on `127.0.0.1`, no internet auth model). Treat LAN exposure as out of design scope unless documented otherwise.
- Player builds are **unsigned hobby OSS**. Antivirus false positives on `FusionRpg.Server.exe` are expected for some products; see [docs/runbook/players.md](docs/runbook/players.md#trust--antivirus-unsigned-hobby-builds). The launcher never asks users to disable antivirus entirely.
- Reports about **game piracy, cracking, or bypassing game DRM** are out of scope.
- Do not attach proprietary third-party plugin source or illegal game binaries.
