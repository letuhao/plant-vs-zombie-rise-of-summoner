# item-content — capability map

**Status:** Phase 0 of `/spec`, proposed 2026-09-06, awaiting approval. Source:
[item-content-ideal.md](item-content-ideal.md) — four research passes, each finding sorted
built/wiring-gap/real-gap against current code, with genre prior art.

**Read before proposing against this:** [item-content-ideal.md](item-content-ideal.md) in full — it
holds every citation this map compresses into a one-line module description.

---

## 1. What this program builds, in one paragraph

The item program's atom/container/instance/display layer is real and mostly complete. What is missing is
narrow and specific: a handful of production call sites that would carry already-built or
already-authored content the last few feet to a player — a name, a lore sentence, a preview, and a wider
slice of already-written mechanical descriptions. This program closes those specific gaps. It authors no
new game *mechanics*; every module below either wires an existing function to a real caller, fixes a
loader that drops a field, or fills a tuning file that already has 94 empty rows next to 14 full ones.

## 2. Modules

| Module id | Responsibility | Depends on |
|---|---|---|
| `item-naming` | Wire `ItemNameComposer` into the real card-assembly path; stop discarding authored set/base-type names at load; replace every raw-id/GUID surface in the shipped web UI (compare deltas, combination titles, the armoury list's name fallback, the workbench's typed recipe/container ids) with a real name or a real picker | — |
| `item-lore` | Fix unique-flavour import to read the authored sentence, not just its key; replace the `keyTail()` placeholder with a real string-catalog lookup; resolve and implement the sets-lore decision (ideal doc §8.1) | — |
| `atom-preview` | A read-only preview route accepting an *unsaved* container, and a page that renders it through the existing, unmodified `ItemCardRenderer` — the `render-tree-cards.mjs` / `AlmanacDumpPage.tsx` pattern, adapted, not reinvented | — |
| `affix-draw-coverage` | Author the missing `tier-bands.v1.json` rows for the 95 refused families; add the 2 missing display templates (`chill-punisher`, `rot-punisher`); register the 3 missing `UnitClass` entries (`elpw-focus`/`-overflow`/`-pierce`); widen the "every atom renders" guard's scope once the tuning file admits them | `item-lore` for the guard-widening step only (needs real lore text present to assert against meaningfully) — the tuning-file authoring itself has no dependency |
| `granted-action-text` | Add a description field to `rpg_action`'s schema and to the 114 seeded actions' content; wire it into card block 9, which already expects it | — |

**No cycles.** Four of five modules (`item-naming`, `item-lore`, `atom-preview`, `granted-action-text`)
have no dependency on one another or on `affix-draw-coverage` and can build in any order or in parallel.
`affix-draw-coverage`'s content-authoring half is likewise independent; only its own guard-test-widening
step waits on `item-lore` landing first, so that widening the test's scope doesn't newly assert against
lore text that still reads as a placeholder.

## 3. Build order

```
wave 0   item-naming · item-lore · atom-preview · granted-action-text   (parallel, no shared files)
wave 1   affix-draw-coverage (content pass, independent) · its guard-widening step (after item-lore)
```

Nothing here is sequenced by necessity — this is a suggested order that lands the widest-impact, purely
wiring fixes first (matching the owner's own stated preference for a small, genuinely playable slice
before any larger generation decision), with the one content-authoring module last only because it is
the one piece with a real, if small, authoring cost.

## 4. What is explicitly NOT in this program

- **The seed→concrete item generator itself** — a separate, already-owner-decided program (phased
  rollout). This program does not touch it and does not depend on it; every module here is provable
  against a hand-seeded or lightly-generated item.
- **The `classes.v1.json` v4 full generation run** — held, unrelated to this program's own scope.
- **Localising the 1,075 name keys not yet in `content/display/en.json`** (ideal doc §6.2, §8.2) — those
  already render real English text via inline literals; moving them behind keys is an architecture change
  to the localisation layer, not a content-quality fix, and is named as a separate, lower-priority
  question for the owner rather than folded in here.
- **Any new game mechanic, balance number, or magnitude.** Every module above is presentation, wiring, or
  filling an already-designed tuning file — never a new effect, channel, or formula.

## 5. Paths

| Artifact | Path |
|---|---|
| This map | `docs/architecture/item-content-map.md` |
| Ideal doc | `docs/architecture/item-content-ideal.md` |
| Module specs | `docs/architecture/item-content/spec-<module-id>.md` |
| Plan | `tasks/item-content-plan.md` |
| Task list | `tasks/item-content-todo.md` |

`SPEC.md`, `tasks/plan.md` and `tasks/todo.md` belong to other streams and are never used here.
