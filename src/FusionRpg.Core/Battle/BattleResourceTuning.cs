using System.Text.Json;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Battle;

/// <summary>
/// `battle-tempo` `battle-resources` (spec-battle-resources.md §2.2) — each actor resource pool's
/// max as a per-mille share of <see cref="BattleRuleset.BaseHp"/>. A projection of the shipped
/// ladder, never a second curve: the only numbers this module introduces are the shares themselves.
///
/// <para><c>hp</c> is deliberately absent — it mirrors <c>BattleActorSetup.MaxHp</c> directly (spec
/// §2.6), because two disagreeing HP maxima is a worse outcome than an incomplete row.</para>
/// </summary>
public sealed record BattleResourceTuning(
    int SchemaVersion, int Version,
    IReadOnlyDictionary<string, int> PoolShareMilli)
{
    /// <summary>Refuses rather than defaults: a resource the closed set ships but config forgot is a
    /// missing balance row, not a request for a built-in fallback (the same stance
    /// <see cref="BattleTuning.ProfileOf"/> already takes).</summary>
    public int ShareOf(string resourceId) =>
        PoolShareMilli.TryGetValue(resourceId, out var s) ? s : throw new BattleResourceTuningRejection(
            $"battle-resources tuning: no poolShareMilli entry for resource '{resourceId}'. Every " +
            "resource in DerivedStatChannels.ResourceIds except 'hp' must carry a share — there is no " +
            "built-in default to fall back to.");
}

public sealed class BattleResourceTuningRejection : Exception
{
    public BattleResourceTuningRejection(string message) : base(message) { }
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class BattleResourceTuningLoader
{
    public static BattleResourceTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new BattleResourceTuningRejection("battle-resources tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new BattleResourceTuningRejection($"battle-resources tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;

            if (!root.TryGetProperty("poolShareMilli", out var sharesEl) || sharesEl.ValueKind != JsonValueKind.Object)
                throw new BattleResourceTuningRejection("battle-resources tuning: missing or non-object 'poolShareMilli'");

            var shares = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var prop in sharesEl.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Number || !prop.Value.TryGetInt32(out var v))
                    throw new BattleResourceTuningRejection(
                        $"battle-resources tuning: poolShareMilli.{prop.Name} is not an integer");
                if (v < 0)
                    throw new BattleResourceTuningRejection(
                        $"battle-resources tuning: poolShareMilli.{prop.Name} must be >= 0 (it is a share of a pool); got {v}");
                shares[prop.Name] = v;
            }

            // Every id in the closed set except `hp` must be authored. A silently-missing share would
            // make exactly one pool max 0 -- which is the bug this whole module exists to end, and it
            // would reappear as "the counter declines sometimes" rather than as a load error.
            foreach (var id in DerivedStatChannels.ResourceIds)
            {
                if (id == "hp") continue;
                if (!shares.ContainsKey(id))
                    throw new BattleResourceTuningRejection(
                        $"battle-resources tuning: poolShareMilli has no entry for '{id}'. Every resource " +
                        "in the closed set except 'hp' must carry one — a missing share silently produces " +
                        "an empty pool, which is the defect this module removes.");
            }

            if (shares.ContainsKey("hp"))
                throw new BattleResourceTuningRejection(
                    "battle-resources tuning: poolShareMilli must NOT carry 'hp' — hp's max mirrors " +
                    "BattleActorSetup.MaxHp directly (spec-battle-resources.md §2.6), and a share here " +
                    "would create a second, disagreeing HP maximum.");

            return new BattleResourceTuning(
                SchemaVersion: Int(root, "schemaVersion"),
                Version: Int(root, "version"),
                PoolShareMilli: shares);
        }
    }

    static int Int(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new BattleResourceTuningRejection($"battle-resources tuning: missing or non-integer '{key}'");
        return v;
    }
}
