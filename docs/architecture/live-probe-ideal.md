# Live probe (RPG Server Debug) — the ideal

**Status:** idea phase, 2026-09-13, adversarially audited same day (3 parallel reviews: citation
fact-check, live-engine-proof-gap check, design-decision steelman — all findings folded in below,
not left as a separate report). Not a spec. No build authorized.

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
| Fusion | `POST /api/fusion/execute` | `FusionEndpoints.cs:30` → `RpgStore.ExecuteFusion`; Recipe mode mints via the SAME `MintCreatureUnlocked` (`RpgStore.Fusion.cs:298`), StarMerge updates the existing row in place (`RpgStore.Fusion.cs:109-139`, the `UPDATE rpg_creature_profiles SET star = …` itself at line ~132). `FusionHttpRequest` (`FusionEndpoints.cs:249`) is likewise selection-only |
| Delve altar pull | `PullAtAltar` → `CloseDelve(Extracted)` | `RpgStore.Delve.cs:1418`, `:950` — mint uses server-rolled `entry.Rarity`/`Variant`/`TraitIds` from the haul, not caller input |
| Identity-only debug shortcut | `POST /api/debug/spawn-unique-actor` | `CreatureEndpoints.cs:187`, `SpawnUniqueActorRequest` (`:294`) is `PlayerId, Side, GameTypeId` only — despite the `/api/debug/` prefix this is a **legitimate** RPG Server Debug shortcut (skips the RNG/soul cost of a real summon, produces an identical row through `store.CreateUniqueActor`), not a fabrication vector. Correctly shaped already |
| Level / XP | `POST /api/unique/actors/{id}/xp` | `UniqueActorEndpoints.cs:113` → `ua.AwardXp` → `RpgStore.AwardUniqueActorXp` (`RpgStore.UniqueActors.cs:1808`). Only checks `delta > 0`, not its source — see Real gap below |
| Aptitude allocation | `POST /api/aptitudes/unique/allocate` | `AptitudeEndpoints.cs:64` — real, budget-checked, persisted. **Already proven correct** by the 2026-09-13 T12 probe (30 pts → `Might`, `bonusAtk=1060`, `appliedAtk=1080`, matched live) |
| Equip | `POST /api/items/equip`, `PUT /api/unique/actors/{id}/equipment/{slot}` | **Not symmetric — corrected after audit.** `POST /api/items/equip` (`ItemEquipEndpoints.cs:104-117`) hard-checks the item is real, stored, owned by this player, and `Disposition == "owned"` — genuine fabrication-proof. `PUT /api/unique/actors/{id}/equipment/{slot}` (`RpgStore.UniqueActors.cs:1408`) checks only `UniqueEquipmentCatalog.IsKnownItem`/`SlotMatchesItem` — a **fixed, explicitly-documented stub allowlist** (`UniqueEquipmentCatalog.cs:37-38`: "item-id allowlist only, not combat SSOT"), with **no per-player ownership check**. Not a fabrication risk in practice (magnitudes come from fixed seeded containers, not caller input) but a live probe should prefer the `/api/items/equip` path where the item in question is a real rolled instance, and name the PUT route's stub nature rather than call it equally "hard-validated." Both trigger the same real `mods_json` rebuild: `RebuildUniqueModsFromEquipmentUnlocked` (`RpgStore.UniqueActors.cs:1774`) |
| Deploy | `POST /api/unique/actors/{id}/deploy` | `UniqueActorEndpoints.cs:43` → `UniqueActorService.DeployAsync` — real, persisted, real board placement. **Its own `loadoutJson` parameter is the defect seam** — see below |

**This chain is ~90% already built and real.** The idea that a live probe *has to* fabricate an actor
is false — it was a shortcut taken under time pressure, not a missing capability.

**Second stub found by the same audit, same shape as `loadoutJson`:** `UniqueEquipmentCatalog` is
itself documented as a stub, not combat SSOT (`UniqueEquipmentCatalog.cs:37-38`). Two independent
"this is a placeholder, not the real gear system" seams sit on the equip/deploy path — worth the
`bound-loadout-hub`/equip-program owners knowing they're the same class of debt, not raised further
here.

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

### Real gap — no server-side compose read-back exists for a UniqueActor's derived combat stats

Verified independently by two audit passes: `GET /api/unique/actors/{id}` returns `UniqueActorDto`
(`UniqueActorDtos.cs:15-30` — instanceId, playerId, side, typeId, phase, level, xp, matchKey, lastPtr,
correlationId, revision, timestamps) and `GET /api/unique/actors/{id}/equipment` returns
`UniqueEquipmentListDto` (`:71-77` — slot/itemId list and the raw `modsJson` string). **Neither field
set includes a composed atk/maxHp/derived-combat value; neither handler calls anything resembling
`ActorHub.Resolve`.** The only endpoint in this whole area that computes anything derived is
`AptitudeEndpoints.ProjectUniqueState`, and even that returns budget/spent/shares, not atk/maxHp.

This means the two GETs, by themselves, can only ever prove **inputs were persisted correctly**
(level, xp, equipment assignment, raw `mods_json`) — never that a bonus actually reached
`AppliedCombat` the way `ActorHub.MergeAppliedCombat` composes it. For a combat-stat feature like
`bound-loadout-hub`, the composed value is **only observable at all** through the Injector's own
live `CheatState.ActorHub` instance — there is no Server-side equivalent to check instead. This is
not a gap this program should close (building a Server-side ActorHub-equivalent compose-and-expose
endpoint would be significant new work, is arguably a duplicate compose per `DESIGN-GATE.md` §2.15's
"ActorHub sole Hot compose" invariant, and is out of scope for a testing-tooling idea) — it is a
structural fact "The shape" below must design around: **the live-engine half of a T12/T14-style
probe is not an optional nice-to-have on top of the Server reads — for a combat-stat claim
specifically, it is currently the only place the composed value can be observed.**

### Not a real gap this program found — already a named, owned, larger gap in `party-dungeon`

`POST /api/delve/rooms/{id}/talk` and `/cage` (`DelveWildEndpoints.cs:62-65`) accept a caller-supplied
`CreatureMintSpec{Rarity, Variant, TraitIds}` with only catalog-known-id validation. **Corrected after
reading `tasks/party-dungeon-todo.md`'s D4.8 entry directly (2026-09-13):** this is not a fresh finding
— it is the exact same gap that task's own "Still not built, named precisely" note already identifies
(2026-09-07): `TalkTree`'s multi-verb `Step`-style orchestrator does not exist anywhere, so `talk`/
`cage` are deliberately scoped to COMMITTING an already-resolved outcome only, not resolving one from a
bare verb. `docs/architecture/party-dungeon/spec-wild-room.md` is a **fully owner-approved, unbuilt**
spec for exactly this orchestrator (§2's whole verb/eligibility/outcome-draw table). Fixing this
properly means building that already-spec'd `party-dungeon` module — real, large, and already owned;
forking it into `live-probe` would duplicate ownership of an approved module. Handed off, not built or
re-specified here — see the capability map's own note and `tasks/party-dungeon-todo.md` D4.8's
cross-link.

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

Two decisions, strengthened by an adversarial audit pass (2026-09-13, same session) that steelmanned
the opposing choice against this repo's actual conventions rather than generic practice — both
decisions survived, but neither survived unchanged:

1. **Do not split `DebugEndpoints.cs` into two files yet — but name the guard that replaces "revisit
   if it becomes a problem."** The original soft trigger ("revisit if growth/confusion makes the
   mixing a problem") was itself a defect by this repo's own `planning-and-task-breakdown` rule that a
   gate needs a named resolver, not a vibe. The audit found the fix does not require a file split at
   all: every existing `DebugEndpoints.cs` route already has a **mechanically greppable** signature —
   an RPG-Server-Debug-shaped handler calls `store.*`/`ua.*` (a real domain/persistence method)
   directly in its body, while a Game-Injector-Debug relay is uniformly a `MapPost(g, path,
   "debug.xyz")` wrapper that only forwards a command string. **`scripts/guard-debug-scope.ps1`** (not
   yet built — named here as the concrete follow-up, matching this repo's `guard-*.ps1` convention for
   every other boundary rule, e.g. `guard-single-writer.ps1`, `guard-actor-hub.ps1`, none of which are
   comment-enforced) can assert this shape today, from the handler bodies, with zero refactor
   prerequisite. A banner comment (`// Game Injector Debug` / `// RPG Server Debug` above each
   grouping) is still worth adding for human readability, but it is not what makes the rule
   enforceable — the guard is. File split stays deferred; the guard does not.
2. **The live-probe "recipe" is an operator script, not a new server endpoint — but this is net-new
   engineering, not a citation of existing precedent.** The audit read `prove-hub-combat.ps1` and
   `tools/ProveHubCombat/Program.cs` directly: it drives `RpgStore.InMemory()` **in-process**, with
   **zero `HttpClient` usage** anywhere in the file — it proves an offline/in-memory invariant, not a
   live server + live game over real HTTP. Citing it as "this repo's own precedent" for a live,
   multi-step, real-HTTP-plus-async-SignalR-ack probe overstated the support that citation carries;
   corrected here. The decision itself still holds, for a sharper reason than "matches precedent": a
   new server-side "prove" endpoint would be a second orchestrator of the same real calls the web FE
   already makes, in its own sequence, that must independently stay in sync with the FE forever — the
   same shape `DESIGN-GATE.md` §2.15 (SOLID) already bans for a combat compose ("a second `*Composer*`
   or private fold... never a template"), applied here to orchestration instead of composition. An
   operator script that calls the SAME real HTTP endpoints the FE calls, in the SAME sequence, has no
   independent logic to drift — only real work correctly reused. Building it means real
   `Invoke-RestMethod` calls plus the same async ack/poll pattern `DebugEndpoints.cs` already uses
   server-side for injector round trips (`PollForKind`, e.g. lines 85/373/382) — genuinely new
   scripting work, not "extend an existing prove script."

   **The recipe's own missing step, found by the same audit:** as drafted, the chain stopped at
   the two Server GETs — which, per the real gap above, can only prove **persisted inputs**, never
   that the composed value reached `AppliedCombat`. The corrected six-step recipe:

   1. Summon (or `POST /api/debug/spawn-unique-actor` for the identity-only shortcut)
   2. `POST /api/aptitudes/unique/allocate`
   3. `POST /api/items/equip` (prefer this over the PUT stub route — see the Equip row above)
   4. `POST /api/unique/actors/{id}/deploy` — **`loadoutJson` omitted/empty, always**
   5. Read back persisted state: `GET /api/unique/actors/{id}` + `.../equipment` — proves inputs
      were persisted correctly. **This is not yet a live proof.**
   6. **Separately, labeled as the distinct live-engine claim** (`live-probe-standard.md` §1's "may
      NOT prove" row): call `debug.board-stats` for the deployed specimen's ptr
      (`CheatCommandRunner.cs:325` → `DebugRuntime.BoardEntityStats()`, `DebugRuntime.cs:183-213`,
      relayed at `DebugEndpoints.cs:314` — reads the actual live Unity fields: `attackDamage`,
      `thePlantHealth`, `thePlantMaxHealth`, the exact fields the 2026-09-13 incident compared) and
      assert they match step 5's persisted values. **A probe that stops at step 5 and calls that
      "proven" repeats this program's own founding mistake, just shifted from over-trusting an
      Injector read to over-trusting a Server read.**

## Tunables

None. This is verification tooling, not a balance surface — nothing here is a number a balance pass
would touch.

## What this deliberately does not decide

- Does not redesign or split `DebugEndpoints.cs` (§ above already recommends against it for now).
- Does not build `guard-debug-scope.ps1` — named as the concrete follow-up mechanism, not built here;
  writing and wiring it into `deploy-play.ps1`/CI is implementation work for `/spec`.
- Does not build the six-step operator script itself — the recipe and its evidence are specified
  above; the script is implementation work for `/spec`.
- Does not fix `DeployAsync`'s `loadoutJson` override, or decide whether that parameter should
  eventually be removed — named as a known, already-documented stub, not re-opened here.
- Does not fix the `UniqueEquipmentCatalog` stub-allowlist gap on the PUT equipment route — named
  alongside `loadoutJson` as the same class of debt, not re-opened here.
- Does not build a Server-side compose-and-expose endpoint for a UniqueActor's derived combat stats —
  considered and rejected above as a likely duplicate-compose (`DESIGN-GATE.md` §2.15); the Injector's
  `debug.board-stats` remains the only place this claim is observable, by design, not by gap.
- Does not build the `/talk`/`/cage` `CreatureMintSpec` fix — it is `party-dungeon`'s own `wild-room`
  module (§ above), already approved and unbuilt; cross-linked into `tasks/party-dungeon-todo.md`
  D4.8, not forked into this program.
- **Does** redo the T12/T14 Actor Hub live proof, using `live-probe-tool` — see the capability map's
  `actor-hub-live-proof` module.

## Open questions (both resolved 2026-09-13)

1. ~~The `/talk`/`/cage` `CreatureMintSpec` trust gap~~ — **resolved: hand off, not build here.** It
   turned out to already be `party-dungeon`'s own named, approved, unbuilt `wild-room` module, not a
   fresh finding. Cross-linked into `tasks/party-dungeon-todo.md` D4.8 rather than specced or built
   under `live-probe`.
2. ~~Redo the T12/T14 Actor Hub proof now?~~ — **resolved: yes, in this program.** `actor-hub-live-
   proof` is a module in `live-probe-map.md`, depending on `live-probe-tool`.

No open questions remain blocking `/spec`.
