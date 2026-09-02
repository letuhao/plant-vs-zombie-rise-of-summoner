# Implementation plan: `seed-to-concrete`

**Spans two capability maps** — [demon-seed-map.md](../docs/architecture/demon-seed-map.md) (16 modules)
and [effect-pipeline-map.md](../docs/architecture/effect-pipeline-map.md) (10 modules). 26 modules,
**62 tasks, 9 phases, 9 checkpoints.**

> **Why one plan and not two.** Both maps say it in their own words: *"neither program can finish
> alone."* `demon-seed` 15-16 gate on `effect-pipeline` 1-4 and 7-8, and the phases genuinely
> interleave — Phase 5 is a single vertical slice made of modules from both. Two plans would split that
> slice down the middle and hide the only dependency that matters.
>
> **Why not `tasks/seedsmith-plan.md`.** That pair belongs to the existing seedsmith program (60/395
> checkboxes, the audit stream). It is not a fallback and was not read. Both capability maps gain a
> pointer to this file so the map stays the index.

**Tasks:** [seed-to-concrete-todo.md](seed-to-concrete-todo.md).

---

## Overview

Turn every one of ~904 captured PvZ species into a demon that has an identity, a stat block, and
**effects that differ per player** — with no model ever choosing a number, and no player's machine ever
calling a model.

```text
[dev]     almanac -> anchors (enums only) -> containers        seedsmith, committed, shipped
[release] the seed ships inside the zip
[runtime] import -> catalog_revision -> roll per player -> that player's tables
```

---

## Architecture decisions (locked in the ideals; restated so the task list is readable)

- **Seed → concrete → per-player.** Seedsmith emits seeds; the *game runtime* rolls concrete objects
  seeded per player. One shared SDK — `Instantiator` — never a second roll implementation.
- **The LLM writes identity; deterministic code writes magnitude.** Enforced by a mechanical schema
  audit, not by review.
- **Two layers.** Species *stats* are deterministic and shared; only *effects* roll. This is what keeps
  `WaveCatalog`, `DemonRecipeCatalog`, `DemonMaterialCatalog` and `LaneCost` free of player context.
- **Four resolution layers**, in this order: `slots → affixes → atoms → tiers → values`, each with its
  own named RNG stream.
- **`traits_json` answers *which*; a `trait.{id}` container answers *what it does*.** Fusion needs no
  change and no save migrates.
- **One power ladder.** Contests read `Θ`; magnitudes read `P(Θ)`. `long` everywhere, overflow throws.

---

## Phases and the slice each one delivers

| Phase | Slice — what is demonstrably true at the end | Modules |
|---|---|---|
| **0** | Every decision doc agrees with what we are about to build | 12 amendments |
| **1** | All ~904 species are visible, with a **measured** power basis — **zero model calls** | ds 1-5 |
| **2** | 904 classified anchors exist, and their distribution is measured | ds 6-9, 14 |
| **3** | ⭐ **The effect runtime is no longer inert** — one container rolls, binds, and executes | ep 1-4 |
| **3.5** | ⭐ **One species walks the whole chain** — an automated seam test, stubs where modules are missing | — |
| **4** | Demons have stats in the game; the compiled catalog is gone | ds 10-13 |
| **5** | ⭐ **A demon does something, and two players' demons differ** | ep 7-8, ds 15-16 |
| **6** | The two legacy effect paths are absorbed; one path remains | ep 5-6 |
| **7** | Named multi-atom affixes exist | ep 9-10 |

**Phase 3 does not depend on Phases 1-2** — it needs no anchors, only a fixture container. If two
streams are ever available, that is the split.

**Phase 3.5 is the vertical slice this plan otherwise lacked.** Phases are organised by module, which is
closer to horizontal layers than the planning discipline asks for. The walking skeleton corrects that:
one species from almanac row to executed effect, real modules where they exist and stubs where they do
not, asserting **the shape at each seam**. It carries **no human gate** — it is a test that runs in CI
from Phase 3.5 onward, and each later phase replaces at least one of its stubs.

**Metrics are built alongside, not at the end.** Owner: *"seedsmith don't have FE, use logs + metrics to
evaluate it — so need plan to build metrics along with seedsmith, consider it a part of seedsmith."*
Every phase therefore registers its own metrics with **declared targets in tuning** (P2) and a stated
loop kind (P3): T1.10 corpus coverage, T2.12 pipeline health, T3.8 affix health. There is **no FE in
this plan at all** — every output is a committed file, a CLI report, or a debug endpoint.

---

## Why this order

**Phase 1 spends no tokens and still produces the roster's real coverage number.** Every module in it is
deterministic, so the expensive stage's inputs are reviewable before a single call is made. This is the
same property the seedsmith G0/G1 wave was built on.

**Phase 3 is deliberately early.** `effect-atom-map.md:213` records that E6/E7/E15/E19 are *"proven
correct end to end by tests, unreachable end to end in production."* Four built modules need one call.
Getting that call working against a **fixture** container — before any content exists — is the single
highest-information task in this plan, and it can fail in ways no amount of design review would find.

**Phase 6 is late and parallel.** Both absorptions migrate shipped, save-affecting data. They wait until
the new path is proven, because two risks in one change is how a proof becomes a post-mortem.

---

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| **T3.7 (the proof) fails for a reason design review cannot find** | High — it gates Phases 5-7 | It is scheduled early *because* of this. A fixture container needs no content, so the failure is cheap and isolated |
| `patron-absorption` moves a number and invalidates the patron program's standing SIM results | High | Acceptance is a before/after equality test across the **full** (rarity × star × level × Θ) grid, not a spot check |
| `catalog-runtime` changes how 9 shipped call sites get data | High | Diff test against the compiled roster **while both exist**; deletion only after it passes; a real lawn run is a required acceptance step, not optional |
| The full classification run (~16,000 calls, ~14 h) fails mid-way | Medium | `run-control` is built *before* the run (T2.8-2.9), and T2.11 runs a 20-species subset first |
| `rarity-migration` silently changes meaning — `>= DemonRarity.Rare` becomes 90% of the roster, not 75% | High | A guard test forbids relational comparisons against named members and bare int casts; the fusion output set is pinned by rung |
| The 8 `poolRolls` files grow before T3.2 lands | Low now, compounding | Measured: 8 files today. Every container authored before Phase 3 adds to it |
| The walking skeleton's stubs hide a real seam defect | Medium | Its stub count is **printed**, so the remaining gap is visible rather than assumed; Checkpoint 5 requires zero stubs left |
| A metric ships with no declared target and becomes an opinion | Medium | Each metrics task asserts its targets live in tuning — the item corpus once ran three waves with nine empty partitions and green validators |
| Impure input reaches the materialiser, so rosters differ per machine | High, silent | A guard test over the materialiser's source; an enumeration-order shuffle test |

---

## Verification commands

```powershell
# per task
python -m pytest tools/seedsmith/tests/<file>.py
dotnet test tests/FusionRpg.Core.Tests --filter <Name>

# per checkpoint
python -m pytest tools/seedsmith/tests
dotnet test tests\FusionRpg.Core.Tests; dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.Guard.Tests; dotnet test tests\FusionRpg.Launcher.Tests
.\scripts\guard-single-writer.ps1; .\scripts\guard-secondary-no-unity.ps1
.\scripts\guard-funnel-delta.ps1; .\scripts\guard-dal.ps1
python scripts\audit-overflow.py
python scripts\audit-magic-numbers.py --summary
```

⚠️ **`dotnet test` in the background can die silently** — use plain `>` redirection, never a pipe.
⚠️ **CI's test step only checks the last `dotnet test` exit code**; run the suites individually.

---

## Open questions

**None.** Sixteen were raised across the two ideal docs and this plan's audit; all sixteen are answered.
The audit itself found two defects in this plan — a missing amendment (the `seed-contract.md` status
line, which forbids the authoring Phases 1-2 do) and an ordering error (the shared authoring shape was
asserted in T7.2 *after* T5.3 had already built one). Both are fixed above. What remains are
balance numbers — the variant-shift table, the affinity→weight mapping, the ten-rung summon-rate spread
— and those follow `ssot-power-scale.md` §5.3's own precedent: **pick starting values, tune from play**,
with a comment saying they are starting values. They are tasks, not decisions held open.

---

## Owner-only steps

Git is hands-off in this repo, so every task ends with the work in the tree and a suggested commit
message. Two further steps are the owner's alone:

- **The full classification run** (T2.11) — it is ~14 h against the local model on the owner's machine.
- **The live lawn check** (CP4) — `deploy-play.ps1 -RestartServer` from the owner's own terminal, per
  the server-lifetime rule.
