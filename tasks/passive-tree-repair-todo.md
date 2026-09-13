# Task list — passive tree repair

Plan: [passive-tree-repair-plan.md](passive-tree-repair-plan.md). Parent plan:
[passive-tree-plan.md](passive-tree-plan.md). Map:
[docs/architecture/passive-tree-map.md](../docs/architecture/passive-tree-map.md).
Workflow skill: [.agents/skills/seedsmith-passivetree-repair/SKILL.md](../.agents/skills/seedsmith-passivetree-repair/SKILL.md).

**Rewritten 2026-09-12** from a full distribution census, not an aggregate. Baseline:
42/42 trees `Fail`, **266/1,680 nodes bound (15.8%)**, 108 bound-but-inert, 75,697‰ budget unspent,
mechanism nodes bind at **2.1%** vs magnitude **29.5%**, tiers 8–10 are **100% mechanism and produce
nothing**, 75 of 101 chosen affixes cannot resolve, `FamilyExpandGen` refuses **94 of 125** families,
the binder **crashes**, and the corpus **drifts**.

**Owner direction (2026-09-12):** Bundle C, full — and **do not split lawn from battle**. The repo has
one battle engine for all gameplay mechanisms, so a tree contribution must reach that shared engine
and be correct on both read modes; "battle-only first pass" is not an option.

**Standing verification for every task:** the module's own tests green · `dotnet build` clean ·
`guard-single-writer`, `guard-secondary-no-unity`, `guard-funnel-delta`, `guard-dal` pass ·
`guard-power` where a magnitude or curve is touched · `python scripts/audit-overflow.py` 0 critical ·
`python scripts/audit-magic-numbers.py` 0 M1/M2 attributable to the task. For `tools/seedsmith` work,
add `python -m pytest tools/seedsmith/tests/adapters/trees`.

---

## ▶ RESUME HERE (state at 2026-09-13, owner decisions landed — plan extended, `/build` not yet resumed)

**Committed and gated:** P0.1, P0.1b, P0.2, P0.3, P1.1, P1.2, P1.3, P4.1, P4.2, P4.3a.
Commits: `d352a027` `52ad4948` `bbe47eb1` `c82d6f5a` `c3ef3d9a` `bd5b651b` `38286a0d` `4e21bc2f`
(`ab1a1b56` cleanup) `1d76131f`.

**Measured gain from Phase 4:** `op 'more'` refusals 80 → 0; the binder no longer crashes; live bind
rate 15.8% → 24.2%. **But `readable = 0.0% of bound`** — every bound atom is `stat.modify` and the
resolver reads only `stat.derived`, so the tree still contributes nothing in play. That is P4.3.

**Confirmed live this session (`docs/architecture/passive-tree-ideal.md` §17):** the derived-side
consumption path is already fully built and tested end to end — lawn (`PassiveTree/Resolve/TreeAtomSource.cs`
→ `AtomDerivedSubsystem` fan-in) **and** battle (`Battle/TreeAtomSource.cs` → `BattleStatComposer`),
task B6/D6/D7. The Injector's own Hot `ActorHub` was *also* separately fixed to hydrate tree-bound
derived atoms live (`actor-hub-and-combat-power-solid-fixing` T13, concurrent session) — the derived
route was NOT the gap; the binder emitting the wrong kind was.

**Both §4 blockers are now RESOLVED by owner decision (2026-09-13):**

1. **P2.1 (mechanism-kind anchors) — ANSWERED: publish now.** The power program publishes
   `power-scale.v3.json` (R10, 20 channels) **and** a new `status.apply` chance/duration anchor. Real
   work, not yet built — Phase 2/3 are UN-DEFERRED and actionable. See P2.1 below (defer flag removed).
2. **P4.3 (kind fork) — ANSWERED: both kinds.** Derived route proceeds exactly as originally scoped
   (P5 + P3 dependency unchanged). Primary route is corrected from the original proposal — **not** a
   battle-side `FA1`/`BattleStatModifierLedger` bolt-on; it contributes through **`ActorHub`** as a
   registered `IActorStatSubsystem` (owner's own correction: *"it is actor hub, not battle engine"*),
   matching the repo's One-ActorHub-compose hard rule. Split out to **Phase 11** below because it is
   architecturally distinct work (a new Core subsystem, not a binder/resolver change) and because of
   blocker #3.
3. **NEW real blocker — `solid-run-20260912-eb53` (worktree `actor-hub-and-combat-power-solid-fixing`).**
   Actively fusing `ActorHub`/`BattleStatComposer` and relocating subsystem registration
   (`src/FusionRpg.Core/**`, `scripts/guard-actor-hub.ps1`) — the exact surface Phase 11 needs to
   register against. Status as read this session: T1–T23 done, "program complete, owner sign-off
   pending," most recent commit enables agent-shell git merge — **close, not stuck.**
   **Named resolver:** owner merge of that worktree. **Default if this plan reaches Phase 11 before
   it merges:** skip Phase 11, ship Phases 0–10 (the derived route reaches full playability on its
   own), track Phase 11 as a standing follow-up. Phase 11 also needs its own `decisions.md` row
   (AGENTS.md: an architecture change that locks behavior) — write it only after the merge, against
   the merged subsystem contract, not before.

**L0 added — new Phase 10, not blocked, safe to build in parallel with everything above.**
`effect-pipeline` modules 11 `affix-power-class` / 12 `affix-channel-weights` — specced 2026-09-03,
still zero `src/` lines as of today. Not required for playability (P5's own vocabulary-restriction
task gets the tree readable without it), but the owner asked for it in this round: it fixes the
narrow, accidental 26-family vocabulary (Herfindahl 0.027) with a principled distribution.

---

**Standing rule:** a task that edits generated seed/generated JSON **without** a `src/` or
`tools/seedsmith` code change is not a repair. If the only change is data, it is class D/E and must
cite the code evidence that the pipeline is already correct.

**Standing rule:** a balance surface (`tier-bands.v{n}`, `power-scale.v{n}`, `bands.v1`) is
**published as a new version, never edited in place**. `bands.v1.json` is frozen; `power-scale` is at
v2; `tier-bands` is at v5.

**Standing rule:** cite `Battle*` files by symbol, never by line (other streams edit them).

**Standing rule (from the distribution):** do **not** fix the bind-rate metric by narrowing the
vocabulary first. The refusals are the honest signal. Expand the pipeline, then narrow to what
genuinely resolves.

---

## Phase 0 — reproduce and freeze the distribution baseline

### P0.1: Commit a distribution census command
**Spec:** repair skill §1, §2. **Description:** the plan §1 tables must be one command, not a
transcript, so every later phase reports a delta. It must report bind rate **by class, by tier, by
category, and by tree** — an aggregate hides the mechanism collapse.
**Acceptance:**
- [x] One command prints: trees, expected/bound/refused, by-class bind rate, by-tier mechanism %, by-tree bind rate, refusal buckets, unspent‰
- [x] The baseline is recorded with the exact commands and git revision
- [x] Binder crash, `FamilyExpandGen --check` drift, and the `wither` ungenerated node are captured verbatim
**Depends on:** none. **Scope:** S. **Done:** `d352a027` — `seedsmith trees census [--json]`.

### P0.2: Name the focused regression test each fix must ship
**Spec:** repair skill §8. **Description:** CI runs whole test projects and this repo has no
`Skip`-a-known-failure convention, so a committed red test breaks the build. **Each regression test
therefore lands WITH its fix, in the same commit** (repair skill §5 step 4: "add/update focused
regression tests that would fail on the old code"). This task fixes the *list* — what test proves what
— so every later fix task knows its acceptance test before it edits code:

| Defect | Test (must fail on the pre-fix code) |
|---|---|
| R1 vocabulary | `permitted_for_branch(b) ⊆ resolvable_family_ids` for all three tags |
| R2 `More` | a `more`-op atom resolves end to end; never reaches an `Enum.TryParse` failure |
| R3 pool channel | a pool-shaped `channel` resolves or refuses as a `BindRefusal`, never throws |
| R4 kind parity | a bound node's `KindId` is one the resolve path reads, for **both** kinds |
| R5 mechanism | a tier 8–10 mechanism node reaches `NodeAtom` and contributes |
| R7 mechanism kinds | a `status.apply` family expands with the `statusMagnitudeAndDuration` ladder |
| R8 `Replace`/`Flag` | the chosen semantics are named; no magnitude invented |
| R9 board/economy verb | `params.op` is read as a modifier only for kinds that own one |
| R10 curve | a Flat family on a curve-less channel refuses by name, not by crash |

**Acceptance:**
- [x] Each fix task below names its test, and the test is committed in the same commit as the fix
- [x] No test asserts a corpus count — the envelope/contract only
- [x] No test is committed in a failing state
**Depends on:** P0.1. **Scope:** S (the list; the tests ride with their fixes). **Done:** `d352a027` (census), `52ad4948` (re-counts).

### P0.3: Propagate the stale counts the specs still carry
**Spec:** DESIGN-GATE §3 evidence rule 6. **Description:** `spec-tree-language.md` §3 still says
16 kinds / 7 attach points / 98 affix families / 21 statuses; live it is 18 / 9 / 125 / 24. Correct
the citations in the specs this program touches, naming the counting command each time.
- [x] `spec-tree-language.md` §3 and `spec-tree-binder.md` §2 counted tables match a fresh count
- [x] Every corrected number names what was counted, not where it was quoted from
**Depends on:** none. **Scope:** S. **Done:** `52ad4948`.

---

## Phase 1 — corpus self-consistency (drift + not-yet-generated + provenance)

### P1.1: Resolve the three stale `family-expand` files
**Spec:** `spec-family-expand.md` §3.2 step 3. **Description:** `FamilyExpandGen --check` exits 1 on
`g-armour`, `g-precision`, `g-tempo`. Determine whether the source family file changed or the
generator did, fix the code if the generator is wrong, and regenerate through the real CLI. **Record
which of the two it was** — a stale generated file with a correct generator is class D; the reverse is
class A.
- [x] Root cause classified A or D with `file:line` evidence (two class D renames + one class B line-ending)
- [x] `dotnet run --project tools/FamilyExpandGen -- --check` exits **0**
- [x] The three files were regenerated by the CLI, never hand-edited
**Depends on:** P0.1. **Scope:** S. **Done:** `bbe47eb1`.

### P1.2: Classify the `wither` ungenerated plan node and add the envelope check
**Spec:** repair skill §2 (distinguish PASS/FAIL/NOT_MEASURED), §3 (class E). **Description:** measured
2026-09-12 in both directions: there are **zero** true orphans (no generated node outside a plan) and
exactly **one** ungenerated plan node — `skill.wither-def-t9-n1`, in `wither`'s plan but in no seed
document (39 of 40; zero ledger row). That is class **E**, an incomplete run, which the binder already
refuses correctly. The P0.1 census initially mislabeled it an "orphan"; this task corrects the
semantics (done in the census) and decides whether to finish the node through the real CLI now or
leave it for the Phase 8 regeneration.
**Acceptance:**
- [x] The census reports `orphanGeneratedNodeIds` (defect) and `neverGeneratedNodeIds` (class E) separately
- [x] Whole-corpus both-direction reconciliation is 0 true orphans, and a check fails if one appears
- [x] The decision on `skill.wither-def-t9-n1` is recorded: it is left for the Phase 8 regeneration (it is one node, the whole corpus is being re-rolled there anyway, and finishing it now would spend model calls on a node that regeneration replaces)
**Depends on:** P1.1. **Scope:** S. **Done:** `c82d6f5a`.

### P1.3: Establish uniform current provenance
**Spec:** `spec-tree-language.md` §5.1; PRIOR-SESSION work on `PROMPT_VERSION`. **Description:**
`PROMPT_VERSION` is `tree-language/3` but **zero** seed documents carry it: 20 `mixed`, 22 `/1`,
5 `/2`. `mixed` exists because the emit path stamps `mixed` + `promptVersionByNode`. Decide whether
the corpus must be re-generated under a uniform current version (it must, before Phase 8), and add the
guard that prevents a stale vintage from being reported as healthy.
**Acceptance:**
- [x] The intended vintage is stated, with the command that shows it (`trees census` → `tree-language/3`; 1,066 records are not current)
- [x] A check fails when a seed document's vintage is stale or `mixed` (classification + real-corpus envelope tests; `stale_vintage_trees()` is the gate input)
- [x] The regeneration scope is written down for Phase 8 (all 42 trees; the 1,066 stale records are the re-roll scope)
**Depends on:** P1.1. **Scope:** S. **Done:** `c3ef3d9a`.

---

## Phase 2 — expander: non-magnitude kinds (R7, R8)

### P2.1: Teach `FamilyExpansion` the mechanism-kind formulas — ✅ UN-DEFERRED 2026-09-13 (owner: publish anchors now)

**Owner decision (2026-09-13, /spec round):** publish the missing anchors now (Decision B, "publish
anchors now" over "rescope to magnitude-only"). This task is actionable; the anchors themselves
(`power-scale.v3.json`, the `status.apply` chance/duration anchor) are still real work — see P3.1 and
the new anchor sub-task below. **Verified 2026-09-13 during `/build full`, still accurate. This is a
data gap, not a code gap — do not implement by inventing a number.** The formulas' *shape* is locked,
but their *anchors* were authored nowhere as of this reading:

- `bands.v1.json` → `powerBand.channelFamilyGroups.statusMagnitudeAndDuration` has **no `formula` and
  no `sharePermilleOwnership`** key. It gives `twoLadderRule` (chance 1.75‰, duration 1.4‰ *mandatory*),
  a `memberFamilies` list, and a `workedExample` whose own `status` field reads
  **`"illustrative, inherited, not balanced"`**. There is no `m1` anchor for chance or duration.
- `docs/architecture/seedsmith/spec-numerics.md:210-212` states the other three groups (incl.
  `statusMagnitudeAndDuration`) **"have locked formulas and need their own shares… specced when their
  families are resolved."** The shares do not exist.
- `data/seed/items/_tuning/tier-bands.v5.json` has **no per-family chance/duration override surface**
  (keys are only `baseSharePermille`, `channelWeightPermille`, `opWeightPermille`), and
  `bands.v1.json`'s own `note` says a rider's family-specific chance ladder is *"recorded in the
  family's own tier-bands input, never this registry's default."*
- `sharePermilleOwnership` states the binding rule: *"A generator with no authored share for a channel
  must reject at import, not guess one."* The existing tool already follows it — `Program.cs:130-138`
  returns `null` for a channel with no `BattleRuleset` curve and refuses honestly.

**The owner decision (plan §6 A1/A5, confirmed again 2026-09-13):** author the missing anchors as a
new published balance surface — a `power-scale.v3.json` curve for the magnitude kinds (already
R10/P3.1) **and** a chance/duration anchor for `status.apply` (new sub-task: `P3.1b`, see Phase 3).
Until the anchor is published, the honest behavior stays the status quo: refuse by name. No longer a
§4 stop — build P3.1/P3.1b, then this task.

### P2.1 (original task text, retained for reference): Teach `FamilyExpansion` the mechanism-kind formulas
**Spec:** `bands.v1.json` `statusMagnitudeAndDuration` + `familiesOutOfFourWaySplit`;
`spec-family-expand.md`; `spec-mechanism-wiring.md`. **Ask first** (owner confirms the formula
reading). **Description:** `TryReferenceBaseM1` (`FamilyExpansion.cs:253-284`) refuses everything whose
kind is not `stat.modify`/`stat.derived`. Implement the frozen registry's own formulas: `status.apply`
chance at `r = 1.75` and duration at `r = 1.4` (mandatory); `resource.delta`/`resource.economy`/
`spawn.entity`/`board.action`/`shield.grant`/`status.clear`/`grid.*`/`box.set` by
`familiesOutOfFourWaySplit`'s stated analogy. **40 families**, and every deep tier.
**Acceptance:**
- [ ] A `status.apply` family emits rows carrying chancePermille + durationMs, laddering at 1.75/1.4
- [ ] Each named non-magnitude kind emits rows with its own params intact (no magnitude invented)
- [ ] The 40 `no supported tier-magnitude formula` refusals drop to **0**
- [ ] `FamilyExpansion` stays pure and deterministic (`--check` reproducible)
**Depends on:** P0.2. **Scope:** L. **Files:**
`src/FusionRpg.Core/Effects/Atoms/Generation/FamilyExpansion.cs`, `FamilyExpansionTypes.cs`, tests.

### P2.2: Stop conflating modifier `op` with board/economy verbs (R9)
**Spec:** `bands.v1.json` `familiesOutOfFourWaySplit`; `FamilyExpansionTypes.cs`. **Description:**
`board.action` families carry `op:"cherry"|"fireline"|"freeze"|"doom"` meaning *which action*, and
`resource.economy` carries `op:"add"` meaning a verb. Reading those as modifier ops produces the
misleading `no opWeightPermille entry` refusal. Read `params.op` as a modifier **only** for kinds that
own one; carry the verb through unchanged.
**Acceptance:**
- [ ] `atom.cherry-bloom`/`firelining`/`flash-freeze`/`dooming`/`midas`/`econ-*` expand with their verb preserved
- [ ] The 11 authored verb refusals vanish; a modifier-op check still guards the kinds that have one
- [ ] A test proves the two fields are separate
**Depends on:** P2.1. **Scope:** M.

### P2.3: Define `Replace`/`Flag` tier semantics (R8)
**Spec:** `bands.v1.json` (frozen: *"Replace/Flag carry no tier-band magnitude at all"*);
`spec-tree-catalog.md` §2.3. **Ask first.** **Description:** 19 families are refused because their op
is structural. Either (a) a structural op expands as a **magnitude-less row** that the mechanism path
carries, or (b) those families are excluded from the tree vocabulary by the vocabulary task. Option
(a) is the owner's "everything, unified" direction; (b) is cheaper. Decide, then implement at the
expander.
**Acceptance:**
- [ ] The chosen semantics are implemented in the expander and named in a test
- [ ] The 19 `Replace`/`Flag` refusals are gone (expanded or explicitly excluded with a reason)
- [ ] No magnitude is invented for a structural op
**Depends on:** P2.1. **Scope:** M.

---

## Phase 3 — expander: publish the missing channel curves (R10)

### P3.1: Request and consume a `power-scale.v3.json` covering all Flat channels
**Spec:** `ssot-power-scale.md` §10.2; `tunables-ssot.md` T4/T5. **Ask first — power program owns
this.** **Description:** `FlatReferenceBase` (`tools/FamilyExpandGen/Program.cs:135-137`) knows only
`atk`/`defense`. 20 families name real, registered channels with no curve: `combat.accuracy.*`,
`combat.dodge.*`, `combat.crit.rate/damage/resist*`, `combat.shield.*`, `arm1Max`, `arm2Max`,
`attackInterval`, `produceInterval`, `status.power`, `status.resist`. Publish **v3** (never edit v2),
add a §10.2 row per new curve, and make the generator read the latest.
**Acceptance:**
- [ ] `power-scale.v3.json` exists with a curve for every channel named above; v2 is untouched
- [ ] `ssot-power-scale.md` §10.2 lists each new row (the row is the reviewed change)
- [ ] `FlatReferenceBase` reads the latest version and returns non-null for all 20
- [ ] The 20 `no referenceBaseGameUnits` refusals drop to **0**
- [ ] `guard-power` passes
**Depends on:** P2.2. **Scope:** L (cross-program).

### P3.1b: Publish a `status.apply` chance/duration anchor — NEW 2026-09-13 (Decision B)
**Spec:** `bands.v1.json` `statusMagnitudeAndDuration` (ratios only, chance 1.75‰/duration 1.4‰, no
base); `spec-numerics.md:210-212` ("specced when their families are resolved"). **Owner: publish now.**
**Description:** author the missing base/`sharePermilleOwnership` anchor `bands.v1.json` itself says
does not exist yet, as a reviewed addition (never an in-place edit of the frozen file) — the power
program's call on the number, this program's call on wiring it into `FamilyExpansion`.
**Acceptance:**
- [ ] A published anchor exists for `status.apply` chance AND duration, with the 1.75/1.4 ratios intact
- [ ] `bands.v1.json` is unedited; the anchor lives in its own reviewed addition/new file
- [ ] `FamilyExpansion` reads it and the 40 P2.1 refusals for this group drop to 0
**Depends on:** P3.1. **Scope:** M (cross-program — power program owns the number).

### P3.2: Publish the missing `opWeightPermille` rows (if any remain)
**Spec:** `tier-bands` `_tuning`. **Description:** after P2.2/P2.3, re-measure the op refusals. Any
real modifier op still missing a weight gets a row in a **new** `tier-bands.v6.json`, with the value
justified by the existing ladder rather than invented. If none remain, close this task with the zero.
**Acceptance:**
- [ ] The residual op refusals are re-measured and reported
- [ ] Any new weight lives in `tier-bands.v6.json` with its reasoning; v1–v5 untouched
- [ ] `no opWeightPermille entry` refusals drop to **0**
**Depends on:** P3.1. **Scope:** S–M.

### P3.3: Add the four missing channel pools (D)
**Spec:** `spec-channel-pool.md` §6.1 (the 12-pool list and the add-one rule). **Description:**
`combat.power.pierce.{variant}` and `combat.power.overflow.{variant}` are named by 4 families with no
pool, and the spec's own §6.1 states the add rule. Note the spec also records these two stems as **not
registered channel families** — verify which is true before adding a pool.
**Acceptance:**
- [ ] Whether the stem is registered is verified against `DerivedStatChannels`, not assumed
- [ ] If registered: a pool is added by the §6.1 id rule; if not: the refusal is a `BadParamValue` naming the module that owes registration
- [ ] The 4 `no matching E30 channel pool` refusals are resolved with a named reason
**Depends on:** P3.1. **Scope:** S.

---

## Phase 4 — binder robustness and kind parity (R2, R3, R4)

### P4.1: Make `AffixComposer` handle pool-shaped channels without crashing (R3)
**Spec:** `spec-tree-binder.md` §7.1 (refuse, never repair); `spec-channel-pool.md` §3.2. **Description:**
`AffixComposer.ParseAtom` (`AffixComposer.cs:69`) calls `GetString()` on a pool object and throws an
unhandled exception, killing the run. A pool must resolve to a concrete channel deterministically
(§3.2's roll rules) or refuse as a named `BindRefusal`.
**Acceptance:**
- [x] `tools/TreeBinder --check` completes on all 42 trees without an unhandled exception
- [x] A pool-shaped channel is resolved with the §3.2 rule named, or refused as a `BindRefusal`
- [x] A test proves both paths; `--check` no longer crashes
**Depends on:** P0.2. **Scope:** M. **Done:** gate PASS 2026-09-13. Chosen disposition: **refuse by
name**, not resolve — `spec-channel-pool.md` §4 makes the roll effect-pipeline module 2's, and §3.4
prices a pool as `count × weighted_mean(member)`, so a bake-time pick would store a number the roll can
contradict. Verified: `--check` exits 1 (stale corpus, separate task) with **0 unhandled exceptions**
and 289 pool channels named; AffixComposerTests 10/10; PassiveTree 405/405; 4 guards green. Gate noted
the malformed-channel assertion was only weakly discriminating (word "channel") and the boxes were
unticked at gate time — both fixed in this commit.

### P4.2: Add `More` to the tree op vocabulary (R2)
**Spec:** `spec-tree-catalog.md` §2.3; `AtomKindRegistry.cs:517`. **Owner chose: add the member**
(Bundle C). **Description:** `stat.modify` legally supports `More` and it is priced (550‰); `NodeAtomOp`
lacked it, so every `more`-op node was refused at `TreeBinderRun.ParseOp`. Add `More`, and move the
derived-side M3 rule from a *structural* property (the member's absence) to a **named, kind-aware
refusal at both the load path and the bind path** — otherwise adding the member would silently convert
a loud refusal into the silent drop `TreeAtomSource.BoundAtomsFor` performs when
`AtomDerivedSubsystem.TryParseOp` fails.
**Acceptance:**
- [x] A `more`-op atom parses to a real op end to end and is priced (`A_more_op_stat_modify_atom_parses_binds_and_is_priced` asserts `NodeAtomOp.More` and `kMicro > 0`)
- [x] The `op 'more'` refusals drop to **0** (measured 80 → 0 on the live binder; live bind rate 15.8% → 24.2%)
- [x] A source-shape test proves both read modes map every `NodeAtomOp` including `More` (`Both_read_modes_map_every_NodeAtomOp_including_More`)
- [x] `NodeAtom`'s doc comment states which kinds own which ops
- [x] M3 stays LOUD at both sites: `Derived_atom_with_a_more_op_is_refused_by_name_at_load` (loader) and the bind arm in `ChannelLegalityTests`; both proven to FAIL when the arms are mutated out
**Depends on:** P4.1. **Scope:** M. **Done:** gate PASS 2026-09-13 (round 2; round 1 was FAIL for missing
loader/pricing/source-shape tests and a stale doc comment, all fixed). Verified: focused 101/101,
full Core.Tests green (`13369/13369` as read at the gate — a reading, and it moves as other streams add
tests), `op 'more'` 80 → 0 with 0 unhandled exceptions, 6 guards green.

### P4.3a: Census counts READABLE atoms, not priced ones — DONE `4e21bc2f`

**Found while investigating P4.3, and it is the program's own instrument lying.** The P0.1 census's
"inert" metric counted atoms the binder *priced*, but the resolver reads only `stat.derived` — so 243
bound nodes reported healthy while contributing nothing. By the repair skill's own §0.1 definition
("bound nodes carrying zero READABLE atoms"), the real number is **266/266 unreadable, readable 0.0%
of bound**. Fixed: `bound_with_readable_atoms`, `bound_atoms_by_kind`, `unreadable_share_permille`,
a named `DEFECT` line when no readable kind binds, and a test asserting `READABLE_KIND_ID` against the
resolver source so the census cannot drift from the thing it measures. 37 census tests, 545 tree tests.

### P4.3: Make binder and resolver agree on kind — DERIVED ROUTE ONLY (R4) — ✅ UN-DEFERRED 2026-09-13

**Owner decision, 2026-09-13 (/spec round): both kinds, confirmed.** Split in two, because the two
routes are architecturally different work with a different blocker:

- **Derived half — THIS task.** The language stage draws from the quota cell's *derived*
  `channelFamily` rather than a branch tag (R1/P5) **and** the 8 `stat.derived` families' pool
  channels resolve (P3.3 — P4.1 chose `refuse` for pools at bind time, so they cannot bind until
  their pool is registered). No architecture change; proceeds once P3/P5 land, same as originally
  scoped. **This alone reaches full playability** — the lawn+battle consumption path for `stat.derived`
  is already built and tested (B6/D6/D7), confirmed again this session (`passive-tree-ideal.md` §17).
- **Primary half — moved to Phase 11**, `P11.1`. Corrected from the original proposal (a
  `FA1`/`BattleStatModifierLedger` bolt-on): the owner named the mechanism explicitly —
  *"it is actor hub, not battle engine"* — so the primary contribution registers as an
  `IActorStatSubsystem` on `ActorHub`, the repo's single compose gate for both lawn and battle
  (`decisions.md` "ActorHub sole Hot compose gate"). Blocked on the `solid-run-20260912-eb53` merge
  (see ▶ RESUME HERE) and needs its own `decisions.md` row, written after that merge.

No longer a §4 stop for this half. Unblocking order unchanged: P2.1/P3.1/P3.1b's anchor → P3 → P5 →
this task.

**Measured detail (2026-09-13, live bind):**

```
fresh bind (tools/TreeBinder --out):  bound=407  onlyModify=243  onlyDerived=0  empty=164
bound atoms by kind/channel:          stat.modify/atk: 261   stat.modify/defense: 76
                                      stat.derived: 0
resolve path (Resolve/TreeAtomSource.BoundAtomsFor):  `if (atom.KindId != "stat.derived") continue;`
```

100% of bound tree atoms are dropped by the resolver. The committed census reads 266 bound (lower
than the live 407 because the committed corpus is older than the current generator), of which
`readable=0.0%` — the number P4.3a's fix made visible.

### P4.3 (current scope, derived route only — primary route is P11.1)
**Spec:** `spec-tree-binder.md` §4.1; `spec-tree-resolve.md` §2.1, §12 test 15. **Owner answered:
both kinds — this task builds the derived half; P11.1 builds the primary half.** **Description:** the
binder must emit `stat.derived` for every node whose plan-assigned `channelFamily` is a derived
channel (R1/P5's job to align the picker; this task's job to align the emission and remove the silent
skip `Resolve/TreeAtomSource.BoundAtomsFor` performs today for `stat.modify`).
**Acceptance:**
- [ ] Every `NodeAtom.KindId` the binder emits for a derived-family node is `stat.derived`, and is read
      by `Resolve/TreeAtomSource.BoundAtomsFor`
- [ ] A `TreeFanInTests`-shape test registers the real subsystems and reads the moved channel back in **lawn and battle**
- [ ] Lawn and battle totals agree byte-identically (spec-tree-resolve §12 test 15)
- [ ] No `stat.modify` node is silently dropped either: it is bound and stored (for P11.1 to read),
      never treated as an error by this task's own path
**Depends on:** P4.2. **Scope:** L. **Files:**
`Binding/TreeBinderRun.cs`, `Resolve/TreeAtomSource.cs`, tests.

---

## Phase 5 — vocabulary alignment, now safe (R1)

### P5.1: Restrict the permitted affix enum to binder-resolvable families
**Spec:** `spec-tree-language.md` §4.2 step 6, §5.1; `vocab.py:17-25`'s own named blocker.
**Description:** after Phases 2–3 the resolvable set is far larger. `permitted_for_branch` must
return only ids that resolve to a generated `AtomRow`. The enum is the schema's `enum`, so an
unresolvable id becomes **unsampleable**, not rejected.
**Acceptance:**
- [ ] `permitted_for_branch(b)` returns only ids with a generated atom, for all three tags
- [ ] A test asserts `set(permitted_for_branch(b)) ⊆ resolvable_family_ids` for every tag
- [ ] An empty result is still **held** (`UnsatisfiableCell`), never widened to the full list
- [ ] The `does not exist` refusals drop to **0** by expansion, and the gate records the before/after bound-set size
**Depends on:** P3.3. **Scope:** M.

### P5.2: Reconcile the residue and record the boundary
**Spec:** repair skill §3. **Description:** any family still unresolvable after Phases 2–4 is a real
boundary. Classify each (no pool / no curve / structural op / not-registered channel / intentional)
and make the classification data the vocabulary reads, not prose.
**Acceptance:**
- [ ] Every remaining unresolvable family id is classified with a reason
- [ ] The classification is machine-readable and drives P5.1's enum
- [ ] The count is a **reading** recorded with the command, not a constant in a test
**Depends on:** P5.1. **Scope:** M.

### P5.3: Re-measure the affix-pick concentration
**Spec:** plan §1.5. **Description:** the language stage concentrated picks on 101 ids (Herfindahl
0.027, effective ≈37) because the resolvable set was 26. After expansion, re-measure. If the new
vocabulary is still concentrated, the quota/brief is the next defect — not this task's to fix, but its
to surface.
**Acceptance:**
- [ ] Herfindahl and effective-distinct are re-measured over the new vocabulary
- [ ] A concentration GAP is filed or closed with a named owner
**Depends on:** P5.1. **Scope:** S.

---

## Phase 6 — mechanism carriage (R5)

### P6.1: Carry mechanism-class atoms through the binder
**Spec:** `passive-tree-ideal.md` §3.5; `spec-tree-resolve.md` §2.3; `spec-mechanism-wiring.md`.
**Description:** mechanism nodes bind at **2.1%** and tiers 8–10 are 100% mechanism. Even after
Phases 2–3 make their atoms exist, `TreeBinderRun.cs:49-61` skips any atom lacking a ladder channel.
Carry the atom with its real `kindId` and no magnitude, and make the resolve path not drop it.
**Acceptance:**
- [ ] A mechanism-class node's atom reaches `NodeAtom` with a real `kindId`
- [ ] A **tier 8–10** mechanism node is proven live in the resolver (owned, gate-open, contributing)
- [ ] The bound-but-inert count is **0**; by-class bind rate for mechanism is no longer the worst
- [ ] `PassiveTree/MechanismRamp` still matches `archetypes[].mechNodes[t]` both directions
**Depends on:** P4.3. **Scope:** L.

---

## Phase 7 — resolve proof, both modes, one engine (acceptance)

### P7.1: End-to-end proof — one node changes a number in lawn and battle
**Spec:** `spec-tree-resolve.md` §12 test 15, §14 success criteria 1–4a. **Description:** the central
acceptance: one owned, gate-open, enabled node — magnitude **and** mechanism — moves a real channel
through the shipped fan-in on both read modes with identical totals, no new subsystem, no new order
band, and through the one shared engine.
**Acceptance:**
- [ ] A magnitude node and a mechanism node each proven live in lawn and battle
- [ ] `F ∈ [1, Fmax]` both bounds; `Fmax = 1000‰` removes `F` byte-identically
- [ ] Withdrawal (un-owning) returns the channel to zero
- [ ] Every contribution carries `tree.{treeId}.{nodeId}` (GG-49)
- [ ] No second combat path: verified against `guard-actor-hub` and the ActorHub rule
**Depends on:** P6.1. **Scope:** L.

---

## Phase 8 — regenerate the corpus and re-audit

### P8.1: Regenerate all 42 trees through the real CLI
**Spec:** repair skill §6. **Description:** after Phases 1–7 and 12, regenerate
`data/generated/passive-tree`, re-run the language stage (P5.1 changed its vocabulary, P1.3 its
vintage, P12.2 what a pool-shaped channel does), and record scope + why.
**Regeneration is proof, not repair.**
**Acceptance:**
- [ ] `tools/TreeBinder --check` exits **0** on the regenerated corpus (byte-reproducible)
- [ ] `FamilyExpandGen --check` still exits **0**
- [ ] Every tree's verdict is `Pass`, or `Fail` with a named evidenced reason
- [ ] `python -m seedsmith check --family PassiveTree` runs with real wired data (not `NOT_MEASURED`)
- [ ] The hard gate(s) are genuinely measured and green
**Depends on:** P7.1, P12.3. **Scope:** L (machine time, resumable).

### P8.2: Re-run the distribution census and prove the gates
**Spec:** plan §4 gates G1–G10. **Description:** re-run P0.1's command and report the delta for every
§1 table — bind rate by class, by tier, by category, by tree; refusal buckets; unspent‰.
**Acceptance:**
- [ ] Mechanism bind rate is no longer the worst class; tiers 8–10 contribute
- [ ] No tree binds 0%; `agility`/`spark`/`leech` specifically re-measured
- [ ] Unspent‰ is materially reduced, with the remainder explained
- [ ] Before/after table committed
**Depends on:** P8.1. **Scope:** M.

### P8.3: Live-boot proof against the regenerated corpus
**Spec:** parent plan H9. **Description:** boot a real server, confirm the import with zero refusals,
and confirm a node's value is readable — on both read modes per the owner's direction.
**Acceptance:**
- [ ] A live boot imports the corpus with zero refusals
- [ ] A real player with an allocation shows a non-zero tree contribution in **lawn and battle**
**Depends on:** P8.2. **Scope:** M.

---

## Phase 9 — balance measurement (the actual goal)

### P9.1: Re-run `squad-harness` S4 on the real direct-channel model
**Spec:** `spec-squad-harness.md` §4/§11 S4; parent plan F7/F8. **Description:** only now, with real
magnitude **and** mechanism contributions bound, can the sweep answer a balance question.
**Acceptance:**
- [ ] `F ∈ [1, Fmax]` at squad scope; no tree is OP
- [ ] D42's dials are republished with the measurement behind them, or explicitly still flagged
- [ ] The report names which of the ideal's §3.5 mechanism classes actually rescue a focus build
**Depends on:** P8.3. **Scope:** M–L.

### P9.2: Close or file the two content GAP findings
**Spec:** parent plan (2026-09-07 pass). **Description:** `ExclusionRate` (999‰ vs ≤30‰) and
`NearDuplicate` (69‰ vs ≤5‰). `ExclusionRate`'s root cause was fixed in the brief; `NearDuplicate` has
no live suppression. Either land it or file with an owner and a default.
**Acceptance:**
- [ ] Each finding closed with a measurement or filed with an owner and a default
- [ ] Neither silently dropped
**Depends on:** P8.1. **Scope:** M.

---

## Phase 10 — NEW 2026-09-13: `effect-pipeline` L0 (affix distribution quality, not blocking)

Not required for playability — P5 gets the tree readable without it. Owner asked for it this round
because the resolvable vocabulary is narrow and accidental (26 families, Herfindahl 0.027, effective
≈37 out of 101 chosen ids). **Safe to build in parallel with Phases 2–9**; only `P10.3` feeds back
into Phase 5's vocabulary.

### P10.1: `affix-power-class` (effect-pipeline module 11) — LLM classification stage
**Spec:** `docs/architecture/effect-pipeline/spec-affix-power-class.md` (to be written as this task's
first step — specced only in `effect-pipeline-map.md` today, no module doc exists yet). **Description:**
classify every affix in the shipped corpus (`data/seed/items/affix-families/*.json`) into one
closed-enum power class, carrying `basis`. Never a number, never a rate. Content-addressed and
recorded so a re-run with the same ledger row does not re-call the model.
**Acceptance:**
- [ ] Every authored affix has a class + `basis`; the enum is closed; `check` fails on a missing class
- [ ] Re-running against an unchanged ledger row makes zero model calls
**Depends on:** none. **Scope:** L. **Files:** new `tools/seedsmith` classification module + spec.

### P10.2: `affix-channel-weights` (effect-pipeline module 12) — deterministic weight policy
**Spec:** `docs/architecture/effect-pipeline/spec-affix-channel-weights.md` (new, this task).
**Description:** a deterministic `(powerClass × channel) → weight` table in `data/tuning/`;
`poolFor(container, channel, rarity)` composes the candidate list L1 draws from. Runs **before** L1,
consumes no RNG. `data/seed/channel-policy/defaults.json` grows from its current 2-entry stub to the
six named channels (`drop`/`boss`/`set`/`socket`/`unique`/`craft`).
**Acceptance:**
- [ ] `channelWeights[powerClass][channel]` exists for all six named channels
- [ ] `drop`-channel weight is never exactly zero (0.01% floor); any structural zero carries a comment
- [ ] Adding L0 shifts no historical roll (proof: replays a fixed seed before/after, diff is empty)
**Depends on:** P10.1. **Scope:** M.

### P10.3: Tree-language consumes the L0 candidate list (R-2.6)
**Spec:** `spec-tree-language.md` §4.2. **Description:** `permitted_for_branch` currently offers every
branch-tagged family regardless of resolvability or fit; make it draw from L0's composed candidate
list for the node's own `channelFamily`, folded with P5.1's resolvable-set restriction.
**Acceptance:**
- [ ] The picker reads L0's candidate list, not the raw branch tag
- [ ] Concentration (Herfindahl) is re-measured and reported against the P5.3 baseline
**Depends on:** P10.2, P5.1. **Scope:** M.

---

## Phase 11 — NEW 2026-09-13: ActorHub primary producer (`stat.modify` route) — ⛔ PRE-WORK GATE

**Gate, not a checkpoint — genuinely irreversible.** `solid-run-20260912-eb53`
(`actor-hub-and-combat-power-solid-fixing`) is fusing `ActorHub`/`BattleStatComposer` and relocating
subsystem registration on `src/FusionRpg.Core/**` right now. Registering a new `IActorStatSubsystem`
against that surface mid-fusion is a collision with no clean repair after the fact — this passes both
"Gates vs. checkpoints" tests (irreversible; no reversible default exists for *how* to register against
a surface that is being restructured). **Named resolver:** owner, merging that worktree. **Stated
default if this plan reaches Phase 11 unresolved:** skip it — Phases 0–10 already deliver full
playability via the derived route — and carry Phase 11 as a tracked, non-blocking follow-up.

### P11.0: Write the `decisions.md` row (AGENTS.md: architecture that locks behavior needs one first)
**Depends on:** `solid-run-20260912-eb53` merged. **Description:** write the row against the *merged*
subsystem contract, not the pre-merge one — writing it earlier risks locking against an interface the
fusion is about to change. Names: the new subsystem, its `ContributionSourceIds` grammar
(`tree.{treeId}.{nodeId}`), and that it is additive to the derived route, not a replacement.
**Scope:** S.

### P11.1: Register a new `IActorStatSubsystem` carrying the tree's `stat.modify` atoms
**Spec:** `actor-hub-ssot.md` §8.1 (SourceId grammar); `decisions.md` "ActorHub sole Hot compose gate".
**Description:** a new subsystem reads bound `stat.modify` atoms (already stored by P4.3's binder,
never dropped) and contributes them into `ActorHub.Resolve`/`ResolveDerived`, GG-49 SourceId
`tree.{treeId}.{nodeId}`. **No BattleStatComposer bolt-on, no private fold** — once solid-run's fusion
lands, both lawn and battle read Hub output through the same path this subsystem feeds.
**Acceptance:**
- [ ] A real `IActorStatSubsystem` is registered, contributing `stat.modify`-kind tree atoms
- [ ] `guard-actor-hub.ps1` passes — no second composer, no `BattleStatComposer`-only path
- [ ] A `TreeFanInTests`-shape test proves the contribution reaches Hub output in **both** lawn and
      battle reads, byte-identical
**Depends on:** P11.0, P4.3. **Scope:** M–L. **Files:** new `src/FusionRpg.Core/Stats/Derived/Subsystems/*.cs`, `ActorHub.cs` registration, tests.

### P11.2: Live-boot proof — a real allocated primary-kind node changes a number
**Spec:** `spec-tree-resolve.md` §14 acceptance 1–4a. **Description:** same shape as P7.1/P8.3, scoped
to a `stat.modify` node specifically, proving the Decision-A "both kinds" promise end to end.
**Acceptance:**
- [ ] One owned, gate-open `stat.modify` node changes a real actor number on the lawn
- [ ] The same node's contribution is visible in a battle read, through Hub, not a bolt-on
**Depends on:** P11.1. **Scope:** M.

---

## Phase 12 — NEW 2026-09-13 (plan-coverage audit): the real join, not the patch

**Why this exists.** P4.3 (Phase 4) is a minimal hand-patch to the tree's own bespoke
`Binding/AffixComposer.cs` — real work, and the fastest path to a readable tree, but it keeps a
second, parallel resolver alive beside the shared `Effects/Atoms/Resolver` (module 2). Per this
repo's SOLID-is-binding hard rule, extending a bespoke seam is allowed only if the remediation is
**named and sequenced**, not left implicit — this phase is that naming. It also closes R-1.1 and
R-3.1–3.3 from the repair report, both dropped when Phase 10/11 were added and caught by this audit.
**Bonus:** it supersedes P4.1's "refuse by name" disposition for pool-shaped channels —
`spec-channel-pool.md` §4 already says the roll is module 2's job, so once the tree delegates to it,
pool nodes stop being a permanent refusal.

### P12.1: Disposition the tree as `effect-pipeline`'s fifth path
**Spec:** `effect-pipeline-map.md` §1 (currently dispositions four paths — atom layer, `mods_json`,
patron plugin, aura catalog — with the invariant *"an actor never receives the same source through two
paths"*). **Description:** add the passive tree as path 5, stating its ownership and relationship to
the other four, before the code below makes it true.
**Acceptance:**
- [ ] `effect-pipeline-map.md` §1 names the tree as a dispositioned path
- [ ] The "never twice" invariant is restated for it explicitly
**Depends on:** none. **Scope:** S. **Files:** `docs/architecture/effect-pipeline-map.md`.

### P12.2: Delegate tree-atom resolution to the shared `Resolver` (R-1.1)
**Spec:** `Effects/Atoms/Resolver.cs` (module 2, five-step order, per-layer RNG streams);
`spec-tree-binder.md` §4.1. **Description:** `Binding/AffixComposer.Resolve` is a catalog-local copy
of what `Resolver` already does. Replace or delegate it so there is **one** resolution implementation
— per D24, this stays a **bake-time, single-resolve** call (never a per-player roll): the tree is
static content, and `Resolver`'s determinism is what makes baking its output legal at all.
**Acceptance:**
- [ ] `AffixComposer.Resolve` is deleted or delegates to `Resolver`; no parallel resolution logic remains
- [ ] A test asserts emitted `channelId` ∈ the node's own permitted `channelFamily` (R-1.2's mismatch
      case is a named refusal, never a silent divergence)
- [ ] Pool-shaped channels resolve through `ChannelPool` (E30) at this same bake-time call, superseding
      P4.1's "refuse by name" interim disposition — or still refuse, but naming *why Resolver itself*
      refused, not the tree's own missing capability
- [ ] All 42 trees still `--check` clean (no unhandled exception regression from P4.1)
**Depends on:** P12.1, P4.3. **Scope:** L. **Files:** `Binding/AffixComposer.cs`, `Binding/TreeBinderRun.cs`, tests.

### P12.3: Bind the tree as a `skill`-container producer via `InstanceProducer` (R-3.2/3.3)
**Spec:** `spec-container-schema.md`; `definitions.md:41` (`^skill\.[a-z0-9-]+$`); `ContainerRow.cs:29`.
**Description:** a node becomes an affix inside a `skill` container, materialized by `InstanceProducer`
(module 4 — already has production callers elsewhere, per `passive-tree-ideal.md` §13.2's 2026-09-13
correction) into a real `InstanceRow` + `BindingRow`, instead of the tree's own `NodeAtom` record.
**Acceptance:**
- [ ] Container ids follow `definitions.md:41`'s pattern; a load-time validator refuses a malformed one
- [ ] A node yields a real `InstanceRow` + `BindingRow` through `Resolver`'s five-step order
- [ ] `--check --family PassiveTree` reads real wired data, not `NOT_MEASURED`
**Depends on:** P12.2. **Scope:** L. **Files:** `Binding/TreeBinderRun.cs`, `Catalog/NodeAtom.cs` (or its replacement), tests.

**Note for Phase 8:** once Phase 12 lands, P8.1's regeneration picks up any pool-shaped nodes that were
previously refused. Re-run P8.2's census after Phase 12, not only after Phase 6.

---

## Ask-first items (owner decisions)

| # | Decision | Owner direction so far | Blocking task |
|---|---|---|---|
| A1 | Publish `power-scale.v3.json` for 20 channel families | ✅ **ANSWERED 2026-09-13: publish now** | P3.1 |
| A2 | `Replace`/`Flag` semantics: bind mechanism-less, or exclude? | owner leans "everything, unified" | P2.3 |
| A3 | Add `More` to `NodeAtomOp`? | ✅ DONE — added, `38286a0d` | P4.2 (closed) |
| A4 | Mechanism-carriage shape (`kMicro = 0` + real `kindId`, or new field)? | `kMicro = 0` + real `kindId` (default) | P6.1 |
| A5 | Confirm the `statusMagnitudeAndDuration` reading for all non-magnitude kinds | ✅ **ANSWERED 2026-09-13: publish the anchor, registry stays frozen** | P2.1, P3.1b |
| A6 | `tier-bands`/`power-scale` version publication is the power program's call | request + consume, never edit | P3.1, P3.2 |
| A7 | Kind fork mechanism (not just direction) | ✅ **ANSWERED 2026-09-13: both kinds; primary route via ActorHub `IActorStatSubsystem`, never a battle-side bolt-on** | P4.3, P11.1 |
| A8 | Include effect-pipeline L0 (modules 11/12) in this program's scope? | ✅ **ANSWERED 2026-09-13: yes, spec and build here** | P10.1–P10.3 |
| A9 | `decisions.md` row for the ActorHub primary producer | ⛔ **OPEN — write after `solid-run-20260912-eb53` merges**, not before | P11.0 |

## Gate summary (plan §4)

| Gate | Phase | One-line pass |
|---|---|---|
| G0 | P0 | baseline distribution reproducible from one command |
| G1 | P1 | `FamilyExpandGen --check` 0; zero true orphans; uniform current vintage |
| G2 | P2 | 40 mechanism/magnitude refusals → 0; status ladders real |
| G3 | P3 | 20 curve refusals → 0; `power-scale.v3` published; pools resolved |
| G4 | P4 | binder runs to completion, never crashes |
| G5 | P4 | emitted kind is read by the resolve path for that kind |
| G6 | P5 | enum ⊆ resolvable; `does not exist` refusals → 0 by expansion |
| G7 | P6 | mechanism binds; tiers 8–10 contribute; inert count 0 |
| G8 | P7 | one node moves lawn **and** battle, identical totals |
| G9 | P8 | corpus byte-reproducible; `check --family PassiveTree` real |
| G10 | P8 | mechanism class no longer worst; no tree binds 0% |
| G11 | P9 | balance measured; no tree OP; GAP findings closed or filed |
| G12 | P10 | L0 classifies + weights the corpus; tree-language consumes it; historical rolls unchanged |
| G13 | P11 | primary-kind node reaches a real actor number via `ActorHub`, lawn **and** battle, `guard-actor-hub` green |
| G14 | P12 | tree dispositioned as path 5; `AffixComposer`'s parallel resolve deleted/delegates to `Resolver`; pool channels resolve, not permanently refused |
