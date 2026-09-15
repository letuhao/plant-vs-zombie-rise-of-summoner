# Spec: `combat-numerics`

**Program:** `lawn-combat-wire` · **Map:** [../lawn-combat-wire-map.md](../lawn-combat-wire-map.md)
**Depends on:** — (leaf, parallelisable)

---

> **Amended 2026-09-15 — owner ruling (lawn-combat-wire `L-N12`): floating-point is allowed.** Banning
> `float`/`double` from the combat interior was the wrong requirement. Probabilities, ratios, multipliers,
> mitigation curves and derived-channel reads may be `double`. What still holds: the magnitude that leaves
> the calculator for the Funnel is a `checked` `long` (overflow throws, never wraps), integer per-mille
> arithmetic divides by 1000 last, and the Unity-boundary narrowing throws or reports. The rows below
> that demand an integer-only interior or a no-`double` source scan are superseded by this note and are
> kept only as the history of the original audit.

## Objective

The overlay damage path violates this repo's own numeric rules end to end. This feature routes far
more traffic through it, so the violations must be fixed before they are amplified — not after.

| Violation | Where | Rule broken |
|---|---|---|
| `double BaseOverlayDamage`; whole interior in `double` | `OverlayCombatCalculator.cs:9` | ~~`long` for any magnitude, never `float`/`double` — `double` is non-deterministic across runtimes and must never sit in a hashed or persisted path~~ *(superseded 2026-09-15, see the amendment above: floating-point allowed; a hashed `double` records the platform stamp)* |
| `combinedMult = 1.0` | `ElementHub.cs:17` | same |
| divides by `1000.0` **before** multiplying | `OverlayCombatCalculator.cs:252` | divide by 1000 **last**, exactly once — per-mille intermediates are 1000× closer to the ceiling |
| exit `(long)Math.Round(...)` **unchecked** | `OverlayCombatCalculator.cs:297` | overflow **throws**, never wraps |
| `ClampToInt32` silently saturates at the Unity boundary | `EntityStatWriter.cs:191` | a bound on a magnitude is **derived and throws**, never clamps silently — a clamp turns "your gear stopped mattering" into a bug with no symptom |

`python scripts/audit-overflow.py --targets A3` matches `int` declarations and **structurally cannot
see any of this**, so none of it is on the existing audit's radar.

Success (amended 2026-09-15): the magnitude handed to the Funnel is a checked `long`, integer per-mille
arithmetic divides last, and an out-of-range magnitude throws rather than saturating. Floating-point is
allowed inside the calculation (see the amendment note above).

## Tech stack

`FusionRpg.Core` — `OverlayCombatCalculator`, `ElementHub`, `OverlayCombatMath`. Unity-free.
Boundary clamp lives in `FusionRpg.Injector/Stats/EntityStatWriter.cs`.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~OverlayCombat|ElementHub"
python scripts/audit-overflow.py
.\scripts\guard-single-writer.ps1
```

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs` | `long`/per-mille interior; divide last; checked exit |
| `src/FusionRpg.Core/Combat/Element/ElementHub.cs` | Per-mille multiplier instead of `double` |
| `src/FusionRpg.Injector/Stats/EntityStatWriter.cs` | The Unity-boundary bound: decide throw vs documented structural clamp |

## Code style

Per-mille discipline, the same shape the repo already uses elsewhere:

```csharp
// multiply in long, divide by 1000 exactly once, at the end
checked
{
    long scaled = attackerPower * matchupShareMilli;   // widen before multiplying
    long delta  = scaled / 1000L;                      // divide LAST
}
```

Never `(long)(a * b)` — the cast binds to the *result*, so the multiply has already overflowed.

**The Unity boundary is the one legitimate narrowing**, because `thePlantHealth` genuinely is an
`int` field in the host game. That clamp may stay **only** if it is documented as a structural limit
of the host (the caps rule's stated exemption) and it **reports** rather than silently saturating —
a magnitude that exceeds `int` at the boundary is a real gameplay event, not a rounding detail.

## Testing strategy

| Level | Cases |
|---|---|
| Core unit | Golden values unchanged for ordinary magnitudes — this is a representation change, not a balance change |
| Core unit | Determinism: same inputs produce byte-identical output |
| Core unit | ~~**No `double`/`float` remains**, asserted by a source scan test~~ — **superseded 2026-09-15 (owner ruling, `L-N12`): floating-point is allowed; no floating-point ban or scan test.** |
| Core unit | **D1's prohibition is guarded:** `MergeAppliedCombat` (`ActorHub.cs:89-113`) folds only `progression.bonus.*` and **no `combat.*` channel**. D1 says this must never change, and until now no spec asserted it — this is the test that fails if someone "helpfully" adds omni to the field bridge and double-dips RPG power into one hit |
| Core unit | Overflow: a magnitude past the `long` ceiling **throws**, not wraps |
| Core unit | Divide-last: a case where dividing first and last differ, asserting the last-divide result |
| Boundary | A magnitude exceeding `int` at `EntityStatWriter` produces the documented behaviour (throw or reported clamp), never a silent wrap |
| Audit | `python scripts/audit-overflow.py` clean; note that A3 cannot see `double`, so this module's own tests are the real guard |

## Boundaries

- **Always:** widen before multiplying; divide by 1000 last, exactly once; `checked` on magnitude
  arithmetic.
- **Ask first:** changing any shipped damage number. This is a representation fix — **goldens must not
  move**. If a golden moves, the conversion is wrong, not the golden.
- **Never:** let the magnitude handed to the Funnel wrap or narrow unchecked; silently clamp a magnitude
  without a stated structural reason; "fix" an overflow by widening the clamp.

## Success criteria

- [x] ~~No `double`/`float` on the overlay damage path from input to Funnel delta.~~ Superseded 2026-09-15
      (owner ruling, `L-N12`): floating-point is allowed; the Funnel-bound magnitude is a checked `long`.
- [ ] Division by 1000 happens exactly once, last, with a test that would fail if reordered.
- [ ] Overflow throws; a test asserts it.
- [ ] The Unity-boundary narrowing either throws or reports, and carries a comment naming it a
      structural host limit.
- [ ] **Existing goldens unchanged** — representation change only.
