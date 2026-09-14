# Spec: `species-rank`

**Module id:** `species-rank` · **Program:** [creature-seed](../creature-seed-map.md) · **Build order:** after `threat-band`, beside `rarity-migration`; before any gate reads it.
**Depends on:** `anchor-contract` (DERIVED field slot), `threat-band` (rung table), `rarity-migration` (shared ladder).
**Authored from:** [gameplay-tiers-ideal.md](../gameplay-tiers-ideal.md) + owner answers 2026-09-12 (10 ranks mirroring rarity · threat×rarity grid · full-gate scope · this program).
**Amendments over the parent ideal (recorded, not silent):** the ideal's Q1 decided 5–8 ids with rarity-only banding; owner answers overrule with 10 mirrored ids and a 10×10 grid (same date). The ideal's gate list (fusion floor + display) is extended by owner answer to wild/expedition/wave gates. Dissent from this session's adversarial debate (cut grid to 10 rows; cut non-fusion gates; alias rarity) is recorded in Open Questions — the owner answers stand unless the owner overturns them on review.
**Status:** proposed. No build authorized until owner reviews this spec.

## ASSUMPTIONS I'M MAKING

1. Rank ids mirror rarity ids 1:1 (`chaff`…`almanac`) as *separate* enum values — DECIDED, owner-confirmed 2026-09-12: each tier/rank axis owns its own closed vocabulary; unifying all ranks into one shared enum is lazy design (it collapses distinct narrative axes — scarcity vs hunting fantasy — into one, so the vocabulary can never say anything the other doesn't). Precedent: the `common→chaff` migration orphaned persisted `shard.*` ids (`spec-rarity-migration.md:84`). If you wanted DQM letters instead, say so — swap cost is one enum + tuning rewrite, no logic change.
2. The 10×10 grid ships diagonal-by-default (rank rung = rarity rung; threat dimension resolves through the same table but defaults to the diagonal cell). Balance authors divergence later in tuning, never in code. Every one of the 100 cells validates against the closed vocabulary at load.
3. Rank gates join as *additional* floors defaulting to pass-through (bottom rank), so landing this module moves zero goldens and changes zero behavior until a gate is explicitly tuned above bottom.
4. `threatBand`/`rarity` values read here include honestly-stamped `deterministic-fallback` rungs; only literal `unresolved` blocks rank (skip, don't fabricate) — with the sentinel mapping in §1.7, since Python emits the string `"unresolved"` while C# models honest gaps as null.
5. This is a C# + seedsmith-Python + tuning-JSON change with no FE framework choice to make (web reads the same DTOs) and no new packages.

→ Correct me now or implementation proceeds with these.

## Objective

Give every creature species a fixed, near-cosmetic **rank**: deterministic identity derived from its already-classified `threatBand` × `rarity`, used only for display and for the §6 gates — never a magnitude input. Players get a memorable hunting-fantasy label; gates get a creature-side vocabulary decoupled from future shared-ladder changes. Success: all resolved species carry a rank, reruns byte-identical, zero golden moves, §6 gates green at pass-through defaults.

## 1. Derivation contract

Rank is a `DERIVED` anchor field: deterministic code over the resolved `(threatBand, rarity)` pair through the tuning grid. The model never authors it, nothing votes on it.

- **Seedsmith edits:** `anchor/schema.py` — rank joins `DERIVED_FIELDS` (auto-propagated from `OWNERSHIP` at `:58-73`); add the hand-written `build_anchor_schema()` property entry (`:114-146`) **plus a `descriptions.py` entry**, else `_desc` raises `KeyError` (`:79-83`). `anchor/derive.py` — new `derive_rank(threatBand, rarity, tuning)` beside `derive_posture`/`derive_pure`; unresolved either input → `"unresolved"` string (matching `derive.py:24` convention, not `None` — the `None`/null mapping happens at the C# boundary, §1.7). `audit.py` needs **no** edit (string-enum passes `numeric_audit`, `audit.py:83-135`; "rank" is not in `MAGNITUDE_DENY_NAMES` `:26-28`).
- **Call sites (all three, or ranks go stale):** pipeline finalize `run/runner.py:_finalize` (`:842-849`, the derive family's one real caller) · `fix_unresolved` second posture/pure recompute (`:507-544`, rank recomputes after fallbacks stamp) · pipeline-scoped-rerun merge path (`:767-774`, `:851-855` — a threat/rarity-only rerun must recompute rank). No ordering constraint vs `derive_posture`/`derive_pure` (rank reads only threat×rarity). `anchor/emit.py:20-27` picks the field up automatically — no edit, stated so nobody adds one.
- **C# boundary (§1.7 sentinel mapping):** Python's `"unresolved"` string maps to C# `null` on `AnchorRow.rank` (new nullable field beside `AnchorRow.cs:34-40`; note `Rarity` there is non-nullable and throws on missing at `:75` — this spec explicitly does **not** change `Rarity` nullability, only adds nullable `rank`). `ThreatBand` is already nullable (`:76-77`); same shape.
- **Expansion:** `SpeciesExpander.cs` computes rank post-expansion (`:66-68` theta path untouched — rank excluded from `theta`/`pTheta` by construction, never by a flag); unresolved list (`:35-44`) gains rank-skipped reporting beside the existing skip.

## 2. Tuning table

`data/tuning/creature-rank.v1.json` (NEW — glob confirms absent): closed rank vocabulary (10 ids mirroring rarity) + 100-cell `threatBand × rarity → rank` grid, diagonal default (`{ "threatBand": "warden", "rarity": "fused", "rank": "fused" }` — diagonal per `creature-rarity-power-fallback.v1.json:8`). Precedent note, stated honestly: the fallback file is a **ten-row 1:1 list** (`:4-13`), not a grid — the grid's own justification is future off-diagonal divergence without migration, not precedent. Threat selects the *row*, rarity selects the *column*, the cell holds the rank — the deterministic counterpart to the model's "never conflate" instruction (`descriptions.py` rarity `:75`, threatBand `:64-69`); threat never moves a magnitude here because rank never enters one (§7 gate rule below + Never list).

## 3. Ladder helpers

`CreatureRank.cs` (NEW, absent — glob confirms) + `CreatureRankLadder.cs` (NEW): `AtLeast`/`AtMost`/`RungsBelow`/`OneRungAbove`/`All`, mirroring `CreatureRarityLadder.cs:46` (`AtLeast` impl; `:49` `AtMost`; `:51-52` `All`; `:12` `RungCount = 10`; `:16-21` top-throw; prose `:3-9`). New/extended guard test beside `tests/FusionRpg.Guard.Tests/CreatureRarityLadderGuardTests.cs:36,45` (existing regexes are `CreatureRarity`-specific — rank needs its own; `audit-magic-numbers.py` scans only `*Policy|Rules|Catalog|Math.cs`, so the ladder file trips nothing either way).

## 4. Persist + catalog-runtime

- **DAL (sole writer):** `RpgStore.Species.cs` — `EnsureColumn` precedent (`:57-65`, def `RpgStore.cs:3893`); new column **nullable** (a `NOT NULL DEFAULT` would fabricate a rank, violating Assumption 4); `ReadStoredUnlocked` (`:261-316`) handles null; `SameContent` (`:318-329`) compares rank or reimports drift forever; INSERT + `ON CONFLICT` lists (`:123-141`) name rank; all-or-nothing validator (`:75-103`) needs **no** rank refusal (null is non-failing — one line, stated).
- **Catalog:** `CreatureSpeciesDef` (`CreatureSpeciesCatalog.cs:6-46`) gains rank; `Validate()` (`:98-127`) treatment named in code. Rank reaches every gate through `ConcreteSpeciesMapper.cs:82-109` — the ONE `ConcreteSpecies→CreatureSpeciesDef` funnel both hosts share.
- **Concrete triad:** `ConcreteSpecies.cs:13-85` (new record field) · `ConcreteSpeciesSerializer.cs:15-41` (`Canonical` dict — every added key rewrites all generated files; regen covered below) · `ConcreteSpeciesSeedReader.cs:27-56` (Injector read path; `Str()` throws on missing keys → optional-read handling for rank).
- **Injector half (not covered by the DAL edit):** Server is already store-backed (`Server/Program.cs:363-370`); the Injector bypasses SQL via committed-tree reads (`Injector/Host/RpgHost.cs:113-125` → SeedReader + Mapper) — rank must ride SeedReader/Mapper or Injector gates never see it. (Doc hazard, bundle with this change: `SpeciesSnapshot.cs:78-91` still claims every host calls `ConfigureFromCompiledDefault` — stale vs `Program.cs:370`.)
- **Regen:** `data/seed/creatures/species/**` + `data/generated/creatures/**` regenerated (`CreatureSpeciesGen --check`, `Program.cs:35` flag, `:101-119` impl, will fail until regen — expected, not drift).

## 5. Display payloads

There is **no almanac/species DTO** — `/api/almanac/*` serves dump/seed text (`Server/Program.cs:1190-1276`), never species. Real payloads: `GET /api/creatures/catalog` anonymous projection (`Server/CreatureEndpoints.cs:19-36`, rarity at `:30`) · `CreatureProfileDto` + `ListCreatureRoster` (`RpgStore.Creatures.cs:154-201`) · codex (`:203-229`) · fusion preview `resultRarity` (`FusionEndpoints.cs:205-216`) · summon outcome specimens. Rank id + display name join these; display copy lives in the runtime **catalog** file, never the number file (tunables-ssot T7/T8).

## 6. Gates

Each gate reads rank through named-threshold helpers with its default set to pass-through (bottom rank). Behavior identical to pre-rank until a floor is explicitly tuned above bottom — proven by pass-through tests, not review. Null rank maps to bottom **at each gate** (explicit — `AtLeast(null,…)` would crash; Assumption 3's pass-through depends on this line).

| Gate | Enforcing site (not just preview) | Rank join | Slice |
|---|---|---|---|
| Fusion promotion | `RpgStore.Fusion.cs:154-196` (gate `:161-164`); preview `FusionEndpoints.cs:148-157` — both, or preview/enforce diverge | rank floor beside `CanPromote` rarity/star/promoted check; preview mirrors it | **Landing** |
| Fusion recipe eligibility + inherit picks | `CreatureRecipeCatalog.cs:198-204` (`EligibleOutputs`); `RpgStore.Fusion.cs:240,274-279`; preview `FusionEndpoints.cs:187-194` (`AtLeast(sourceRarity, OutputEligibilityFloor)` at `:190`; `OutputEligibilityFloor = Cultivated` at `CreatureRecipeCatalog.cs:39`) | rank floor beside each `AtLeast` rarity check (`FusionTuning.cs:76,99` are loader loops building cost dicts — nothing joins there) | **Landing** |
| Expedition wild bands | `ExpeditionResolver.cs:227-244` (`RollWildSpecies` `:227-236`, 840/990 roll, Chaff fallback `:234`; `WildBand` filter `:238-244` — note `:242` excludes `Sunwoven`, and `WildBand` admits `EventOnly` while `SummonRoller` demands `Summonable` at `:152-153`: pinned, not fixed here) | rank floor on the band filter | Follow-up (own pass-through proof) |
| Wave bands | `WaveCatalog.cs:127-130` (`Band(Chaff/Cultivated/Heirloom/Sunwoven)`; `Band` `:153-154` has no acquisition filter — CaptureOnly can march: pinned, not fixed here) | rank floor on band membership | Follow-up (own pass-through proof) |
| Delve wild selection | **No Delve-side band selector exists** — the repo's only `WildBand` is Expedition's. Real candidates: `Cage.OccupantEligible` (`Delve/Wild/Cage.cs:19`, per-candidate bool) and pray-path `SummonRoller.Roll` (`Delve/Wild/AltarPull.cs:59`); talk/cage commit caller-assembled specs (`DelveWildEndpoints.cs:22-27`). Pricing stays rarity-keyed: `OfferPricing.Contract(rarity, thetaRoom, …)` (`Core/Delve/Wild/OfferPricing.cs:64`, via `ContractPolicy.RitualPrice` at `Contracts/ContractPolicy.cs:161`) — rank never prices (Code Style rule). | rank floor on `Cage` eligibility | Follow-up (own pass-through proof) |
| Delve encounter display | `ThreatWindow(FloorRung, CeilRung)` (`SlotFilter.cs:109`) is a generator filter tuple with zero Server/FE consumers — "display carries rank" is not a change at any site. Rank reaches Delve through `ConcreteAnchor.From` corpus join (`SlotFilter.cs:25-79`) carrying rank; display reads it from the anchor payload (§5). Filter logic unchanged; null-threat throw (`:143-144`) unchanged. | corpus-join carries rank; no filter change | **Landing** (with §5 payloads) |

**No recompute on promotion:** `FusionRoller.SlotsFor` reads rarity-keyed `SlotsByRarity` (`FusionRoller.cs:81`, untouched); promotion changes *specimen* rarity, never *species* rank — nobody recomputes, by design (rank is species-identity, promotion is specimen-state).

## 7. Content-hash decision

`ContentHashRegistry.cs:44-351` (V1–V9) covers effect/power/rarity/action tables only — **no `creature_species` table is hashed**, so 904 regenerated files move **zero** hash goldens (evidence, not assumption). Display-only payload additions ride §5 DTOs (no hash covers them). Therefore: landing moves zero goldens by construction; the pass-through tests are the enforcement, not review. `CreatureQualityReport` gains a rank-coverage line beside unresolved-rate (`Program.cs:131-140`; reports-only `:23-25`; diversity section `:160-209` reads the new tuning vocab) — reports-only, non-blocking, and ordered after `AnchorRow.rank` exists.

## Tech Stack

C# / .NET (Core domain + Data DAL + Server endpoints), Python (seedsmith anchor derive), versioned tuning JSON. No new dependencies. Test: xUnit (`FusionRpg.Core.Tests`, `FusionRpg.Data.Tests`, `FusionRpg.Guard.Tests`), seedsmith pytest, `audit-magic-numbers.py`, `guard-dal.ps1`.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CreatureRank"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Fusion"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Guard"
dotnet test tests/FusionRpg.Data.Tests
cd tools/seedsmith; python -m pytest tests/ -q -k "anchor or rank"
python scripts/audit-magic-numbers.py --targets M1
.\scripts\guard-dal.ps1
dotnet run --project tools/CreatureQualityReport
```

## Project Structure

```text
tools/seedsmith/seedsmith/adapters/creatures/anchor/
  schema.py            EDIT — rank joins DERIVED_FIELDS (:58-73) + build_anchor_schema property (:114-146) + descriptions.py entry (KeyError otherwise)
  derive.py            EDIT — derive_rank(threatBand, rarity, tuning); unresolved→"unresolved" string (C# maps to null, §1)
  run/runner.py        EDIT — wire into _finalize (:842-849) + fix_unresolved recompute (:507-544) + scoped-rerun merge (:767-774, :851-855)
  anchor/emit.py       READ — picks the field up automatically (:20-27), no edit
  anchor/audit.py      READ — no edit (string-enum passes numeric audit)
data/tuning/
  creature-rank.v1.json           NEW — closed vocabulary + 100-cell grid (diagonal default) + per-gate floors (all bottom)
  creature-threat.v1.json         READ — rung ids/offsets (:4-13), no change
  creature-rarity-power-fallback.v1.json  READ — ten-row list precedent (:4-13), no change
src/FusionRpg.Core/Creatures/
  CreatureRank.cs                 NEW — closed 10-value enum (mirrors rarity ids, separate type)
  CreatureRankLadder.cs           NEW — AtLeast (:46-pattern)/AtMost/RungsBelow/OneRungAbove/All
  Generation/AnchorRow.cs         EDIT — nullable rank field (Rarity nullability untouched)
  Generation/SpeciesExpander.cs   EDIT — compute rank post-expansion (:66-68 path untouched); unresolved reporting (:35-44)
  Generation/ConcreteSpecies.cs   EDIT — record field (:13-85)
  Generation/ConcreteSpeciesSerializer.cs  EDIT — Canonical dict (:15-41)
  Generation/ConcreteSpeciesSeedReader.cs  EDIT — optional-read for rank (:27-56)
  Generation/ConcreteSpeciesMapper.cs      EDIT — funnel field through (:82-109)
  Fusion/CreatureRecipeCatalog.cs EDIT — rank floor constant beside OutputEligibilityFloor (:39)
  Delve/Encounter/SlotFilter.cs   READ — ConcreteAnchor.From join carries rank (:25-79); filter + throw unchanged
src/FusionRpg.Core/Delve/Wild/
  Cage.cs                         EDIT — rank floor on OccupantEligible (:19)
src/FusionRpg.Core/Expeditions/
  ExpeditionResolver.cs           EDIT — rank floor on WildBand filter (:238-244; Sunwoven exclusion + EventOnly admission pinned)
src/FusionRpg.Core/Battle/
  WaveCatalog.cs                  EDIT — rank floor on Band membership (:127-130, :153-154)
src/FusionRpg.Data/Sqlite/
  RpgStore.Species.cs             EDIT — EnsureColumn (:57-65) nullable; read-back null handling (:261-316); SameContent compares (:318-329); INSERT/ON CONFLICT lists (:123-141); validator needs no rank refusal (:75-103)
  RpgStore.Fusion.cs              EDIT — rank floor at enforcing gate (:154-196, gate :161-164) + pickable/inherit gates (:240, :274-279)
src/FusionRpg.Server/
  FusionEndpoints.cs              EDIT — preview mirrors enforcing gates (:148-157, :187-194)
  CreatureEndpoints.cs            EDIT — catalog projection carries rank (:19-36)
  DelveWildEndpoints.cs           READ — commit paths assemble mint specs (:22-27), no change
data/seed/creatures/species/**    REGENERATED — anchors gain derived rank (deterministic; rerun-identical)
data/generated/creatures/**       REGENERATED — concrete rows carry rank (--check fails until regen, expected)
tests/FusionRpg.Core.Tests/Creatures/
  CreatureRankTests.cs            NEW — grid mapping, unresolved→null, helper parity, pass-through gates
tests/FusionRpg.Guard.Tests/
  CreatureRankLadderGuardTests.cs NEW — bare-comparison prohibition (beside :36/:45 pattern)
```

## Code Style

Ladder helpers mirror the existing shape exactly — named thresholds, throwing at the top, guard test forbidding bare comparisons outside the file (`CreatureRarityLadder.cs:3-9` pattern):

```csharp
// Rank is identity, never a magnitude: it gates and displays, it never prices,
// scales, or contests. Any use of rank in a magnitude path is a bug.
public static bool AtLeast(CreatureRank rank, CreatureRank threshold) => (int)rank >= (int)threshold;
```

Tuning mirrors the fallback file's ten-row correspondence, extended to the grid (`creature-rarity-power-fallback.v1.json:4-13` pattern — list precedent honestly cited; the grid's own justification is future off-diagonal divergence without migration):

```jsonc
{ "threatBand": "warden", "rarity": "fused", "rank": "fused" }
```

Conflation guard, stated in code where the grid is read: threat selects the *row*, rarity selects the *column*, the cell holds the rank — threat never moves a magnitude here because rank never enters one.

## Testing Strategy

- **xUnit (Core):** grid mapping (diagonal default = rarity rung; authored off-diagonal cells resolve); `unresolved` input → null rank, never a default; ladder helper parity (`AtLeast`/`AtMost`/`RungsBelow` agree with rarity ladder row-for-row); new guard test green; gate pass-through defaults (landing-slice gates green at bottom rank with behavior identical to pre-rank; follow-ups prove their own on landing); null→bottom mapping per gate (no `AtLeast(null,…)` crash path).
- **xUnit (Data):** rank persists round-trip through the sole DAL path; null-rank rows persist as null; reimport idempotent (SameContent compares rank).
- **Seedsmith pytest:** `audit_schema` still clean; derive tests for voted + fallback-stamped + unresolved inputs; byte-identical rerun; scoped-rerun staleness test (threat/rarity-only rerun recomputes rank).
- **Tuning validation:** grid covers all 100 cells; every cell in the closed vocabulary; unknown id rejects naming table + cell.
- **Goldens:** zero moves on landing (§7 evidence + pass-through tests). Quality report rank-coverage line (reports-only).
- **No new flaky clocks/RNG:** derivation pure over committed fields.

**Tunables (tunables-ssot.md cited; all in `data/tuning/creature-rank.v1.json`):** rank vocabulary (closed ids), the 100-cell grid, per-gate rank floors (fusion promotion, fusion recipe/inherit, expedition wild, wave band, Cage eligibility — all default bottom). Units: rung ordinals (dimensionless), floors (rank ids). Nothing here is a `const` except the *shape* (10×10 grid, DERIVED-not-voted, null-on-unresolved) with why-not-tunable comments.

**ActorHub gate:** neither produces nor consumes any actor combat/derived/AppliedCombat number — rank is per-species identity on the anchor/catalog path, never composed into Hub snapshots. No `IActorStatSubsystem`, no `ContributionSourceIds`, no private fold. (Rank-scaled combat is a new spec with its own Hub-contribution design — explicitly out.)

**Numeric types:** no magnitudes produced or consumed — rank is a string-id enum; rung ordinals are `int` 0–9, never arithmetically scaled; touched prices stay `long` with widen-before-multiply/divide-last. No per-mille arithmetic in this module.

## Boundaries

- **Always:** SQL only inside `FusionRpg.Data` (guard-dal green); `long` for any touched price; widen-before-multiply/divide-last; deterministic byte-identical reruns; guard test for bare rank comparisons; quality-report line for rank coverage; preview mirrors every enforcing gate.
- **Ask first:** any gate floor above bottom (behavior change + possible golden move — measured justification); adding rank to any content hash (one explicit version outcome); changing `OutputEligibilityFloor` semantics vs joining beside it; renaming rank ids after content ships; off-diagonal grid cells above diagonal (content decision, not refactor).
- **Never:** rank in Θ/`P(Θ)`/`PowerVector`/contest/resolver/pricing math; model-authored or voted rank; a default rank for unresolved input; bare `(int)rank` comparisons outside `CreatureRankLadder.cs`; blocking generation on rank (skip, don't fabricate); a second rank-like vocabulary beside this one; unifying distinct rank/tier axes into one shared enum (each axis owns its closed vocabulary — owner-confirmed); touching sealed ideals except by owner decision notes; `NOT NULL DEFAULT` on the rank column.

## Success Criteria

- [ ] `CreatureRank` 10-value closed enum + `CreatureRankLadder` helpers + new guard test green.
- [ ] `creature-rank.v1.json` validates (100 cells, closed vocab, diagonal default); every resolved anchor derives the table's rank; unresolved → null, proven by test.
- [ ] Derive wired at all three runner sites; scoped-rerun staleness test green; full rerun byte-identical.
- [ ] Persist round-trip + null handling + reimport idempotence green; Injector SeedReader/Mapper carry rank.
- [ ] Landing-slice gates (fusion promotion, recipe/inherit, display payloads) read rank through named helpers at pass-through defaults, preview/enforce parity proven, behavior identical to pre-rank; each follow-up gate lands later with its own pass-through proof.
- [ ] Full Core + Data + Guard suites green with zero golden moves; quality report shows rank coverage; audit-magic-numbers + guard-dal clean.
- [ ] DESIGN-GATE §5 boxes: subsystems identified ✓ · boundary record ⛔ UNTICKED (no `tasks/sessions/*.json` record in this harness — owner runs `/session-start`) · gate docs read this session ✓ · decisions checked ✓ · claims cite file:line ✓ · verified against code (3-agent audit this session) ✓ · surrounding sections read ✓ · constraints tested ✓ · §2 invariants hold ✓ · corrections propagated ✓ · no population counts pinned ✓ · ActorHub N/A stated ✓ · no SOLID-violating path ✓.

## Open Questions

None standing — idea phase closed 8/8; this spec's assumptions decided above. Recorded dissent from this session's adversarial debate: cut the 100-cell grid to the ideal's 10-row rarity banding; cut wild/expedition/wave joins to fusion-floor + display; alias `CreatureRarity` instead of a separate enum. Rulings 2026-09-12 — alias bid **OVERRULED** (separate-vocabulary principle above); grid **STANDS** (divergence without migration); gates land **incrementally** (fusion + display first, each further join on evidence, not all at once). Remaining micro-choices (display names, off-diagonal cells, floors above bottom) are tuning content for implementation review.
