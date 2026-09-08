# Spec: resource-ownership (A-R1)

**Status: DRAFTED 2026-09-03** — added by the spec-coverage audit, which found this work **marked ✅ done
in the ideal and owned by no spec, with its deliverable absent from the repo.** Module **A-R1**,
action-corpus Phase 0. **No dependencies.**

**What it owns: the generator that makes resource coverage true by construction.** Phase 0 fixed the
*instance* — all four resource families now cover all six resources. It did not fix the *cause*: 526
aptitude edges are still hand-maintained, so a seventh resource would need 36 more edges published by
hand.

---

## 1. Why this module exists

[`action-corpus-ideal.md`](../action-corpus-ideal.md) §30 task 0.4 read:

> **0.4** ✅ Author `resource-ownership.v1.json` + the generator that emits edges from it

**The file does not exist.** `find . -name "*resource-ownership*"` returns nothing, and a repo-wide grep
for `resource-ownership` / `ResourceOwnership` across `*.cs`, `*.json`, `*.py` and `*.ps1` returns zero
hits. The task is corrected to ⛔ in §30.1, and this spec is its owner.

**What actually happened:** the 92 missing edges were added **by hand**, through the `--add-edge` and
`--rename-key` support built into `tools/tuning/publish.py` for exactly that purpose. **The symptom was
fixed; the declared root cause was not** — and §29 is emphatic about the difference:

> *"The root cause is that 486 edges are hand-maintained… So the fix is not 'add 92 rows by hand' —
> **that reproduces the defect at a larger size.**"*

So §29's promised property — *"a seventh resource is covered by construction: add it to `ResourceIds`
and the generator emits its 36 edges"* — **does not hold today.**

---

## 2. What exists today

| Thing | State | Evidence |
|---|---|---|
| All four resource families cover all six resources | **built** | `data/tuning/aptitudes.v5.json`, 526 edges |
| A drift guard holding that coverage | **built** | `AptitudeTuningTests.EveryResourceIsFedInEveryResourceFamily` |
| `DominanceGuard.ReservedFamilies` loops `ResourceIds` rather than listing | **built** | the one place that already got this right |
| `tools/tuning/publish.py --add-edge` / `--rename-key` | **built** | added 2026-09-02; each refuses duplicates, unknown sources and new families |
| `data/tuning/resource-ownership.v1.json` | ⛔ **does not exist** | — |
| A generator emitting edges from an ownership table | ⛔ **does not exist** | — |

**Sorted: real gap.** Nothing here is inert wiring — the artifact and its generator were never written.

---

## 3. The contract

### 3.1 The ownership table — 18 rows a human can read

`data/tuning/resource-ownership.v1.json`, published through `publish.py`, never hand-edited:

```jsonc
{
  "schemaVersion": 1,
  "_meta": { "note": "18 declared rows generate 216 aptitude edges. Never hand-author an edge." },
  "families": {
    "max": {
      "floors": { "hp": 6000, "stamina": 8000, "hunger": 5000, "spirit": 3000, "qi": 6000, "poise": 5000 },
      "owners": { "hp": {"Bulwark": 32000}, "stamina": {"Vigor": 26000},
                  "hunger": {"Retribution": 26000}, "spirit": {"Composure": 28000},
                  "qi": {"Focus": 30000}, "poise": {"Bulwark": 28000} }
    },
    "regen":      { "floors": { }, "owners": { } },
    "efficiency": { "floors": { }, "owners": { } },
    "restore":    { "floors": { }, "owners": { } }
  }
}
```

**The shape is floor-plus-owner-spike**, because that is the pattern the grid already follows — measured
across all edges, every filled `(family, resource)` cell has exactly 12 edges, one per aptitude, shaped
as a shared floor plus one owner. **This module declares the pattern the data already has; it does not
invent one.**

### 3.2 Density differs by family, and that is principled

- **`max` and `regen` are dense** — a floor for every aptitude, so no build is helpless on any pool.
- **`efficiency` and `restore` are sparse** — owners only. `resource.efficiency` is `SumIncreased`
  **capped at 1.0** (`DerivedStatPolicy.ResourceEfficiencyCap`), so twelve contributors against a hard
  cap would make it trivially reachable and turn a build choice into a formality.

**This matches the shipped data**, measured: `resource.efficiency.{hp:2, stamina:3, hunger:2, spirit:2,
qi:1, poise:2}` — sparse, as §30 task 0.3 decided.

### 3.3 The generator

Emits the full edge set from the 18 rows, and **publishes through `publish.py`** so the output is a
normal versioned tuning file with no special path.

- **Deterministic and total** — same table in, byte-identical edges out.
- **A `--check` mode** that regenerates in memory and diffs against the shipped `aptitudes.v{n}.json`,
  exiting non-zero on drift. This is what makes the property testable rather than aspirational.
- **It reads `ResourceIds` and the aptitude roster**, never a copied list. That is the whole point.

---

## 4. What this module must NOT do

- **Change any shipped coefficient.** The first emission must reproduce `aptitudes.v5.json`'s resource
  edges **exactly**. A generator whose first run moves balance is indistinguishable from a bug.
- **Hand-author an edge.** If the generator cannot express a cell, the *table* is wrong.
- **Copy `ResourceIds` or the aptitude list.** Read the SSOT — that is the defect being fixed.
- **Bypass `publish.py`.** Tuning files are never hand-edited.
- **Use `float`.** Per-mille integers; overflow throws.
- **Cap a magnitude.** The 1.0 efficiency cap is a **bounded ratio**, exempt under `AGENTS.md` and it
  must carry the comment saying so.

---

## 5. Testing strategy

| # | Test | Proves |
|---|---|---|
| 1 | Generating from the table reproduces **`aptitudes.v5.json`'s resource edges byte-for-byte** | The first run is balance-neutral |
| 2 | Adding a **seventh** id to `ResourceIds` emits **36 new edges** with no generator change | §29's promised property, mechanically |
| 3 | `--check` exits non-zero when a shipped edge is hand-modified | Drift is caught |
| 4 | Generation is deterministic across two runs | Byte-identical |
| 5 | **Planted violation:** a table naming an unknown aptitude is **refused by name** | No silent skip |
| 6 | **Planted violation:** a table missing a resource from a dense family is refused | Density is a declared property, not an accident |
| 7 | `EveryResourceIsFedInEveryResourceFamily` still passes on generated output | The existing drift guard holds |
| 8 | Dominance and residual baselines are **unchanged** by the first emission | Follows from test 1 |

**Test 2 is the reason this module exists.** Everything else confirms it changed nothing; test 2 is the
only one that proves the defect is actually fixed.

---

## 6. Acceptance criteria

1. `data/tuning/resource-ownership.v1.json` exists, published, 18 declared rows.
2. A generator emits the full edge set from it, deterministically.
3. **The first emission reproduces the shipped edges byte-for-byte** — zero balance movement.
4. A seventh resource id yields 36 edges with no code change (test 2).
5. `--check` fails on drift and runs in CI.
6. No list is copied — `ResourceIds` and the aptitude roster are read.
7. §30 task 0.4 is marked ✅ **with the file present**, and §30.1's correction notes the closure.

---

## 7. Dependencies and cross-program hazards

| | |
|---|---|
| **Depends on** | Nothing |
| **class-system** | Owns the aptitude tuning and its three baselines. **A regeneration is a re-bless** — coordinate; the baselines were re-blessed 2026-09-02 and are uncommitted |
| **`publish.py`** | The only legal write path. Its `--add-edge` exists because a coverage gap previously had *no legal way to be closed*; after this module, gaps close by editing the table instead |
| **`DominanceGuard`** | Already loops `ResourceIds`; its coverage block must stay in step with generated output |
