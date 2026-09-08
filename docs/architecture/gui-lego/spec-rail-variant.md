# Piece: rail-variant

**Program:** gui-lego · **Kind:** chrome · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/rail-variant.html](../../design/gui-lego/pieces/rail-variant.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
Variant tablist

## Structure
- Landmark / root class: .cat-bar.variant-bar (see draft HTML)
- Slots: chips
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "rail-variant",
  "instanceId": "demo:rail-variant",
  "phase": "ready",
  "selectedId": "fire",
  "chips": []
}
`
phase: 
eady|loading|empty|error|pending. Magnitudes use alueRaw + alueText (VM formats).

## Theme slots
- Pack kind(s): element|status|…
- Reads: --piece-accent, --piece-rail-edge, paint.accent / paint.accentMuted when visual
- Vfx keys: none

## Data flow
- Bind: vm.variantRail
- Bus out: derived.variant.set

## Focus (GG-19)
roving tabindex

## Motion (GG-31/32)
none

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
