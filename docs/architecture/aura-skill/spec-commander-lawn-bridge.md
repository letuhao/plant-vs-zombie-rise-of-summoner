# Spec: `commander-lawn-bridge`

**Program:** aura-skill · **Map:** [../aura-skill-map.md](../aura-skill-map.md) ·
**Ideal:** [../aura-skill-ideal.md](../aura-skill-ideal.md)
**Status:** specced 2026-08-30, not built. Foundation module, independent of the others.

---

## 1. Objective

**Make the commander's level and primary-stat allocation actually reach lawn plants and zombies.**

This is the Heroes-of-Might-and-Magic-III half of the owner's ask — *"their level and primary stats
distribution will vibe in pvz lawn run like HoMM3 heroes"* — and it delivers **with zero aura content**.

The whole path is already built and disconnected at exactly two points (W1, W2 in the ideal). The
machinery downstream of them is real: `AptitudeSubsystem` → `AptitudeResolver` → five
`progression.bonus.*` channels → `ActorHub.MergeAppliedCombat` (`ActorHub.cs:75-99`) →
`EntityStatWriter` → the actual Unity fields. Nothing in that chain needs building **for four of the
five channels** — `progression.bonus.defense` is the exception and reaches no Unity field at all
(§4.3, W6).

### The two breaks

**W1 — no allocation reaches the injector.** `src/FusionRpg.Injector/CheatState.cs:43-44`:

```csharp
public static ActorHub ActorHub => _actorHub ??= ActorHubBootstrap.CreateDefault(
    Stats, powerIndex: PowerIndex, aptitudeTuning: AptitudeTuningHub.Tuning);
```

No `aptitudeAllocation` delegate, so `AptitudeSubsystem.cs:43` falls back to
`_allocation = allocation ?? (_ => AptitudeAllocation.Empty)`. Its own comment calls the subsystem
*"wired into production and provably inert."* **Every one of the 486 aptitude edges evaluates to zero
on a live lawn.**

**W2 — `Θ` is never hydrated.** `InjectorPowerIndexProvider`'s own comment: *"No such source exists
yet… `ActorIndex` therefore returns 0 for every context."* At `Θ = 0` every magnitude collapses to
`P(0) = C` — *"the same floor for every build"* (`spec-aptitude-resolve.md:82-84`), which reads exactly
like a coefficient bug and is not one.

**W2 is smaller than it looks.** `InjectorPowerIndexProvider.Hydrate(StatContext, ActorLadderSnapshot)`
**already exists** (`InjectorPowerIndexProvider.cs:24`) and delegates to `HydratedPowerIndexProvider`.
It has no caller. This module supplies one.

---

## 2. Commands

```powershell
$env:FUSIONRPG_GAME_DIR = "<game folder>"
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Aptitude
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Guard.Tests
.\scripts\guard-single-writer.ps1
.\scripts\guard-secondary-no-unity.ps1
```

Live check (owner-run, per `docs/runbook/local-dev.md`): deploy, allocate commander points in the web
UI, start a lawn run, confirm plant/zombie stats move.

---

## 3. Project structure

| Path | Change |
|---|---|
| `src/FusionRpg.Injector/Stats/CommanderAllocationSource.cs` | **new** — holds the current commander allocation, refreshed from the server |
| `src/FusionRpg.Injector/CheatState.cs` | edit — pass the `aptitudeAllocation` delegate (W1) |
| `src/FusionRpg.Injector/Stats/LadderHydrationSource.cs` | **new** — builds `ActorLadderSnapshot`, calls `Hydrate` (W2) |
| `src/FusionRpg.Server/…` | edit if needed — an endpoint the injector can pull allocation + ladder from |
| `tests/FusionRpg.Core.Tests/Stats/Aptitudes/…` | **new** — resolve-with-allocation coverage |

---

## 4. Design

### 4.1 W1 — the allocation delegate

`AptitudeSubsystem` takes `Func<StatContext, AptitudeAllocation>`. The injector supplies one backed by
a cached commander allocation.

**Which allocation, and why the scope question is already settled.** The commander's allocation is
loaded **commander-scope only** — `store.LoadAllocation(AllocationScope.Commander, AptitudeEndpoints.ScopeKey(playerId))`,
the same call `WebMatchService.cs:364-372` already makes, with `ScopeKey(playerId) => $"player:{playerId}"`
(`AptitudeEndpoints.cs:67`). Because that object contains only commander-scope entries,
`AptitudeAllocation.Share(id)` over it is **already commander-relative** — `Total` sums four scopes but
three are zero. No `TotalForScope` call is needed and there is no ambiguity to resolve.

> This matters for `aura-magnitude`, which reads the same quantity. Both modules must use `Share()` on
> a commander-scoped allocation, never a per-scope share of a merged one — `AptitudeAllocation.cs:13-17`
> is explicit that *"a per-scope share, later combined, is a different (and wrong) number."*

**Refresh, not poll.** The allocation changes only when the player saves it. The injector must not poll
per-resolve: cache it, and invalidate on the existing revision/notification path the cheat document
already uses. **A per-resolve server read would violate the hot-path rule** (`overlay-control-loops.md`:
*"Never await SignalR, HTTP, or SQLite for the roll or apply"*).

### 4.2 W2 — ladder hydration

`ActorLadderSnapshot(int DaveLevel, …)` (`PowerIndexComposer.cs:33`) is the input. `Θ_actor` reads
`Wd·daveLevel + Wa·realmsAdvanced + Wr·runTerm(pvzRuns)` with `Wd = 1000‰`
(`ssot-power-scale.md:229,238,296`) — **the commander's level is the ladder's main line**, which is
precisely why this module is the HoMM3 half.

Call `Hydrate(ctx, snapshot)` once per actor per match-start (or on revision change), never per hit.

### 4.3 What actually moves on the lawn

Only the five `progression.bonus.*` channels reach a Unity field, and only four of them land:

| Channel | Fed by | Reaches |
|---|---|---|
| `progression.bonus.maxHp` | Fortitude 8000, Vigor 12000 | `EntityFinal.MaxHp` → both sides |
| `progression.bonus.atk` | Might 10000, Ferocity 6000 | `EntityFinal.Atk` → both sides |
| `progression.bonus.arm1` | Bulwark 8000 | `Arm1/Arm1Max` → **zombies only** |
| `progression.bonus.arm2` | Vigor 8000 | `Arm2/Arm2Max` → **zombies only** |
| `progression.bonus.defense` | Fortitude 10000, Bulwark 6000 | ⚠️ **composed but never written** (W6) |

**W6 — corrected 2026-08-30. "Wire it to a Unity field" is not available, and was the wrong framing.**

An earlier draft said *"either wire it or document why not."* The audit found there is nothing to wire
it to:

- **Plants have no defense-shaped Unity field at all.** `WritePlant` writes five fields, none of them
  mitigation (the only near-neighbour is `theShieldHealth`, which is vanilla PvZ's own shield int and
  unrelated to `combat.shield.*`).
- **The zombie candidates are `float`** — `z.theArmor`, `z.takeDmgMultiplier`. Routing a `long`
  magnitude through them collides head-on with the repo's own rule (*"Never `float` for a magnitude"*),
  which fails at `Θ`=232, inside normal play.
- **The live defense path never consults the hub anyway.** `GameHooks.EnsureDamageScaleCache`
  (`:589-615`) resolves `CheatState.Stats.Resolve(...)` — the **primary `StatSystem`** — once per
  cheat/pvz revision against a synthetic baseline, and stores a **global per-side** pct/flat pair
  consumed in the `TakeDamage` prefixes. It never sees `ActorHub`'s merged `EntityFinal`. So writing
  `DefenseFlat` in `WritePlant` would not reach the damage math even if a field existed.

**The `DefenseFlat` *field* is live in Core/sim** (`SimEngine.cs:345,382` via
`StatMath.ScaleIncoming`) — but ⚠️ **this family's contribution still does not reach it**, because
`SimEngine.Stats` is a `StatSystem` (`SimEngine.cs:22`), not an `ActorHub`, so the sim never sees
`MergeAppliedCombat`'s output either. The field has consumers; `progression.bonus.defense` has none.

**Decision for this module: do not attempt to wire it.** Instead, the concrete fixable defect is a
**documentation lie**, and it is already fixed: `data/seed/derived-stats/catalog.json` claimed this
family's consumer chain was `ActorHub.MergeAppliedCombat -> EntityStatWriter` — true for
`maxHp`/`atk`/`arm1`/`arm2`, **false for `defense`**. Corrected 2026-08-30; see
[derived-pipeline-audit-2026-08-30.md](../derived-pipeline-audit-2026-08-30.md).

Making per-actor defense reach the live lawn is a real piece of work (the global cache would have to
become per-entity) and belongs to whichever program wants it — **not this one.**

That narrowness is **fine and expected**: HoMM3's hero also contributed only Attack/Defense/HP-shaped
numbers. The rest of the commander's identity arrives through auras.

---

## 5. Code style

Inject seams, do not reach for statics — match `AptitudeSubsystem`'s own delegate shape. Injector code
stays Unity-free where the guard requires (`guard-secondary-no-unity.ps1`). All magnitudes `long`;
widen before multiplying; divide by 1000 last.

---

## 6. Testing strategy

| # | Test | Asserts |
|---|---|---|
| 1 | Non-empty commander allocation → non-zero `progression.bonus.atk` | W1 is closed |
| 2 | `Θ` hydrated → magnitudes differ across two `Θ` values | W2 is closed; catches the `P(0) = C` flat-floor symptom |
| 3 | `Θ = 0` still resolves without throwing | the un-hydrated path stays safe |
| 4 | Empty allocation → every edge zero | the current behaviour is preserved as a real state, not an accident |
| 5 | Share is commander-relative | a commander-scoped allocation's `Share()` matches hand-computed values |
| 6 | Allocation is not re-read per resolve | cached; a counting fake proves one read per revision |
| 7 | `MergeAppliedCombat` early-return | all-zero bonuses still return `primary` unchanged (`ActorHub.cs:82-83`) |
| 8 | Guards | `guard-single-writer`, `guard-secondary-no-unity` green |

**Live verification is required and cannot be automated** — a human must watch real plants/zombies
change. Matches the `patron-demon` and `buff-debuff-scope` T11 precedent: an owner-run checklist item.

---

## 7. Boundaries

**Always**
- Cache the allocation; refresh on revision.
- Write to Unity only through `EntityStatWriter`.
- Treat `Θ = 0` as a valid state, never a crash.

**Ask first**
- Making per-actor defense reach the live lawn at all (the global per-side cache would have to become
  per-entity). **Out of scope for this module** — see §4.3.
- Any new server endpoint.

**Never**
- Read the server on the hot path.
- Introduce a private `f(level)` — read `Θ`/`P(Θ)`.
- Use a per-scope share of a merged allocation.

---

## 8. Success criteria

- [ ] A commander with points in Might produces a measurably higher plant/zombie `atk` on a live lawn.
- [ ] Two different `Θ` values produce two different magnitudes (not the `P(0)` floor).
- [ ] No server read on the per-hit path; allocation read once per revision.
- [ ] W6 is **documented as not-wirable** (§4.3) and the `catalog.json` consumer-chain correction is in place. **Do not attempt to wire it** — there is no Unity field to wire it to.
- [ ] Core + Guard suites green; boundary guards green.
- [ ] Owner-run live check passes.

## 9. Open questions

1. **Where does the injector get the allocation — pull, or push over the existing SignalR hub?** Push
   fits the cold-loop model better (`overlay-control-loops.md`); pull is simpler. → resolve in build.
2. ~~**W6**~~ **CLOSED 2026-08-30 — do not wire.** No plant-side defense field exists, the zombie
   candidates are `float` (banned for magnitudes), and the live defense path reads the primary
   `StatSystem`, never the hub. Making per-actor defense reach the lawn is a separate piece of work for
   whichever program wants it. See §4.3.
