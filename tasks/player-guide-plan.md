# Player guide — plan

**Program:** `player-guide`
**Paths:** `tasks/player-guide-plan.md` · `tasks/player-guide-todo.md`
**Deliverable:** `docs/guide/` — player/marketing/concept docs; also the **product-vision SSOT**.

**Status:** Current through guide **v11** (2026-09-05). Started as v1 the same day; v2 locked RPG + empire + ten loops; later passes added features brief, HTML vision site, mechanisms handbook, teach pages (Local control room, Souls), and hygiene (v11).

---

## Goal

Centralize and index every player-facing feature and mechanism as a **user guide** folder: concept, loops, glossary, pillar pages, market brief, HTML skim, and mechanisms handbook. Audience is players and marketing. Full fantasy, with honest **Shipped / WIP / Vision** labels.

This folder is the **product-vision SSOT**: what the game is and which loops a feature must extend. Architecture docs remain the system SSOT (power ladder, three stocks, Funnel, no player class).

## Non-goals

- Screenshots (PG-F1)
- Lingui / in-app help strings
- Developer companion docs or links into `docs/architecture/` from guide pages (DESIGN-GATE may link *to* the guide)
- Rewriting `docs/runbook/players.md` (install stays there)
- Dates or promises
- Documenting cheats, sim, dumps, telemetry, seedsmith
- Implementing Dave-level rail gates in code (separate unlock program)
- Fleshing every mechanism stub in one pass (PG-F6 waves)

## Voice

- Second person, fiction words only
- Genre: RPG + empire building; lawn is first core, not the whole war
- Loop one-liner from [docs/guide/the-loops.md](../docs/guide/the-loops.md) / README
- You are Dave; homeworld is capital; win = take Zomboss’s fortress; lose = homeworld falls
- Banned: engine vocabulary (GG-23) — typeId, UniqueActor, Intent, mods_json, Cold, injector, SignalR, module ids, wave numbers, file paths
- You have no class — aptitudes are free-build; classes are Zomboss patterns
- Teach pages: blind-user spots first; no math or source code on player pages

## Folder shape

```text
docs/guide/
  README.md                 index + legend + full catalog (version stamp)
  features.md               market feature brief
  the-game.md               concept, lore, win / lose (product vision)
  the-loops.md              ten named loops (product-vision SSOT)
  how-you-play.md           loop, lawn first, unlock ladder live + Vision
  sanctum.md … almanac.md   pillar pages
  glossary.md
  mechanisms/               handbook index + stubs + teach pages
    _content/               JSON SSOT for rendered teach pages
    _render.py              md + site HTML from JSON
  site/                     HTML vision skim (tabs)
    mechanisms/             HTML teach pages (when rendered or hand-authored)
```

## Wiring

- `docs/README.md` Players row → `guide/` as product vision SSOT, runbook for install
- `docs/DESIGN-GATE.md` §1 → product vision row → `guide/the-game.md` + `guide/the-loops.md`
- Root `README.md` → player guide link; pitch matches guide
- `SUPPORT.md` → one-liner after install steps

## Status badge rules

- Map headers and IA win over README marketing when they disagree
- Thin-but-open = Shipped + “thin” note; fiction rule live = Shipped (fiction)
- Catalog only grows; Vision stays Vision until it ships
- Live unlock rail = event beats (Shipped); Dave-level chapters = Vision until a separate code program

## Acceptance (current)

- [x] `the-loops.md` — ten named loops
- [x] Pitch is RPG + empire, lawn first core (not “Fusion is extension”)
- [x] `features.md` + `site/` HTML skim + Mechanisms tab
- [x] Mechanisms handbook (~67 ids); teach pages for Local control room + Souls
- [x] HTML sibling links fall back to `.md` when no teach HTML exists
- [x] Guide version stamp on README; PG todo tracks each version pass
