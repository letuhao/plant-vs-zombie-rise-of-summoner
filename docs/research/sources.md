# Sources

Study notes only. Paths are on the machine that has the 3.8.1 pack.

## Related public code

| Repo | Why it matters |
|---|---|
| https://github.com/Tproplay/Simple-Spawner | MIT, 3.8.1. Almanac `SelectCard` + `CreatePlant.SetPlant` / `CreateZombie.SetZombie`. No stats log. See [simple-spawner.md](simple-spawner.md) |
| https://github.com/Dynamixus/PVZRHTools-English | Infinite75 modifier fork, Apache-2.0. Older than 3.8.1. |
| https://github.com/CarefreeSongs712/PVZRHTools | Same lineage, last noted around game 3.1.1. |
| https://github.com/Infinite-75/PVZRHCustomization | Linked from this pack’s `License/NOTICE.txt`. **404 / gone.** |
| https://docs.bepinex.dev/master/articles/dev_guide/plugin_tutorial/1_setup.html | Official BepInEx 6 IL2CPP plugin tutorial |
| https://github.com/BepInEx/HarmonyX/wiki/Patching | Prefix / Postfix / Transpiler rules |
| https://github.com/LavaGang/MelonLoader | MelonLoader (Blooms 3.8.1 pack uses 0.7.3) |
| https://github.com/Teyliu/PVZF-Translation/releases | 3.8.1 multi-lang zip is MelonLoader-only |
| [mod-loaders.md](mod-loaders.md) | BepInEx vs MelonLoader comparison + host decision |

## Local files used for the 3.8.1 dump

*(Author machine examples — use your own game folder / `FUSIONRPG_GAME_DIR`.)*

Read with Mono.Cecil (does not execute the game):

```
<your game folder>\BepInEx\interop\Assembly-CSharp.dll
<your game folder>\BepInEx\core\Mono.Cecil.dll
```

Game identity from `PlantsVsZombiesRH_Data/app.info`:

- Company: `LanPiaoPiao`
- Product: `PlantsVsZombiesRH`

BepInEx log on this pack: Unity **2022.3.62f1c1**, BepInEx 6 IL2CPP, `.NET 6`. About 149 plugins already load.

## License

This repo is AGPL-3.0-or-later. Do not paste foreign plugin source into this tree. Field names (`thePlantHealth`, `theMaxHealth`, …) come from the game interop dump.
