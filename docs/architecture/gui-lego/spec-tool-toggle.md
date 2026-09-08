# Piece: tool-toggle

**Program:** gui-lego · **Kind:** chrome · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/tool-toggle.html](../../design/gui-lego/pieces/tool-toggle.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
Boolean tool (show unchanged)

## Structure
- Landmark / root class: .toggle (see draft HTML)
- Slots: _none_
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "tool-toggle",
  "instanceId": "demo:tool-toggle",
  "phase": "ready",
  "value": false,
  "label": "Show unchanged"
}
`
phase: 
eady|loading|empty|error|pending. Magnitudes use alueRaw + alueText (VM formats).

## Theme slots
- Pack kind(s): neutral
- Reads: --piece-accent, --piece-rail-edge, paint.accent / paint.accentMuted when visual
- Vfx keys: none

## Data flow
- Bind: vm.showUnchanged
- Bus out: derived.showUnchanged.set

## Focus (GG-19)
yes

## Motion (GG-31/32)
knob 160ms / reduced=instant

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
