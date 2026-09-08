# Piece: channel-row

**Program:** gui-lego · **Kind:** entity · **ERM rung:** Row  
**Draft:** [../../design/gui-lego/pieces/channel-row.html](../../design/gui-lego/pieces/channel-row.html)  
**Composition:** [spec-composition.md](spec-composition.md)

## Role
One derived channel row

## Structure
- Landmark / root class: .row (see draft HTML)
- Slots: _none_
- CSS > ancestors: only when parent is surface-shell / split-inspect — **no illicit wrappers**

## Payload (sketch)
`json
{
  "piece": "channel-row",
  "instanceId": "demo:channel-row",
  "phase": "ready",
  "channelId": "combat.power.fire",
  "title": "Power",
  "reading": "Fire power",
  "valueRaw": 2847,
  "valueText": "2,847",
  "state": "active",
  "selected": true,
  "themeRef": {
    "kind": "element",
    "id": "fire"
  },
  "glyphRef": {
    "catalogIcon": "flame"
  }
}
`
phase: 
eady|loading|empty|error|pending. Magnitudes use alueRaw + alueText (VM formats).

## Theme slots
- Pack kind(s): themeRef
- Reads: --piece-accent, --piece-rail-edge, paint.accent / paint.accentMuted when visual
- Vfx keys: select

## Data flow
- Bind: families[].rows[]
- Bus out: derived.channel.select

## Focus (GG-19)
yes

## Motion (GG-31/32)
vfx.select

## Empty / error
If bind missing or phase not ready, parent mounts the matching phase-* piece — this leaf does not invent data.

## Samples
HTML draft shows structure + sample JSON; themeable pieces include fire vs ice (or status-dot) swap.
