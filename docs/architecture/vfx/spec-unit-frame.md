# Spec: VFX UnitFrame — shared unit anchor and scale

**Status:** Implemented (2026-08-30)  
**Parent:** [vfx-ssot.md](../vfx-ssot.md) §9.1  
**Consumers:** `UnitFrameResolver`, `ShieldBarPool`, `VfxDirector` sustained/floaters/bursts

## Problem

World VFX duplicated three incompatible anchor paths:

- `LawnCoords.BodyWorld` — pivot X + lane Y
- `ShieldBarPool.BarAnchorWorld` — bounds X only (private)
- `VfxDirector.EstimateCellSize` — cell span only

New primitives would copy one path and drift. Sustained auras read small and bottom-left because pivot X and cell span were used without sprite bounds.

## Solution

One resolution step after `AnchorResolver`:

```text
ptr → AnchorResolver → Transform
Transform → UnitFrameResolver → VfxUnitFrame (cached per instance id per frame)
VfxUnitFrame → World(kind) + Span(recipeScale)
```

### Core (pure)

| Type | Role |
|---|---|
| `VfxAnchorKind` | `Feet`, `Body`, `Crown`, `Cell` |
| `VfxAnchorCatalog` | `VfxAuraStyle` → kind (identity batches 1–5) |
| `VfxSpanMath` | `Max(cellSpan, boundsMax) × spanScale × recipeScale` |
| `VfxUnitFrameMath` | Pure X/Y pick from frame fields |

### Injector

| Type | Role |
|---|---|
| `VfxUnitFrame` | Snapshot; `World(kind)`, `Span()`, `CellSize` |
| `UnitFrameResolver` | Bounds read + lane Y + cell span; per-frame cache |

### Anchor kinds

| Kind | Y baseline | Typical styles |
|---|---|---|
| `Feet` | Lane ground line | DoT drip, pact feet pulse |
| `Body` | Bounds center (fallback lane + half cell) | Orbit, crackle, spore |
| `Crown` | Upper sprite band (+ aura grammar bias) | Command halo, rise sparkle, markers |
| `Cell` | Cell center | Cell-anchored bursts |

X is always `bounds.center.x` when bounds exist; else pivot X.

### Tunables (`data/tuning/vfx.v3.json`)

- `sustained.spanScale` — global sustained aura scale multiplier (default 1.5)
- `render.sortOffsetAboveUnit` — layers particles at or above the host sprite's sorting order
- `render.sustainedWorldYOffset` — global world-Y lift for body/feet sustained auras (Crown excluded)
- `render.markerYOffsetScale` / `render.markerSizeScale` — badge lift and size as fractions of span
- `render.markerGlowStrength` — outer halo weight on procedurally generated marker textures

Per-recipe `SizeScale` in `VfxCatalog` still applies on top of span math.

## Rules for new VFX

1. **Do not** call `BodyWorld` or read `Renderer.bounds` inside primitives.
2. Resolve `VfxUnitFrame` once per host per tick via `UnitFrameResolver.Resolve(transform)` — infers board cell from plant col/row or zombie col/row (not cheat spawn cell).
3. Pick `VfxAnchorKind` from catalog or recipe (future `AnchorKind` field on `VfxPrimitiveSpec`).
4. Scale via `frame.Span(recipeSizeScale)` — not local cell math.

## Extension point

`VfxPrimitiveSpec.AnchorKind` (optional override) deferred until a non-aura primitive needs a kind outside `VfxAnchorCatalog`.

## Verification

- `tests/FusionRpg.Core.Tests/Vfx/UnitFrameTests.cs` — pure math + catalog map
- LIVE: sustained auras centered on sprite X, scaled to large zombies; batch-5 feet vs crown separation preserved
