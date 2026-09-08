# Piece: family-list

**Program:** gui-lego · **Kind:** layout · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/family-list.html](../../design/gui-lego/pieces/family-list.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
Ordered family blocks

## Structure
- Landmark / root class: .list-pane (see draft HTML)
- Slots: blocks
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "family-list",
  "instanceId": "demo:family-list",
  "phase": "ready",
  "count": 2
}
`
phase: 
eady|loading|empty|error|pending. Magnitudes use alueRaw + alueText (VM formats).

## Theme slots
- Pack kind(s): neutral
- Reads: --piece-accent, --piece-rail-edge, paint.accent / paint.accentMuted when visual
- Vfx keys: none

## Data flow
- Bind: vm.families
- Bus out: _none (parent or host)_

## Focus (GG-19)
no

## Motion (GG-31/32)
none

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
