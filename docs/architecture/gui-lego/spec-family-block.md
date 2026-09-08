# Piece: family-block

**Program:** gui-lego · **Kind:** layout · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/family-block.html](../../design/gui-lego/pieces/family-block.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
Section header + rows

## Structure
- Landmark / root class: .family-block (see draft HTML)
- Slots: header,rows
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "family-block",
  "instanceId": "demo:family-block",
  "phase": "ready",
  "familyId": "power",
  "title": "Power",
  "rowCount": 3
}
`
phase: 
eady|loading|empty|error|pending. Magnitudes use alueRaw + alueText (VM formats).

## Theme slots
- Pack kind(s): neutral
- Reads: --piece-accent, --piece-rail-edge, paint.accent / paint.accentMuted when visual
- Vfx keys: none

## Data flow
- Bind: families[]
- Bus out: _none (parent or host)_

## Focus (GG-19)
no

## Motion (GG-31/32)
none

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
