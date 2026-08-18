# Mod loaders: BepInEx vs MelonLoader

Observation + **host recommendation** for FusionRpg on PVZ Fusion 3.8.1. Not an EffectBag design. Harmony hook inventory stays in [harmony-hook-map.md](harmony-hook-map.md). Effect surface stays in [effect-runtime/00-index.md](effect-runtime/00-index.md).

Locked host: [architecture/decisions.md](../architecture/decisions.md) row **Injector host**.

## Verdict

**Stay on BepInEx 6 IL2CPP** as the FusionRpg injector host on the FULL MOD TOOL pack.

MelonLoader does **not** inject deeper into `Plant` / `Zombie` / `Bullet` / `Board` than Harmony already does. Both loaders:

- Bootstrap a **.NET 6 CoreCLR** domain next to Unity IL2CPP
- Generate interop assemblies with **Cpp2IL + Il2CppInterop**
- Patch game methods with **HarmonyX** (native detours on `GameAssembly.dll` function pointers)
- Call game methods the same way (`CreateZombie.SetZombie`, `Zombie.Buttered`, field writes)

The Effect system is blocked by **game method shape** (virtual `Hit*` overrides, TakeDamage bypasses, IL2CPP trampoline AV), not by which doorstop DLL loaded first.

Switch to MelonLoader **only** if the shipping target is the Blooms **multi-lang** zip (that pack is MelonLoader-only). That is an ecosystem move, not a deeper combat engine.

**Do not dual-load** BepInEx and MelonLoader on the same `PlantsVsZombiesRH.exe`.

## Two installs on this machine

| Pack | Path | Loader | Proxy | Unity | Notes |
|---|---|---|---|---|---|
| FULL MOD TOOL (FusionRpg dev) | *(author machine example — set `FUSIONRPG_GAME_DIR`)* | **BepInEx 6** IL2CPP / net6 | `winhttp.dll` (Doorstop) | 2022.3.62f1c1 | ~149 plugins; injector output `BepInEx\plugins\FusionRpg\` |
| Blooms 3.8.1 Multi-lang | *(author machine example — MelonLoader pack)* | **MelonLoader 0.7.3** | `version.dll` | 2022.3.62 (Il2CppAssemblyGenerator) | Official 3.8.1 translation zip is **MelonLoader only** |

Blooms `Mods\` currently has:

- `PvZ_Fusion_Translator.dll`
- `Blooms_QOL.dll`
- `AudioImportLib.dll`

`Mods\CURRENT_GAME_VER` still says `3.6.1` (stale file; the pack is 3.8.1).

Interop dumps (same game types, different folder):

```text
BepInEx:     ...\BepInEx\interop\Assembly-CSharp.dll
MelonLoader: ...\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll
```

## What a loader is (and is not)

```text
PlantsVsZombiesRH.exe
        │
        ├─ proxy DLL (winhttp.dll XOR version.dll)
        │
        ├─ CoreCLR net6  ── FusionRpg.Injector (C#)
        │                      HarmonyX Prefix/Postfix
        │                      Il2CppInterop wrappers
        │
        └─ GameAssembly.dll (native IL2CPP)  ← actual game
```

The loader is the **host**. FusionRpg's Effect engine is:

```text
capture Harmony  →  Activity / combat.hit
       ↓
Core EffectBag / StatSystem  (no Unity)
       ↓
Intent pvz.*  →  EntityStatWriter / SetZombie / Buttered
```

Server, SQLite, web, and Core **do not care** which loader hosts the injector.

## Depth myth

| Claim | Reality |
|---|---|
| MelonLoader hooks “native / deeper” | Same native detours HarmonyX already uses on IL2CPP |
| MelonLoader can transpile IL2CPP | **Neither** can. Transpilers need managed IL; the game is C++ |
| NativeHook unlocks HitZombie | NativeHook is a **last-resort** function-pointer detour. BepInEx has the same via MonoMod / Dobby / Funchook (`BepInEx.cfg` `[Detours]`) |
| MelonLoader OnUpdate is safer than RpgLoop | Same Unity `Update`. FusionRpg already forbids Harmony-patching `Update` |
| Switching loaders fixes Hit* trampoline AV | Unlikely. The crash is Il2CppInterop trampoline + null Plant/Zombie args. Same interop stack on both |

### Harmony (what FusionRpg uses)

Prefix / Postfix on exported IL2CPP methods. This is the Effect capture/write path (`TakeDamage`, `Start`, `Die`, `SetZombie`, …).

Limits (both loaders):

- Inlined methods: patch does nothing
- Virtual overrides: base `Bullet.HitZombie` may not run for `Bullet_pea` (already a FusionRpg risk)
- Stripped / unhollowed signatures differ slightly between generators
- Patching some methods at chainloader time AV with other mods (FusionRpg already defers `Bullet.Hit*`)

### NativeHook (escape hatch, not the Effect engine)

MelonLoader: `MelonLoader.NativeUtils.NativeHook<T>`.

BepInEx: `INativeDetour` / configured Dobby or Funchook.

Use only if Harmony cannot attach (inlined, no wrapper, stripped). Cost: ABI, GC pinning, no Harmony patch stacking with other mods. **Do not** rebuild the Effect pipeline on NativeHook.

### Class injection

MelonLoader `[RegisterTypeInIl2Cpp]` and BepInEx `ClassInjector.RegisterTypeInIl2Cpp<T>` both register managed types into IL2CPP. FusionRpg does not need custom IL2CPP components for v1 Effects (writer + Intent is enough).

## Effect-system mapping

What Effects need vs what the loader changes:

| Prerequisite | Status today | Loader impact |
|---|---|---|
| Flat → Inc → More compose | Core StatSystem | None |
| HP/ATK Writer + TakeDamage DEF | BepInEx Harmony **WRITE-proven** | Same Harmony on ML |
| `pvz.spawn.extra` | Intent + `SetZombie` | Direct call; loader-agnostic |
| `zombie.die` onKill | Capture **CODE** | Same |
| `combat.hit` from `Bullet.Hit*` | Deferred (trampoline unsafe) | **Will not magically work on ML** |
| `pvz.status.apply` | Debug HTTP calls `Buttered` / `SetFreeze` | Direct call; loader-agnostic |
| Proc ICD / chance | Core design (B3) | None |
| Tick / aura | Prefer status timers, not `Update` | ML `OnUpdate` ≠ new mechanism |
| Coexist with translator / Better Fusion | Blooms pack is ML-only | **This** is the only strong ML argument |

See [effect-runtime/05-effect-system-prerequisites.md](effect-runtime/05-effect-system-prerequisites.md).

## Feature comparison (IL2CPP Unity 2022)

| Topic | BepInEx 6 (current) | MelonLoader 0.7.3 (Blooms pack) |
|---|---|---|
| Runtime | CoreCLR net6 | CoreCLR net6 |
| Interop | Il2CppInterop | Il2CppInterop |
| Patching | HarmonyX (`0Harmony`) | HarmonyX (`MelonLoader\net6\0Harmony.dll`) |
| Native detours | Dobby / Funchook / MonoMod | `NativeHook<T>` |
| Plugin entry | `BasePlugin.Load()` | `MelonMod.OnInitializeMelon` / `OnLateInitializeMelon` |
| Tick | `AddComponent<RpgLoop>()` (already) | `MelonMod.OnUpdate` (same Unity frame) |
| Config | `BepInEx/config/*.cfg` | `MelonPreferences` / `UserData` |
| Output folder | `BepInEx\plugins\FusionRpg\` | `Mods\` |
| Coroutines | Unity / Il2Cpp | `MelonCoroutines` (convenience) |
| SignalR.Client | Documented load risk; HTTP fallback | Same CoreCLR risk |
| FusionRpg proof | Live P1–P6 on this pack | **Not ported / not proven** |
| PvZ Fusion 3.8.1 community | Older / Chinese plugin packs | **Official multi-lang + translator** |
| Dual with the other | Officially **not** supported on 3.8.1 Blooms zip | Same |

Public comparison (jakzo, 2023): both work similarly; communities pick one; BepInEx slightly more compatible, MelonLoader slightly more convenient. That matches this game.

## Ecosystem (why Blooms is MelonLoader)

[Teyliu/PVZF-Translation 3.8.1_beta](https://github.com/Teyliu/PVZF-Translation/releases):

> This version is MelonLoader only and is **NOT** compatible with mods that require the BepInEx ecosystem.

Install docs still mention MelonLoader **0.7.1**; this copy is **0.7.3**.

Older 2.8.x zips offered **both** loaders. From 3.8 they standardized on MelonLoader.

`BepInEx.MelonLoader.Loader` exists but targets MelonLoader **0.5.7** and is a compatibility shim, not a dual-engine. Unhollowed assemblies often **differ**. Do not use it for FusionRpg.

## Dual-load

Same process cannot own both `winhttp.dll` (Doorstop) and `version.dll` (Melon proxy) cleanly. Two CoreCLR hosts, two Il2CppInterop generators, two Harmony instances on the same native methods → trampoline fights and boot failure.

Keep the two game folders **separate**. FusionRpg stays on FULL MOD TOOL. Do not copy `BepInEx\` into the Blooms `Game Files` folder.

## Port cost (if shipping onto Blooms later)

Thin **host** port. Do not rewrite Core / Server / Web / Harmony patch bodies.

| Piece | Work |
|---|---|
| `Plugin.cs` | `BasePlugin` → `MelonMod`; `Load` → `OnInitializeMelon`; log via `MelonLogger` |
| `RpgLoop` | Move `Update` body to `OnUpdate` (drop `AddComponent`) |
| Config | `Config.Bind` → `MelonPreferences` |
| csproj | Refs: `MelonLoader\net6\*.dll` + `Il2CppAssemblies\*.dll` instead of `BepInEx\core` + `interop` |
| Output | `Mods\FusionRpg.dll` (+ deps) |
| Harmony classes | Keep; re-test SafePatchAll vs translator / Blooms_QOL |
| Hit* | Still skip until a pointer-safe path exists on **that** pack |
| HTTP / SignalR | Unchanged |

Estimate: days for a boot + Hello + existing capture, **not** a reason to delay EffectBag design.

## Recommendation

1. **Effect engine** = Core (compose + future EffectBag) + Harmony Prefix/Postfix + Intent writers. Loader is not the engine.
2. **Dev default** = BepInEx on FULL MOD TOOL (proven).
3. **Player support** = two injector artifacts (BepInEx plugin + MelonLoader MelonMod), never both in one process. Roadmap: [injector/dual-host-roadmap.md](../injector/dual-host-roadmap.md).
4. Do not NativeHook the hit pipeline until Harmony Hit* is proven unsafe **and** a specific native pointer is identified.
5. Do not dual-load.

## Sources

| Source | Why |
|---|---|
| https://github.com/LavaGang/MelonLoader | Loader, CoreCLR IL2CPP, NativeHook changelog |
| https://melonwiki.xyz | Install + Il2Cpp differences |
| https://github.com/BepInEx/BepInEx | BepInEx 6 Unity IL2CPP |
| https://github.com/BepInEx/Il2CppInterop | Shared interop both loaders use |
| https://github.com/BepInEx/HarmonyX | Prefix/Postfix; native method patching |
| https://jf.id.au/blog/modding-unity-il2cpp-games | Side-by-side: both similar; community picks |
| https://github.com/Teyliu/PVZF-Translation/releases | 3.8.1 zip is MelonLoader-only |
| https://github.com/BepInEx/BepInEx.MelonLoader.Loader | Shim is 0.5.7-era; not a dual host |
| Local `MelonLoader\net6\MelonLoader.dll` | FileVersion **0.7.3.0** |
| Local `UserData\Loader.cfg` + `Il2CppAssemblyGenerator\Config.cfg` | Unity 2022.3.62, Cpp2IL 2022.1.0-pre-release.21 |
