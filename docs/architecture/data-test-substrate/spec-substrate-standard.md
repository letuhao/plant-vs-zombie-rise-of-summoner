# Spec: `substrate-standard`

**Module id:** `substrate-standard` · **Program:** [data-test-substrate](../data-test-substrate-map.md) · **Build order:** 6 of 6
**Depends on:** `memory-storage-plan`, `store-test-migration`, `disk-write-probe`.
**Model calls:** none.

## Objective

Make the test-substrate rule **binding and documented**, so the 65.5 GB class of leak cannot return and
a future session does not re-derive the policy from scratch.

Most of this module **already shipped** (`7183a59e`): the standard doc and the static gate. What
remains is the **`data-architecture.md` amendment** (the store now has a storage plan; the DAL boundary
is unchanged; a shared-cache memory DB cannot be opened read-only) and the cross-links.

**Success looks like:** `testing-standard.md` states R1–R5 and is referenced from `AGENTS.md`/`CLAUDE.md`
and `data-architecture.md`; the gate enforces the mechanically-checkable half; the architecture doc
describes the shipped storage plan.

## Assumptions I'm making (correct me now)

1. The standard lives at `docs/contributing/testing-standard.md` (owner-selected 2026-09-12), mirroring
   `session-boundary.md`.
2. `AGENTS.md`/`CLAUDE.md` are gitignored local files; they **point** at the tracked standard and are
   not themselves the durable record (owner-authorized edit, already made).
3. The `data-architecture.md` amendment is the module's only remaining build work; R1–R5 and the gate
   are review, not new construction.

## Design

### 1. R1–R5 (shipped — document, do not rebuild)

| Rule | Statement |
|---|---|
| **R1** | A store test runs **in memory** by default; it does not create a temp dir or build a store from a path. |
| **R2** | Disk **only** when the disk is the thing under test: legacy migration + sidecar, `journal_mode == 'wal'`, `File.Exists` on hot/media, archive slices on disk, purge. Those are named in the gate baseline and say why. |
| **R3** | A failed cleanup **is a failure**. Never `catch { }` a temp delete — the swallow is the exact pattern that hid 65.5 GB. |
| **R4** | The baseline **only shrinks**. Removing a line is required when a file is fixed; adding one is a review event. A stale line fails the gate. |
| **R5** | Assert the **relationship**, never a population count (`validation-ssot.md`) — a delta, an envelope, a closed enum. |

### 2. The `data-architecture.md` amendment (the build work)

Add to the Data doc, in its own words:

- **The store has a storage plan.** `RpgStore` can be constructed file-backed (production, the
  default) or in-memory (a named shared-memory DB with one keeper connection per DB). Memory is a
  **test-first** substrate; production is file.
- **The DAL boundary is unchanged.** Both plans keep all SQL inside `FusionRpg.Data`;
  `guard-dal.ps1` still passes. The memory branch is one URI test in `SqliteConnectionFactory`, not a
  second data path.
- **A shared-cache memory DB cannot be opened read-only.** A fact worth recording: the 9 test read-only
  sites became plain opens, and a memory store's archive entry points throw until `archive-target`.

### 3. Cross-linking

- `testing-standard.md` links the gate and the baseline.
- `data-architecture.md` links `testing-standard.md`.
- `AGENTS.md`/`CLAUDE.md` carry the one-line pointer to the standard (local files, already edited).
- The map and the plan link each other and the standard.

### 4. What this deliberately does not do

- Does not restate R1–R5 in `AGENTS.md` — it points, so the standard stays the single source.
- Does not change any behavior (everything else shipped).

## Commands

```powershell
.\scripts\guard-test-substrate.ps1
# Doc consistency
Select-String -Path docs/architecture/data-architecture.md -Pattern 'storage plan|read-only'
```

## Project structure

```
docs/contributing/testing-standard.md   → R1–R5 + gate wiring (shipped)
docs/architecture/data-architecture.md  → + the storage-plan / read-only amendment (this task)
AGENTS.md, CLAUDE.md                    → pointer line (local, shipped)
```

## Code style

Not applicable (documentation). Convention: the standard is imperative and short; the architecture doc
states the **shipped** shape and links the standard rather than repeating it.

## Testing strategy

Documentation, so verification is a read-through plus the gate:

| Concern | Verify |
|---|---|
| R1–R5 present and unambiguous | read `testing-standard.md` |
| The gate enforces R1/R3 mechanically | `guard-test-substrate.ps1` + its planted-violation tests |
| The architecture doc describes the shipped shape | read `data-architecture.md`; no stale claim contradicts the spec |
| `decisions.md` and the architecture doc agree | read both |

No population count is asserted.

## Boundaries

- **Always:** the standard is the single source; the architecture doc describes shipped behavior.
- **Ask first:** changing R1–R5; relocating the standard.
- **Never:** restate the rules in `AGENTS.md`; assert a count; describe behavior that did not ship.

## Success criteria

1. `testing-standard.md` carries R1–R5 and links the gate + baseline.
2. `data-architecture.md` states the storage plan, the unchanged DAL boundary, and the read-only
   limitation; links the standard.
3. `AGENTS.md`/`CLAUDE.md` point at the standard (local).
4. `decisions.md` (T1's row) and `data-architecture.md` agree.
5. `guard-test-substrate.ps1` green.

## Numeric types / Tunables / ActorHub gate

Not applicable — documentation. No magnitude, no tuning key, no stat surface.

## Open questions

None. The standard and gate shipped; the amendment's content is fixed by the specs it documents.

## Gate checklist (DESIGN-GATE §5)

- [x] Subsystem: Data/SQL/schema → `data-architecture.md`, `architecture-map.md` read this session.
- [x] Boundary recorded; drift owner-accepted.
- [x] `decisions.md` — T1 adds the storage-plan row; this module's doc agrees with it.
- [x] Claims cite file:line and the shipped commits (`7183a59e`).
- [x] Verified: the standard, the gate, and the pointer already exist (read this session).
- [x] Constraints tested: the gate's planted-violation and stale-baseline cases (probe).
- [x] DAL boundary restated as unchanged.
- [x] No population-count assertion.
- [x] ActorHub: N/A.
