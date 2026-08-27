using System.Text.Json;
using System.Text.RegularExpressions;
using FusionRpg.Core.Balance.Guards;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Combat.Shield;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Status;

// class-system-todo.md Checkpoint 8's own remaining gap: _baseline-dominance.json's dominanceMatrix and
// dominantCorners fields come from tools/CombatSim's `trinity --json`, which reads its own internal,
// still-v1-only tuning copy (P8.5) -- blocked from updating by the same concurrent-edit hazard as
// P3.4/P8.1, and even an override would silently ignore the new AptitudeMitigation dial (P8.3).
//
// trinity's own 12-corner dominance matrix is ALREADY element-blind (BestResponse.DominanceMatrix calls
// Analytic.Predict directly, confirmed by reading it this session for P8.1) -- a pure closed-form
// computation, no simulator/RNG involved. FusionRpg.Core.DominanceGuard.Measure is the SAME kind of
// closed-form computation (via Predictor.Predict, the production resolver TerminationGuard.Assert
// already uses), and reads the LIVE data/tuning/aptitudes.v*.json automatically -- so it can reproduce
// this specific pair of fields accurately against v2, without touching tools/CombatSim at all.
//
// Deliberately does NOT reproduce trinity's own `chains` field (a best-response CHASE from named
// archetype starting points -- a different, more complex search DominanceGuard has no equivalent for,
// confirmed by reading DominanceGuardTests.cs's own comment: "DominanceGuard has no best-response
// chase -- that machinery stays in tools/CombatSim"). scripts/regen-class-system-baselines.ps1 overlays
// this tool's dominanceMatrix/dominantCorners onto trinity's own output, leaving chains/model/theta
// alone -- same overlay pattern already used for coverage.tuningSync (P8.5).
//
// spec-residual-fit.md §5: "this module ships no src/ code... measures and publishes numbers." This
// tool lives in tools/, calling FusionRpg.Core's shipped DominanceGuard/TerminationGuard as black boxes.

var theta = long.Parse(ArgOrDefault(args, "--theta", "100"));
var outPath = ArgOrDefault(args, "--out", "");

// Loads the LIVE shipped config (highest data/tuning/aptitudes.v*.json) -- never a hand-picked version
// literal, matching AptitudeTuningHub.cs's own doc-comment warning about exactly that staleness risk,
// and tools/ResidualFitLoop's own established Configure-at-startup pattern.
var repoRoot = FindRepoRoot();
var tuningDir = Path.Combine(repoRoot, "data", "tuning");
string Read(string domain) => File.ReadAllText(Path.Combine(tuningDir, LatestTuningFileName(tuningDir, domain)));

// TerminationGuard.ToActor -> ActorHubBootstrap.CreateDefault touches every one of these hubs (the
// SAME full set tools/ResidualFitLoop already configures for the identical reason) -- never a
// hand-picked version literal, matching AptitudeTuningHub.cs's own doc-comment warning about exactly
// that staleness risk; --input overrides ONLY the aptitudes domain (the one this task is measuring).
var tuningFileName = ArgOrDefault(args, "--input", LatestTuningFileName(tuningDir, "aptitudes"));
var tuningPath = Path.IsPathRooted(tuningFileName) ? tuningFileName : Path.Combine(tuningDir, tuningFileName);
AptitudeTuningHub.Configure(AptitudeTuningLoader.Parse(File.ReadAllText(tuningPath)));
CombatPolicy.Configure(CombatTuningLoader.Parse(Read("combat")));
ShieldPolicy.Configure(ShieldTuningLoader.Parse(Read("shield")));
DerivedStatPolicy.Configure(DerivedStatTuningLoader.Parse(Read("derived-stats")));
PowerTuningHub.Configure(PowerTuningLoader.Parse(Read("power-scale")));
StatusPolicy.Configure(StatusTuningLoader.Parse(Read("status")));
StatsTuningHub.Configure(StatsTuningLoader.Parse(Read("stats")));

// The real roster's own 12 (data/seed/aptitudes/roster.json) and the exact spike/floor corner shape
// BestResponse.DominanceMatrix uses -- verbatim from DominanceGuardTests.cs's own
// Measure_theRealTwelveCornerShape_matchesTheCheckedInBaselinesEmptyDominantCorners (already green
// against the live shipped config) and tools/ResidualFitLoop's own identical roster -- kept identical
// across all three rather than re-derived, so a future drift in one is a diff against the other two.
string[] roster =
{
    "Might", "Fortitude", "Vigor", "Onslaught", "Agility", "Composure",
    "Pierce", "Focus", "Bulwark", "Retribution", "Precision", "Ferocity",
};
// Not a balance dial -- BestResponse.DominanceMatrix's own fixed corner-shape constant
// (100/roster.Length/2, per-mille), reproduced verbatim so this tool's corners match trinity's.
const long floor = 4167;
long Spike() => 100_000 - floor * (roster.Length - 1);
AptitudeAllocation Corner(string spikeId) =>
    roster.Aggregate(AptitudeAllocation.Empty, (acc, id) =>
        acc + AptitudeAllocation.Single(AllocationScope.Commander, id, id == spikeId ? Spike() : floor));

var builds = roster.Select(Corner).ToArray();
var report = DominanceGuard.Measure(builds, theta);

// DominanceGuard.Measure names its actors positionally ("corner{i}", read directly from its own
// source: `actors[i] = TerminationGuard.ToActor($"corner{i}", builds[i], theta)`) rather than by the
// aptitude id -- ToActor itself is internal, so this tool cannot build actors with real names directly.
// "corner{i}" maps exactly to builds[i], which is roster[i] by construction (builds = roster.Select
// (Corner)), so parsing the index back out is exact, not a guess about implementation behavior.
static int CornerIndex(string name) => int.Parse(name["corner".Length..]);

// wins[i][j]: attacker i's win share against defender j; diagonal 0.5, matching the existing checked-in
// baseline's own convention (self-vs-self placeholder, never read).
var wins = new double[roster.Length][];
for (var i = 0; i < roster.Length; i++)
{
    wins[i] = new double[roster.Length];
    for (var j = 0; j < roster.Length; j++) wins[i][j] = 0.5;
}
foreach (var arrow in report.Matrix)
{
    var i = CornerIndex(arrow.AttackerName);
    var j = CornerIndex(arrow.DefenderName);
    wins[i][j] = arrow.WinShareAttacker;
}

// unending[i][j]: the termination invariant per ordered pair, via the SAME public entry point
// (TerminationGuard.Assert) tools/ResidualFitLoop already uses for this exact sweep (P8.6/P8.7) --
// a 2-build array per call, try/catch on TerminationViolation, never a re-derivation of the net-
// attrition condition itself.
var unending = new bool[roster.Length][];
for (var i = 0; i < roster.Length; i++)
{
    unending[i] = new bool[roster.Length];
    for (var j = 0; j < roster.Length; j++)
    {
        if (i == j) continue;
        try { TerminationGuard.Assert(new[] { builds[i], builds[j] }, theta); }
        catch (TerminationViolation) { unending[i][j] = true; }
    }
}

var payload = new
{
    model = "data/tuning/aptitudes (live, via FusionRpg.Core.DominanceGuard/TerminationGuard — not tools/CombatSim)",
    theta,
    dominanceMatrix = new { names = roster, wins, unending },
    dominantCorners = report.DominantBuildNames.Select(n => roster[CornerIndex(n)]).ToArray(),
};

var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
if (string.IsNullOrEmpty(outPath)) Console.WriteLine(json);
else File.WriteAllText(outPath, json);

return 0;

static string ArgOrDefault(string[] a, string flag, string fallback)
{
    var idx = Array.IndexOf(a, flag);
    return idx >= 0 && idx + 1 < a.Length ? a[idx + 1] : fallback;
}

// Mirrors tools/tuning/publish.py's own latest_version(domain) / tools/ResidualFitLoop's own
// LatestTuningFileName exactly (highest {domain}.v{n}.json in the tuning dir) rather than a hardcoded
// version literal.
static string LatestTuningFileName(string tuningDir, string domain)
{
    var pat = new Regex($@"^{Regex.Escape(domain)}\.v(\d+)\.json$");
    var best = Directory.EnumerateFiles(tuningDir)
        .Select(Path.GetFileName)
        .Select(n => (Name: n!, Match: pat.Match(n!)))
        .Where(x => x.Match.Success)
        .Select(x => (x.Name, Version: int.Parse(x.Match.Groups[1].Value)))
        .OrderByDescending(x => x.Version)
        .FirstOrDefault();
    if (best.Name is null) throw new InvalidOperationException($"no {domain}.v*.json found in {tuningDir}");
    return best.Name;
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "scripts", "guard-class-system.ps1"))) return dir.FullName;
        dir = dir.Parent;
    }
    throw new InvalidOperationException("could not locate repo root above " + AppContext.BaseDirectory);
}
