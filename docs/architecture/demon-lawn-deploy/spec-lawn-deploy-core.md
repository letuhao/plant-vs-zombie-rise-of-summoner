# Spec: lawn-deploy-core (`demon-lawn-deploy` module 1)

**Status: proposed — pending owner review. No build authorized.**

## Objective

Let a real, owned unique demon specimen (a `rpg_unique_actors` row with a demon profile attached — not
a bare equipment actor, and never one currently designated Commander or holding the active Patron aura)
be deployed onto the live PvZ lawn through the *existing* deploy pipeline, carrying its own species
stats and rolled traits into real combat via the same Funnel every other effect grant uses. This module
is the foundational mechanism only — it does not decide *when* a deploy is allowed (that's
`lawn-deploy-events`) or *what Zomboss does* (that's `zomboss-deploy-ai`). Success here means: given an
`instanceId` that is already a demon specimen and not Commander/Patron-designated,
`POST /api/unique/actors/{id}/deploy` spawns a real plant/zombie on a live board whose combat stats and
trait effects are provably the specimen's own, not vanilla `type_id` defaults, and stay correct even
after that specimen is later promoted.

## Why this is a content-resolution gap, not a plumbing gap

Traced end to end before writing this:

- A demon specimen already lives in `rpg_unique_actors` (`RpgStore.cs:402-416`) — same table, same
  `phase`/`match_key`/`last_ptr`/`deploy_correlation_id` columns, same FSM (`Roster → Deploying →
  ActiveBound`) equipment-bound unique actors use. Confirmed field-for-field against a real demon actor
  row read live this session.
- `UniqueActorService.DeployAsync` (`UniqueActorService.cs:108-188`) reads the actor generically via
  `_store.GetUniqueActor(instanceId)` — no equipment-specific guard exists. It would admit a demon
  specimen's `instanceId` today, mechanically.
- `UniqueBoundLoadout.TryApply` (`src/FusionRpg.Injector/Match/UniqueBoundLoadout.cs:21`) —
  the Injector-side apply step — calls `UniqueLoadoutSpec.BindToPtr(bound.Ptr)` → `BindGrant` per grant
  → `funnel.EnqueueModifier`. This is source-agnostic: it applies whatever grants it's given through the
  same path combat math already reads. **No Injector code needs to change.**
- The actual gap: nothing today resolves a demon specimen's `TraitIds` (its own rolled traits, e.g.
  `["critical-hunter","guardian"]`) or its species magnitudes into anything that reaches this deploy
  path. `GetUniqueStatModsJson` only knows about equipment.
- **The wrong fix, ruled out before writing this spec**: appending demon-derived grants onto whatever
  `UniqueLoadoutMerge.Merge` already assembles. Its own doc comment
  (`UniqueLoadoutSpec.cs:22-25`, `Merge` logic) says it is *not* additive — "empty-ish deploy falls back
  to mods; non-empty deploy wins," picking one whole input, not merging field-by-field. This repo already
  hit and solved the identical-shaped problem once (getting atom-bound equipment content onto a deployed
  actor) and the fix that shipped was explicitly **not** "append onto whichever loadout `Merge` picked"
  (`tasks/seed-to-concrete-todo.md:2661-2674` documents that exact wrong-then-corrected turn) — atom-bound
  content rides the wire as its own separate fields via `AtomPushService.Build` →
  `RpgHub.cs`'s `BuildApplyCommand`, never inside the legacy `grants` list.
- **The right fix, matching that precedent**: bind a demon specimen's traits to `effect_binding` rows
  the same way an equipment slot already binds (`ModsAbsorptionTests.cs`'s real weapon/trinket bindings
  are the working example), so the *existing*, already-proven atom-push pipeline
  (`AtomPushService.OwnersForPlayer`/`.Build` — confirmed by the strengthen pass to include every
  `ActiveBound` `rpg_unique_actors` row automatically, `AtomPushService.cs:33-43`, no kind allowlist to
  fight) picks them up with zero new wire format. A demon's own traits already map to real
  `trait.{traitId}` containers (`trait.critical-hunter` is live and grant-backed as of this session).

## ⛔ Corrections (strengthen pass, 2026-09-06 — found by adversarial review, not assumed)

Four real defects the first draft did not survive:

1. **Patron must be refused, not just "assumed already designated" — DONE 2026-09-06.** The map's own
   first draft said this program deploys "a unique demon or Commander" — directly contradicting
   `demon-system-map.md`'s Axis 2 ("neither one ever fights"). No code guard existed: `IsPatronUnlocked`
   was checked only at `RpgStore.Fusion.cs:354` for sacrifice refusal, never in the deploy path.
   **Implemented**: `RpgStore.UniqueActors.cs`'s `TryBeginUniqueDeploy` now calls the same
   `IsPatronUnlocked` check and refuses with `"patron.cannot-deploy"` before admitting a spawn. **Proven,
   not just built**: `tests/FusionRpg.Data.Tests/DemonLawnDeployCommanderRefusalTests.cs` (3 cases: the
   active Patron refuses, a non-Patron specimen deploys normally, switching Patron away lets the old one
   deploy again) — 39/39 passing, including the full pre-existing `PatronStoreTests`/
   `UniqueActorStoreTests` suites, zero regressions.
   **Commander is a separate finding, not fixed the same way — it can't be, yet.** Investigated directly
   (`Core/Commanders/CommanderId.cs`, `PlayerEmpireCommanders.cs`) rather than assumed: `CommanderId` is
   a FIXED two-value enum (`Dave` = the player, `Zomboss` = the AI), and `PlayerEmpireCommanders.ForPlayer`
   always returns exactly `[Dave]` for any player — there is no demon-`instanceId` binding anywhere in
   this mechanism. `demon-system-map.md`'s own Axis 2 prose ("designate ONE demon" as Commander)
   describes a binding `commander-surface-map.md`'s own text says isn't built yet ("Dave today; roster
   grows later"). There is currently no state meaning "this specimen IS the Commander" to refuse
   against — the risk the strengthen pass named (a Commander-designated demon double-dipping aura +
   combat) is real once that binding ships, but not exploitable today. A code comment at the real call
   site names this precisely, so whoever builds that binding later knows a second refusal branch is
   owed here.
2. **Binding must be a reconciled diff, run on every deploy — never a one-time mint-time snapshot.**
   A specimen's `traits_json` can change on an EXISTING `instanceId` after mint: `RpgStore.Fusion.cs`'s
   `PromotionUnlocked` (~160-174) runs `FusionRoller.RollPromotionTraits` and updates
   `rpg_demon_profiles.traits_json` in place, keeping existing traits and appending new ones
   (`FusionRoller.cs:52-53`). If binding only ran at mint, a promoted specimen's new trait would reach the
   Codex correctly but silently never reach combat — no error, no refusal, just a wrong stat nobody sees
   fail. **Fix**: the binder mirrors `ReconcileUniqueEquipmentAtomBindingsUnlocked`'s own established
   shape exactly (`RpgStore.UniqueActors.cs:1172-1221`) — compute wanted-vs-existing bindings and diff,
   called from `DeployAsync` itself (not from every trait-mutation call site individually), so a stale
   binding can never survive past the next deploy. This also means "bind at mint or at deploy" (the
   prior draft's own open question) is answered: **always reconcile at deploy**; mint-time binding is
   ruled out.
3. **The `DeployMode`-driven side override needs an explicit decision, not a silent one.**
   `rpg_unique_actors.side` is written once at creation (`RpgStore.UniqueActors.cs:29-32`,
   `RpgStore.Demons.cs:49-54`) and never updated afterward — but `GET /api/actors/{id}/derived`
   (`AuraDerivedEndpoints.cs:36-52`) reads that column to resolve `ForZombie`/`ForPlant` species/stat
   data. If a `HypnoAlly` deploy overrides the WIRE side (what actually spawns) without also updating the
   column, this endpoint would resolve the wrong side relative to what's actually alive on the board.
   **Open, not silently decided**: either (a) `DeployAsync` also updates the `side` column for a
   `HypnoAlly` demon at deploy time (a real, deliberate mutation of a column every other code path
   currently treats as immutable — needs its own review), or (b) `AuraDerivedEndpoints` becomes
   `DeployMode`-aware. Named as Open Question 4 below, not assumed away.
4. **A demon's own species magnitudes (not just `TraitIds`) need a delivery path too, and none exists
   yet.** The original framing named both "traits and species magnitudes" as what needs to reach the
   spawned unit, but the actual `effect_binding` design above only wires trait ids. `ConcreteSpecies`'s
   own magnitudes (already `long`-typed, `PTheta`-derived) still have no path into a deploy grant. Named
   as Open Question 5, not silently dropped.

## Project structure (files this module likely touches)

- `src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs` — new `ReconcileDemonTraitBindingsUnlocked`,
  mirroring `ReconcileUniqueEquipmentAtomBindingsUnlocked`'s exact diff shape, called from `DeployAsync`.
- `src/FusionRpg.Server/UniqueActorService.cs` — `DeployAsync`: call the new reconcile method; refuse
  Commander/Patron-designated specimens by name (Correction 1); resolve spawn `side`/`typeId` from the
  demon's own `DeployMode` per whichever Open Question 4 resolution is chosen.
- No `src/FusionRpg.Injector` changes anticipated (see above) — a task that discovers otherwise should
  treat that as a real, reportable finding, not something to route around silently.
- New tests: `tests/FusionRpg.Data.Tests/DemonLawnDeployTests.cs` (binding creation AND reconciliation —
  explicitly including a promotion-then-redeploy case per Correction 2, not just a first-deploy case), a
  new case in `tests/FusionRpg.E2E.Tests/StorageE2ETests.cs`-style live E2E asserting a demon deploy
  reaches `ActiveBound` with its trait bindings present (mirroring the existing equipment E2E exactly),
  and a `tests/FusionRpg.Data.Tests/DemonLawnDeployCommanderRefusalTests.cs`-style case proving the
  Correction-1 refusal fires for both a designated Commander and the active Patron.

## Cross-references (found by the strengthen pass, must not be silently duplicated)

- **`TraitAtomSource.cs`/`BattleStatComposer.cs:162-174`** already resolve a specimen's `TraitIds` into
  stat effects for the turn-based expedition `BattleEngine`, via the SAME `trait.{traitId}` container
  ids this module targets — but through an entirely separate mechanism (no `effect_binding` involved).
  This module is now the SECOND real consumer of those container ids. If a trait container's own shape
  or semantics ever change, both consumers must be checked — `TraitAtomSource.cs`'s own doc comment
  already admits only a parity test keeps ITS hardcoded fallback in sync; this module needs the same
  discipline, not a shared implementation (the two run on genuinely different battle engines).
- **Party-dungeon / expedition mutual exclusion is already free.** `TryBeginUniqueDeploy`'s own
  `expedition.locked` check (`RpgStore.UniqueActors.cs:78-134`) already refuses a deploy for a specimen
  currently on an expedition, mirrored by `DispatchExpedition`'s own `specimen.deployed`/
  `specimen.on-expedition` checks (`RpgStore.Expeditions.cs:61-64`). Because this module reuses that same
  FSM unmodified, a specimen can never be lawn-deployed and on-expedition at once — cite this guard
  directly in the acceptance tests below rather than re-deriving it, and this same invariant is what a
  future Delve party-member feature should inherit, not reinvent.

## Code style

Match `UniqueActorService.cs`'s own shape exactly: `_store` calls return simple result records
(`Ok`/`Reason` pairs), no exceptions for expected refusals. Match
`ReconcileUniqueEquipmentAtomBindingsUnlocked`'s own before/after diff style for the new reconcile
method and `ModsAbsorptionTests.cs`'s own before/after-state assertion style for its tests.

## Testing strategy

- Unit: the reconcile method is deterministic and idempotent given `(instanceId, TraitIds)` — same
  input, same `effect_binding` rows, re-running is a no-op; a CHANGED `TraitIds` (simulating a
  post-promotion state) produces the correct added/withdrawn diff, not a blind re-insert.
- Integration: a real demon specimen (imported through the real species/trait pipeline this session
  already built) deployed through the real `DeployAsync`, asserting the resulting `AtomPushService`
  output for that owner includes the specimen's own trait containers; a second test promotes the SAME
  specimen, re-deploys, and asserts the NEW trait's container is now present too.
- Refusal: a specimen marked as the active Commander, and separately one holding the active Patron aura,
  both refuse deploy with a named reason — never a silent no-op or a generic error.
- Live E2E: the same live-lawn check pattern `StorageE2ETests.cs:90-120` already uses — deploy, read
  back, assert `ActiveBound` — extended to also assert the live board entity's combat stats reflect the
  specimen (not vanilla `type_id` defaults). Needs the `live-lawn-quick-start` skill's own board setup.

## Boundaries

- **Always do**: reuse `AtomPushService`/`effect_binding` — never invent a second content-delivery wire
  format for demons specifically. Always reconcile bindings at deploy time, never trust a stale snapshot.
- **Ask first**: Open Question 4 (the `side`-column decision) — a real, cross-cutting design choice, not
  an implementation detail this spec can resolve unilaterally.
- **Never do**: change `UniqueLoadoutMerge.Merge`'s own semantics, or the legacy `mods_json` shape — both
  are mid-migration away from (`spec-mods-absorption.md`) and this module must not add a new dependent
  on the path being retired. Never let a Commander- or Patron-designated specimen deploy.

## Open questions (real, not filler)

1. ~~Bind at mint or at deploy?~~ **Resolved by Correction 2**: always reconcile at deploy.
2. ~~`OwnerKind` for a demon specimen~~ — **resolved while writing this spec**: `OwnerKind.UniqueActor`
   is real, already built (T6.1, `OwnerScope.cs`), and already used for equipment slot bindings via
   `RpgStore.UniqueActors.cs`'s `ReconcileUniqueEquipmentAtomBindingsUnlocked`. A demon specimen is a
   `rpg_unique_actors` row like any other, so it already qualifies as `OwnerKind.UniqueActor` — no new
   owner-kind variant needed.
3. ~~`DeployMode` → spawn `side` precisely~~ — **resolved 2026-09-06, owner-confirmed after code
   archaeology was exhausted.** Investigated the only hypno-adjacent mechanism in the tree
   (`MatchRuntime.cs`'s `"zombie.hypno"` dispatch, PvZ's own native mind-control observation) and the
   `SpecimenOwnershipOracle` precedent (ownership and board-mechanical side are already separate axes
   elsewhere) — neither settled the question on its own, so it was asked directly rather than guessed:
   **"pvz engine don't allow us spawn zombie in dave side without hypno, without hypno, spawn cause
   zombie become enemy."** `side`/`typeId` pass through **unchanged** for both `DeployMode` values — a
   spawned zombie-type entity is hostile to the plant side by construction, and only the game's native
   hypnotize operation flips that. Confirms Option 1 from the question asked: no avatar-chassis remap,
   no column mutation.
4. ~~The `side`-column mutation question (Correction 3)~~ — **resolved as "no mutation, ever"** (see
   point 3). `rpg_unique_actors.side` stays exactly what every other read path already assumes it is.
5. **New finding from the same investigation: `HypnoAlly` deploy is a named refusal, not built.** Making
   a spawned zombie-type entity fight FOR its owner needs PvZ's own native hypnotize operation — and
   this codebase already investigated that operation once, for a different feature
   (`tasks/content-stack-todo.md`, the `content-stack` program), and found real mind-control is "a
   side-swap, not a flag," with no verified safe way to invoke or reverse it from the Injector today.
   That program named it a refusal rather than ship a guess. **This module does the same**:
   `TryBeginUniqueDeploy` refuses `HypnoAlly`-mode demons with `"deploy.hypno-ally-not-implemented"`.
   `PlantAvatar`-mode demons (the common case — "most demons deploy as plant-side avatars") deploy
   normally today. Unblocking `HypnoAlly` deploy is real, separate follow-up work gated on that harder,
   pre-existing native-hypnotize problem, not on anything in this module's own scope.
6. **A real regression this same change caused, found and fixed the same day**: `DemonLawnDeployCommanderRefusalTests.cs`/`DemonLawnDeployTests.cs` (T1.1/T1.2) picked their test
   species via `.First(s => s.Side == "zombie" && ...)` with no `DeployMode` filter, and that pick landed
   on a `HypnoAlly` species — breaking 5 of their own tests once the refusal above shipped. Fixed by
   excluding `HypnoAlly` from both files' own species selection (their own subject is unrelated to
   `DeployMode`).
7. **Species-magnitude delivery (Correction 4) — resolved 2026-09-07, built as T1.5.** A 6-question
   investigation (species-magnitude storage/reader, equipment's own `Absolutes` mechanism,
   `BattleStatComposer`'s expedition-engine precedent, `PowerLadderKMicro`/`KMilli`'s real shape,
   `thetaContent`'s type, per-species channel mapping) found:
   - `ConcreteSpecies.Magnitudes` (`long`-typed, `PTheta`-derived, already channel-shaped matching
     `DerivedStatChannels` exactly) is real and stored, but **dropped at the one shared
     `ConcreteSpeciesSeedReader.ToDemonSpeciesDef` seam** — the same seam `TraitPool` already needed
     curation at. `DemonSpeciesDef` (the LIVE catalog type `DemonSpeciesCatalog.Get` serves) had no
     `Magnitudes` field at all, and `RpgStore.GetSpecies` (which DOES read it) had zero production
     callers.
   - **`Absolutes`/`UniqueLoadoutSpec`/`mods_json` is explicitly forbidden** by this spec's own
     Boundaries ("never add a new dependent on the path being retired") — confirmed dead for every real
     item today besides.
   - **No existing precedent anywhere** (lawn or the expedition `BattleEngine`) turns a species'
     magnitude into a stat effect — `BattleStatComposer`/`BattleRuleset.BaseHp/Atk/Defense` are
     level-only, species-blind. This is genuinely new mechanism, not a wiring gap to a sibling engine.
   - **`PowerLadderKMicro`/`KMilli`-scaled atom ops are unwired for reuse**: never supplied at the real
     `AtomPushService.Build` call site (would throw), a single scalar Θ per push can't express multiple
     specimens' own differing Θ in one compile, and the op's linear formula doesn't match
     `AptitudeReadFunctions.Magnitude`'s own `share^γ` term anyway.
   - `ProduceAndBind`'s `thetaContent` is `int` and architecturally Θ (the ladder INPUT), never `PTheta`
     (the ladder OUTPUT, a `long`) — confirms a specimen's own `PTheta` was never a valid `thetaContent`
     value in the first place.

   **Design chosen**: magnitudes are pre-computed (already folded PTheta at species-generation time),
   so they ride as **flat, pre-authored atom content** — the same shape T1.2's trait grants already use
   — never a dynamically `PowerLadder`-scaled op. `DemonSpeciesDef.Magnitudes` now carries the value
   forward (added to the ONE shared mapper seam). `RpgStore.UniqueActors.cs`'s new
   `ReconcileDemonMagnitudeBindingsUnlocked` mirrors `ReconcileDemonTraitBindingsUnlocked`'s exact
   diff-reconcile shape, wired into `TryBeginUniqueDeploy` right after it. One container per species
   (`SpeciesMagnitudeContainerId`, holding every channel as an internal atom — a specimen's magnitude
   package is all-or-nothing, unlike a trait set), filed under `ContainerKind.Trait` (id prefix
   `trait.species-magnitude-{speciesId}`, a single kebab-case token after the `trait.` prefix — a
   further embedded dot is refused) — deliberately **not** `ContainerKind.SpeciesPassive`, whose
   `species-passive.` prefix already names a different, existing mechanism
   (`species-effects`/roster-materialise's own per-player ROLLED content) that this would otherwise
   collide with on the same speciesId. A genuinely new `ContainerKind` would be the cleaner semantic
   fit but is real, separate, cross-cutting surface left out of this task's own scope.
   **Full-corpus content generation (829+ species × N channels each) is real, separate follow-up
   work** — not blocking this task, matching the program's own established phased-rollout precedent;
   T1.5 proves the mechanism against a hand-authored fixture, mirroring exactly how T1.2 was proven
   against one real trait before the full 68-trait corpus existed.
8. ~~Numeric type safety through the compiled-atom step~~ — **T1.5's own re-run of this check, resolved
   2026-09-07, named not fixed.** T1.5 chose the flat-atom path (point 7 above), not
   `PowerLadderKMicro`/`KMilli` — so `AtomCompiler.cs:568,572`'s `checked(...)`-guarded arithmetic is
   never reached, same as T1.2. But the flat path has its OWN, different int ceiling: `ValueSpec.Min`
   (`ValueSpec.cs:117`) is `int`, and `AtomCompiler.cs:606`'s plain-flat resolution
   (`CurveTable.ApplyMilli(spec.Min, ...)`, `CurveTable.cs:98`) returns `int`, **unwrapped in
   `checked`** — a genuinely large species magnitude authored as a flat amount would silently wrap, not
   throw. **Named, not fixed**: this is a repo-wide, structural fact of `ValueSpec` shared by EVERY flat
   atom (the trait grant's own `150` included), not something T1.5 introduces — fixing it would mean
   widening `ValueSpec.Min` for the entire atom vocabulary, real cross-cutting surface outside a single
   deploy-mechanism task's scope. Currently far from load-bearing: real committed Θ values are tiny
   (13, per `AbyssSwordStar.json`) against `int`'s own whole-units ceiling (Θ≈103,557 per CLAUDE.md's
   table) — but a future balance pass pushing Θ that high would need `ValueSpec.Min` widened first, and
   this is where that future work should look.
