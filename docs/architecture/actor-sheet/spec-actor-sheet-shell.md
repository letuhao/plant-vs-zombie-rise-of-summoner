# Spec: `actor-sheet-shell`

**Module id:** `actor-sheet-shell` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Ideal:** [actor-sheet-ideal.md](../actor-sheet-ideal.md) · **Visual (shell chrome):**
[actor-sheet-shell-rail.html](../../design/actor-sheet-shell-rail.html) · Tab inventories still on
[13-actor-sheet.html](../../design/13-actor-sheet.html).
**Status:** Draft — vertical rail amend 2026-09-09. **Replaces** the 2026-08-29 six-tab shell draft
at this path.

**Depends on:** `actor-surface-catalog` · **Blocks:** all `*-tab` modules.

---

## Assumptions

1. **“Near-fullscreen modal” means ActorSheet (`ActorPanel`), not Band B lawn HUD tokens.** Lawn
   tokens stay GG-60. Owner complaint about unused space maps to today’s
   `PanelShell` `w-[min(640px,92vw)]` / `max-h-[min(720px,82vh)]`
   ([PanelShell.tsx](../../../web/fusion-rpg-web/src/shell/PanelShell.tsx)).
2. **Default `PanelShell` size for other panels (Roster, etc.) does not change.** ActorSheet passes a
   size variant so only this surface grows.
3. **GG-61 still applies:** declared max height/width; slim chrome header + leftover footer
   `flex-none`; **right panel** scrolls; page/stage never scrolls to compensate. Near-fullscreen ≠
   `100vw×100vh` stage replacement (GG-1).
4. **Tab kinds are structural** (Condition … Paths). Catalog may order/hide/retitle/icon; a ninth
   kind without a React renderer is a load reject (catalog module).
5. **Tech stack (buy before build):** `lucide-react`, `recharts`, `react-tiny-sparkline`, `motion`,
   and `@xyflow/react` on Paths — [actor-sheet-map.md](../actor-sheet-map.md) Tech stack;
   [tech-stack.md](../../design/tech-stack.md) §3.3. Fat chunk ⇒ split; never ban these libs.
6. **Commander leftover v1** until UniqueDemon POST — Confirm uses existing allocate API; scope chip
   honest. Leftover footer **only** on Aptitudes (or while aptitude draft is dirty).
7. **`defaultOpen`** from `actor-sheet.v1.json`; **`versionStamp`** on the fan-in DTO for cache bust.
8. **Height reclaim (owner, 2026-09-09):** horizontal pill tab row + fat dual identity header are
   rejected. Identity lives once in the left rail summarize; tabs are a vertical rail with
   expand/collapse.
9. **Essential identity only:** the left-rail summarize may grow beyond `name / level / role`, but
   only to the current-recognition set: portrait, name, species, role/side, level, element chips,
   and phase. It does not absorb XP gauge, pools, Standing, statuses, rarity prose, or kit detail.

→ Correct these now or this spec proceeds as written.

---

## Objective

Deliver the one band-2 ActorSheet chrome: near-fullscreen shell, **slim** PanelShell header
(close/Esc), left rail (actor summarize + vertical catalog tabs, expand/collapse), right tab panel
hosting existing tab mounts, sticky leftover/Confirm footer — so every tab module mounts into a
surface that can show a dense catalog without wasting height on chrome.

**Users:** any player opening a unique / commander sheet (GG-9).

**Success:** At 1280×720 and 1920×1080 the sheet occupies most of the viewport with a visible stage
margin; tabs come from `actor-sheet.v1.json`; horizontal pill row is gone; Esc pops the sheet.

---

## Tech Stack

| Piece | Choice |
|---|---|
| Shell | Existing Radix `Dialog` via `PanelShell` + **`size="actorSheet"`** + **`headerMode="slim"`** |
| Tabs | `ActorSheetTabRail` (vertical; not the horizontal `TabList`) |
| Layout | Left rail \| right panel flex; tab bodies keep their own InspectSplit where needed |
| Meters / radials / leftover | **`recharts`** under tab modules / `LeftoverBar` |
| Sparks | **`react-tiny-sparkline`** on StatRow |
| Icons | `CatalogIcon` → **`lucide-react`** keyed by catalog tab `icon`; GG-58 fallback |
| Rail toggle | Lucide `PanelLeftClose` / `PanelLeftOpen` |
| Motion | Instant width OK; if animated, honour prefers-reduced-motion (GG-31/32) |
| State | Zustand layer stack; local tab + railCollapsed; React Query for catalog + actor GETs |
| Persist | `localStorage` key `fusionRpg.actorSheet.railCollapsed` |

**Near-fullscreen bound (locked for this module):**

```text
width:  min(1800px, 96vw)
height: min(960px, 92vh)
```

---

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run src/ui/actor/ActorPanel
npm test -- --run src/shell/shells.test.tsx
npx playwright test e2e/actor-sheet.spec.ts
```

---

## Project Structure

```text
web/fusion-rpg-web/src/shell/PanelShell.tsx           # size + headerMode
web/fusion-rpg-web/src/ui/actor/ActorPanel.tsx        # sheet orchestration
web/fusion-rpg-web/src/ui/actor/ActorSheetTabRail.tsx # vertical tabs + collapse
web/fusion-rpg-web/src/ui/actor/ActorSummarize.tsx    # essential actor identity summarize
web/fusion-rpg-web/src/ui/actor/CatalogIcon.tsx
docs/design/actor-sheet-shell-rail.html              # shell visual SSOT
data/tuning/actor-sheet.v1.json                      # tabs + optional icon
```

---

## Design

### Chrome

```text
┌ PanelShell slim header (sr-only title · close/Esc) ───────────────────┐
│ ┌ left rail ──────── [«] ┐  ┌ right panel (flex-1, scrolls) ────────┐ │
│ │ portrait / name        │  │ active tab body (existing mounts)     │ │
│ │ species · Lv · role    │  │                                       │ │
│ │ elements · phase       │  │                                       │ │
│ │ vertical tabs          │  │                                       │ │
│ └────────────────────────┘  └───────────────────────────────────────┘ │
│ leftover · Reset · Confirm — **only when Aptitudes active or dirty**  │
└───────────────────────────────────────────────────────────────────────┘
```

**Expanded rail:** portrait · name · species/side/phase chips · `Lv n` · role · up-to-two element
chips · icon+label tabs.  
**Collapsed rail:** portrait/glyph only (tooltip = `Name · Species · Lv n · Role`) · icon-only tabs.  
**Role:** commander → `Commander`; creature → `Plant` / `Zombie`.

Leftover footer is **only** on Aptitudes (or while an aptitude draft is dirty). Other tabs omit that
strip — do not move Confirm into a band-3 dialog (GG-63).

### Tab bar

```tsx
const tabs = surface.tabs
  .filter(t => !t.hidden)
  .sort((a, b) => a.order - b.order)
  .map(t => ({
    id: t.kind,
    label: t.label,
    icon: t.icon,
    testId: `actor-sheet-tab-${t.kind}`
  }));
```

Renderer switch unchanged: `condition` | `aptitudes` | `derived` | `shield` | `status` |
`elements` | `kit` | `paths`. `defaultOpen` from catalog.

### Catalog tab icons (lucide keys)

| Kind | `icon` |
|---|---|
| condition | `heart-pulse` |
| aptitudes | `sparkles` |
| derived | `sigma` |
| shield | `shield` |
| status | `activity` |
| elements | `atom` |
| kit | `backpack` |
| paths | `git-branch` |

Collapsed tabs keep `aria-label={label}` and `title={label}`.

### Shared widgets

| Widget | Job |
|---|---|
| `ActorSummarize` | essential identity only: portrait / name / species / level / role / elements / phase |
| `ActorSheetTabRail` | vertical tabs · expand/collapse |
| `StatRow` / `InspectSplit` / … | unchanged tab kit |

### Filename / routing

Export alias `ActorSheet`. `?panel=…&sel=<instanceId>`. No `#/actor/:id`.

---

## Tunables

| Key | Home | Unit |
|---|---|---|
| Sheet width/height caps | structural CSS (GG-61 bound, not balance) | CSS px / vh |
| Tab labels/order/icons | `actor-sheet.v1.json` | — |
| Rail collapsed default | localStorage preference | boolean |

No new combat numbers.

---

## Code Style

```tsx
<PanelShell
  open={open}
  onOpenChange={onOpenChange}
  title={name}
  headerMode="slim"
  size="actorSheet"
  footer={footer}
  testId="actor-panel"
>
  <div className="flex min-h-0 flex-1">
    <ActorSheetTabRail ... summarize={<ActorSummarize ... />} />
    <div data-testid="actor-sheet-tab-panel" className="min-w-0 flex-1 overflow-y-auto">
      {/* existing tab mounts */}
    </div>
  </div>
</PanelShell>
```

---

## Testing Strategy

| Level | What |
|---|---|
| Unit | Catalog tabs + icons; rail expand/collapse; summarize fields; leftover rules |
| Shell fixture | `headerMode=slim`; actorSheet size bound |
| Viewport | 1280×720 / 1440×900 / 1920×1080 — no page horizontal scroll |
| a11y | Collapsed tabs have aria-label; toggle aria-expanded |

---

## Boundaries

- **Always:** Near-fullscreen bound; GG-61 right-panel scroll; catalog-driven tabs/icons; leftover
  footer only on Aptitudes/dirty draft; Esc pops sheet; slim header (no duplicate name hero).
- **Ask first:** Changing the bound numbers; making *all* PanelShells this large; UniqueDemon
  allocate; a second overlapping presentation library.
- **Never:** `#/actor/:id`; nested dialog for readings; hand-rolling radials/icons to dodge npm;
  Band B numeric wall; hardcoded eight tab labels in React; horizontal pill tab row on this sheet;
  five-string `ResourceId` roster.

---

## Success Criteria

- [x] ActorSheet uses `min(1800px, 96vw)` × `min(960px, 92vh)`
- [x] Other PanelShell consumers unchanged at default size
- [ ] Vertical left rail with expand/collapse; horizontal pill row gone
- [ ] Actor summarize = portrait / name / species / level / role / elements / phase
- [ ] Collapsed rail summarize = portrait or glyph + tooltip
- [ ] Tabs match `actor-sheet.v1.json` order/labels/`defaultOpen`/icons
- [ ] InspectSplit does not push band 3; Confirm stays footer on Aptitudes only
- [ ] Shell visual matches [actor-sheet-shell-rail.html](../../design/actor-sheet-shell-rail.html)

---

## Open Questions

None — leftover visibility locked; rail visual SSOT is `actor-sheet-shell-rail.html`.
