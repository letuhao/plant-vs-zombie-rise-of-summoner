# Piece: chip

**Program:** gui-lego · **Kind:** entity · **ERM rung:** Chip  
**Draft:** [../../design/gui-lego/pieces/chip.html](../../design/gui-lego/pieces/chip.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
One selectable rail option

## Structure
- Landmark / root class: .chip (see draft HTML)
- Slots: _none_
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "chip",
  "instanceId": "demo:chip",
  "phase": "ready",
  "id": "fire",
  "label": "Fire",
  "selected": true,
  "themeRef": {
    "kind": "element",
    "id": "fire"
  },
  "count": 12
}
`
phase: 
eady|loading|empty|error|pending. Magnitudes use alueRaw + alueText (VM formats).

## Theme slots
- Pack kind(s): themeRef
- Reads: --piece-accent, --piece-rail-edge, paint.accent / paint.accentMuted when visual
- Vfx keys: select vfx

## Data flow
- Bind: rail.chips[]
- Bus out: tab/variant set via parent

## Focus (GG-19)
yes

## Motion (GG-31/32)
vfx.select

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
