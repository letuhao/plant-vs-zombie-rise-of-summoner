# Spec: `movement-payload` (A-M1)

**Module id:** `movement-payload` · **Status:** proposed 2026-09-03 · **Program:** [action-corpus](../action-corpus-map.md) · **Model calls: no**
**Depends on:** — (none in-program) · **Feeds:** `A-M2 lawn-reposition`
⚠️ The capability map's gate still stands — *"Not approved. No module spec may be written until it is"*
(`action-corpus-map.md:3-5`). Written ahead of approval on the owner's instruction.

**What it owns.** The **RPG-layer half of a movement action** — the buff, status or tempo effect it carries.
This is the half that is legal today and works with the game closed, and it is what makes a movement action
*standalone-first*: the reposition is lawn **enrichment**, so a movement action is enriched by `A-M2` and
never **gated** on it (`decisions.md:105`, *"Lawn position write"*; `action-corpus-map.md:76`). Concretely,
A-M1 owns the closed payload vocabulary for `category = Movement`, the deterministic rule that decides which
channels and statuses a movement action may carry, and the validator that refuses a movement action with no
standalone payload.

**⛔ Binding constraints, restated inline — a downstream session reads this file, not its links.**

1. **The LLM writes identity. Deterministic code writes magnitude.** This module is entirely deterministic;
   no model picks a number, weight, probability, duration, tier or rung — and no model touches it at all.
2. **Three pipelines, not one parameterised stage** (P-general, P-family, P-signature). A-M1 supplies the
   payload vocabulary all three draw from via the planner's pool; it is not a fourth pipeline.
3. **Permute every enum**, seeded from `(entity_id, field, sample_index)` with `sample_index` inside the
   seed — applies wherever a model reads this module's vocabulary, never here.
4. **Majority-vote only load-bearing fields;** 1-1-1 → `unresolved`, never the first option.
5. **Every enum description carries a negative clause.** `none` is a value; a missing key is a defect —
   binding on the JSON vocabulary this module publishes.
6. **TRANSIENT ≠ QUALITY** on any run that consumes this vocabulary.
7. **Small-batch proof first** — a movement payload set is reviewable before any generation round runs.
8. **Tests never call a model** — the transport stub raises. Trivially true here, and asserted anyway.
9. **The roster is 84 species (53 with family assignments), not 904.**

## 1. What exists today

### Built

| Thing | Evidence |
|---|---|
| `ActionCategory.Movement` — one of the five closed categories | `src/FusionRpg.Core/Actions/ActionEnums.cs:26-33` |
| `ActionTag.Movement` — one of the eight closed tags `A7` selects on | `src/FusionRpg.Core/Actions/ActionEnums.cs:37-47` |
| `DerivedStatChannels.ActionCategoryMovement = "movement"`, and the closed 5-member `ActionCategories` list | `src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs:474,477-480` |
| `skill.cooldown.{category}` / `skill.effectiveness.{category}` builders, registered for all five categories | `DerivedStatChannels.cs:482-486`; `DerivedStatRegistry.cs:177-180` |
| `move.range` — a registered derived channel, `FlatSum`, `StatClass.Pool` | `DerivedStatChannels.cs:525`; `DerivedStatRegistry.cs:237` |
| 21 statuses in the shipped catalog, including CC (`freeze`, `cold`, `kelp`/slow, `hypno`) and buff/debuff (`rally`, `expose`) | `src/FusionRpg.Core/Status/StatusCatalogBootstrap.cs:16-58` |
| `ActionSeeder.Generate` gates `Area` shapes on `boardAvailable` — a board-free action is already a first-class case | `src/FusionRpg.Core/Actions/Seeding/ActionSeeder.cs:50-53` |
| The rung table, with per-row `structureBudget` | `data/tuning/action-rungs.v1.json:12-21` |
| Tunables live in `data/tuning/<domain>.v{n}.json`, published through `tools/tuning/publish.py` | repo standard, `docs/architecture/tunables-ssot.md` |

### Wiring gap

| Thing | Evidence |
|---|---|
| **`move.range` has no production reader.** It is registered and reserved, and nothing consumes it | `DerivedStatRegistry.cs:237` (registration) and `Balance/Guards/DominanceGuard.cs:103` (reserved list) are its only two mentions outside the constant itself |
| **`skill.cooldown.*` / `skill.effectiveness.*` have no production reader either** — registration plus the same reserved list | `DerivedStatRegistry.cs:177-180`; `DominanceGuard.cs:118-119`; no cooldown resolver reads them (`src/FusionRpg.Core/Actions/Duration/` contains no reference) |
| `Instantiator.TryInstantiate` — doc-comment references only, no production caller | `Instantiator.cs:92`; `InstanceProducer.cs:22`, `Resolver.cs:28`, `RpgStore.AtomInstances.cs:104` |
| `OnActivate` is authorable but raised nowhere in the injector | `decisions.md:97` (amended 2026-09-03); `effect-atom-map.md:317` (E33) |

**These are wiring gaps, not architectural walls, and the distinction is the whole point of this module.**
The RPG layer already expresses movement — a category, a tag, a range channel, two tempo channels and a
status system. What is missing is a reader for three registered channels. *"Does the lawn support movement"*
is the wrong question; *"is the movement path wired end-to-end"* is the right one, and the answer is "not
yet, in three named places."

### Real gap

- **The payload vocabulary itself.** Nothing anywhere says which channels and statuses constitute a legal
  movement payload, so nothing can refuse a movement action that carries none.
- **The standalone-first check.** No validator asserts that a `category = Movement` action resolves with
  `boardAvailable = false`. `ActionSeeder` gates the *target shape* on the board (`ActionSeeder.cs:50-53`);
  nothing gates the *payload*.
- **`type-weights.json`** — named by `spec-action-seeding.md:173,176` and **does not exist**
  (`action-corpus-map.md:33`). A-T1 owns it; A-M1 only notes that the movement weight has no home yet.

## 2. The API and the write path

**There is no Unity write path in this module.** That is the design: everything here resolves in the RPG
layer, during a lawn match or with the game closed, and touches no `Plant`/`Zombie` field. The Unity write
belongs to `A-M2` and is the only part that needs one.

### The data — `data/tuning/movement-payload.v1.json`

A published tunable, not code, because a balance pass would change which payloads a movement action may
carry. Three closed lists, each entry carrying a `description` with a **negative clause**:

- **`channels`** — the derived channels a movement payload may write. Ships with `move.range`,
  `skill.cooldown.movement`, `skill.effectiveness.movement`. Each row states what it is not (*"`move.range`
  is how far an actor may reposition; it is not attack reach and not movement speed"*).
- **`statuses`** — the status ids a movement payload may apply, drawn from the shipped catalog
  (`StatusCatalogBootstrap.cs:16-58`), never a new vocabulary.
- **`payloadKinds`** — the closed set `buff | status | tempo | none`, where `none` exists so a planner can
  state "no payload" explicitly rather than by omission, and where the validator then rejects it for a
  movement action.

No magnitudes anywhere in this file — ids and descriptions only. Magnitudes come from the atom roll
(`ActionSeeder.Generate` → `Instantiator.Draw`, `ActionSeeder.cs:47`), which is the one roll (Law 1).

### The API — `FusionRpg.Core.Actions.Movement.MovementPayloadPolicy`

A pure, deterministic policy class in Core with no Unity reference, three members:

- `IsLegalPayloadChannel(string channel)` — membership in the published `channels` list.
- `IsLegalPayloadStatus(string statusId)` — membership in the published `statuses` list, cross-checked
  against the shipped status catalog so a typo in the tuning file is a load-time failure, not a silent
  no-op.
- `HasStandalonePayload(CompiledAction action)` — **the load-bearing one**: true when the action carries at
  least one legal payload channel or status that resolves with **no board**. A `category = Movement` action
  for which this is false is rejected by `ActionValidator`, with the rejection message naming the action id
  and the reason *"a movement action must do something with the game closed."*

**⛔ CORRECTED 2026-09-03 (review) — `category` is not a field on either action type today.** This
API reads `action.Category` and there is no such member. `ActionRow`'s complete field list is
`ActionId, Name, Kind, Rung, Tags, Enabled, Revision, Grantable, DefaultAttackEligible, ContainerId,
Envelope, Targeting, MinRange, MaxRange, RangeChannel, RequiresLineOfSight, ConditionsJson`
(`ActionRow.cs:15-54`), and `CompiledAction.cs:17-35` matches it member for member. `ActionCategory`
**exists** as an enum (`ActionEnums.cs:26-33`) but it names derived channels
(`ActionCategories.Name` → the `DerivedStatChannels` constants, `ActionEnums.cs:96-104`) and **sits on
no row**.

**This is a wiring gap with a named owner, not a wall.** `A-E1` widened its scope on 2026-09-03 to own
the whole schema surface, and `category` is the second row of its own table — which names *"A-M1's
`category = Movement` rejection"* as one of the five things depending on it
(`spec-eligibility-axis.md` §3.0, AC1b). So:

- `MovementPayloadPolicy` is **specified against A-E1's widened row**, and this module is unbuildable
  until A-E1's `category` field lands — recorded in §6's dependency table rather than discovered at
  build time.
- The signature stays `HasStandalonePayload(CompiledAction action)`; what changes is that
  `action.Category` is a real member by then. Nothing here re-derives a category from tags or from a
  container, which would be a second categories vocabulary — the exact defect
  `spec-action-seeding.md` §3 names, and the one A-E1 AC1c forbids in the same words.

The policy is read by the planner (`A-S1`, to build the pool a movement brief is allowed to draw from) and
by the validator (to refuse a bad one). It is never read on a hot path.

### Where the payload actually resolves

Unchanged from today's shipped stack, and stated so a later session does not "fix" it into a Unity write:
the payload is atoms over derived channels, composed by the actor hub and applied through the existing
apply paths — `EntityApply` / `EntityStatWriter` for stats (`src/FusionRpg.Injector/Stats/EntityApply.cs:84-89`),
the Unity CC executor for status, FA10 Add for HP. A movement payload needs **none** of those to be widened.

## 3. What it must NOT do

- **Never write a Unity position.** That is `A-M2`, one guarded entry point, and it is blocked on E33.
- **Never gate a movement action on the board.** A movement action with no reposition is a *smaller* action,
  never an illegal one. This is invariant 9 (standalone-first) and it is the reason this module exists.
- **Never invent a status or a channel.** The status catalog and the derived-stat registry are the
  vocabularies; a movement payload references them and never extends them.
- **Never carry a magnitude in the tuning file.** No range values, no durations, no cooldown multipliers —
  the roll owns those.
- **Never read PvZ state.** The overlay observes events and contributes signed deltas; it does not read the
  game's current values to decide a payload.
- **Never become a fourth generation pipeline.** A-M1 publishes a vocabulary and a policy; the three model
  stages consume it through the planner's pool.
- **Never assume `move.range` has a reader.** Writing to a channel nothing reads is inert, and this module
  must say so in its own report rather than let a green test imply a working feature.

## 4. Testing strategy

1. **Stubbed transport that raises.** This module makes no model call by construction; a test installs a
   raising transport and runs the whole policy path under it anyway, so a later refactor that reaches for a
   model fails immediately (`tools/seedsmith/tests/test_classify_pipelines.py:36 (NOT test_offline_guarantee.py — that file PERMITS 127.*/localhost/::1/0.0.0.0, which is exactly where the model runs: llm_caller.py:40 endpoint http://localhost:1234):1-8` is the precedent).
2. **Determinism / replay.** `movement-payload.v1.json` loaded twice → identical policy state, asserted by
   hash over the canonical serialisation (sorted keys, fixed indent, `\n`). The same action evaluated twice
   → the same `HasStandalonePayload` verdict, and the verdict does not depend on evaluation order or on any
   ambient board state.
3. **Planted violations**, one test each, all expected to be caught:
   - a movement action whose only effect is a reposition → `HasStandalonePayload` false → validator rejects,
     message names the action id;
   - **a Unity type referenced from this module's own source** → the new `FusionRpg.Guard.Tests` case
     exits 1 naming the file. A test asserting `guard-secondary-no-unity.ps1` catches it would pass
     for the wrong reason: that guard never reads this directory (`guard-secondary-no-unity.ps1:9,37-40`);
   - a tuning row naming a status not in `StatusCatalogBootstrap` → load-time failure, not a silent skip;
   - a tuning row naming an unregistered derived channel → load-time failure;
   - a tuning row carrying a numeric value → load-time failure (the file is ids and prose only);
   - a `payloadKinds` list missing `none` → schema test fails;
   - a description with no negative clause → test fails.
4. **An inertness test that tells the truth.** One test asserts, and names in its failure message, that
   `move.range` has no production reader — so the day one lands, the test goes red and someone updates this
   spec instead of the claim quietly rotting.

## 5. Acceptance criteria

1. `data/tuning/movement-payload.v1.json` exists, is published through `tools/tuning/publish.py`, and
   contains **no numeric value of any kind**.
2. Every entry in every list carries a `description` containing a negative clause, asserted mechanically.
3. `payloadKinds` admits `none`; every field is required; unknown keys are rejected at load.
4. `MovementPayloadPolicy` lives in `FusionRpg.Core` and references no Unity type — asserted by a
   test in `FusionRpg.Guard.Tests` that scans **this module's own files**, not by
   `guard-secondary-no-unity.ps1`. ⛔ **CORRECTED 2026-09-03 (review):** that criterion was
   **vacuous**. The guard sets `$PluginDir = src\FusionRpg.Core\Effects\Plugins`
   (`scripts/guard-secondary-no-unity.ps1:9`) and enumerates only that directory
   (`:37-40`), so a class in `FusionRpg.Core.Actions.Movement` is never scanned and the guard passes
   whatever this module does. A criterion a guard cannot fail is a comment. Either the new test
   above, or a reviewed extension of the guard's scanned roots with the same justification written
   beside it — not the unchanged guard.
5. Every status id in the tuning file resolves in the shipped status catalog, and every channel resolves in
   the derived-stat registry — both checked at load, both failing loudly.
6. `ActionValidator` rejects a `category = Movement` action for which `HasStandalonePayload` is false, and
   the rejection names the action id and the reason.
7. A movement action with a legal payload validates and compiles with `boardAvailable = false`.
8. No code path in this module assigns any Unity field; `guard-single-writer.ps1` stays green.
9. The full test module passes with the transport stubbed to raise.
10. The module's own report states, in plain words, that `move.range`, `skill.cooldown.*` and
    `skill.effectiveness.*` currently have no production reader — no acceptance claim implies otherwise.

## 6. Dependencies and cross-program hazards

| Needs | From | State |
|---|---|---|
| **A `category` field on the action row** | **A-E1** `eligibility-axis` | ⛔ **does not exist** — `ActionRow.cs:15-54` / `CompiledAction.cs:17-35` carry no `Category`; A-E1 §3.0 owns it and names this module's rejection as a dependant |
| The movement pool a brief may draw from | **A-S1** `distribution-planner` | does not exist |
| Per-species movement weighting | **A-T1** `type-weights` | `type-weights.json` **does not exist** (`action-corpus-map.md:33`) |
| The reposition half | **A-M2** `lawn-reposition` | drafted, not built, **blocked on effect-atom E33** |
| `OnActivate` raised on the lawn | **effect-atom E33** | absent from `EffectDtos.EffectTriggers`, raised nowhere (`effect-atom-map.md:317`) |
| Channel pools · binding production | **effect-atom E30** · **effect-pipeline module 4** | outside this program; `effect_binding` has zero rows |

**Hazards.**

1. **Three registered channels with no reader.** A movement payload written entirely in `move.range` is
   inert today. The honest framing is a **wiring gap with three named lines**, and the plan should decide
   whether A-M1 ships a reader for at least one of them or ships payloads over channels that already have
   consumers (statuses do).
2. **A movement action without `A-M2` may read as pointless in play.** That is a design risk, not an
   architectural one — standalone-first says it must *work*, not that it must feel complete. The smoke batch
   is where that gets judged, by the owner, on evidence.
3. **The rung window has no entry in the caps register.** `ssot-power-scale.md` §11 has no row for it and
   §5 constraint 2 promised one (`action-corpus-map.md:132`). A movement payload's rung band inherits that
   gap.
4. **`AFFIX_SCHEMA`-style vocabularies must not be forked.** If a movement payload needs a new status, it is
   a reviewed change to the status catalog, not a second list in this file — inventing a third vocabulary is
   the exact defect the atom program exists to stop.
