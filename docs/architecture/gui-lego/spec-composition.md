# Module: composition — surface recipe + registries

**Program:** `gui-lego`  
**Ideal:** [../gui-lego-ideal.md](../gui-lego-ideal.md)  
**Map:** [../gui-lego-map.md](../gui-lego-map.md)  
**Sample recipe:** [../../design/gui-lego/recipes/derived-console.json](../../design/gui-lego/recipes/derived-console.json)

---

## 1. Intent

Define how Lego pieces snap together without rebuilding god-surfaces. Three registries + named
slots + recipe trees.

---

## 2. Three registries

| Registry | Key | Value |
|---|---|---|
| **Piece** | `pieceId` | Contract path, HTML draft path, (later) React factory |
| **Theme** | `themeId` (`kind.id`) | Pack: `css`, `paint`, `vfx`, optional `glyphDefault` |
| **Recipe** | `surfaceId` | Root piece tree + slot fills + bind paths |

A **surface** is `recipe + fold + bus`. It is not a freestyle TSX tree.

---

## 3. Slot grammar

1. Each **layout** / **panel-slice** piece declares a closed set of **slot names** (strings).  
2. A slot accepts either:
   - one child piece ref, or  
   - an ordered array of piece refs (e.g. `tools`).  
3. Domain / chrome pieces may declare **zero** slots (leaves).  
4. Filling a slot must not introduce DOM between a parent and a child that CSS selects with `>` —
   the mount layer emits the child as a **direct** DOM child of the parent landmark (see
   [html-design-implementation.md](../html-design-implementation.md)).

### Declared slots (Derived v1)

| piece-id | Slots |
|---|---|
| `surface-shell` | `identity`, `tools`, `railPrimary`, `railVariant`, `main`, `foot` |
| `split-inspect` | `dock`, `inspect` |
| `family-block` | `rows` (array of `channel-row`) — **header is payload-owned** (`title` / `hint`), not a child piece |
| `family-list` | `blocks` (array of `family-block`); empty filter uses payload `count === 0` → dock `phase-empty` (keep chrome) |
| `inspect-pane` | `hero`, `meta`, `cap`, `gauges`, `sources` |
| `scroll-region` | `content` |
| `rail-primary` / `rail-variant` | `chips` (array of `chip`) |

Leaves (`chip`, `channel-row`, `tool-search`, gauges, lifecycle, …): no slots.

---

## 4. Piece ref shape

```json
{
  "piece": "channel-row",
  "instanceId": "row:combat.power.fire",
  "bind": "vm.families[0].rows[0]",
  "slots": {}
}
```

| Field | Required | Notes |
|---|---|---|
| `piece` | yes | Registry piece-id |
| `instanceId` | yes at mount | Stable for tests / virtualization |
| `bind` | yes | Dot path into surface VM (design-time contract) |
| `slots` | if parent has slots | Map slot name → ref or ref[] |

---

## 5. Recipe document shape

```json
{
  "surfaceId": "derived-console",
  "host": "actor-panel-tab",
  "version": 1,
  "root": { "piece": "surface-shell", "instanceId": "shell:derived", "bind": "vm", "slots": {} }
}
```

- `host`: where the surface mounts (`actor-panel-tab` for v1).  
- `version`: bump when slot names or required binds change.  
- Design SSOT lives under `docs/design/gui-lego/recipes/`. Runtime home (FE module vs `data/`)
  is decided at implementation — not this module's gate.

---

## 6. Instance ids and selection

- Payload map: `Record<instanceId, PiecePayload>`.  
- Surface VM holds `selectedInstanceId` (and/or `selectedChannelId` for Derived).  
- Row/chip payloads receive `selected: boolean` **computed by the fold**, not private React state
  as the SSOT.

---

## 7. BindSurface (facade, later)

`bindSurface(recipe, vm, themeRegistry) → MountPlan`

1. Walk recipe; resolve each `bind` against `vm`.  
2. Resolve `themeRef` → pack; attach `css` / `paint` / `vfx` handles for the view.  
3. Validate payload `piece` field matches recipe `piece`.  
4. Emit mount plan (instanceId → props).  

Design phase documents the contract; no production code yet.

---

## 8. Anti-patterns

| Ban | Why |
|---|---|
| Anonymous wrappers for “convenience” | Breaks `.console > .inspect-split` |
| Open-ended slot names per feature | Kit cannot be shared |
| Piece that fetches to fill a sibling slot | Breaks MVVM |
| Recipe that embeds hex colors | Themes own paint |
