# Piece: identity-hd

**Program:** gui-lego · **Kind:** chrome · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/identity-hd.html](../../design/gui-lego/pieces/identity-hd.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
Who / level / side / cook meta

## Structure
- Landmark / root class: .identity (see draft HTML)
- Slots: _none_
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "identity-hd",
  "instanceId": "demo:identity-hd",
  "phase": "ready",
  "who": "Emberling",
  "meta": "Lv 12 \u00b7 Plant \u00b7 Elements / Fire",
  "themeRef": {
    "kind": "side",
    "id": "plant"
  }
}
`
phase: 
eady|loading|empty|error|pending. Magnitudes use alueRaw + alueText (VM formats).

## Theme slots
- Pack kind(s): side
- Reads: --piece-accent, --piece-rail-edge, paint.accent / paint.accentMuted when visual
- Vfx keys: none

## Data flow
- Bind: vm.identity
- Bus out: _none (parent or host)_

## Focus (GG-19)
no

## Motion (GG-31/32)
none

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
