# Capability map: demon lawn deploy

Source: owner decision 2026-09-06 (this session) — resolves the "does a unique demon ever fight on the
literal PvZ lawn" tension between `demon-system-map.md`'s original 2026-08-21 deploy-mode decision and
its own 2026-09-06 Vocabulary section. Owner's resolution: **the lawn's default population stays general
demons** (species-stats-only, no owned instance — already the `SpeciesAllocationSource` mechanism).
**A new event/AI layer lets either side additionally deploy a unique demon onto the lawn in specific,
triggered cases.**

Status: **proposed — pending owner review. No build authorized.**

Module specs live in [demon-lawn-deploy/](demon-lawn-deploy/), one per module id, written in dependency
order. Implementation plan/task list: [tasks/demon-lawn-deploy-plan.md](../../tasks/demon-lawn-deploy-plan.md) /
[tasks/demon-lawn-deploy-todo.md](../../tasks/demon-lawn-deploy-todo.md).

**Strengthen pass (2026-09-06):** four independent adversarial reviews (mechanism soundness, cross-
program/economy, hard-rule compliance, spec-quality-vs-this-repo's-own-bar), matching
`commander-surface-map.md`'s own strengthen-pass precedent. One severe self-contradiction and several
real gaps found and closed below — see each module's own "Corrections" section for the finding-by-
finding detail, mirroring `spec-ai-commander.md`'s own format.

---

## ⛔ Corrected same day: this program never deploys a Commander or a Patron

**The first draft of this map said "deploy a unique demon or Commander" throughout — wrong, caught by
the strengthen pass before any task was broken down.** `demon-system-map.md`'s own Axis 2 (added the
SAME day this map was first drafted) is unambiguous: *"Neither one ever fights"* and *"Do not use
'commander' to mean 'the demon fighting for me.'"* No code-level guard against this exists today —
`TryBeginUniqueDeploy` (`RpgStore.UniqueActors.cs:78-134`) checks `not_found`/idempotency/
`expedition.locked`/phase/contract, and `IsPatronUnlocked` is checked ONLY at `RpgStore.Fusion.cs:354`
(fusion-sacrifice refusal) — never in the deploy path. Left unfixed, a Patron (100-soul switch cost,
"unconsumable") or a Commander could get its aura AND free lawn combat value from the same instance,
voiding both economies. **`lawn-deploy-core`'s own acceptance now includes a real, tested refusal**: a
demon currently designated Commander or holding the active Patron aura refuses deploy outright. This
program deploys *unique demons only*, full stop — the map's own name never meant to imply otherwise, and
every mention of "or Commander" below has been removed.

---

## What this program is

Three things, in dependency order:

1. The raw mechanism to deploy an owned unique demon specimen onto the live PvZ lawn, carrying its own
   traits/stats through the real combat pipeline (plant-avatar or hypno-zombie-ally, per `DeployMode`) —
   refusing outright if that specimen is the active Commander or Patron.
2. An event/trigger layer deciding *when* such a deploy is available during a lawn run, for the plant
   (player-facing) side.
3. A Zomboss-side AI policy that decides *whether and which* unique demon to deploy during an active
   event, since the zombie side has no human player to ask.

**Explicitly not this program:** the lawn's default/general-demon population (already built via
`SpeciesAllocationSource`); the world-map turn-based AI (`ai-commander`, a different domain — days-scale
belief state over a sector graph, not seconds-scale board combat); the Commander-*picker* UI
(`commander-surface-map.md`, a separate, not-yet-authorized program covering which demon is designated
Commander — and which explicitly scopes "Commander" to the player empire only, listing "Zomboss in the
player list" as out of its own scope too, confirming Zomboss never gets a Commander of its own kind at
all); `commander-surface-map.md`'s own "creature deploy berths / T21" (a distinct **pre-run** squad-
ceremony concept, not this program's **mid-run** trigger — disclaimed here, not duplicated); the Delve
party-member mechanic (`party-dungeon-ideal.md`, an abstract, non-lawn battle resolver — already
mutually exclusive with lawn-deploy for free, see `lawn-deploy-core`'s own Corrections); class-system's
own per-instance point-economy delivery (a real, adjacent gap this program's own investigation surfaced
— `StatContext`/`SpeciesAllocationSource` have no way to express "this specific owned instance" at all,
only `(side, typeId)` — named here as a class-system gap this program does not own fixing, matching the
established "found, named, not this program's own scope" pattern this repo already uses elsewhere).

---

## Why this is smaller than it first looked

The obvious framing — "build a new deploy pipeline for demons" — is wrong. A demon specimen is already
a row in `rpg_unique_actors` (`RpgStore.cs:402-416`), the exact same table and phase-FSM
(`Roster → Deploying → ActiveBound`) that equipment-bound unique actors already use. The live, working,
E2E-tested deploy pipeline (`POST /api/unique/actors/{id}/deploy` → `UniqueActorService.DeployAsync` →
`pvz.spawn.extra` → `MatchHost.TryBeginUniquePending` → `UniqueBindings.TryBindOnSpawn` →
`UniqueBoundLoadout.TryApply`, proven live by
`tests/FusionRpg.E2E.Tests/StorageE2ETests.cs:90-120`) is **already generic over `instanceId`** — nothing
in it checks "is this equipment," and `AtomPushService.OwnersForPlayer` already includes every
`ActiveBound` `rpg_unique_actors` row regardless of kind (confirmed by the strengthen pass,
`AtomPushService.cs:33-43`). A demon specimen could be admitted through this exact endpoint today.

What's actually missing is narrower: nothing today resolves a demon specimen's own `TraitIds` and species
magnitudes into the grants that reach the spawned unit. That is a **content-resolution gap, not a
plumbing gap** — module 1 below is scoped to close exactly that, reusing the atom/`effect_binding`
pipeline equipment already migrated to (`spec-mods-absorption.md`), not the legacy `mods_json` blob it is
migrating away from. It also is NOT the only real consumer of `trait.{traitId}` containers —
`TraitAtomSource.cs`/`BattleStatComposer.cs` already resolve the same ids for the turn-based expedition
battle path; module 1's own spec names this sibling consumer explicitly so the two never silently drift.

---

## ⛔ Program-level acceptance (no module is done on its own criteria alone)

Matching `commander-surface-map.md`'s own binding rule — the first spec pass could tick every module's
internal checkboxes while a player never actually sees a working deploy in a live run:

> **A real lawn run, played to a triggered event, on both sides**: the plant-side prompt fires, the
> player deploys an owned unique demon, and the spawned entity's combat stats/trait effects are provably
> the specimen's own (not vanilla `type_id` defaults) — **and**, separately, a Zomboss-triggered event
> results in Zomboss deploying one of its own unique demons, chosen by the real policy, not a stub. Until
> an E2E proves both halves in one sitting, the program is not done, regardless of module status.

---

## Modules

| Module id | Responsibility | Depends on |
|---|---|---|
| `lawn-deploy-core` | Resolve a demon specimen's traits into `effect_binding` rows (mirroring an equipment slot bind, as a **reconciled diff on every deploy**, never a one-time mint-time snapshot) so the existing atom-push pipeline (`AtomPushService.Build` → `RpgHub.BuildApplyCommand`) delivers them; wire `UniqueActorService.DeployAsync` to admit a demon-kind `instanceId`, refuse a Commander/Patron-designated one, and pick the right spawn side/type from `DeployMode`. No new Injector code — `UniqueBoundLoadout.TryApply`/the Funnel are already generic. | — |
| `lawn-deploy-progression` | **Approved 2026-09-08.** Award a Bound unique specimen its own lawn XP for attributed enemy kills and active Bound duration. Persist binding sessions and idempotent receipts; use the `UniqueSpecimen` source and never species XP or the empire fallback. Spec: [spec-lawn-deploy-progression.md](demon-lawn-deploy/spec-lawn-deploy-progression.md). | `lawn-deploy-core`, `progression-source-contract` |
| `lawn-deploy-events` | Define trigger conditions for when a unique-demon deploy becomes available during a lawn run (frequency, cost, which side, player-facing UI for the plant side), reading a Hot/Cold-safe roster snapshot rather than a live Cold-plane query mid-tick. No reusable trigger/condition system exists yet anywhere in the tree for the live lawn (`AmbushDraw` is Delve-only and itself only partially built) — this is new. | `lawn-deploy-core` |
| `zomboss-deploy-ai` | The zombie-side counterpart: decides *whether and which* unique demon Zomboss deploys during an active event, reading board state through an explicit, enforcing view type (never `WorldState`/full Cold-plane access) — mirroring `spec-ai-commander.md`'s own `IWorldView` discipline in shape, not in code (this is lawn-scale, not world-turn-scale). Deliberately **not** a reuse of `ai-commander` (world-map turns, fog-of-war belief state, days-scale) despite the shared "Zomboss decides something" flavor; the data shape and decision cadence are unrelated. | `lawn-deploy-core`, `lawn-deploy-events` |

Build order: `lawn-deploy-core` → (`lawn-deploy-progression` after `progression-source-contract` || `lawn-deploy-events`) → `zomboss-deploy-ai`.

## Deliberately deferred (not in any module here)

- The Commander-picker UI (who is designated Commander at all) — `commander-surface-map.md`'s own scope,
  proposed but not owner-authorized.
- **Zomboss's own unique-demon roster/pool source** — `zomboss-deploy-ai` cannot build without this, and
  nothing in the codebase gives the zombie side a demon roster today. Named here explicitly (not just
  buried in that module's own open questions) so it isn't lost: this is a real, blocking, owner-level
  design decision that gates the third module, not an implementation detail to discover mid-build.
- class-system's own per-instance point-economy delivery path (see "What this program is" above) —
  a real gap this program's own investigation found, owned by `class-system-map.md`, not here.
