# Spec: action-instance-and-grant (A21)

Module **A21** in the [action map](../action-map.md) §14. Depends on A11 (`unlock-ladder`), A12
(`rung-table`), A13 (`action-seeding`), A15 (`grant-seam`), A16 (`loadout`), A23
(`cost-scaling-holder-rung`) — all built, A23 building first in this reopening. **This is the module
that makes a real player able to hold a real, generated, priced action at all** — every downstream
piece (dispatch, costs, cooldowns, loadout resolution, battle) is already built and proven; nothing
upstream of a grant has ever run in production.

> **Read `action-map.md` §14 before this spec.** It traces why every piece this module orchestrates
> already exists with zero non-test callers, and names the one place this exact shape already runs in
> production (`RpgStore.ProduceAndBind`, unique-equipment binding) — read for the pattern, not copied
> verbatim, because this module's roll happens at a different time for a different reason (§1 below).

## Objective — three built mechanisms, no orchestrator, and one design fork resolved by re-reading the map's own words

Traced directly against shipped code, not assumed from module names:

1. `ActionEligibility.Candidates(actions, actorSpeciesKey, familyOf)` (A-E1) is a **pure filter over
   an already-supplied `IEnumerable<ActionRow>`** (`ActionEligibility.cs:31-59`) — it does not read a
   catalog, does not roll anything, and does not know `ActionSeeder` exists. Its own test
   (`AuthoredEligibilityResolvesTests.cs`) proves it resolves the real `committed-round-{1,2}.json`
   corpus's `id`/`scope`/`scopeKey` fields correctly — but constructs bare `ActionRow`s carrying only
   those three fields, never atoms, envelope, or cost. **Nothing populates `rpg_action` with this
   corpus today** — confirmed by grep: zero references to `committed-round` anywhere in `src/`.
2. `ActionSeeder.Generate` (A13) rolls a concrete atom subset from a weighted pool — but the corpus
   briefs (`data/seed/actions/committed-round-*.json`) carry `atomFamilies` (e.g. `"atom.sporing"`,
   `"atom.volley"` — **families**, not atom ids) alongside a stable, shared `id`
   (`"action.family.cactus.001"`), a `category`, a `rungBand`, `targetMode`/`relation`. This is a
   **brief**, not a finished row: something must still resolve family membership to concrete atoms.
3. **The design fork this spec resolves, by re-reading `action-map.md` §10.5a rather than assuming
   the item-equipment pattern applies unchanged**: *"a granted action has no instance and no rolls"*
   — stated there specifically to distinguish `rpg_action_grant` from `effect_binding`. **A granted
   action is never rolled per-player.** The roll that turns a brief's `atomFamilies` into concrete
   atoms happens **once, at import**, producing one shared, static `rpg_action` row every later
   holder receives identically — the same shape a WoW talent or a FF materia has, not a rolled loot
   affix. This is why A21 does **not** reuse `ProduceAndBind`'s per-player-`WorldSeed`-derived seed —
   that pattern exists specifically because equipment DOES vary per player. A brief's roll seed is
   derived from the brief's own stable `id`, so two servers importing the same corpus produce
   byte-identical content, and re-running the import is idempotent by construction.
4. `ExecuteSummon` (`RpgStore.Summons.cs:28-174`, the only production specimen-mint path) grants a
   new demon **zero** actions — and per point 3, **this is correct today, not a gap**: the unlock
   ladder's own contract (`spec-unlock-ladder.md`) prices `earnCount` as *"successful acquisitions ...
   only when a slot was free"* — a fresh Level-1 specimen with `EarnCount = 0` has earned nothing yet
   by design. **The real gap is that nothing ever advances `EarnCount` for a real specimen**, because
   nothing calls `UnlockState.TryAccept` in production, and `UnlockState` has no persistence at all
   (`RpgStore.ActionUnlocks.cs`, named in `spec-unlock-ladder.md`, was never built — confirmed by T21's
   own evidence in `action-todo.md`).
5. **⛔ Corrected during this spec's own audit pass — the first draft named the wrong seam.**
   `RpgProgression.cs`'s `ILevelChangeHandler`/`LevelChangePipeline.Run(LevelChangeEvent e)`
   (`RpgProgression.cs:246-280`) looked like exactly the right per-level-up hook — until reading
   `LevelChangeEvent`'s own fields (`PlayerId`, `Kind`, `TypeId` — `RpgProgression.cs:137-148`) against
   where it is actually raised (`RpgStore.Progression.cs:155-156`, `RpgXpApply.Apply(kind, state,
   delta, playerId, typeId, reason)`). **This event is player- or species-mastery-scoped, keyed by
   `(playerId, kind, typeId)` — it carries no specimen `instanceId` at all.** A specific summoned
   demon's own level lives on a completely different, direct path:
   `RpgStore.AwardUniqueActorXpUnlocked(db, instanceId, delta)` (`RpgStore.UniqueActors.cs:1333-1368`)
   computes `level` inline (a plain loop against `RpgXpCurve.XpToNext(RpgActorKinds.Specimen, level)`)
   and writes it directly with **no event raised, no callback, no extension seam of any kind** — its
   only two production callers are `UniqueActorService.AwardXp` and the expedition reward apply
   (`RpgStore.Expeditions.cs:318`). `ILevelChangeHandler` genuinely does not apply to a specimen's own
   level at all; §4 below is corrected accordingly.

**What "done" looks like:** a corpus of real, priced, category-tagged actions is imported once into
`rpg_action`/`rpg_action_cost`; a real specimen's level-ups roll against the unlock ladder for one it
does not already hold, from the pool it is eligible for; a successful roll grants it via the already-
proven `RpgStore.UpsertGrant` → `ActionSetAssembler` → `LoadoutSet` → `BattleEngine.Resolve` chain.

## Design (locked on approval)

### 1. Corpus import — once, content-seeded, idempotent

A new, small orchestrator (`ActionCorpusImporter` or similar, `Core/Actions/Corpus/`) that, given the
parsed `committed-round-*.json` entries, for each brief not already present (checked by `ActionId`,
matching `UpsertAction`'s own revision-bump guard so a re-import of an unchanged brief moves nothing):

1. Rolls the brief's `atomFamilies` into a concrete atom subset via the **same** `Instantiator.Draw`
   mechanism `ActionSeeder` already wraps — reused directly, not re-implemented — seeded from
   `Fnv1a64`-style hash of the brief's own stable `id` (mirroring `SeededRng.DeriveStream`'s existing
   "seed XOR stable name" shape used everywhere else in this codebase), **never** a per-player
   `WorldSeed`. Two imports of the same brief on two servers produce byte-identical containers.
2. Mints a concrete `ContainerRow` (`Kind = Skill`) plus its atom rows via the already-built
   `RpgStore.UpsertContainer` (`RpgStore.Containers.cs:216`) — a new container per brief, keyed
   `container.action.{briefId}` or similar, never reusing a shared template container (that would
   make every holder's container identical by aliasing, which is true here anyway since the roll
   itself is content-seeded — named so a future session does not "fix" it into a per-player roll by
   mistake).
3. Composes the full `ActionRow`: `ActionId`/`Scope`/`ScopeKey`/`Category`/`RungBand` straight from
   the brief (byte-for-byte, matching `AuthoredEligibilityResolvesTests.cs`'s own mapping); `Rung =
   RungBand.Collapse()` (the existing, already-decided rule — `ActionRow.cs:104` — though this
   importer would be its first real caller: `grep` finds zero today, only the method's own doc
   comment); `Targeting` compiled from
   `targetMode`/`relation`; `ContainerId` from step 2; **`Envelope` and `MinRange`/`MaxRange` come
   from a new, small per-category template** (§2 below) — the brief itself carries no timing data,
   and inventing one per-brief would be exactly the "private `f(level)`"/per-row magic-number defect
   `tunables-ssot.md` forbids.
4. Persists via `RpgStore.UpsertAction`, then one `ActionCostRow` per the same per-category template,
   scaled by the brief's own `RungBand.Collapse()` (the **authored** rung — `ActionCostRow.AmountSpec`
   is a `ValueSpec`, scaled later, per-holder, by A23's corrected `CostLedger` at grant/battle time;
   the import step authors the base amount, never the holder-scaled one), via `RpgStore.UpsertCost`.

**Where this runs**: a `FusionRpg.Server` startup step (mirroring how `BattleTuningHub.Configure`/
`RungPolicy` already bootstrap from files at startup), gated behind a config flag defaulting to
**on** — content that exists and is idempotent to re-import needs no separate trigger. **Never** a
live game/injector path; this is server-side catalog population, matching T30's own "actions are
battle-mode and the injector never sees one."

### 2. The per-category envelope/cost template — a new, small tunable

**Real gap, named honestly**: `effect-pipeline-ideal.md` §3.4 already named this exact number as
undeclared — *"`skill` (action): ~1-5, 'we will fine tune later'... no tuning file declares them, per
kind or at all"* — for roll COUNT bands; the same is true here for envelope timing and cost amounts
per category. This module authors `data/tuning/action-corpus-templates.v1.json`: one row per
`ActionCategory` (`Attack`, `Defense`, `Support`, `Movement`, `Status`), each giving `WindupTicks`,
`RecoveryTicks`, `CooldownTicks`, `Class` (`CooldownClass.Specific` — T56.4's own found lesson: `None`
silently no-ops both the check and the arm), a `CooldownChannel`/`EffectivenessChannel` matching the
category (`DerivedStatChannels.SkillCooldown/SkillEffectiveness(category)`, S2's existing pattern),
and one `(resourceId, baseAmountAtRung1, timing)` cost row. **Shipped with a stated derivation and a
named re-tune trigger** (`action-corpus-ideal.md` §36's own precedent for "default now, re-tune later
is what a tunable is for") — not left as an open balance question this module cannot close.

### 3. `UnlockState` persistence — the missing table

`RpgStore.ActionUnlocks.cs` (named, never built): `rpg_actor_unlock_state(owner_kind, owner_key,
earn_count)` + `rpg_actor_held_unlock(owner_kind, owner_key, unlock_id, earn_count_at_acceptance)` —
reusing `OwnerScope`/`OwnerKind` (the `UniqueActor` kind, already built for exactly this durability
shape per `decisions.md`'s 2026-09-02 row) rather than inventing a ninth scope concept. Round-trips
through `UnlockState.FromPersisted`/`.EarnCount`/`.Held` unchanged — this table is pure persistence
for an already-fully-specified, already-tested pure class; no new game logic here.

### 4. The grant trigger — inside `AwardUniqueActorXpUnlocked`'s own transaction, not a new event bus

**Corrected shape**, matching where the specimen's level transition is actually computed
(`RpgStore.UniqueActors.cs:1333-1368`), not a generic progression event that does not carry a
specimen identity at all (§Objective point 5). `AwardUniqueActorXpUnlocked` widens its own return
tuple with one trailing, additive field: `LevelsGained: int` (`level - row.Level`, computed where the
loop already tracks both) — the same "trailing, additive, zero call-site rewrite needed" shape
`CompiledAction.Category`/`ActionCostRow.AllowLethal` already established elsewhere in this program.

The roll-and-grant logic itself is a new `ActionUnlockGrantService` (`Core/Actions/Unlock/`), pure and
DB-free like its sibling `UnlockDiscardService` (T20) — same injected-delegate seam
(`Func<string, UnlockState> loadUnlockState`, `Action<string, UnlockState> saveUnlockState`,
`Func<IReadOnlyList<ActionRow>> catalog`, `IReadOnlyDictionary<string,string> familyOf`,
`Action<string, string> grant`), so Core stays free of SQL per the DAL boundary. Both of
`AwardUniqueActorXpUnlocked`'s two production callers (`UniqueActorService.AwardXp`, the expedition
reward apply) already sit where the real, `RpgStore`-backed delegates can be constructed and the
service invoked when `LevelsGained > 0` — no new event pipeline, no new pub/sub, matching this
method's own existing "XP award inside an open transaction" framing.

Immediately after the `UPDATE` (same transaction, same connection — matching `RetireUniqueActorUnlocked`'s
own cited precedent for same-connection sequencing), when `LevelsGained > 0`:

1. Loads the specimen's `UnlockState` from the new persistence (§3) — `UnlockState.Empty()` for a
   specimen with no row yet (its first-ever level gain).
2. Resolves `ActionEligibility.Candidates(catalog, speciesKey, familyOf)` (the now-imported corpus,
   §1; `catalog`/`familyOf` read once by the caller, matching `ActionEligibility`'s own DB-free,
   caller-supplies-everything contract) minus `state.Held`'s own ids.
3. If the candidate set is non-empty, picks **one** deterministically via a named seeded stream
   (`SeededRng.DeriveStream(specimenWorldSeed, $"unlock:{instanceId}:{state.EarnCount}")` — the
   ratchet's own `earnCount` is already the "which attempt is this" discriminator, so no separate
   counter is needed) and calls `UnlockState.TryAccept(chosenId, tuning, rng)` **once per level
   gained** (`LevelsGained` may exceed 1 — a large XP award can cross several level thresholds in one
   call, and the ladder's own contract prices each attempt independently regardless of how many land
   in the same transaction).
4. On `UnlockOutcome.Success`: persists the updated `UnlockState` and grants via the **already-built,
   already-proven** `RpgStore.UpsertGrant` — **no roll, no instance, matching §Objective point 3
   exactly.**

**Why same-transaction, not a post-commit hook**: `UnlockState`'s own persistence (§3) must never
observe a level the XP award itself failed to commit — the two are one atomic fact about the
specimen, not two independently-failing writes.

## Golden-safety

**No shipped golden holds a granted action today** (every golden fixture's `EquippedActionIds` is
either unset or hand-constructed in a test, never sourced from a real grant). Importing the corpus
changes what `rpg_action` contains but touches no golden's own `BattleSetup`. **Zero-golden-mover by
construction, proven by running the full suite after import lands, not assumed.**

## Acceptance criteria

1. Importing the corpus twice produces byte-identical `rpg_action`/`rpg_action_cost`/container rows —
   the second import's `UpsertAction`/`UpsertCost` calls move zero revisions (T30's own guard,
   exercised for real against this content).
2. Every imported action's `Rung` equals its `RungBand.Collapse()`; `StructureBudgetGuard.Check`
   accepts every imported row at its authored rung (no brief exceeds its own declared structure
   budget — a real content-quality check, not just a schema check).
3. A synthetic specimen with a real, persisted `UnlockState` at a non-trivial `EarnCount`, run through
   a real `AwardUniqueActorXpUnlocked` call that crosses a level threshold, either gains a new held
   unlock (chance permitting) or does not — both outcomes leave `EarnCount`/`Held` in the exact state
   `UnlockState.TryAccept`'s own contract specifies, round-tripped through real persistence in the
   SAME transaction as the XP award, not just the in-memory class. An award crossing **multiple**
   level thresholds in one call attempts one roll per level gained, not one regardless of count.
4. A successful unlock reaches a real battle: the same `BuildSquadEquippedActionsTests.cs`-shaped
   proof T22 already ran for a manually-granted action, but ending in a **generated, imported** one —
   summon → level up until an unlock lands → `WebMatchService.BuildSquad` → `BattleEngine.Resolve`
   equips and can activate it.
5. The corpus import never runs against a live game/injector path, and the harness (A20) never
   triggers a real import — both asserted directly, not assumed from "it's server-side."

## Testing strategy

- Unit (Core): the import composition (brief → `ActionRow`/`ActionCostRow`/container atoms) against a
  hand-built brief fixture, not the live corpus file, so the test does not silently pass or fail as
  the corpus grows; `ActionUnlockGrantService`'s own logic against injected fakes, exactly like
  `UnlockDiscardServiceTests`.
- Integration (Data): the importer against a real SQLite database, twice, proving idempotency
  (criterion 1); `RpgStore.ActionUnlocks.cs`'s round-trip.
- Integration (Server): the real corpus file, imported for real, checked against
  `StructureBudgetGuard` for every row (criterion 2) — this is the one place the live
  `committed-round-*.json` content itself is exercised, matching `AuthoredEligibilityResolvesTests.cs`'s
  own precedent of reading the real files rather than a fixture.
- End-to-end (Server): criterion 4's full chain, extending `BuildSquadEquippedActionsTests.cs`'s own
  established pattern.
- Regression: full A17-A23 sweep plus a full `Core.Tests`/`Data.Tests`/`Server.Tests` run.

## Boundaries

- **Never** roll per-player. §Objective point 3 is load-bearing: a granted action has no instance and
  no roll. If a future module wants per-player variance on top of this (closer to `ActionSeeder`'s own
  original "loot model" framing), that is a new, explicitly-scoped module, not a silent change here.
- **Never** let the importer touch a live game/injector session. Server-side catalog population only.
- **Never** invent action timing/cost numbers per-brief. They come from the one per-category template
  (§2), a real tunable, never a magic number on the corpus row.
- **Ask first** before changing which event triggers a roll attempt (a specimen's own level gain,
  inside `AwardUniqueActorXpUnlocked`'s transaction). A per-kill or per-victory trigger is a real,
  different design with its own economy implications — this module builds against the specimen's own
  real level-transition write, not a new event bus, and not the player/species-scoped
  `ILevelChangeHandler` seam, which was this spec's own first-draft mistake (§Objective point 5).
- **Ask first** before wiring `ActionSeeder`'s per-player rolling path into anything real — it stays
  built-and-inert after this module ships, by design, until a later module explicitly claims it.
