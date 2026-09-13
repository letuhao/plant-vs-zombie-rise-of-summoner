# Action system — closing the gap to playable — the ideal

**Status:** idea phase, 2026-09-13. Not a spec. No build authorized. This does not reopen any of
[action-ideal.md](action-ideal.md)'s 26 sealed decisions — it inventories what is real vs. inert in the
already-built engine those decisions produced, and proposes what closes next.

## Which loop this extends

[the-loops.md](../guide/the-loops.md) **Combat depth** ("the fight language every place uses") and
**Level up and power** (spine A — "Specimens, species, and types also level from work they did"). Also
touches **Idle expeditions** (auto-resolve, shipped), **Dungeon crawler — the Delve** ("interactive battle
stage shared with it," WIP), and **Farming/hunting/defending the empire** (sieges), because all four
route real battles through the one action engine (confirmed below — no parallel resolver exists).

This does **not** invent a new loop. It does not touch the lawn (PvZ mode is a stateless observer; actions
are a battle-mode concept only, action-map.md §10.2) and does not add a fourth stock, a class, or a
stamina gate.

## What this is

Today, a creature's fighting moves are supposed to grow with it: level up, sometimes learn a new skill,
choose which five you carry into a fight, and have that choice actually change what happens in battle.
The mechanism for all of that — the unlock ladder, the five-slot loadout, cost/cooldown, targeting,
even the seeded content generator — is fully built and tested in isolation. This ideal is about the
remaining step: making that mechanism actually run for a real player, in a real battle, instead of only
in tests and synthetic harnesses.

## What already exists

### Built — proven live, for a real player, today

| Area | Evidence |
|---|---|
| Full action engine (model, targeting, usability, costs, unlock ladder, dispatch, container binding, triggers, resource/shield grants, status apply, live stat modifiers, runner-path) | `action-map.md` §4–16, modules A1–A25, **all closed 2026-09-07**. Zero goldens moved across the whole build |
| Battle board / grid | **Corrects a stale doc claim.** `action-map.md` §12.3a (2026-09-06) says A10 has "zero production callers" — false as of this session. `WebMatchService.cs:151,218,361` build a real `NormalBattleBoard` for every real web match (replay + fresh); `DistrictAssaultResolver.cs:151-153` builds one for siege. `BattleRunState.PositionOf`/`TryMoveTowardNearestEnemy` consume it live |
| Movement kernel path | `BasicAttack.cs:347-351` dispatches a real `ActionCategory.Movement` action through `BattleRunState.TryMoveTowardNearestEnemy` (`:725-743`) using real `MoveAction.MoveToward`; `TryDeclareObjectiveAdvance` (`:287-318`) drives real `BoardPathfinder`. Kernel-complete |
| Reaction lane | `TimelineDispatch.cs:230-253` — a real defender-side `ReactionLane.TryEnter` → `ReactionCounter.TryCounter` path, distinct from guard-as-stance |
| Siege target-selection AI | `SiegeAiIntentSource.cs:167-281` — real hit-chance, missing-HP, killing-blow, threat scoring. Used in production (`DistrictAssaultResolver.cs:151-162`) |
| Party-dungeon (Delve) and siege both route through the one engine | `DelveBattleSession.cs:170` and `DistrictAssaultResolver.cs:151` both call the same `BattleEngine.Resolve`/`ActionRunner`/`TimelineDispatch`/`UsabilityEvaluator` kernel 1v1 and expeditions use. **No parallel/legacy combat resolver exists anywhere** |
| Level-up → unlock roll → grant, the full chain | `AwardUniqueActorXpUnlocked` (`RpgStore.UniqueActors.cs:1829`) → `TryRollActionUnlocks` (`:1895`) → `ActionUnlockGrantService.TryRollOnce` → `ActionEligibility.Candidates` → `UnlockState.TryAccept` (`UnlockState.cs:81`) → `RpgStore.UpsertGrant`, one transaction. Code-complete and correct |
| Real match reads a real specimen's grants + loadout | `WebMatchService.cs:565,684-704` — `EquippedActionIdsFor` merges real unlock-ladder grants + item-granted actions, then `GetLoadoutOrAutoEquip` — a persisted loadout wins, auto-equip is the live fallback |
| Commander's own loadout, REST | `GET/POST /api/loadout/{playerId}` (`LoadoutEndpoints.cs:43-71`, wired at `Program.cs:782`) — real, validated, reachable |
| Seedsmith corpus import (partial) | `Program.cs:395-408` → `ActionCorpusImporter.Import` (`ActionCorpusImporter.cs:26-78`) — real, non-test import into `rpg_action`/`rpg_action_cost` |

### Wiring gap — the actual punch list, ranked by leverage

| # | Gap | file:line | Why it's a wiring gap, not a wall |
|---|---|---|---|
| **1** | **The unlock ladder is dark for every real player.** `RpgStore.UniqueActors.cs:1902` early-returns (`if (UnlockTuningPolicy.Tuning is not { } tuning) return;`) before `Candidates`/`TryAccept`/`UpsertGrant` ever run | `UnlockTuningPolicy.Configure(...)` (`UnlockTuningPolicy.cs:15`) is **never called anywhere under `src/`** — only from test fixtures and `tools/ProveHubCombat`. `Program.cs`, the real host, never calls it | A **single missing call at server startup**, reading the already-shipped `data/tuning/action-unlock.v1.json`. Every downstream piece (eligibility, ladder math, grant write, equip read) is already correct once fed a non-null tuning — proven by the same tests that exercise it |
| **2** | **A specimen's own loadout has no REST/SignalR surface.** Only the commander (`OwnerKind.Player`) has one | `LoadoutEndpoints.cs:74` hardcodes `OwnerKind.Player`. No `GET/POST` threads `OwnerKind.Entity`/`OwnerKind.UniqueActor` + `instanceId` anywhere in `src/FusionRpg.Server/**` | `RpgStore.Loadouts.cs`/`GetLoadoutOrAutoEquip` are already generic over owner scope (`WebMatchService.cs:684-704` already calls them this way for battle setup) — this is routing an existing generic store call through a new endpoint, not building a new mechanism |
| **3** | **No discard endpoint.** `DiscardPolicy`/`UnlockDiscardService` (T20, built, 12 tests) has zero Server caller | searched `Discard`/`Withdraw` against `rpg_action_grant`/`UnlockState` in `src/FusionRpg.Server/**` — zero hits | Same shape as #2 — the policy object exists and is tested; nothing calls it from an endpoint |
| **4** | **FE has an explicit stub, not a missing file.** `ActionsTab.tsx:7-14` states in its own text: *"no catalog exists yet... Unlocks once the action system ships (approved, not yet built)"*, renders `PLACEHOLDER_ACTIONS` as locked slots (`:118`) | `web/fusion-rpg-web/src` has **zero** references to `/api/loadout` anywhere | Only auras (`useAuraCatalog`/`useAuraRuntime`) are live-wired in the same file — proving the pattern for wiring a real catalog + real mutation calls already exists in this exact component, unused for actions |
| **5** | **Corpus importer reads 2 of 4 accepted files.** `committed-round-909.json` (102 briefs) and `committed-round-2000.json` (53 briefs) are never read by `src/`; only `committed-round-1.json`+`committed-round-2.json` (24 rows total) are | `Program.cs:395-408`'s literal `new[] { "committed-round-1.json", "committed-round-2.json" }` | Same importer, same schema family (per the action-corpus audit, all four files are `ActionCorpusBriefJson`-shaped) — likely a literal-list edit, **but see Open Questions** on whether the newer two files' schema evolution needs an importer check first |
| **6** | **`move.range` is granted by no content.** A9's kernel path (row above) never fires because no actor ever has a non-zero move range | `DerivedStatChannels.MoveRange` (`:525`) has zero non-test grant sites; `BasicAttack.cs:312,344-346` says so in its own comments | Kernel is done; this is a content-authoring gap, same shape as the action-corpus's own thin-fill problem |
| **7** | **Poise is granted by no content outside the delve profile.** A8's reaction lane is `wReact=0` for classic/galaxy/hybrid-atb/siege (`battle.v5.json:29,34`), `wReact=1` only for delve (`battle.v4.json:38,44`, itself commented "inherited and inert"). `ReactionCounter.cs:41` requires a `PoiseLedger` grant that nothing produces | same shape as #6 — a tuning value plus a missing content grant, not a missing mechanism |
| **8** | **Action *choice* among a real multi-skill loadout is fixed preference order, not a decision.** Both AIs (`StubIntentSource.cs:64-75`, `SiegeAiIntentSource.cs:112-123`) loop `heldActions` pre-sorted by `ActionTagPreference.Compare` (tag rank, then `action_id` ordinal) and fire the first usable one | `ActionTagPreference.cs:44-48` | **Superseded 2026-09-13 — see [action-choice-ideal.md](action-choice-ideal.md).** This is not a low-priority AI-polish item: with every shipped consumer (expeditions, siege) auto-resolved, this tiebreak is the *only* mechanism by which a build ever produces a different outcome. The alphabetical `action_id` tiebreak means a rung-9 unlock-ladder skill can be permanently shadowed by a rung-1 basic of the same tag whose id sorts first — provably, not hypothetically. Un-deferred; split into **A31** `action-choice-rung-tiebreak` (small, no new tunables, sequence right after A26) and **A32** `action-choice-condition-awareness` (larger, separable) |

### Real gap — nothing exists yet, and that is correct

| Gap | Why it's correctly deferred, not missed |
|---|---|
| **Player-driven, turn-by-turn interactive skill choice** (a human picking which of their 5 equipped skills to use, mid-battle) | `the-loops.md` §2 already names this as its own, separate, not-yet-built surface: *"interactive battles you play yourself **WIP**"* — expeditions stay auto-resolve by design (spine sibling, never replaced). This ideal's bar for "playable" is a real loadout changing a real auto-resolved outcome, not a battle UI |
| **Fog of war / line of sight wiring into any live path** | Real, tested infrastructure exists (`FoggedBattleView.cs`, `SiegeVisibility.cs`, `LineOfFire.CanFire`) with zero production callers — but this is an **owner-deferred decision already on record** (`action-map.md` §10.4d: *"fog of war ships later... the expensive part is not the geometry, it is that the battle stops having one true state"*), not a gap this ideal proposes closing |

## Prior art

**Utility AI scoring for action choice** (Game AI Pro, ch. 9 & 13; Wikipedia "Utility system"): score each
usable action 0–1 on factors like hit chance, expected damage, resource remaining, own HP; take the max,
or weighted-random over the top scores. This is the standard fix for gap #8 — replacing
`ActionTagPreference`'s fixed ordinal sort with a real per-turn score is a small, well-understood pattern,
not a research problem. [Game AI Pro ch. 9](https://www.gameaipro.com/GameAIPro/GameAIPro_Chapter09_An_Introduction_to_Utility_Theory.pdf) ·
[ch. 13](https://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter13_Choosing_Effective_Utility-Based_Considerations.pdf) ·
[Utility system (Wikipedia)](https://en.wikipedia.org/wiki/Utility_system). This repo already has an
`ai-behavior-trees-utility-ai` skill covering this exact pattern (response curves, considerations,
action evaluators) — reuse it rather than re-deriving the math at spec time.

**"Built but dark" as a known, named failure mode**: this is exactly the *dark launch* pattern from
continuous-delivery practice — code deployed to production but gated by a flag/config nobody has set,
producing a system that is provably correct in tests and provably inert for every real user. `UnlockTuningPolicy.Configure`
never being called is textbook: the fix is the same as the industry's — call the configuration step once,
at host startup, the same place every other `*TuningLoader`/`*TuningHub` in this codebase already gets
configured. [The Only Guide to Dark Launching](https://launchdarkly.com/blog/guide-to-dark-launching/) ·
[Feature Flags 101](https://launchdarkly.com/blog/what-are-feature-flags/).

## The shape

**This is a sequencing question, not a design question.** Every piece named above already has a correct,
tested shape — action-ideal.md's 26 decisions and A1–A25's implementations are not being revisited. The
work left is wiring closure, in an order where each step is provable independently and nothing is claimed
"playable" on an assumption:

1. **Flip the unlock-ladder gate** — call `UnlockTuningPolicy.Configure` once at server startup, reading
   the tuning file that already ships. Near-zero risk (the whole downstream chain is already tested
   against a configured tuning); unblocks everything else, because nothing below is worth building against
   a specimen that can never actually earn a second action.
2. **Specimen loadout endpoint** — thread `OwnerKind.Entity/UniqueActor` + `instanceId` through the
   existing generic `RpgStore.Loadouts` calls, mirroring the commander endpoint's shape. Without this, #1
   changes what a specimen *holds* but a player still cannot see or choose it.
3. **Corpus 4-file import** — more real content immediately, if the schema check in Open Questions comes
   back clean.
4. **FE `ActionsTab` real wiring** — replace `PLACEHOLDER_ACTIONS` with a real catalog call against #2's
   endpoint, following the exact pattern already proven for auras in the same file.
5. **Action-choice scoring** — only meaningful once #1–#2 mean a real loadout can hold more than one real
   skill at once.

**What counts as "playable" at the end of this**: a real specimen can level up, actually gain a real
second action, a player can see and choose their 5, and an auto-resolved battle report measurably differs
between two different real loadouts on the same specimen — the acceptance bar A20 (`synthetic-loadout-harness`)
already defined, now provable on real content instead of only synthetic. Interactive per-turn play (the
real gap above) is explicitly a later, separate surface.

**Alternative shape rejected**: building FE/scoring first, against synthetic data, to "make progress in
parallel." Rejected because #1 is the actual root cause — every visible symptom above (empty ActionsTab,
"nothing to choose from") traces back to it, and building on top of a dark system means re-verifying
everything the moment it turns on, rather than once, in order.

## Tunables

No new balance numbers exist yet. If gap #8 (action-choice scoring) is picked up, it introduces a new
tunable surface — per-factor weights (hit%, damage, resource-remaining, own-HP) — which belongs in a new
`data/tuning/action-ai-selection.v{n}.json`, not a `const`, per this repo's tunables-ssot rule. Not
authored here; this ideal only names that it will exist.

## What this deliberately does not decide

- Does not reopen any of action-ideal.md's 26 sealed decisions (three action kinds, guard-as-stance,
  cost/cooldown shape, duration-rides-the-ladder, etc.) — all stay as sealed.
- Does not decide the FE layout/visual design of a real `ActionsTab` — that is `/idea-ui` territory if it
  turns out to need one (this ideal only says the API call needs to exist; the component already exists
  and already knows how to render locked/unlocked slots for auras).
- Does not decide the exact utility-AI weight values for gap #8 — only that the pattern (score, take max)
  is the established fix, with numbers as a future tunable.
- Does not decide whether/when to author real content granting `move.range` or `poise` (gaps #6/#7) —
  that is a balance-content decision downstream of this wiring closure, not part of it.
- Does not touch fog of war, line of sight, or the reaction lane's `wReact` value outside delve — all
  three are on record as owner-deferred, not reopened here.

## Open questions

**Q1 — does the C# importer already handle the newer two corpus files' schema, or does it need updating
first?** `committed-round-909.json`/`committed-round-2000.json` were produced by a later, fixed generator
(post the 2026-09-13 distribution-gaps audit: G1/G2 pairing fixes, `affixClass` split, `ALGORITHM_VERSION`
bump). `ActionCorpusImporter.Import` (`ActionCorpusImporter.cs:26-78`) has only ever been run against
`committed-round-1/2.json`. Answerable by running the importer against the other two files and reading
the result — a task, not a design question, but it gates whether gap #5 is a one-line literal-list edit
or needs an importer patch first.

**Q2 — RESOLVED 2026-09-13, reversing the original recommendation.** Originally: defer gap #8, since
"scoring a choice among skills nobody can hold yet is designing against data that does not exist." The
owner correctly pushed back: this game is automation-first (expeditions, siege — the only shipped battle
consumers — are both auto-resolved), so action *choice* is not AI polish layered on a human-played game,
it is the mechanism. See [action-choice-ideal.md](action-choice-ideal.md): split into a small, zero-new-
tunable fix (**A31**, rung-descending tiebreak, sequence right after A26) that does not need real content
to design against — it is provably correct against *any* two actions of different rung — and a larger,
separable piece (**A32**, condition-awareness) that can follow.
