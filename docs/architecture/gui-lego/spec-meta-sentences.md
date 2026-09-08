# Piece: meta-sentences

**Program:** gui-lego · **Kind:** chrome · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/meta-sentences.html](../../design/gui-lego/pieces/meta-sentences.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
Compose / unit / join lines

## Structure
- Landmark / root class: .sentence (see draft HTML)
- Slots: _none_
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "meta-sentences",
  "instanceId": "demo:meta-sentences",
  "phase": "ready",
  "lines": [
    "Compose: Hub derived",
    "Unit: whole",
    "Join: channelId"
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
- Bind: vm.inspect.meta
- Bus out: _none (parent or host)_

## Focus (GG-19)
no

## Motion (GG-31/32)
none

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
