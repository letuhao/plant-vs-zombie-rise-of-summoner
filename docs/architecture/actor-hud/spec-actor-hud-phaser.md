# Spec: `actor-hud-phaser`

**Module id:** `actor-hud-phaser` · **Program:** [../actor-hud-map.md](../actor-hud-map.md) ·
**Ideal:** [../actor-hud-ideal.md](../actor-hud-ideal.md) ·
**Pipeline:** [../../research/actor-hud-data-pipeline-audit-2026-08-30.md](../../research/actor-hud-data-pipeline-audit-2026-08-30.md)
**Depends on:** `actor-hud-fold` · **Blocks:** program E2E
**Status:** implemented 2026-08-31 — shipped; `SyncFromModelSystem` tests + Playwright canvas hooks green.

---

## Assumptions

1. **v1 gate:** program is **not done** without Phaser parity (ideal §0, map acceptance rule).
2. **Input:** `Occupant.hud` on view model — same object Inspector uses (fold spec).
3. **Vanilla HP bar** from existing `setHpDisplay` **remains** while `hpSliverEnabled` is false — dual
   bars documented; RPG read is shield + status rows (audit).
4. **Geometry:** band-relative offsets scaled to canvas cell — mirror unity tunable ratios where possible.
5. **No second fold** — Phaser reads model only; never parses raw events.

---

## Objective

Draw Band B HUD chips on the Phaser lawn canvas so a browser spectator sees the same semantics as Unity
world HUD.

**Success:** Unit test with fixture `Occupant` + `hud` → container children include tier badge, shield bar,
status tokens; E2E selects unit and asserts canvas + Inspector agree.

---

## Program acceptance share

`SyncFromModelSystem` unit test (or lawnProjectorFold + phaser harness): fixture occupant with fire shield
+ 2 statuses → `setHudDisplay` creates expected named children. Part of `e2e/actor-hud.spec.ts`.

---

## Commands

```powershell
cd web\fusion-rpg-web
npm run test -- SyncFromModelSystem
npx playwright test e2e/actor-hud.spec.ts
```

---

## Project structure

| Path | Change |
|------|--------|
| `web/fusion-rpg-web/src/game/systems/SyncFromModelSystem.ts` | edit — `setHudDisplay`, call from sync |
| `web/fusion-rpg-web/src/game/systems/ActorHudDisplay.ts` | **new** — pure layout helpers (testable) |
| `web/fusion-rpg-web/src/game/systems/SyncFromModelSystem.test.ts` | **new** or extend existing |
| `web/fusion-rpg-web/e2e/actor-hud.spec.ts` | **new** — program E2E (P6) |

---

## Design

### API

```typescript
function setHudDisplay(
  scene: Phaser.Scene,
  container: Phaser.GameObjects.Container,
  hud: ActorHudSnapshot | undefined
): void;
```

Called from occupant sync after position update, alongside existing `setHpDisplay`.

### Visual mapping (plate 10)

| HUD field | Phaser object | Notes |
|-----------|---------------|-------|
| `identity.tier` | `tierFrame` Graphics or sprite | border color by tier |
| `identity.levelBand` | `levelBadge` Text | mono font, small |
| `identity.role` | `rolePip` Text/icon | demon vs vanilla |
| `resources.shield.stacks` | `shieldBar` rectangles | element colors |
| `statuses[]` | `statusToken_*` Text | 2-letter tokens |
| `overflow.statusCount` | `overflowPip` Text | `+N` |

Named children for test hooks: `hudIdentity`, `hudShield`, `hudStatus0`, `hudOverflow`.

### When `hud` undefined

Remove HUD children; leave vanilla HP bar unchanged.

### Priority / overflow

Use same cap as tunable `statusStripMax` (passed via model or constant from shared TS config mirroring JSON).

---

## Boundaries

- No Unity/injector code.
- Do not duplicate fold logic — if `hud` missing, fold bug not phaser workaround.
- No raw channel ids in player-visible text (GG-23).

---

## Test plan

| Test | Assert |
|------|--------|
| `setHudDisplay_shield_and_statuses` | 2 status children + shield bar width > 0 |
| `setHudDisplay_clears_on_undefined` | HUD children removed |
| E2E | Canvas hooks match Inspector `data-testid` values |

---

## Related

- [spec-actor-hud-fold.md](spec-actor-hud-fold.md)
- [actor-hud-data-pipeline-audit-2026-08-30.md](../../research/actor-hud-data-pipeline-audit-2026-08-30.md) — single fold SSOT; no second shield read
- [SyncFromModelSystem.ts](../../../web/fusion-rpg-web/src/game/systems/SyncFromModelSystem.ts)
- [10-actor-hud.html](../../design/10-actor-hud.html) §C dual render
