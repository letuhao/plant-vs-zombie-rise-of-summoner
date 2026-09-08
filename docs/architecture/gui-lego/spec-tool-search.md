# Piece: tool-search

**Program:** gui-lego · **Kind:** chrome · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/tool-search.html](../../design/gui-lego/pieces/tool-search.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
Search query field

## Structure
- Landmark / root class: .search (see draft HTML)
- Slots: _none_
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "tool-search",
  "instanceId": "demo:tool-search",
  "phase": "ready",
  "query": "",
  "placeholder": "Search channels"
}
`
phase: 
eady|loading|empty|error|pending. Magnitudes use alueRaw + alueText (VM formats).

## Theme slots
- Pack kind(s): neutral
- Reads: --piece-accent, --piece-rail-edge, paint.accent / paint.accentMuted when visual
- Vfx keys: none

## Data flow
- Bind: vm.search
- Bus out: derived.search.set

## Focus (GG-19)
yes tab stop

## Motion (GG-31/32)
none

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
