# ActorSheet Derived — structure + component contract

**Program:** actor-sheet · **Visual SSOT:** [docs/design/derived-combat-console.html](../docs/design/derived-combat-console.html)  
**Process:** [docs/architecture/html-design-implementation.md](../docs/architecture/html-design-implementation.md)  
**Skill:** `html-design-implementation`

Written before the module cut so DOM/`>` contracts cannot silently drift again.

---

## 1. Landmark tree (draft class names)

```text
.derived-combat-console.console
  header.console-hd
    .identity (.who / .meta)
    .hd-tools (.search / .toggle)
  nav.cat-bar                         # cook primary: elements|status|resources|other
  nav.cat-bar.variant-bar             # Fire|Ice|… or status/resource/action variants
  .inspect-split
    .dock
      .list-pane
        .family-block
          .family-hd
          button.row                  # data-kind, .glyph .nm .rd .val .badge|.state-tag
    aside.inspect
      .hd / .big / .reading / .sentence / .cap-note
      .contrib
        .share-donut (+ .share-legend)
        .stack > .stack-row
        ul.sources
  footer.foot
```

### CSS child combinators (fragile — no intervening DOM)

Production CSS under `.derived-combat-console` uses:

| Selector | Requires |
|---|---|
| `.derived-combat-console.console > .inspect-split` | `.inspect-split` is a **direct** child of `.console` |
| `.console > .inspect-split > .dock` | `.dock` direct child of `.inspect-split` |
| `.console > .inspect-split > .inspect` | `aside.inspect` direct child of `.inspect-split` |

**Ban:** any wrapper between `.console` and `.inspect-split` (including `display: contents`).  
`data-testid="derived-combat-console"` and `data-testid="derived-tab"` live on the **same** `.console` root.

---

## 2. Component contract (draft region → file)

| Draft region | Production module | Owns | Must not own |
|---|---|---|---|
| Compose / data | `DerivedTab.tsx` | Hooks → cook model → `<DerivedCombatConsole />` | Markup chrome, gauges |
| Cook/join math | `derived/derivedCook.ts` | join, expand, render state, toLiveMap, buckets, caps, cook tab ids | React, classNames |
| `.console` shell | `derived/DerivedCombatConsole.tsx` | Landmark tree as direct children of `.console` | Sheet fetch |
| `button.row` | `derived/DerivedChannelRow.tsx` | Six-state row chrome, CatalogIcon | Inspect |
| `aside.inspect` | `derived/DerivedInspector.tsx` | Big value, sentences, cap-note | Cook expand |
| donut/stack/sources | `derived/derivedGauges.tsx` | `.share-donut`, `.stack-row`, `.sources` | Channel join |
| Look | `DerivedCombatConsole.css` (import from shell) | SSOT selectors synced from HTML `<style>` | |

**Shared keep:** `ActorPanel`, `CatalogIcon`, `useDerivedSurface` / `useActorSheet` / `useActorDerived`.

**Do not reuse for Derived:** React `InspectSplit`, `StatRow`, `ChannelContributions` (plate-13 kit).

---

## 3. Intentional embed deltas (not defects)

| Delta | Why |
|---|---|
| Console height `min(640px, 72vh)` + `min-height: 28rem` | Sheet embed vs standalone HTML `min(820px, 88vh)` |
| Dual identity (ActorPanel header + `.console-hd` identity) | HTML SSOT keeps identity inside console; panel keeps sheet chrome |
| Live audit numbers vs Emberling fiction in draft | Real `/sheet` data |
| Lucide via CatalogIcon vs draft emoji glyphs | Locked presentation lib; same `.glyph` slot |

---

## 4. Verification plan

| Gate | Assert |
|---|---|
| Unit contract | `root.querySelector(":scope > .inspect-split")`; cook primary ids; forbid Offense/Pools primary; share-donut/stack/sources when contribs |
| Live e2e | `derived-sheet-visual` + `derived-ssot-side-by-side` |
| SPA currency | Rebuild wwwroot (sync to dist if serving from dist) |
| Visual | `e2e/artifacts/derived/ssot-html.png` + `ssot-spa.png` — left\|right split; owner gate |

**Behavior green ≠ visual green.**

---

## 5. Closed landmark class set

`console`, `console-hd`, `identity`, `who`, `meta`, `hd-tools`, `search`, `toggle`, `track`, `knob`, `cat-bar`, `variant-bar`, `chip`, `inspect-split`, `dock`, `list-pane`, `family-block`, `family-hd`, `row`, `glyph`, `nm`, `rd`, `val`, `badge`, `state-tag`, `inspect`, `big`, `reading`, `sentence`, `cap-note`, `contrib`, `share-donut`, `share-legend`, `stack`, `stack-row`, `sources`, `foot`.
