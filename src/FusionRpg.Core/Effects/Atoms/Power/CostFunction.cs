using FusionRpg.Core.Stats;
using System.Text.Json;

namespace FusionRpg.Core.Effects.Atoms.Power;

/// <summary>Why an atom could not be priced. Unpriced is never zero.</summary>
public readonly record struct PriceVerdict(bool Ok, string Reason)
{
    public static PriceVerdict Priced => new(true, "");
    public static PriceVerdict No(string reason) => new(false, reason);
}

/// <summary>A price and, when it could not be computed, the reason.</summary>
public readonly record struct PricedAtom(PowerVector Power, PriceVerdict Verdict)
{
    public bool Ok => Verdict.Ok;
}

/// <summary>
/// What one atom costs (spec-power-vector.md, E9).
///
/// <code>
/// power[category] = coeff(kind, channel) × normalize(magnitude, referenceScale) × conditionality
/// </code>
///
/// <para><b>Pure and integer.</b> The same row prices to the same vector on every machine and in
/// every process, because the price is stored, hashed, budgeted and compared. A <c>double</c> would
/// make two runs disagree in the last bit and move a content hash for nothing.</para>
///
/// <para><b>It is knowingly wrong on multiplicative pairs, by design.</b> Crit rate × crit damage,
/// the element ring and shield layers all multiply — two strong element slots give 1.25 × 1.25 =
/// +562.5‰ where naive addition says +500‰. A per-atom cost function prices each half in isolation
/// and underprices both. E9 does not solve that; <b>E10's marginal read</b> does, where it matters.
/// Stored atom power stays context-free and approximately right, which is all budgets and display
/// need — and the ±25% drift tolerance exists because of exactly this, not out of vagueness.</para>
/// </summary>
public static class CostFunction
{
    /// <summary>How deep a spawn may price the actor it makes. See <see cref="ActorPowerCache"/>.</summary>
    public const int MaxSpawnDepth = 1;

    public static PricedAtom Price(AtomRow atom, PowerTables? tables = null, int depth = 0)
    {
        if (atom is null) return new PricedAtom(PowerVector.Zero, PriceVerdict.No("null row"));

        var t = tables ?? PowerTables.Current;
        var kind = AtomKindRegistry.Get(atom.KindId);
        if (kind is null)
            return new PricedAtom(PowerVector.Zero, PriceVerdict.No($"unknown kind '{atom.KindId}'"));

        var pars = Read(atom.ParamsJson);
        var when = Read(atom.WhenJson);

        var coeff = t.Find(atom.KindId, StringOf(pars, "channel"));
        if (coeff is null)
            return new PricedAtom(PowerVector.Zero,
                PriceVerdict.No($"no coefficient for {atom.KindId}"
                                + (StringOf(pars, "channel") is { } c ? $" channel '{c}'" : "")));

        // The MAGNITUDE, not the delta. `-100 hp` on a hit is 100 points of offense, not −100 of it:
        // the sign says which way the resource moves, and which kind of worth that is, is what the
        // category already carries. Pricing the signed value made every damage atom worth a negative
        // amount — so a budget over a damage item RELAXED as the item got deadlier.
        var magnitude = Math.Abs(MeanMagnitude(atom, kind, pars));
        var normalisedMilli = PowerMath.DivRound(magnitude * PowerMath.One, Math.Max(1, coeff.ReferenceScale));
        var conditionalityMilli = Conditionality(when, pars, t);

        var basePoints = PowerMath.MulMilli(normalisedMilli, coeff.CoeffMilli);
        var points = PowerMath.MulMilli(basePoints, conditionalityMilli);

        // E16: on a lower-is-better channel, going DOWN is the buff. `quickening` reduces an attack
        // interval, and pricing the raw magnitude would file the game's most wanted affix as a
        // penalty — negative power, failing no budget, sorting last in every UI.
        if (StatChannels.IsLowerBetter(StringOf(pars, "channel")) && SignOf(atom, kind, pars) > 0)
            points = -points;

        var vector = PowerVector.FromCategory(kind.Categories, points);

        // A spawn is worth the body it makes, priced from its own hp/atk rather than treated as base
        // stats worth nothing — else `spawn.entity{hp: 5000}` prices at zero (D3).
        if (string.Equals(atom.KindId, "spawn.entity", StringComparison.Ordinal))
            vector += SpawnBody(pars, conditionalityMilli, t, depth);

        return new PricedAtom(vector, PriceVerdict.Priced);
    }

    /// <summary>
    /// <c>(chance/1000) × triggerFrequency × icdFactor × targetCountFactor</c>, in per-mille
    /// throughout with a single rounding at the end.
    ///
    /// <para><b>Every factor stays in per-mille</b> (D1). Rounding <c>chance/1000</c> to an integer
    /// as the formula went made it 0 for every proc below 1000‰ — the entire conditional half of the
    /// catalog priced at zero, and it read as a design limitation rather than as arithmetic.</para>
    ///
    /// <para><b>A triggerless atom is unconditional.</b> Permanent modifiers are not event-driven, and
    /// without this short-circuit the 26 passive families price at zero.</para>
    /// </summary>
    public static long Conditionality(
        IReadOnlyDictionary<string, JsonElement> when,
        IReadOnlyDictionary<string, JsonElement> pars,
        PowerTables tables)
    {
        var trigger = StringOf(when, "trigger");
        if (string.IsNullOrEmpty(trigger)) return PowerMath.One;

        var chanceMilli = IntOf(when, "chance") ?? 1000;

        // Frequency is normalised against a nominal one-per-second baseline (60/min), so a trigger
        // that fires as often as a hit is worth 1.0 and rarer ones are worth proportionally less.
        var perMinute = tables.FrequencyOf(trigger);
        var frequencyMilli = PowerMath.DivRound((long)perMinute * PowerMath.One, 60);

        var factor = PowerMath.CombineMilli(chanceMilli, frequencyMilli);
        factor = PowerMath.CombineMilli(factor, IcdFactorMilli(IntOf(when, "icd_ms") ?? 0, perMinute));
        factor = PowerMath.CombineMilli(factor, TargetFactorMilli(pars));
        return factor;
    }

    /// <summary>
    /// <c>min(1, triggerFrequency⁻¹ / (icd_ms/60000))</c> — how much of the trigger's natural rate an
    /// internal cooldown actually lets through.
    ///
    /// <para>1 when there is no ICD, <b>and</b> when the frequency is zero: the formula divides by the
    /// frequency, so an unlisted trigger would otherwise divide by zero rather than simply be rare.</para>
    /// </summary>
    public static long IcdFactorMilli(int icdMs, int perMinute)
    {
        if (icdMs <= 0 || perMinute <= 0) return PowerMath.One;

        // Fires the ICD permits per minute, capped at the trigger's own rate.
        var permitted = PowerMath.DivRound(60_000L, icdMs);
        return permitted >= perMinute
            ? PowerMath.One
            : PowerMath.DivRound(permitted * PowerMath.One, perMinute);
    }

    /// <summary>
    /// <c>min(maxTargets, expectedTargets)</c>, floored at 1 (D4).
    ///
    /// <para>An omitted target count is one target, not none. Zero would price every single-target
    /// atom — which is most of them — at nothing.</para>
    /// </summary>
    public static long TargetFactorMilli(IReadOnlyDictionary<string, JsonElement> pars)
    {
        var expected = IntOf(pars, "expectedTargets") ?? IntOf(pars, "maxTargets") ?? 1;
        return Math.Max(1, expected) * PowerMath.One;
    }

    /// <summary>
    /// The body a spawn makes, at depth 1.
    ///
    /// <para>Mutually recursive by construction — an atom's price calls the actor price function,
    /// the same shape as a card game pricing a summon by the body it makes. <b>Depth 1</b> truncates:
    /// a spawned actor's own spawn atoms are priced and then stop. Without that a chain of summoners
    /// prices forever.</para>
    /// </summary>
    static PowerVector SpawnBody(
        IReadOnlyDictionary<string, JsonElement> pars, long conditionalityMilli, PowerTables tables, int depth)
    {
        if (depth >= MaxSpawnDepth) return PowerVector.Zero;

        // Floored at 1 (D3): an omitted count defaulting to 0 prices the whole spawn at zero, which
        // is the defect the body pricing exists to fix.
        var count = Math.Max(1, IntOf(pars, "count") ?? 1);

        var hp = IntOf(pars, "maxHp") ?? IntOf(pars, "hp") ?? 0;
        var atk = IntOf(pars, "atk") ?? 0;
        if (hp == 0 && atk == 0) return PowerVector.Zero;

        var body = ActorPowerCache.PriceBody(hp, atk, tables);
        return body.ScaleMilli(PowerMath.CombineMilli(conditionalityMilli, count * PowerMath.One));
    }

    /// <summary>
    /// The magnitude a price is computed from — the <b>mean</b> of an authored range.
    ///
    /// <para>Variance itself has value and this ignores it, deliberately: an atom rolling 100–200 and
    /// one fixed at 150 are priced alike, which is wrong in the direction that does not matter for a
    /// budget.</para>
    /// </summary>
    public static int MeanMagnitude(
        AtomRow atom, AtomKind kind, IReadOnlyDictionary<string, JsonElement> pars)
    {
        foreach (var def in kind.Params.Defs)
        {
            if (def.Kind != ParamKind.Value) continue;
            if (!pars.TryGetValue(def.Name, out var raw)) continue;
            if (!AtomJson.TryReadValueSpec(raw, out var spec).IsOk) continue;

            return (int)PowerMath.DivRound((long)spec.Min + spec.Max, 2);
        }

        // No magnitude at all — a status application, a board op, a shield whose amount rides the
        // overlay. One reference unit, so it prices as "one of whatever this kind does".
        return 1;
    }

    /// <summary>
    /// Which way the atom moves its channel, before the direction flip. Reads the authored sign,
    /// which <see cref="Price"/> has already taken the absolute value of.
    /// </summary>
    static int SignOf(AtomRow atom, AtomKind kind, IReadOnlyDictionary<string, JsonElement> pars) =>
        Math.Sign(MeanMagnitude(atom, kind, pars));

    // ---- json helpers -----------------------------------------------------------------------------

    internal static Dictionary<string, JsonElement> Read(string? json)
    {
        var d = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return d;
        try
        {
            using var doc = JsonDocument.Parse(json!);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return d;
            foreach (var p in doc.RootElement.EnumerateObject()) d[p.Name] = p.Value.Clone();
        }
        catch (JsonException) { /* E4 already refused this row */ }
        return d;
    }

    static string? StringOf(IReadOnlyDictionary<string, JsonElement> map, string name) =>
        map.TryGetValue(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    static int? IntOf(IReadOnlyDictionary<string, JsonElement> map, string name) =>
        map.TryGetValue(name, out var el) && el.ValueKind == JsonValueKind.Number
        && el.TryGetInt32(out var v) ? v : null;
}
