# Spec: `placeholder-battle-hub`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** [../actor-hub-and-combat-power-solid-fixing-ideal.md](../actor-hub-and-combat-power-solid-fixing-ideal.md) — stub hygiene B1–B4 locked  
**Wave:** 5 (after `battle-hub-fuse` + `prove-hub-combat`)  
**Code anchors:** `PlaceholderBattleResolver` · `TurnEngine` / `DistrictAssaultResolver` · `IntelRecorder` · `PlaceholderBattleTuning` · stub equip leftovers if still present after `cold-equip-one`

---

## Objective

**Stub hygiene only.** Remove world combat / intel-strength stubs that pretend to work. **Do not** design or ship world stage combat, world actor state, or Hub-fed assaults in this program.

| In scope (this module) | Out of scope (tracked elsewhere) |
|---|---|
| Delete `PlaceholderBattleResolver` + `PlaceholderBattleTuning` surface | World / district combat engine |
| Fail-loud or feature-off call sites (no silent Hp×Level wins) | World force / actor persistent RPG state |
| Drop Intel Strength/bands that depended on placeholder weight (B4) | Honest intel combat weight |
| Point docs/map at tracked program id | Implementing that program |
| Delete remaining stub equip if still shipped as usable gear | Item M4 content design beyond “no stub SSOT” |

**Tracked program (no specs yet):** provisional id **`world-actor-combat`** — world force/actor state + world combat resolve + intel strength when that layer exists. Owner starts `/idea` when ready; rename allowed at idea time.

Success: stubs gone; deferred work is a **named track**, not unfinished code; this program never claims world combat Done.

---

## Tech stack

- Core World turn / assault / intel (delete / disable only)
- Tests that blessed placeholder outcomes → re-bless to no stub combat
- Map + ideal + optional DESIGN-GATE / decisions cross-link to `world-actor-combat` track

---

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~PlaceholderBattle|DistrictAssault|TurnEngine|World|Intel|UniqueEquipment"
.\scripts\guard-actor-hub.ps1
```

---

## Project structure

| Path | Duty |
|---|---|
| `PlaceholderBattleResolver` | **Delete** |
| `PlaceholderBattleTuning` | **Delete** or stop reading |
| `DistrictAssaultResolver` / `TurnEngine` | No stub default; fail loud / combat kinds **off** until `world-actor-combat` |
| `IntelRecorder` | **Drop** Strength/bands from placeholder weight; presence (id/owner/kind) OK if non-stub |
| Map / ideal / this spec | Track `world-actor-combat` — **no** world-combat module specs under this program |

---

## Code style

Match surrounding World/Core: plain comments, no PlaceholderV2, `// DEBT` only for short-lived delete shims with a Done criterion that removes them.

---

## Testing strategy

| Level | Cases |
|---|---|
| Unit | No production resolve via Hp×Level placeholder |
| Unit | Without real engine, no invented winner |
| Unit | Intel does not call deleted Strength formula |
| Regression | World/intel tests updated for delete / feature-off |

---

## Boundaries

- **Always:** delete unfinished stub; SOLID; one ladder when real combat returns under `world-actor-combat`.
- **Ask first:** Exact feature-off UX for assaults (throw vs skip vs UI hide) at implement time; rename of tracked program id at `/idea`.
- **Never:** Hub assault mini-build here; PlaceholderV2; keep stub “until big program”; write `world-actor-combat` module specs under this program’s folder.

---

## Success criteria

- [ ] `PlaceholderBattleResolver` removed from production paths.
- [ ] No silent Hp×Level combat outcomes.
- [ ] Intel Strength/bands from placeholder weight removed (B4 drop).
- [ ] Map **Out of scope / Tracked** names `world-actor-combat` (or renamed successor); no claim of Hub world assault Done.
- [ ] Stub equip not a player-usable SSOT (align `cold-equip-one` Done).
