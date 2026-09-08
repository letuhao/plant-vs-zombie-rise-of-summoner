# Piece: scroll-region

**Program:** gui-lego · **Kind:** layout · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/scroll-region.html](../../design/gui-lego/pieces/scroll-region.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
Declared overflow region

## Structure
- Landmark / root class: .scroll-region (see draft HTML)
- Slots: content
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "scroll-region",
  "instanceId": "demo:scroll-region",
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
- Bind: vm.*Scroll
- Bus out: _none (parent or host)_

## Focus (GG-19)
contains focusables

## Motion (GG-31/32)
none

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
