# Piece: gauge-donut

**Program:** gui-lego · **Kind:** gauge · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/gauge-donut.html](../../design/gui-lego/pieces/gauge-donut.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
Share donut using paint hex

## Structure
- Landmark / root class: .share-donut (see draft HTML)
- Slots: _none_
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "gauge-donut",
  "instanceId": "demo:gauge-donut",
  "phase": "ready",
  "segments": [
    {
      "id": "base",
      "label": "Base",
      "sharePm": 620,
      "paintKey": "accent"
    },
    {
      "id": "gear",
      "label": "Gear",
      "sharePm": 380,
      "paintKey": "accentMuted"
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
- Bind: vm.inspect.donut
- Bus out: _none (parent or host)_

## Focus (GG-19)
no

## Motion (GG-31/32)
none

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
