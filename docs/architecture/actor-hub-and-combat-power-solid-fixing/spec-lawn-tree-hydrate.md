# Spec: `lawn-tree-hydrate`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** HF-lawn-tree · ActorHub named follow-on  
**Wave:** 3 (after `battle-hub-fuse`)  
**Code anchors:** `PassiveTreeTuningHub` Server-only · `TreeBoundAtoms.ForPlayer` · `UniqueActorHubCompose` tree fan-in · Injector `GateCounterHost` / loop comments — hub never configured · `Battle.TreeAtomSource` retired in Wave 1 ops

---

## Objective

Injector Hot ActorHub must receive the same **passive-tree bound atoms** the Server sheet already fans in via `TreeBoundAtoms`, so lawn combat Derived includes tree grants. Today `PassiveTreeTuningHub` is **not configured** in the injector process — lawn tree is a wiring gap, not a missing vocabulary.

Success: Injector configures or injects tree tuning + player bound atoms into Hub on reload/bind; Bound/general actors show tree channel deltas on lawn Hub resolve consistent with Server for the same player tree state.

---

## Tech stack

- Core: `PassiveTreeTuningHub` / tuning DTO inject pattern (hosts load JSON — Core still no File.Read)
- Injector: RpgHost/RpgClient reload path; Hub bootstrap registers atom reader with tree bounds
- Data: existing passive-tree store reads used by Server

---

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~TreeBound|PassiveTree|AtomDerived"
# Live: allocate tree node → lawn Hub channel moves
.\scripts\guard-secondary-no-unity.ps1
.\scripts\guard-actor-hub.ps1
```

---

## Project structure

| Path | Duty |
|---|---|
| Injector host bootstrap | `PassiveTreeTuningHub.Configure` (or inject equivalent) from Server-shipped tuning payload / local seed |
| Tree atom fan-in | Same `TreeBoundAtoms` (or shared Core helper) into Injector Hub |
| Reload | On aptitude/tree reload commands, refresh bounds |
| Docs | Clear “injector tree hydrate” from named-gap lists |

---

## Code style

- Prefer shared Core helper over duplicating Server-only `TreeBoundAtoms` logic.
- No battle-only tree path (Wave 1 already Hub).

---

## Testing strategy

| Level | Cases |
|---|---|
| Unit | With tuning configured, tree atom appears in Hub contributions |
| Unit | Without hydrate, documented empty (fail test if silently ignored when configured) |
| Live/probe | Tree spend → lawn derived channel |

---

## Boundaries

- **Always:** Hub contribution; Secondary Unity-free; long.
- **Ask first:** Shipping full tuning JSON into injector vs fetching from Server.
- **Never:** Unity tree; BattleStatComposer tree slot revival; skip SourceIds.

---

## Success criteria

- [ ] Injector Hub includes tree bound atoms when player has tree state.
- [ ] Parity with Server sheet tree fan-in for same playerId.
- [ ] Named gap “injector tree hydrate” closed in actor-hub / UniqueActorHubCompose comments.
