# Spec: element-extension (V1 wave 1)

Module id `element-extension` in the [demon system map](../demon-system-map.md). Prerequisite for all demon typing. **Touches a design-locked area** — ships with a decisions.md amendment, and nothing else in the demon program starts until its golden tests are green.

## Objective

Extend the ElementHub roster from `{omni, fire, ice, air, earth}` to also carry **`light`** and **`dark`**, so demon species can be typed with the vision's celestial/infernal identity. `void`/`chaos` from the vision are explicitly **not** elements — they become rare traits later.

Success looks like: `ElementHub` resolves matchups over 6 concrete elements with unchanged behavior for the existing 4, and the derived-stat catalog exposes the new combat channels without disturbing any existing channel id.

## Design (locked on approval of this spec)

- **Roster:** `omni | fire | ice | air | earth | light | dark`. `omni` remains non-slottable; actors still carry 0–2 concrete types; `primary == secondary` still invalid; unknown ids still reject.
- **Matchups:** the classic ring is untouched (`fire → ice → earth → air → fire`). New rules: `light` STR vs `dark` and `dark` STR vs `light` (mutual counter); `light`/`dark` are NEU vs all four ring elements and vice versa. No STAB, same as today.
- **Math:** unchanged — additive base-damage share, `MatchupShareK = 0.25`, dual-type via multiplier conversion (STR 1.25 / WEK 0.75 / NEU 1.0, multiply, convert back). The mutual counter means light-vs-light/dark-vs-dark stays NEU; light-vs-(dark/x) dual types compose exactly like ring dual types.
- **Channels:** combat families extend from 8×5 = 40 to 8×7 = **56** channels (`combat.*.{omni,fire,ice,air,earth,light,dark}`). Omni stays additive-only. Registration lives where it lives today (Actor Hub catalog); Element Hub keeps element semantics.
- **Contract:** additive change — no existing channel id, ring entry, or default changes. A new **`ElementRoster`** constant (concrete list + omni; today no such constant exists) becomes the only legal way to iterate elements.

### Hand-maintained surfaces (the actual risk — from the 2026-08-21 review; code is verified free of literal-5 loops)

1. `CombatDerivedReader` — **eight** element→channel switch maps ending in a throwing default: missing one compiles clean and then **throws on the injector hot path** at first light/dark use. All eight must be extended, and the exhaustiveness test below walks them.
2. `ElementRingMatrix` — the `_ => Neutral` default is **fail-open**: forgetting the `(light, dark)` pairs silently drops the mutual counter. The golden matrix must be generated from `ElementRoster` (exhaustive by construction), not hand-listed.
3. `ElementFxPalette` — hardcodes the four ring elements twice: light/dark hits would render **white**, and `Concrete()` filters them out of hybrid VFX. In scope: palette colors for light/dark + roster-driven `Concrete()`.
4. Element name parsing uses `Enum.TryParse`, which accepts numeric strings (`"42"` parses; after extension `"4"`/`"5"` would silently mean light/dark). Replace with an explicit name→id map that rejects digits; add a rejection golden test.
5. `DerivedStatRegistryTests` asserts `Count == 40` — expected churn; see success criteria.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests          # incl. new matrix golden tests
.\scripts\guard-single-writer.ps1; .\scripts\guard-funnel-delta.ps1
```

## Structure

```
src/FusionRpg.Core/Combat/Element/   → ElementHub.cs, ElementRingMatrix.cs (extend)
src/FusionRpg.Core/Stats/Derived/    → DerivedStatChannels.cs (56-channel expansion)
tests/FusionRpg.Core.Tests/Combat/   → new ElementMatrixLightDarkTests.cs (golden 6×6 + dual-type)
docs/architecture/decisions.md       → amendment row (ring extension)
docs/architecture/element-hub-ssot.md → §roster/§matrix update
```

## Testing strategy

Golden-table tests: full 6×6 single-type matrix **generated from `ElementRoster`** (exhaustive by construction — defeats the fail-open matrix default), the light/dark mutual-counter pairs, representative dual-type compositions, and a regression assert that all pre-existing 40 channels and 4-element matchups produce byte-identical results. Plus an **exhaustiveness walk**: `ElementRoster × 8 channel families` through `CombatDerivedReader`, the ring matrix, and `ElementFxPalette` — every combination resolves without throwing and without falling through to a ring-only default. Plus the numeric-string rejection test for element parsing.

## Boundaries

- **Always:** additive only; regression-lock existing behavior; update the SSOT doc + decisions.md in the same change.
- **Ask first:** any change to `MatchupShareK`, STAB, or ring order; adding a 7th concrete element.
- **Never:** touch Funnel/Writer paths; break the 40 existing channel ids; per-element probability scales (still deferred).

## Success criteria

1. All existing Core tests pass with **only roster-size assertions updated** (the `Count == 40` → 56 test is the sole permitted change). 2. Golden + exhaustiveness tests cover the full extended matrix and all four hand-maintained surfaces. 3. `decisions.md` amendment recorded. 4. Element-hub SSOT doc updated. 5. Unknown-id rejection holds for bogus names **and numeric strings**. 6. `ElementRoster` exists and the four surfaces consume it.
