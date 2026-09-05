// battle-tempo MEAS -- the staged sweep (B34's own shape, spec-action-timing.md, spec-tempo-content.md
// §9), executed standalone because Core.Tests (which HybridAtbSweepTests.cs lives in) is blocked by
// the same pre-existing, unrelated loam-economy/progression-shape WIP PoiseProbe's header documents.
// Loads REAL production tuning (the same files Program.cs loads at Server startup), resolves REAL
// BattleEngine battles, over the SAME 240-seed band and the SAME CloseSetup shape
// BattleGoldenTests/HybridAtbSweepTests use (replicated here since it is a Core.Tests-internal helper
// this Core-only probe cannot reference).

using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Combat.Shield;
using FusionRpg.Core.Status;
using FusionRpg.Core.Stats.Derived;

var dir = new DirectoryInfo(AppContext.BaseDirectory);
while (dir != null && !File.Exists(Path.Combine(dir.FullName, "data", "tuning", "derived-stats.v2.json")))
    dir = dir.Parent;
if (dir == null) throw new InvalidOperationException("could not locate data/tuning by walking up from " + AppContext.BaseDirectory);
string Load(string rel) => File.ReadAllText(Path.Combine(dir.FullName, "data", "tuning", rel));

DerivedStatPolicy.Configure(DerivedStatTuningLoader.Parse(Load("derived-stats.v2.json")));
FusionRpg.Core.Power.PowerTuningHub.Configure(FusionRpg.Core.Power.PowerTuningLoader.Parse(Load("power-scale.v2.json")));
FusionRpg.Core.Stats.Derived.StatsTuningHub.Configure(FusionRpg.Core.Stats.Derived.StatsTuningLoader.Parse(Load("stats.v1.json")));
ShieldPolicy.Configure(ShieldTuningLoader.Parse(Load("shield.v1.json")));
CombatPolicy.Configure(CombatTuningLoader.Parse(Load("combat.v1.json")));
StatusPolicy.Configure(StatusTuningLoader.Parse(Load("status.v1.json")));
BattleTuningHub.Configure(BattleTuningLoader.Parse(Load("battle.v4.json")));
// battle-tempo battle-resources (2026-09-05): the pool-share projection BattleStatComposer's own
// resource seeding reads. Its own file rather than a battle.v{n}.json section because publish.py's
// `set` path refuses to invent keys (spec-battle-resources.md S2.2a) -- and its own Configure, which
// every host must remember, exactly like ActionTimingPolicy already needs.
BattleRuleset.ConfigureResources(BattleResourceTuningLoader.Parse(Load("battle-resources.v1.json")));
FusionRpg.Core.Actions.ActionTimingPolicy.Configure(FusionRpg.Core.Actions.ActionTimingTuningLoader.Parse(Load("action-timing.v1.json")));
// `LAND1` staged sweep (2026-09-05): shipped `hybrid-atb` now runs through `RunTimelineActionPhase`,
// which reads this whenever `WReact > 0` (hybrid-atb's own shipped value) -- needed here for the
// first time because this is the first probe to exercise the SHIPPED catalog row with the flag on.
ReactionLanePolicy.Configure(ReactionLaneTuningLoader.Parse(Load("reaction-lane.v1.json")));

// Mirrors BattleGoldenTests.Actor()/CloseSetup() exactly (tests/FusionRpg.Core.Tests/Battle/BattleGoldenTests.cs).
BattleActorSetup Actor(string key, string side, int level, ElementTypeId? elem = null, long attackIntervalMs = 0, params string[] traits) => new()
{
    Key = key, Side = side, SpeciesId = "golden-species", TypeId = 10_001, Level = level,
    ElementPrimary = elem, TraitIds = traits,
    MaxHp = BattleRuleset.BaseHp(level), Atk = BattleRuleset.BaseAtk(level), Defense = BattleRuleset.BaseDefense(level),
    AttackIntervalMs = attackIntervalMs,
};

BattleSetup CloseSetup(long squadIntervalMs = 0, long waveIntervalMs = 0) => new()
{
    WaveId = "golden-close",
    Squad = new[]
    {
        Actor("squad:0", "squad", 5, ElementTypeId.Air, squadIntervalMs, "regenerator"),
        Actor("squad:1", "squad", 5, ElementTypeId.Earth, squadIntervalMs, "guardian", "loyal"),
    },
    Wave = new[]
    {
        Actor("wave:0", "wave", 5, ElementTypeId.Dark, waveIntervalMs, "bloodthirsty"),
        Actor("wave:1", "wave", 5, ElementTypeId.Fire, waveIntervalMs, "soul-eater"),
    },
};

const int Seeds = 240;

double WinRate(BattleModeProfile profile, BattleSetup setup)
{
    var wins = 0;
    for (var i = 0; i < Seeds; i++)
    {
        var report = BattleEngine.Resolve(setup, (ulong)(9_000 + i), profile: profile);
        if (report.Outcome == BattleOutcome.Victory) wins++;
    }
    return (double)wins / Seeds;
}

Console.WriteLine("=== MEAS: staged sweep, replicating B34's own shape with action-timing/tempo-content now live ===");
Console.WriteLine();

// ---- 1. Wind-up alone (action-timing's basic-attack token wind-up; goldens carry no seeded action
//         catalog, so BasicAttack's own felt beat is the only source of wind-up here -- see AT2's own
//         evidence note on this exact caveat) ----
var baselineSetup = CloseSetup();
var stage0 = BattleModeProfileCatalog.ClassicRound;
var stage1 = stage0 with { AdvancePolicy = AdvancePolicyKind.FixedIncrement };
var stage2 = stage1 with { W = 4 };
var stage3 = stage2 with { DefaultCommitment = Commitment.EarlyBoundWithFallback };
var stage4 = stage3 with { NewEconomy = static () => new ActionPointsEconomy(2) };
var stage5 = stage4 with { OrdersBySpeed = true };
var shipped = BattleModeProfileCatalog.HybridAtb;

var r0 = WinRate(stage0, baselineSetup);
var r1 = WinRate(stage1, baselineSetup);
var r2 = WinRate(stage2, baselineSetup);
var r3 = WinRate(stage3, baselineSetup);
var r4 = WinRate(stage4, baselineSetup);
var r5 = WinRate(stage5, baselineSetup);
var rShipped = WinRate(shipped, baselineSetup);

Console.WriteLine("-- Staged attribution (B34's own table, action-timing NOW LIVE) --");
Console.WriteLine($"  stage0 classic-round                  winRate={r0:P2}");
Console.WriteLine($"  stage1 +AdvancePolicy                 winRate={r1:P2}  delta={r1 - r0:+0.00%;-0.00%}");
Console.WriteLine($"  stage2 +W=4                            winRate={r2:P2}  delta={r2 - r1:+0.00%;-0.00%}");
Console.WriteLine($"  stage3 +Commitment                     winRate={r3:P2}  delta={r3 - r2:+0.00%;-0.00%}");
Console.WriteLine($"  stage4 +ActionPointsEconomy             winRate={r4:P2}  delta={r4 - r3:+0.00%;-0.00%}");
Console.WriteLine($"  stage5 +OrdersBySpeed                   winRate={r5:P2}  delta={r5 - r4:+0.00%;-0.00%}");
Console.WriteLine($"  shipped hybrid-atb (all axes together)  winRate={rShipped:P2}");
Console.WriteLine($"  stage5 == shipped: {r5 == rShipped}");
Console.WriteLine();

// ---- 2. Tempo alone (species AttackIntervalMs difference between squad and wave, classic-round,
//         so only the speed-ordering effect shows) ----
var tempoSetup = CloseSetup(squadIntervalMs: 500, waveIntervalMs: 3000); // squad flurry, wave ponderous
var rTempoClassic = WinRate(stage0, tempoSetup);
var rBaselineClassic = WinRate(stage0, baselineSetup);
Console.WriteLine("-- Tempo alone (classic-round, squad=flurry(500ms) vs wave=ponderous(3000ms)) --");
Console.WriteLine($"  baseline (no tempo authored) winRate={rBaselineClassic:P2}");
Console.WriteLine($"  tempo-differentiated winRate={rTempoClassic:P2}  delta={rTempoClassic - rBaselineClassic:+0.00%;-0.00%}");
Console.WriteLine();

// ---- 3. Both together (shipped hybrid-atb + tempo) ----
var rBoth = WinRate(shipped, tempoSetup);
Console.WriteLine("-- Both together (shipped hybrid-atb + tempo-differentiated setup) --");
Console.WriteLine($"  winRate={rBoth:P2}  delta-vs-shipped-notempo={rBoth - rShipped:+0.00%;-0.00%}");
Console.WriteLine();

// ---- 4. Headline: does W / Commitment stop measuring 0.00%? ----
Console.WriteLine("-- Headline: do W and Commitment stop measuring 0.00%? --");
Console.WriteLine($"  W        (stage1->stage2) delta = {r2 - r1:+0.0000%;-0.0000%}  {(r2 != r1 ? "NON-ZERO" : "STILL ZERO")}");
Console.WriteLine($"  Commitment (stage2->stage3) delta = {r3 - r2:+0.0000%;-0.0000%}  {(r3 != r2 ? "NON-ZERO" : "STILL ZERO")}");
Console.WriteLine($"  AdvancePolicy (stage0->stage1) delta = {r1 - r0:+0.0000%;-0.0000%}  {(r1 != r0 ? "NON-ZERO" : "STILL ZERO")}");
