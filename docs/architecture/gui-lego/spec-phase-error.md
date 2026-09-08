# Piece: phase-error

**Program:** gui-lego · **Kind:** lifecycle · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/phase-error.html](../../design/gui-lego/pieces/phase-error.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
Sheet/cook unavailable

## Structure
- Landmark / root class: .phase-error (see draft HTML)
- Slots: _none_
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "phase-error",
  "instanceId": "demo:phase-error",
  "phase": "error",
  "message": "Sheet unavailable",
  "retryLabel": "Retry"
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
- Bus out: derived.retry

## Focus (GG-19)
retry focus

## Motion (GG-31/32)
none

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
