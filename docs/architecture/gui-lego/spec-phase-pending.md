# Piece: phase-pending

**Program:** gui-lego · **Kind:** lifecycle · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/phase-pending.html](../../design/gui-lego/pieces/phase-pending.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
Pending field reason visible

## Structure
- Landmark / root class: .phase-pending (see draft HTML)
- Slots: _none_
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "phase-pending",
  "instanceId": "demo:phase-pending",
  "phase": "pending",
  "message": "Awaiting Hub compose",
  "field": "value"
}
`
phase: 
eady|loading|empty|error|pending. Magnitudes use alueRaw + alueText (VM formats).

## Theme slots
- Pack kind(s): neutral
- Reads: --piece-accent, --piece-rail-edge, paint.accent / paint.accentMuted when visual
- Vfx keys: none

## Data flow
- Bind: field
- Bus out: _none (parent or host)_

## Focus (GG-19)
no

## Motion (GG-31/32)
none

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
