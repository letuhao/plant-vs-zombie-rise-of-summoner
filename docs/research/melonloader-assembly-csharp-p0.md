# MelonLoader Assembly-CSharp P0 probe

Gate before LIVE MelonLoader Play (see [dual-host-roadmap.md](../injector/dual-host-roadmap.md) P0).

## Pack probed

| Field | Value |
|---|---|
| Path | `H:\Games\PvZ.Fusion.3.8.1.Multi-lang.Beta.by.Blooms\Game Files` |
| DLL | `MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll` |
| Date | 2026-08-16 |
| Compare to | BepInEx FULL MOD TOOL `BepInEx\interop\Assembly-CSharp.dll` |

Do **not** use `PVZ-Fusion-3.9_MelonLoader` for this **3.8.1** gate (different line). See [melonloader-assembly-csharp-39.md](melonloader-assembly-csharp-39.md) for the 3.9 Melon audit (`Zombie.theHealth` → **Int64**, etc.).

## Results

| Symbol | Melon (Blooms 3.8.1) | BepInEx FULL MOD TOOL | Match? |
|---|---|---|---|
| `Plant` / `Zombie` / `Board` | **`Il2Cpp.*`** namespace | **global** | Prefix differs |
| `Plant.TakeDamage` | 5 args (`Int32, IDamageMaker, DamageType, PlantType, Boolean`) | Same 5-arg shape | Yes |
| `Zombie.TakeDamage` | Same 5-arg shape | Same | Yes |
| `CreateZombie.SetZombie` | **5** params (`row, type, x, isIdle, isMindControlled`) | **4** params (`row, type, x, isMindControlled`) | **Arity delta** |
| `CreateZombie.SetZombieWithMindControl` | 4 params | 4 params | Yes |

## Workaround (implemented)

- Melon host compiles with `global using Il2Cpp;` + `Il2CppAlmanacData` / `Il2CppGameLevel` / `Il2CppTMPro` ([`GlobalUsings.Il2Cpp.cs`](../../src/FusionRpg.Injector.MelonLoader/GlobalUsings.Il2Cpp.cs)).
- Shared sources stay unqualified (`Plant`, `BoardVictory`, …). Bep uses [`InteropUsings.Bep.cs`](../../src/FusionRpg.Injector/Host/InteropUsings.Bep.cs) for `AlmanacData` / `GameLevel` / `TMPro`.
- **No `#if` in every hook.**
- `CreateZombie.SetZombie` arity delta: Harmony `SafePatchAll` may log `patch.failed` for that processor on Melon; other hooks proceed. Do not invent a second overload in shared code until LIVE confirms — use env `FUSIONRPG_MELON_SKIP_HARMONY=1` for stub-only boot if needed.

## Status

- **P0 dump: done** (Blooms 3.8.1 Melon).
- Melon host **builds** against Blooms refs with Il2Cpp global usings.
- LIVE Melon Harmony / Hello still out of CI (author smoke with `deploy-play.ps1 -LoaderHost MelonLoader`).

## How to re-dump

```powershell
$env:FUSIONRPG_ML_GAMEDIR = "<Blooms Game Files>"
.\scripts\dump-melon-p0.ps1
```
