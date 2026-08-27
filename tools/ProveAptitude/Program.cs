using System.Globalization;
using System.Text.Json;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;

// class-system-todo.md P2.6, V3: drives a resolve on BOTH engines for the same allocation/Theta and
// emits { theta, perChannel: {overlay, battle}, deltas } -- fails (exit 1) on any non-zero delta.
// Follows prove-overlay-combat.ps1's -OutJson/exit-1-on-failure shape; unlike that script, both
// engines being compared are pure FusionRpg.Core types, so this is a console tool, not a live-game
// REST probe (V3's own note on why prove-aptitude.ps1 could not be written before this existed).

string repoRoot = FindRepoRoot();
string tuningDir = Path.Combine(repoRoot, "data", "tuning");

var aptitudeTuning = AptitudeTuningLoader.Parse(File.ReadAllText(Path.Combine(tuningDir, "aptitudes.v2.json")));
var powerTuning = PowerTuningLoader.Parse(File.ReadAllText(Path.Combine(tuningDir, "power-scale.v2.json")));
DerivedStatPolicy.Configure(DerivedStatTuningLoader.Parse(File.ReadAllText(Path.Combine(tuningDir, "derived-stats.v1.json"))));

var ladder = new PowerLadder(powerTuning);
var registry = DerivedStatRegistry.CreateDefault();

int theta = ArgInt(args, "--theta", 1000);
string source = ArgStringOrDefault(args, "--source", "Might");
long points = ArgLong(args, "--points", 100);
string? outPath = ArgString(args, "--out", null);
// Default: unfiltered -- every channel the allocation touches, compared. class-system-todo.md P2.6 /
// Checkpoint 2 is scoped to "Might -> combat.power.omni" (the one vertical slice P2.4/P2.5 actually
// built and proved), so its own invocation passes --channels explicitly. Left unfiltered by default
// because the wider comparison is useful and HONEST: running it that way surfaces a real, pre-existing
// gap -- BattleStatComposer's ChannelMods loop is unconditionally additive and applies no cap at all
// (confirmed: zero `Cap(` calls in that file), so a SumIncreased-kind channel with a cap (e.g.
// status.resist.*, capped at DerivedStatPolicy.CategoryResistCap on the overlay side) will never agree
// between engines once BOTH sides carry a large enough contribution to hit that cap. Not introduced by
// this tool or by P2.4/P2.5 -- true for every ChannelMods producer that has ever existed (Star, Loyalty,
// traits) -- and not fixable here: spec-aptitude-resolve.md §8 forbids changing BattleStatComposer's
// compose logic. It is P3.1's inheritance ("all twelve, all live channels... zero deltas"), not P2.6's.
var channelFilter = ArgString(args, "--channels", null)?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToHashSet(StringComparer.Ordinal);

var allocation = AptitudeAllocation.Single(AllocationScope.Commander, source, points);

// Overlay path: AptitudeSubsystem's own seam, minus the ActorHub/StatContext ceremony this tool
// doesn't need -- Resolve -> DerivedComposer.Compose, exactly what AptitudeSubsystem.ContributeDerived
// does per-call.
var overlayMods = AptitudeResolver.Resolve(allocation, aptitudeTuning, ladder, theta, registry);
var overlaySnapshot = new DerivedComposer(registry).Compose(overlayMods);

// Battle path: ResolveForBattle -> BattleActorSetup.ChannelMods -> BattleStatComposer.Compose,
// exactly what WebMatchService.AptitudeChannelMods feeds into a real squad setup. ElementPrimary/
// Secondary and TraitIds stay at their record defaults (null / empty) so BattleStatComposer.Tuning
// (PrimaryAffinityDivisor/SecondaryAffinityDivisor) is never touched -- this tool proves the
// aptitude seam agrees, not the whole battle-setup pipeline, and needs no BattleTuningHub.Configure.
var battleMods = AptitudeResolver.ResolveForBattle(allocation, aptitudeTuning, ladder, theta, registry);
var setup = new BattleActorSetup { Key = "prove-aptitude", Side = "squad", Level = theta, ChannelMods = battleMods };
var battleSnapshot = BattleStatComposer.Compose(setup);

var channels = overlayMods.Select(m => m.ChannelId)
    .Concat(battleMods.Select(m => m.ChannelId))
    .Distinct(StringComparer.Ordinal)
    .Where(c => channelFilter is null || channelFilter.Contains(c))
    .OrderBy(c => c, StringComparer.Ordinal)
    .ToList();

var perChannel = new Dictionary<string, PerChannel>(StringComparer.Ordinal);
var deltas = new Dictionary<string, double>(StringComparer.Ordinal);
var anyNonZero = false;
const double Epsilon = 1e-9;

foreach (var ch in channels)
{
    var overlayVal = overlaySnapshot.Get(ch, 0.0);
    var battleVal = battleSnapshot.Get(ch, 0.0);
    var delta = overlayVal - battleVal;
    perChannel[ch] = new PerChannel(overlayVal, battleVal);
    deltas[ch] = delta;
    if (Math.Abs(delta) > Epsilon) anyNonZero = true;
}

if (channels.Count == 0)
{
    Console.Error.WriteLine(channelFilter is null
        ? $"error: source '{source}' at {points} points funds no edge in the shipped tuning -- nothing to compare"
        : $"error: --channels filter matched none of the channels source '{source}' funds -- nothing to compare");
    return 1;
}

var result = new ProveAptitudeResult(theta, source, points, perChannel, deltas, !anyNonZero);
var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });

outPath ??= Path.Combine(repoRoot, "docs", "research", "class-system", "_prove-aptitude.json");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, json);
Console.WriteLine(json);
Console.WriteLine();
Console.WriteLine(anyNonZero
    ? $"FAIL — {deltas.Count(kv => Math.Abs(kv.Value) > Epsilon)} channel(s) disagree between overlay and battle"
    : $"OK — {channels.Count} channel(s), all deltas zero. Wrote {outPath}");

return anyNonZero ? 1 : 0;

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "scripts", "guard-class-system.ps1")))
            return dir.FullName;
        dir = dir.Parent;
    }
    throw new InvalidOperationException("could not locate repo root above " + AppContext.BaseDirectory);
}

static string? ArgString(string[] a, string flag, string? fallback)
{
    var i = Array.IndexOf(a, flag);
    return i >= 0 && i + 1 < a.Length ? a[i + 1] : fallback;
}

static string ArgStringOrDefault(string[] a, string flag, string fallback) =>
    ArgString(a, flag, fallback) ?? fallback;

static int ArgInt(string[] a, string flag, int fallback)
{
    var s = ArgString(a, flag, null);
    return s is null ? fallback : int.Parse(s, CultureInfo.InvariantCulture);
}

static long ArgLong(string[] a, string flag, long fallback)
{
    var s = ArgString(a, flag, null);
    return s is null ? fallback : long.Parse(s, CultureInfo.InvariantCulture);
}

readonly record struct PerChannel(double Overlay, double Battle);
sealed record ProveAptitudeResult(
    int Theta, string Source, long Points,
    Dictionary<string, PerChannel> PerChannel, Dictionary<string, double> Deltas, bool Pass);
