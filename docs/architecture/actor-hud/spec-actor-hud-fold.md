# Spec: `actor-hud-fold`

**Module id:** `actor-hud-fold` · **Program:** [../actor-hud-map.md](../actor-hud-map.md) ·
**Ideal:** [../actor-hud-ideal.md](../actor-hud-ideal.md) ·
**Pipeline:** [../../research/actor-hud-data-pipeline-audit-2026-08-30.md](../../research/actor-hud-data-pipeline-audit-2026-08-30.md)
**Depends on:** `actor-hud-core`, `actor-hud-dump` (observe contract) · **Blocks:** `actor-hud-phaser`
**Status:** implemented 2026-08-31 — shipped; `lawnProjectorFold.test.ts` + Inspector green.

---

## Assumptions

1. **Fold is pure** — `lawnProjectorFold.ts` maps events → `LawnViewModel`; no React in projector.
2. **`actorHud` on wire** matches Core DTO camelCase (dump spec).
3. **Inspector is expansion, not a second layout** — same `Occupant.hud` fields rendered as chip row +
   compact labels; demote duplicate KeyValue shield/chips when `hud` present (audit §2.3).
4. **Status ids:** strip membership is live instances whose ids are in the injected **status-catalog**
   (ideal §4.1). Shipped fold extended `OBSERVE_CHIPS` to custom VFX ids — that list is a **migration
   mirror**, not the long-term roster SSOT. After catalog inject, iterate catalog; snapshot ids ⊆ catalog.
5. **`ptr` / `instanceId` debug rows** stay in Inspector for dev — not primary player readout (GG-23).
6. **Token paint:** Inspector/Phaser status glyphs resolve `hudToken`/`color`/`displayName` from the
   same catalogs as Unity (H2). Do not hardcode initials maps as player SSOT.

---

## Objective

Project injector `actorHud` into `Occupant.hud` on the lawn view model so Phaser and Inspector read one
fold SSOT.

**Success:** Fold test feeds `entity.stats` with `actorHud` → `findOccupant(m, ptr).hud` populated with
shield stacks and statuses; selecting unit shows matching chip row in Inspector.

---

## Program acceptance share

`web/fusion-rpg-web/src/features/lawn/lawnProjectorFold.test.ts` — cases:
`actorHud_from_entity_stats`, `actorHud_from_board_stats`, `status_chips_extended_to_custom_ids`.

---

## Commands

```powershell
cd web\fusion-rpg-web
npm run test -- lawnProjectorFold
npm run build
```

---

## Project structure

| Path | Change |
|------|--------|
| `web/fusion-rpg-web/src/features/lawn/lawnViewModel.ts` | edit — `ActorHudSnapshot` type, `Occupant.hud?` |
| `web/fusion-rpg-web/src/features/lawn/lawnProjectorFold.ts` | edit — `foldActorHud`, map on stats/board-stats |
| `web/fusion-rpg-web/src/features/lawn/LawnPage.tsx` | edit — Inspector `ActorHudInspector` section |
| `web/fusion-rpg-web/src/features/lawn/ActorHudInspector.tsx` | **new** — chip row from `hud` |
| `web/fusion-rpg-web/src/contract/types.ts` | edit — export shared types if needed |

---

## Design

### TypeScript types (align with core)

```typescript
export type ActorHudTier = "normal" | "elite" | "boss" | "unique";
export type MagnitudeBand = "low" | "mid" | "high";

export type ActorHudSnapshot = {
  identity: {
    tier: ActorHudTier;
    role: "specimen" | "vanilla" | string;
    levelBand?: number;
    flags: string[];
  };
  resources?: {
    shield?: {
      hp: number;
      max: number;
      stacks: { element: string; hp: number; max: number }[];
    };
    hpSliver?: { ratio: number };
    meters?: { id: string; ratio: number }[];
  };
  statuses: {
    id: string;
    cc: boolean;
    magnitudeBand: MagnitudeBand;
  }[];
  overflow: { statusCount: number };
};

export type Occupant = {
  // ... existing fields ...
  hud?: ActorHudSnapshot;
};
```

### Fold rules

| Event | Action |
|-------|--------|
| `entity.stats` | If payload has `actorHud`, set `occupant.hud = foldActorHud(p.actorHud)` |
| `debug.board-stats` | Merge `actorHud` per plant/zombie row when upserting living |
| `debug.actor-hud` | Patch single occupant by ptr |
| Shield break | When `actorHud.resources.shield` absent or max 0, clear resource row |

`foldActorHud` validates shape; malformed payloads **omit** `hud` (do not invent defaults).

**Fold must not** compute tier, level, or role from `typeId`, REST, or Unity fields — only maps wire
`actorHud` from injector builder ([pipeline audit §5](../../research/actor-hud-data-pipeline-audit-2026-08-30.md)).

### Legacy deprecation table (transitional)

| Legacy path | Forward SSOT | Retire when |
|-------------|--------------|-------------|
| `rpgShieldHp` / `rpgShieldMax` on entity row | `hud.resources.shield` | Fold + Inspector prefer `hud`; migration complete |
| `statusChips` comma text + OBSERVE_CHIPS fold | `hud.statuses[]` | Inspector `ActorHudInspector` + strip wired |
| Inspector KeyValue `Shield` / `Chips` rows | `Occupant.hud` sections | When `selected.hud` present — hide duplicates |

Inspector **must prefer `hud`** when present; legacy KeyValue is fallback only during transition.

### OBSERVE_CHIPS extension (shipped; catalog supersedes roster)

Shipped: extend hardcoded chip set to 13 custom ids from `StatusVfxIdentity.CustomIds`:

`wither`, `blight`, `rot`, `spark`, `spore`, `pact_mark`, `leech`, `expose`, `shatter`, `bond`,
`rally`, `command`, `charm_pulse`

**Amend:** once `status-catalog` injects, FE/Inspector iterate the catalog for membership and
`hudToken`/`color`; keep vanilla engine chips only as needed until HUD strip owns them. Do not grow
a parallel FE union as the roster.

### Inspector (`ActorHudInspector`)

When `selected.hud` present:

- Render identity row: tier frame, role pip, level badge (reuse design tokens / small components)
- Resource row: shield segments by element color
- Status strip: catalog tokens + `+N` overflow
- **Hide or collapse** redundant KeyValue `Shield` and `Chips` rows that duplicate `hud`

When `selected.hud` absent: fall back to existing KeyValue (transition period).

---

## Boundaries

- No Phaser drawing in this module — phaser spec owns canvas.
- No Unity code.
- Do not fetch `/api/actors/.../derived` for HUD fields.

---

## Test plan

| Test | Assert |
|------|--------|
| `entity.stats` with `actorHud` | `occupant.hud.statuses.length === 2` |
| Board-stats merge | Two zombies, only one with hud |
| Missing `actorHud` | `hud` undefined; legacy `rpgShield` still works |
| Custom status id in fold | `spark` appears in chip path when not yet in hud |

---

## Related

- [spec-actor-hud-dump.md](spec-actor-hud-dump.md)
- [spec-actor-hud-phaser.md](spec-actor-hud-phaser.md)
- [actor-hud-data-pipeline-audit-2026-08-30.md](../../research/actor-hud-data-pipeline-audit-2026-08-30.md)
- [lawnProjectorFold.ts](../../../web/fusion-rpg-web/src/features/lawn/lawnProjectorFold.ts)
