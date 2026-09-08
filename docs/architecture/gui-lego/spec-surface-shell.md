# Piece: surface-shell

**Program:** gui-lego · **Kind:** layout · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/surface-shell.html](../../design/gui-lego/pieces/surface-shell.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
Outer frame; slot host for identity/tools/rails/main/foot

## Structure
- Landmark / root class: .console (see draft HTML)
- Slots: identity,tools,railPrimary,railVariant,main,foot
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "surface-shell",
  "instanceId": "demo:surface-shell",
  "phase": "ready"
}
`
phase: 
eady|loading|empty|error|pending. Magnitudes use alueRaw + alueText (VM formats).

## Theme slots
- Pack kind(s): neutral
- Reads: --piece-accent, --piece-rail-edge, paint.accent / paint.accentMuted when visual
- Vfx keys: none

## Data flow
- Bind: vm
- Bus out: _none (parent or host)_

## Focus (GG-19)
initial focus may land in tools

## Motion (GG-31/32)
none

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
