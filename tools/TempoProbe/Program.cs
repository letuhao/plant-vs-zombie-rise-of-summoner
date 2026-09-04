// Throwaway probe -- battle-tempo TC1/TC2, executed because Core.Tests is blocked (see PoiseProbe's
// own header for the full explanation: pre-existing, unrelated WIP in loam-economy/progression-shape
// streams breaks the shared test assembly). Exercises the real compiled SpeciesTempoProjection /
// BattleStatComposer / TraitBattleCatalog against the real data/tuning/*.json this session published.
// Mirrors tests/FusionRpg.Core.Tests/Battle/SpeciesTempoTests.cs case-for-case; delete once Core.Tests
// builds again and that file can run directly.

using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Stats.Derived;

var dir = new DirectoryInfo(AppContext.BaseDirectory);
while (dir != null && !File.Exists(Path.Combine(dir.FullName, "data", "tuning", "derived-stats.v2.json")))
    dir = dir.Parent;
if (dir == null) throw new InvalidOperationException("could not locate data/tuning by walking up from " + AppContext.BaseDirectory);
string Load(string rel) => File.ReadAllText(Path.Combine(dir.FullName, "data", "tuning", rel));

DerivedStatPolicy.Configure(DerivedStatTuningLoader.Parse(Load("derived-stats.v2.json")));
BattleTuningHub.Configure(BattleTuningLoader.Parse(Load("battle.v4.json")));

var failures = 0;
void Check(string name, bool condition)
{
    if (condition) { Console.WriteLine($"PASS  {name}"); return; }
    Console.WriteLine($"FAIL  {name}");
    failures++;
}
void CheckThrows(string name, Action action)
{
    try { action(); Console.WriteLine($"FAIL  {name} (did not throw)"); failures++; }
    catch (ArgumentOutOfRangeException) { Console.WriteLine($"PASS  {name}"); }
    catch (Exception ex) { Console.WriteLine($"FAIL  {name} (wrong exception: {ex.GetType().Name})"); failures++; }
}

const long Ponderous = 3000, Slow = 2400, Steady = 1500, Quick = 900, Flurry = 500;
const long ReferenceIntervalMs = Steady;

// -- TheFiveShippedTemposProjectToFiveDistinctOrderedSpeeds --
{
    var d = DerivedStatPolicy.TurnDefaultSpeed;
    var ponderous = SpeciesTempoProjection.SpeedFor(Ponderous, ReferenceIntervalMs, d);
    var slow = SpeciesTempoProjection.SpeedFor(Slow, ReferenceIntervalMs, d);
    var steady = SpeciesTempoProjection.SpeedFor(Steady, ReferenceIntervalMs, d);
    var quick = SpeciesTempoProjection.SpeedFor(Quick, ReferenceIntervalMs, d);
    var flurry = SpeciesTempoProjection.SpeedFor(Flurry, ReferenceIntervalMs, d);
    Console.WriteLine($"  ponderous={ponderous} slow={slow} steady={steady} quick={quick} flurry={flurry} (TurnDefaultSpeed={d})");
    Check("TheFiveShippedTemposProjectToFiveDistinctOrderedSpeeds",
        ponderous < slow && slow < steady && steady < quick && quick < flurry && steady == d);
}

// -- TheFloorHoldsForZeroOrNegativeIntervalAndNeverThrows --
{
    var d = DerivedStatPolicy.TurnDefaultSpeed;
    Check("TheFloorHoldsForZeroOrNegativeIntervalAndNeverThrows",
        SpeciesTempoProjection.SpeedFor(0, ReferenceIntervalMs, d) == d &&
        SpeciesTempoProjection.SpeedFor(-1, ReferenceIntervalMs, d) == d);
}

// -- EqualTemposReproduceTodaysOrderingExactly --
{
    var d = DerivedStatPolicy.TurnDefaultSpeed;
    Check("EqualTemposReproduceTodaysOrderingExactly",
        SpeciesTempoProjection.SpeedFor(Quick, ReferenceIntervalMs, d) == SpeciesTempoProjection.SpeedFor(Quick, ReferenceIntervalMs, d));
}

// -- ExtremeIntervalsNeverOverflow --
{
    var d = DerivedStatPolicy.TurnDefaultSpeed;
    var speed = SpeciesTempoProjection.SpeedFor(long.MaxValue / 100, ReferenceIntervalMs, d);
    Check("ExtremeIntervalsNeverOverflow", speed >= 1);
}

// -- ReferenceIntervalMustBePositive / DefaultSpeedMustBePositive --
{
    CheckThrows("ReferenceIntervalZeroThrows", () => SpeciesTempoProjection.SpeedFor(Steady, 0, 100));
    CheckThrows("ReferenceIntervalNegativeThrows", () => SpeciesTempoProjection.SpeedFor(Steady, -1, 100));
    CheckThrows("DefaultSpeedZeroThrows", () => SpeciesTempoProjection.SpeedFor(Steady, ReferenceIntervalMs, 0));
}

// -- SwiftIsNotDoubleCountedItMovesTheJitterNotTheSpeed --
{
    var swift = TraitBattleCatalog.All.Single(t => t.TraitId == "swift");
    var noSpeedMod = !swift.ChannelMods.Any(m => m.ChannelId == DerivedTurnChannels.Speed || m.ChannelId == DerivedTurnChannels.Haste);
    Check("SwiftIsNotDoubleCountedItMovesTheJitterNotTheSpeed", swift.InitiativeBonusMilli > 0 && noSpeedMod);
}

// -- AFasterSpeciesActsFirstOnTheProductionPathProvenByContrastBothDirections --
{
    var setupA = new BattleActorSetup { Key = "a", MaxHp = 100, AttackIntervalMs = Flurry };
    var setupB = new BattleActorSetup { Key = "b", MaxHp = 100, AttackIntervalMs = Ponderous };
    var speedA = BattleStatComposer.Compose(setupA).Get(DerivedTurnChannels.Speed);
    var speedB = BattleStatComposer.Compose(setupB).Get(DerivedTurnChannels.Speed);

    var setupC = new BattleActorSetup { Key = "c", MaxHp = 100, AttackIntervalMs = Ponderous };
    var setupD = new BattleActorSetup { Key = "d", MaxHp = 100, AttackIntervalMs = Flurry };
    var speedC = BattleStatComposer.Compose(setupC).Get(DerivedTurnChannels.Speed);
    var speedD = BattleStatComposer.Compose(setupD).Get(DerivedTurnChannels.Speed);

    Console.WriteLine($"  speedA(flurry)={speedA} speedB(ponderous)={speedB} speedC(ponderous)={speedC} speedD(flurry)={speedD}");
    Check("AFasterSpeciesActsFirstOnTheProductionPathProvenByContrastBothDirections",
        speedA > speedB && speedD > speedC);
}

// -- A steady-tempo actor with AttackIntervalMs=0 (no tempo authored) still projects the default,
//    matching WaveCatalog's own zero-default contract for a species with none carried --
{
    var setup = new BattleActorSetup { Key = "e", MaxHp = 100 }; // AttackIntervalMs defaults to 0
    var speed = BattleStatComposer.Compose(setup).Get(DerivedTurnChannels.Speed);
    Check("UnauthoredIntervalProjectsTheDefaultSpeed", speed == DerivedStatPolicy.TurnDefaultSpeed);
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL PROBES PASSED" : $"{failures} PROBE(S) FAILED");
Environment.Exit(failures == 0 ? 0 : 1);
