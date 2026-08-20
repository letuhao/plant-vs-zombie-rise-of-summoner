# Spec: standalone-charter (wave 1)

Module id `standalone-charter` in the [standalone RPG map](../standalone-rpg-map.md). Pure architecture module — docs, decisions, and one profile constant; no feature code. Everything else in the program cites this charter.

## Objective

Make it official and enforceable that **the RPG is the game and PvZ is an extension**: define the mode taxonomy, the gameless-first rule, and the identity web-mode matches carry through the pipeline — so every later module builds on declared architecture instead of convention.

## The charter (normative once approved)

### 1. Mode taxonomy

| Mode | Producer | Source tag | Game profile | Status |
|---|---|---|---|---|
| **Web RPG** (core) | Server `BattleEngine` / expedition resolver | `web` | `webrpg-1` | primary — must always work |
| **PvZ run** (extension) | Injector in `PlantsVsZombiesRH.exe` | `injector` | `pvzrh-3.8.1` / `pvzrh-3.9` | optional enricher |
| **SIM** (dev/test) | `/api/sim/*` under `FUSIONRPG_SIM=1` | `sim` | any | dev-only, CI backbone |

`webrpg-1` joins the game-profile vocabulary (event `game` field, runs rows). Web-mode matches are real runs: they mint `matchKey`s, write `runs`/facts/ledgers through the **same ingest** as injector events — one economy, source-tagged, never forked.

### 2. Gameless-first rule

Every RPG feature must be **fully playable and CI-provable with the PvZ game closed**. The injector may *enrich* a feature (exclusive capture, boosted earn, second battlefield, trophies — the four adopted extension roles), never *gate* one. Acceptance tests for RPG features run in SIM/web mode; a feature that only works with the game open is incomplete by definition.

### 3. Authority rule

All web-mode outcomes resolve **server-side** with seeded, recorded RNG and correlation-idempotent commands (summon-roller precedent). The FE renders state and sends commands; it never computes an outcome the server merely trusts.

### 4. What does not change

Injector Hot-path invariants (single writer, Funnel, no-Data-in-hot-plane), DAL boundary, guard scripts, MatchRuntime/UniqueActor FSMs, and the whole PvZ capture pipeline stay exactly as locked. This program adds a peer producer beside the injector; it subtracts nothing.

## Deliverables

1. This spec approved → its charter section becomes normative.
2. `decisions.md` amendment: "Standalone-first: web RPG is the core game; PvZ is extension gameplay (4 roles); gameless-first rule" + the `webrpg-1` profile row.
3. [software-architecture.md](../software-architecture.md) §1/§3 updated: top-level shape gains the web-mode producer; the overlay principle section notes it governs *PvZ mode* specifically.
4. `RpgConstants`/game-profile catalog gains `webrpg-1` (constant only; first consumer is `match-source-core`).
5. Doc map rows for this program.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests    # constant + profile catalog tests only
```

## Boundaries

- **Always:** cite this charter from later standalone-program specs; keep the one-economy rule explicit in every ledger-touching design.
- **Ask first:** anything that weakens gameless-first (a feature gated on the real game); new modes beyond the three.
- **Never:** remove or degrade the injector path; fork economies by source; client-side outcome authority.

## Success criteria

1. decisions.md + software-architecture.md updated and consistent with this charter. 2. `webrpg-1` exists in the profile vocabulary with a unit test. 3. The demon-program specs' SIM-provability requirements now trace to a named rule (gameless-first) instead of convention.
