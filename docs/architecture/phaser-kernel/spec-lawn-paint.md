# Spec: `lawn-paint` (deferred)

**Module id:** `lawn-paint` · **Program:** [phaser-kernel-map.md](../phaser-kernel-map.md)  
**Status:** Deferred (lock **4c**) — no build authorization until Wave 1b freeze + owner paint pick.  
**Depends on:** `lawn-plane`, `board-contract`

---

## Objective

Replace lawn `ensureGrid` Graphics paint with a `BoardLayers` / terrain path using either a
Graphics-compatible adapter **or** an owner-approved Playwright canvas golden — then delete
`ensureGrid` in the **same flip commit**. Does **not** discharge Decision 40.

---

## Contract (frozen for later)

- **Shadow paint:** same Game, two paint functions compared; then delete old.
- **Not a shadow run:** re-running green `BoardLayers.test.ts` alone.
- **Never:** Vitest as pixel golden; “byte-identical” without Playwright `canvas.screenshot()`.
- Owner picks Graphics-match vs new look before implement.

---

## Dual-track

Shadow → flip commit deletes `ensureGrid` body.

---

## Success criteria (when activated)

1. Playwright golden approved (or Graphics command-list oracle agreed).
2. Live lawn uses board terrain path; `ensureGrid` deleted same commit.
3. Decision 40 still undischarged until siege+battle live callers exist.

---

## Boundaries

- **Never:** flip paint in Wave 1; claim Decision 40 done by lawn-only consumer.
