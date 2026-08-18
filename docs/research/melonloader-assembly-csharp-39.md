# MelonLoader Assembly-CSharp — PVZ Fusion **3.9** audit

Read-only interop dump. **Do not deploy the 3.8.1-shaped injector to 3.9 until this delta is handled.**

## Pack probed

| Field | Value |
|---|---|
| Path | `H:\Games\PVZ-Fusion-3.9_MelonLoader` |
| DLL | `MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll` (~8.4 MB) |
| Date | 2026-08-16 |
| Compare to | Blooms Melon 3.8.1 + BepInEx FULL MOD TOOL 3.8.1 |

## Headline deltas (3.9 Melon vs 3.8.1)

| Symbol | 3.9 Melon | Blooms Melon 3.8.1 | Bep 3.8.1 | Impact |
|---|---|---|---|---|
| Namespace | `Il2Cpp.*` | `Il2Cpp.*` | global | Same Melon workaround (`global using Il2Cpp`) |
| `Plant.thePlantHealth` / Max / `attackDamage` | **Int32** | Int32 | Int32 | OK |
| `Zombie.theHealth` / `theMaxHealth` | **Int64** | Int32 | Int32 | **Breaks shared Writer/Apply** (`long` → `int` CS1503 / CS0266) |
| `Zombie.CurrentFirstHealth` / `CurrentAllHealth` / `TotalAllHealth` | **Int64** | Int32 | Int32 | Dump / metrics need long-safe path |
| `Zombie.TotalFirstHealth` | **Int64** | Single | Single | Type change (not just width) |
| `Plant.TakeDamage` / `Zombie.TakeDamage` | 5-arg (same) | 5-arg | 5-arg | Harmony arity OK |
| `CreateZombie.SetZombie` | **4** (`row, type, x, isMindControlled`) | **5** (+ `isIdle`) | **4** | 3.9 matches **Bep**, not Blooms Melon |
| `SetZombieWithMindControl` | 4 | 4 | 4 | OK |
| `Bullet.Damage` | Int32 | Int32 | Int32 | OK |
| `BoardVictory` / `BoardConfig` | `Il2CppGameLevel.*` | `Il2CppGameLevel.*` | `GameLevel.*` | Melon aliases already |

## Compile failure (observed)

Building Melon host against 3.9 refs fails in shared sources that treat zombie HP as `int`:

- [`EntityStatWriter.cs`](../../src/FusionRpg.Injector/Stats/EntityStatWriter.cs) — `Remember` / `ProofWrite` args from `z.theHealth`
- [`EntityApply.cs`](../../src/FusionRpg.Injector/Stats/EntityApply.cs) — `Hp` / `MaxHp` assignment
- [`GameHooks.cs`](../../src/FusionRpg.Injector/GameHooks.cs) / [`DebugActions.cs`](../../src/FusionRpg.Injector/DebugActions.cs) — dump helpers

Plant path is still Int32 on 3.9.

## What this means for LIVE

1. **3.9 is a different game line** from the Bep/Blooms 3.8.1 proofs — not a drop-in Melon twin of 3.8.1.
2. **Do not** point `FUSIONRPG_ML_GAMEDIR` at 3.9 and expect the current shared injector to build or safely write HP.
3. Blooms Melon 3.8.1 remains the pack that matches P0 / current Melon build (aside from SetZombie 5-vs-4).
4. Supporting 3.9 requires an intentional **Int64-safe zombie HP** design in Writer/Apply/dumps (and tests), not silent casts at call sites without a plan.

## Safe next steps (ordered)

1. Keep this doc as the gate.
2. Design Int64 zombie HP in shared injector (Core DTOs may already use wider types — verify before coding).
3. Build Melon host against 3.9 refs only after Writer/Apply compile clean.
4. Then `deploy-play.ps1 -LoaderHost MelonLoader` with `FUSIONRPG_ML_GAMEDIR` = this 3.9 folder.
5. Fill [`melon-live-checklist.md`](../runbook/melon-live-checklist.md) against **3.9** (separate from Bep 3.8.1 Pass rows).

## Re-dump

```powershell
# Extend scripts/dump-melon-p0.ps1 or re-run the temp audit probe against:
#   H:\Games\PVZ-Fusion-3.9_MelonLoader\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll
```
