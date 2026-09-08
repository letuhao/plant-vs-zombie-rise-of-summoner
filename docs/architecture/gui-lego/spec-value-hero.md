# Piece: value-hero

**Program:** gui-lego · **Kind:** entity · **ERM rung:** Token→display  
**Draft:** [../../design/gui-lego/pieces/value-hero.html](../../design/gui-lego/pieces/value-hero.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
Big magnitude

## Structure
- Landmark / root class: .big (see draft HTML)
- Slots: _none_
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "value-hero",
  "instanceId": "demo:value-hero",
  "phase": "ready",
  "title": "Power",
  "valueRaw": 2847,
  "valueText": "2,847",
  "reading": "Fire power",
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
- Bind: vm.inspect.hero
- Bus out: _none (parent or host)_

## Focus (GG-19)
no

## Motion (GG-31/32)
none

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
