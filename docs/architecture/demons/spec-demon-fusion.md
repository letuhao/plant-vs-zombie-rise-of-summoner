# Spec: demon-fusion

Status: **shipped 2026-08-21** (all eight owner locks implemented; star merges, promotion, recipe fusion, and the recipe book live in SIM/FE at `#/fusion`; the E2E legendary chain proves commons → legendary purely via fusion). Module id `demon-fusion` in the [demon system map](../demon-system-map.md). Depends on `demon-core`, `soul-economy`, and the expedition material inventory. Consumed later by `patron-demon` (star scaling) and `demon-contracts`.

## Objective

Give duplicates and low-rarity demons lasting value (design rule 2) and open the build-crafting midgame (rule 3): every spare demon is either star-fuel for an individual you keep, or an ingredient in a discoverable recipe. Fusion is the anti-gacha — you always know the species you'll get.

Success looks like: a player feeds three spare commons into their favorite starter and watches its star rank climb; discovers by experiment that two specific rares fuse into an epic; farms expedition essences to afford it; and eventually crafts a legendary through a deep recipe — all offline in the web FE, all through the one economy.

## Locked decisions (owner, 2026-08-21)

1. **Identity — both, by mode:** star merges evolve the BASE (instanceId/nickname/XP/lineage survive; sacrifices consumed); recipe fusions consume ALL inputs and mint the output.
2. **Recipes — both layers:** rarity-band star merges always available; discoverable cross-species recipes (code-generated from the species catalog) as the ceiling, hidden until first success, codex-recorded with discovery Soul bonuses.
3. **Materials — cost + element gate:** rarity-matched shards as cost; the result's primary element demands matching essences; Souls base fee.
4. **Randomness — sure species, rolled extras:** output species guaranteed; traits/variant seeded server-side, correlation-idempotent (summon-pull discipline).
5. **Traits — pick one, roll rest:** player picks ONE guaranteed trait from any input; remaining slots (1/2/2/3 by result rarity) seeded-roll from the combined input pool.
6. **Ceiling — recipes reach legendary;** capture-only species excluded everywhere.
7. **Merge floor — stars + capped promotion:** stars cap by rarity; per-star combat bonuses; a max-star base may promote ONE rarity once, gaining slots (existing traits kept, new slots rolled).
8. **Patron demon un-parked:** builds immediately after this module (separate spec).

## Design

### Star merges (identity-preserving floor)

- `StarPolicy` (Core): star caps by rarity — common 3★ / rare 4★ / epic 5★ / legendary 5★. Sacrifice cost to reach star n: n+1 specimens of the base's CURRENT rarity band (any species). Per-star combat bonus: **+30‰ power and +30‰ defense** on the omni channels, materialized as flat `BattleChannelMod`s from the specimen's level stats at squad-build time — the engine and its goldens never change.
- Promotion (once, at max stars): rarity +1, `promoted` flag set, star rank resets to 0, trait slots grow to the new rarity's count — existing traits KEPT, new slots seeded-rolled from the species pool. Shard cost jumps one band.
- Sacrifices must be: owned, phase Roster, not locked, not on an expedition, not the base. Locked = player-protected from consumption (the lock finally grows teeth).

### Recipe fusion (minting ceiling)

- `DemonRecipeCatalog` (Core): built deterministically from `DemonSpeciesCatalog` at startup (WaveCatalog pattern — code, no capture data): every summonable rare/epic/legendary species gets exactly one recipe. Inputs: two species from the band below whose elements relate to the output (primary match + ring-adjacent or secondary donor). Capture-only species get no recipe and appear in no recipe. Validation at startup (unknown ids, cycles, band violations reject — fail fast like the species catalog).
- Recipe inputs are SPECIMENS of those species (any stars; stars are not refunded). All inputs consumed. Output minted at level 1, origin `fusion`, variant/traits rolled from the fusion seed; the pick-one trait comes from the request (validated ∈ combined input trait set).
- Discovery: `rpg_fusion_discovery` rows per player; undiscovered recipes render as silhouettes (inputs visible once you own both species' codex entries — the experiment hint; the cost's essence element is ALSO shown deliberately, as the breadcrumb that lets a player stock the right essence for the experiment). First success awards discovery Souls by output rarity (existing `discovery` reason, dedupe `recipe:{id}`) — the stored flag replays with the correlation, so a lost response still shows its reveal. A species first obtained via fusion (or an expedition wild-join) pays the same `species:{id}` discovery bonus a summon would — one discovery policy across every acquisition path.

### Costs (both modes)

| Result band | Souls | Shards | Essences (result's primary element) |
|---|---|---|---|
| star merge (per merge) | 50 | 1 × base rarity | 1 |
| promotion | 200 | 3 × NEW rarity | 3 |
| recipe → rare | 150 | 2 × common | 2 |
| recipe → epic | 400 | 3 × rare | 4 |
| recipe → legendary | 1000 | 4 × epic | 8 |

(Numbers are spec-initial; tuning is ask-first per the balance boundary.)

### Data

- `rpg_demon_profiles`: `star INTEGER DEFAULT 0`, `promoted INTEGER DEFAULT 0` (EnsureColumn).
- `rpg_demon_lineage`: append-only (instance_id, event `star-merge|promotion|recipe-birth|consumed-by`, detail_json, t) — the individuals-with-history rule made durable.
- `rpg_fusion_log`: per-player UNIQUE correlation, mode, inputs_json, output_json, seed TEXT, t — replay returns the stored outcome (summon-log pattern).
- `rpg_fusion_discovery`: (player_id, recipe_id) PK.
- Consumption = phase → `Retired` (soft; profile + lineage survive for history; roster/deploy/expedition surfaces filter Retired).
- `ExecuteFusion` — ONE gate-serialized transaction (summon discipline): replay-check → validate inputs/costs → spend Souls → spend materials → consume sacrifices → mutate base OR mint output → lineage rows → discovery + Souls bonus → log. Refusals write nothing; mid-sequence failure leaves zero rows.

### Server + FE

- `POST /api/fusion/preview` (pure read: given base/inputs → mode, costs, result species or `recipe.unknown`, pickable traits); `POST /api/fusion/execute` (correlationId ≤64, server seed, hub pushes DemonsUpdated/SoulsUpdated); `GET /api/fusion/{playerId}/recipes` (discovered + silhouettes). Wire projections never leak seeds.
- FE `#/fusion`: lab with base slot + sacrifice tray (locked/expedition specimens greyed), recipe browser with silhouettes, cost panel (Souls/shards/essences with have/need), pick-one trait selector, result reveal; star pips render on roster cards here and in `#/demons`/`#/expeditions`.
- Battle integration: `WebMatchService.BuildSquad` reads `star` and appends the per-star channel mods. This is the only battle-facing change; battle goldens are untouched (stars enter as ordinary ChannelMods in setups).

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests      # recipe catalog validation, star policy, fusion roller goldens
dotnet test tests\FusionRpg.Data.Tests      # ExecuteFusion atomicity, consumption, lineage
dotnet test tests\FusionRpg.E2E.Tests       # SIM full loops: merge, promote, discover, legendary chain
.\scripts\guard-dal.ps1
```

## Structure

```
src/FusionRpg.Core/Demons/Fusion/   → DemonRecipeCatalog.cs, StarPolicy.cs (incl. FusionCostTable), FusionRoller.cs
src/FusionRpg.Data/Sqlite/          → RpgStore.Fusion.cs (+ schema/EnsureColumn), roster Retired filter
src/FusionRpg.Server/               → FusionEndpoints.cs
web/fusion-rpg-web/src/features/fusion/ + lib/bus/fusion.ts
tests/                              → Core catalog/policy/roller, Data atomicity, E2E loops, Vitest cost math
```

## Code style

Catalog discipline everywhere (unknown species/recipe/trait/material ids reject at write gates); pure Core policies with injected `SeededRng` streams (`fusion:traits`, `fusion:variant`); integer per-mille; store transactions mirror `ExecuteSummon`.

## Testing strategy

- **Catalog:** every non-capture rare+ species has exactly one valid recipe; determinism (two builds identical); band/element rules hold as properties.
- **Roller goldens:** fixed seeds lock trait/variant rolls; pick-one validation (outside combined pool rejects).
- **Atomicity:** forced mid-sequence failure ⇒ zero rows across profiles/actors/ledger/materials/lineage/log; replay returns stored outcome; refusals (insufficient Souls/materials, locked sacrifice, expedition member, retired input) write nothing.
- **Consumption:** Retired specimens vanish from roster/deploy/dispatch surfaces but keep lineage; consumed-by rows point at the surviving base/output.
- **E2E (SIM):** seed → summon dupes → star-merge ×3 → stars visible in roster + battle setup channel mods present; promotion at cap; recipe discovery awards Souls once; legendary chain (farm materials via force-due expeditions → epic recipe → legendary recipe); collect/battle replay adds nothing.
- **FE Vitest:** cost math + have/need display, star pip rendering, silhouette gating.

## Boundaries

- **Always:** one transaction per fusion; correlation idempotency; seeds server-minted and never on the wire; lineage rows for every mutation; locked specimens unconsumable.
- **Ask first:** cost-table or star-bonus tuning (game balance); recipe-graph shape changes; making stars affect PvZ deploy before `patron-demon`.
- **Never:** SQL outside Data; a fusion that can fail after spending; minting capture-only species; deleting specimen rows (Retired only); touching BattleEngine/goldens.

## Success criteria

1. Star merge, promotion, and recipe fusion each playable end-to-end in SIM/FE. 2. Recipe catalog validated + deterministic; discovery loop pays once. 3. A legendary is reachable purely via fusion (E2E chain proves it). 4. Fusion atomicity proven by forced-failure test. 5. Stars measurably swing web battles via squad channel mods. 6. All suites + guards green.
