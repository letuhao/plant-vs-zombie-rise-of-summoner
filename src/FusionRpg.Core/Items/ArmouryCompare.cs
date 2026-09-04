using System.Text.Json;
using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Items;

/// <summary>One channel's magnitude on each side. <c>Unit</c> labels rather than converts — SC4:
/// magnitudes across channel families are not comparable, so they are never summed across channels.</summary>
public sealed record ChannelDelta(string Channel, string Unit, long Incumbent, long Candidate, long Delta);

public enum DominanceVerdict
{
    StrictlyBetter,
    StrictlyWorse,
    Sidegrade,
    /// <summary>The two items touch entirely disjoint channel sets — there is nothing to weigh.</summary>
    Incomparable,
}

/// <summary>Where a rolled value sits in its atom's authored <c>[Min, Max]</c>, unit-free. 1000‰ for a
/// <c>Fixed</c> value spec — nothing rolled, so there is nothing to grade against.</summary>
public sealed record RollQualityEntry(string AtomId, int Milli);

public sealed record CompareResult(
    IReadOnlyList<ChannelDelta> Deltas,
    DominanceVerdict Dominance,
    IReadOnlyList<RollQualityEntry> RollQualities,
    int MeanRollQualityMilli);

/// <summary>One item's atom, paired with its frozen roll (<c>InstanceAtomRow.ValuesJson</c>) — the pair
/// this file needs and nothing more, so it stays a Core, DB-free, unit-testable type.</summary>
public readonly record struct CompareAtom(AtomRow Atom, string ValuesJson);

/// <summary>
/// I13 §5.5's three signals: per-channel delta, dominance verdict, roll quality. **No invented
/// scalar** (SC9) — a weighted sum across channel families would be wrong and look authoritative,
/// which is worse than no number. When module 9 lands, <c>power_json</c> becomes a fourth column
/// here, never a replacement: a single number cannot say *what* got better.
///
/// <para><b>Honestly scoped, not fully general.</b> Only <c>stat.modify</c>/<c>stat.derived</c> atoms
/// carry a <c>channel</c>; a <c>flat</c>/<c>replace</c> op labels its magnitude <c>game-units</c>, an
/// <c>increased</c>/<c>more</c> op labels it <c>per-mille</c>. Kinds with no <c>channel</c>
/// (<c>board.action</c>, <c>resource.delta</c>, …) do not contribute a channel delta — an interim
/// simplification, matching <c>item-power-reads</c>'s own precedent of shrinking to what a real
/// consumer needs rather than a fully general reader nobody has asked for yet.</para>
/// </summary>
public static class ArmouryCompare
{
    public static CompareResult Compare(IReadOnlyList<CompareAtom> incumbent, IReadOnlyList<CompareAtom> candidate)
    {
        var incumbentChannels = ChannelMagnitudes(incumbent);
        var candidateChannels = ChannelMagnitudes(candidate);

        var incumbentKeys = incumbentChannels.Keys.ToHashSet(StringComparer.Ordinal);
        var candidateKeys = candidateChannels.Keys.ToHashSet(StringComparer.Ordinal);
        var overlaps = incumbentKeys.Overlaps(candidateKeys);

        var union = incumbentKeys.Union(candidateKeys).OrderBy(k => k, StringComparer.Ordinal);
        var deltas = new List<ChannelDelta>();
        foreach (var channel in union)
        {
            incumbentChannels.TryGetValue(channel, out var i);
            candidateChannels.TryGetValue(channel, out var c);
            var unit = i.Unit ?? c.Unit ?? "game-units";
            deltas.Add(new ChannelDelta(channel, unit, i.Value, c.Value, c.Value - i.Value));
        }

        // Disjoint channel sets (both non-empty, nothing shared) means there is nothing to weigh --
        // that is the honest "incomparable" case, distinct from a sidegrade (shared channels moving
        // in opposite directions).
        var dominance =
            incumbentKeys.Count > 0 && candidateKeys.Count > 0 && !overlaps
                ? DominanceVerdict.Incomparable
                : VerdictFrom(deltas);

        var rollQualities = candidate.Select(a => new RollQualityEntry(a.Atom.AtomId, RollQualityMilliOf(a))).ToList();
        var mean = rollQualities.Count == 0 ? 1000 : (int)Math.Round(rollQualities.Average(r => r.Milli));

        return new CompareResult(deltas, dominance, rollQualities, mean);
    }

    static DominanceVerdict VerdictFrom(IReadOnlyList<ChannelDelta> deltas)
    {
        var anyPositive = deltas.Any(d => d.Delta > 0);
        var anyNegative = deltas.Any(d => d.Delta < 0);
        if (anyPositive && anyNegative) return DominanceVerdict.Sidegrade;
        if (anyPositive) return DominanceVerdict.StrictlyBetter;
        if (anyNegative) return DominanceVerdict.StrictlyWorse;
        return DominanceVerdict.Sidegrade; // identical on every shared channel -- no change either way
    }

    static Dictionary<string, (long Value, string? Unit)> ChannelMagnitudes(IReadOnlyList<CompareAtom> atoms)
    {
        var map = new Dictionary<string, (long, string?)>(StringComparer.Ordinal);
        foreach (var a in atoms)
        {
            if (a.Atom.KindId is not ("stat.modify" or "stat.derived")) continue;
            if (!TryReadChannelOpAmount(a.ValuesJson, out var channel, out var op, out var amount)) continue;

            var unit = op is "increased" or "more" ? "per-mille" : "game-units";
            map[channel] = map.TryGetValue(channel, out var existing)
                ? (existing.Item1 + amount, existing.Item2 ?? unit)
                : (amount, unit);
        }
        return map;
    }

    static bool TryReadChannelOpAmount(string json, out string channel, out string op, out long amount)
    {
        channel = ""; op = ""; amount = 0;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;

            if (!doc.RootElement.TryGetProperty("channel", out var ch) || ch.ValueKind != JsonValueKind.String)
                return false;
            channel = ch.GetString() ?? "";

            op = doc.RootElement.TryGetProperty("op", out var opEl) && opEl.ValueKind == JsonValueKind.String
                ? opEl.GetString() ?? "" : "";

            if (!doc.RootElement.TryGetProperty("amount", out var amt)) return false;
            amount = amt.ValueKind switch
            {
                JsonValueKind.Number => amt.GetInt64(),
                _ => 0,
            };
            return true;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>
    /// 1000‰ for a <c>Fixed</c> spec (<c>min == max</c>, or "amount" is a plain number rather than a
    /// <c>{min,max}</c> object) — nothing rolled, so there is nothing to grade against. Otherwise the
    /// rolled value's position in <c>[min, max]</c>, clamped, so a malformed or content-edited bound
    /// never reports outside 0..1000.
    /// </summary>
    static int RollQualityMilliOf(CompareAtom a)
    {
        if (!TryReadAmountBounds(a.Atom.ParamsJson, out var min, out var max)) return 1000;
        if (max <= min) return 1000;
        if (!TryReadRolledAmount(a.ValuesJson, out var rolled)) return 1000;

        var milli = (int)Math.Round((rolled - min) * 1000.0 / (max - min));
        return Math.Clamp(milli, 0, 1000);
    }

    static bool TryReadAmountBounds(string paramsJson, out long min, out long max)
    {
        min = 0; max = 0;
        if (string.IsNullOrWhiteSpace(paramsJson)) return false;
        try
        {
            using var doc = JsonDocument.Parse(paramsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            if (!doc.RootElement.TryGetProperty("amount", out var amt)) return false;
            if (amt.ValueKind != JsonValueKind.Object) return false; // a plain number is Fixed -- 1000 is correct
            if (!amt.TryGetProperty("min", out var minEl) || !amt.TryGetProperty("max", out var maxEl)) return false;
            if (minEl.ValueKind != JsonValueKind.Number || maxEl.ValueKind != JsonValueKind.Number) return false;
            min = minEl.GetInt64();
            max = maxEl.GetInt64();
            return true;
        }
        catch (JsonException) { return false; }
    }

    static bool TryReadRolledAmount(string valuesJson, out long rolled)
    {
        rolled = 0;
        if (string.IsNullOrWhiteSpace(valuesJson)) return false;
        try
        {
            using var doc = JsonDocument.Parse(valuesJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            if (!doc.RootElement.TryGetProperty("amount", out var amt) || amt.ValueKind != JsonValueKind.Number)
                return false;
            rolled = amt.GetInt64();
            return true;
        }
        catch (JsonException) { return false; }
    }
}
