# Task list — passive tree repair

Plan: [passive-tree-repair-plan.md](passive-tree-repair-plan.md). Parent plan:
[passive-tree-plan.md](passive-tree-plan.md). Map:
[docs/architecture/passive-tree-map.md](../docs/architecture/passive-tree-map.md).
Workflow skill: [.agents/skills/seedsmith-passivetree-repair/SKILL.md](../.agents/skills/seedsmith-passivetree-repair/SKILL.md).

**Rewritten 2026-09-11** from a measured baseline: 42/42 trees `Fail`, 266/1,680 nodes bound,
1,414 refused, 108 bound-but-inert, and a binder that crashes on the committed corpus.

**Standing verification for every task:** the module's own tests green · `dotnet build` clean ·
`guard-single-writer`, `guard-secondary-no-unity`, `guard-funnel-delta`, `guard-dal` pass ·
`guard-power` where a magnitude is touched · `python scripts/audit-overflow.py` 0 critical ·
`python scripts/audit-magic-numbers.py` 0 M1/M2 attributable to the task. For `tools/seedsmith` work,
add `python -m pytest tools/seedsmith/tests/adapters/trees`.

**Standing rule:** a task that edits generated seed/generated JSON **without** a `tools/seedsmith` or
`src/` code change is not a repair. If the only change is data, it is class D/E and must cite the code
evidence that the pipeline is already correct.

**Standing rule:** cite `Battle*` files by symbol, never by line (R9).

---

## Phase 0 — reproduce and freeze the baseline

### P0.1: Record the baseline census as a committed artifact
**Spec:** repair skill §1, §2. **Description:** the numbers in the plan's §1 are the program's
starting evidence. Write them into a small machine-readable baseline (or a test that recomputes them)
so every later phase reports a delta against a fixed number, not a remembered one.
**Acceptance:**
- [ ] A command exists that prints trees / bound / refused / inert / refusal-buckets in one run
- [ ] The baseline is recorded with the exact commands and the git revision it was taken at
- [ ] `tools/TreeBinder --check`'s crash is captured verbatim (stack trace + the pool-shaped family
      that triggers it)
**Depends on:** none. **Scope:** S.

### P0.2: Add a regression test that fails on today's code
**Spec:** repair skill §8. **Description:** a focused test that reproduces R1–R4 without the full
corpus: (a) a permitted-for-branch id that has no generated atom must be **excluded from the enum**,
(b) a `more`-op family must not crash the op parse, (c) a pool-shaped channel must not throw an
unhandled exception, (d) a bound `stat.modify` atom must be readable by the resolve source. These are
the tests the fixes below must turn green.
**Acceptance:**
- [ ] Each of R1–R4 has a named failing test on the current code, citing `file:line`
- [ ] No test asserts a corpus aggregate — each is focused enough to run in seconds
**Depends on:** P0.1. **Scope:** M.

### P0.3: Propagate the stale counts the specs still carry
**Spec:** DESIGN-GATE §3 evidence rule 6; repair skill §9. **Description:** `spec-tree-language.md`
§3 still says 16 kinds / 7 attach points / 98 affix families / 21 statuses; the live vocabulary is
18 / 9 / 125 / 24. These stale counts are how a later session re-derives a wrong answer. Correct the
citations in the specs this program touches; do not touch files outside passive-tree.
**Acceptance:**
- [ ] `spec-tree-language.md` §3's counted table matches a fresh count, with the counting command named
- [ ] `spec-tree-binder.md` §2's counted table matches
- [ ] Every corrected number names what was counted, not where it was quoted from
**Depends on:** none. **Scope:** S.

---

## Phase 1 — vocabulary alignment (R1)

### P1.1: Restrict the permitted affix enum to binder-resolvable families
**Spec:** `spec-tree-language.md` §4.2 step 6, §5.1; `vocab.py:17-25`'s own named blocker.
**Description:** `permitted_for_branch` currently returns every branch-tagged family (125). It must
return only ids the binder can resolve to a generated `AtomRow` (26 today). Build the missing
cross-reference: the set of family ids present in `data/seed/atoms/generated/family-expand.*.json`.
The enum is the schema's `enum`, so an unresolvable id becomes **unsampleable**, not rejected — the
spec's own design sentence.
**Acceptance:**
- [ ] `permitted_for_branch(b)` returns only ids that resolve to a generated atom, for all three tags
- [ ] A test asserts `set(permitted_for_branch(b)) ⊆ resolvable_family_ids` for every tag
- [ ] An empty result is still **held** (`UnsatisfiableCell`), never widened to the full list
- [ ] The 1,356 `affix 'X' does not exist` refusals drop to **0** on a fresh generation
**Verification:** a fixture family file with one entry and no generated atom is excluded; the real
corpus's refusal count is measured before/after.
**Depends on:** P0.2. **Scope:** M. **Files:**
`tools/seedsmith/seedsmith/adapters/trees/nodegen/vocab.py`, tests.

### P1.2: Decide and document the 99 unresolvable families
**Spec:** repair skill §3 (failure taxonomy). **Description:** 99 pickable families have no generated
atom. Either they are genuinely unexpandable (no shipped pool — `FamilyExpansion` already refuses some
by id) and belong out of the tree vocabulary permanently, or the item program owes them an expansion.
This task writes the list down with the reason per family, so the boundary is explicit rather than an
accident of which files were expanded.
**Acceptance:**
- [ ] Every unresolvable family id is classified: *no shipped pool* / *expansion never run* / *other*
- [ ] The classification is data the next phase reads, not prose in a comment
**Depends on:** P1.1. **Scope:** M.

---

## Phase 2 — binder robustness and kind parity (R2, R3, R4)

### P2.1: Make `AffixComposer` refuse pool-shaped channels instead of crashing (R3)
**Spec:** `spec-tree-binder.md` §7.1 (refuse, never repair). **Description:**
`AffixComposer.ParseAtom` reads `params.channel` as a string; 7 families author it as a pool object.
A pool must be resolved deterministically to one concrete channel, or the atom refused with a named
`BindRefusal` — never an unhandled `InvalidOperationException`.
**Acceptance:**
- [ ] `tools/TreeBinder --check` completes on all 42 trees without an unhandled exception
- [ ] A pool-shaped channel is either resolved to a concrete channel (with the rule named) or refused
      as a `BindRefusal`, and a test proves both paths
- [ ] The refusal names the family, the pool, and the rule (`§7.1`)
**Depends on:** P0.2. **Scope:** S. **Files:**
`src/FusionRpg.Core/PassiveTree/Binding/AffixComposer.cs`, tests.

### P2.2: Add `More` to the tree's op vocabulary, or exclude `more`-op families (R2)
**Spec:** `spec-tree-catalog.md` §2.3; `AtomKindRegistry.cs:517`; `NodeAtom.cs:9-15`.
**Ask first** — widens a shipped contract. **Description:** `stat.modify` legitimately supports
`More`; `NodeAtomOp` does not. Either add `More` and thread it through `TreeBinderRun` /
`TreeAtomSource` / `TreeBinderExplain` / `PassiveTreeCatalogLoader`, or exclude the 6 `more`-op
families from the tree vocabulary in P1.1. The plan's default is **exclude first** (cheap, reversible,
no shipped-contract change), then decide `More` on evidence.
**Acceptance:**
- [ ] A `more`-op atom either parses to a real op and resolves, or is excluded from the permitted set
- [ ] A test proves the chosen path; a `more` op never reaches an `Enum.TryParse` failure
- [ ] If `More` is added: a source-shape test proves `TreeAtomSource` handles it, and the derived-side
      absence (`AtomDerivedSubsystem.TryParseOp`) is unchanged
**Depends on:** P1.1. **Scope:** S–M.

### P2.3: Decide the one kind a tree node targets, and make binder + resolver agree (R4)
**Spec:** `spec-tree-binder.md` §4.1; `spec-tree-resolve.md` §2.1, §12 test 15. **Ask first.**
**Description:** the binder prices `stat.modify` (primary channels); `TreeAtomSource` reads only
`stat.derived`. Pick one. The plan's default is `stat.derived` — it is the resolve spec's worked
example, the shipped `AtomDerivedSubsystem` fan-in's kind, and the one that needs no new producer.
Then make `TreeBinderRun` emit that kind, or add a primary-channel producer if the owner chooses the
other direction.
**Acceptance:**
- [ ] Every `NodeAtom.KindId` the binder emits is a kind `TreeAtomSource.BoundAtomsFor` reads
- [ ] A test in the `TreeFanInTests` shape registers the real `AtomDerivedSubsystem`, resolves an
      actor owning one node, and reads the moved `combat.*` channel back
- [ ] Lawn and battle totals agree (spec-tree-resolve §12 test 15)
**Depends on:** P2.1. **Scope:** M. **Files:**
`src/FusionRpg.Core/PassiveTree/Binding/TreeBinderRun.cs`, `src/FusionRpg.Core/PassiveTree/Resolve/`,
`src/FusionRpg.Core/Battle/TreeAtomSource.cs`, tests.

---

## Phase 3 — mechanism carriage (R5)

### P3.1: Carry mechanism-class atoms through the binder
**Spec:** `passive-tree-ideal.md` §3.5; `spec-tree-resolve.md` §2.3; `spec-mechanism-wiring.md`.
**Description:** a mechanism node's atom (`status.apply`, a trigger rider) is skipped by
`TreeBinderRun.cs:49-61`, so 108 of 266 bound nodes are inert and no deep-tier mechanism survives.
Carry the atom with its real `kindId` even when it has no ladder-scaled amount, and make the resolve
source not drop it.
**Acceptance:**
- [ ] A mechanism-class node's atom appears on the bound `NodeAtom` with a real `kindId`
- [ ] A deep-tier mechanism node is proven live in the resolver (owned, gate-open, contributing)
- [ ] `PassiveTree/MechanismRamp` still matches `archetypes[].mechNodes[t]` exactly, both directions
- [ ] No node is "bound" while carrying zero readable atoms — the metric that counts that is 0
**Depends on:** P2.3. **Scope:** M.

---

## Phase 4 — resolve proof (acceptance)

### P4.1: End-to-end proof — one node changes a number on lawn and in battle
**Spec:** `spec-tree-resolve.md` §12 test 15, §14 success criteria 1–4a. **Description:** the
program's central acceptance: a single owned, gate-open, enabled node moves a real channel through
the shipped fan-in on both read modes, with identical totals, no new subsystem and no new order band.
**Acceptance:**
- [ ] The `TreeFanInTests` shape extended to both modes
- [ ] `F ∈ [1, Fmax]` both bounds; `Fmax = 1000‰` removes `F` byte-identically
- [ ] Withdrawal (un-owning) returns the channel to zero
- [ ] Every contribution carries `tree.{treeId}.{nodeId}` (GG-49)
**Depends on:** P3.1. **Scope:** M.

---

## Phase 5 — regenerate the corpus and re-audit (R6)

### P5.1: Regenerate all 42 trees through the real CLI
**Spec:** repair skill §6. **Description:** after R1–R5 land, regenerate `data/generated/passive-tree`
via `tools/TreeBinder` and re-run the language stage where the vocabulary changed. Record exactly what
was regenerated and why. **This is Phase 5, not Phase 1 — regeneration is proof, not repair.**
**Acceptance:**
- [ ] `tools/TreeBinder --check` exits 0 on the regenerated corpus (byte-reproducible)
- [ ] Every tree's verdict is `Pass` or carries a named, evidenced `Fail` reason
- [ ] `python -m seedsmith check --family PassiveTree` runs with real wired data (not `NOT_MEASURED`)
- [ ] The one hard gate (`PassiveTree/UnresolvedCount`) is genuinely measured and green
- [ ] `ExclusionRate` / `NearDuplicate` GAP findings are re-measured and either closed or deferred
      with a named owner
**Depends on:** P4.1. **Scope:** L (machine time, resumable).

### P5.2: Live-boot proof against the regenerated corpus
**Spec:** parent plan H9's own acceptance. **Description:** boot a real server against the committed
corpus and confirm the import prints the tree count with zero refusals — and now, that a node's value
is actually readable.
**Acceptance:**
- [ ] A live boot imports the corpus with zero refusals
- [ ] A real player with an allocation shows a non-zero tree contribution
**Depends on:** P5.1. **Scope:** M.

---

## Phase 6 — balance measurement (the actual goal)

### P6.1: Re-run `squad-harness` S4 against the real direct-channel model
**Spec:** `spec-squad-harness.md` §4/§11 S4; parent plan F7/F8. **Description:** the parent plan's F7
found the fold-back model is not representative and F8 rebuilt it on the direct-channel shape. Only
now, with real bound contributions, can the sweep produce a balance answer. Re-run concentration,
cross-unlock and the soul track; republish `budget.treeTotalPoints` / `treeShareMilli` /
`soulTrack.thetaPerSoulLevelMilli` if the measurement moves them.
**Acceptance:**
- [ ] `F ∈ [1, Fmax]` at squad scope; no tree is OP
- [ ] D42's dials are republished with the measurement behind them, or explicitly still flagged
- [ ] A balance report names which of the ideal's §3.5 mechanism classes actually rescue a focus build
**Depends on:** P5.2. **Scope:** M–L.

### P6.2: Close or file the parent plan's two content GAP findings
**Spec:** parent plan (2026-09-07 pass). **Description:** `ExclusionRate` (999‰ vs ≤30‰) and
`NearDuplicate` (69‰ vs ≤5‰) are real, measured content-quality findings on the committed corpus.
`ExclusionRate`'s root cause was fixed at the source (`tree-language` brief wording, `PROMPT_VERSION`
bumped); `NearDuplicate` has no live-generation suppression yet. Either land the suppression or file it
with a named owner and a default.
**Acceptance:**
- [ ] Each finding is either closed with a measurement or filed with an owner and a default
- [ ] Neither is silently dropped
**Depends on:** P5.1. **Scope:** M.

---

## Ask-first items (owner decisions)

| # | Decision | Default if no answer | Blocking task |
|---|---|---|---|
| A1 | Tree node targets `stat.derived` only, or primary `stat.modify` too? | `stat.derived` only | P2.3 |
| A2 | Add `More` to `NodeAtomOp`, or exclude `more`-op families? | Exclude first | P2.2 |
| A3 | Mechanism-carriage shape (`kMicro = 0` + real `kindId`, or a new field)? | `kMicro = 0` + real `kindId` | P3.1 |
| A4 | The cross-program `tier-bands.v1.json` pricing gap (parent plan D2) — accept the cap, or fund the fix? | Accept + document | P5.1 |
