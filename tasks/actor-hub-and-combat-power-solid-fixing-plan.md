# Implementation plan: actor-hub-and-combat-power-solid-fixing

**Status:** Plan ready for owner review (specs approved 2026-09-12; **ACs amended 2026-09-12** to match Success criteria).  
**Program id:** `actor-hub-and-combat-power-solid-fixing`  
**Map:** [docs/architecture/actor-hub-and-combat-power-solid-fixing-map.md](../docs/architecture/actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideals:** [combat-power-number-ideal.md](../docs/architecture/combat-power-number-ideal.md) · [actor-hub-and-combat-power-solid-fixing-ideal.md](../docs/architecture/actor-hub-and-combat-power-solid-fixing-ideal.md)  
**Task list:** [actor-hub-and-combat-power-solid-fixing-todo.md](actor-hub-and-combat-power-solid-fixing-todo.md)

---

## Overview

Retire dual compose (ActorHub vs `BattleStatComposer`), migrate ChannelMods combat writers into Hub, ship one Cold equip atom path, make Standing / chip / copy honest, finish lawn UniqueDemon + tree + Bound loadout parity, close sim/Θ/docs/prove gaps, then **delete** world combat stubs and track **`world-actor-combat`** (out of scope — no Hub assault build here).

**Build order:** Wave 1 fuse-first → Wave 2 Standing → Wave 3 lawn → Wave 4 prove matrix → Wave 5 stub hygiene.

---

## Spec coverage audit (2026-09-12)

Module→task map T1–T23 covers all **17** specs. First-pass ACs were weaker than Success criteria in places; **todo amended** so each Success criterion is locked in task acceptance:

| Gap closed | Where |
|---|---|
| ChannelMods full-set parity + species aptitude | T2 |
| Cold equip SourceIds / single-rebuild / ops on Hub | T3–T4 |
| Fuse baseline seeds + Compose↔Hub parity before delete | T5–T6 |
| Ops Partial-lie / ModsFor / unknown-op | T7 |
| Standing cooldown + Compose(AtomRow[]) only | T9 |
| HF-chip / HF-copy ideal ticks | T10–T11 |
| unique-lawn-wire criteria quoted + guard | T12 |
| Tree reload; Bound Funnel HP / no plant:N / no silent drop | T13–T14 |
| Coeff missing-row; prove three bullets verbatim | T16, T19 |
| Code comments in stale-docs; PlaceholderBattleTuning; O2 Θ | T18, T20, T22 |
| Map Done: ideal cross-link + golden re-bless | Program Done |

Ask-first defaults added: baseline seed re-home only; new SourceId grammar families ask first.

---

## Architecture decisions (locked in map / ideal)

| Decision | Default if ask-first unanswered |
|---|---|
| Fuse before Standing/chip/lawn honesty | Mandatory |
| One RulesetVersion bump for fuse goldens | Bump once under `battle-hub-fuse` (today `= 4` → `5`) |
| Standing membership = map §7 | Closed predicate; loot/MF stay excluded |
| D4 coeffs | Family/mask first, not full ~196 rows day one |
| World combat | **Out of scope**; delete stubs; track `world-actor-combat` |
| Assault feature-off UX | **Fail loud** (throw / no-op with no winner) until `world-actor-combat` |
| Lawn Bound wire | Implementation stays in `aptitude-sheet` `unique-lawn-wire`; this program owns Done gate |
| Stub equip | Delete as usable SSOT; no PlaceholderV2 |
| Baseline seed formulas | Re-home only — ask before formula changes |
| New SourceId grammar families | Ask first |

**No pre-work hard gates.** Specs are approved. Ask-first items ship behind the defaults above and are listed as non-blocking follow-ups in the todo.

---

## Spec coverage (all 17)

| Wave | Module | Tasks |
|---|---|---|
| 1 | `channelmods-hub` | T1–T2 |
| 1 | `cold-equip-one` | T3–T4 |
| 1 | `battle-hub-fuse` | T5–T6 |
| 1 | `battle-ops-parity` | T7 |
| 2 | `combat-membership` | T8 |
| 2 | `standing-compose` | T9 |
| 2 | `chip-honesty` | T10 |
| 2 | `copy-surfaces` | T11 |
| 3 | `lawn-aptitude-parity` | T12 |
| 3 | `lawn-tree-hydrate` | T13 |
| 3 | `bound-loadout-hub` | T14 |
| 4 | `sim-hub-parity` | T15 |
| 4 | `standing-coeff-tuning` | T16 |
| 4 | `unique-theta-wire` | T17 |
| 4 | `stale-compose-docs` | T18 |
| 4 | `prove-hub-combat` | T19 |
| 5 | `placeholder-battle-hub` (+ O2 Level-as-Θ fold) | T20–T23 |

---

## Dependency graph

```
T1–T2 channelmods ──┐
T3–T4 cold-equip  ──┼──► T5–T6 fuse ──► T7 ops
                    │         │
                    │         ├──► T8 membership ──► T9 standing ──┬──► T10 chip ──► T17 theta
                    │         │                                    └──► T11 copy
                    │         │                                    └──► T16 coeffs
                    │         ├──► T12 lawn-aptitude ──► T14 loadout
                    │         ├──► T13 tree
                    │         ├──► T15 sim
                    │         └──► T18 stale-docs
                    │
                    └──► T19 prove (needs W1–3 Done) ──► T20–T23 hygiene
```

**Parallel within wave (after deps):** T10 ∥ T11; T15 ∥ T18; T16 after T9; T17 after T10.

---

## Phases and checkpoints

| Phase | Tasks | Checkpoint |
|---|---|---|
| W1a — migrate writers + cold path | T1–T4 | Guard still allows BattleStatComposer; ChannelMods allowlist only DEBT shims; cold SourceIds + single-rebuild |
| W1b — fuse + ops | T5–T7 | No production `BattleStatComposer.Compose`; guard green; one RulesetVersion bump; ops Full |
| W2 — Standing honesty | T8–T11 | Standing includes aptitude; chip not “power”; O+S+C label |
| W3 — Lawn / loadout | T12–T14 | Bound UniqueDemon Hot; injector tree; Bound loadout via Hub/Funnel |
| W4 — Matrix / prove | T15–T19 | Sim Full; coeffs; Θ wire; docs clean; prove script green |
| W5 — Stub hygiene | T20–T23 | Placeholder gone; intel Strength dropped; stubs deleted; `world-actor-combat` tracked |

---

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Fuse golden thrash | High | Single RulesetVersion bump; triage notes in T6; freeze unrelated streams; do not multi-bump |
| Compose↔Hub channel drift | High | T5 pre-delete parity matrix green before T6 delete |
| ChannelMods re-homed wrong SourceId | Med | Full-set parity fixtures (T2); guard allowlist shrinks each task |
| `unique-lawn-wire` slips (other program) | Med | T12 quotes wire criteria + blocks T14; do not invent second lawn wire |
| Stub delete breaks world/intel tests | Med | Re-bless to feature-off / presence-only; no Hp×Level golden |
| Standing double-count equip/tree | Med | Explicit synthetic tests in T9 |
| Bound loadout silent drop / Writer abs | Med | T14 maps every absolute key; Funnel HP; owner sign-off leftovers |
| Assault UX unclear | Low | Default fail-loud; ask-first non-blocking |

---

## Out of scope (do not plan-implement)

- `world-actor-combat` (state + resolve + intel weight) — track only  
- Second power ladder; PvZ Unity rewrite; aptitude allocate UX  
- PlaceholderV2  

---

## Verification commands (program-wide)

```powershell
.\scripts\guard-actor-hub.ps1
.\scripts\guard-single-writer.ps1
.\scripts\guard-funnel-delta.ps1
dotnet test tests/FusionRpg.Core.Tests
dotnet test tests/FusionRpg.Guard.Tests
# After fuse / Standing / FE:
dotnet test tests/FusionRpg.Server.Tests   # if present / relevant filters
# FE aptitude/condition (from web/fusion-rpg-web):
npm test -- --run foldAptitudesSurfaceVm
npm test -- --run foldConditionSurfaceVm
```

---

## Open follow-ups (non-blocking)

See todo “Ask-first defaults.” Rename `world-actor-combat` only at its `/idea`. Amend in-scope specs if build discovers gaps — do not add world-combat modules under this program folder.

---

## Handoff

1. Owner reviews this plan + [todo](actor-hub-and-combat-power-solid-fixing-todo.md).  
2. On approval, `/build` starts at **T1** (or owner-named task).  
3. Stop at each Wave checkpoint for a short green review before the next wave.
