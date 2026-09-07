# Spec: `soul-curve-resolution`

**Status: BUILT + VERIFIED 2026-09-07** — `tasks/passive-tree-todo.md` task J12. `NodeAtom.SoulCurveId`
removed; all six real production call sites and ten test files' fixture data updated; full passive-tree
suite green (399/399). See J12 for the complete evidence trail, including a scope correction found
mid-build (18 more test call sites the original citation sweep missed, all positional not named args).

Module of [passive-tree](../passive-tree-map.md), closing [`tree-catalog`](spec-tree-catalog.md) §OQ1
and [`tree-binder`](spec-tree-binder.md) §5's own dangling field. Amends `NodeAtom`
(`src/FusionRpg.Core/PassiveTree/Catalog/NodeAtom.cs`), `TreeBinderRun.cs`, `RpgStore.TreeCatalog.cs`,
and `PassiveTreeCatalogLoader.cs` — all `tree-catalog`/`tree-binder`'s own files, no cross-program
change.

**D58 (owner, 2026-09-06), as originally framed:** *"Where does soul level enter `CurveInput`? Add a
4th `CurveInput` member for soul level explicitly."* **This spec's own investigation — the "careful
investigation of every `CurveInput` consumer" D58 was flagged as needing before its shape could be
touched — reaches a different, better-evidenced answer than the menu it was chosen from offered: soul
level does not belong in `CurveInput` at all, in any form, because it already has a different, shipped,
tested mechanism that the repo's own hard rules require it to use instead.** §0 below shows the work;
this is not a unilateral override of the owner's choice, it is the investigation that choice was
waiting on, reported honestly rather than forced to fit the frame it was asked inside.

> **Reads [`passive-tree-ideal.md`](../passive-tree-ideal.md)** §4 (D3's own elaboration) and
> [`spec-tree-binder.md`](spec-tree-binder.md) §5 — both cited below as the deciding evidence.

## 0. ⛔ Why "add a 4th `CurveInput` member" is the wrong answer, shown not asserted

**The literal question has an existing, different, shipped answer.** `spec-tree-binder.md` §5.1 —
built, tested, and cited in its own Decisions table as D3's implementation — reads:

```text
Θ_node    = Θ_actor + (soulTrack.thetaPerSoulLevelMilli · soulLevel(node)) / 1000
magnitude = kMicro · P(Θ_node) / 1_000_000
```

*Souls do not multiply a coefficient through a lookup curve. They shift `Θ_node`, and the shared
`PowerLadder.P(Θ)` does the rest* — the exact shape `passive-tree-ideal.md` §4 requires by name: *"The
bonus [souls buy] is not exempt and must read `P(Θ)`."* Not a suggestion — the ideal's own words,
about the very mechanism D58 is asking where to hook. `TreeBinderRun.cs`'s real code implements this
today, and `Soul_level_offsets_theta_never_the_coefficient` (`SoulTrackTests.cs:56-82`, real, passing,
independently re-run this session) is the test pinning it — `spec-tree-binder.md` §Testing's own row
names it in `snake_case`, matching the file's own convention for describing test names in prose.
⚠ **`spec-tree-binder.md`'s own status line still reads "spec... No build authorized."** Code beats
docs (DESIGN-GATE.md evidence rule 2): the formula and its test are real and passing regardless of what
that header says, but the header itself is a separate, pre-existing staleness this spec does not fix —
named here so it isn't mistaken for evidence against the formula being real.

**The field the "4th member" question was actually chasing is dead code, not a wiring gap.**
`NodeAtom.SoulCurveId` (`NodeAtom.cs:50`) exists, is validated on import
(`PassiveTreeCatalogLoader.cs:44-45,316-326` — a regex requiring `curve.<id>` shape, and this
validation path is genuinely live: it runs and would accept a real value if one were ever authored —
the gap is that none ever is, confirmed by grepping `data/` for `soulCurveId`, zero hits) and
round-trips through SQLite (`RpgStore.TreeCatalog.cs:319`, both directions — this is itself a real
production **write**, of the constant `null` `TreeBinderRun.cs` always supplies, not merely a test
artifact). **Both `TreeBinderRun.cs:72` and its sibling `TreeBinderExplain.cs:102-103` write
`SoulCurveId: null` unconditionally** — no code path in either file ever supplies a real value — and a
repo-wide grep for `.SoulCurveId` finds **three** hits total: the `RpgStore.TreeCatalog.cs:319` write
just named, and two round-trip assertions in `CatalogHardeningTests.cs` (`"curve.might.resist"` in,
same string out; `null` in, `null` out). **Nothing anywhere — not `RpgStore.TreeCatalog.cs`'s own read
side, not `tree-resolve`, not `tree-state` — ever asks a node for its `SoulCurveId` to compute a
magnitude.** It is schema with zero behavior — the named "SC7" pattern ([`effect-atom/spec-atom-kind-registry.md:19`](../effect-atom/spec-atom-kind-registry.md),
restated at `atom-catalog-ssot.md:293`): *"A row that no code consumes is not content."* This exact
repo already has a live instance of the same shape in a different program —
`decision-d3-cost-rarity-rebase.md` §Q2.1 finds `RarityRow.PoolRolls` has zero production readers and
names it *"the `status.expose.*` shape SC7 names"* — the identical defect, independently confirmed
twice, in two different programs, both times by grepping for real readers rather than trusting a
field's presence.

**The "curve, never a formula" misreading is not a one-off comment — it is this program's own older,
recorded belief, reconciled here rather than left for a later reader to trip over.**
[`docs/research/passive-tree/21-plan-coverage-data.md`](../../research/passive-tree/21-plan-coverage-data.md)
§C7 and its task A13 both describe `soulCurveId` the same way `PassiveTreeCatalogLoader.cs:318`'s
comment does — *"a curve reference, never a formula (D3)"*. That research document is a dated planning
snapshot, correctly left unedited as historical record, but A13's own acceptance criteria (*"`kMicro`
is byte-identical at soul level 0 and 50; only `Θ_node` moves"*) describe exactly the `Θ`-offset
formula that is now real and tested — the SAME task's description and acceptance bar already disagreed
with each other before this spec existed. This is not a competing, still-live plan for a curve-based
soul track; it is the same historical confusion this spec resolves, recorded in one more place.

**The field's own justifying comment cites the wrong D3.** `PassiveTreeCatalogLoader.cs:318` says
*"D3: souls are a curve READ, never a roll or a formula."* D3's actual, canonical text
(`passive-tree-ideal.md:35`) is *"souls scale bonus power (unlimited, arithmetic cost)"* — it names
**what** souls buy, not **how** the read works; both a curve-table read and a `Θ`-offset formula are
consistent readings of that one sentence in isolation. What settles which one is real is §4's later,
more specific sentence (quoted above: *"must read `P(Θ)`"*) plus which one actually has shipped,
tested code — and that is unambiguously the `Θ`-offset formula, not a curve table. The comment's
citation is not fabricated, but it is citing D3 for a specific mechanism D3 itself never named, and
that specific mechanism was never built.

**A directly analogous case was already decided the other way, in this same codebase.**
`decision-d3-cost-rarity-rebase.md` §Q1.6 rejected minting `CurveInput.Band` for the item program's
own recipe-cost curve, on exactly this reasoning: *"Rejected: adding `CurveInput.Band`. It is an
ask-first change to E2 for something [the existing tool] already express[es], and it would express
*less*."* The soul-level case is the mirror image of the same principle: not "the existing curve
vocabulary already covers this, don't add a redundant one" but *"the existing power ladder already
covers this, better than a curve ever could, don't add a competing one."* Same principle, same
conclusion direction: don't mint.

**CLAUDE.md's own hard rule makes this a correctness question, not a style preference.** *"One power
ladder — no private curves... Writing a fresh `f(level)` in a subsystem is the exact defect that let
three incompatible curves ship at once."* `CurveTable.MultiplierAt` is a complete, independent,
per-mille interpolation function with no reference to `PowerLadder`/`P(Θ)` anywhere in its own file
(`CurveTable.cs`, read in full this session). Routing soul-level scaling through it — via any
`CurveInput` member, existing or new — would be building the second private curve this rule exists to
prevent, for a magnitude that already has a correct, single-ladder answer.

## Objective

Retire `NodeAtom.SoulCurveId` and its validation/round-trip machinery. Confirm, in the one place
`tree-catalog`'s own open question lives, that soul-level scaling is `tree-binder`'s `Θ`-offset formula
and nothing else — so the next reader of `tree-catalog`'s layer table (§1(a): *"the soul curve
reference"*) is not pointed at a field that has never done anything.

## 1. What exists today, and what changes

| Fact | Where | Disposition |
|---|---|---|
| `NodeAtom.SoulCurveId` (`string?`) | `NodeAtom.cs:50` | **Removed.** No consumer anywhere reads it to compute anything (verified: repo-wide `.SoulCurveId` grep, three hits — one production round-trip write at `RpgStore.TreeCatalog.cs:319` plus its own read-side deserialization at `:435`, and two test-only round-trip assertions in `CatalogHardeningTests.cs`; none of the three ever acts on the value) |
| `SoulCurveIdPattern` regex + import validation | `PassiveTreeCatalogLoader.cs:36-45,316-326` | **Removed** with the field — the validation is real and live (it would accept a genuine `curve.<id>` value today), it simply protects a value nothing has ever authored or read |
| `soulCurveId` SQLite column | `RpgStore.TreeCatalog.cs:71` (`CREATE`), `:310-319` (`INSERT`), `:419,435` (`SELECT`/deserialize) | **Stop reading and writing it; leave the column in place, harmless.** `FusionRpg.Data`'s only real schema-migration mechanism is `RpgStore.cs`'s `EnsureColumn` — **purely additive** (`ALTER TABLE ... ADD COLUMN`); this repo has no drop-column precedent anywhere, and `TreeCatalogMigrationTests.cs`'s own 7 tests all cover node-level `catalog_revision` retirement, a different meaning of "migration" than SQLite DDL. Claiming a "migration precedent" for a column drop would be citing evidence that does not exist. A dead, unread, unwritten column costs nothing and is the honest, minimal change; dropping it for real is optional follow-up work with its own new mechanism to design, not assumed here |
| `TreeBinderRun.cs:72`'s and `TreeBinderExplain.cs:102-103`'s `SoulCurveId: null` arguments | Both files | **Removed** along with the constructor parameter — two call sites, not one |
| `spec-tree-catalog.md` §1(a) — *"the soul curve reference"* | table row | **Corrected** to name the real mechanism: *"the soul-scaling formula's own tunable (`soulTrack.thetaPerSoulLevelMilli`, owned by `tree-binder`) — not a per-node field"* |
| `spec-tree-catalog.md` §OQ1 | Open questions | **Closed by this spec** — see §5 |
| `CurveInput` enum (`Level, Rarity, Tier`) | `CurveTable.cs:4-9` | **Untouched.** No member added, none removed — this spec's whole point is that soul level was never a `CurveInput` case to begin with |

## 2. The contract

### 2a. What ships instead of a fourth `CurveInput` member

Nothing new. `tree-binder` §5.1's formula is already the complete mechanism:
`Θ_node = Θ_actor + thetaPerSoulLevelMilli · soulLevel(node) / 1000`, then `P(Θ_node)` through the one
shared `PowerLadder`. This spec adds no code to compute a magnitude — it removes code that could never
have run.

### 2b. No migration — stop touching the column, leave it in place

**Corrected against a real check, not assumed:** this repo has no drop-column precedent anywhere.
`FusionRpg.Data`'s only schema-migration mechanism (`RpgStore.cs`'s `EnsureColumn`) is purely additive,
and `TreeCatalogMigrationTests.cs`'s own 7 tests all cover a different thing — node-level
`catalog_revision` retirement, not SQLite DDL. Designing and testing a real `DROP COLUMN` path is
optional follow-up work this spec does not need and does not attempt. The actual, minimal change: stop
reading `soul_curve_id` in the `SELECT` (`RpgStore.TreeCatalog.cs:419,435`) and stop writing it in the
`INSERT` (`:310-319`), remove `NodeAtom.SoulCurveId` from the C# record, and leave the SQLite column
itself in the `CREATE TABLE` statement, permanently `NULL`, harmless dead schema — no `catalog_revision`
bump needed, because nothing about the catalog's own content shape changes; only an internal field
nothing ever read stops being populated. If a real column drop is ever wanted later, it is new,
separately-designed work, not a step this spec's own removal depends on.

### 2c. What `soulCurveId`'s existence was plausibly protecting against, and why it is not needed

**Concern: might some FUTURE atom kind want a genuinely curve-shaped (non-`Θ`) per-node scale?** Yes,
in principle — but that is a different question than "how does *soul level* scale a node," and it
already has an answer if it ever arises: `CurveInput` stays exactly as extensible as it is today (3
members, "adding one is a reviewed change" — `CurveTable.cs:3`), and a future atom kind that
genuinely needs a curve read (keyed on `Level`/`Rarity`/`Tier`, or a real reviewed 4th input for some
other axis) can still add one *for that atom kind*, unconnected to soul-level scaling. This spec closes
one dead field; it does not narrow `CurveInput`'s own future.

## 3. What it must NOT do

- Add a `CurveInput.SoulLevel` (or any other name) member — §0's whole argument is that this is the
  wrong tool for this job, not merely an unbuilt one.
- Route any part of `Θ_node`'s computation through `CurveTable.MultiplierAt` — that would be exactly
  the "second private curve" CLAUDE.md's one-ladder rule forbids.
- Treat this as a magnitude retune requiring `tree-review`'s `provenance-supersede` gate — it changes
  no coefficient and no committed magnitude, only a field nothing ever read (§2b).
- Design or claim a `DROP COLUMN` migration this repo has no precedent for (§2b) — stop
  reading/writing the field and leave the column in place; a real drop is separate, optional,
  future work, not part of this change.
- Bump `catalog_revision` for this change — the catalog's own committed content shape is unaffected;
  only an internal, always-`NULL`, never-consumed field stops being populated.

## 4. Testing strategy

- `NodeAtom` no longer compiles with a `SoulCurveId` argument — both existing call sites
  (`TreeBinderRun.cs:72`, `TreeBinderExplain.cs:102-103`) and `RpgStore.TreeCatalog.cs`'s own
  constructor call at its `SELECT`-deserialization site (`:429-435`) update in the same change; a
  stray reference anywhere else fails the build, which is the cheapest possible regression guard for
  "did I actually remove every reference."
- `PassiveTreeCatalogLoaderTests.cs`: remove the `soulCurveId`-shape-validation test cases; confirm
  loading a **committed, real** node record (which has always carried `soulCurveId: null`) is
  unaffected byte-for-byte apart from the field's absence.
- `CatalogHardeningTests.cs`: remove the two test-only round-trip assertions this spec's own §1 names —
  there is nothing left to round-trip.
- A real test proves the `INSERT`/`SELECT` in `RpgStore.TreeCatalog.cs` no longer reference
  `soul_curve_id` at all (source-shape or a straightforward "column not in the parameter list" check) —
  no migration test needed, since the column itself is untouched (§2b) and every existing database
  keeps loading exactly as before, just with one fewer field populated on the C# side.
- `SoulTrackTests.cs`'s existing `Soul_level_offsets_theta_never_the_coefficient` and
  `spec-tree-binder.md`'s `theta_per_soul_level_is_read_as_per_mille` tests are untouched — this spec
  changes no behavior they cover, it removes a field neither of them touches.

## 5. Open questions

**Zero.** `tree-catalog`'s own §OQ1 (*"where does soul level enter `CurveInput`?"*) is closed here: it
does not, and the field that implied it might (`SoulCurveId`) is retired. `tree-binder`'s own §5.1
formula is confirmed, by this investigation, as the sole mechanism — no follow-up decision needed.

## Decisions implemented

| Requirement in this spec | Decision |
|---|---|
| §0, §1 | **D58** (owner, 2026-09-06) — the investigation that decision's own framing asked for, reported honestly even where it changes the shape of the original menu |
| §2a | **D3** (`passive-tree-ideal.md:35`, elaborated at §4) — souls scale bonus power, and that power **must read `P(Θ)`** |
| §2a | Implements nothing new — confirms `spec-tree-binder.md` §5.1's own D3 implementation is complete |
| §3 | CLAUDE.md / AGENTS.md's "One power ladder — no private curves" hard rule |
| §0 (precedent) | `decision-d3-cost-rarity-rebase.md` §Q1.6 — the same "don't mint a `CurveInput` member the existing mechanism already covers" reasoning, applied here in the opposite direction (the *existing power ladder*, not the existing curve vocabulary, already covers it) |
