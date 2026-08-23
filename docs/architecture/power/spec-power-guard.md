# Spec: power-guard

Module **`power-guard`**, wave 4 in the [power map](../power-map.md). Depends on **waves 1-3**.

> **Reads [ssot-power-scale.md](ssot-power-scale.md)** — the parent SSOT. Where this spec and the
> SSOT disagree, **the SSOT wins**.

**Status:** Draft — pending owner review. No build authorized.

---

## 1. Objective

Make PS-7 and the closed inventory mechanically true, so the drift cannot return.

Without this, the SSOT decays into advice within two months — which is exactly how three
incompatible curves came to ship (SSOT §0). This module is the difference between a document and a
constraint.

## 2. Design

Follows the shape of the four existing guards (`guard-dal.ps1`, `guard-single-writer.ps1`,
`guard-funnel-delta.ps1`, `guard-secondary-no-unity.ps1`): a PowerShell script taking `-Root` and an
allowlist, `$ErrorActionPreference = "Stop"`, regex patterns over `src/`, non-zero exit with the
offending `file:line`. Wired into `Guard.Tests` and `deploy-play.ps1` like its neighbours.

### 2.1 The four checks

| Check | Fails when | Rationale |
|---|---|---|
| **G1 — no literal curve** | a numeric literal appears in `Core/Power` outside `PowerTuningLoader` | PS-7: every constant is data |
| **G2 — no private `f(level)`** | a file outside `Core/Power` computes a magnitude from a raw level | The core defect. Catches the next `BaseHp(int level) => 80 + 30 * level` |
| **G3 — no new curve** | an `f(level)`-shaped declaration appears that `inventory.json` does not list | **Scoped by audit F6.** A scanner cannot judge what is "power-shaped" in general — that is the A3 triage's job. It *can* diff against a known baseline, which catches the real regression: someone adding a curve |
| **G4 — pin holds** | `Value(pinIndex) != pinValue` for any `data/tuning/power-scale.v*.json` | Protects the item corpus from every past and future tuning version |

### 2.2 G2 is the hard one, and it needs an allowlist

A regex cannot decide whether `80 + 30 * level` is a magnitude or a coincidence. The heuristic:
flag a method whose parameter is named `level`/`lvl`/`index` **and** whose body contains arithmetic
on it **and** which returns a numeric type.

That will over-match. Two mitigations, both borrowed from `guard-dal.ps1`, which ships with an
allowlist parameter and an empty default:

- **Explicit allowlist**, empty at first, each entry carrying a one-line reason next to it.
- **Fail closed.** An unrecognised match is a failure, not a warning. A guard that warns is a lint,
  and lints get ignored.

> **The known blind spot, stated rather than discovered later:** `guard-dal.ps1` scans only `src/`,
> so `tools/` is invisible to it ([DESIGN-GATE.md](../../DESIGN-GATE.md) §1). This guard inherits
> that gap. `tools/seedsmith` authors magnitudes and is **not** covered. Fixing it is out of scope
> here; pretending it is covered would be worse than naming it.

### 2.3 G3 needs the inventory to be machine-readable

SSOT §10's table is prose. G3 needs a list it can compare against, so this module adds
`docs/architecture/power/inventory.json` — generated from the table, reviewed with it, and the thing
G3 diffs. If the table and the JSON disagree, that is itself a failure.

## 3. Commands

```powershell
.\scripts\guard-power.ps1                    # standalone
dotnet test tests\FusionRpg.Guard.Tests       # wired like the other four
.\scripts\deploy-play.ps1                    # runs all five
```

## 4. Structure

```
scripts/guard-power.ps1                                    (new)
docs/architecture/power/inventory.json                     (new — machine-readable §10)
tests/FusionRpg.Guard.Tests/PowerGuardTests.cs             (new)
scripts/deploy-play.ps1                                    (edit — add the fifth guard)
.github/workflows/ci.yml                                   (edit — add the fifth guard)
```

## 5. Testing strategy

Mirrors `DalGuardTests` — plant a violation, assert non-zero exit and the right message.

| Case | Expect |
|---|---|
| Clean tree | exit 0 on `main` |
| G1 planted | a literal in `Core/Power` -> exit 1, `POWER GUARD FAILED`, file:line |
| G2 planted | `int Foo(int level) => 5 + 3 * level;` in `Core/Battle` -> exit 1 |
| G2 allowlist | the same file allowlisted -> exit 0 |
| G3 planted | a scale absent from `inventory.json` -> exit 1 |
| G4 planted | a tuning file whose pin is broken -> exit 1 naming the version |
| **False-positive survey** | run G2 against the whole tree pre-migration and record every hit. Any hit that is not a real violation becomes an allowlist entry **with a reason**, before the guard is armed |
| Inventory sync | `inventory.json` and SSOT §10 list the same rows |

## 6. Boundaries

**Always** — fail closed · report `file:line` · keep the allowlist empty by default and reasoned when
not · run in CI and `deploy-play.ps1`.

**Ask first** — adding an allowlist entry · widening a pattern · extending the scan to `tools/`
(a real change of scope).

**Never** — warn instead of fail · allowlist a whole directory · claim `tools/` coverage the scan
does not have.

## 7. Success criteria

1. All four checks pass clean and fail on planted violations.
2. False-positive survey complete; every allowlist entry carries a reason.
3. Wired into `Guard.Tests`, `deploy-play.ps1`, and CI.
4. `inventory.json` matches SSOT §10.

## 8. Open

**None — decided 2026-08-23.** The question was whether the scan should cover `tools/`.

It should not, and the reason is sharper than "it is a bigger change": **`tools/` holds no C#.**
`--paths src tools` returns identical counts because `tools/seedsmith` is Python. A C# source guard
cannot scan it, and pretending otherwise would be the false coverage this spec's §2.2 already warns
about.

The real concern behind the question stands and is **reassigned**: seedsmith *authors magnitudes*,
and nothing cross-checks its authored values against the ladder. That is a `content-scale` obligation
(the corpus must remain scale-free), not a guard one, and seedsmith already owns validation of its
own numbers via `numerics`. Named in `spec-content-scale.md` §2.3 rather than left as a gap here.
