# Piece: source-list

**Program:** gui-lego · **Kind:** entity · **ERM rung:** Row list  
**Draft:** [../../design/gui-lego/pieces/source-list.html](../../design/gui-lego/pieces/source-list.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
GG-49 sources

## Structure
- Landmark / root class: .sources (see draft HTML)
- Slots: _none_
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "source-list",
  "instanceId": "demo:source-list",
  "phase": "ready",
  "items": [
    {
      "sourceId": "gear:weapon",
      "label": "Weapon",
      "valueText": "+1,083"
    }
  ]
}
`
phase: 
eady|loading|empty|error|pending. Magnitudes use alueRaw + alueText (VM formats).

## Theme slots
- Pack kind(s): neutral
- Reads: --piece-accent, --piece-rail-edge, paint.accent / paint.accentMuted when visual
- Vfx keys: none

## Data flow
- Bind: vm.inspect.sources
- Bus out: _none (parent or host)_

## Focus (GG-19)
no

## Motion (GG-31/32)
none

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
