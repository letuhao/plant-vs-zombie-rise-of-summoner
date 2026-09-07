# ActorHub enforcement audit — 2026-09-07 (refreshed 2026-09-08)

**Status:** measurement for the sole-Hot-gate + FULL GG-49 program.
**Code beats docs.** Line cites verified in this session.

---

## 1. Owns / bans (Ideal)

| Plane | Owner | Ban |
|---|---|---|
| Cold UniqueActor identity / FSM / SQLite | `FusionRpg.Data` | Hub must not own specimen rows |
| Hot primary compose | `StatSystem` | — |
| Hot derived + AppliedCombat merge | **`ActorHub`** | Private folds; persisting Derived / AppliedCombat / contribution history as SQLite SSOT |
| Battle derived | `BattleStatComposer` | **Locked separate** (class-system 2026-08-26; ADR 2026-09-07) |
| Player PvzStats sheet cache | `pvz_stat_contributions` | Do not bridge to ActorHub bags |

---

## 2. Host fan-in (asymmetric — honest)

| Host | Subsystems / bound atoms today | Gap |
|---|---|---|
| **Server** (`UniqueActorHubCompose`) | progression + aptitude + **equip** (`EquippedBoundAtoms`) + **tree** (`TreeBoundAtoms`) | No Hot status; no session grants |
| **Injector** (`CheatState.ActorHub`) | progression + aptitude + **grants** + **status L2b** | **No tree** — `PassiveTreeTuningHub` is Server-only (`InjectorLoop` comment). Lawn equip arrives as **`grant:`** via bag (do not also re-read bindings — double-count). |

Closing Injector tree is a **separate hydrate program**, not claimed closed by the Hub gate.

---

## 3. Contribution SourceIds (FULL grammar)

| Producer | SourceId | Evidence |
|---|---|---|
| Progression | `rpg.progression` | `ContributionSourceIds.Progression` |
| Aptitude | `aptitude.{Share}` | `AptitudeResolver` |
| Equip | `equip:{role}:{itemRef}` | `EquipAtomSource` + `EquippedBoundAtoms` (Server battle + sheet) |
| Tree | `tree.{treeId}.{nodeId}` | `TreeAtomSource` / `TreeBoundAtoms` (Server) |
| Status | `status:{instanceId}` | `StatusStatPayload` (Injector) |
| Grant | `grant:{effectOrGrantId}` | `GrantedDerivedAtomReader` |
| Primary (sheet) | `primary:{kind}\|{id}` | `UniqueActorHubCompose.ProjectSheet` |

Empty SourceId is skipped by `AtomDerivedSubsystem` (§8.1).

---

## 4. Bypass inventory

| Site | Disposition |
|---|---|
| `GameHooks.EnsureDamageScaleCache` | **Fixed** → `ActorHub.Resolve` → AppliedCombat; guarded |
| `SimEngine` apply sites | **Fixed** → `ActorHub.Resolve`; guarded |
| §6.1 patron / stars / injuries / contracts | Stay on `stat.derived` → atom path |
| `BattleStatComposer` | Exception-listed |

---

## 5. CI

`scripts/guard-actor-hub.ps1` — Server compose, EntityApply/GameHooks/SimEngine Hub calls, Injector/Sim `Stats.Resolve` ban, Program `EquippedBoundAtoms`, `/derived` composeKind. Guard.Tests include a negative fixture.

---

## 6. Verdict

Gate + Server FULL durable attribution shipped. **Injector tree hydrate** and **FE InspectSplit** remain follow-ons. Lawn vs sheet SourceId labels for equip may differ (`grant:` vs `equip:`) until push tags equip grammar without double-count.
