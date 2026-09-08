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

1. Rolls the brief's `atomFamilies` into a concrete atom subset — **⛔ corrected during T59.3's own
   BUILD, 2026-09-06: not via `ActionSeeder.Generate`** (that wrapper also rolls a target SHAPE from a
   weighted pool and composes a NAME via `ActionNameTemplates` — the brief already authors both
   `targetMode`/`relation` and `name` directly, so `Generate`'s other two jobs do not apply here; only
   the atom half, `Instantiator.Draw` itself, is reused). **The real template for "one or more atom
   families → a container's pool" is `UniqueContainerBuild.From`**
   (`src/FusionRpg.Core/Items/Uniques/UniqueContainerBuild.cs`), generalized from its one
   `VarianceSlot.Family` to the brief's `atomFamilies` list (plural, unlike the unique case):
   1. `atomsInFamily` (a caller-supplied `Func<string, IReadOnlyList<AtomRow>>`, the SAME seam
      `UniqueContainerLookups` already declares — "nothing in the codebase indexes atoms by family
      today... the caller... is the only one who can answer it") resolves every family in
      `atomFamilies`, flattened into one candidate list — no tier filter (unlike the unique build's
      own `ReferenceTier` step: nothing in the corpus brief or `action-corpus-ideal.md` correlates a
      rung to an atom TIER, and inventing one would be a fabricated rule).
   2. **Reused verbatim, not reinvented**: `AffixValidator.AffixClassOfAtom` (internal, same
      assembly) classifies the FIRST candidate; every candidate of that class becomes one
      `ContainerPoolRow` via `AffixLibraryGenerator.SingleAtomAffix`; a mismatched-class candidate is
      DROPPED with a reported reason, exactly `UniqueContainerBuild`'s own policy — the alternative
      (splitting the roll budget proportionally across both classes) is a new allocation rule nothing
      specifies, so this module does not invent one.
   3. **`PoolRolls` already answers "how many" — no new tunable.** `RungRow.PoolRolls`
      (`action-rungs.v2.json`, already loaded via `RungPolicy.Table`) is the rung-keyed roll-count
      `effect-pipeline-ideal.md` §3.4 named as undeclared ("skill (action): ~1-5... no tuning file
      declares them") — true when that finding was written, **no longer true for rung-keyed content**:
      the brief's own `RungBand.Collapse()` indexes a real row with a real `PoolRolls`. The whole count
      goes to whichever side (`PrefixRolls` or `SuffixRolls`) step 2's `rollClass` selected; the other
      stays `0` — a THIRD instance this reopening has found of "the number already exists on a
      rung-keyed table, don't author a private one" (after A23's holder-rung fix and T59.1's
      cost-vs-timing-template correction).
   4. `Instantiator.Draw` runs ONCE against this temporary, pool-bearing `ContainerRow`, seeded from a
      `Fnv1a64`-style hash of the brief's own stable `id` (mirroring `SeededRng.DeriveStream`'s existing
      "seed XOR stable name" shape), **never** a per-player `WorldSeed` — two imports of the same brief
      on two servers produce byte-identical results. **The drawn atoms become the container's
      PERMANENT fixed core** (`Atoms`), and the pool is discarded (`Pool = []`, both roll budgets `0`)
      in the row this module actually persists — matching §Objective point 3 exactly ("a granted
      action has no instance and no rolls"): nothing will ever call `Instantiator.Draw` on this
      container again, unlike the unique-item case, which keeps its pool for a later per-player
      `TryInstantiate`.
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
   comment); `Targeting` compiled from `targetMode`/`relation`; `ContainerId` from step 2.
   **`Envelope`'s `CooldownChannel`/`EffectivenessChannel` come from the small per-category mapping**
   (§2 below, corrected) — set on the row's OWN baseline envelope, which `BuildActionCatalog`'s
   existing `ActionTimingDerivation.Derive` call preserves untouched while it separately (and already,
   with no help from this importer) derives Windup/Recovery/TimeCost/Cooldown/`Class`/`CooldownKey`
   from `row.Category`. **`MinRange`/`MaxRange` stay the fixed structural default every existing
   action fixture already uses** (`0`/`int.MaxValue`, `RangeChannel: null` — unbounded): the brief
   carries no range data, and no board-based range distinction is meaningful yet for a squad-vs-wave
   battle (`A10`, unbuilt) — a per-category range window would be a guess with nothing to gate against,
   not a balance choice this module can honestly make. Inventing per-brief TIMING numbers, by
   contrast, would be exactly the "private `f(level)`"/per-row magic-number defect `tunables-ssot.md`
   forbids — moot now that timing needs no per-brief or per-category authoring here at all.
4. Persists via `RpgStore.UpsertAction`, then one `ActionCostRow` per the same per-category template,
   scaled by the brief's own `RungBand.Collapse()` (the **authored** rung — `ActionCostRow.AmountSpec`
   is a `ValueSpec`, scaled later, per-holder, by A23's corrected `CostLedger` at grant/battle time;
   the import step authors the base amount, never the holder-scaled one), via `RpgStore.UpsertCost`.

**Where this runs**: a `FusionRpg.Server` startup step (mirroring how `BattleTuningHub.Configure`/
`RungPolicy` already bootstrap from files at startup), gated behind a config flag defaulting to
**on** — content that exists and is idempotent to re-import needs no separate trigger. **Never** a
live game/injector path; this is server-side catalog population, matching T30's own "actions are
battle-mode and the injector never sees one."

### 2. The per-category cost template — a new, small tunable (⛔ narrowed during T59.1's own BUILD)

**⛔ Corrected during T59.1's own build pass, 2026-09-06 — the first draft duplicated an already-shipped
module it did not know existed.** The original design below authored WindupTicks/RecoveryTicks/
CooldownTicks/`Class` per category in a new file. Reading `RpgStore.BuildActionCatalog`
(`RpgStore.ActionCatalog.cs:122-130`) during BUILD found this **already done, already wired to
production**: `battle-tempo`'s `action-timing` module (2026-09-05, `ActionTimingTuning`/
`ActionTimingDerivation`, `data/tuning/action-timing.v1.json`) derives WindupTicks/RecoveryTicks/
TimeCostTicks/CooldownTicks/`Class`/`CooldownKey` from `row.Category` for **every** row
`BuildActionCatalog` compiles — the real, only production path `WebMatchService`'s three
`BattleEngine.Resolve` call sites use. `Class` there is `CooldownClass.Category` (not `.Specific` as
first assumed here) when `cooldownTicks > 0`, which is equally non-`None` and therefore already clear
of T56.4's found bug (`.None` is what silently no-ops both the check and the arm — `.Category` and
`.Specific` are both real, working, non-`None` classes; `.Category`'s own doc comment — "shared across
a named group, the group is `CooldownKey`" — is in fact the MORE correct choice for a category-templated
action than a per-action `.Specific` cooldown would have been). **Writing a second timing tunable here
would have been exactly the "two incompatible curves" failure `ssot-power-scale.md` warns about, for
timing instead of power.** Corrected scope, narrower than originally drafted:

1. **Envelope timing (Windup/Recovery/TimeCost/Cooldown/Class/CooldownKey) needs NOTHING new.** T59.3's
   importer does not set these at all — `BuildActionCatalog` derives them unconditionally for any row
   with a non-null `Category`, imported or not.
2. **`CooldownChannel`/`EffectivenessChannel` per category IS a real, separate gap** — confirmed neither
   `ActionCompiler.Compile` nor `ActionTimingDerivation.Derive` ever sets either field (`Derive`'s own
   `baseline with {...}` touches six fields, not these two), so every row compiled today carries both
   `null` — the ONLY place either channel is ever populated is a hand-built test fixture
   (`ActionCostsCooldownsAdoptionTests.cs`'s `PerTickCostedSkill`/`CooldownGatedAttackSkill`). This is
   not a balance number (a balance pass tunes what a channel's modifiers are worth, never which channel
   name an action reads), so it is a small, pure, static mapping — `ActionCategory → (SkillCooldown,
   SkillEffectiveness)` key strings, via `DerivedStatChannels.SkillCooldown/SkillEffectiveness` — not a
   tuning-file row. T59.3's composer sets both directly on each imported row's own baseline
   `ActionRow.Envelope` (the field `BuildActionCatalog`'s `ActionTimingDerivation.Derive` call takes as
   its `baseline` and preserves untouched).
3. **The `(resourceId, baseAmountAtRung1, timing)` cost row per category remains genuinely new** —
   `ActionTimingTuning` has no notion of resource costs at all. This is the one real balance-surface
   number left for this module to author: `data/tuning/action-corpus-cost-templates.v1.json`, one row
   per `ActionCategory`. **Shipped with a stated derivation and a named re-tune trigger**
   (`action-corpus-ideal.md` §36's own precedent for "default now, re-tune later is what a tunable is
   for") — not left as an open balance question this module cannot close.

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
