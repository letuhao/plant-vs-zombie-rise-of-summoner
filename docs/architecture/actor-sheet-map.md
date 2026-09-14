# Capability map: actor-sheet

**Status:** Draft — pending owner review. **No build authorized until approved.** Ideal:
[actor-sheet-ideal.md](actor-sheet-ideal.md). Shell visual (root rail):
[actor-sheet-shell-rail.html](../design/actor-sheet-shell-rail.html). Tab inventories:
[13-actor-sheet.html](../design/13-actor-sheet.html). **Derived pane visual SSOT:**
[derived-combat-console.html](../design/derived-combat-console.html) (modern-stat-hud bar). Sibling HUD:
[actor-hud-ideal.md](actor-hud-ideal.md) (glyphs consume the same catalogs). Sibling lawn chrome
(not this program): [lawn-interactive-map.md](lawn-interactive-map.md) · design landing
[spec-lawn-interactive.md](../design/spec-lawn-interactive.md). Plan parked:
[tasks/actor-sheet-plan.md](../../tasks/actor-sheet-plan.md) (SPECIFY gate).

> **Supersedes the 2026-08-29 six-tab map** (Overview unchanged / derived doorway / locked
> Actions·Passives). That shape is kept only as the trail under [actor-sheet/](actor-sheet/) —
> five pre-catalog `spec-*.md` files with old module ids. Do not implement from those trail files;
> implement from the module ids below.

Keep `ActorPanel.tsx`; export alias `ActorSheet`. `?sel=` = `instanceId` only. Promote is out of
scope. Action corpus is sealed in [action-ideal.md](action-ideal.md) — Kit shows slots, does not
author actions.

---

## What this program is

One band-2 ActorSheet over whichever stage the player is already on (GG-9). Eight **structural
tab kinds** (Condition · Aptitudes · Derived · Shield · Status · Elements · Kit · Paths). Row
membership, names, readings, icons, and tab **labels/order** come from versioned runtime catalogs
in `data/tuning/` — hosts inject, FE iterates `GET /api/catalogs/actor-surface`. No hardcoded
roster unions in the FE.

**Shell size (owner, 2026-09-07):** the sheet is **near-fullscreen** — it uses most of the viewport
because it holds a dense catalog. Today `PanelShell` is `min(640px,92vw)` × `min(720px,82vh)` and
leaves unused stage space. ActorSheet opts into
`width: min(1800px, 96vw)` · `height: min(960px, 92vh)` (see `actor-sheet-shell`); it does
**not** become a stage or a route (GG-1). GG-61 still holds: declared max, body scrolls, footer
sticky, stage peeks at the margins.

**Runtime catalog SSOT** (file table, load path, file-save vs code):
[actor-sheet-ideal.md](actor-sheet-ideal.md) § Runtime catalog SSOT.

---

## Tech stack (icons, meters, graphs, assets)

Locked against [design/tech-stack.md](../design/tech-stack.md) §3.3 (**buy before build**, amended
2026-09-07) and GG-58 / GG-38 / GG-64. **Buy libraries** — do not hand-roll meters, icon packs, or
graphs. Fat chunk ⇒ split (`React.lazy` / dynamic import); never ban the dep.

| Need | Choice | Why |
|---|---|---|
| Stat sparks | **`react-tiny-sparkline`** | Dense StatRow lists |
| HP/shield radials, element donuts, leftover bar | **`recharts`** | Restored; plate shapes map cleanly |
| Catalog `icon` / status `hudToken` glyphs | **`lucide-react`** + GG-58 generated fallback (side/element tint + authored `hudToken` text, never `idWords`) | Buy before a local SVG registry as primary path |
| Paths / passive glance + tree push | **Reuse `PassivesTab` / tree-surface**; tree graph on **`@xyflow/react`** (read-only) | Node UI — not world-map HOW (Phaser) |
| Motion | **`motion`** when CSS insufficient | Buy springs; honour prefers-reduced-motion |
| Portrait / rarity frames | Existing `ActorFrame` + art registry when portraits exist | No new asset pipeline in v1 |
| Band B lawn VFX | **Out of scope** — status sustain VFX stays vfx program; HUD tokens only resolve catalog `hudToken`/`color` | Owner “modal” = this sheet, not lawn tokens |

Ask only before adding a *second* library that overlaps one already locked. GG-38 = Phaser stage-lazy,
not a veto on these packages.

---

## Modules

| Module id | Responsibility | Depends on | Spec |
|---|---|---|---|
| `actor-surface-catalog` | Versioned `*-catalog.v{n}.json` + `actor-sheet.v1.json`. Pure parsers. Host inject (`ConfigureAll` on Server + Injector). Core register-from-object. `GET /api/catalogs/derived-surface` shipped; full `GET /api/catalogs/actor-surface` fan-in still open. Load-reject unknown kinds. HUD tokens from status/resource catalogs. | — | [spec-actor-surface-catalog.md](actor-sheet/spec-actor-surface-catalog.md) |
| `actor-sheet-shell` | Near-fullscreen band-2 panel; header; InspectSplit; leftover sticky footer; tab bar iterates catalog kinds; shared widgets (StatRow, …); `PanelShell` size variant. Esc pops. | `actor-surface-catalog` | [spec-actor-sheet-shell.md](actor-sheet/spec-actor-sheet-shell.md) |
| `condition-tab` | HP radial + shield overlay, resource meters from resource-catalog, Standing, live status glyphs. | `actor-sheet-shell` | [spec-condition-tab.md](actor-sheet/spec-condition-tab.md) |
| `aptitudes-tab` | Tiles from aptitude-catalog, leftover Confirm — **superseded allocate scope** by [aptitude-sheet-map.md](aptitude-sheet-map.md) (UniqueCreature Mode A / commander Mode C). | `actor-sheet-shell` | [spec-aptitudes-tab.md](actor-sheet/spec-aptitudes-tab.md) |
| `derived-tab` | StatRows from **`GET /api/catalogs/derived-surface`** joined to **`/sheet`**. Six states. **Harden program:** [derived-cook-map.md](derived-cook-map.md). | `actor-sheet-shell` | [spec-derived-tab.md](actor-sheet/spec-derived-tab.md) |
| `shield-tab` | Segmented stack + omni rows. **Program:** [shield-sheet-map.md](shield-sheet-map.md). Noun **Shield**. | `actor-sheet-shell` | [spec-shield-tab.md](actor-sheet/spec-shield-tab.md) |
| `status-tab` | Glyphs from status-catalog. | `actor-sheet-shell` | [spec-status-tab.md](actor-sheet/spec-status-tab.md) |
| `elements-tab` | Radials from element-catalog + mastery StatRows. | `actor-sheet-shell` | [spec-elements-tab.md](actor-sheet/spec-elements-tab.md) |
| `kit-tab` | ActionSlot chrome + paper-doll roles from actor-sheet catalog. | `actor-sheet-shell` | [spec-kit-tab.md](actor-sheet/spec-kit-tab.md) |
| `paths-tab` | Species vs shared corpus glance; may push tree once. | `actor-sheet-shell` | [spec-paths-tab.md](actor-sheet/spec-paths-tab.md) |

Shared React kit is owned by `actor-sheet-shell` and reused by tab modules.

---

## Build order

```text
actor-surface-catalog
        │
        ▼
actor-sheet-shell
        │
        ├── condition-tab
        ├── aptitudes-tab
        ├── derived-tab
        ├── shield-tab
        ├── status-tab
        ├── elements-tab
        ├── kit-tab
        └── paths-tab
```

---

## Acceptance (program-level)

> With Fusion closed, opening ActorSheet for a commander or unique shows **catalog displayNames**
> (never dotted channel ids); resource meters include every id in resource-catalog (including
> `poise`); tab labels/order match `actor-sheet.v1.json`. The shell fills most of a 1280×720 /
> 1920×1080 viewport via `min(1800px, 96vw)` × `min(960px, 92vh)` with a visible stage margin; body
> scrolls; leftover/Confirm footer appears **only** on Aptitudes (or while an aptitude draft is dirty)
> (GG-61). Adding a family/status/resource row on an existing axis/kind is `publish.py` + restart with
> **no FE change**. Presentation libs (`lucide-react`, `recharts`, `react-tiny-sparkline`,
> `@xyflow/react`, `motion`) are allowed — fat chunk ⇒ split, not ban.

Lawn Band B still obeys GG-60. HUD glyphs resolve status-catalog `hudToken`/`color`.

---

## Explicitly not in this program

- **Promote** — owner: ignore.
- **Action corpus / costs / targeting** — sealed; Kit shows slots.
- **Aspect-scope aptitude** — reverted.
- **UniqueCreature allocate POST** — open product question; commander leftover recommended for v1.
- **Moving `ItemRole` off the C# enum** — item program owns append-only `registryVersion`.
- **Hot-reload** without process restart.
- **A third channel classification** — six render states + thirteen `UnitClass` only.
- **Hand-rolling chart/icon/graph UIs** when the locked presentation libs already fit — buy before build.
- **Enlarging Band B lawn tokens** — not this request; GG-60 stays.
- **Implementing from the five trail specs** with old module ids — trail only.

---

## Trail (do not implement)

| Path | Why kept |
|---|---|
| ~~`spec-actor-sheet-shell.md` (six-tab)~~ | **Replaced** by the catalog-era shell spec at the same path |
| [spec-progression-tab.md](actor-sheet/spec-progression-tab.md) | Aptitudes were “Progression” |
| [spec-derived-stats-tab.md](actor-sheet/spec-derived-stats-tab.md) | Doorway-only, superseded by `derived-tab` |
| [spec-locked-preview-tabs.md](actor-sheet/spec-locked-preview-tabs.md) | Actions/Passives locked — Passives now live |
| [spec-gear-tab.md](actor-sheet/spec-gear-tab.md) | Empty-state only; Kit supersedes |
