# Action skill tiers — the ideal

**Status:** idea phase, 2026-09-12. Not a spec. No build authorized.

**Program:** `action-skill-tiers`. Map (when approved) → `docs/architecture/action-skill-tiers-map.md`; plan → `tasks/action-skill-tiers-plan.md` + `tasks/action-skill-tiers-todo.md`. Parent programs: `action` (sealed ideal), `action-corpus` (sealed idea, approved map). This doc does not amend either; it names the tier layer both need.

## Which loop this extends

- **Spine C — item collection and progression** (`docs/guide/the-loops.md`): find, equip, compare, craft, socket, salvage. Skill tiers give the same "later hunt stays relevant, earlier tier stays useful" shape items already have.
- **Combat depth** (hangs on places, not an eleventh loop): the fight language every place uses — element ring, shields, statuses, crit. Tiers vary what a skill can hold, never invent a second ring or new elements.
- **Spine A — level up and power**: Dave's level is the main line, endless grind, no cap. A tier never caps it.

A feature that needs a new named loop stops here — adding a loop is an owner edit to `the-loops.md`. This one does not.

## What this is

In the player's language: some skills are simply bigger versions of the same idea, and some skills are rarer shapes. A low-tier spark and a high-tier spark share a name and a job; the high one hits harder, costs more, and may carry a rider the low one cannot. The game tells you which tier a skill is, where it can drop, and what it takes to hold it — no wiki, no second lottery.

In system language: **actions get the same two-part tier discipline items already have — an atom tier (`t1–t5`, how strong one affix is) plus a rung window (which tiers this skill may roll), with rung playing the role rarity plays for items.** No new roll, no new curve, no new column.

Load-bearing principles, restated where they bite (a downstream session reads this doc, not its links):

- **Every RPG feature lives in the RPG layer, never by changing what PvZ is.** A skill tier never needs a Unity field, a plant/zombie stat, or a lawn representation. It resolves in `DamagePacket` → dispatcher → shield/combat math → Funnel → FA10, all of which run during a lawn match. "Does the lawn support tier X" is the wrong question; "does the RPG layer have a channel/atom/runtime for it, and is it wired?" is the right one.
- **One power ladder, no private `f(level)`.** Contests read `Θ` (linear, difference-based); magnitudes read `P(Θ)`. A tier step reuses the shipped `1.75` ladder (`m_t = m1 · 1.75^(t-1)`), never invents a skill-only curve. A rung step reuses `qPower(r) = 1.75^((r-1)/2)` — two rungs make one shipped tier.
- **The balance surface is data.** Every number below lives in `data/tuning/<domain>.v{n}.json`, published through `tools/tuning/publish.py`, never hand-edited, never a `const`. A structural constant (circuit size 4, capacity ceiling 8, `PowerVector.One = 1000`) stays a `const` with a comment saying why it is not tunable.
- **No hard progression ceilings.** A tier is a window, not a wall. Caps on magnitudes are soft/configurable; absolute bounds throw, never clamp silently. Rung keys on monotonic earn history that never rewinds, so everything eventually climbs.
- **Gameless-first is capability, not the pitch.** A tiered skill must work with the game closed after it unlocks. The injector may enrich (capture, blessing, deploy), never permanently gate.
- **Seed → concrete → per-player.** Seedsmith emits seeds (enums, offline, committed, no magnitudes). The runtime rolls concrete per player, seeded, like Diablo loot. The model writes identity; deterministic code writes magnitude — enforced by schema audit, never by review.
- **A guardrail validates the contract and closed enums, never a population count or generated text.** Corpus sizes (3,307 today) are readings that grow as content ships, never acceptance values. Tests assert joins, uniqueness, determinism, envelopes, and closed-vocabulary membership.

## What already exists

Verified against `src/` + `data/` + `tools/seedsmith/` on 2026-09-12 by three parallel surveys. Three buckets, exact words.

### Built

- **No `Tier` on `ActionRow`; actions carry `Rung 1–10`. `t1–t5` is the atom axis plus a tier window.** `src/FusionRpg.Core/Actions/ActionRow.cs:37` (`public int Rung { get; init; }` — "Never a magnitude"); `src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:154` (`MinTier`/`MaxTier` — "The tier window the pool may offer"); `src/FusionRpg.Core/Effects/Atoms/VariantShift.cs:31` (`MaxTier = 5`); `src/FusionRpg.Core/Actions/Rungs/RungRow.cs:22` (`MinTier`/`MaxTier` — rung→atom-tier mapping).
- **Rung table ships with tier windows.** `data/tuning/action-rungs.v1.json:11-20` pairs `(1-1,1-1,2-2,2-2,3-3,3-3,4-4,4-4,5-5,5-5)`; `:9` `"cap": 10`.
- **`rung(n) = min(earnCount, cap)` holds literally.** `src/FusionRpg.Core/Actions/Unlock/UnlockLadder.cs:76` (`return new EffectiveRung((int)Math.Min(earnCount, tuning.RungCap))`); `:57` ("the same earnCount always get the same rung"). Holder stores only `EarnCountAtAcceptance`, recomputed per read — `src/FusionRpg.Core/Actions/Unlock/UnlockState.cs:46`.
- **Rung row shape is power + cost + structure.** `src/FusionRpg.Core/Actions/Rungs/RungRow.cs:24` (`PoolRolls, QPowerMilli, CostMulti, CdMulti, StructureBudget`); `data/tuning/action-rungs.v1.json:6` (`power x9.38, cost x13.15 across rungs 2–10, a 1.40x tax`); `qPower(r) = 1.75^((r-1)/2)`, never evaluated at runtime — `RungRow.cs:5`.
- **Structure budget enforced at load.** `src/FusionRpg.Core/Actions/Rungs/RungTable.cs:39` (closed 7 axes `ScopeSplit, RiderStatus, Condition, Sequence, Consumption, Reaction, Restriction`); `src/FusionRpg.Core/Actions/StructureBudgetGuard.cs:48` (`rung {row.Rung} does not budget for axis '{axis}'`).
- **Eligibility tiers exist and are wired.** `src/FusionRpg.Core/Actions/ActionRow.cs:77` (`Scope` default `General`); `:83` (`ScopeKey` opaque); `src/FusionRpg.Core/Actions/Eligibility/ActionEligibility.cs:34` (`Candidates` implements `general ∪ family ∪ species`); `src/FusionRpg.Core/Actions/Unlock/ActionUnlockGrantService.cs:72` (production caller, not tests); `src/FusionRpg.Core/Actions/ActionEnums.cs:84` (`enum EligibilityScope`).
- **`RungBand` window with ceiling collapse.** `src/FusionRpg.Core/Actions/ActionRow.cs:116` (`record RungBand(int Floor, int Ceiling)`); `:118` (`Collapse() => Ceiling`).
- **Runtime roll reuses the item draw verbatim, no second roll.** `src/FusionRpg.Core/Actions/Seeding/ActionSeeder.cs:47` (`Instantiator.Draw(container, ...)`); `:19` ("only its visibility widened"); `src/FusionRpg.Core/Effects/Atoms/Instantiator.cs:207` (`public static List<string> Draw(`); `docs/architecture/action/spec-action-seeding.md:54` ("An unlocked action is a container roll, and its rung is its rarity").
- **Item tier ladder to copy.** `src/FusionRpg.Core/Effects/Atoms/FamilyExpansion.cs:28` (`TierCount=5, MagnitudeRatioPermille=1750, BandFloorPermille=670, BandCeilingPermille=1330`); `data/seed/items/_tuning/tier-bands.v1.json:45` (`bandCount:5, referenceLevel:20`); `docs/architecture/item/spec-affix-legality.md:146` (D29 ships `t1@1,t2@1,t3@8,t4@18,t5@32`); `:159` (collapsing envelope `env.maxTier=min(band.MaxTier,maxTierAt(ilvl))`); `docs/architecture/effect-atom/definitions.md:40` (`atom_id` grammar `{family}[.{variant}].t{tier}`, derived not authored); `:51` (`UNIQUE(family_id,tier,variant)`).
- **Tier-access-gate A-G1 built and wired.** `data/tuning/action-rungs.v2.json:10` (`powerBudgetMilli(r) = poolRolls(r) * referencePower * qPowerMilli(r) / 1000`); `RungRow.PowerBudgetMilli`, `StructureBudgetGuard.UndetectableAxes()`, caller `RpgStore.ActionCatalog.cs:92-119` — `docs/architecture/action-corpus-map.md:112`.
- **Seedsmith action adapters exist.** `tools/seedsmith/seedsmith/adapters/actions/kinds.py:24` (`ACTION_SEED_REQUIRED` carries `scope, category, rungBand, targetMode, relation, atomFamilies, pairingRole`); `:75` (`len(KINDS) == 10`); `generate_general_actions.py:1`, `generate_family_actions.py:1`, `generate_signature_actions.py:1`; P1 enforced by `tools/seedsmith/seedsmith/pipeline/model.py:146` (bare numeric field rejected mechanically by `audit_schema`).
- **Corpus scale is a reading.** `docs/architecture/action-corpus-map.md:30` (roster and plan sizes are readings, not constants; assert joins, never totals); `docs/architecture/action-corpus-ideal.md:18` (current position 3,307 — 2,712 signature 3/species, 95 family, 500 general — supersedes Part I's ~1,000).

### Wiring gap

- **Scope-clamp never reaches the guard.** `src/FusionRpg.Core/Actions/Unlock/UnlockLadder.cs:71` (`EffectiveRung(earnCount, tuning)` — still 2-arg, no `scopeMax`); `src/FusionRpg.Core/Actions/StructureBudgetGuard.cs:41` (reads authored `row.Rung` only, never holder `EffectiveRung`). A scope ceiling gates magnitude but not structure today. This is unfinished wiring, never an architectural limit.
- **Rung row still carries single `PoolRolls`; split lives only on containers/rarity.** `src/FusionRpg.Core/Actions/Rungs/RungRow.cs:24` (singular) vs `src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:165` (`PrefixRolls`/`SuffixRolls`); adapter puts the whole roll on one side — `src/FusionRpg.Core/Actions/Corpus/ActionCorpusComposer.cs:112`.
- **Rarity-band ranges not representable.** `RpgStore.Containers.cs:54` + `ContainerRow.cs:207` still single ints; `docs/architecture/item/spec-rarity-bands.md:36` asks for nullable `prefix_rolls_max`/`suffix_rolls_max`. Same shape would bite per-tier roll ranges if copied blindly.
- **Coverage pipeline mechanically sound but open-loop.** `docs/architecture/action-corpus-map.md:190` (S5→S1 top-up never wired; `compute_verdict` ignores `gates=False`); `tools/seedsmith/seedsmith/adapters/actions/generate_general_actions.py:11` ("Acceptance is A-S3's; persistence is A-S6's"). A tier that relies on coverage pressure needs the S5→S1 wire first.
- **Old gap prose still claims zero callers where callers now exist.** `docs/architecture/action-corpus-ideal.md:93` (`TryInstantiate` zero callers) is stale — counter-proofs `src/FusionRpg.Core/Items/Drops/LootMintAt.cs:84`, `Delve/Encounter/EliteAffix.cs:78`, `Data/Sqlite/RpgStore.cs:1974`, `Server/CreatureEndpoints.cs:239`. Read code, not the old table.

### Real gap

- **No per-action tier identity beyond the window.** Nothing names "this skill is tier 3" the way `gem.<family>.t<n>` does. `ActionRow` has `Rung` (authored, A12 table) + `RungBand` (window) + container `MinTier`/`MaxTier` — and no `t{tier}` in its own id. Whether that is even wanted is the design question below.
- **C1 family-access widening stays structure-only until priced.** `docs/architecture/action-corpus-map.md:79` (gated on per-rung `powerBudget` + family-aware non-additive price + budget check with production caller). One of three has landed (`powerBudgetMilli` in v2); family-aware price needs E9-D2; until all three hold the generator emits structure-gated tiers only.
- **Rung-window two-sided clamp unpriced.** `docs/architecture/action-corpus-ideal.md:1484` ("§4 authorizes a one-sided clamp; §5 uses a two-sided one"): forcing a first unlock to rung 5 also forces `costMulti = 3627‰`, so a floor is a tax as well as a gift. Needs A-U1 (`effectiveRung`→guard + price `minRung`).
- **Full model-call corpus not authorized.** `docs/architecture/action-corpus-map.md:16` ("Nothing here authorizes A-P1/A-P2/A-P3 — prove LLM pipeline work very well before big batch run"). A tier that needs 3,307 rows waits on the Checkpoint 4 smoke batch. Do not sequence a tier run before it and call it done.

## Prior art

Numbers and failure modes, not vibes. Unverified flags kept as unverified.

- **Path of Exile 1/2 — skill gems level 1–20, cost and requirements climb with power.** Examples from the live rosters: Disengage `Cost: (5-36) Mana`, `Attack Damage: (85-308)% of base` across levels; Volcano `Cost: (7-73) Mana` + `(6-61) per second`; Freezing Salvo `(48-150)%`, Snipe `(121-312)%` — `https://www.poe2wiki.net/wiki/List_of_skill_gems`. Gems level by XP; on level-up both the granted ability and the equip requirements (character level + attribute) and the mana cost rise; if requirements are not met the level-up icon sits greyed out and unusable — `https://pathofexile.fandom.com/wiki/Skill_gem`. Hard cap is 20 by XP (21 corrupted); gear/corruption push it higher — level 32 cited as the stacked maximum (unverified upper bound, needs a primary-source check) — same page. Support gems (added 2026-09-07 count: kinds 18, triggers 13 per `AtomKindRegistry`) are the closest analogue to our riders/conditions: they multiply rather than add (`more` vs `increased` discipline). **Failure mode to steal:** PoE avoids "one gem, one number" by splitting power (gem level), cost (mana + attribute gates), and shape (quality + support links) into three separately tuned axes. Copy the split, not the values.
- **Diablo 4 — ranks are flat wallpaper when every point is +10%.** Basic skills: "Ranking up grants 1% damage per point… no way 4 additional points would ever be worth it" — `https://us.forums.blizzard.com/en/d4/t/why-do-basic-skills-even-have-ranks/32163`; "every single skill extra points are a 10% boost, no variation, no scaling up" (player-measured, unverified beyond the thread) — same. Worse, the tree forces waste: "you need X points in Basic to access lower nodes… Putting 14 points in basic and redistributing later is annoying" — `https://us.forums.blizzard.com/en/d4/t/skill-tree-locked-to-level-and-not-to-amount-of-skill-points-unlocked/258135`. Diablo Immortal shows the other edge: max ranks differ per skill (Frenzy 6, Lacerate/Whirlwind 13, Grab/Sunder 2) so granular skills feel smooth while 2-rank skills feel dead until maxed — `https://us.forums.blizzard.com/en/diablo-immortal/t/confused-about-skills-ranks/3074`. **Failure mode to avoid:** a tier step that is only `+x%` with no new structure is wallpaper; a tier that gates the rest of the tree manufactures waste. Our rung already answers both (structure budget grows `[]→…→[reaction,restriction]`; rung keys on earn history, never on points spent elsewhere).
- **Guild Wars 2 — unlock tiers, then horizontal forever.** Weapon skills unlock at fixed levels (slot 2 at 2, 3 at 4, 4 at 6, 5 at 8), utility at 11/15/19, elite at 31 — `https://wiki.guildwars2.com/wiki/Skill_bar`. The retired skill-point costs were explicitly tiered: utility tier 1/2/3 cost 1/3/6, elite tier 1/2 cost 10/30 — `https://wiki.guildwars2.com/wiki/Skill_point`. Endgame philosophy is horizontal: "no endless gear treadmill… Exotic→Ascended is ~12% stats" (player summary, widely repeated, treat the 12% as approximate) — `https://steamcommunity.com/app/1284210/discussions/0/4356746484465429772`; "driving philosophy is horizontal progression… non-numerical bonuses via Mastery, cosmetics, Elite" — `https://wiki.guildwars2.com/wiki/Endgame`. **What to steal:** fixed unlock cadence (ours: rung windows general 1–4, family 1–7, signature 5–10 per `action-corpus-ideal.md:1261`) plus horizontal breadth (more families, more pairings) does the long-term work that a taller tier ladder would do worse.

## The shape

**Proposed: a skill tier is the item T1+T2 discipline applied to actions, with rung as rarity. Nothing else is copied.**

| Item (source) | Action (this) | Where it lives |
|---|---|---|
| Affix tier `t1–t5`, `m_t = m1 · 1.75^(t-1)`, band `[0.67m, 1.33m]` | Atom tier unchanged — actions roll atoms from the same families through the same `numerics` | `ContainerRow.MinTier/MaxTier`, `Instantiator.Draw`, `FamilyExpansion` |
| Rarity selects `(pool_rolls, min_tier, max_tier)` + `ilvl→tierCeiling` (D29 `1/1/8/18/32`, collapsing envelope) | Rung selects `(poolRolls, minTier, maxTier)` + scope rung windows (general 1–4, family 1–7, signature 5–10) | `data/tuning/action-rungs.v{n}.json`, `ActionRow.RungBand`, A-S1 planner |
| `sharePermille` authored, magnitudes derived | Same `sharePermille` through the same arithmetic; a missing share rejects rather than defaults | `spec-action-seeding.md:59`, `ContainerRow` |
| `group = (family, variant)`, at most one per group; Mixed spends one of each | Same group rule for action containers; a mixed bundle consumes one prefix and one suffix | `ContainerRow.cs:56`, `Instantiator.cs:197` |
| Tier weights `1000/600/300/120/35` inside the window | Same baked weights unless a sweep moves them; the window does half the work | `ssot-affixes.md:555` |

Concretely: seed carries `scope/scopeKey + rungBand + atomFamilies + pairingRole` (model writes identity, `audit_schema` blocks numbers); tuning carries `minRung/maxRung` rows + `powerBudgetMilli` + `structureBudget`; the existing `Draw` rolls atoms, target shape, and name. **No new `tier` column on `ContainerRow` or `ActionRow`.** A tier is the window plus the ceiling, exactly as items do it.

Alternatives rejected, with reasons:

- **New `skill.tier` column.** Rejected: it duplicates `Rung + RungBand + MinTier/MaxTier` and creates a second curve to keep in sync. The item program already proved rarity-vs-tier separation (`ContainerRow.cs:150`); actions need the same split (rung vs tier), not a third axis.
- **Copy gem-identity tiers (`gem.<family>.t<n>`).** Rejected: gems are fixed stackable containers with zero rolls; skills are rolled containers. Fixed tiers would freeze what the rung window is for.
- **Copy set-threshold tiers (`pieces_required → bonus`).** Rejected: set tiers count pieces across items; a skill tier counts nothing. Different shape, same word.
- **Copy socket `+1` enhanced tier.** Rejected: attunement is a per-circuit soft bonus (`GrantedTier = BaseTier + attuned`); a skill has no circuit and no affinity. Do not import the bonus without the board.
- **Private skill power curve.** Rejected by the one-ladder rule: `value(rung,Θ) = anchor(Θ) · q(rung)`, `anchor = sharePermille · P(Θ)/1000`, never `× contentScale` (PS-4). Cooldown rides rung only (ticks, not a magnitude); duration rides the ladder with a relative bound.

## Tunables

Every number this introduces, and which versioned file owns it. Nothing here is a `const`.

| Number | Meaning | Owner |
|---|---|---|
| `TierCount = 5`, `L_ref = 20`, `r = 1.75` (magnitudes), `r = 1.4` (durations), band `[670, 1330]‰` | The shipped item ladder, reused verbatim | `data/seed/items/_tuning/tier-bands.v{n}.json` (frozen `bands.v1.json` is registry-side, not tunable — `TierBandsFile.cs:8`) |
| `sharePermille` per channel/family | The entire tunable surface for how big tier 1 is | Same bands file + `spec-numerics.md` arithmetic |
| `minTier/maxTier` per rung (today `1-1…5-5`), `poolRolls`, `qPowerMilli` (`1.75^((r-1)/2)`), `qCostMilli` (`1.38^(r-1)`), `qCdMilli` (`1.15`), `structureBudget` (closed 7) | What a rung may hold and what it costs | `data/tuning/action-rungs.v{n}.json` (v2 adds `powerBudgetMilli` + derivation in `_meta`) |
| `powerBudgetMilli(r) = poolRolls(r) · referencePower · qPowerMilli(r) / 1000`, `referencePower = 1000 = PowerVector.One` | The rung ceiling that makes C1 enableable; rung 1 lands on exactly one unit of power by construction | Same rungs file, `_meta`-documented, `long` arithmetic, widen-before-multiply, divide-by-1000-last-once |
| `W_tier 1000/600/300/120/35`, `W_family = 1`, per-scope `minRung/maxRung` (general 1–4, family 1–7, signature 5–10) | Draw weights inside the window + who may hold which rung | Rungs file + A-S1 planner outputs + `type-weights.json` (Python, `data/seed/actions/`) |
| D29 `1/1/8/18/32` + collapsing envelope (`maxTier = min(band.Max, ceiling(ilvl))`, `minTier = min(band.Min, maxTier)`) | Which tiers are reachable at all; t1 never falls out, t2 at ilvl 1 is on purpose | `spec-affix-legality.md:146` logic, applied to rung windows for actions |
| `referencePower` scalar | Moves the whole budget ladder up/down together; can never introduce a second curve shape | Same rungs file; smoke-batch report tunes it (accepted-container cost vs budget per rung) |

Structural (keep as `const` + why-not-tunable comment): `SocketCircuitSize = 4`, `SocketCapacityMaximum = 8` (legibility, not growth); `PowerVector.One = 1000` (unit, not feel); `UnlockLadder` cap shape (`HeldCap`/`RungCap` split lives in tuning, the `min()` shape does not).

## What this deliberately does not decide

- Whether a skill shows `t3` in its id or only in its card. Identity grammar (`definitions.md` §1 wins over any spec) is an effect-atom decision, not this doc's.
- The exact per-tier family sets (which families open at which rung). That is C1's gated widening — needs E9-D2 family-aware price first.
- Duration ladder arm (`r = 1.4` has no `FamilyExpansion` arm today — `bands.v1.json:59`). Reuse the ratio; do not build the generator arm here.
- The `might m1=4 hi1=5 lo2=5` edge and `round_legible` 1/2/5 snap — documented item gaps (`bands.v1.json:78`, `formulas.py:4`), not action-tier blockers.
- Any full-corpus numbers. The 3,307-row shape is today's reading; the planner re-reads the live roster every run.
- Any emissive UI (compendium reveal, one-swap-away preview). Module 20 owns those for combinations; the skill-tier analogue waits on a surfaces pass.

## Open questions

Owner decisions only. Each is answerable; a recommendation nobody disputes is a decision.

1. **Confirm the copy: tier = atom-tier window (T1+T2), rung = rarity — no new `tier` column?** Recommendation: yes. It reuses `Draw`, `numerics`, D29, and A-G1 unchanged.
2. **Confirm rung stays 1–10 with the half-tier map (`qPower = 1.75^((r-1)/2)`, two rungs per shipped tier)?** Recommendation: yes — it is the only shape that keeps `1.75` single-sourced in `tier-bands`.
3. **Confirm `powerBudgetMilli` derivation (`poolRolls · 1000 · qPowerMilli / 1000`) ships as the neutral default, stated untuned until the smoke batch?** Recommendation: yes; the scalar moves together or not at all.
4. **Confirm scope windows stay moderate-default-tunable (general 1–4, family 1–7, signature 5–10) and A-U1 (two-sided clamp pricing + `effectiveRung`→guard) lands before any window is treated as a structure gate?** Recommendation: yes — otherwise a floor is a hidden tax.
5. **Confirm gating: Checkpoint 4 smoke batch through A-P1/A-P2/A-P3 → A-S4 → A-S3 → A-S5 proves quality before any tier-driven full run, and S5→S1 top-up + verdict semantics (`action-distribution-gaps`) land before coverage pressure is trusted?** Recommendation: yes; §17's call budget is a ceiling, not a plan.
6. **Confirm program path: new `action-skill-tiers` ideal (this doc) graduates to its own map, rather than reopening sealed `action-ideal.md` or `action-corpus-ideal.md`?** Recommendation: yes — both stay sealed; this doc is the reasoning trail.

---

## Reading gate (this session, per DESIGN-GATE §1)

Product vision: `docs/guide/the-game.md`, `docs/guide/the-loops.md` (genre RPG + empire building; loops named above). Anything at all: `docs/architecture/software-architecture.md`, `docs/architecture/decisions.md` (Product vision + Standalone-first + Eight-socket topology + Set topology rows). Actions corpus: `docs/architecture/action-ideal.md` (sealed, 26 decisions), `docs/architecture/action-map.md`, `docs/architecture/action/spec-action-seeding.md`, `docs/architecture/action-corpus-ideal.md` (Parts III–VIII current), `docs/architecture/action-corpus-map.md`, `docs/architecture/action-corpus/spec-tier-access-gate.md`, `docs/architecture/action-corpus/spec-general-propose.md`. Atom layer: `docs/architecture/effect-atom/definitions.md` (wins over specs). Power/tunables: `docs/architecture/power/ssot-power-scale.md`, `docs/architecture/tunables-ssot.md`. Item analogy: `docs/architecture/item/ssot-sockets.md`, `docs/architecture/item/spec-sockets.md`, `docs/architecture/item/spec-strain-splice-gen.md`, `docs/architecture/item/ssot-rarity.md`, `docs/architecture/item/spec-affix-legality.md`, `docs/ideas/crafting-coverage-engine.md`. Invariants: `CLAUDE.md` (RPG-layer rule, ActorHub sole compose, SOLID, guardrail-contract rule), `docs/DESIGN-GATE.md` §1–§2. Code verified at the `file:line` cites above, not from comments. Counts verified by counting; populations treated as readings.

**Boundary honesty:** `.\scripts\session-boundary-check.ps1` reports 8 drift overlaps between other active sessions (this session wrote no `tasks/sessions/*.json` record — owner runs `/session-start`). Target path `docs/architecture/action-skill-tiers-ideal.md` collides with no active `paths` claim checked this session, but the §5 boundary box cannot be ticked until a record exists.
