# Passive tree — capability map

**Status:** in use, 2026-09-05. All eleven module specs written against it — see `passive-tree/spec-*.md`. Source: [passive-tree-ideal.md](passive-tree-ideal.md) — 36 owner decisions, 16 research
documents in [../research/passive-tree/](../research/passive-tree/).

This is the index of what exists for this program. **Never guess which spec is active from a
filename** — read this table.

---

## Modules

| Module id | Responsibility | Depends on |
|---|---|---|
| `squad-harness` | Six-vs-wave balance measurement. Answers D33's scope mismatch: every existing number is a 1v1 duel, the game fields six | — |
| `mechanism-wiring` | The four inert lines that make mechanism nodes executable and *scorable*. §3.5 proved these are the only node class that rescues a focus build | — |
| `tree-plan` | Stage 1, deterministic. Topology (10 tiers × 2 branches, 40 nodes, **rootless**), tier ladder, budgets, shape archetypes, potency ceiling (**91‰**, derived), the property vocabulary, and the plan schema handed downstream | — |
| `tree-catalog` | The baked artifact. Node record shape, id stability, catalog versioning, the freeze line, the load path | `tree-plan` |
| `tree-language` | Stage 2. What the language stage may choose, from which closed vocabularies, under which quotas, behind which validation gates | `tree-plan` |
| `tree-binder` | Stage 3, deterministic. Budget share → stored coefficient, atom composition, channel legality, conversion refusal | `tree-plan`, `tree-language`, `tree-catalog` |
| `tree-review` | Making ~35,160 nodes across 879 trees reviewable: sampling design, the tree-card artifact, escalation, incremental re-review | `tree-catalog` |
| `tree-state` | Per-actor allocation and soul levels. Sparse storage, rising unlock cost, respec, the migration boundary | `tree-catalog` |
| `tree-resolve` | How tree power reaches combat. Tier gates, cross-unlock, the concentration index, the soul→Θ read | `tree-state`, `mechanism-wiring` |
| `tree-surface` | The player surface. Browse, plan-before-spend, printed exclusions, per-actor management | `tree-state`, `tree-catalog` |
| `species-tree` | D23/D30's unique per-species trees (**840 species × 40 nodes = 33,600**) and their own generation pipeline | `tree-language`, `tree-binder`, `tree-review` |

**No cycles.** Every arrow points one way. `tree-resolve` reads `tree-state`; `tree-state` never
reads `tree-resolve`.

## Build order

```
wave 0   squad-harness · mechanism-wiring · tree-plan        (fully parallel, no shared files)
wave 1   tree-catalog · tree-language
wave 2   tree-binder · tree-state
wave 3   tree-resolve · tree-review · tree-surface
wave 4   species-tree
```

## Two gates the audit round added (2026-09-05)

**A10 gates `tree-language --write`, not `tree-plan --emit`.** The plan is cheap and mints no ids, so
being wrong there costs one regeneration. The irreversible step is the next one: ~4,680 model calls
for the generic corpus, ~105,840 for species, and ~34 human hours per review pass. Committing that
against an unmeasured premise is how the program buys 35,160 nodes and discovers in wave 3 that the
deep tiers do nothing. **Corrected 2026-09-05 after `spec-mechanism-wiring.md` split A10 against code — "after G1 and G3" was wrong in both directions:**

- **A10a**, the static-snapshot difference, needs **no wiring at all**. `BattleActorSetup.ChannelMods` already carries `(ChannelId, long)`, the composer folds it and **throws** on an unknown id, and the resolver reads all eight defensive families off the defender's snapshot. It runs over `BattleEngine` in **wave 0**.
- **A10b**, the shipped vehicle, needs G1 **and G2** — which this map never named — **plus a Battle status→`DerivedLedger` producer that does not exist and is in no module's modified-files table.** `BattleStatusSpec` carries no `StatMods` at all, and `BattleDerivedModifierLedger.Add` has one caller: the construction-time aura loop.

So: **run A10a now, gate `tree-language --write` on it, and treat A10b as a separate item with an unowned prerequisite.**

**A tree's gate quantity must exist before that tree's content is generated** (§13.4). Today only
the 12 primary trees have one. `element_mastery` belongs to the demon program's `aspect-scope`
module; `status_applied` belongs to nobody yet. Generating the elemental and status corpus first
buys **1,080 nodes at tier 0**.

So the corpus order is: **primary trees (480 nodes) → then one category per gate quantity as it
lands**, never the whole 1,560 at once.

**Wave 0 is parallel by construction** — the harness is a `tools/` project, the wiring is four named
lines in `Core`, and the planner is new code with no shipped caller. Nothing in wave 0 touches
another wave-0 module's files.

**Why `squad-harness` is first even though nothing depends on it.** It is not a gate any more (D33 as
amended), but it is cheap and it is the only thing that can tell us whether `F`, `Fmax`, D28's
largest-mate rule and *"magnitude cannot rescue focus"* survive at the scope the game is played at.
Every number it touches is a **tunable** (§14) — so the specs downstream name the key and the unit,
and the harness settles the value later without reopening a spec.

**Why `mechanism-wiring` is in wave 0.** `tree-plan` must reserve budget for mechanism nodes at
deep tiers. If the wiring never lands, that budget buys nodes that measurably do nothing, and we
would not find out until `tree-resolve`. Its critical path is one file: a fourth
`IActorStatSubsystem`, ~90 lines by the shipped `AtomDerivedSubsystem` precedent.

## Boundaries between modules

- `tree-plan` emits a **plan document**; it never emits node text or a magnitude.
- `tree-language` chooses **from closed enums**; it never writes a number. The permitted subset *is*
  the schema enum, so an out-of-quota value is unsampleable rather than rejected.
- `tree-binder` writes **coefficients**, not magnitudes — which is what lets one static catalog be
  correct for every player at every Θ.
- `tree-catalog` is the only module that defines the on-disk record. Everything else reads it.
- `tree-state` stores **effort** (which nodes, how many souls), never derived power — so a rebalance
  needs no migration.
- `tree-resolve` is the only module that multiplies anything by `P(Θ)`.

## Paths

| Artifact | Path |
|---|---|
| This map | `docs/architecture/passive-tree-map.md` |
| Module specs | `docs/architecture/passive-tree/spec-<module-id>.md` |
| Plan | `tasks/passive-tree-plan.md` |
| Task list | `tasks/passive-tree-todo.md` |

`SPEC.md`, `tasks/plan.md` and `tasks/todo.md` belong to other streams and are never used here.

## Assumptions this map makes

Correct any of these now — they shape module boundaries, and boundaries are expensive to move later.

1. **The generator is a `tools/` program, not a runtime.** D24 makes the catalog build-time content,
   so generation lives beside `tools/seedsmith/` and its output is committed data. Nothing in
   `src/` generates a node.
2. **`tree-resolve` extends the shipped resolver rather than forking it.** Tree power arrives as
   ordinary channel contributions through `IActorStatSubsystem` / atoms, not as a parallel combat
   path.
3. **The web surface is the primary one; the injector may enrich it, never gate it** (standalone-first).
4. **Species trees reuse the generic node record**, differing in content and provenance rather than
   in schema — otherwise `tree-catalog` needs two record types.
5. **`squad-harness` is measurement only.** It ships no balance change; it reports numbers that later
   land as tunables.
6. **Every balance number named in a spec is a tunable key with a unit**, per §14 — specs do not carry
   values that a balance pass would move.
