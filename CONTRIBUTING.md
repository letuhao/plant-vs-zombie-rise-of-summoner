# Contributing to Rise of Summoner

Thanks for helping. English is the docs language for architecture and runbooks. **FusionRpg** remains the internal module prefix and binary name; **Rise of Summoner** is the player-facing product name.

Repo: https://github.com/letuhao/plant-vs-zombie-rise-of-summoner

## Writing and commits

Write like a teammate, not a template engine.

- Use direct, natural language in docs, issues, PRs, and commit messages.
- Do **not** name tooling vendors or “assisted authorship” anywhere in the repo.
- Do **not** use extra commit trailers, bot attribution lines, or watermark phrasing (`agent assert`, `agent turn`, and similar) in docs or history.
- Commit subjects: imperative, concise, focused on *why*. Example: `Add XP watermark repair path for trimmed ledgers`.

**Automated assistants:** read [AGENTS.md](AGENTS.md) — never commit or push; the owner handles all git writes.

## Quick start (developers)

1. Clone this repo.
2. Install [.NET 8 SDK](https://dotnet.microsoft.com/download) (and .NET 6 targeting pack for the Injector) and [Node 20+](https://nodejs.org/) for the web UI.
3. Read [docs/contributing/dev-setup.md](docs/contributing/dev-setup.md) and [docs/contributing/architecture-map.md](docs/contributing/architecture-map.md).
4. Build / test:

```powershell
dotnet test tests/FusionRpg.Launcher.Tests
dotnet test tests/FusionRpg.Core.Tests
dotnet test tests/FusionRpg.Data.Tests
dotnet test tests/FusionRpg.Guard.Tests
```

Player zip (needs a legal game install for Injector interop refs):

```powershell
$env:FUSIONRPG_GAME_DIR = "<your game folder with BepInEx\core and BepInEx\interop>"
.\scripts\publish-player.ps1
```

Never hardcode machine-local paths like `H:\Games\...` in code or player docs.

## Pull requests

- Match existing code style; keep diffs focused.
- Architecture lock changes go through [docs/architecture/decisions.md](docs/architecture/decisions.md) **before** large code changes.
- Include a short test plan in the PR template.
- Do not paste proprietary / third-party cheat plugin source (see [docs/research/sources.md](docs/research/sources.md)).

## Boundaries

- **Read [docs/DESIGN-GATE.md](docs/DESIGN-GATE.md) before proposing any design change.** It names,
  per subsystem, the documents you must read first. This repo runs several programs in parallel and
  its subsystems are deliberately asynchronous — proposals written without reading the relevant
  architecture are the most common source of wasted review time.
- Do **not** commit game binaries, BepInEx/MelonLoader runtimes, or large interop dumps.
- Do **not** add code that downloads or patches the PVZ Fusion game binary. “Update” means FusionRpg only.
- Dual-loading BepInEx + MelonLoader remains forbidden.
- Player zip stays self-contained; contributors need SDKs only to **build**.

## Code of conduct

See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). Security reports: [SECURITY.md](SECURITY.md).

## Suggested GitHub topics

`plants-vs-zombies`, `bepinex`, `agpl`, `mod`, `rpg`
