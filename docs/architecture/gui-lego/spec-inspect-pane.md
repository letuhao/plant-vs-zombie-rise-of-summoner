# Piece: inspect-pane

**Program:** gui-lego · **Kind:** entity · **ERM rung:** Panel-slice  
**Draft:** [../../design/gui-lego/pieces/inspect-pane.html](../../design/gui-lego/pieces/inspect-pane.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
Assembles hero/meta/cap/gauges/sources

## Structure
- Landmark / root class: .inspect (see draft HTML)
- Slots: hero,meta,cap,gauges,sources
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "inspect-pane",
  "instanceId": "demo:inspect-pane",
  "phase": "ready",
  "channelId": "combat.power.fire",
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
- Bind: vm.inspect
- Bus out: _none (parent or host)_

## Focus (GG-19)
contains

## Motion (GG-31/32)
none

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
