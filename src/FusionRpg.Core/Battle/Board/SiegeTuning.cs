using System.Text.Json;

namespace FusionRpg.Core.Battle.Board;

/// <summary>
/// base-defense `siege-board` (spec-siege-board.md) — the board's balance surface
/// (tunables-ssot.md T1). `MaxCells` rides the same file even though it is structural, not balance
/// (an allocation/perf bound on one board, AGENTS.md's exemption for structural limits) — kept as a
/// config row rather than a hidden `const`, per tunables-ssot.md's own preference for one balance
/// surface over a scattered one. See <see cref="SiegeTuningPolicy"/>.
/// </summary>
public sealed record SiegeTuning(
    int SchemaVersion, int Version,
    int MoveCostOpen, int MoveCostRough, int DiagonalSurcharge, int MaxCells,
    DistrictTuning District);

/// <summary>
/// base-defense `district-layout` (spec-district-layout.md §2/§3). <see cref="SideByBaseTier"/> is a
/// LOOKUP keyed by <c>WorldSector.DevelopmentLevel</c> (base-defense-ideal.md: "Seat / tier ...
/// DevelopmentLevel is exactly this"), never a formula — a DevelopmentLevel past the highest
/// authored key plateaus at that key's side. <see cref="CoreSideMilli"/> is a per-mille RATIO
/// (0..1000), exempt from the progression-ceiling rule for the same reason any ratio is.
/// </summary>
public sealed record DistrictTuning(
    IReadOnlyDictionary<int, int> SideByBaseTier,
    int CoreSideMilli, int GateCount, int RampartThickness, int FortressRampartBonus,
    int ApproachDepth, int ApproachDepthPerWardLevel);

public sealed class SiegeTuningRejection : Exception
{
    public SiegeTuningRejection(string message) : base(message) { }
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class SiegeTuningLoader
{
    public static SiegeTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new SiegeTuningRejection("siege tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new SiegeTuningRejection($"siege tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            var board = Obj(root, "board");
            var moveCost = Obj(board, "moveCost");

            var open = Int(moveCost, "open");
            if (open <= 0)
                throw new SiegeTuningRejection($"siege tuning: board.moveCost.open must be > 0; got {open}");

            var rough = Int(moveCost, "rough");
            if (rough <= 0)
                throw new SiegeTuningRejection($"siege tuning: board.moveCost.rough must be > 0; got {rough}");

            var diagonal = Int(moveCost, "diagonalSurcharge");
            if (diagonal < 0)
                throw new SiegeTuningRejection($"siege tuning: board.moveCost.diagonalSurcharge must be >= 0; got {diagonal}");

            var maxCells = Int(board, "maxCells");
            if (maxCells <= 0)
                throw new SiegeTuningRejection($"siege tuning: board.maxCells must be > 0; got {maxCells}");

            var district = Obj(root, "district");

            var sideByBaseTier = new Dictionary<int, int>();
            foreach (var prop in Obj(district, "sideByBaseTier").EnumerateObject())
            {
                if (!int.TryParse(prop.Name, out var tier) || tier < 0)
                    throw new SiegeTuningRejection($"siege tuning: district.sideByBaseTier key '{prop.Name}' is not a non-negative integer");
                if (prop.Value.ValueKind != JsonValueKind.Number || !prop.Value.TryGetInt32(out var side) || side <= 0)
                    throw new SiegeTuningRejection($"siege tuning: district.sideByBaseTier.{prop.Name} must be a positive integer");
                sideByBaseTier[tier] = side;
            }
            if (sideByBaseTier.Count == 0)
                throw new SiegeTuningRejection("siege tuning: district.sideByBaseTier must name at least one tier");

            var coreSideMilli = Int(district, "coreSideMilli");
            if (coreSideMilli is <= 0 or > 1000)
                throw new SiegeTuningRejection($"siege tuning: district.coreSideMilli must be in (0, 1000]; got {coreSideMilli}");

            var gateCount = Int(district, "gateCount");
            if (gateCount <= 0)
                throw new SiegeTuningRejection($"siege tuning: district.gateCount must be > 0; got {gateCount}");

            var rampartThickness = Int(district, "rampartThickness");
            if (rampartThickness <= 0)
                throw new SiegeTuningRejection($"siege tuning: district.rampartThickness must be > 0; got {rampartThickness}");

            var fortressBonus = Int(district, "fortressRampartBonus");
            if (fortressBonus < 0)
                throw new SiegeTuningRejection($"siege tuning: district.fortressRampartBonus must be >= 0; got {fortressBonus}");

            var approachDepth = Int(district, "approachDepth");
            if (approachDepth < 0)
                throw new SiegeTuningRejection($"siege tuning: district.approachDepth must be >= 0; got {approachDepth}");

            var approachPerWard = Int(district, "approachDepthPerWardLevel");
            if (approachPerWard < 0)
                throw new SiegeTuningRejection($"siege tuning: district.approachDepthPerWardLevel must be >= 0; got {approachPerWard}");

            return new SiegeTuning(
                SchemaVersion: Int(root, "schemaVersion"),
                Version: Int(root, "version"),
                MoveCostOpen: open,
                MoveCostRough: rough,
                DiagonalSurcharge: diagonal,
                MaxCells: maxCells,
                District: new DistrictTuning(
                    SideByBaseTier: sideByBaseTier,
                    CoreSideMilli: coreSideMilli,
                    GateCount: gateCount,
                    RampartThickness: rampartThickness,
                    FortressRampartBonus: fortressBonus,
                    ApproachDepth: approachDepth,
                    ApproachDepthPerWardLevel: approachPerWard));
        }
    }

    static JsonElement Obj(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Object)
            throw new SiegeTuningRejection($"siege tuning: missing or non-object '{key}'");
        return el;
    }

    static int Int(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new SiegeTuningRejection($"siege tuning: missing or non-integer '{key}'");
        return v;
    }
}

/// <summary>See <see cref="SiegeTuning"/>.</summary>
public static class SiegeTuningPolicy
{
    static SiegeTuning? _tuning;

    public static void Configure(SiegeTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    static SiegeTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "SiegeTuningPolicy.Configure(...) has not run. GridSpec/BoardState read " +
        "data/tuning/siege.v1.json (tunables-ssot.md T5) — there is no built-in default to fall back to.");

    public static int MoveCostOpen => Tuning.MoveCostOpen;
    public static int MoveCostRough => Tuning.MoveCostRough;
    public static int DiagonalSurcharge => Tuning.DiagonalSurcharge;

    /// <summary>Structural, not balance — see <see cref="SiegeTuning"/>'s own doc comment. Enforced
    /// loudly at <see cref="GridSpec"/> construction, never at render.</summary>
    public static int MaxCells => Tuning.MaxCells;

    public static DistrictTuning District => Tuning.District;
}
