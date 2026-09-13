# Live probe (RPG Server Debug) — the ideal

**Status:** idea phase, 2026-09-13. Not a spec. No build authorized.

## Which loop this extends

**None directly — this is developer/verification infrastructure, not a player feature.** Per the
idea-phase skill's own rule ("if none fit, stop — that is an owner decision to grow `the-loops.md`,
not permission to invent a parallel pitch"): this program does not extend a loop, it **exercises**
real ones to prove other RPG-layer features actually work —

- **Spine A (level up and power)** — real aptitude allocation, real XP/level.
- **Spine B (creature summon and fusion)** — real acquisition of a durable `UniqueActor`.
- **Place 1 (lawn)** — real deploy onto a live board.

It never changes what those loops are. Flagging this honestly rather than force-fitting a fake loop
name is the correct outcome of Step 0, not a gap in the analysis.

## What this is

A standard + a documented recipe (and, if the owner wants it, a thin script) for **proving a feature
works through the real RPG server pipeline**, using a real `UniqueActor` a real player-facing flow
could have produced — never a fabricated one — with proof read back through the same persisted-state
path the normal frontend uses. The doctrine itself already shipped as
[`live-probe-standard.md`](../contributing/live-probe-standard.md); this doc is the idea phase for
**building the recipe/tooling that follows it**, triggered by a real incident (below) that the
doctrine alone would not have prevented — a doctrine you can violate by accident needs a habit to go
with it, not just a rule.

**The incident this exists because of (2026-09-13).** A live probe for `actor-hub-and-combat-power-
solid-fixing` T14 (`bound-loadout-hub`) deployed a WallNut by binding a hand-typed loadout JSON
straight through the Injector's debug bind path, then read the result back from the same injector's
own telemetry (`debug.board-stats`). `hp` matched the requested value; `maxHp`/`attack` did not — the
feature was actually broken, and the probe's shape (fabricate, then read your own fabrication back)
could not have caught that on its own. It happened to be caught only because a sibling check in the
SAME probe (T12, aptitude allocation) used a real Server endpoint and disagreed with what "should"
have also been broken by the same class of defect had it too gone through the fabricated path.

## What already exists

### Built — the real acquisition → progression → deploy chain

| Step | Endpoint | Evidence |
|---|---|---|
| Summon (gacha) | `POST /api/creatures/summon` | `CreatureEndpoints.cs:83` → `RpgStore.ExecuteSummon` (`RpgStore.Summons.cs:28`) spends souls, rolls via `SummonRoller.Roll`, mints via `MintCreatureUnlocked` (`RpgStore.Creatures.cs:29`, a real `INSERT INTO rpg_unique_actors`). `SummonRequest` (`CreatureEndpoints.cs:280`) is `PlayerId, BannerId, Count, CorrelationId` — identity/selection only, no stats fields exist to fabricate |
| Fusion | `POST /api/fusion/execute` | `FusionEndpoints.cs:30` → `RpgStore.ExecuteFusion`; Recipe mode mints via the SAME `MintCreatureUnlocked` (`RpgStore.Fusion.cs:298`), StarMerge updates the existing row (`RpgStore.Fusion.cs:126`). `FusionHttpRequest` (`FusionEndpoints.cs:249`) is likewise selection-only |
| Delve altar pull | `PullAtAltar` → `CloseDelve(Extracted)` | `RpgStore.Delve.cs:1418`, `:950` — mint uses server-rolled `entry.Rarity`/`Variant`/`TraitIds` from the haul, not caller input |
| Identity-only debug shortcut | `POST /api/debug/spawn-unique-actor` | `CreatureEndpoints.cs:187`, `SpawnUniqueActorRequest` (`:294`) is `PlayerId, Side, GameTypeId` only — despite the `/api/debug/` prefix this is a **legitimate** RPG Server Debug shortcut (skips the RNG/soul cost of a real summon, produces an identical row through `store.CreateUniqueActor`), not a fabrication vector. Correctly shaped already |
| Level / XP | `POST /api/unique/actors/{id}/xp` | `UniqueActorEndpoints.cs:113` → `ua.AwardXp` → `RpgStore.AwardUniqueActorXp` (`RpgStore.UniqueActors.cs:1808`). Only checks `delta > 0`, not its source — see Wiring gap below |
| Aptitude allocation | `POST /api/aptitudes/unique/allocate` | `AptitudeEndpoints.cs:64` — real, budget-checked, persisted. **Already proven correct** by the 2026-09-13 T12 probe (30 pts → `Might`, `bonusAtk=1060`, `appliedAtk=1080`, matched live) |
| Equip | `PUT /api/unique/actors/{id}/equipment/{slot}`, `POST /api/items/equip` | `RpgStore.UniqueActors.cs:1408` and `ItemEquipEndpoints.cs:104` both hard-require a real, owned, catalog-known item — no fabrication possible. Equip write triggers the real `mods_json` rebuild: `RebuildUniqueModsFromEquipmentUnlocked` (`RpgStore.UniqueActors.cs:1774`) |
| Deploy | `POST /api/unique/actors/{id}/deploy` | `UniqueActorEndpoints.cs:43` → `UniqueActorService.DeployAsync` — real, persisted, real board placement. **Its own `loadoutJson` parameter is the defect seam** — see below |

**This chain is ~90% already built and real.** The idea that a live probe *has to* fabricate an actor
is false — it was a shortcut taken under time pressure, not a missing capability.

### Wiring gap — `DeployAsync`'s `loadoutJson` overrides real equipment state

`UniqueActorService.DeployAsync` (`UniqueActorService.cs:164`): `var effectiveLoadout =
UniqueLoadoutMerge.Merge(loadoutJson, _store.GetUniqueStatModsJson(instanceId));`.
`UniqueLoadoutMerge.Merge` (`UniqueLoadoutSpec.cs:151-165`), doc comment verbatim: *"Empty-ish deploy
(Parse.IsEmpty) falls back to mods; non-empty deploy wins."* Body: a non-empty caller `loadoutJson`
**replaces** the real, equipment-derived `mods_json` outright — not a merge, a full override. This is
exactly the seam the 2026-09-13 incident used, and it sits on the REAL, non-debug deploy endpoint —
proving that "used the real endpoint" is not by itself sufficient; a real endpoint with a
fabrication-shaped parameter is still a fabrication vector (`live-probe-standard.md` §2's rule already
covers this, but the ideal doc needed to name the concrete line).

Confirmed unused in production: the real web FE's sole deploy caller (`web/fusion-rpg-web/src/lib/
bus/mutations.ts:437-458`, `useDeployUniqueActor`) sends `{ col, row, correlationId, matchKey }` only —
`loadoutJson` is absent from both the TS type and the payload, and a repo-wide grep of `web/` for
`loadoutJson` returns zero hits outside tests. **A correct live probe must never populate it** — leave
it empty so `DeployAsync` falls through to the real persisted `mods_json`.

**Not a wiring gap to "fix" here** (`UniqueBoundLoadout`'s own doc comment already calls it "Not the
W8 gear shop — JSON/deploy stub only" — a known, named stub, not a hidden defect): the fix for THIS
program is procedural (never populate it in a probe), not a code change to `DeployAsync`. Whether the
parameter itself should eventually be removed once nothing legitimate uses it is a question for
`bound-loadout-hub`/the deploy program, out of this idea's scope.

### Real gap — XP has no computed-source path yet

`AwardXp`'s only guard is `delta > 0` (`RpgStore.UniqueActors.cs:1812`); the `reason` string is
explicitly unused for anything but audit (`_ = reason; // audit reason reserved`, line 1819). There is
currently **no** production caller that computes a delta from a real match/expedition outcome and
awards it through this endpoint — matching `unique-actor-runtime.md`'s own "Specimen XP curves /
balance: Stub thresholds shipped W8-B" non-goal note. A live probe calling this endpoint directly is
not fabricating a fake mechanism (there is no other mechanism to bypass yet) — it is exercising the
only entry point that exists, which is legitimate, but it proves "does XP → level compute correctly,"
never "does a real match reward XP" (that second claim has no code to test yet — a real gap, not
this program's to close).

### Real gap, found as a byproduct, unrelated to this program

`POST /api/delve/rooms/{id}/talk` and `/cage` (`DelveWildEndpoints.cs:62-65`, real, non-debug,
production endpoints) accept a caller-supplied `CreatureMintSpec{Rarity, Variant, TraitIds}`
(`DelveWildJoinRequest.Spec`, `CreatureDtos.cs:58-68`) with only catalog-known-id validation in
`MintCreatureUnlocked` — no independent server-side re-roll or bound check against what the delve
room's own resolved encounter should produce. This is a genuine trust gap in **production** code (a
player-reachable endpoint trusting a client-supplied rarity), not a debug-scope question at all. Named
here because it surfaced during this survey; **not this idea's to fix** — see Open questions.

### Structural observation — `DebugEndpoints.cs` already mixes both scopes correctly in two spots

Most of the file is `MapPost(g, path, "debug.xyz")` relays into the Injector (Game Injector Debug,
correctly shaped for what it is). `POST /api/debug/reforge-world` (line 457, "Pure DAL, no injector
round trip") and `POST /api/debug/derived-audit-actor` (line 519, its own comment: *"real UniqueActor
→ Hub → /sheet (never a synthetic 269 paint)"*) are already RPG-Server-Debug-shaped, in the same file,
under the same route group. The distinction this program needs already has two working examples to
copy — it does not need to be invented.

## Prior art

This is a software-engineering-practice question, not a game-genre one — the relevant prior art is
testing methodology, not PvZ mechanics.

- **Sociable vs. solitary unit tests** (Martin Fowler) — a sociable test lets the unit under test
  interact with real collaborators; a solitary test isolates it with test doubles. The "classicist"
  school defaults to sociable; the "mockist" school defaults to solitary. RPG Server Debug is the
  sociable/classicist shape; treating an Injector fabrication as proof of server behavior is neither —
  it is proving nothing about the server at all. ([martinfowler.com/bliki/UnitTest.html](https://martinfowler.com/bliki/UnitTest.html))
- **"Don't mock what you don't have to"** — a team principle (DFINITY, via David de Kloet) of mocking
  only the true external boundary (network/third-party) and letting everything else run for real.
  Maps directly onto "the RPG server is the real boundary; do not fake the Injector's or the server's
  own domain logic to get a green result."
- **Tautological tests** — a test that verifies data the test itself fabricated, rather than real
  behavior: *"if you cannot state the expected output without calling the implementation, you do not
  yet have a specification."* The T14 incident is a textbook case — the probe fabricated the loadout
  AND read the result back from the same fabrication surface, so a broken feature and a working one
  were indistinguishable by construction. ([vzurauskas.com — "Isolation makes tests tautological"](https://www.vzurauskas.com/2020/02/02/mocking-makes-tests-tautological))

No PvZ-genre or RPG-genre prior art applies here — this is not a player-facing mechanic.

## The shape

Two decisions, not two open questions — the engineering answer is clear enough to state as a
recommendation, per this skill's own "a recommendation nobody disputes is a decision" rule:

1. **Do not split `DebugEndpoints.cs` into two files yet.** It already contains two correctly-shaped
   examples in one file under one route group; splitting is a real refactor with no behavior change
   and no urgency — revisit only if the file's growth or reviewer confusion makes the mixing itself a
   problem. Cheaper first step: a one-line `// Game Injector Debug` / `// RPG Server Debug` banner
   comment above each route grouping, so the distinction is visible without a file move.
2. **The live-probe "recipe" is a documented sequence of the real endpoints above, not a new
   orchestration endpoint.** The chain is already five real HTTP calls (summon-or-`spawn-unique-actor`
   → aptitude allocate → equip → deploy-with-empty-`loadoutJson` → read back via `GET /api/unique/
   actors/{id}` and `GET /api/unique/actors/{id}/equipment`) — thin enough that a new server-side
   "prove" endpoint would just be one more thing that could itself drift from the real pipeline it's
   supposed to prove. Matches this repo's own `prove-hub-combat.ps1`/`prove-aptitude.ps1` precedent: an
   **operator script**, not a server capability, chains the real calls and asserts the read-back
   matches. Building that script (or extending an existing prove script) is implementation work for
   `/spec`, not this doc.

## Tunables

None. This is verification tooling, not a balance surface — nothing here is a number a balance pass
would touch.

## What this deliberately does not decide

- Does not redesign or split `DebugEndpoints.cs` (§ above already recommends against it for now).
- Does not fix `DeployAsync`'s `loadoutJson` override, or decide whether that parameter should
  eventually be removed — named as a known, already-documented stub, not re-opened here.
- Does not fix the `/talk`/`/cage` `CreatureMintSpec` trust gap — a real production-validation
  finding, out of scope for a live-probe-tooling idea.
- Does not redo the T12/T14 Actor Hub live proof itself — that is downstream execution work once a
  probe script exists, for the `actor-hub-and-combat-power-solid-fixing` program (or a successor) to
  run, not something this idea phase produces.

## Open questions

1. **The `/talk`/`/cage` `CreatureMintSpec` trust gap** (real production endpoint, caller-supplied
   rarity/variant/traits, weak validation) — fix now as a small separate task, or file it and let the
   delve/wild-join program pick it up on its own schedule? This is unrelated to live-probe tooling and
   was found only as a byproduct of this survey.
2. **Redo the T12/T14 Actor Hub proof now**, using the real chain this doc names (once a probe script
   exists per "The shape" §2), or treat that as separate follow-up work for
   `actor-hub-and-combat-power-solid-fixing` to schedule on its own?
