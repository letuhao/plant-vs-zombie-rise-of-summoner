# Spec: delve-quests

Status: **APPROVED by the owner 2026-09-05 (wave 4) — unbuilt.** Nothing under `Core/Delve/`, `Core/Dungeon/` or
`data/seed/dungeon/` exists today (checked on disk). Every `file:line` below was opened this session; drift against
the brief is in §Drift. Every number is a starting shape, never a balance decision.

Module id `delve-quests` in the [party-dungeon map](../party-dungeon-map.md) (row 14, `:127`; wave 4, `:140`; gate
G4 `:160`). Depends on `dungeon-loot` (`dungeon-quest` source kind, `rewardBand` window, `RarityShift`) and
`event-deck` (the report's `events[]` row); reads by reference `dungeon-registries`, `dungeon-seed-contract` (§1.5),
`delve-graph-roll` (`Facts`), `delve-scope`, `delve-battle-profile`, `delve-attrition`, `difficulty-ladder` (`Θ_run`),
`loot-pack`, `supplies-and-objects`. External: none.

## Objective

Be the runtime half of the seedsmith quest generator: turn a domain's `questPool` of **template instances** into two or
three offered quests at delve entry, drawn only from what the rolled graph can satisfy; evaluate them as a **pure read
model** over the delve's own records; and pay a completed quest **once, at extraction**, through `dungeon-loot`'s
`dungeon-quest` source with the anchor's `rewardBand` as the tier window. A quest rewards; it never unlocks, gates, wins or writes.

Success looks like: `QuestOffer.Draw` never offers an unsatisfiable quest over 256 graphs; `QuestProgress.Evaluate`
called twice, or replayed from `(seed, decisions_json, battle traces)`, is byte-identical; a wipe pays nothing;
`CloseDelve(Extracted)` rolls exactly one `loot:delve:{delveId}:quest:{questId}` per completed quest and a replayed
close rolls none; `QuestPreflight.Run` refuses a domain with no satisfiable set for a layout, naming domain, layout
and template; every battle, expedition, world and item golden is byte-identical.

## Locked anchors (quoted, not paraphrased)

- **Ideal §11.3 (`:1095-1114`):** *"no quest system exists, whole"*; the anchor is `objectiveTemplate` *"(VALIDATED,
  closed: `explore-rooms · cleanse-fights · gather-curio-kind · kill-boss · extract-with-item-kind ·
  bring-demon-home-alive · finish-under-hunger · survive-no-downed · spend-no-provision`) · `targetRef` (a **kind**,
  never a number, or `none`) … · `rewardBand` (a tier window resolved through `LootPipeline` with a `dungeon-quest`
  source kind — never a gold number) · `scope` (`delve · domain · roster`)"*; *"evaluation is pure and idempotent on
  `(playerId, questId, delveId)` under the expedition exactly-once envelope"*; *"Doran and Parberry's structural
  analysis (>750 quests, 9 motivations each with 2–7 strategies as verb-noun pairs) decides the template list: our
  templates are their strategies with the noun replaced by a kind ref and the count by a band."* `:1129-1130`: *"no
  event may gate the boss or a room kind; quests reward, never unlock, in v1."*
- **Ideal §10 Law 2 (`:790-796`):** *"A graph, a deck draw or a slot fill is not an atom container … reuse
  `SeededRng.DeriveStream(seed, name)` … and `WeightedChoice` … **as long as the structural roller never touches a magnitude**."* Which quest is offered is structural; what it pays is `LootPipeline`'s.
- **Audit D14 (`audit-2026-09-05.md:239`), verbatim:** *"`finish-under-hunger · survive-no-downed ·
  spend-no-provision` reward avoiding sinks | Eligible at rung ≥ hard or paired with a risk objective."* The registry's
  `sinkAvoidance` column (`spec-dungeon-registries.md:75`) and the contract's DERIVED `riskPaired` (`:104`) are this finding.
- **Audit S1-1 (`:195`):** victory souls *"once per delve at extraction on `Θ_run` … forfeited on a wipe."* A mid-delve quest payout would be S1-1 in a different currency.
- **`ssot-rarity.md` §3.5 (`:187-188`):** overlap is *"the product of three variances that already live in shipped
  columns, and no fourth mechanism is introduced."* §3.6 via `spec-dungeon-loot.md:38-39`: *"A multiplier on the rung
  makes rarity dominant and destroys the overlap."* The window bounds **which rung is drawn**; it multiplies nothing.
- **`spec-delve-attrition.md` §9 (`:269-271`):** `won` = raid extracted **and** (boss killed **or** half the route cleared) **and** not `afflicted`. No quest term, and it gains none.

## Design

### 1. Quest anchors and templates

A quest anchor (`quests/<id>.json`, seed contract `:93-104`) is a **template instance**: the model writes *which*
template, target kind and theme; every count is a band; every reward is a window. `objective-templates.v1.json`
(registries `:75`) is the closed list — nine rows with `targetKind ∈ room-kind · curio-kind · item-kind · boss · none`
and `sinkAvoidance`, exposed by `ObjectiveTemplateCatalog`; this module adds no member.

| Template | `targetKind` | Count | Fact read (§3) | Risk | Sink-avoid |
|---|---|---|---|---|---|
| `explore-rooms` | `none` | ‰ of non-secret rooms | `rooms[].visited` | at `most`/`all` | no |
| `cleanse-fights` | `room-kind` (`fight`/`elite`) | ‰ of rooms of that kind | `rooms[].cleared` where `kind = target` | yes | no |
| `gather-curio-kind` | `curio-kind` (an event `kind`) | ‰ of rooms whose event has that kind | `events[]` with `choice ≠ leave`, `outcomeOrdinal ≠ nothing` (`spec-event-deck.md:199`) | no | no |
| `kill-boss` | `boss` | — | `boss` room `cleared`; the `role: boss` actor `Survived == false` (`BattleModels.cs:342-345`) | yes | no |
| `extract-with-item-kind` | `item-kind` (a role) | — (≥ 1) | a pack at extraction holds that role (`spec-loot-pack.md:75`) | no | no |
| `bring-demon-home-alive` | `none` | — | no member `downed` at extraction (`spec-delve-attrition.md:81`) | no | no |
| `finish-under-hunger` | `none` | — | no member carries a hunger exhaustion status at extraction (`:79`) | no | **yes** |
| `survive-no-downed` | `none` | — | no member `downedOnce` (`:81`) | no | **yes** |
| `spend-no-provision` | `none` | — | no `pack.drop{by: use}` in `decisions_json` (`spec-loot-pack.md:79`) | no | **yes** |

Two readings fixed here and named as this spec's: `bring-demon-home-alive` is *standing at extraction*,
`survive-no-downed` is *never downed* — strictly harder, hence sink-avoidance. `extract-with-item-kind` takes no count:
`quests.countBand.*Milli` is *‰ of rooms* (registries `:136`); an item count is a new unit — ask first. **Counts are
`int`s derived at entry from the rolled graph**: `need = max(1, ceil(rooms_of_kind × milli / 1000))` over
`DelveGraph.Facts` (`spec-delve-graph-roll.md:64`) — never from the anchor or the model (Law 3, `:797-799`).

### 2. Offering at entry and satisfiability

**Per raid, not per party.** The map names one stream, `dungeon:quest` (`:127`); the report, extraction (S2-16,
`spec-loot-pack.md:85`) and `won` are all raid-wide. Per-party quests would need per-party streams, could offer one
anchor twice, and would make `explore-rooms` mean "on my route" — a fourth scope nobody approved. `questScope` names
the **fact source**: `delve` = this run's report; `domain` adds the player's closed `rpg_delves(player_id, domain_id,
state)` rows (`spec-delve-scope.md:77`); `roster` adds the member records at extraction — caller-supplied bundles on one
evaluator, *"never I/O from inside the leaf"* (`PredicateNode.cs:13-17`).

**The draw**, at `CreateDelve` after the graph validates (`spec-delve-scope.md:215-219`):

1. `pool` = the domain's `questPool` (≥ 2 ids, `spec-dungeon-seed-contract.md:52`) in ordinal `questId` order.
2. **Satisfiability filter** over `Facts` and the corpus: `cleanse-fights` needs ≥ 1 room of the target kind;
   `gather-curio-kind` ≥ 1 rolled room whose archetype `eventPool` (`:70`) holds that kind; `extract-with-item-kind` the
   role in some `lootBinding` table's base-type set (`LootContentView.BaseTypesFor`, `LootPipeline.cs:76`); the other six
   hold on every valid graph (boss row and first-row fights are validator rules, `spec-delve-graph-roll.md:143-163`).
3. **D14 filter:** a `sinkAvoidance` quest is eligible iff `rung.ordinal ≥ hard.ordinal` **or** the set drawn so far
   holds a risk quest (`kill-boss`, `cleanse-fights`, `explore-rooms` at `most`/`all`). Non-sink quests draw first.
4. `offeredAtEntry` draws **without replacement**: slot `n` = `WeightedChoice.Pick(options, seed_n,
   $"dungeon:quest:{n}")` (`WeightedChoice.cs:25`; the library prefixes the stream, `:39`) with **equal weights** — an
   anchor carries no weight (Law 3; S2-12); `seed_n = DeriveStream(delveSeed, $"dungeon:quest:{n}").NextULong()` (`SeededRng.cs:26-27`).

The offer persists as `rpg_delves.quests_json` (ids in draw order, `need` per quest) — **filed on `delve-scope`** as
an `EnsureColumn`, the `event_id` argument (`spec-event-deck.md:249`): a corpus re-import mid-delve would silently
change a re-derived offer, so the stored row is truth and the rebuild is asserted equal on load (`spec-delve-graph-roll.md:17-19`).

### 3. Progress as a read model

`DelveReport` is the read model this module introduces (`event-deck` already extends it by name, `:199`), assembled by the host from rows other modules write and this one never touches:

| Slice | Source | Writer |
|---|---|---|
| `rooms[] (row, col, kind, visited, cleared, isSecret, eventId)` | `rpg_delve_rooms` (`spec-delve-scope.md:79-89`) | `delve-scope` / `event-deck` |
| `kills[] (roomId, speciesId, role)` | each room's `BattleReport` results (`SpeciesId`, `Survived`, `Retreated`, `BattleModels.cs:342-345`) joined to its `EncounterHalf` setups for the role, via correlation `delve:{delveId}:{r}:{c}:p{partyIndex}` (`spec-delve-battle-profile.md:149-150`) | `delve-battle-profile` |
| `events[] (roomId, eventId, outcomeOrdinal, choice)` | the deck's hook row (`spec-event-deck.md:199`) | `event-deck` |
| `decisions[]` | `decisions_json` — `route`, `pack.drop`, `talk`, `object.{verb}`, `extract` (`spec-delve-battle-profile.md:141-145`; `spec-supplies-and-objects.md:189`) | every appender |
| `members[] (partyIndex, instanceId, downed, downedOnce, statuses[])` | `parties_json` (`spec-delve-attrition.md:75-81`) | `delve-attrition` |
| `haul[] (partyIndex, refId, role)` | `parties_json` packs (`spec-loot-pack.md:75`) | `loot-pack` |

`QuestProgress.Evaluate(quest, report) → QuestVerdict { QuestId, Done, Have, Need }` is pure and total: it
**recomputes from scratch every call**, holds no counter, and is what the stage's tracker reads for "3 / 5 rooms". An
impossible quest reads `Done = false` and is never removed or replaced. The *reward decision* is taken once, at
extraction (§4) — the map's "evaluated once" and the brief's "recompute each time" are one rule seen from two callers.

**Predicates.** An anchor's optional predicate is an E3 tree (`PredicateNode.cs:53-71`) over the closed leaves (`:19-33`)
plus `event-deck`'s four (`spec-event-deck.md:220-223`), compiled by `PredicateCompiler.TryCompile` (`PredicateCompiler.cs:34`)
at import — malformed is a refusal, never "always" (ideal `:1047-1048`). Facts are the extraction snapshot (`Self` = the
raid, `Target` = the deepest cleared room); meaningless leaves are refused as the deck refuses them (`:215-216`), and
`RoomKindIs boss` too — a boss gate by another door (`:273`).

### 4. Completion and reward

Inside `RpgStore.Delve.CloseDelve(Extracted)`, in the **same transaction** as attrition's settlement, loot's victory
rows and the pack's write list (`spec-delve-attrition.md:265-267`; `spec-dungeon-loot.md:216`; `spec-loot-pack.md:85`):

1. `Evaluate` every offered quest over the final report; `quests_json` gains the verdicts.
2. Per `Done` quest, in offer order, `QuestReward.Request(quest, delve)` builds `LootSourceRow("dungeon-quest",
   $"{delveId}:quest:{questId}", table = lootBinding[cache], ContentLevel = Θ_run)`, correlation
   `loot:delve:{delveId}:quest:{questId}` (`spec-dungeon-loot.md:108-111` — the arm `LootCorrelation.Derive` gains;
   today `:91-98` throws on the kind and `DropTableValidator.KnownSourceKinds` `:52-53` refuses it), seed
   `DeriveStream(delveSeed, $"dungeon:loot:quest:{questId}")`, `ThetaActor = Θ_commander`, into `DelveLoot.RollRoom`'s
   shape (`:292-312`). **No private table**: the domain's `cache` binding is the table; the quest brings only its window.
3. **The window** composes into the synthesized view as loot §4 composes floor and shift (`:144-151`): `floorRung`
   joins `RarityShift.ComposeFloor`; `ceilRung` zeroes every rung above it in `RarityWeightShift` — *"row kept, never
   drawn"* (`:151`). Nothing is multiplied; loot's `:154-155` magnitude-identity test is repeated with the window.
4. The grant **banks at the close** as the `dungeon-clear` relic does — *"owned then, never in a pack"*
   (`spec-dungeon-loot.md:163, :209`). The brief said "into the party's pack"; at extraction the pack is being emptied
   in this same transaction, so a cell tax on an item that never travels is ceremony and a no-fit reward lost to a
   vanished floor is a punishment nobody designed. Reveal attribution: `loot.bossGrantDistribution` round-robin by `PartyIndex` (`spec-loot-pack.md:73`).
5. `Wiped` → step 1 runs for the record; steps 2–4 never do (S1-1; `spec-delve-attrition.md:255-259`).

**Idempotency, twice over.** `CloseDelve` is gated on `Active → Extracted` as `CloseExpeditionUnlocked` gates
`ApplyExpeditionRewards` (`RpgStore.Expeditions.cs:271-274, :296-297`), so a replayed close rolls nothing; beneath it,
`LootPipeline` step 1 returns the recorded manifest on `(player_id, correlation_id)` (`LootPipeline.cs:105-106`) —
`(playerId, questId, delveId)` **is** that key. **Souls:** none in v1 — a fourth faucet needs a `ssot-power-scale.md`
§11 row; if ever added it accrues to `souls_unbanked` (`spec-dungeon-loot.md:212-220`).

### 5. Sink avoidance

Two rules, one column. **(a) D14, the eligibility rule** — `ObjectiveTemplateCatalog.SinkAvoidance(id)` is true for
the three templates that pay the player for *not* using a sink (rations, provisioning, the merchant); unpaired and
cheap, they teach "skip the sinks" — P1 inverted. So: eligible only at `rung.ordinal ≥ hard.ordinal`, or beside a risk
quest in the same offer (§2 step 3); the DERIVED `riskPaired` (`spec-dungeon-seed-contract.md:104`) is the corpus check,
and an all-sink-avoidance `questPool` is refused at preflight. **(b) No template may require a sink** — the brief's
framing, the mirror image and equally true: "buy three wards" refunds the merchant. It holds structurally (`targetKind`
is closed with no `sink` or verb member, `:75`) and is a validator rule anyway (§8), so no future member admits it by accident.

### 6. Boundaries with victory and the boss

`won` is attrition §9's and reads no quest; `DelveLoot.AtExtraction(…, won, …)` takes it from `ExtractionSettlement.Decide`
(`spec-dungeon-loot.md:316-319`; `spec-delve-attrition.md:423`), never from a verdict. No quest gates the boss room, a
door, a room kind or the next rung — the deck's rule extends verbatim: no `consequence`, `seal` or unlock; `prereqRefs`/
`chainRef` are *"not unlocks"* (`spec-dungeon-seed-contract.md:103`). A quest's only outputs are a `QuestVerdict` and, at extraction, one `LootRequest`.

### 7. Chains

`chainRef` (VALIDATED same-kind ref, `none` legal, `:103`) is **a seam only in v1**: the importer checks it exists, is a
quest and is acyclic (`:150`); the runtime **never reads it**. A domain story needs per-player completion rows `(playerId,
domainId, questId)` outliving the graph (the `rpg_delve_event_seen` shape, `spec-event-deck.md:250`) and a ruling on whether a chained quest may displace a satisfiable draw — neither is in the approved anchor; filed as v2.

### 8. Refusals and preflight

Every refusal is a thrown `QuestRefusal` naming domain, quest and rule — no flag, no fallback, no empty offer. **At
import / preflight** (`QuestPreflight.Run(corpus, domains, layouts, tuning)`, model-free, run by the `domain-catalog`
importer and `dungeon audit`): a template not in the registry; a `targetRef` mismatching `targetKind`, or naming an id or
a number; a `countBand` on a count-less template that is not `none` (§Drift); a tree failing `TryCompile`, using a refused
leaf, or naming `RoomKindIs boss`; `floorRung > ceilRung`; cyclic or cross-kind `chainRef`/`prereqRefs`; fewer than
`offeredAtEntry` non-sink anchors in a pool; and the **satisfiability sweep** — every `(domain, layout, raidMode, rung)`
over 256 seeds: `Roll`, assert the §2 filter leaves ≥ `offeredAtEntry`, else refuse the domain naming the layout and the
first template that starved. **At play:** a filter leaving fewer than `offeredAtEntry` (a corpus edited after import — a
bug, not a state); a `quests_json` the rebuild does not reproduce; a pipeline rejection (propagated); a second roll for
one correlation (`delve.quest-twice`, the `delve.victory-twice` shape, `spec-dungeon-loot.md:226`).

### 9. Determinism

Pure over `(pool, Facts, delveSeed, rung, tuning)` for the offer and `(quest, DelveReport)` for the verdict. Pools
enumerate in ordinal id order; every draw is a named stream off the sealed seed; the reward rides `LootPipeline`'s replay
contract. No `System.Random`, `DateTime`, store or I/O under `Core/Delve/Quests/`; `(delve seed, decisions_json, every
room's battle trace)` — already *"the whole run"* (`spec-delve-battle-profile.md:145`) — reproduces every verdict.

## Tunables

Read from `data/tuning/dungeon.v1.json` through `DungeonTuningHub`. Existing keys, owned by `dungeon-registries`:

| Key | Unit / type | Read here as |
|---|---|---|
| `quests.offeredAtEntry` (`spec-dungeon-registries.md:134`) | count int | draws per raid; starting shape 2 (the map's "2–3") |
| `quests.countBand.{few,some,most,all}Milli` (`:136`) | ‰ of rooms, long | `need` per counted template; 250 / 500 / 900 / 1000 (DD's "90% of rooms" is `most`, ideal `:1102`) |
| `quests.rewardBand.{modest,fair,rich}.{floorRung,ceilRung}` (`:136`) | rung id | the window; three adjacent two-rung windows climbing the ten-rung ladder (`item-rarity.v1.json:7-18`), names fixed at build |
| `bands.{questScope,rewardBand,countBand}.*` (`:80`) | member sets | vocabularies the importer checks the anchor against |
| `difficulty.rungs[].ordinal` via `DifficultyRungCatalog` · `loot.bossGrantDistribution` | int · rule id | the D14 `≥ hard` test · reveal attribution |

One new key, filed on `dungeon-registries` (unit in the name, T6; required, T5):

| Key | Unit / type | Owner | Purpose |
|---|---|---|---|
| `quests.autopilotCompletionBand.{min,max}Milli` | ‰ long | `delve-quests` | §Testing's regression band — never a target; 300 / 900. No `quests.weight*`, `quests.*Souls` or per-template count key exists |

## Numeric types

`need`, `have`, `offeredAtEntry`, rung ordinals, `Θ_run`: **`int`** — bounded by the graph or the ladder. Every ‰: **`long`**,
widened before the multiply: `need = (int)Math.Max(1, ((long)rooms * milli + 999) / 1000)`. Souls, if ever: `long`. No
`float`/`double`; a fractional tuning value is a load rejection. Overflow throws, never wraps.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve.Quests"   # offer, evaluate, preflight, goldens
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Delve"          # CloseDelve exactly-once, quests_json
dotnet test tests\FusionRpg.Core.Tests                                              # battle/expedition/world/item goldens untouched
.\scripts\guard-dal.ps1; .\scripts\guard-power.ps1; python scripts\audit-magic-numbers.py --domain dungeon  # M1 = 0
cd tools\seedsmith; python -m seedsmith dungeon audit         # quest anchors + satisfiability sweep
```

## Structure

```
src/FusionRpg.Core/Delve/Quests/
  QuestRow.cs · QuestCatalog.cs     Load(rows, registries, tuning): template/target/band checks, trees compiled, §8 rules
  QuestOffer.cs                     Satisfiable(quest, facts, corpus) · Draw(pool, facts, seed, rung, tuning) on dungeon:quest:{n}
  QuestProgress.cs · QuestReward.cs Evaluate(quest, report) → QuestVerdict · Request(quest, delve) → source row + windowed view
  QuestPreflight.cs · QuestCoverage.cs · QuestRefusal.cs
src/FusionRpg.Core/Delve/Report/DelveReport.cs            the read model (§3) — owned here, extended by event-deck's row
src/FusionRpg.Core/Dungeon/Registry/ObjectiveTemplateCatalog.cs   dungeon-registries' file; read for TargetKind/SinkAvoidance
src/FusionRpg.Data/Sqlite/RpgStore.Delve.cs               quests_json (offer at CreateDelve; verdicts at CloseDelve) — delve-scope's file
src/FusionRpg.Server/DelveEndpoints.cs                    GET …/delves/{id}/quests — tracker projection, no engine words
tests/FusionRpg.Core.Tests/Delve/Quests/ · tests/FusionRpg.Data.Tests/Delve/   (goldens, property, counting-view tests)
UNTOUCHED: BattleEngine, BattleModels.cs, LootPipeline.cs (beyond dungeon-loot's arms), SoulEarnPolicy.cs, WorldState.cs
```

## Code style

Pure over inputs, tuning injected, no I/O (tunables-ssot §7.2); no parameter named `level`/`lvl`/`index` on a numeric method (`guard-power.ps1` G2); refusals name the rule and the id.

```csharp
/// <summary>Which pool rows this graph can satisfy. Pure; ordinal order in, ordinal order out.</summary>
public static IReadOnlyList<QuestRow> Satisfiable(IReadOnlyList<QuestRow> pool, IReadOnlyList<DelveRoomFact> facts,
    QuestCorpusView corpus, DungeonTuning tuning)
{
    var kept = new List<QuestRow>(pool.Count);
    foreach (var q in pool)                                   // sorted by questId at load — never a dictionary
    {
        var need = QuestCounts.Need(q, facts, tuning);        // ‰ of rooms → int, ceil, ≥ 1; 0 for count-less templates
        if (q.Template.Id switch
        {
            "cleanse-fights"         => facts.Count(f => f.Kind == q.TargetRef) >= need,
            "gather-curio-kind"      => facts.Count(f => corpus.EventPoolHasKind(f.ArchetypeId, q.TargetRef)) >= need,
            "extract-with-item-kind" => corpus.LootBindingOffersRole(q.TargetRef),
            _                        => true,                 // kill-boss, explore-rooms, the five `none` templates: structural
        }) kept.Add(q);
    }
    return kept;
}

/// <summary>Recomputed from scratch on every call; holds no counter. Same (quest, report) ⇒ same verdict.</summary>
public static QuestVerdict Evaluate(QuestRow q, DelveReport r)
{
    var need = q.Need;                                        // fixed at entry from the rolled graph, stored in quests_json
    var (have, done) = q.Template.Id switch
    {
        "explore-rooms"          => Count(r.Rooms.Count(x => x.Visited && !x.IsSecret), need),
        "cleanse-fights"         => Count(r.Rooms.Count(x => x.Cleared && x.Kind == q.TargetRef), need),
        "gather-curio-kind"      => Count(r.Events.Count(e => e.Kind == q.TargetRef && e.Choice != "leave" && e.Outcome != "nothing"), need),
        "kill-boss"              => Flag(r.Kills.Any(k => k.Role == "boss")),
        "extract-with-item-kind" => Flag(r.Haul.Any(h => h.Role == q.TargetRef)),
        "bring-demon-home-alive" => Flag(r.Members.All(m => !m.Downed)),
        "finish-under-hunger"    => Flag(r.Members.All(m => !m.Statuses.Contains(ResourceStatusIds.HungerExhausted))),
        "survive-no-downed"      => Flag(r.Members.All(m => !m.DownedOnce)),
        "spend-no-provision"     => Flag(!r.Decisions.Any(d => d.Kind == "pack.drop" && d.By == "use")),
        _ => throw new QuestRefusal(q.QuestId, $"template '{q.Template.Id}' has no evaluator"),   // registry ≠ code: loud
    };
    var predicateHolds = q.Predicate is null || q.Predicate.Evaluate(r.ExtractionFacts);
    return new QuestVerdict(q.QuestId, Done: done && predicateHolds, Have: have, Need: need);
}
```

## Testing strategy

- **Goldens per template × layout tier.** Nine templates × `sizeBand` (`spec-dungeon-seed-contract.md:74`): one fixture
  domain, one seed; offer ids, every `need`, verdicts after a scripted autopilot run and reward correlations locked.
- **Property — every offered quest is satisfiable.** 256 seeds × shipped domains × `solo/pair/quad` × ten rungs:
  exactly `offeredAtEntry` ids, each passing `Satisfiable`, none twice; no `sinkAvoidance` quest below `hard` unpaired.
- **Evaluate is idempotent and replay-equal.** Twice on one report: byte-identical; a report rebuilt from `(seed,
  decisions_json, traces)` equals the live one; one extra cleared room changes only the counted quests.
- **Reward rolls only at extraction; a wipe pays nothing.** A counting `LootPipeline` view sees zero `dungeon-quest`
  requests before `CloseDelve`, one per `Done` quest at `Extracted`, zero on a replayed close, zero on `Wiped`.
- **Window, not multiplier.** One `(seed, Θ_run)` with and without a `rich` window: every frozen magnitude at equal `(atomId, tier)` identical; every drawn rung inside `[floorRung, ceilRung]`.
- **SinkAvoidance validator red fixtures.** An all-sink-avoidance pool, `targetKind: sink`, `RoomKindIs boss`,
  `floorRung > ceilRung` → each refused by name; a registry template with no `Evaluate` arm → `QuestRefusal`, not `false`.
- **`won` untouched.** All quests `Done`, no boss, < half the route → `won == false`; the inverse → `won`, zero rewards.
- **Goldens untouched.** All four battle hashes, the 32-seed sweep, the four expedition tier hashes, world and item goldens byte-identical — no field is added to any hashed record.
- **Metrics (G4 input).** `QuestCoverage.Report(domain, rung, 32 seeds)`: every pool row offered ≥ 1 time per layout
  tier (closed loop); autopilot completion ‰ per template inside `quests.autopilotCompletionBand.{min,max}Milli` — a
  regression band a balance pass moves through `countBand` ordinals on anchors, never through code.

## Boundaries

- **Always:** templates from `objective-templates.v1.json` only; counts as ‰ bands resolved from the rolled graph;
  the offer on `dungeon:quest:{n}` after the satisfiability and D14 filters; `Evaluate` pure and recomputed; one
  `dungeon-quest` request per completed quest at `CloseDelve(Extracted)` through `LootPipeline` on the domain's `cache`
  binding, window in the view, correlation server-derived; `quests_json` through `RpgStore.Delve.cs` only; refusals thrown.
- **Ask first:** a new template (a registry member); an item-count unit for `extract-with-item-kind`; a soul term
  (`ssot-power-scale.md` §11 row); reading `chainRef` at runtime; per-party quests; routing the reward through the pack
  grid; a `domain`-scope completion table; a ceiling expressed other than as zeroed rungs (pity interaction, `LootPity.cs`).
- **Never:** a quest that writes state (rooms, doors, lanes, pools, packs, bank); a mid-delve reward or a wipe
  consolation; a count, weight or soul number from a model or an anchor; a quest as victory condition or as a
  boss/door/rung gate; a private drop table; a template whose completion requires spending at a sink; a rarity
  multiplier; `float`/`double`; a private `f(level)`; SQL outside `FusionRpg.Data`; `System.Random` or a clock.

## Success criteria (G4, `party-dungeon-map.md:160`)

1. The six shipped domains pass `QuestPreflight.Run` and `dungeon audit` with 15 quest anchors; a rerun is byte-identical.
2. A 4-party raid on autopilot is offered `offeredAtEntry` satisfiable quests, finishes some, extracts, and
   `CloseDelve` rolls exactly one `dungeon-quest` request per finished quest — proven by the counting view.
3. The 256-seed property holds; the 9 × 3 goldens are locked; the wipe test pays nothing; `QuestCoverage` shows every
   pool row offered and completion inside the band for every shipped domain.
4. Battle, expedition, world and item goldens byte-identical; guards green; M1 = 0 under `Delve/Quests`.

## Interface exposed to dependents

| Member | Returns | Consumer |
|---|---|---|
| `QuestOffer.Draw(pool, facts, seed, rung, tuning)` | `IReadOnlyList<OfferedQuest { QuestId, Need }>` | `RpgStore.Delve.CreateDelve` (writes `quests_json`) |
| `QuestProgress.Evaluate(quest, report)` · `DelveReport` | `QuestVerdict { QuestId, Done, Have, Need }` | `delve-stage` (wave 5) quest tracker, live; `RpgStore.Delve.CloseDelve` (the only reward caller) |
| `QuestReward.Request(quest, delve)` | `(LootSourceRow, view-with-window, seed)` for `DelveLoot.RollRoom`'s shape | `dungeon-loot` — the `dungeon-quest` request (`spec-dungeon-loot.md:395`) |
| `QuestPreflight.Run(corpus, domains, layouts, tuning)` · `QuestCoverage.Report(domain, rung, seeds)` | refusals · coverage + completion ‰ | `domain-catalog` importer (pool import), `dungeon audit`, G4 |
| `QuestDto` (template name, flavour, `have / need`, done — no `Θ`, rung id or `PartyIndex`) | projection | `delve-stage` (`vocabularyGuard`, `decisions.md:113`) |
| **Rows filed on siblings:** `rpg_delves.quests_json` (`delve-scope`); `quests.autopilotCompletionBand.*` (`dungeon-registries`); `countBand: none` on count-less templates (`dungeon-seed-contract`) | — | — |

## Drift found this session (report, not fixed here)

- **The brief's template list is not the registry's** (`slay-kind`, `slay-count-band`, `interact-with`,
  `explore-rooms-band`, `find-secret`, `escort/protect`, `reach-boss-with-N-standing`); the approved row
  (`spec-dungeon-registries.md:75`; ideal `:1099-1101`) fixes nine other ids, followed here. Each is a registry member
  and ask-first; `find-secret` is the strongest candidate (`Facts.isSecret` exists, `:64`).
- **`questScope`:** brief `room / route / delve`; approved `delve · domain · roster` (`spec-dungeon-seed-contract.md:97`;
  map `:127`). **Sink avoidance is D14, the inverse of the brief's framing** — reward *avoiding* a sink, not *require*
  one; both rules written (§5). **`countBand` has no `none`** (`:100`) while six of nine templates take no count — a
  Law 6 gap (ideal `:814`), filed on `dungeon-seed-contract`.
- **Line drift in the ideal:** the leaf list is `PredicateNode.cs:19-33`, not `:17-31` (`:1049`); the envelope is
  `RpgStore.Expeditions.cs:271-274` (audit A8's +1). `ItemCategoryTable.cs:29`'s `"quest"` and `SlotUnlock.cs:10`'s
  `ISlotUnlockRule` remain the only quest-shaped code in `src/`, read by nothing quest-like. **No quest-structure
  research file** exists under `docs/research/genre-mechanics/` (01–09 checked); Doran & Parberry and the DD row
  (ideal `:225`) are the ideal's own provenance.

## Design-gate checklist

```
[x] Subsystems: party dungeon, item drops (pipeline, rarity window), effect predicates (E3), tunables, DAL boundary,
    power ladder (Θ_run read only). Read this session: party-dungeon-map.md (row 14, gates, external deps, build
    order); the twelve approved specs at the sections cited; ideal §3.1, §4.9, §10, §11.1, §11.3 whole, §11.9/§11.10;
    audit §1(g)-(i), S1-1, S2-15/16, §4 (D14), §5; ssot-rarity §3.5 whole; spec-expeditions.md whole;
    decisions.md:113-116; DESIGN-GATE §5; tunables-ssot T5/T6.
[x] Code opened and cited by line: PredicateNode.cs, FactReader.cs, PredicateCompiler.cs, LootPipeline.cs (:60-140),
    DropTableValidator.cs (:30-90), SeededRng.cs, WeightedChoice.cs, BattleModels.cs (:7-60, :336-440),
    RpgStore.Expeditions.cs (:262-300), SoulEarnPolicy.cs (:49-57), ItemCategoryTable.cs:29, SlotUnlock.cs:10.
    Verified against CODE, not comments: the leaf range; Derive throws on unknown kinds; the exactly-once gate is
    the state transition; BattleActorResult has SpeciesId/Survived/Retreated but no role (setup join, §3).
[x] Surrounding sections read for every quoted rule: D14 with §4's table; S1-1 with loot §2/§7; Law 2 with §10 whole;
    attrition §9 with §8; ssot-rarity §3.5 with §3.6 as loot quotes it.
[ ] Nothing was run — no code exists. "Goldens untouched" is argued from adding no field to a hashed record; the
    suites are the proof and the first build task. Starting shapes are unmeasured.
[x] No §2 invariant contradicted. Readings added and named: reward banks at the close like the dungeon-clear relic, not
    through the pack (§4 step 4, against the brief's wording); alive-at-extraction vs never-downed (§1). Corrections
    propagated within this file. Rows landed 2026-09-05 (verification pass): `rpg_delves.quests_json` on delve-scope;
    `quests.autopilotCompletionBand.*` on registries; `countBand: none` on the seed contract; domain-catalog now calls
    `QuestPreflight.Run(corpus, domains, layouts, tuning)`; dungeon-loot's quest row banks at the close.
```
