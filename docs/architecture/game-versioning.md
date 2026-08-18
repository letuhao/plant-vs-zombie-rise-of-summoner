# Game versioning (profile × loader)

PVZ Fusion keeps shipping new builds. FusionRpg supports a **matrix of game profiles × loader hosts**, not one forever DLL.

See also: [dual-host-roadmap.md](../injector/dual-host-roadmap.md), [melonloader-assembly-csharp-39.md](../research/melonloader-assembly-csharp-39.md), catalog [`game-profiles.json`](../../game-profiles.json).

## Model

```text
GameProfile (pvzrh-3.8.1 | pvzrh-3.9 | …)
    × LoaderHost (BepInEx | MelonLoader)
    → one compiled injector DLL + DropIntoGame subtree
```

- **Shared** hooks / Writer / Server / Core — one tree.
- **Version bridges** under `src/FusionRpg.Injector/Bridges/{profile}/` absorb field width and arity (zombie HP Int32 vs Int64; `CreateZombie.SetZombie` + Harmony postfix). Shared hooks never assign `theHealth` / `theMaxHealth` or call `SetZombie` directly.
- **3.8.1 Melon** SetZombie is **5-arg** (`isIdle`); Bep 3.8.1 and Melon 3.9 are **4-arg**. One `#if FUSIONRPG_MELON` is allowed **only** inside the 3.8.1 spawn/Harmony bridge file.
- **No** runtime reflection adapters. **No** `#if` in every hook. **No** dual-load.

## Support policy

| Rule | Meaning |
|---|---|
| Current + previous | At most two active profiles; older freeze on last known-good DLL |
| New game drop | New profile id + dump doc + bridge + Drop path — not silent retarget |
| Protocol | One Server/Core; events carry injector `game` id (`pvzrh-*`) |
| Refuse | Building/deploying a profile against the wrong pack fingerprint |

## Profiles (v1)

| Id | Packs | Loaders | Zombie HP | SetZombie |
|---|---|---|---|---|
| `pvzrh-3.8.1` | FULL MOD TOOL Bep, Blooms Melon | Bep + Melon | Int32 | Bep 4 / Blooms Melon 5 |
| `pvzrh-3.9` | `PVZ-Fusion-3.9_MelonLoader` | Melon first | Int64 | Melon 4 |

Fingerprints (GameAssembly / ACS sizes) live in [`game-profiles.json`](../../game-profiles.json).

## Drop layout

```text
DropIntoGame/
  pvzrh-3.8.1/
    BepInEx/       FusionRpg.Injector.dll
    MelonLoader/   FusionRpg.Injector.MelonLoader.dll
  pvzrh-3.9/
    MelonLoader/   FusionRpg.Injector.MelonLoader.39.dll
```

Legacy flat `DropIntoGame\*.dll` and unscoped `DropIntoGame\BepInEx\` remain accepted for 3.8.1 Bep.

## Launcher

1. Detect loader (Bep vs Melon; refuse Both).
2. Resolve **game profile** from fingerprints (override via `launcher.json` `GameProfile`).
3. Install only `DropIntoGame/{profile}/{loader}/`.
4. Clear error if payload missing for that cell of the matrix.

## Author process (new Fusion build)

1. `.\scripts\dump-game-profile.ps1` → `docs/research/game-types-{id}.md`
2. Diff HP widths / TakeDamage / SetZombie / namespace
3. Add `Bridges/{id}/` + host flavor (csproj `GameProfile`)
4. Nested Drop + fingerprint row in `game-profiles.json`
5. LIVE checklist with profile + loader header
6. Update this doc’s profile table

## Env

| Var | Role |
|---|---|
| `FUSIONRPG_GAME_DIR` | Bep pack root |
| `FUSIONRPG_ML_GAMEDIR` | Melon pack root |
| `FUSIONRPG_GAME_PROFILE` | `pvzrh-3.8.1` (default) or `pvzrh-3.9` |

Deploy/publish must pass profile guards when fingerprints are known.
