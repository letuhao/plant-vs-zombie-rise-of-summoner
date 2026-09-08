# Piece: gauge-stack

**Program:** gui-lego · **Kind:** gauge · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/gauge-stack.html](../../design/gui-lego/pieces/gauge-stack.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
Contribution stack bars

## Structure
- Landmark / root class: .stack (see draft HTML)
- Slots: _none_
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "gauge-stack",
  "instanceId": "demo:gauge-stack",
  "phase": "ready",
  "rows": [
    {
      "id": "base",
      "label": "Base",
      "valueText": "+1,764",
      "sharePm": 620
    },
    {
      "id": "gear",
      "label": "Gear",
      "valueText": "+1,083",
      "sharePm": 380
    }
  ],
  "themeRef": {
    "kind": "element",
    "id": "fire"
  }
}
`
phase: 
eady|loading|empty|error|pending. Magnitudes use alueRaw + alueText (VM formats).

## Theme slots
- Pack kind(s): themeRef
- Reads: --piece-accent, --piece-rail-edge, paint.accent / paint.accentMuted when visual
- Vfx keys: none

## Data flow
- Bind: vm.inspect.stack
- Bus out: _none (parent or host)_

## Focus (GG-19)
no

## Motion (GG-31/32)
none

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
