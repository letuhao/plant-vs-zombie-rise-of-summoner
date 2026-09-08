using System.Text.Json;
using FusionRpg.CheatCore;
using FusionRpg.Contracts;
using FusionRpg.Core;
using FusionRpg.Data;
using FusionRpg.Data.Abstractions;
using FusionRpg.Data.Seed;
using FusionRpg.Server;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
var urls = Environment.GetEnvironmentVariable("FUSIONRPG_URLS");
var listenUrl = string.IsNullOrWhiteSpace(urls) ? "http://127.0.0.1:5088" : urls.Trim();
builder.WebHost.UseUrls(listenUrl);
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FUSIONRPG_DATA")))
    builder.WebHost.UseContentRoot(AppContext.BaseDirectory);

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.SetIsOriginAllowed(origin =>
         origin.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase)
         || origin.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));
builder.Services.AddSignalR().AddJsonProtocol();

// tunables-ssot.md §7.2: hosts load and inject; Core stays file-I/O-free. Migrated domains are
// copied next to the exe by FusionRpg.Server.csproj (M.1 contracts, M.2 loam).
var tuningDir = Path.Combine(AppContext.BaseDirectory, "data", "tuning");
FusionRpg.Core.Demons.Contracts.ContractPolicy.Configure(
    FusionRpg.Core.Demons.Contracts.ContractTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "contracts.v1.json"))));
FusionRpg.Core.World.Loam.LoamPolicy.Configure(
    FusionRpg.Core.World.Loam.LoamTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "loam.v4.json"))));
var worldTuning = FusionRpg.Core.World.WorldTuningLoader.Parse(
    File.ReadAllText(Path.Combine(tuningDir, "world.v5.json")));
FusionRpg.Core.World.WorldTuningHub.Configure(worldTuning);
FusionRpg.Core.World.Growth.RecruitPolicy.Configure(worldTuning.Growth);
FusionRpg.Core.Demons.SoulEarnPolicy.Configure(
    FusionRpg.Core.Demons.SoulEarnTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "souls.v1.json"))));
FusionRpg.Core.Demons.Patron.PatronPolicy.Configure(
    FusionRpg.Core.Demons.Patron.PatronTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "patron.v1.json"))));
FusionRpg.Core.Match.LawnDeployEventsTuningHub.Configure(
    FusionRpg.Core.Match.LawnDeployEventsTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "lawn-deploy-events.v1.json"))));
FusionRpg.Core.Match.Ai.ZombossDeployTuningHub.Configure(
    FusionRpg.Core.Match.Ai.ZombossDeployTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "zomboss-deploy-ai.v1.json"))));
FusionRpg.Core.Combat.Shield.ShieldPolicy.Configure(
    FusionRpg.Core.Combat.Shield.ShieldTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "shield.v1.json"))));
FusionRpg.Core.Combat.CombatPolicy.Configure(
    FusionRpg.Core.Combat.CombatTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "combat.v1.json"))));
FusionRpg.Core.Demons.Fusion.StarPolicy.Configure(
    FusionRpg.Core.Demons.Fusion.FusionTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "fusion.v2.json"))));
FusionRpg.Core.Status.StatusPolicy.Configure(
    FusionRpg.Core.Status.StatusTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "status.v1.json"))));
FusionRpg.Core.Stats.Derived.DerivedStatPolicy.Configure(
    FusionRpg.Core.Stats.Derived.DerivedStatTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "derived-stats.v2.json"))));
FusionRpg.Core.ActorSurface.ActorSurfaceCatalogHub.ConfigureAll(
    FusionRpg.Core.ActorSurface.AptitudeSurfaceCatalogLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "aptitude-catalog.v1.json"))),
    FusionRpg.Core.ActorSurface.DerivedStatSurfaceCatalogLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "derived-stat-catalog.v2.json"))),
    FusionRpg.Core.ActorSurface.StatusSurfaceCatalogLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "status-catalog.v1.json"))),
    FusionRpg.Core.ActorSurface.ResourceSurfaceCatalogLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "resource-catalog.v1.json"))),
    FusionRpg.Core.ActorSurface.ElementSurfaceCatalogLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "element-catalog.v1.json"))),
    FusionRpg.Core.ActorSurface.ActorSheetSurfaceCatalogLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "actor-sheet.v1.json"))));
FusionRpg.Core.Overlay.OverlayTuningHub.Configure(
    FusionRpg.Core.Overlay.OverlayTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "overlay.v1.json"))));
FusionRpg.Core.Stats.Derived.StatsTuningHub.Configure(
    FusionRpg.Core.Stats.Derived.StatsTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "stats.v1.json"))));
FusionRpg.Core.Expeditions.ExpeditionTuningHub.Configure(
    FusionRpg.Core.Expeditions.ExpeditionTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "expeditions.v1.json"))));
// party-dungeon D1.4: registries load first (pure, no tuning needed); DungeonTuningHub and
// EncounterTuningHub configure next, cross-checked against those registries at parse time; then
// DungeonRegistryHub last -- RoomKindDef.WeightMilli joins DungeonTuningHub at first property
// read, so the tuning hub must already be configured by the time any catalog row is touched.
var dungeonRegistryDir = Path.Combine(AppContext.BaseDirectory, "data", "seed", "dungeon", "_registry");
var dungeonRegistries = FusionRpg.Core.Dungeon.Registry.DungeonRegistryLoader.LoadAll(dungeonRegistryDir);
FusionRpg.Core.Dungeon.Tuning.DungeonTuningHub.Configure(
    FusionRpg.Core.Dungeon.Tuning.DungeonTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "dungeon.v3.json")), dungeonRegistries));
FusionRpg.Core.Dungeon.Tuning.EncounterTuningHub.Configure(
    FusionRpg.Core.Dungeon.Tuning.EncounterTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "encounter.v1.json")), dungeonRegistries));
FusionRpg.Core.Dungeon.Registry.DungeonRegistryHub.Configure(dungeonRegistries);
// LayoutTemplateHub after DungeonRegistryHub -- Load reads BandCatalog.All/RaidModeCatalog.All,
// both configured by the call above (D4.30's real prerequisite chain, 2026-09-07: closes D4.19/
// D4.21's own named "no LayoutTemplateCatalog exists" gap now that real content exists).
{
    var layoutRows = FusionRpg.Core.Delve.Roll.LayoutSeedFile.LoadAll(
        Path.Combine(AppContext.BaseDirectory, "data", "seed", "dungeon", "layouts"));
    var layoutLoad = FusionRpg.Core.Delve.Roll.LayoutTemplateCatalog.Load(
        layoutRows, FusionRpg.Core.Dungeon.Registry.BandCatalog.All, FusionRpg.Core.Dungeon.Registry.RaidModeCatalog.All);
    if (layoutLoad.Rejections.Count > 0)
        throw new InvalidOperationException(
            "data/seed/dungeon/layouts/*.json failed to load: " + string.Join("; ", layoutLoad.Rejections));
    FusionRpg.Core.Delve.Roll.LayoutTemplateHub.Configure(layoutLoad.Catalog);
}
FusionRpg.Core.SimDefaults.Configure(
    FusionRpg.Core.SimTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "sim.v1.json"))));
FusionRpg.Core.Progression.ProgressionTuningHub.Configure(
    FusionRpg.Core.Progression.ProgressionTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "progression.v1.json"))));
// passive-tree A1: server-only for now — no consumer reads PassiveTreeTuningHub.Tuning yet (B1+
// build the first ones). Wired here ahead of need so every later task finds it already configured,
// matching AptitudeTuningHub's own precedent.
FusionRpg.Core.PassiveTree.State.PassiveTreeTuningHub.Configure(
    FusionRpg.Core.PassiveTree.State.PassiveTreeTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "passive-tree.v1.json"))));
// species-build T1.1: server-only, mirroring AptitudeTuningHub's own shape — the injector never
// computes a species level, so it never loads this file.
FusionRpg.Core.Progression.SpeciesProgressionTuningHub.Configure(
    FusionRpg.Core.Progression.SpeciesProgressionTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "species-progression.v1.json"))));
// species-build T1.5: server-only, mirroring SpeciesProgressionTuningHub's own shape — the generation
// tool (tools/DemonBuildPlanGen) reads this file directly and never goes through this hub.
FusionRpg.Core.Demons.Generation.SpeciesBuildTuningHub.Configure(
    FusionRpg.Core.Demons.Generation.SpeciesBuildTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "species-build.v1.json"))));
// species-build T2.1: the committed, generated plan (tools/DemonBuildPlanGen owns writing it) —
// `SpeciesBuildPlanCatalog` is `demon-type-allocation`'s runtime reader for it. `tuningDir`'s parent is
// ".../data", so "generated/demons" (not "data/generated/demons") — same relative shape Program.cs
// already uses for the generated-tree convention.
FusionRpg.Core.Demons.Generation.SpeciesBuildPlanCatalog.Configure(
    FusionRpg.Core.Demons.Generation.SpeciesBuildPlanReader.Parse(
        File.ReadAllText(Path.Combine(
            Directory.GetParent(tuningDir)!.FullName, "generated", "demons", "_species-build-plan.json"))));
// species-build T4.4: ⛔ server-only — the Zomboss exists on battle and expedition surfaces, never the
// lawn, so wiring this into the injector would be dead weight.
FusionRpg.Core.Battle.Ai.ZombossAdaptiveTuningHub.Configure(
    FusionRpg.Core.Battle.Ai.ZombossAdaptiveTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "zomboss-adaptive.v1.json"))));
FusionRpg.Core.Battle.BattleTuningHub.Configure(
    FusionRpg.Core.Battle.BattleTuningLoader.Parse(
        // v2 -> v3 (battle-tempo tempo-content, 2026-09-05): adds speciesTempo.referenceIntervalMs.
        // v3 also closes a pre-existing, unrelated gap -- ruleset.loopGuardRoundMultiple, which
        // BattleTuningLoader already required (base-defense F2 WIP) but no version of this file ever
        // carried; see battle.v3.json's own _meta.noteV3LoopGuard.
        // v3 -> v4 (battle-tempo reaction-lane RL1, 2026-09-05): timeline.profiles.hybrid-atb.wReact
        // 0 -> 1, published via tools/tuning/publish.py (no hand-edit). classic-round/galaxy-sync
        // stay at 0. Config-only -- ReactionLane has no production caller yet (D14's own pattern), so
        // this is a tuning row change with no observable effect until a caller exists, matching the
        // module's own "buy the option, don't pay for the feature" framing.
        // v4 -> v5 (combat-unification Phase 7 F1, 2026-09-07, owner decision): hybrid.
        // secondaryWeightMilli 0 -> 300, published via tools/tuning/publish.py. Wave E3's mechanism
        // goes live -- an actor with a real ElementSecondary now carries a genuine two-component
        // attack payload instead of the pre-F1 single-primary shape.
        File.ReadAllText(Path.Combine(tuningDir, "battle.v5.json"))));
// battle-tempo battle-resources (2026-09-05): the per-resource share of BaseHp that
// BattleStatComposer seeds every actor's six pools from. Before this, every battle actor held all
// six pools at max 0, so no action in a battle could cost anything and reaction-lane's counter
// declined every time. Its own file rather than a battle.v{n}.json section because publish.py's
// `set` path refuses to invent keys, and the file forbids hand-editing
// (spec-battle-resources.md §2.2a).
FusionRpg.Core.Battle.BattleRuleset.ConfigureResources(
    FusionRpg.Core.Battle.BattleResourceTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "battle-resources.v1.json"))));
FusionRpg.Core.Battle.Board.SiegeTuningPolicy.Configure(
    FusionRpg.Core.Battle.Board.SiegeTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "siege.v1.json"))));
// A10 battle-board (spec-battle-board.md §1): a normal encounter's own seeded, bounded board size --
// siege gets its GridSpec from DistrictLayout, never from this roll, so this is a separate tunable.
FusionRpg.Core.Battle.Board.BattleBoardTuningPolicy.Configure(
    FusionRpg.Core.Battle.Board.BattleBoardTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "battle-board.v1.json"))));
// base-defense-todo.md 25.4: structure-catalog-import reads the committed corpus instead of the C#
// literal — the same AppContext.BaseDirectory-relative pattern the dungeon registry above already
// uses (`dungeonRegistryDir`), with the matching .csproj copy rule (FusionRpg.Server.csproj) so a
// published build finds it next to the exe, not just in a local dev checkout.
FusionRpg.Core.World.StructureCatalog.Configure(
    FusionRpg.Core.World.StructureSeed.StructureCorpus.Load(
        Path.Combine(AppContext.BaseDirectory, "data", "seed", "structures")));
FusionRpg.Core.Demons.SummoningTuningHub.Configure(
    FusionRpg.Core.Demons.SummoningTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "summoning.v1.json"))));
FusionRpg.Core.Aura.AuraTuningHub.Configure(
    FusionRpg.Core.Aura.AuraTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "aura.v1.json"))));
FusionRpg.Core.World.Ai.WorldAiPolicy.Configure(
    FusionRpg.Core.World.Ai.WorldAiTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "ai.v2.json"))));
FusionRpg.Data.Policies.SealedCompactionPolicy.Configure(
    FusionRpg.Data.Policies.DataTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "data.v1.json"))));
FusionRpg.Core.Power.PowerTuningHub.Configure(
    FusionRpg.Core.Power.PowerTuningLoader.Parse(
        // T4.2 (power-dial, 2026-08-24): v1 (bMilli=0) -> v2 (bMilli=400). v1 stays on disk --
        // reverting is pointing this back at power-scale.v1.json and un-bumping RulesetVersion, no
        // other code change (PS-7).
        File.ReadAllText(Path.Combine(tuningDir, "power-scale.v2.json"))));
FusionRpg.Core.Stats.Aptitudes.AptitudeTuningHub.Configure(
    FusionRpg.Core.Stats.Aptitudes.AptitudeTuningLoader.Parse(
        // class-system-todo.md P8.2/P8.3 (2026-08-27): v1 -> v2. Phase 0 six-resource coverage (2026-09-02): v2 -> v3, then v3 -> v4 (0.8: combat.heal.power generalised to resource.restore.{resource}) -- 32 edges added so every (family x resource) cell is fed, closing P7.2's poise gap. v2 stays on disk -- reverting is pointing this back at aptitudes.v2.json. passive-tree C6 (2026-09-06): v5 -> v6, pointEconomy gains skillPointsPerThetaMilliByScope (D34) -- v5 stays on disk. passive-tree D55 (2026-09-06): v6 -> v7, published via tools/tuning/publish.py -- demonType/aspect/uniqueDemon skillPointsPerThetaMilliByScope moved from the borrowed-placeholder {4,4,6} to the {3,4,4,6}-ratio-derived {15,15,22} against the already-settled commander=11 (spec-tree-state.md open question 3) -- v6 stays on disk.
        File.ReadAllText(Path.Combine(tuningDir, "aptitudes.v8.json"))));
// Server-side only (spec-action-catalog.md, T30): actions are battle-mode and the injector never
// sees one, so the rung ladder has no reason to load there.
FusionRpg.Core.Actions.Rungs.RungPolicy.Configure(
    FusionRpg.Core.Actions.Rungs.RungTableLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "action-rungs.v1.json"))));
// battle-tempo action-timing (2026-09-05): every seeded action's wind-up/recovery/timeCost/cooldown,
// derived at RpgStore.BuildActionCatalog (D2), never by the seeder.
FusionRpg.Core.Actions.ActionTimingPolicy.Configure(
    FusionRpg.Core.Actions.ActionTimingTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "action-timing.v1.json"))));
// battle-tempo reaction-lane RL2/RL3 (2026-09-05/06): the counter's poise spend and riposte
// share. v1 -> v2/v3 (RL3, 2026-09-06): sized against the landed Phase 2 sweep -- poiseSpend=50
// matches AptitudeGuardEconomy.flatCommitCost, riposteShareCapMilli=400 matches its
// riposteShareCapPermille, so a counter costs about what a guard costs from the same pool
// (spec-reaction-lane.md's own "the spend range is this module's to size"). Loaded regardless
// so ReactionCounter.TryCounter always has a real, non-hardcoded number wherever a caller
// reaches it.
FusionRpg.Core.Battle.Timeline.ReactionLanePolicy.Configure(
    FusionRpg.Core.Battle.Timeline.ReactionLaneTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "reaction-lane.v3.json"))));
// item-ideal.md, rarity-bands (module 7): no Hub needed today -- SeedRarityLadder consumes this
// parse once, at boot, to populate rarity_budget rows. A future consumer (drop-volume/enhance-reroll)
// reads those rows back through RpgStore.GetRarityBudget, not this parsed value directly.
var itemRarityTuning = FusionRpg.Core.Items.ItemRarityTuning.Parse(
    File.ReadAllText(Path.Combine(tuningDir, "item-rarity.v1.json")));
// item-ideal.md, item-power-reads (module 9): no Hub needed either -- parsed and validated at boot
// (the powerDisplayBandPercent == DriftTolerancePercent check fails fast here rather than at first
// use), consumed by module 10 item-card once it exists as the real production caller.
var itemPowerTuning = FusionRpg.Core.Items.Power.ItemPowerTuningLoader.Parse(
    File.ReadAllText(Path.Combine(tuningDir, "item-power.v1.json")));
_ = itemPowerTuning;
// item-ideal.md, drop-volume (module 11): D18's volume curve, D38's kill rate and Correction 5's
// re-solved pity thresholds. Parsed and validated at boot so a self-inconsistent balance edit fails
// here rather than at the first drop; ImportLootCorpus below consumes it.
var dropVolumeTuning = FusionRpg.Core.Items.Drops.DropVolumeTuning.Parse(
    File.ReadAllText(Path.Combine(tuningDir, "item-drop-volume.v1.json")));
// item-ideal.md, threshold-grants (module 12): D3's frame-mix recovery curve. Parsed and validated at
// boot for the same reason as the two above — a knot list that is flat over any interval reinstates
// the step function the curve exists to prevent, and that failure must land here rather than at the
// first hybrid body priced against it.
var frameMixTuning = FusionRpg.Core.Items.Thresholds.FrameMixTuning.Parse(
    File.ReadAllText(Path.Combine(tuningDir, "item-frame-mix.v1.json")));
_ = frameMixTuning;
// item-ideal.md, salvage-craft (module 14): the ten-operation reference cost table and the ten-rung
// salvage coefficients. Parsed and validated at boot for the same reason as the four above — the
// parser refuses rather than defaults, and D24's "imbue prices on bore's curve" is checked at load,
// so a balance pass that moves one and forgets the other fails here rather than at the first
// crafted socket. Consumed by SeedSalvageYield and by the recipe import below.
var materialTuning = FusionRpg.Core.Items.Materials.MaterialTuning.Parse(
    File.ReadAllText(Path.Combine(tuningDir, "materials.v1.json")));
// item-ideal.md, enhance-reroll (module 15): the gain asymptote's K, the three risk bands, the craft
// pity threshold, the transfer ratio and the reroll price's two legs. ⛔ THE soft cap lives in this
// file — the parser refuses a closed top band, a zero success floor, a lossless transfer ratio and a
// rung-dominant reroll price at BOOT, so an edit that would put a hard ceiling back on +X fails here
// rather than at the first crafted item. Consumed by SeedRerollCostMult below.
var enhancementTuning = FusionRpg.Core.Items.Mutation.EnhancementTuning.Parse(
    File.ReadAllText(Path.Combine(tuningDir, "enhancement.v1.json")));
// item-ideal.md, sockets (module 16): the per-role socket ceiling, the per-rung grant windows, the
// resonance shapes, the removal tiers and D20's ingredient count. Parsed and validated at boot for
// the same reason as the five above — the parser refuses a ceiling above the structural 4, a
// rarityGrant table whose adjacent windows do not overlap (which would make socket count a strict
// ladder), a `standard` ceiling row D14 puts out of scope, and an ingredient count no item could
// ever hold. Consumed by SeedSocketGrants and SeedComboRecipes below.
var socketTuning = FusionRpg.Core.Items.Sockets.SocketTuning.Parse(
    File.ReadAllText(Path.Combine(tuningDir, "sockets.v1.json")));
// item-ideal.md, strain-splice-gen (module 21): the min-tier plan D20's four ingredients are priced
// on, the per-shape base tier, the learnability bar and the two name-distinctness thresholds.
// CROSS-VALIDATED against socketTuning at parse time — a min-tier plan whose length disagrees with
// the ingredient count, or a tier outside the shipped insert ladder, fails here rather than
// producing 102 combinations the evaluator can never match. ⛔ It deliberately re-declares NONE of
// module 16's socket numbers; the seedsmith parser refuses a file that does.
var strainSpliceTuning = FusionRpg.Core.Items.Sockets.StrainSpliceTuning.Parse(
    File.ReadAllText(Path.Combine(tuningDir, "strain-splice.v1.json")), socketTuning);
// item-ideal.md, uniques (module 17): the rung floor, the identity spread, the shared 1.5 AE premium,
// `narrow`'s ceiling, the role quota and the parity band. Parsed at boot for the same reason as the
// six above — the parser refuses an inverted parity band, a forbiddenRoles entry naming a role that
// does not exist, and a budget drift tolerance that has drifted from definitions §7's shared number,
// so a bad edit fails here rather than at the first imported unique. Consumed by SeedUniqueEligible.
var uniqueTuning = FusionRpg.Core.Items.Uniques.UniqueTuning.Parse(
    File.ReadAllText(Path.Combine(tuningDir, "uniques.v1.json")));
// item-ideal.md, consumables (module 18): which of the six classes and four use contexts v1 authors,
// `bands.v1.json`'s mirrored grade map, §4.4's authoring ceiling and the run-start binding priority.
// Parsed and validated at boot for the same reason as the seven above — the parser refuses a
// gradeTierMap that is not a bijection onto 1..5, an empty authored list, and (BY NAME) a withdrawn
// `carryLimit` key, so a balance edit that would silently do nothing fails here rather than at the
// first dispatch. ⛔ Nothing seeds a rarity_budget key from it: consumables never enter the ladder.
var consumableTuning = FusionRpg.Core.Items.Consumables.ConsumableTuning.Parse(
    File.ReadAllText(Path.Combine(tuningDir, "consumables.v1.json")));
_ = consumableTuning;
// item-ideal.md, item-surfaces (module 20): the GG-50 render bands, the compendium's four-state
// boundary and name-only tail cap, the loot filter's default and its two per-content-event watch
// numbers, and each surface's GG-44 unlock key. Parsed at boot for the same reason as the eight
// above — the parser refuses an unordered render band, a zero one-away distance (which would name
// the active set) and, by name, a surfaceUnlocks table that has forgotten one of the six surfaces,
// because a surface with no declared unlock renders as present-but-dead. ⛔ Nothing here meters the
// player: every number is a presentation threshold and D26 keeps it that way.
var itemSurfaceTuning = FusionRpg.Core.Items.Surfaces.ItemSurfaceTuning.Parse(
    File.ReadAllText(Path.Combine(tuningDir, "item-surfaces.v1.json")));
// item-ideal.md, charm-carry (module 22, split out of 12 by D40): the AP cost domain, the capacity
// LADDER (its last rung is the last AUTHORED rung, never a ceiling — AGENTS.md), the axis and copy
// caps, and the run-start binding shape. Parsed at boot for the same reason as the nine above — the
// parser refuses a starting capacity below the largest charm (which would make every signet dead
// content), a `unique_carry` cap looser than the default, a non-negative binding priority, and BY
// NAME both a `maxCapacityAp`-shaped ceiling key and a `player`/`match` binding owner kind, because
// D33(a) binds charms at unique-actor: scope and `player:` resolves match-wide in the stat layer —
// a charm that buffs the zombies. A bad edit fails here rather than at the first dispatch.
var charmAttunementTuning = FusionRpg.Core.Items.Thresholds.CharmAttunementTuning.Parse(
    File.ReadAllText(Path.Combine(tuningDir, "charm-attunement.v1.json")));
_ = charmAttunementTuning;

// Default: {ServerExeDir}/data/{rpg-hot,rpg-media}.sqlite — override with FUSIONRPG_DATA only for tests/special runs.
var dataDir = Environment.GetEnvironmentVariable("FUSIONRPG_DATA");
if (string.IsNullOrWhiteSpace(dataDir))
    dataDir = Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(dataDir);
Console.WriteLine($"[data] {dataDir} (hot=rpg-hot.sqlite media=rpg-media.sqlite)");
builder.Services.AddSingleton(new RpgStore(dataDir));
builder.Services.AddSingleton(sp => new TypeIconStore(sp.GetRequiredService<RpgStore>()));
builder.Services.AddSingleton<FusionRpg.Core.Power.IPowerIndexProvider>(sp =>
    new FusionRpg.Server.Power.ServerPowerIndexProvider(sp.GetRequiredService<RpgStore>(), FusionRpg.Core.Power.PowerTuningHub.Tuning));
builder.Services.AddSingleton<IColdArchiveWriter>(sp => new ColdArchiveWriter(sp.GetRequiredService<RpgStore>()));
builder.Services.AddSingleton<IColdArchiveCatalog>(sp => new ColdArchiveCatalog(sp.GetRequiredService<RpgStore>()));
builder.Services.AddSingleton<IHotCompactor>(sp => new HotCompactor(sp.GetRequiredService<RpgStore>()));
builder.Services.AddSingleton<CompactionWorker>();
builder.Services.AddSingleton<EventIngest>();
builder.Services.AddSingleton<InjectorCommandInbox>();
builder.Services.AddSingleton<FusionRpg.Core.Effects.EffectGrantSession>();
builder.Services.AddSingleton<FusionRpg.Core.Effects.SimEffectHost>();
builder.Services.AddSingleton<UniqueActorService>();
builder.Services.AddSingleton<PerfWindowBuffer>();
builder.Services.AddSingleton<WebMatchService>();
builder.Services.AddSingleton<ExpeditionService>();
builder.Services.AddSingleton<IDelveLivePush, HubDelveLivePush>();
builder.Services.AddSingleton<DelveBattleSessionManager>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<EventIngest>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<CompactionWorker>());
builder.Services.AddHostedService<UniqueActorDeployWatchdog>();
if (SimFlags.Enabled)
{
    builder.Services.AddSingleton<SimService>();
    builder.Services.AddHostedService<SimHeartbeatHost>();
}

var app = builder.Build();
var store = app.Services.GetRequiredService<RpgStore>();
store.Init();
// catalog-runtime (demon-seed module 13, spec-catalog-runtime.md §7 step 5) — THE FLIP, 2026-09-05:
// species now load from the store `species-import` writes (data/generated/demons/**.json, imported
// via `tools/DemonSpeciesImport`), not the compiled `DemonSpeciesCatalog.Generated.cs` snapshot.
// Owner decision (2026-09-05): the classification/generation pipeline is real and current
// (829 species, up from 84); further review happens against the real, running roster, not by
// withholding it. Loaded once here, immutable for the process lifetime (§3a — no live reload; an
// import needs a server restart, already how this repo deploys).
FusionRpg.Core.Demons.DemonSpeciesCatalog.Configure(store.BuildDemonSpeciesSnapshot());
store.SeedRarityLadder(itemRarityTuning);
// item-ideal.md, salvage-craft (module 14): `salvage_yield`, the sixth `rarity_budget` key, whose
// shape ssot-rarity.md §5 recorded as "awaiting I9" until this module decided it. Seeded here rather
// than inside SeedRarityLadder so module 7's own seeding never grows a dependency on a later
// module's tuning file.
store.SeedSalvageYield(materialTuning.Salvage);
// item-ideal.md, enhance-reroll (module 15): `reroll_cost_mult`, the seventh `rarity_budget` key,
// whose shape ssot-rarity.md §5 recorded as "awaiting I7" until this module decided it. Same
// placement rule as salvage_yield above — a later module's tuning never reaches module 7's seeding.
store.SeedRerollCostMult(enhancementTuning);
// item-ideal.md, sockets (module 16): `socket_min` and `socket_max`, the eighth and ninth
// `rarity_budget` keys, whose shape ssot-rarity.md §5 recorded as "awaiting I4" until this module
// decided it. Same placement rule as the two above.
store.SeedSocketGrants(socketTuning);
// item-ideal.md, uniques (module 17): `unique_eligible`, the tenth `rarity_budget` key and the ONE key
// ssot-uniques.md §5.3 asked for. Derived from each rung's ordinal against the tuning floor rather
// than authored as a second per-rung table, and seeded here under the same placement rule as the three
// above — a later module's tuning never reaches module 7's own seeding.
store.SeedUniqueEligible(uniqueTuning);
// action-instance-and-grant (A21, T59.4): the corpus import — content-seeded, idempotent (a
// re-import of an unchanged brief moves zero revisions, T30's own guard). Defaults ON, matching
// this file's own kill-switch convention (FUSIONRPG_PERF, FUSIONRPG_NO_BROWSER) rather than
// requiring an opt-in for content that already exists and is safe to re-run every start. Never a
// live game/injector path -- this runs once, here, before the server accepts any connection.
if (Environment.GetEnvironmentVariable("FUSIONRPG_ACTION_CORPUS_IMPORT") != "0")
{
    var actionCorpusTemplatePath = Path.Combine(AppContext.BaseDirectory, "data", "tuning", "action-corpus-cost-templates.v1.json");
    if (File.Exists(actionCorpusTemplatePath))
    {
        var actionCostTemplate = FusionRpg.Core.Actions.Corpus.ActionCorpusCostTemplateLoader.Parse(File.ReadAllText(actionCorpusTemplatePath));
        var actionBriefs = new List<FusionRpg.Core.Actions.Corpus.ActionCorpusBrief>();
        foreach (var briefFile in new[] { "committed-round-1.json", "committed-round-2.json" })
        {
            var briefPath = Path.Combine(AppContext.BaseDirectory, "data", "seed", "actions", briefFile);
            if (File.Exists(briefPath))
                actionBriefs.AddRange(FusionRpg.Core.Actions.Corpus.ActionCorpusBriefJson.Parse(File.ReadAllText(briefPath)));
        }
        FusionRpg.Data.ActionCorpusImporter.Import(store, actionBriefs, actionCostTemplate, FusionRpg.Core.Actions.Rungs.RungPolicy.Table);
    }
}
{
    var comboRecipes = FusionRpg.Core.Items.Sockets.ResonanceGenerator.Generate(socketTuning);
    // item-ideal.md, strain-splice-gen (module 21): every Strain/Splice recipe on the seed path is
    // checked against the DERIVED 102-cell grid before it is written — the id is a real grid cell,
    // it takes D20's four ingredients, its `min_sockets` is the derived value and its host role can
    // actually hold that many inserts. ⛔ Live, not dead code: today's 25 generated resonances are
    // neither Strain nor Splice so it refuses nothing, and it starts refusing the moment the
    // authored 102 land beside them — which is the point of putting the guard in before the content.
    // The archetype axis is READ from module 13's registry, never declared in Core (§7.2).
    var archetypes = new List<string>();
    var buildThemesPath = Path.Combine(
        AppContext.BaseDirectory, "data", "seed", "items", "_registry", "build-themes.v1.json");
    if (File.Exists(buildThemesPath))
    {
        using var themesDoc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(buildThemesPath));
        foreach (var theme in themesDoc.RootElement.GetProperty("themes").EnumerateArray())
            if (theme.TryGetProperty("archetype", out var arch) &&
                arch.GetString() is { Length: > 0 } value && !archetypes.Contains(value))
                archetypes.Add(value);
    }
    if (archetypes.Count > 0)
    {
        var refused = 0;
        foreach (var recipe in comboRecipes)
            foreach (var problem in FusionRpg.Core.Items.Sockets.StrainSpliceGrid.ValidateRecipe(
                         recipe, socketTuning, archetypes, strainSpliceTuning))
            {
                Console.WriteLine($"[items] combo recipe refused: {problem.Detail}");
                refused++;
            }
        if (refused > 0)
            Console.WriteLine($"[items] {refused} Strain/Splice refusals — see strainsplice.* above");
    }
    // The 25 resonance combinations, GENERATED from the element roster rather than authored
    // (ssot-sockets.md §4.4). Module 21's 102 Strains and Splices land in the same table beside them;
    // this seed never deletes a row it did not write.
    store.SeedComboRecipes(comboRecipes);
}

// item-ideal.md, salvage-craft (module 14): the authored recipe corpus
// (data/seed/items/recipes/*.json) resolved against the reference cost table. Never fatal, same rule
// as the loot corpus below. ⛔ Entries this build cannot resolve are refused BY NAME with the module
// that unblocks them — never silently dropped — and the count is printed so a corpus regression is
// visible at boot.
// ⭐ Kept rather than discarded after the import: the workbench executor resolves every price
// through this same catalog, so the running server and the imported rows can never be two
// different corpora.
FusionRpg.Core.Items.Materials.MaterialRecipeCatalog? recipeCatalog = null;
{
    var recipesDir = Path.Combine(AppContext.BaseDirectory, "data", "seed", "items", "recipes");
    if (Directory.Exists(recipesDir))
    {
        try
        {
            var catalog = FusionRpg.Core.Items.Materials.MaterialRecipeCatalog.Load(
                Directory.EnumerateFiles(recipesDir, "*.json").OrderBy(f => f, StringComparer.Ordinal)
                    .Select(File.ReadAllText),
                materialTuning);
            var imported = store.ImportRecipeCatalog(catalog);
            recipeCatalog = catalog;
            Console.WriteLine($"[craft] imported {imported} recipes, refused {catalog.Refusals.Count}");
            foreach (var refusal in catalog.Refusals)
                Console.WriteLine($"[craft]   refused {refusal.RecipeId}: {refusal.Rule} — {refusal.Detail}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[craft] recipe import failed — no recipes loaded: {ex.Message}");
        }
    }
}

// ⭐ item modules 14/15/16 — THE WORKBENCH EXECUTOR. All three shipped their own half of the
// salvage → craft → enhance → socket loop and all three recorded the same blocker: nothing called
// them against a stored item, so `TrySpendRecipe`, `AppendMutationOp` and `SetSockets` each had zero
// production callers. This is the joint. It is registered only when the recipe corpus loaded —
// a workbench with no prices could only ever refuse, and a route that always refuses is worse than
// a route that is absent, because it looks wired.
// ⏸ Module 16 shipped the gem catalog as seed JSON, not a table — the same boot-time stopgap shape
// `BaseTypeSocketMaxCorpus` is. Loaded ONCE here and handed to all three item route groups: the
// workbench (`socket-insert`), the surface routes (`/combinations`) and the card routes each need an
// insert's real element, and three separate loads would be three chances to disagree about it.
var gemInserts = FusionRpg.Server.GemInsertCorpus.Load(
    Path.Combine(AppContext.BaseDirectory, "data", "seed", "items", "gems"));
// ⏸ The same boot-time stopgap, for the same missing table (module 6 shipped the 740-entry base-type
// corpus as seed JSON and no `item_base_type`). Hoisted above `MapItemSurfaces` by item-content T3 so
// the armoury row and the item card read ONE corpus: two loads are two chances to disagree about what
// an item is called, which is exactly the class of defect T2 fixed inside the card.
var itemBaseTypes = FusionRpg.Server.ItemBaseTypeCorpus.Load(
    Path.Combine(AppContext.BaseDirectory, "data", "seed", "items", "base-types"));

// item-ideal.md, drop-volume (module 11) — `item_base_type`, the table the two ⏸ comments above
// already name as their own target. `BuildLiveLootContentView`'s own `BaseTypesFor` reads it (D4.12,
// party-dungeon-todo.md, 2026-09-07); the two boot-time JSON readers above stay as they are (display /
// socket-max shape, a different job) rather than being folded into this import.
{
    var baseTypesDir = Path.Combine(AppContext.BaseDirectory, "data", "seed", "items", "base-types");
    try
    {
        var baseTypeRows = FusionRpg.Core.Items.Drops.BaseTypeSeedFile.LoadAll(baseTypesDir);
        store.ImportBaseTypes(baseTypeRows);
        Console.WriteLine($"[items] imported {baseTypeRows.Count} base types");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[items] base-type import failed — no base types loaded: {ex.Message}");
    }
}

FusionRpg.Server.ItemWorkbench? itemWorkbench = null;
if (recipeCatalog is { } workbenchRecipes)
{
    // ⏸ Module 6 shipped the base-type corpus but no `item_base_type` table, so socketMax comes off
    // the seed JSON at boot. Deleted the day that table exists — see BaseTypeSocketMaxCorpus.
    var socketMaxForBaseType = FusionRpg.Server.BaseTypeSocketMaxCorpus.Load(
        Path.Combine(AppContext.BaseDirectory, "data", "seed", "items", "base-types"));
    itemWorkbench = new FusionRpg.Server.ItemWorkbench(
        store, materialTuning, workbenchRecipes, enhancementTuning, socketTuning, socketMaxForBaseType,
        gemInserts);
}
// item-ideal.md, item-card (module 10): N1's item_display_template, seeded from the already-shipped
// data/seed/items/display-templates/*.json (98 rows, one per affix family) -- never re-authored here.
{
    var displayTemplatesDir = Path.Combine(AppContext.BaseDirectory, "data", "seed", "items", "display-templates");
    if (Directory.Exists(displayTemplatesDir))
    {
        var rows = Directory.EnumerateFiles(displayTemplatesDir, "*.json")
            .SelectMany(f => FusionRpg.Core.Items.Display.DisplayTemplates.Parse(File.ReadAllText(f)))
            .ToList();
        store.SeedItemDisplayTemplates(rows);
    }
}

// item-ideal.md, drop-volume (module 11): the runtime drop-table corpus (data/seed/loot/tables*.json).
// Validated whole and imported in one transaction — E14's all-or-nothing policy. Never fatal: a
// broken loot corpus must not take the server down, the same rule the content boot below follows.
{
    var lootDir = Path.Combine(AppContext.BaseDirectory, "data", "seed", "loot");
    if (Directory.Exists(lootDir))
    {
        try
        {
            var corpus = FusionRpg.Core.Items.Drops.LootCorpusReader.Merge(
                Directory.EnumerateFiles(lootDir, "tables*.json")
                    .Select(f => FusionRpg.Core.Items.Drops.LootCorpusReader.Parse(File.ReadAllText(f))));
            store.ImportLootCorpus(corpus, dropVolumeTuning, new FusionRpg.Core.Items.Drops.DropContentLookups(
                CurrencyExists: id => string.Equals(id, "souls", StringComparison.Ordinal),
                RarityIdExists: FusionRpg.Core.Items.RarityLadder.RungIds.Contains));
            Console.WriteLine($"[loot] imported {corpus.Tables.Count} drop tables, {corpus.Sources.Count} loot sources");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[loot] drop-table import failed — no loot tables loaded: {ex.Message}");
        }
    }
}

// item-ideal.md, threshold-grants (module 12): the authored set catalog
// (data/seed/items/sets/*.json) into ssot-sets.md §4.2's three tables. Never fatal, same rule as the
// loot corpus above. ⛔ The tier CONTAINERS these ids name cannot be bound yet — ContainerKind ships
// six values and D27's `set` is not one of them (X7, effect-atom's own ask) — so this populates the
// breakpoint table the evaluator reads and stops there. A wiring gap, named, not a silent drop.
{
    var setsDir = Path.Combine(AppContext.BaseDirectory, "data", "seed", "items", "sets");
    if (Directory.Exists(setsDir))
    {
        try
        {
            var sets = Directory.EnumerateFiles(setsDir, "*.json")
                .SelectMany(f => FusionRpg.Core.Items.Thresholds.SetCorpus.Parse(File.ReadAllText(f)))
                .ToList();
            store.ImportSetCorpus(sets);
            Console.WriteLine($"[sets] imported {sets.Count} item sets, "
                              + $"{sets.Sum(s => s.Members.Count)} members, {sets.Sum(s => s.Tiers.Count)} tiers");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[sets] set corpus import failed — no sets loaded: {ex.Message}");
        }
    }
}

// item-ideal.md, charm-carry (module 22): the authored charm catalog (data/seed/items/charms/*.json)
// into ssot-charms.md §4.2's charm_def and charm_resonance. Never fatal, same rule as the two corpus
// imports above. `resonance.json` is a SEPARATE population and is deliberately imported only into
// charm_resonance — §4.2: "a `charm.` container with no charm_def row is not attunable", which is how
// a resonance tier can be granted BY the pouch and never sit in it. ⛔ The containers these ids name
// still cannot be bound: ContainerKind ships six values and D27's `charm` is not one of them (X7,
// effect-atom's own ask), so this populates the def and breakpoint tables the gate and the evaluator
// read, and stops there. A wiring gap, named, not a silent drop.
{
    var charmsDir = Path.Combine(AppContext.BaseDirectory, "data", "seed", "items", "charms");
    if (Directory.Exists(charmsDir))
    {
        try
        {
            var defs = new List<FusionRpg.Core.Items.Thresholds.CharmDef>();
            var resonance = new List<FusionRpg.Core.Items.Thresholds.CharmResonanceRow>();

            foreach (var f in Directory.EnumerateFiles(charmsDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                var json = File.ReadAllText(f);
                if (Path.GetFileNameWithoutExtension(f).Equals("resonance", StringComparison.OrdinalIgnoreCase))
                    resonance.AddRange(FusionRpg.Core.Items.Thresholds.CharmResonance.DeriveTable(json));
                else
                    defs.AddRange(FusionRpg.Core.Items.Thresholds.CharmCorpus.Parse(json));
            }

            foreach (var d in defs)
            {
                var fails = FusionRpg.Core.Items.Thresholds.CharmPouchGate.ValidateForCarry(d, charmAttunementTuning);
                foreach (var fail in fails) Console.WriteLine($"[charms] {fail}");
            }

            store.ImportCharmCorpus(defs, resonance);
            Console.WriteLine($"[charms] imported {defs.Count} attunable charms, "
                              + $"{resonance.Count} resonance breakpoints");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[charms] charm corpus import failed — no charms loaded: {ex.Message}");
        }
    }
}

// E46 (player-content-boot): AtomImporter was only ever invoked from a dev script
// (scripts/deploy-play.ps1), never from a player's own install — so a real player's content tables
// were never populated and the server ran forever on the shipped code fallback with nothing saying
// so. Self-heal here, gated on catalog_revision so a normal relaunch never re-imports (that would
// bump the revision and make every already-rolled effect_instance unbindable,
// spec-player-content-boot.md §4): revision 0 means this is the first launch since unzip (or a
// corrupted/skipped install), so this is also the "first-run repair" the spec's own §3.1 asks for —
// for this launcher there is no separate install step, so both happen at the same moment, in the
// same code path. RunSelfHealing never throws: a broken or missing seed tree must not take the
// server down with it (§3.2, §4) — the fallback stays playable either way.
var contentBoot = SeedImportRunner.RunSelfHealing(store, AppContext.BaseDirectory);
store.RecordContentBootOutcome(contentBoot.ContentSource, contentBoot.Detail);
Console.WriteLine(contentBoot.Status switch
{
    SeedImportStatus.Imported =>
        $"[content] imported the seed tree — catalog now at revision {store.GetCatalogRevision()}",
    SeedImportStatus.AlreadyCurrent =>
        $"[content] catalog already at revision {store.GetCatalogRevision()} — no import needed",
    SeedImportStatus.SeedTreeNotFound =>
        $"[content] no seed tree found near the server — running on the shipped code fallback ({contentBoot.Detail})",
    _ => $"[content] seed import failed — running on the shipped code fallback: {contentBoot.Detail}",
});

// H9's own "committed" acceptance bullet (passive-tree-todo.md, found 2026-09-07): ImportTreeCatalog/
// ImportTreeCatalogFiles (task C4/C5) had no production caller anywhere — a bound
// data/generated/passive-tree/*.json catalog never reached the store on its own. Independent of the
// atom-content self-heal above and never gating it or being gated by it, matching this codebase's own
// established "a lint, never a gate" pattern for boot-time content checks: H9's corpus is still
// partial, and a tree-catalog import finding nothing (or failing) must never slow or block anything
// else in content boot. Never throws, for the identical reason SeedImportRunner.RunSelfHealing doesn't.
var treeBoot = PassiveTreeImportRunner.RunSelfHealing(store, AppContext.BaseDirectory);
Console.WriteLine(treeBoot.Status switch
{
    PassiveTreeImportStatus.Imported =>
        $"[content] imported the passive-tree catalog — {treeBoot.Outcome!.TreesImported} tree(s), " +
        $"now at revision {store.GetTreeCatalogRevision()}",
    PassiveTreeImportStatus.AlreadyCurrent =>
        $"[content] passive-tree catalog already at revision {store.GetTreeCatalogRevision()} — no import needed",
    PassiveTreeImportStatus.TreeNotFound =>
        $"[content] no generated passive-tree catalog found near the server ({treeBoot.Detail})",
    _ => $"[content] passive-tree catalog import failed: {treeBoot.Detail}",
});

// E20: without this, ElementTable/PowerTables.Current never move off their shipped code copy, and
// an imported roster or coefficient row changes the content hash and nothing else (completeness
// audit A2). A store with nothing imported behaves exactly as before.
store.LoadContentIntoRuntime();
// item-ideal.md, item-power-reads (module 9): the rarity-keyed power budget, with a REAL ceilingFor.
// Runs here and not beside SeedRarityLadder above because it needs all three halves at once — the
// imported `rarity` rows and containers (the self-healing import two blocks up), the seeded
// `rarity_budget.power_ceiling` column, and the coefficients LoadContentIntoRuntime just published.
// A lint, never a gate: an over-budget container is reported naming its id and the server boots
// anyway (ContentValidation's own "a content test that fails naming the offender — and never a
// generation input"). The `evaluated` count is the load-bearing half of the line: it was
// structurally 0 for as long as no caller passed a ceilingFor at all.
{
    var rarityBudget = store.ValidateRarityPowerBudget();
    var ceilings = store.GetRarityPowerCeilings();
    Console.WriteLine(
        $"[items] {rarityBudget.Render("rarity power budget")} "
        + $"— {ceilings.PricedRungs} rung(s) priced, pinAE {ceilings.RenderPinAe()}");
}
// Fail fast on demon content errors: the catalogs are lazy, and a bad species surfacing on the
// first request would permanently poison WaveCatalog's static initializer (review I6).
_ = FusionRpg.Core.Demons.DemonSpeciesCatalog.All;
// base-defense siege-waves §3.5 (task 12.4, 2026-09-06): wave composition moved out of a hand-written
// WaveCatalog.cs array into data/tuning/waves.v1.json — species selection by rarity band still
// happens in Core (WaveCatalog.Band/Enemies, reused verbatim by the loader), only WHICH waves exist
// and their picks are now data. Must run after DemonSpeciesCatalog.Configure above: Band() reads
// DemonSpeciesCatalog.All. Same Loader.Parse(File.ReadAllText(...)) -> Configure(...) shape as every
// other data/tuning/*.json load in this file.
FusionRpg.Core.Battle.WaveCatalog.Configure(
    FusionRpg.Core.Battle.WaveCatalogLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "waves.v1.json"))));
// ds 18 fusion-recipe-runtime §3 step 4 — THE FLIP, 2026-09-06: recipes now load from the committed
// seed fusion-recipe-reconcile owns writing, not a live BuildDeterministicOnly() recomputation
// (T8.4's own transitional call). Today's committed file carries 695 real deterministic recipes
// plus 14 real crossRungGapFill entries for every Almanac deficit — Checkpoint 8a's own propose
// pass, performed 2026-09-06 by reasoning directly (this environment cannot reach a live LM
// Studio endpoint), run through the unmodified reconcile()/vote/validate pipeline exactly like any
// other proposal source. A missing/unparseable file fails loudly here rather than falling back to
// the old live algorithm, matching spec-catalog-runtime.md's own binding rule for the sibling
// catalog.
{
    var fusionRecipesPath = Path.Combine(AppContext.BaseDirectory, "data", "generated", "demons", "_fusion-recipes.json");
    if (!File.Exists(fusionRecipesPath))
        throw new InvalidOperationException(
            $"Fusion recipe seed not found at '{fusionRecipesPath}'. Run 'python tools/seedsmith/seedsmith/" +
            "adapters/demons/fusion/reconcile.py' (add --deterministic-only for a model-free pass) against " +
            "the data directory this host points at.");
    var fusionRecipes = FusionRpg.Core.Demons.Fusion.FusionRecipeSeedReader.Parse(File.ReadAllText(fusionRecipesPath));
    FusionRpg.Core.Demons.Fusion.DemonRecipeCatalog.Configure(fusionRecipes);
}
// Boot sweep: web matches logged but never ingested (crash window) re-resolve deterministically.
var sweptMatches = app.Services.GetRequiredService<WebMatchService>().SweepUnresolved();
if (sweptMatches > 0)
    Console.WriteLine($"[web-match] boot sweep re-ingested {sweptMatches} logged matches");
var portraits = app.Services.GetRequiredService<TypeIconStore>().BackfillPortraitsFromDumps();
if (portraits > 0)
    Console.WriteLine($"[icons] backfilled {portraits} portraits from dump layer 'image'");
// C1 (completeness-audit.md): entity: bindings are never durable — IL2CPP reuses the pointer, so
// one surviving a restart would attach to whatever object takes its address next. A fresh boot is
// exactly the moment every entity: binding from the previous process is guaranteed stale, whether
// the last shutdown was clean or a crash. A no-op today (nothing binds one yet — completeness-audit
// A4), and the cheap place for this to already be correct once something does.
var clearedBindings = app.Services.GetRequiredService<RpgStore>().ClearSessionScopedBindings();
if (clearedBindings > 0)
    Console.WriteLine($"[atoms] boot sweep cleared {clearedBindings} stale session-scoped binding(s)");
var orphanInstances = app.Services.GetRequiredService<RpgStore>().CountOrphanInstances();
if (orphanInstances > 0)
    Console.WriteLine($"[atoms] {orphanInstances} orphan instance(s) remain after the boot sweep");
// `equip-atom-source-not-wired` (action-plan.md §5) + GG-49 equip SourceId (2026-09-08):
// BattleStatComposer and UniqueActorHubCompose share EquippedBoundAtoms so battle ModsFor and
// sheet DerivedAtomsFor mint equip:{role}:{itemRef}, never bare atom ids / equip:unknown.
var equipStore = app.Services.GetRequiredService<RpgStore>();
FusionRpg.Core.Battle.BattleStatComposer.UseEquipment(
    EquippedBoundAtoms.SourceFromStore(equipStore));
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapStorageEndpoints();
app.MapUniqueActors();
app.MapRelics();
app.MapDemons();
app.MapZombossDeploy();
app.MapSouls();
app.MapExpeditions();
app.MapFusion();
app.MapPatron();
app.MapCommanders();
app.MapContracts();
app.MapDelve();
app.MapDelveWild();
app.MapDelveBattle();
app.MapWorld();
app.MapWorldWarden();
app.MapAptitudes();
app.MapPassiveTree();
app.MapGateCounters();
app.MapSpeciesBuild();
app.MapLoadout();
app.MapAuraDerived();
app.MapDerivedSurface();
app.MapAuraRuntime();
app.MapAuraCatalog();
app.MapOnboarding();
// item module 20 (`item-surfaces`) — READ-ONLY. No MapPost lives in that file: equipping, socketing
// and salvaging already have owners (modules 4, 16, 14), and a second write path through the
// presentation layer is the "second surface" this module exists to prevent.
app.MapItemSurfaces(itemSurfaceTuning, socketTuning, gemInserts, itemBaseTypes);
// ⭐ item modules 14/15/16 — the WRITE half, and the production caller all three named as their
// shared blocker. Mapped only when the recipe corpus loaded: a workbench with no prices could only
// ever refuse, and a route that always refuses is worse than an absent one because it looks wired.
if (itemWorkbench is { } workbench) app.MapWorkbench(workbench);
// ⭐ item module 4 (`equip-assign`) — the WRITE half, and the production caller `SaveAssignment` /
// `RemoveAssignment` never had. Unconditional, unlike the workbench above: equipping needs no recipe
// corpus and no price, so there is no state in which these routes could only refuse.
app.MapItemEquip(new FusionRpg.Server.ItemEquipService(store));
// ⭐ item modules 10 + 20 — the SEE and COMPARE surfaces. `ItemCardRenderer.Render`,
// `ItemCardCompare.Compare` and `DominancePresentation` all shipped tested with zero callers outside
// `tests/`, which is why every block of the web card rendered its honest "pending" state. These two
// read-only routes are their production caller. Unconditional, like the equip routes: an item card
// needs no recipe corpus and no price, so there is no state in which they could only refuse.
//
// ⏸ The two corpora are the same boot-time stopgap `BaseTypeSocketMaxCorpus` already is — module 6
// shipped the base-type corpus and module 16 the gem corpus as seed JSON, neither as a table.
// item-lore T6: N2's string catalog (ssot-presentation.md §5.3), copied next to the exe the same way
// data/tuning and data/seed/items already are. Absent degrades to "no sentence resolved" — the card's
// flavour line still carries its key and nothing is invented, which is the same
// absence-degrades-never-guesses rule DisplayCheck applies to the very same file.
var displayStrings = FusionRpg.Server.DisplayStringCatalogFile.Load(
    Path.Combine(AppContext.BaseDirectory, "content", "display", "en.json"));
// ⭐ item-content T1: the two corpora module 8's `ItemNameComposer` needed and nobody loaded, which is
// the whole reason a rolled item had no name. Same boot-time stopgap shape as the base-type and gem
// corpora above — the `nameWords` half is the `item_affix_name` PROJECTION `AffixNameTable`'s own doc
// describes and no importer ever built; the rare half is a head/tail word table that did not exist in
// the seed tree at all until today.
//
// ⛔ A malformed family is reported and naming degrades to the base type's authored name — it never
// takes the process down. Same posture as the recipe-corpus import above: a card that reads
// "Card-Proof Blade" instead of "Sap Tangle" is a worse card; a server that will not start is no game.
Func<string, FusionRpg.Core.Items.AffixNameSlot?>? affixNameWords = null;
try
{
    affixNameWords = FusionRpg.Server.AffixNameWordCorpus.Load(
        Path.Combine(AppContext.BaseDirectory, "data", "seed", "items", "affix-families"));
}
catch (Exception ex)
{
    Console.WriteLine($"[items] affix nameWords corpus failed to load — item naming stays off: {ex.Message}");
}

var rareNameDraw = FusionRpg.Server.RareNameCorpus.Load(
    Path.Combine(AppContext.BaseDirectory, "data", "seed", "items", "rare-names", "rare-names.json"));
if (affixNameWords is null || rareNameDraw is null)
    Console.WriteLine(
        "[items] item naming is OFF — every card falls back to its base type's authored name. " +
        "Both halves are required: the families' nameWords AND rare-names.json (a 3+ affix item has " +
        "no honest two-word name without the second).");
var itemCardCorpus = new ItemCardCorpus(
    itemBaseTypes,
    gemInserts,
    socketTuning, itemSurfaceTuning, enhancementTuning,
    LookupString: displayStrings,
    LookupNameWords: affixNameWords,
    RareNameDraw: rareNameDraw);
app.MapItemCard(new FusionRpg.Server.ItemCardService(store, itemCardCorpus));
// ⭐ item-content module `atom-preview` — the same renderer, over an UNSAVED container. It shares the
// corpus above rather than loading a second one: a preview that read a different base-type corpus than
// the live card would preview something the game does not ship.
//
// ⛔ Read-only despite being a POST. The body is a whole container definition, which is why it cannot
// be a GET; nothing it receives is persisted anywhere.
app.MapItemPreview(new FusionRpg.Server.ItemPreviewService(
    store, itemCardCorpus, FusionRpg.Core.Power.PowerTuningHub.Tuning));
PatronEndpoints.RefreshRuntimeState(app.Services.GetRequiredService<RpgStore>()); // SIM plugins read it

app.MapGet("/health", (RpgStore store, EventIngest ingest) => ingest.Decorate(store.ToHealth(SimFlags.Enabled)));

app.MapGet("/api/players", (RpgStore store) => new PlayersListDto
{
    Items = store.ListPlayers(),
    CurrentPlayerId = store.GetCurrentPlayerId()
});
app.MapPost("/api/players", (CreatePlayerRequest body, RpgStore store) =>
{
    if (string.IsNullOrWhiteSpace(body.Name))
        return Results.BadRequest(new { error = "name required" });
    return Results.Ok(store.CreatePlayer(body.Name));
});
app.MapGet("/api/players/current", (RpgStore store) =>
{
    var p = store.GetCurrentPlayer();
    return p is null ? Results.NotFound() : Results.Ok(p);
});
app.MapPut("/api/players/current", (SelectPlayerRequest body, RpgStore store) =>
    store.SetCurrentPlayer(body.Id) ? Results.Ok(store.GetCurrentPlayer()) : Results.NotFound());

// --- PvzStats: player-bound modifier SSOT + derived sheet ---
app.MapGet("/api/pvz-stats/current", (RpgStore store) =>
{
    var id = store.GetCurrentPlayerId();
    var sheet = store.GetPvzStatsSheet(id);
    return sheet is null ? Results.NotFound() : Results.Ok(sheet);
});
app.MapGet("/api/pvz-stats/{playerId:long}", (long playerId, RpgStore store) =>
{
    var sheet = store.GetPvzStatsSheet(playerId);
    return sheet is null ? Results.NotFound() : Results.Ok(sheet);
});
app.MapGet("/api/pvz-stats/{playerId:long}/channels/{channel}", (long playerId, string channel, RpgStore store) =>
{
    var detail = store.GetPvzStatsChannel(playerId, channel);
    return detail is null ? Results.NotFound() : Results.Ok(detail);
});
app.MapGet("/api/pvz-stats/{playerId:long}/modifiers", (long playerId, RpgStore store) =>
{
    var mods = store.GetPvzStatsModifiers(playerId);
    return mods is null ? Results.NotFound() : Results.Ok(mods);
});
app.MapPost("/api/pvz-stats/{playerId:long}/modifiers/upsert", async (long playerId, PvzStatModifierDto body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    try
    {
        var sheet = store.UpsertPvzStatModifier(playerId, body);
        await BroadcastPvzStats(store, hub, playerId, sheet.Revision);
        await SendInjectorCommand(hub, inbox, new CommandDto { Name = "pvz.stats.reload", Payload = new { playerId, revision = sheet.Revision } });
        return Results.Ok(sheet);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
});
app.MapPost("/api/pvz-stats/{playerId:long}/modifiers/withdraw", async (long playerId, JsonElement body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    try
    {
        var sourceKind = body.TryGetProperty("sourceKind", out var sk) ? sk.GetString() : null;
        var sourceId = body.TryGetProperty("sourceId", out var sid) ? sid.GetString() : null;
        var channel = body.TryGetProperty("channel", out var ch) ? ch.GetString() : null;
        var op = body.TryGetProperty("op", out var opEl) ? opEl.GetString() : null;
        var pluginId = body.TryGetProperty("pluginId", out var pl) ? pl.GetString() : null;
        var sheet = store.WithdrawPvzStatModifiers(playerId, sourceKind, sourceId, channel, op, pluginId);
        await BroadcastPvzStats(store, hub, playerId, sheet.Revision);
        await SendInjectorCommand(hub, inbox, new CommandDto { Name = "pvz.stats.reload", Payload = new { playerId, revision = sheet.Revision } });
        return Results.Ok(sheet);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
});
app.MapPost("/api/pvz-stats/{playerId:long}/modifiers/reset", async (long playerId, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    try
    {
        var sheet = store.ResetPvzStats(playerId);
        await BroadcastPvzStats(store, hub, playerId, sheet.Revision);
        await SendInjectorCommand(hub, inbox, new CommandDto { Name = "pvz.stats.reload", Payload = new { playerId, revision = sheet.Revision } });
        return Results.Ok(sheet);
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
});

// --- PvzActivity: append-only facts + rollup cache ---
app.MapGet("/api/pvz-activity/current", (RpgStore store) =>
{
    var id = store.GetCurrentPlayerId();
    var rollup = store.GetPvzActivityRollup(id);
    return rollup is null ? Results.NotFound() : Results.Ok(rollup);
});
app.MapGet("/api/pvz-activity/{playerId:long}", (long playerId, RpgStore store) =>
{
    var rollup = store.GetPvzActivityRollup(playerId);
    return rollup is null ? Results.NotFound() : Results.Ok(rollup);
});
app.MapGet("/api/pvz-activity/{playerId:long}/facts", (long playerId, RpgStore store, string? kind, long? runId, int limit = 100) =>
{
    var page = store.ListPvzActivityFacts(playerId, kind, runId, limit);
    return page is null ? Results.NotFound() : Results.Ok(page);
});
app.MapPost("/api/pvz-activity/{playerId:long}/facts/append", async (long playerId, PvzActivityAppendRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
{
    try
    {
        var result = store.AppendPvzActivityFact(playerId, body);
        await BroadcastPvzActivity(store, hub, playerId, result.Rollup.Revision);
        foreach (var d in result.Progression)
            await BroadcastRpgProgression(hub, d.PlayerId, d.Kind, d.TypeId, d.Revision);
        return Results.Ok(result.Rollup);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
});

// --- RpgProgression ---
app.MapGet("/api/rpg/progression/{playerId:long}/summary", (long playerId, RpgStore store) =>
{
    var s = store.GetRpgProgressionSummary(playerId);
    return s is null ? Results.NotFound() : Results.Ok(s);
});
app.MapGet("/api/rpg/progression/{playerId:long}/stats", (long playerId, RpgStore store) =>
{
    var s = store.GetRpgProgressionStats(playerId);
    return s is null ? Results.NotFound() : Results.Ok(s);
});
app.MapGet("/api/rpg/progression/{playerId:long}", (long playerId, RpgStore store, string? kind, string sort = "level", int limit = 200, int offset = 0) =>
{
    var list = store.ListRpgProgression(playerId, kind, sort, limit, offset);
    return list is null ? Results.NotFound() : Results.Ok(list);
});
app.MapGet("/api/rpg/progression/{playerId:long}/ledger", (long playerId, RpgStore store, string? kind, int? typeId, string? reason, int limit = 100, long? afterId = null) =>
{
    var page = store.ListRpgXpLedger(playerId, kind, typeId, reason, limit, afterId);
    return page is null ? Results.NotFound() : Results.Ok(page);
});
app.MapGet("/api/rpg/progression/{playerId:long}/{kind}/{typeId:int}", (long playerId, string kind, int typeId, RpgStore store) =>
{
    var row = store.GetRpgActor(playerId, kind, typeId);
    return row is null ? Results.NotFound() : Results.Ok(row);
});
app.MapPost("/api/rpg/progression/{playerId:long}/{kind}/{typeId:int}/clear-demotion", async (long playerId, string kind, int typeId, RpgStore store, IHubContext<RpgHub> hub) =>
{
    var row = store.ClearRpgDemotion(playerId, kind, typeId);
    if (row is null) return Results.NotFound();
    await BroadcastRpgProgression(hub, playerId, kind, typeId, row.Revision);
    return Results.Ok(row);
});

// --- PvzIntent: game write commands ---
app.MapPost("/api/pvz-intent/spawn-extra", async (PvzSpawnExtraRequest body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox, HttpContext http) =>
{
    var playerId = body.PlayerId ?? store.GetCurrentPlayerId();
    if (!store.PlayerExists(playerId)) return Results.NotFound();
    var correlationId = string.IsNullOrWhiteSpace(body.CorrelationId)
        ? Guid.NewGuid().ToString("N")
        : body.CorrelationId.Trim();
    var reason = string.IsNullOrWhiteSpace(body.Reason) ? "extra" : body.Reason.Trim();
    var side = string.IsNullOrWhiteSpace(body.Side) ? "zombie" : body.Side.Trim().ToLowerInvariant();
    try
    {
        var (rollup, inserted) = store.RecordExtraSpawnIntent(playerId, correlationId, body.TypeId, reason, side);
        if (inserted)
            await BroadcastPvzActivity(store, hub, playerId, rollup.Revision);

        if (inserted)
        {
            var payload = new
            {
                typeId = body.TypeId,
                col = body.Col,
                row = body.Row,
                reason,
                correlationId,
                side,
                playerId,
                source = "extra"
            };
            await SendInjectorCommand(hub, inbox, new CommandDto
            {
                Id = correlationId,
                Name = "pvz.spawn.extra",
                Payload = payload
            });

            // No live injector: drive sim so CI can prove source=extra spawn without Unity.
            if (!store.LiveInjector && SimFlags.Enabled)
            {
                var sim = http.RequestServices.GetService<SimService>();
                if (sim is not null)
                {
                    await sim.RunAsync(stats => sim.Engine.SpawnZombie(stats, new SimSpawnZombieRequest
                    {
                        Type = body.TypeId,
                        Source = "extra",
                        Ptr = "extra-" + correlationId
                    }));
                }
            }
        }

        return Results.Ok(new { ok = true, correlationId, inserted, rollup });
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
});

app.MapGet("/api/stats", (RpgStore store) => store.GetStats());
app.MapPut("/api/stats", async (StatsConfig body, RpgStore store, IHubContext<RpgHub> hub) =>
{
    store.PutStats(body);
    await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("StatsUpdated", body);
    return Results.Ok(body);
});

app.MapPost("/api/events", (JsonElement body, EventIngest ingest) =>
{
    var accepted = 0;
    var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("events", out var arr) && arr.ValueKind == JsonValueKind.Array)
    {
        foreach (var item in arr.EnumerateArray())
            accepted += AcceptOne(item, ingest, json);
    }
    else
    {
        accepted += AcceptOne(body, ingest, json);
    }
    return Results.Ok(new { accepted });
});

app.MapGet("/api/events", (RpgStore store, int limit = 100, long afterId = 0, long? playerId = null) =>
    new { items = store.ListEvents(limit, afterId, playerId) });

app.MapGet("/api/runs", (RpgStore store, long? playerId) => new { items = store.ListRuns(playerId) });
app.MapGet("/api/types", (RpgStore store, string? side) => new { items = store.ListTypes(side) });

app.MapGet("/api/icons/dump", (TypeIconStore icons, string? side) =>
    Results.Ok(new { items = icons.ListDumps(side) }));

app.MapGet("/api/icons/dump/{side}/{typeId:int}", (string side, int typeId, TypeIconStore icons) =>
{
    if (!TypeIconStore.IsValidSide(side) || typeId < 0) return Results.BadRequest();
    var dump = icons.GetDump(side, typeId);
    return dump is null ? Results.NotFound() : Results.Ok(dump);
});

app.MapGet("/api/icons/dump/{side}/{typeId:int}/layer/{layer}", (string side, int typeId, string layer, TypeIconStore icons) =>
{
    if (!TypeIconStore.IsValidSide(side) || typeId < 0) return Results.BadRequest();
    var png = icons.GetLayerPng(side, typeId, layer);
    return png is null ? Results.NotFound() : Results.File(png, "image/png");
});

app.MapPut("/api/icons/dump/{side}/{typeId:int}", async (string side, int typeId, HttpRequest req, TypeIconStore icons, IHubContext<RpgHub> hub) =>
{
    if (!TypeIconStore.IsValidSide(side) || typeId < 0) return Results.BadRequest(new { error = "bad side/typeId" });
    try
    {
        using var doc = await JsonDocument.ParseAsync(req.Body);
        var (created, layerCount, url, portraitSet) = await icons.SaveDumpAsync(side, typeId, doc.RootElement);
        await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("TypeIconUpdated", new
        {
            side = icons.NormalizeSide(side),
            typeId,
            dump = true,
            created,
            layerCount,
            portraitSet,
            url
        });
        return Results.Ok(new
        {
            created,
            layerCount,
            portraitSet,
            url,
            side = icons.NormalizeSide(side),
            typeId,
            composedUrl = portraitSet ? $"/api/icons/{icons.NormalizeSide(side)}/{typeId}.png" : null
        });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/icons/{side}/{typeId:int}.png", (string side, int typeId, TypeIconStore icons) =>
{
    if (!TypeIconStore.IsValidSide(side) || typeId < 0) return Results.BadRequest();
    var png = icons.GetComposedPng(side, typeId);
    return png is null ? Results.NotFound() : Results.File(png, "image/png");
});

app.MapGet("/api/icons/{side}/{typeId:int}", (string side, int typeId, TypeIconStore icons) =>
{
    if (!TypeIconStore.IsValidSide(side) || typeId < 0) return Results.BadRequest();
    var composed = icons.GetComposedPng(side, typeId);
    if (composed != null)
        return Results.Ok(new { side = icons.NormalizeSide(side), typeId, url = $"/api/icons/{icons.NormalizeSide(side)}/{typeId}.png", composed = true });
    var dump = icons.GetDump(side, typeId);
    if (dump is null) return Results.NotFound();
    return Results.Ok(new { side = icons.NormalizeSide(side), typeId, dump = true, layers = dump.Layers.Count, url = $"/api/icons/dump/{icons.NormalizeSide(side)}/{typeId}" });
});

app.MapGet("/api/recipes", (RpgStore store) => new { items = store.ListRecipes() });

app.MapGet("/api/almanac/dump", (RpgStore store, string? side) =>
    Results.Ok(new { items = store.ListAlmanacTextDumps(side) }));

app.MapGet("/api/almanac/dump/{side}/{typeId:int}", (string side, int typeId, RpgStore store) =>
{
    if (!TypeIconStore.IsValidSide(side) || typeId < 0) return Results.BadRequest();
    var dump = store.GetAlmanacTextDump(side, typeId);
    return dump is null ? Results.NotFound() : Results.Ok(dump);
});

app.MapPut("/api/almanac/dump/{side}/{typeId:int}", async (string side, int typeId, HttpRequest req, RpgStore store, IHubContext<RpgHub> hub) =>
{
    if (!TypeIconStore.IsValidSide(side) || typeId < 0) return Results.BadRequest(new { error = "bad side/typeId" });
    try
    {
        using var doc = await JsonDocument.ParseAsync(req.Body);
        if (!doc.RootElement.TryGetProperty("fields", out var fieldsEl) || fieldsEl.ValueKind != JsonValueKind.Object)
            return Results.BadRequest(new { error = "fields required" });

        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in fieldsEl.EnumerateObject())
            fields[p.Name] = p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.GetString();

        Dictionary<string, string>? sources = null;
        if (doc.RootElement.TryGetProperty("sources", out var srcEl) && srcEl.ValueKind == JsonValueKind.Object)
        {
            sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in srcEl.EnumerateObject())
            {
                var v = p.Value.GetString();
                if (!string.IsNullOrWhiteSpace(v)) sources[p.Name] = v!;
            }
        }

        if (fields.Count == 0) return Results.BadRequest(new { error = "empty fields" });
        var created = !store.HasAlmanacTextDump(side, typeId);
        store.UpsertAlmanacTextDump(side, typeId, fields, sources);
        var sNorm = side.Trim().ToLowerInvariant();
        await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("AlmanacTextUpdated", new
        {
            side = sNorm,
            typeId,
            created,
            fieldCount = fields.Count
        });
        return Results.Ok(new { created, fieldCount = fields.Count, side = sNorm, typeId, url = $"/api/almanac/dump/{sNorm}/{typeId}" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});
app.MapGet("/api/almanac/seed", (RpgStore store, string? side) =>
    Results.Ok(new { contractVersion = RpgStore.AlmanacSeedContractVersion, items = store.ListAlmanacSeed(side) }));

app.MapGet("/api/almanac/seed/{side}/{typeId:int}", (string side, int typeId, RpgStore store) =>
{
    if (!TypeIconStore.IsValidSide(side) || typeId < 0) return Results.BadRequest();
    var dto = store.GetAlmanacSeed(side, typeId);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
});

app.MapPost("/api/almanac/seed/rebuild", (RpgStore store) => Results.Ok(store.RebuildAlmanacSeed()));

app.MapPost("/api/almanac/seed/enrich", (RpgStore store) =>
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "data", "seed", "external-reference", "almanac-enrichment", "pvz-fusion-almanac-3.6.1.json")))
        dir = dir.Parent;
    if (dir is null)
        return Results.Problem("enrichment export file not found (data/seed/external-reference/almanac-enrichment/pvz-fusion-almanac-3.6.1.json)", statusCode: 404);

    var path = Path.Combine(dir.FullName, "data", "seed", "external-reference", "almanac-enrichment", "pvz-fusion-almanac-3.6.1.json");
    List<AlmanacEnrichmentImportRow>? rows;
    try
    {
        var json = File.ReadAllText(path);
        rows = JsonSerializer.Deserialize<List<AlmanacEnrichmentImportRow>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch (Exception ex)
    {
        return Results.Problem("failed to read/parse enrichment export: " + ex.Message, statusCode: 400);
    }
    if (rows is null) return Results.Problem("enrichment export deserialized to null", statusCode: 400);

    var summary = store.ImportAlmanacEnrichment(rows, "pvz-fusion-almanac-3.6.1");
    return Results.Ok(new { matched = summary.Matched, unmatched = summary.Unmatched });
});

app.MapGet("/api/runs/{id:long}/spawns", (long id, RpgStore store) => new { items = store.ListSpawnStats(id) });
app.MapGet("/api/metrics", (RpgStore store) => new { items = store.ListMetrics() });
app.MapPost("/api/heartbeat", (HeartbeatDto? body, RpgStore store) =>
{
    store.Heartbeat(body?.Source);
    return Results.Ok(new { ok = true, source = store.Source });
});
app.MapPost("/api/metrics", (JsonElement body, RpgStore store) =>
{
    if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
    {
        foreach (var item in arr.EnumerateArray())
        {
            var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(name)) continue;
            var value = item.TryGetProperty("value", out var v) && v.TryGetDouble(out var d) ? d : 0;
            store.UpsertMetric(name, value);
        }
    }
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/commands/reload-stats", async (RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    var stats = store.GetStats();
    await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("StatsUpdated", stats);
    await SendInjectorCommand(hub, inbox, new CommandDto { Name = "reload-stats" });
    return Results.Ok(new { ok = true });
});

// Injector polls this when SignalR server→client Command delivery fails.
app.MapGet("/api/cheats/commands/pending", (InjectorCommandInbox inbox) =>
    Results.Ok(new { items = inbox.Drain(64), remaining = inbox.Count }));

app.MapGet("/api/cheats", (RpgStore store) =>
{
    var json = store.GetCheatsJson();
    if (string.IsNullOrWhiteSpace(json))
        return Results.Ok(new { menuEnabled = true, entries = Array.Empty<object>(), catalog = new { } });
    return Results.Content(json, "application/json");
});

app.MapPut("/api/cheats", async (JsonElement body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    store.PutCheatsJson(body.GetRawText());
    await SendInjectorCommand(hub, inbox, new CommandDto
    {
        Name = "cheat.set",
        Payload = body
    });
    await BroadcastCheats(store, hub);
    return Results.Ok(new { ok = true });
});

// Injector telemetry only — catalog merge, never overwrite entries, never push CheatsUpdated to web.
// One-way cheats: web/server → injector → game (not game → FE).
app.MapPut("/api/cheats/mirror", (JsonElement body, RpgStore store) =>
{
    store.MergeCheatsCatalog(body);
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/cheats/action", async (JsonElement body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    var actionName = body.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";
    if (actionName is "reset-all")
    {
        store.PutCheatsJson("""{"menuEnabled":false,"revision":0,"entries":[]}""");
        await BroadcastCheats(store, hub);
    }
    else if (actionName is "reset-group")
    {
        var prefix = body.TryGetProperty("prefix", out var p) ? p.GetString() ?? "" : "";
        var raw = store.GetCheatsJsonRaw() ?? """{"menuEnabled":false,"revision":0,"entries":[]}""";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            var dict = new Dictionary<string, object?>();
            long revision = 0;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("entries")) continue;
                if (prop.NameEquals("revision") && prop.Value.TryGetInt64(out var r)) revision = r;
                else dict[prop.Name] = prop.Value.Clone();
            }
            var kept = new List<object>();
            if (doc.RootElement.TryGetProperty("entries", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(prefix) && id.StartsWith(prefix, StringComparison.Ordinal))
                        continue;
                    kept.Add(item.Clone());
                }
            }
            dict["entries"] = kept;
            dict["revision"] = revision + 1;
            dict["updatedAt"] = DateTime.UtcNow.ToString("o");
            dict["menuEnabled"] = false;
            // Already bumped once — do not bump again inside PutCheatsJson.
            store.PutCheatsJson(System.Text.Json.JsonSerializer.Serialize(dict), bumpRevision: false);
            await BroadcastCheats(store, hub);
        }
        catch { /* still send command */ }
    }
    await SendInjectorCommand(hub, inbox, new CommandDto
    {
        Name = "cheat.action",
        Payload = body
    });
    return Results.Ok(new { ok = true, queued = inbox.Count });
});

app.MapPost("/api/cheats/toggle", async (JsonElement body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    var id = body.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
    var enabled = body.TryGetProperty("enabled", out var en) && en.GetBoolean();
    store.MergeCheatField(id, enabled, null);
    await SendInjectorCommand(hub, inbox, new CommandDto
    {
        Name = "cheat.toggle",
        Payload = body
    });
    await BroadcastCheats(store, hub);
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/cheats/set-float", async (JsonElement body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    var id = body.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
    double? fv = body.TryGetProperty("value", out var v) && v.TryGetDouble(out var d) ? d : null;
    store.MergeCheatField(id, true, fv);
    await SendInjectorCommand(hub, inbox, new CommandDto
    {
        Name = "cheat.set-float",
        Payload = body
    });
    await BroadcastCheats(store, hub);
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/cheats/clear-field", async (JsonElement body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    var id = body.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
    store.ClearCheatField(id);
    await SendInjectorCommand(hub, inbox, new CommandDto
    {
        Name = "cheat.clear-field",
        Payload = body
    });
    await BroadcastCheats(store, hub);
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/cheats/schema", () =>
    Results.Ok(new
    {
        fields = CheatSchema.All.Select(f => new
        {
            id = f.Id,
            role = f.Role.ToString(),
            kind = f.Kind,
            channel = f.Channel,
            op = f.Op,
            displayDefault = f.DisplayDefault,
            toggleDefault = f.ToggleDefault,
            groupPrefix = f.GroupPrefix
        }).ToList()
    }));

app.MapGet("/api/cheats/packs", () =>
    Results.Ok(new { items = ProbePacks.All.Select(ProbePacks.ToApiDto).ToList() }));

app.MapPost("/api/cheats/probe", async (JsonElement body, EventIngest ingest, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    var packId = body.TryGetProperty("packId", out var pidEl) ? pidEl.GetString() ?? "" : "";
    var pack = ProbePacks.Get(packId);
    if (pack == null)
        return Results.NotFound(new { error = "unknown pack", packId });

    var probeId = body.TryGetProperty("probeId", out var pr) && pr.ValueKind == JsonValueKind.String
        ? pr.GetString()!
        : Guid.NewGuid().ToString("N");

    ingest.Enqueue(new EventEnvelope
    {
        T = DateTime.UtcNow.ToString("o"),
        Kind = "probe.start",
        Payload = new Dictionary<string, object>
        {
            ["probeId"] = probeId,
            ["packId"] = pack.Id,
            ["label"] = pack.Label,
            ["hint"] = pack.Hint,
            ["expectedKinds"] = pack.ExpectedKinds.ToList()
        }
    });

    await SendInjectorCommand(hub, inbox, new CommandDto
    {
        Name = "cheat.probe-begin",
        Payload = new { probeId, packId = pack.Id }
    });

    foreach (var step in pack.Steps)
    {
        var corr = Guid.NewGuid().ToString("N");
        if (step.Op is "toggle")
        {
            await SendInjectorCommand(hub, inbox, new CommandDto
            {
                Name = "cheat.toggle",
                Payload = new
                {
                    id = step.Id,
                    enabled = step.Enabled ?? true,
                    probeId,
                    packId = pack.Id,
                    correlationId = corr,
                    source = "pack"
                }
            });
        }
        else if (step.Op is "set-float")
        {
            await SendInjectorCommand(hub, inbox, new CommandDto
            {
                Name = "cheat.set-float",
                Payload = new
                {
                    id = step.Id,
                    value = step.Value ?? 0,
                    probeId,
                    packId = pack.Id,
                    correlationId = corr,
                    source = "pack"
                }
            });
        }
        else if (step.Op is "action")
        {
            var payload = new Dictionary<string, object?>
            {
                ["action"] = step.Action,
                ["probeId"] = probeId,
                ["packId"] = pack.Id,
                ["correlationId"] = corr,
                ["source"] = "pack"
            };
            if (step.Which != null) payload["which"] = step.Which;
            if (step.Value is { } sv) payload["value"] = sv;
            if (step.Add is { } sa) payload["add"] = sa;
            await SendInjectorCommand(hub, inbox, new CommandDto
            {
                Name = "cheat.action",
                Payload = payload
            });
        }
    }

    return Results.Ok(new
    {
        probeId,
        packId = pack.Id,
        label = pack.Label,
        hint = pack.Hint,
        expectedKinds = pack.ExpectedKinds,
        steps = pack.Steps.Count
    });
});

app.MapPost("/api/cheats/probe/end", async (JsonElement body, EventIngest ingest, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    var probeId = body.TryGetProperty("probeId", out var pr) ? pr.GetString() : null;
    var reason = body.TryGetProperty("reason", out var r) ? r.GetString() ?? "web" : "web";
    ingest.Enqueue(new EventEnvelope
    {
        T = DateTime.UtcNow.ToString("o"),
        Kind = "probe.end",
        Payload = new Dictionary<string, object?>
        {
            ["probeId"] = probeId ?? "",
            ["reason"] = reason
        }
    });
    await SendInjectorCommand(hub, inbox, new CommandDto
    {
        Name = "cheat.probe-end",
        Payload = new { probeId, reason }
    });
    return Results.Ok(new { ok = true, probeId, reason });
});

if (SimFlags.Enabled)
    app.MapSimAndProbes();

app.MapSimEffect();
// Debug endpoints are spawn/kill/mods control — loopback-only unless explicitly forced
// (2026-08-21 review I3: a 0.0.0.0 rebind must not expose them to the LAN unauthenticated).
var debugAllowed = listenUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                   || listenUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(Environment.GetEnvironmentVariable("FUSIONRPG_DEBUG_REMOTE"), "1", StringComparison.Ordinal);
if (debugAllowed)
    app.MapDebug();
else
    Console.WriteLine("[debug] endpoints disabled on non-loopback bind (set FUSIONRPG_DEBUG_REMOTE=1 to force)");
app.MapPerf();

app.MapHub<RpgHub>("/hub/rpg");
app.MapFallbackToFile("index.html");

try
{
    if (Environment.GetEnvironmentVariable("FUSIONRPG_NO_BROWSER") != "1")
    {
        var openUrl = listenUrl.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "http://127.0.0.1:5088";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(openUrl) { UseShellExecute = true });
    }
}
catch { /* no default browser */ }

app.Run();

static async Task SendInjectorCommand(IHubContext<RpgHub> hub, InjectorCommandInbox inbox, CommandDto cmd)
{
    inbox.Enqueue(cmd);
    try
    {
        await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("Command", cmd);
    }
    catch
    {
        /* inbox poll is the reliable path */
    }
}

static async Task BroadcastCheats(RpgStore store, IHubContext<RpgHub> hub)
{
    var json = store.GetCheatsJson();
    if (string.IsNullOrWhiteSpace(json)) return;
    try
    {
        using var doc = JsonDocument.Parse(json);
        await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("CheatsUpdated", doc.RootElement.Clone());
    }
    catch { /* ignore bad json */ }
}

static async Task BroadcastPvzStats(RpgStore store, IHubContext<RpgHub> hub, long playerId, long revision)
{
    var sheet = store.GetPvzStatsSheet(playerId);
    if (sheet is null) return;
    await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("PvzStatsUpdated", sheet);
    await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("PvzStatsUpdated", new { playerId, revision });
}

static async Task BroadcastPvzActivity(RpgStore store, IHubContext<RpgHub> hub, long playerId, long revision)
{
    var rollup = store.GetPvzActivityRollup(playerId);
    if (rollup is null) return;
    await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("PvzActivityUpdated", rollup);
    await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("PvzActivityUpdated", new { playerId, revision });
}

static async Task BroadcastRpgProgression(IHubContext<RpgHub> hub, long playerId, string kind, int typeId, long revision)
{
    await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("RpgProgressionUpdated", new { playerId, kind, typeId, revision });
}

static int AcceptOne(JsonElement item, EventIngest ingest, JsonSerializerOptions json)
{
    var env = item.Deserialize<EventEnvelope>(json);
    if (env is null || string.IsNullOrWhiteSpace(env.Kind)) return 0;
    return ingest.Enqueue(env);
}

public partial class Program { }
