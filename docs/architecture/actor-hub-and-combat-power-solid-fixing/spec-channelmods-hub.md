# Spec: `channelmods-hub`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** HF-channelmods-writers · SOLID §2.15 · ActorHub sole Hot  
**Wave:** 1 (before `battle-hub-fuse`)  
**Code anchors:** `WebMatchService.StarChannelMods` / `LoyaltyChannelMods` / `UniqueCreatureAptitudeChannelMods` / `ApplyZombossPattern` (~455–668) · `AptitudeResolver.ResolveForBattle` · `DraughtProjection.Apply` · `ExpeditionResolver.ApplyInjuries` · `BossBuild.ResolveKit`/`ApplyKit` · `IActorStatSubsystem` / `AptitudeSubsystem` / `AtomDerivedSubsystem`

---

## Objective

Private `BattleChannelMod` writers that invent combat magnitudes (`combat.power.omni`, defense, aptitude edges, draughts, injuries, Zomboss kits) must **stop** feeding a parallel battle fold. They **contribute through ActorHub** (subsystems and/or bound atoms with GG-49 SourceIds) so battle/delve/siege/web consume the same numbers as lawn/sheet after fuse.

Success: Star / Loyalty / unique-creature aptitude / Zomboss pattern / draught / expedition injury magnitudes appear on Hub Derived with attributed SourceIds; new `new BattleChannelMod(` under `src/` outside the guard allowlist fails CI.

---

## Tech stack

- Core: `ActorHub`, `IActorStatSubsystem`, `DerivedModifier` / bound atoms, `AptitudeResolver` (Hub path preferred over `ResolveForBattle`)
- Server: `WebMatchService` squad build — remove ChannelMods concatenation once Hub contributors exist
- Guard: `scripts/guard-actor-hub.ps1` ChannelMods producer allowlist shrinks as files migrate

---

## Commands

```powershell
.\scripts\guard-actor-hub.ps1
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Aptitude|Star|Loyalty|Draught|Expedition"
dotnet test tests/FusionRpg.Server.Tests --filter "FullyQualifiedName~ChannelMods|BuildSquad|Aptitude"
```

---

## Project structure

| Path | Duty |
|---|---|
| New or extended `IActorStatSubsystem`(s) / atom producers | Star, Loyalty, draught, injury, Zomboss kit contributions with SourceIds |
| `WebMatchService.cs` | Stop concatenating ChannelMods once Hub path is wired for squad |
| `DraughtProjection.cs` / `ExpeditionResolver.cs` / `BossBuild.cs` | Contribute via Hub/atoms — not `BattleChannelMod` append |
| `AptitudeResolver.cs` | Prefer Hub `DerivedModifier` path; retire `ResolveForBattle` when fuse lands (may stay shim during migration) |
| `guard-actor-hub.ps1` | Drop migrated files from ChannelMods allowlist |

### Migration inventory (must move)

| Producer | Today | Target SourceId grammar |
|---|---|---|
| Star | `StarChannelMods` → omni power/defense | e.g. `star:{rank}` or documented peer |
| Loyalty | `LoyaltyChannelMods` | e.g. `loyalty:{rank}` |
| UniqueCreature aptitude | `UniqueCreatureAptitudeChannelMods` → `ResolveForBattle` | Existing `aptitude.*` via `AptitudeSubsystem` |
| Species aptitude (unused by BuildSquad) | `AptitudeChannelMods` | Same Hub aptitude path |
| Zomboss wave | `ApplyZombossPattern` Concat | Pattern → allocation → Hub |
| Draught | `DraughtProjection.Apply` | Atom / grant SourceId |
| Expedition injury | `ApplyInjuries` negative omni | Documented injury SourceId |
| Boss kit | `BossBuild.ApplyKit` | AptitudeSubsystem / atoms |

Trait/Equip atom sources folded at Compose today become Hub `AtomDerivedSubsystem` inputs under fuse — owned jointly with `battle-hub-fuse` / `cold-equip-one`.

---

## Code style

- Magnitudes `long`; widen before multiply; overflow throws.
- Every contribution carries non-empty SourceId (actor-hub-ssot §8.1).
- No new `BattleChannelMod` producers; temporary shims must say `// DEBT until battle-hub-fuse` and stay on allowlist only until fuse Done.

---

## Testing strategy

| Level | Cases |
|---|---|
| Unit | Each migrated producer: Hub Derived channel delta matches prior ChannelMods delta at same inputs (parity table) |
| Guard | New `new BattleChannelMod(` outside remaining allowlist → fail |
| Server/Core | BuildSquad / expedition / draught tests green without ChannelMods concatenation (or with shim removed) |

---

## Boundaries

- **Always:** Contribute via Hub/atoms; SourceIds; guard green; long magnitudes.
- **Ask first:** New SourceId grammar families; golden-moving magnitude formula changes beyond re-homing.
- **Never:** New parallel ChannelMods writer; cite old “composers stay separate” ADR; deepen BattleStatComposer.

---

## Success criteria

- [ ] Star, Loyalty, UniqueCreature aptitude, Zomboss, draught, expedition injury contribute through Hub/atoms (or documented one-release shim deleted in fuse).
- [ ] Guard allowlist for ChannelMods producers only contains files still awaiting fuse delete — shrinks to empty after `battle-hub-fuse`.
- [ ] Parity tests prove channel totals match pre-migration ChannelMods for the same fixtures.
- [ ] No production code path **requires** `BattleChannelMod` for these writers after fuse.
