# Spec: `actor-sheet-shell`

**Module id:** `actor-sheet-shell` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Ideal:** [actor-sheet-ideal.md](../actor-sheet-ideal.md) · **Visual:**
[13-actor-sheet.html](../../design/13-actor-sheet.html) ·
**Status:** Draft — pending owner review. **Replaces** the 2026-08-29 six-tab shell draft at this path.

**Depends on:** `actor-surface-catalog` · **Blocks:** all `*-tab` modules.

---

## Assumptions

1. **“Near-fullscreen modal” means ActorSheet (`ActorPanel`), not Band B lawn HUD tokens.** Lawn
   tokens stay GG-60. Owner complaint about unused space maps to today’s
   `PanelShell` `w-[min(640px,92vw)]` / `max-h-[min(720px,82vh)]`
   ([PanelShell.tsx:84-86](../../../web/fusion-rpg-web/src/shell/PanelShell.tsx)).
2. **Default `PanelShell` size for other panels (Roster, etc.) does not change.** ActorSheet passes a
   size variant (or a thin `ActorSheetShell` wrapper) so only this surface grows.
3. **GG-61 still applies:** declared max height/width; header + leftover footer `flex-none`; body
   scrolls; page/stage never scrolls to compensate. Near-fullscreen ≠ `100vw×100vh` stage replacement
   (GG-1).
4. **Tab kinds are structural** (Condition … Paths). Catalog may order/hide/retitle; a ninth kind
   without a React renderer is a load reject (catalog module).
5. **Tech stack (buy before build):** `lucide-react`, `recharts`, `react-tiny-sparkline`, `motion`,
   and `@xyflow/react` on Paths — [actor-sheet-map.md](../actor-sheet-map.md) Tech stack;
   [tech-stack.md](../../design/tech-stack.md) §3.3. Fat chunk ⇒ split; never ban these libs.
6. **Commander leftover v1** until UniqueDemon POST — Confirm uses existing allocate API; scope chip
   honest. Leftover footer **only** on Aptitudes (or while aptitude draft is dirty).
7. **`defaultOpen`** from `actor-sheet.v1.json`; **`versionStamp`** on the fan-in DTO for cache bust.

→ Correct these now or this spec proceeds as written.

---

## Objective

Deliver the one band-2 ActorSheet chrome: near-fullscreen shell, identity header, catalog-driven
tab bar, InspectSplit layout, shared widgets, sticky leftover/Confirm footer — so every tab module
mounts into a surface that can actually show a dense catalog.

**Users:** any player opening a unique / commander sheet (GG-9).

**Success:** At 1280×720 and 1920×1080 the sheet occupies most of the viewport with a visible stage
margin; tabs come from `actor-sheet.v1.json`; InspectSplit does not push band 3; Esc pops the sheet.

---

## Tech Stack

| Piece | Choice |
|---|---|
| Shell | Existing Radix `Dialog` via `PanelShell` + **`size="actorSheet"`** (or equivalent prop) |
| Tabs | Existing `TabList` |
| Layout | CSS grid InspectSplit (list | inspector); plate 13 proportions |
| Meters / radials / leftover | **`recharts`** (`RadialBarChart`, bar for leftover) under `web/.../ui/actor/` |
| Sparks | **`react-tiny-sparkline`** on StatRow |
| Icons | `CatalogIcon` → **`lucide-react`** keyed by catalog `icon` / `hudToken`; GG-58 fallback |
| Motion | **`motion`** for open/tab/InspectSplit when CSS insufficient; prefers-reduced-motion |
| State | Existing Zustand layer stack; local tab state; React Query for catalog + actor GETs |
| i18n | Catalog strings are content; chrome via Lingui later — do not block on Lingui for v1 |

**Near-fullscreen bound (locked for this module):**

```text
width:  min(1800px, 96vw)
height: min(960px, 92vh)
```

Rationale: at 1920×1080 a 1280-wide cap leaves unused stage; owner wants most of the viewport used;
4% / 8% margins keep the stage readable and satisfy GG-61. Fixture asserts `clientHeight ≤ 92vh`
and body scrollbar when content exceeds.

---

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run src/ui/actor/ActorPanel
npm test -- --run src/shell/shells.test.tsx
npx playwright test e2e/actor-sheet.spec.ts   # when added: viewport sweep GG-36
```

---

## Project Structure

```text
web/fusion-rpg-web/src/shell/PanelShell.tsx     # size variant
web/fusion-rpg-web/src/ui/actor/ActorPanel.tsx  # sheet orchestration
web/fusion-rpg-web/src/ui/actor/InspectSplit.tsx
web/fusion-rpg-web/src/ui/actor/StatRow.tsx
web/fusion-rpg-web/src/ui/actor/AptitudeTile.tsx
web/fusion-rpg-web/src/ui/actor/LeftoverBar.tsx
web/fusion-rpg-web/src/ui/actor/CatalogIcon.tsx   # lucide-react + GG-58 fallback
web/fusion-rpg-web/src/ui/actor/meters/          # recharts RadialBar / leftover; tiny-sparkline
web/fusion-rpg-web/src/hooks/useActorSurfaceCatalog.ts
```

---

## Design

### Chrome

```text
┌ header: portrait · name · species · side · level · elements · Fielded/Wave · Esc ─┐
├ tab bar (from catalog) ───────────────────────────────────────────────────────────┤
├──────────────────────────────┬────────────────────────────────────────────────────┤
│ tab body (no page scroll)    │ inspector (scrolls)                                │
│                              │                                                    │
├──────────────────────────────┴────────────────────────────────────────────────────┤
│ leftover meter · Reset · Confirm  — **only when Aptitudes active or draft dirty** │
└───────────────────────────────────────────────────────────────────────────────────┘
```

Leftover footer is **only** on Aptitudes (or while an aptitude draft is dirty). Other tabs omit the
footer strip entirely — do not move Confirm into a band-3 dialog (GG-63). Empty leftover is legal;
Confirm disabled when leftover `< 0`; overspend refuses (409).

### Tab bar

```tsx
// Pseudo — kinds closed in code; labels/order from catalog
const tabs = surface.tabs
  .filter(t => !t.hidden)
  .sort((a, b) => a.order - b.order)
  .map(t => ({ id: t.kind, label: t.label, testId: `actor-sheet-tab-${t.kind}` }));
```

Renderer switch: `condition` | `aptitudes` | `derived` | `shield` | `status` | `elements` | `kit` |
`paths`. Unknown kind → never rendered (host already rejected). `defaultOpen` from catalog.

### Shared widgets

| Widget | Job |
|---|---|
| `StatRow` | lucide icon · displayName · number · tiny-sparkline · `?` |
| `InspectSplit` | left dock / right inspector (list may scroll independently of inspector) |
| `AptitudeTile` | `+`/`−`, selected state |
| `LeftoverBar` | recharts bar; budget − spent; empty legal |
| `ShieldLayer` | recharts radial well |
| `StatusGlyph` | lucide / catalog hudToken + color |
| `CatalogIcon` | lucide-react map + GG-58 fallback |

Spark fill-to-100% only for pools, bounded ratios, registry caps (GG-64 / D14).

### Labels

`channelLabel` and every player-band name read catalog `displayName`. Delete the five-string
`ResourceId` union as a roster once resource-catalog is consumed.

### Filename / routing

Export alias `ActorSheet`. `?panel=…&sel=<instanceId>`. No `#/actor/:id`.

---

## Tunables

| Key | Home | Unit |
|---|---|---|
| Sheet width/height caps | structural CSS in shell (comment: GG-61 bound, not balance) | CSS px / vh |
| Tab labels/order | `actor-sheet.v1.json` | — |

No new combat numbers.

---

## Code Style

```tsx
<PanelShell
  open={open}
  onOpenChange={onOpenChange}
  title={title}
  size="actorSheet"
  footer={<LeftoverFooter ... />}
  testId="actor-sheet"
>
  <InspectSplit
    list={<TabList ... /> /* + active tab body */}
    inspector={<InspectorPane ... />}
  />
</PanelShell>
```

Magnitudes from APIs stay `number`/`bigint` as today’s contract; display formatting via existing
unit-class helpers — never invent a third classification.

---

## Testing Strategy

| Level | What |
|---|---|
| Unit | Tab list built from fixture catalog; unknown kind ignored; leftover Confirm disabled when overspend |
| Shell fixture | Dense content → body `scrollHeight > clientHeight`; shell `clientHeight ≤ 92vh` |
| Viewport | 1280×720 / 1440×900 / 1920×1080 — no horizontal page scroll; stage margin visible |
| Guard | No `idWords` on player-band labels when catalog present |

---

## Boundaries

- **Always:** Near-fullscreen bound above; GG-61 body scroll; catalog-driven tabs; buy-before-build
  presentation libs; leftover footer only on Aptitudes/dirty draft; Esc pops sheet.
- **Ask first:** Changing the bound numbers; making *all* PanelShells this large; UniqueDemon
  allocate; a second overlapping presentation library.
- **Never:** `#/actor/:id`; nested dialog for readings; hand-rolling radials/icons to dodge npm;
  Band B numeric wall; hardcoded eight tab labels in React once catalog ships; five-string
  `ResourceId` roster.

---

## Success Criteria

- [ ] ActorSheet uses `min(1800px, 96vw)` × `min(960px, 92vh)` — visibly larger than today’s
      640×720-capped panel at 1920×1080
- [ ] Other PanelShell consumers unchanged at default size
- [ ] Tabs match `actor-sheet.v1.json` order/labels/`defaultOpen`
- [ ] InspectSplit does not push band 3; Confirm stays footer decision control on Aptitudes only
- [ ] Shared widgets exported for tab modules; plate 13 visual grammar matched for chrome
- [ ] `channelLabel` / resource meters use catalog displayNames

---

## Open Questions

None — leftover visibility locked (Aptitudes / dirty draft only); plate 13 is visual SSOT.
