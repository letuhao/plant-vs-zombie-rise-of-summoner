# Spec: authoring-and-validation (E14a + E14b)

Modules **E14a** and **E14b** in the [atom effect map](../effect-atom-map.md).

**The module splits.** E11 must import seed rows, and the importer was three positions later — so Checkpoint D was unreachable as sequenced.

| | Owns | Depends on | Position |
|---|---|---|---|
| **E14a** | seed/migration file format, `tools/AtomImporter`, schema-validation wiring, all-or-nothing import | E5, E8 | **before E11** |
| **E14b** | budget validation, power drift, content lint, the one-row claim test | E11, E9 | after E9 |

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

## Objective

Make authoring a new effect actually cost **one row**, and make every guarantee this program claimed into a test that fails. Without this module, "data-driven" is a schema with no way to put data in it and no way to know the data is sane.

## Design (locked on approval)

### The authoring path — files, not an editor

Seed and migration files under `data/seed/atoms/` and `data/seed/containers/`, loaded by the same validator the runtime uses. **No runtime loader, no external content, no editor in wave 1** — an editor arrives only if a spec asks for one.

Files are the right first surface because they diff, review, and version like code, while the *content* still lives in rows at runtime. A designer edits a file, the importer validates and upserts, the content hash moves, and the change is visible in review.

### The four validations

**1. Schema validation (E14a)** — every row through E1/E2/E3/E4/E5. **Import is all-or-nothing**: one bad row and nothing is imported, because a partial import produces a content hash for a state nobody authored. Per-row rejection at *load* (E4/E5) is a different phase — defence in depth against a database edited outside the importer.

**2. Budget validation (E14b)** — rarity R may spend at most N power, looked up by the container's `rarity` FK. A content test enumerates every container, sums its atoms' vectors (E9), and **fails naming the offender**. This is the *only* role the budget plays: it never drives generation (E5 does).

*Honest caveat:* at E14b the only containers are E11's migration output — legacy effects and one trait, none of which carry a rarity. The test therefore enumerates almost nothing until real item content exists, and it must **say so in its output** rather than passing silently and looking green.

**3. Power drift (E14b)** — recompute every atom's power and compare to its stored `power_json`. Beyond **±25% per category, floor 1 point** without a `power_note` is a failure (definitions §7). This is what keeps "computed base + stored override" honest rather than decorative.

**4. Runtime support (E14b)** — a **lint**, not a validation. A container has no "claimed runtime" column, and the same container is legitimately bindable on the lawn and rejected in battle — that is the point of the living matrix. So E14b *reports* containers whose atoms have no consumer in any runtime, and the real check stays at bind time (E6). The earlier wording required a column E5 does not define.

### The Checkpoint D claim, as a test

> **A new effect using an existing kind costs one row, no build.**

`OneRowClaimTests` lives in **E11**, not here — Checkpoint D is where the claim is made, and E14b is five modules later. E14b only re-runs it as a regression.

"No rebuild of Core" is **not assertable from inside a process that already loaded Core**, so the test does not pretend to: it asserts the behavioural half, and the no-rebuild half is enforced by the test project referencing no new source. Saying that plainly is the alternative to quietly relaxing it.

### Content lint — the cheap checks that catch real mistakes

| Lint | Why |
|---|---|
| **(family, variant)** with a tier gap (1, 2, 4) | almost always a typo. Keyed on family+variant, not family — `elemental_power` holds 7 variants × 5 tiers, so a family-level check would hide a real gap in `ice` and invent false ones |
| Tier whose range does not exceed the tier below | a tier that is not stronger is not a tier |
| Two families writing the same channel with the same op | duplicate affix under two names |
| A pool group with one member | the group does nothing; likely a mistake |
| An atom no container references | dead content — legal, but worth surfacing |
| **A tier band copied between channel families** | the units trap (E2): `+10 hp` and `+10 fire power` differ by an order of magnitude |

Lints **warn**; validations **fail**. Keeping the two separate stops lint noise from blocking a legitimate edge case.

## Commands

```powershell
dotnet run --project tools\AtomImporter -- data\seed
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Authoring|Budget|PowerDrift"
```

## Structure

```
data/seed/atoms/*.json                                    (authored content)
data/seed/containers/*.json
tools/AtomImporter/                                       (new — validate + upsert + hash)
   !! guard-dal.ps1 scans only src/, so tools/ is a blind spot: the importer MUST call
      RpgStore upserts (E4/E5) and open no connection of its own. The guard cannot enforce it here.
   !! guard-dal.ps1 scans only src/, so tools/ is a blind spot: the importer MUST call
      RpgStore upserts (E4/E5) and open no connection of its own. The guard cannot enforce it here.
tests/FusionRpg.Core.Tests/Atoms/BudgetValidationTests.cs
tests/FusionRpg.Core.Tests/Atoms/PowerDriftTests.cs
tests/FusionRpg.Core.Tests/Atoms/ContentLintTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| Seed file with one malformed row | import fails, names the row, **imports nothing** (E14a) |
| Duplicate `atom_id` across two seed files | import fails — last-write-wins would make content order-dependent on filesystem iteration |
| Container over its rarity budget | budget test fails, names the container and the overage |
| Budget test with no rarity-bearing containers | **reports "0 containers evaluated"** — never a silent green |
| Atom whose stored power drifts, no note | drift test fails |
| Same, with a note | reported, not failed |
| Container whose atoms have no consumer in any runtime | **lint warning**, not a failure |
| **Add one row, no rebuild** | effect grantable and firing — the Checkpoint D claim |
| Import twice | idempotent; content hash unchanged the second time |
| Lint findings | reported, never blocking |

## Boundaries

**Always:** validate before upsert; fail the import rather than importing partially; keep lints non-blocking; keep the one-row test green.

**Ask first:** adding a validation that can fail existing content; building an editor.

**Never:** a runtime content loader; external or user-supplied content; importing a file that failed validation; letting the budget influence which atoms roll.
