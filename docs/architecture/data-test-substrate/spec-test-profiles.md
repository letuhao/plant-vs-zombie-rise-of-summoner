# Spec: `test-profiles`

**Module id:** `test-profiles` · **Program:** [data-test-substrate](../data-test-substrate-map.md) · **Build order:** 7 of 7
**Depends on:** [`substrate-standard`](spec-substrate-standard.md) (the rule that disk is used only when a file is the subject).
**Model calls:** none.

## Objective

Make the **default** test run quiet: no writes to the developer's SSD, and no multi-minute tests. Let
the tests whose subject genuinely *is* a file run where the disk is cheap — CI, nightly, and the
release gate — instead of on every local iteration.

The owner's question that produced this module: *"did we really need disk-writing tests? … during
development, avoid tests that are genuinely harmful."* The measured answer (see the map's module-7
rationale) is that a few file-semantics tests must exist, but they should not run by default.

**Success looks like:** `dotnet test` with no filter runs the whole suite **minus** the disk-semantics
and heavy tests; one documented command runs everything; CI and the release gate run everything.

## Assumptions I'm making (correct me now)

1. **xUnit `[Trait("Category", …)]` is the mechanism** — already used in this repo (`Category=BalanceGuard`,
   CI `--filter "Category=BalanceGuard"` at `ci.yml:140`). No new framework.
2. **A negative filter includes uncategorized tests** — verified this session:
   `--filter "Category!=DiskSemantics"` ran all 1,288. So only the tests being *excluded* need a trait;
   the other ~1,270 are untouched. (`Category!=A&Category!=B` was also verified to run all 1,288.)
3. **Cloud CI disk is ephemeral**, so running the disk tests there costs the owner nothing.
4. **The profile changes only *what runs where*.** It never changes what a test asserts, and it never
   deletes coverage — a tagged test still runs in CI, nightly, and at release.

## Design

### 1. Two categories

| Category | Meaning | Members (initial) |
|---|---|---|
| **`DiskSemantics`** | "the file is the thing under test" — WAL mode, legacy migration + sidecar, archive slices/purge, real-corpus cold-process runs, and any test that must write a fixture tree to disk | the 5 file-bound classes (`RpgStoreDalSmokeTests`, `RpgStoreSmokeTests`, `LegacyMonoMigratorTests`, `ColdArchiveCompactionTests`, `StoragePurgeTests`), `CreatureSpeciesImportCliTests`, and the real-corpus fixture classes (`AtomImportTests`' disk case, `SeedImportRunnerTests`, `PassiveTreeImportRunnerTests`) |
| **`Heavy`** | ≥20s or cold-process or long-run, with no disk requirement | `ActionUnlockGrantWiringTests` (110s), `TreeStateVolumeTests.Two_thousand_actors…` (56s), `ContentHashStoreTests` long cases, `ItemSetStoreTests` corpus round-trips, `ActionStockSpendStoreTests`, `DelveScopeTests` long cases |

**A test is tagged at the class level when every method qualifies, at the method level when only some
do** (the repo already mixes both — `BalanceGuard` is applied per-method in one file and documents why).

**The rule for which category, when both could apply:** `DiskSemantics` wins — it is the stronger
statement ("this test is *about* the disk"), and excluding either one removes it from the default
profile, so the default run is unaffected. Prefer `Heavy` only for a test that is slow *without*
touching disk.

### 2. Four profiles

| Profile | Where | Command |
|---|---|---|
| **default** | local dev, agents, `deploy-play.ps1` | `dotnet test <proj> --filter "Category!=DiskSemantics&Category!=Heavy"` |
| **full** | CI pull-request | `dotnet test <proj>` (no filter) |
| **gate** | `release.yml` tags | `dotnet test <proj>` (no filter), required |
| **nightly** | new `schedule:` workflow | `dotnet test <proj>` (no filter) |

**Where the default is applied.** The exclusion is documented in
[`testing-standard.md`](../../contributing/testing-standard.md) and driven from **one** place so the
default cannot drift: a small `scripts/test-fast.ps1` (mirroring `deploy-play.ps1`'s guard style) that
both the developer and any agent run. `deploy-play.ps1` calls it. CI keeps calling `dotnet test`
directly with **no** filter, so CI is `full` by construction and cannot accidentally inherit the dev
default.

### 3. Why not a `.runsettings` filter

`RunSettingsFilePath` (already wired by `Directory.Build.props`) carries session **timeouts**, not a
test-category filter that CI can override cleanly per invocation. A category filter belongs on the
`dotnet test` command line, which is also what makes the four profiles visible and greppable. We keep
`test.runsettings` exactly as it is.

### 4. The guards stay on `full`

`guard-test-substrate.ps1` (static) and the `disk-write-probe` runtime alarm (T19b) must run against
the **full** profile — the default profile *intentionally* writes the file-bound dirs, so an alarm run
on the default profile would either false-positive or need an ever-growing allowlist. Stated in the
spec so no future session "optimizes" the guards onto the fast profile.

### 5. What this deliberately does not do

- Does not delete or weaken any test (R5/`validation-ssot.md`).
- Does not stop the migration (T18b–T18f continue: a migratable test is migrated so it writes nothing
  in *every* profile).
- Does not change `src/`, `trait`s on production code, or `test.runsettings`.
- Does not make the default profile the only gate — CI/nightly/release all run `full`.

## Commands

```powershell
# default (quiet) — the dev/agent loop
.\scripts\test-fast.ps1 -Project tests/FusionRpg.Data.Tests

# full — CI/gate/nightly form
dotnet test tests/FusionRpg.Data.Tests/FusionRpg.Data.Tests.csproj -c Release --blame-hang --blame-hang-timeout 15min

# prove the categories exist and are excluded
dotnet test tests/FusionRpg.Data.Tests/FusionRpg.Data.Tests.csproj -c Release --filter "Category=DiskSemantics" -v q
```

## Project structure

```
scripts/test-fast.ps1                     → new: the default-profile runner (one place for the filter)
.github/workflows/ci.yml                  → unchanged behaviour (full); a comment naming the profiles
.github/workflows/nightly.yml             → new: schedule-triggered full run (catches a disk regression within a day)
.github/workflows/release.yml             → full + required gate
docs/contributing/testing-standard.md     → the profile table + the "guards run on full" rule
tests/**/*.cs                             → [Trait("Category", …)] on the excluded tests only
```

## Code style

```csharp
// Class-level: every method is disk-semantics.
[Trait("Category", "DiskSemantics")]
public class RpgStoreDalSmokeTests { … }

// Method-level: only this case writes a real seed tree.
[Trait("Category", "DiskSemantics")]
[Fact]
public void A_seed_tree_on_disk_imports_end_to_end() { … }
```

## Testing strategy

| Concern | Verify |
|---|---|
| The default profile excludes the tagged tests | `test-fast.ps1` run count < full run count, and the difference equals the tagged count |
| The default profile writes **no** disk | the T19b runtime alarm run around `test-fast.ps1` reports **0** new `fusionrpg-*` dirs |
| `full` still runs everything | CI is unfiltered; a local unfiltered run equals the pre-change 1,288 |
| Guards are unaffected | `guard-test-substrate.ps1` green; the alarm runs on `full` |
| Nothing was deleted or weakened | per-file `Assert.` counts unchanged for every tagged file |

**Assert the relationship, never a population count** (`validation-ssot.md`): the check is
"default count = full count − tagged count", computed at run time, never a pinned total.

## Boundaries

- **Always:** tag only what is excluded; keep `full` unfiltered in CI; state the categories in the standard.
- **Ask first:** tagging a test that is neither disk-semantic nor ≥20s; changing the profile set.
- **Never:** delete or weaken a test to shrink the default run; run the guards on the default profile;
  move a file-bound test into memory just to un-tag it (that deletes coverage — `substrate-standard` R2).

## Success criteria

1. `scripts/test-fast.ps1` runs the suite minus `DiskSemantics` and `Heavy`, and **writes no disk**.
2. `full` (CI/`release.yml`/nightly) runs everything; CI is unfiltered.
3. The two categories exist and are documented in `testing-standard.md` with the profile table.
4. The guards run only on `full`.
5. No test is deleted, and every tagged file's assertion count is unchanged.
6. A nightly workflow exists so a disk regression is caught within a day.

## Numeric types / Tunables / ActorHub gate

Not applicable — test tooling. No magnitude, no `data/tuning/` key, no stat surface.

## Open questions

None. Owner decided 2026-09-12: extend with module 7, keep `archive-target`, two categories, fix the
124s test now (done).

## Gate checklist (DESIGN-GATE §5)

- [x] Subsystem: Data/test infrastructure → docs read this session.
- [x] Boundary recorded; drift owner-accepted.
- [x] No `decisions.md` row owed (test tooling).
- [x] Claims cite file:line (`ci.yml:140`; `Directory.Build.props` RunSettingsFilePath).
- [x] Verified this session: negative filters include uncategorized tests (both single and combined);
      `Category=BalanceGuard` returns 22.
- [x] Measured, not assumed: 102 baseline files still write disk; 16 tests ≥20s; the 124.6s outlier.
- [x] No DAL change; no `src` change.
- [x] Relationship assertion, never a count.
- [x] ActorHub: N/A.
- [x] Does not weaken any guard or delete coverage.
