# 23 — Passive-tree corpus distribution audit

**Date:** 2026-09-10 · **Population:** 42 shared trees, 1,677 committed nodes (of 1,680 planned) ·
**Method:** seedsmith `PassiveTree/*` metrics + plan-side re-derived quota cells · **Model calls:** none.

> Every claim below names its population. Do **not** summarise this as *"the catalog was reviewed"*
> (spec-tree-review.md §2). Machine gates closed what they can; the human census (H8/J10) is still
> outstanding and is a different claim.

---

## 1. Methodology (already built — this audit used it)

Three layers, from [spec-tree-review.md](../../architecture/passive-tree/spec-tree-review.md):

| Layer | What it answers | Instrument |
|---|---|---|
| **1. Machine gates** | distribution / skew / budgets / naming collisions / exclusion rate | `python -m seedsmith check --family PassiveTree` over 12+ `PassiveTree/*` metrics; thresholds in `data/tuning/passive-tree-targets.v2.json` |
| **2. Statistical sampling** | *"at most X% of trees carry a rejectable defect, at 95%"* | Clopper–Pearson acceptance ladder (J2); stratified via `sampling.stratified_sample` |
| **3. Human census** | *"every tree was judged"* (D24) | tree cards + corpus sheet + `sheetRead` gate (H6/H7); H8 pilot is owner-only |

This session closed two **wiring gaps** that made Layer 1 report `NOT_MEASURED` for the distribution
axes (`QuotaDrift`, `CellOccupancy`):

1. `NodeSeedRecord` now persists `quotaCell` (additive; old records stay legal).
2. `check --family PassiveTree` re-derives cells via `quota_for_plan` and registers
   `SpeciesUniqueness`, so both distribution metrics measure for real.

---

## 2. Findings — Layer 1 over the committed corpus

Measured 2026-09-10. Command: `python -m seedsmith check --family PassiveTree` from `tools/seedsmith`.

### 2.1 Before / after the wiring fix

| Metric | Before (this session, pre-fix) | After (wiring closed) |
|---|---|---|
| TreeEqualValue (plan-side) | NOTE — C1/R-A1/P-1/P-2 hold, 42 trees | unchanged |
| UnresolvedCount (hard gate) | NOTE — 3/1680 (1‰) ≤ 50‰ | unchanged; `--gate` exits **0** |
| ExclusionRate | GAP — 1676/1677 (999‰) vs ≤30‰ | unchanged (content defect) |
| NameCollision | GAP — 646/1677 (385‰) | unchanged |
| NearDuplicate | GAP — 116/1677 (69‰) vs ≤5‰ | unchanged |
| MechanismRamp | GAP — 3 tier shortfalls | unchanged |
| **QuotaDrift** | **NOT_MEASURED** (no cells supplied) | **measured** — 3066 NOTE / 0 GAP vs re-derived plan cells |
| **CellOccupancy** | **NOT_MEASURED** | **measured** — median 1 ≤ 2; max 37; 836‰ singletons |
| ExclusionResolvable | NOT_MEASURED — atom-tag registry unbuilt | unchanged (known blocker) |
| SpeciesUniqueness | not registered under `check --family` | **measured** — 105 U1/U2 GAP + 1 corpus NOTE |
| ExclusionPresentation | not registered (gates=True; would break hard-gate invariant) | run directly: NOTE — 1676/1676 present correctly |
| DeepMechanismValue | NOT_MEASURED — no CombatSim samples | unchanged |
| FavourDrift | NOT_MEASURED — no species favour assignments | unchanged |

**Exit codes (verified 2026-09-10 after wiring):** `check --family PassiveTree` → **1** (`EXIT_GAP`, content
defects above); `check --family PassiveTree --gate` → **0** (only `UnresolvedCount` gates).

### 2.2 Generation-vintage defects (root-caused, remediation = regenerate)

All 42 trees carry `promptVersion: tree-language/1`. The brief was reworded to `tree-language/2`
([brief.py](../../../tools/seedsmith/seedsmith/adapters/trees/nodegen/brief.py)) after the exclusion
flood was measured; the committed corpus was **not** regenerated (owner budget — filed as J13).

| Finding | Evidence | Remediation rung (spec-tree-review §6.2) |
|---|---|---|
| **ExclusionRate 999‰** | form split: reroute 1638 / nullification 38 / precedence 0 / none 1; target ≤30‰ (~2% D14) | **Rung 2** — regenerate trees under `tree-language/2` brief |
| **NameCollision 385‰** | 646 nodes share a display name corpus-wide | **Rung 2** |
| **NearDuplicate 69‰** | 116 nodes in Jaccard ≥0.6 name pairs; target ≤5‰ | **Rung 2** |
| **MechanismRamp shortfalls** | exactly the 3 missing nodes (below) | close the 3 holes, then re-check |

### 2.3 MechanismRamp ↔ missing nodes (verified)

| Missing node | MechanismRamp finding |
|---|---|
| `skill.dark-def-t4-n0` (mechanism, defensive t4) | dark:defensive:t4 — 0 of 1 |
| `skill.fire-def-t9-n0` (mechanism, defensive t9) | fire:defensive:t9 — 1 of 2 |
| `skill.wither-def-t9-n1` (mechanism, defensive t9) | wither:defensive:t9 — 2 of 3 |

These are H9's disclosed holdouts (1,677/1,680), not a separate ramp formula bug.

### 2.4 Plan-conformance distribution (re-derived cells)

Re-deriving every tree's six-axis cells via `quota_for_plan` and feeding them as
`quota_cells_by_tree`:

- **QuotaDrift:** 0 GAP — observed cells match the re-derived target within tolerance (tautological
  for a corpus that never persisted cells; becomes a real check once regenerated records carry
  `quotaCell`).
- **CellOccupancy:** 1,680 plan slots over 245 `(channelFamily, trigger+element)` cells; median 1
  (≤2 threshold); max 37; 836‰ singletons. The high singleton share is expected at this corpus size
  against a fine cell key — reported, not a ship-blocker under current thresholds.

### 2.5 SpeciesUniqueness (shared corpus only — no species trees yet)

- **U1** (name, flavor) repeats: dozens of cross-tree pairs (same generation-vintage defect class as
  NameCollision).
- **U2** (affixIds, quotaCell) composition repeats across trees: many fingerprints shared (notably
  `atom.shld-cycle`+`atom.shld-surge` and `atom.ferocity`+`atom.might` clusters). Expected at shared
  primary-tree scale where permitted affix pools overlap; calibrate thresholds only after a real
  species run (spec-species-tree.md §5.1).
- **U3:** no `affix.species.*` ids in this corpus — nothing to leak.

### 2.6 ExclusionPresentation

All 1,676 non-`none` exclusions print a template-correct `printedText`. The flood is a **rate**
problem (ExclusionRate), not a presentation-contract failure. D40's gate would pass every individual
exclusion today; the lot still fails ExclusionRate.

---

## 3. Still NOT_MEASURED — named blockers

| Metric | Blocked on | Owner |
|---|---|---|
| ExclusionResolvable | atom-tag registry (spec-tree-language.md §5.1) | effect-atom / tree-language |
| DeepMechanismValue | CombatSim win-share samples | tools/CombatSim sweep |
| FavourDrift | species favour-lock run | J9 |
| SpeciesUniqueness U3 (leak check) | species-namespace affix corpus | J7 |

---

## 4. Remediation ladder

| Priority | Action | Spend |
|---|---|---|
| **Done this session** | Persist `quotaCell`; wire `check --family` so QuotaDrift/CellOccupancy/SpeciesUniqueness measure | zero model calls |
| **J13 (owner go/no-go)** | Regenerate the 42 shared trees under `tree-language/2` to clear ExclusionRate / NameCollision / NearDuplicate | ~5,040 base+vote calls for the generic corpus |
| **Close 3 holes** | Finish `dark`/`fire`/`wither` missing mechanism nodes (part of J13 or a targeted resume) | 3 subjects |
| **H8** | 20-tree review pilot — calibrates Layer 2/3 rates | owner time |
| **Later** | atom-tag registry, CombatSim deep-mechanism sample, species run | other programs |

**Do not hand-edit** exclusion forms, names, or `kMicro` values. A rejection names the rule and
regenerates (spec-tree-review.md §6.1).

---

## 5. Commands used

```powershell
cd tools/seedsmith
python -m seedsmith check --family PassiveTree
python -m seedsmith check --family PassiveTree --gate   # only UnresolvedCount can fail the lot
python -m pytest tests/adapters/trees/test_nodegen_emit.py tests/adapters/trees/test_nodegen_cli.py -q
```

---

## 6. Design-gate checklist

```
[x] Subsystem: passive-tree / seedsmith metrics (tree-review, tree-language, tree-plan)
[x] Read: DESIGN-GATE §1 seedsmith + passive-tree map; seedsmith-map P1–P5; spec-tree-review;
    metrics/passive_tree.py; passive-tree-targets.v2.json; brief.py provenance note
[x] decisions.md: no new architecture lock; regeneration is a content pass (D24)
[x] Claims cite file:line or measured command output
[x] Verified against CODE (live check run + ledger/plan cross-check of 3 missing nodes)
[x] No §2 invariant contradicted
[x] Wiring gaps named as wiring gaps, not architectural walls
```
