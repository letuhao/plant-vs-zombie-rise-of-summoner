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
- **Contract:** additive change — no existing channel id, ring entry, or default changes. Consumers that iterate the roster must use the roster constant, not a literal 5.

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

Golden-table tests: full 6×6 single-type matrix (36 cases), the light/dark mutual-counter pairs, representative dual-type compositions (light/fire defender vs dark attacker, etc.), and a regression assert that all pre-existing 40 channels and 4-element matchups produce byte-identical results.

## Boundaries

- **Always:** additive only; regression-lock existing behavior; update the SSOT doc + decisions.md in the same change.
- **Ask first:** any change to `MatchupShareK`, STAB, or ring order; adding a 7th concrete element.
- **Never:** touch Funnel/Writer paths; break the 40 existing channel ids; per-element probability scales (still deferred).

## Success criteria

1. All existing Core tests pass unchanged. 2. New golden tests cover the full extended matrix. 3. `decisions.md` amendment recorded. 4. Element-hub SSOT doc updated. 5. Unknown-id rejection still holds for a bogus element.
