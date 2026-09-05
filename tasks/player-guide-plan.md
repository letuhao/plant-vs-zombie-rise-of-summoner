# Player guide — plan

**Program:** `player-guide`
**Paths:** `tasks/player-guide-plan.md` · `tasks/player-guide-todo.md`
**Deliverable:** `docs/guide/` — player/marketing/concept docs; also the **product-vision SSOT**.

**Status:** v1 implemented 2026-09-05. Coverage pass same day. **v2 vision rewrite 2026-09-05** — RPG + empire, ten named loops, lawn first core, idle forever, Dave-level chapters as Vision.

---

## Goal

Centralize and index every player-facing feature and mechanism as a **user guide** folder: concept, loops, glossary, and pillar pages. Audience is players and marketing. Full fantasy, with honest **Shipped / WIP / Vision** labels.

As of guide version 2, this folder is also the **product-vision SSOT**: what the game is and which loops a feature must extend. Architecture docs remain the system SSOT (power ladder, three stocks, Funnel, no player class).

## Non-goals

- Screenshots
- Lingui / in-app help strings
- Developer companion docs or links into `docs/architecture/` from guide pages (DESIGN-GATE may link *to* the guide)
- Rewriting `docs/runbook/players.md` (install stays there)
- Dates or promises
- Documenting cheats, sim, dumps, telemetry, seedsmith
- Implementing Dave-level rail gates in code (separate unlock program)

## Voice

- Second person, fiction words only
- Genre: RPG + empire building; lawn is first core, not the whole war
- Loop one-liner from [docs/guide/the-loops.md](../docs/guide/the-loops.md) / README
- You are Dave; homeworld is capital; win = take Zomboss’s fortress; lose = homeworld falls
- Banned: engine vocabulary (GG-23) — typeId, UniqueActor, Intent, mods_json, Cold, injector, SignalR, module ids, wave numbers, file paths
- You have no class — aptitudes are free-build; classes are Zomboss patterns

## Folder shape (14 files)

```text
docs/guide/
  README.md                 index + legend + full catalog
  the-game.md               concept, lore, win / lose (product vision)
  the-loops.md              ten named loops (product-vision SSOT)
  how-you-play.md           loop, lawn first, unlock ladder live + Vision
  sanctum.md
  creatures.md
  combat.md
  expeditions.md
  the-rift.md
  the-lawn.md
  delves-and-sieges.md
  relics-and-builds.md
  almanac.md
  glossary.md
```

## Wiring

- `docs/README.md` Players row → `guide/` as product vision SSOT, runbook for install
- `docs/DESIGN-GATE.md` §1 → product vision row → `guide/the-game.md` + `guide/the-loops.md`
- Root `README.md` → player guide link; pitch matches guide v2
- `SUPPORT.md` → one-liner after install steps

## Status badge rules

- Map headers and IA win over README marketing when they disagree
- Thin-but-open = Shipped + “in this build” note
- Catalog only grows; Vision stays Vision until it ships
- Live unlock rail = event beats (Shipped); Dave-level chapters = Vision until a separate code program

## Acceptance (v2)

- [x] `the-loops.md` exists with ten named loops
- [x] Pitch pages no longer say “lawn fight / world afterward” or “Fusion is extension”
- [x] Catalog grew (quest log, world stage, farm/hunt/defend, item/power/summon loops, Dave-level unlocks, expedition→quest)
- [x] DESIGN-GATE product-vision row
- [x] Guide version 2 stamp
- [x] This plan pair updated
