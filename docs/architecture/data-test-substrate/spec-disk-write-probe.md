# Spec: `disk-write-probe`

**Module id:** `disk-write-probe` · **Program:** [data-test-substrate](../data-test-substrate-map.md) · **Build order:** 4 of 6
**Depends on:** [`test-store-helper`](spec-test-store-helper.md).
**Model calls:** none.

## Objective

Make a regression back to disk **red instead of silent** — the 65.5 GB accumulated invisibly because
nothing ever counted what a test run left behind.

The module has **two halves**:

| Half | What it catches | Status |
|---|---|---|
| **Static gate** — `guard-test-substrate.ps1` | a test *source* that swallows a temp-delete, or builds a store from a temp path | ✅ **shipped `7183a59e`** |
| **Runtime alarm** — `test-substrate-leak-alarm.ps1` | a test run that *actually* leaves temp dirs or `rpg-*.sqlite` behind, even if the source pattern changed | ⬜ **this spec's build task (T19b)** |

**Success looks like:** the static gate refuses a bad pattern at authoring time; the runtime alarm
refuses a leak that got past it, on every CI run.

## Assumptions I'm making (correct me now)

1. The static half stays as shipped; this spec documents it and specifies the runtime half.
2. The runtime alarm wraps the **CI test step**, not `deploy-play.ps1` (which does not run the suite).
3. On Windows the test temp root is `[System.IO.Path]::GetTempPath()`; test dirs use the
   `fusionrpg-*` prefix (verified: `Path.Combine(Path.GetTempPath(), "fusionrpg-…")` throughout
   Data.Tests).
4. A **delta** is asserted (`before == after` / `0 new`), never an absolute population count
   (`validation-ssot.md`).

## Design

### 1. The static gate (shipped — document, do not rebuild)

`scripts/guard-test-substrate.ps1` scans `tests/**/*.cs` (comment-stripped) and fails on:

- **`swallowed-delete`** — `Directory.Delete(...)` inside an empty or comment-only `catch` block. This
  is the exact shape in 153 of 154 Data.Tests sites.
- **`temp-store`** — a file that both constructs `new RpgStore(` and references `Path.GetTempPath`.

`scripts/test-substrate-baseline.txt` is the ratchet: one line per grandfathered file
(`path : code`); a file whose violation is fixed **must** lose its line, and a stale line fails.
Wired into `deploy-play.ps1` (`:142-144`), `ci.yml` (`:196-197`), and `FusionRpg.Guard.Tests`. The gate's own
tests are self-exempt (they contain the patterns as fixtures) by one named-file exclusion.

### 2. The runtime alarm (the build task)

`scripts/test-substrate-leak-alarm.ps1`:

```powershell
param([string]$Root = …, [scriptblock]$Run, [string]$RepoRoot)
$before = Snapshot          # temp-root dirs matching fusionrpg-*; repo rpg-*.sqlite count outside file-bound dirs
& $Run
$after = Snapshot
if ($after.NewDirs.Count -gt 0 -or $after.NewSqlite.Count -gt 0) { fail with the names }
```

- **Snapshot = a set diff, not a count.** Compare before/after **membership** (the names), so a
  pre-existing dir is not mistaken for a new leak and a count that happens to be equal does not pass
  while the *contents* differ.
- **Fail loudly** and print each surviving directory name and each new `rpg-*.sqlite` path.
- **Scope:** the alarm ignores dirs the file-bound classes legitimately create during a run **only if**
  they are removed on dispose; any survivor fails. It does not need a hardcoded allowlist of names
  (that would be a population constant) — the invariant is "whatever the run created, it cleaned up".

### 3. Wiring

- Add a CI step after `Restore / test (.NET)` (`ci.yml:139`) that runs the alarm around the Data
  suite (the suite that leaked 65.5 GB), then, if cheap, the full set.
- **`ci.yml` is shared** with session `cold-process-test-build-20260912-e5b1` — the edit is
  owner-coordinated (a single added step), not concurrent.

### 4. What this deliberately does not do

- Does not migrate tests (that is `store-test-migration`).
- Does not fix the leak (the helper + migration do); it only **detects** a regression.
- Does not assert a fixed total of temp dirs or tests.

## Commands

```powershell
# Static gate
.\scripts\guard-test-substrate.ps1
# Runtime alarm (around a focused run while developing)
.\scripts\test-substrate-leak-alarm.ps1 -Run { dotnet test tests/FusionRpg.Data.Tests -c Release --blame-hang }
```

## Project structure

```
scripts/guard-test-substrate.ps1        → static gate (shipped)
scripts/test-substrate-baseline.txt     → the ratchet (shipped)
scripts/test-substrate-leak-alarm.ps1   → runtime alarm (NEW)
.github/workflows/ci.yml                → one added step (shared; coordinate)
tests/FusionRpg.Guard.Tests/TestSubstrateGuardTests.cs → static gate's tests (shipped)
```

## Code style

```powershell
# Set diff, never a count.
$new = $afterDirs | Where-Object { $beforeDirs -notcontains $_ }
if ($new.Count -gt 0) { throw "test run leaked temp dirs: $($new -join ', ')" }
```

## Testing strategy

The alarm is proven by **planting** a leak, exactly as the static gate's tests do:

| Concern | Test |
|---|---|
| Alarm passes on the clean (post-migration) tree | run around a focused suite |
| Alarm **fails** on a planted leaking store test | plant, run, assert non-zero, remove |
| Alarm **fails** on a planted `rpg-*.sqlite` left behind | plant, run, assert non-zero |
| The static gate is unchanged and still passes | `TestSubstrateGuardTests` |

**Assert the delta, never a count** (`validation-ssot.md`).

## Boundaries

- **Always:** set-diff semantics; fail on any survivor; prove the alarm by planting a leak.
- **Ask first:** any change to the shipped static gate's rules; a second CI step.
- **Never:** assert a fixed dir total; allowlist a survivor by name; weaken the static gate.

## Success criteria

1. `test-substrate-leak-alarm.ps1` passes on a clean tree and **fails** on a planted leak (both a
   temp dir and an `rpg-*.sqlite`).
2. It is wired into CI after the test step (owner-coordinated on shared `ci.yml`).
3. The static gate keeps passing unchanged; its tests stay green.
4. No population count is asserted anywhere.

## Numeric types / Tunables / ActorHub gate

Not applicable — CI tooling. No magnitude, no tuning key, no stat surface.

## Open questions

None. The static half shipped; the runtime half's contract is fixed by the ideal
(`../data-test-substrate-ideal.md` Tier 4) and the delta rule.

## Gate checklist (DESIGN-GATE §5)

- [x] Subsystem: Data/test infrastructure → docs read this session.
- [x] Boundary recorded; `ci.yml` shared with another session and flagged.
- [x] No decisions.md row owed.
- [x] Claims cite file:line (`ci.yml:196-197`, `deploy-play.ps1:142-144`, the 153/154 scan).
- [x] Verified against code: the shipped gate is static-only (read this session).
- [x] Constraints tested: the gate fails on a planted violation and on a stale baseline (spike/probe).
- [x] No DAL change.
- [x] Delta, not count — explicitly.
- [x] ActorHub: N/A.
