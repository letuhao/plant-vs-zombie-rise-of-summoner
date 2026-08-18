namespace FusionRpg.Core.Activity;

/// <summary>Canonical PvzActivity fact kinds (append-only ledger).</summary>
public static class PvzActivityKinds
{
    public const string MatchStarted = "MatchStarted";
    public const string MatchEnded = "MatchEnded";
    public const string ZombieKilled = "ZombieKilled";
    public const string PlantLost = "PlantLost";
    public const string PlantPlaced = "PlantPlaced";
    public const string ExtraSpawnFired = "ExtraSpawnFired";
    public const string MowerUsed = "MowerUsed";
    public const string ZombieSpawned = "ZombieSpawned";

    public static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        MatchStarted, MatchEnded, ZombieKilled, PlantLost, PlantPlaced, ExtraSpawnFired, MowerUsed, ZombieSpawned
    };

    public static bool IsKnown(string? kind) =>
        !string.IsNullOrWhiteSpace(kind) && Known.Contains(kind.Trim());

    public static string? FromCaptureKind(string? captureKind) => captureKind switch
    {
        "board.start" => MatchStarted,
        "match.result" => MatchEnded,
        "zombie.die" => ZombieKilled,
        "plant.die" => PlantLost,
        "plant.place" => PlantPlaced,
        "mower.start" => MowerUsed,
        "zombie.spawn" => ZombieSpawned,
        _ => null
    };

    /// <summary>Dedupe key for capture projection (never empty when possible).</summary>
    public static string DedupeKeyForCapture(string factKind, string? ptr, int? col, int? row, string t) =>
        factKind switch
        {
            MatchStarted or MatchEnded => "run",
            ZombieKilled or PlantLost or MowerUsed or ZombieSpawned =>
                !string.IsNullOrWhiteSpace(ptr) ? ptr! : $"t:{t}",
            PlantPlaced =>
                !string.IsNullOrWhiteSpace(ptr)
                    ? ptr!
                    : $"{col?.ToString() ?? "x"}:{row?.ToString() ?? "y"}",
            _ => Guid.NewGuid().ToString("N")
        };

    /// <summary>Normalize match result for rollup victory/defeat counters.</summary>
    public static string? NormalizeMatchResult(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim().ToLowerInvariant();
        return s switch
        {
            "victory" or "win" or "won" => "victory",
            "defeat" or "lose" or "loss" or "lost" => "defeat",
            _ => s
        };
    }
}

/// <summary>Pure rollup counters rebuilt from facts (cache only).</summary>
public sealed class PvzActivityRollupCounters
{
    public long MatchesStarted { get; set; }
    public long MatchesEnded { get; set; }
    public long Victories { get; set; }
    public long Defeats { get; set; }
    public long ZombiesKilled { get; set; }
    public long PlantsLost { get; set; }
    public long PlantsPlaced { get; set; }
    public long ExtraSpawnsFired { get; set; }
}

public static class PvzActivityRollupBuilder
{
    public static PvzActivityRollupCounters Build(IEnumerable<(string Kind, string? PayloadJson)> facts)
    {
        var c = new PvzActivityRollupCounters();
        foreach (var (kind, payload) in facts)
            ApplyDelta(c, kind, payload);
        return c;
    }

    /// <summary>O(1) incremental update — must match <see cref="Build"/> for one fact.</summary>
    public static void ApplyDelta(PvzActivityRollupCounters c, string kind, string? payloadJson)
    {
        switch (kind)
        {
            case PvzActivityKinds.MatchStarted:
                c.MatchesStarted++;
                break;
            case PvzActivityKinds.MatchEnded:
                c.MatchesEnded++;
                var result = PvzActivityKinds.NormalizeMatchResult(TryResult(payloadJson));
                if (result == "victory")
                    c.Victories++;
                else if (result == "defeat")
                    c.Defeats++;
                break;
            case PvzActivityKinds.ZombieKilled:
                c.ZombiesKilled++;
                break;
            case PvzActivityKinds.PlantLost:
                c.PlantsLost++;
                break;
            case PvzActivityKinds.PlantPlaced:
                c.PlantsPlaced++;
                break;
            case PvzActivityKinds.ExtraSpawnFired:
                c.ExtraSpawnsFired++;
                break;
        }
    }

    static string? TryResult(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("result", out var el))
                return el.GetString();
        }
        catch { /* ignore */ }
        return null;
    }
}
