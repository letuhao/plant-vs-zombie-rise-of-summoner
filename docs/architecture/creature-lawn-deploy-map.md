# Capability map: creature lawn deploy

Source: owner decision 2026-09-06 (this session) — resolves the "does a unique creature ever fight on the
literal PvZ lawn" tension between `creature-system-map.md`'s original 2026-08-21 deploy-mode decision and
its own 2026-09-06 Vocabulary section. Owner's resolution: **the lawn's default population stays general
creatures** (species-stats-only, no owned instance — already the `SpeciesAllocationSource` mechanism).
**A new event/AI layer lets either side additionally deploy a unique creature onto the lawn in specific,
triggered cases.**

Status: **partially implemented 2026-09-08.** The core deploy, event, and Zomboss-AI slices are
landed and their live checkpoints are recorded; progression-source conformance remains in Phase 4.

Module specs live in [creature-lawn-deploy/](creature-lawn-deploy/), one per module id, written in dependency
order. Implementation plan/task list: [tasks/creature-lawn-deploy-plan.md](../../tasks/creature-lawn-deploy-plan.md) /
[tasks/creature-lawn-deploy-todo.md](../../tasks/creature-lawn-deploy-todo.md).

**Strengthen pass (2026-09-06):** four independent adversarial reviews (mechanism soundness, cross-
program/economy, hard-rule compliance, spec-quality-vs-this-repo's-own-bar), matching
`commander-surface-map.md`'s own strengthen-pass precedent. One severe self-contradiction and several
real gaps found and closed below — see each module's own "Corrections" section for the finding-by-
finding detail, mirroring `spec-ai-commander.md`'s own format.

---

## ⛔ Corrected same day: this program never deploys a Commander or a Patron

**The first draft of this map said "deploy a unique creature or Commander" throughout — wrong, caught by
the strengthen pass before any task was broken down.** `creature-system-map.md`'s own Axis 2 (added the
SAME day this map was first drafted) is unambiguous: *"Neither one ever fights"* and *"Do not use
'commander' to mean 'the creature fighting for me.'"* The deploy path now enforces the active Patron
refusal in `TryBeginUniqueDeploy` (`RpgStore.UniqueActors.cs:78-134`). Commander-instance refusal remains
unimplemented because no persisted instance-to-Commander binding exists yet. The same path checks
`not_found`/idempotency/`expedition.locked`/phase/contract. **`lawn-deploy-core`'s own acceptance now
includes a real, tested refusal**: a
creature currently designated Commander or holding the active Patron aura refuses deploy outright. This
program deploys *unique creatures only*, full stop — the map's own name never meant to imply otherwise, and
every mention of "or Commander" below has been removed.

---

## What this program is

Three things, in dependency order:

1. The raw mechanism to deploy an owned unique creature specimen onto the live PvZ lawn, carrying its own
   traits/stats through the real combat pipeline (plant-avatar or hypno-zombie-ally, per `DeployMode`) —
   refusing outright if that specimen is the active Commander or Patron.
2. An event/trigger layer deciding *when* such a deploy is available during a lawn run, for the plant
   (player-facing) side.
3. A Zomboss-side AI policy that decides *whether and which* unique creature to deploy during an active
   event, since the zombie side has no human player to ask.

**Explicitly not this program:** the lawn's default/general-creature population (already built via
`SpeciesAllocationSource`); the world-map turn-based AI (`ai-commander`, a different domain — days-scale
belief state over a sector graph, not seconds-scale board combat); the Commander-*picker* UI
(`commander-surface-map.md`, a separate, not-yet-authorized program covering which creature is designated
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

The obvious framing — "build a new deploy pipeline for creatures" — is wrong. A creature specimen is already
a row in `rpg_unique_actors` (`RpgStore.cs:402-416`), the exact same table and phase-FSM
(`Roster → Deploying → ActiveBound`) that equipment-bound unique actors already use. The live, working,
E2E-tested deploy pipeline (`POST /api/unique/actors/{id}/deploy` → `UniqueActorService.DeployAsync` →
`pvz.spawn.extra` → `MatchHost.TryBeginUniquePending` → `UniqueBindings.TryBindOnSpawn` →
`UniqueBoundLoadout.TryApply`, proven live by
`tests/FusionRpg.E2E.Tests/StorageE2ETests.cs:90-120`) is **already generic over `instanceId`** — nothing
in it checks "is this equipment," and `AtomPushService.OwnersForPlayer` already includes every
`ActiveBound` `rpg_unique_actors` row regardless of kind (confirmed by the strengthen pass,
`AtomPushService.cs:33-43`). A creature specimen could be admitted through this exact endpoint today.

The remaining content work is narrower: keep the specimen's own `TraitIds` and species magnitudes
flowing into the grants that reach the spawned unit. The core deploy slice now reconciles trait bindings
on every deploy through the atom/`effect_binding` pipeline equipment already migrated to
(`spec-mods-absorption.md`), not the legacy `mods_json` blob it is migrating away from. It is also NOT
the only real consumer of `trait.{traitId}` containers —
`TraitAtomSource.cs`/`BattleStatComposer.cs` already resolve the same ids for the turn-based expedition
battle path; module 1's own spec names this sibling consumer explicitly so the two never silently drift.

---

## ⛔ Program-level acceptance (no module is done on its own criteria alone)

Matching `commander-surface-map.md`'s own binding rule — the first spec pass could tick every module's
internal checkboxes while a player never actually sees a working deploy in a live run:

> **A real lawn run, played to a triggered event, on both sides**: the plant-side prompt fires, the
> player deploys an owned unique creature, and the spawned entity's combat stats/trait effects are provably
> the specimen's own (not vanilla `type_id` defaults) — **and**, separately, a Zomboss-triggered event
> results in Zomboss deploying one of its own unique creatures, chosen by the real policy, not a stub. Both
> halves were proven in one continuous live game/server sitting on 2026-09-07. Progression-source
> conformance and atomic lawn-XP settlement remain open in Phase 4.

---

## Modules

| Module id | Responsibility | Depends on |
|---|---|---|
| `lawn-deploy-core` | Resolve a creature specimen's traits into `effect_binding` rows (mirroring an equipment slot bind, as a **reconciled diff on every deploy**, never a one-time mint-time snapshot) so the existing atom-push pipeline (`AtomPushService.Build` → `RpgHub.BuildApplyCommand`) delivers them; wire `UniqueActorService.DeployAsync` to admit a creature-kind `instanceId`, refuse a Commander/Patron-designated one, and pick the right spawn side/type from `DeployMode`. No new Injector code — `UniqueBoundLoadout.TryApply`/the Funnel are already generic. | — |
| `lawn-deploy-progression` | **Partial implementation 2026-09-08.** Award a Bound unique specimen its own lawn XP for attributed enemy kills and active Bound duration. Persist binding sessions and idempotent receipts; use the `UniqueSpecimen` source and never species XP or the empire fallback. Spec: [spec-lawn-deploy-progression.md](creature-lawn-deploy/spec-lawn-deploy-progression.md). | `lawn-deploy-core`, `progression-source-contract` |
| `lawn-deploy-events` | Define trigger conditions for when a unique-creature deploy becomes available during a lawn run (frequency, cost, which side, player-facing UI for the plant side), reading a Hot/Cold-safe roster snapshot rather than a live Cold-plane query mid-tick. No reusable trigger/condition system exists yet anywhere in the tree for the live lawn (`AmbushDraw` is Delve-only and itself only partially built) — this is new. | `lawn-deploy-core` |
| `zomboss-deploy-ai` | The zombie-side counterpart: decides *whether and which* unique creature Zomboss deploys during an active event, reading board state through an explicit, enforcing view type (never `WorldState`/full Cold-plane access) — mirroring `spec-ai-commander.md`'s own `IWorldView` discipline in shape, not in code (this is lawn-scale, not world-turn-scale). Deliberately **not** a reuse of `ai-commander` (world-map turns, fog-of-war belief state, days-scale) despite the shared "Zomboss decides something" flavor; the data shape and decision cadence are unrelated. | `lawn-deploy-core`, `lawn-deploy-events` |

Build order: `lawn-deploy-core` → (`lawn-deploy-progression` after `progression-source-contract` || `lawn-deploy-events`) → `zomboss-deploy-ai`.

## Deliberately deferred (not in any module here)

- The Commander-picker UI (who is designated Commander at all) — `commander-surface-map.md`'s own scope,
  proposed but not owner-authorized.
- class-system's own per-instance point-economy delivery path (see "What this program is" above) —
  a real gap this program's own investigation found, owned by `class-system-map.md`, not here.
