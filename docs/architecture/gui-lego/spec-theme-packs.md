# Module: theme-packs

**Program:** `gui-lego`  
**Ideal:** [../gui-lego-ideal.md](../gui-lego-ideal.md)  
**Demos:** [../../design/gui-lego/themes/](../../design/gui-lego/themes/)  
**Tokens:** [../../design/_kit/tokens.css](../../design/_kit/tokens.css)

---

## 1. Intent

Theme packs are first-class design artifacts. Pieces declare **slots**; packs fill **css**,
**paint**, and **vfx**. Extends the Lawn Almanac token layer — does not fork a second palette
(GG-29).

---

## 2. Pack schema

```json
{
  "themeId": "element.fire",
  "kind": "element",
  "id": "fire",
  "css": {
    "--piece-accent": "var(--el-fire)",
    "--piece-rail-edge": "var(--el-fire)",
    "--piece-select-glow": "rgb(224 112 60 / 0.35)"
  },
  "paint": {
    "accent": "#e0703c",
    "accentMuted": "#a04a28",
    "onAccent": "#1a1410"
  },
  "vfx": {
    "select": "vfx.ember-pulse",
    "idle": null
  },
  "glyphDefault": null
}
```

| Channel | Use |
|---|---|
| `css` | Bound as custom properties on piece root |
| `paint` | **Resolved hex** for SVG/`fill`, canvas, charts |
| `vfx` | Ids for binder; GG-32 collapses motion |

**Ban:** piece markup containing `--el-fire` / `#e0703c` for element identity. Demo HTML may show
resolved packs in a side panel for review, but the piece root must bind via pack application.

---

## 3. Taxonomy (design packs on disk)

| kind | Pack ids in `themes/packs/` |
|---|---|
| `element` | omni, fire, ice, air, earth, light, dark |
| `status-category` | omni, dot, cc, contagion |
| `side` | plant, zombie |
| `neutral` | default |

Resource / action-category / rarity / cook-tab packs follow the same schema when a queue row needs
them; v1 demos + swap-lab prove the mechanism with element + status-category + neutral + side.

---

## 4. Resolver precedence

1. Explicit `themeRef` on payload  
2. Inferred from cook expand (`element:fire`, …)  
3. `neutral`

---

## 5. Vfx binder (later)

Map `vfx.*` → CSS class or `motion` preset. Under `prefers-reduced-motion`, treat as instant state
change (GG-32). No Phaser FX pool for menu chrome.

---

## 6. Acceptance

Owner opens theme HTML demos: **same piece markup**, fire vs ice (and status-dot) — accent, rail
edge, and donut **paint** change; structure does not.
