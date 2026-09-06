# Spec: `lock-doc-sync`

**Module id:** `lock-doc-sync` · **Program:** [phaser-kernel-map.md](../phaser-kernel-map.md)  
**Status:** Specify complete 2026-09-06 — Wave 0; **patched T0 2026-09-06** (docs only).  
**Depends on:** — · **Blocks:** all Wave 1 modules (poisoned SSOTs otherwise)

**Ideal:** [phaser-kernel-ideal.md](../phaser-kernel-ideal.md) §Gate before `/spec`.  
**Foundation:** [fe-game-foundation.md](../fe-game-foundation.md) · [base-defense/spec-board-render.md](../base-defense/spec-board-render.md)

---

## Objective

Remove contradictory lock-doc claims so implementers and later `/spec` sessions are not corrected
by stale “one Phaser / lawn deferred” prose.

**Success:** the three (plus budget) rows below no longer contradict HEAD; a reader of DPLP §8 sees
`createGame`, `world/`, `board/`, `camera/`, and `game-host/`.

---

## What already exists (verified)

**Built:** world Phaser island; generic `createGame`; `board/` primitives; ScenePOC; locks 1a–5a in
the ideal.

**Stale (ideal §Gate):**

| Doc | Stale claim |
|---|---|
| `decisions.md` Lawn projector row | “Implementation deferred” (W6–W7 shipped) |
| `spec-board-render.md:15–17` | “Exactly one Phaser integration … lawn-shaped” |
| `fe-game-foundation.md` §8 folder tree | `createLawnGame` only — missing world/board/camera/game-host |
| Docs citing occupant budget 50/80 | Code is `PHASER_OCCUPANT_BUDGET = 96` (`pickPhaserOccupants.ts`) |

---

## Contract

Docs-only. No TypeScript dual-track.

**Patch checklist (each row = acceptance):**

1. Align `decisions.md` Lawn projector / DPLP row with shipped W6–W7 + world island.
2. Rewrite `spec-board-render.md` opening: lawn **and** world islands exist; `board/` serves cell
   stages (Decision 40 still siege+battle for discharge).
3. Extend DPLP §8 tree: `createGame.ts`, `world/`, `board/`, `camera/`, `game-host/` (lock 3a).
4. Grep/fix lingering `50`/`80` occupant budget guidance to **96** where it claims to be current.

---

## Dual-track

N/A — documentation.

---

## Commands

```powershell
# After patches, sanity:
rg -n "exactly one Phaser|Implementation deferred" docs/architecture/decisions.md docs/architecture/base-defense/spec-board-render.md
rg -n "PHASER_OCCUPANT_BUDGET|occupant budget|50/80" docs/ web/fusion-rpg-web/src/game/ -g "*.md" -g "*.ts"
```

---

## Testing strategy

Human + `rg` checklist above. No vitest.

---

## Tunables

None.

---

## Boundaries

- **Always:** patch the named rows in the same Wave 0 commit set; cite HEAD.
- **Ask first:** rewriting Decision 40’s two-caller rule (out of scope — do not).
- **Never:** claim Decision 40 discharged; delete world island from docs; invent a third Phaser host.

---

## Success criteria

1. `spec-board-render.md` no longer asserts “exactly one Phaser integration … lawn-shaped.”
2. `decisions.md` lawn projector language matches shipped monitor/Intent (not “deferred”).
3. DPLP §8 lists `createGame`, `world/`, `board/`, `camera/`, `game-host/` (or equivalent accurate tree).
4. No architecture doc still presents 50/80 as the live occupant budget when code is 96.
5. Wave 1 specs can cite these docs without a “stale caveat” paragraph.

---

## Out of module

Code changes; siege unpause; paint flip; relocating `createGame.ts`.
