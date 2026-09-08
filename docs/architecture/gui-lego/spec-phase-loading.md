# Piece: phase-loading

**Program:** gui-lego · **Kind:** lifecycle · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/phase-loading.html](../../design/gui-lego/pieces/phase-loading.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
Loading frame

## Structure
- Landmark / root class: .phase-loading (see draft HTML)
- Slots: _none_
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "phase-loading",
  "instanceId": "demo:phase-loading",
  "phase": "loading",
  "message": "Loading derived sheet\u2026"
}
`
phase: 
eady|loading|empty|error|pending. Magnitudes use alueRaw + alueText (VM formats).

## Theme slots
- Pack kind(s): neutral
- Reads: --piece-accent, --piece-rail-edge, paint.accent / paint.accentMuted when visual
- Vfx keys: none

## Data flow
- Bind: vm.phase
- Bus out: _none (parent or host)_

## Focus (GG-19)
no

## Motion (GG-31/32)
none

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
