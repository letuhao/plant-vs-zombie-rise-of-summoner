# Spec: `battle-hub-fuse`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** FUSE-battle-hub · Q6 mandatory · SOLID / dual-compose ADR overturn  
**Wave:** 1 (depends on `channelmods-hub`, `cold-equip-one`)  
**Code anchors:** `BattleEngine.cs:38` (`BattleStatComposer.Compose`) · `BattleStatComposer.cs` · `ActorHub.Resolve` / `ResolveDerived` · `UniqueActorHubCompose` · delve/siege/web `BattleEngine` callers · `BattleRuleset.RulesetVersion` (`BattleModels.cs` = **4**) · `scripts/guard-actor-hub.ps1`

---

## Objective

**Retire `BattleStatComposer`.** Battle (and every mode that runs `BattleEngine` — web match, delve, siege, expedition resolution) obtains actor combat Derived the same way lawn/sheet do: **`ActorHub` contribute + compose**. Dual compose is the SOLID defect this program exists to kill.

Success: zero production `BattleStatComposer.Compose(` under `src/`; class deleted or test-only quarantine removed; guard allowlist no longer needs the battle grandfather; one golden / `RulesetVersion` bump if hashes move.

---

## Tech stack

- Core: `ActorHub` (+ bootstrap subsystems), battle setup → `ActorContext` / allocation identity, `CombatDerivedReader` consumers of Hub snapshot
- Server: squad → Hub compose instead of ChannelMods + BattleStatComposer
- Tests: `BattleGoldenTests`, expedition tier hashes, `BattleStatComposerTests` → Hub parity then delete
- Guard: remove `BattleStatComposer.cs` from composer allowlist when deleted; fail any new Compose caller

---

## Commands

```powershell
.\scripts\guard-actor-hub.ps1
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Expedition|Golden"
# After bump:
# re-bless battle traces / expedition tier hashes per decisions.md golden ordering
```

---

## Project structure

| Path | Duty |
|---|---|
| `BattleEngine.cs` | Build Derived via `ActorHub.Resolve` / `ResolveDerived` (or shared helper used by sheet) — **not** `BattleStatComposer.Compose` |
| `BattleStatComposer.cs` | Delete after parity; temporarily thin wrapper → Hub only if needed for one PR |
| Battle baseline seed | Level/defense/tempo/resource seed become Hub subsystems or documented Hub baseline contributors (same numbers, one gate) |
| `UniqueActorHubCompose` / battle squad builders | Same aptitude identity rules: Bound UniqueCreature vs empire species |
| `guard-actor-hub.ps1` | Drop battle grandfather; Compose callers under `src/` = 0 |
| Docs | `actor-hub-ssot` §8.3 debt → **retired**; BattleStatComposer header gone with class |

### Shared-contribution contract (mandatory)

| Input | Hub contribution |
|---|---|
| Aptitude | `AptitudeSubsystem` (commander + UniqueCreature or CreatureType per entity class) |
| Equip | `AtomDerivedSubsystem` via `EquippedBoundAtoms` / cold-equip-one |
| Tree | Bound tree atoms (Wave 1: wire for battle actors that have bindings; lawn hydrate is Wave 3) |
| Star/Loyalty/draught/injury/Zomboss | From `channelmods-hub` |
| Baseline combat flats / tempo / resources | Explicit Hub baseline subsystem(s) — **not** a second composer; document seed parity with old `BattleStatComposer` seeds |

Entity class changes **allocation identity only** — never which composer exists.

### Goldens

- Expect battle/expedition hash movement when Hub parity is imperfect then corrected — **one** `RulesetVersion` **4 → 5** (or current+1) under this module, not a drive-by bump.
- Freeze unrelated streams while re-blessing (decisions.md golden ordering).

---

## Code style

- Prefer deleting `BattleStatComposer` over eternal adapter.
- `long` magnitudes; throw on overflow.
- Breaking dependents fixed in the same wave (owner: fail-to-build is the finder).

---

## Testing strategy

| Level | Cases |
|---|---|
| Parity | Fixture matrix: old Compose vs Hub Resolve channel-for-channel before delete |
| Engine | BattleEngine runs win/loss with Hub-only Derived |
| Modes | Web match / expedition / delve boss kit still resolve |
| Guard | No `BattleStatComposer.Compose` in `src/`; no new `*Composer*` combat fold |
| Golden | Single re-bless pass documented in todo |

---

## Boundaries

- **Always:** One compose gate = ActorHub; fuse before Standing features that would deepen battle debt; SOLID.
- **Ask first:** Changing baseline seed formulas (not just re-home); multi-bump RulesetVersion.
- **Never:** Keep BattleStatComposer as intentional SSOT; new ChannelMods combat writers; “adapters OK forever.”

---

## Success criteria

- [ ] `BattleEngine` (and inherited modes) read Hub Derived only.
- [ ] `BattleStatComposer` removed from production; guard updated.
- [ ] ChannelMods allowlist empty (or only non-combat leftovers justified).
- [ ] Aptitude identity matches sheet for Bound vs empire.
- [ ] Golden bump landed once with triage notes.
- [ ] decisions.md / actor-hub-ssot §8.3 mark dual-compose debt **retired**.
