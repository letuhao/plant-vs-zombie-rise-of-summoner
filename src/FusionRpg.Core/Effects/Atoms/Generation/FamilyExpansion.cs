using System.Text.Json.Nodes;
using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Effects.Atoms.Generation;

/// <summary>
/// E43 <c>family-expand</c> (spec-family-expand.md): turns the authored affix families into atom rows,
/// one row per (family, tier) — the tier axis is the only one that still materialises (§3.2, decided
/// 2026-09-03). Element does not materialise: a <c>{variant}</c>-templated channel emits ONE row per
/// tier carrying an E30 pool reference, never seven rows differing by concrete element (W7.9).
///
/// <para><b>Pure.</b> No file I/O, no database, no static config reads beyond the two delegates the
/// caller supplies. The same inputs always produce the same rows in the same order — the whole basis
/// for the CLI's <c>--check</c> mode and for this module's own determinism test.</para>
///
/// <para><b>Magnitudes come from <c>bands.v1.json</c>'s <c>channelFamilyGroups.primaryChannel</c>
/// formula, ported from <c>tools/seedsmith/seedsmith/numerics/formulas.py</c></b> — same function
/// shapes, same plain-integer <c>round_legible</c> (the registry's own richer 1/2/5-significance snap
/// is a documented, bounded gap neither this module nor its Python sibling closes). Every intermediate
/// is <c>long</c>, widened before multiplying, divided by 1000 last, and overflow throws — this
/// module's own binding numeric rule (CLAUDE.md), not a style choice.</para>
/// </summary>
public static class FamilyExpansion
{
    // Structural (tunables-ssot.md T2) — bands.v1.json's own bandCount/bandCountRationale: "5, one per
    // tier the atom layer already has... a sixth powerBand would need a sixth .t6 row on EVERY family
    // — an atom-layer change, not a bands-registry one." Not a dial a balance pass turns.
    public const int TierCount = 5;

    // Structural — bands.v1.json powerBand.tierScaling.referenceLevel. A balance pass moves
    // sharePermille in tier-bands.v1.json, never the level a reference curve is read at.
    public const int ReferenceLevel = 20;

    // Structural — bands.v1.json powerBand.tierScaling, frozen with the registry.
    const int MagnitudeRatioPermille = 1750;
    const int BandFloorPermille = 670;
    const int BandCeilingPermille = 1330;

    // Structural — bands.v1.json primaryChannel.unit: "Increased or More -> integer per-mille (a
    // ratio against the identity 1000‰)". Not a share, not a curve value — the ratio's own identity.
    const int IdentityPermille = 1000;

    /// <summary>
    /// E30's shipped pools (spec-channel-pool.md), keyed by the exact <c>{variant}</c>-templated
    /// channel an authored family names. Deliberately NOT a guessed/inferred mapping — a template with
    /// no entry here (e.g. <c>combat.power.pierce.{variant}</c>, <c>combat.power.overflow.{variant}</c>)
    /// has no shipped pool and is refused by name, never assigned the nearest-looking one.
    /// </summary>
    static readonly IReadOnlyDictionary<string, string> VariantChannelToPool =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["combat.power.{variant}"] = "pool.element-power",
            ["combat.defense.{variant}"] = "pool.element-defense",
            ["combat.accuracy.{variant}"] = "pool.element-accuracy",
            ["combat.dodge.{variant}"] = "pool.element-dodge",
            ["combat.crit.rate.{variant}"] = "pool.element-crit-rate",
            ["combat.crit.resist.{variant}"] = "pool.element-crit-resist",
            ["combat.crit.damage.{variant}"] = "pool.element-crit-damage",
            ["combat.crit.resist.damage.{variant}"] = "pool.element-crit-resist-damage",
            ["combat.shield.capacity.{variant}"] = "pool.element-shield-capacity",
            ["combat.shield.toughness.{variant}"] = "pool.element-shield-toughness",
            ["combat.shield.pen.{variant}"] = "pool.element-shield-pen",
            ["combat.shield.regen.{variant}"] = "pool.element-shield-regen",
        };

    /// <summary>
    /// <c>formulas.py</c>'s own <c>round_legible</c>, ported as long-safe integer round-half-up:
    /// <c>(numerator + denominator/2) / denominator</c>. Proven against the two committed worked
    /// examples in <c>bands.v1.json</c> (vitality 30‰×680/1000=20.4→20, might 45‰×92/1000=4.14→4).
    /// This is plain rounding, not the registry's richer 1/2/5-significance snap — a documented,
    /// bounded gap, not a silent approximation (see the Python sibling's own docstring).
    /// </summary>
    public static long RoundLegible(long numerator, long denominator)
    {
        if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
        checked { return (numerator + denominator / 2) / denominator; }
    }

    /// <summary><c>m_t = round_legible(m1 × ratio^(t-1) / 1000^(t-1))</c>, one multiply-divide step at
    /// a time so no intermediate needs more than one extra factor of <paramref name="ratioPermille"/>
    /// headroom over <paramref name="m1"/> itself.</summary>
    public static IReadOnlyList<long> TierLadder(long m1, int tierCount, int ratioPermille)
    {
        var ladder = new List<long>(tierCount) { m1 };
        checked
        {
            for (var t = 2; t <= tierCount; t++)
                ladder.Add(RoundLegible(ladder[^1] * ratioPermille, 1000));
        }
        return ladder;
    }

    /// <summary><c>(lo_t, hi_t)</c> — the ±33% band around a tier midpoint (bands.v1.json's own
    /// default width; <c>variance</c> overrides are an authoring-time concern this generator does not
    /// touch — no family in this corpus authors one).</summary>
    public static (long Lo, long Hi) Band(long midTier) => checked((
        RoundLegible((long)BandFloorPermille * midTier, 1000),
        RoundLegible((long)BandCeilingPermille * midTier, 1000)));

    /// <summary>
    /// Expand every family. <paramref name="flatReferenceBaseGameUnits"/> resolves a concrete channel
    /// to its <c>BattleRuleset</c> curve value at <see cref="ReferenceLevel"/> — <c>null</c> when no
    /// shipped curve exists for that channel (e.g. <c>arm1Max</c>/<c>arm2Max</c>/<c>attackInterval</c>
    /// today), in which case a Flat-op family on that channel is refused rather than guessing a base.
    /// Called only for <c>Flat</c> ops — <c>Increased</c>/<c>More</c> use the identity ratio and never
    /// touch a game curve at all (bands.v1.json's own unit note).
    /// </summary>
    public static FamilyExpansionResult Expand(
        IReadOnlyList<FamilyEntryInput> families,
        TierBandsInput tierBands,
        Func<string, long?> flatReferenceBaseGameUnits)
    {
        var rows = new List<AtomRow>();
        var refusals = new List<FamilyRefusal>();
        var seenKeys = new HashSet<(string Family, int Tier, string Variant)>();

        foreach (var family in families)
        {
            var stem = StemOf(family.Id);

            if (!tierBands.ChannelWeightPermille.TryGetValue(stem, out var channelWeight))
            {
                refusals.Add(new FamilyRefusal(family.Id,
                    $"no authored sharePermille for family '{family.Id}' (channel stem '{stem}' not in tier-bands.v1.json)"));
                continue;
            }

            long opWeight = IdentityPermille;
            if (!string.IsNullOrEmpty(family.Op))
            {
                if (!tierBands.OpWeightPermille.TryGetValue(family.Op, out var w))
                {
                    refusals.Add(new FamilyRefusal(family.Id,
                        $"no opWeightPermille entry for op '{family.Op}' on family '{family.Id}'"));
                    continue;
                }
                opWeight = w;
            }

            // sharePermille(family) = baseSharePermille × channelWeightPermille[stem] ×
            // opWeightPermille[op] / 1_000_000 — trusts the shipped DATA FILE's uniform-35‰ shape
            // over ssot-affixes.md §4.5's illustrative-only worked table (spec §3.2 step 1: the doc's
            // own _meta flags those examples "illustrative, not balanced", while tier-bands.v1.json's
            // _meta calls itself "the one genuinely tunable surface of item balance").
            long sharePermille;
            checked { sharePermille = RoundLegible(tierBands.BaseSharePermille * channelWeight * opWeight, 1_000_000); }

            var isElementTyped = family.Channel.Contains("{variant}", StringComparison.Ordinal);
            string? poolId = null;
            if (isElementTyped)
            {
                if (!VariantChannelToPool.TryGetValue(family.Channel, out poolId))
                {
                    refusals.Add(new FamilyRefusal(family.Id,
                        $"no matching E30 channel pool for '{family.Channel}' — none of the 12 shipped pools " +
                        "(data/seed/channel-pools/pools.v1.json) has a member set for this channel family"));
                    continue;
                }
            }

            if (!TryReferenceBaseM1(family, sharePermille, flatReferenceBaseGameUnits, out var m1, out var refuseReason))
            {
                refusals.Add(new FamilyRefusal(family.Id, refuseReason!));
                continue;
            }

            var ladder = TierLadder(m1, TierCount, MagnitudeRatioPermille);
            var opLower = string.IsNullOrEmpty(family.Op) ? "flat" : family.Op.ToLowerInvariant();

            for (var t = 1; t <= TierCount; t++)
            {
                // Element does not materialise (W7.9) — variant stays "" even for a pool-typed
                // channel, so the id grammar never grows a variant segment E43 did not earn.
                const string variant = "";
                var key = (family.Id, t, variant);
                if (!seenKeys.Add(key))
                {
                    refusals.Add(new FamilyRefusal(family.Id,
                        $"collision on (family_id, tier, variant) = ('{family.Id}', {t}, '{variant}')"));
                    continue;
                }

                var (lo, hi) = Band(ladder[t - 1]);

                var paramsObj = new JsonObject
                {
                    ["channel"] = poolId is null
                        ? family.Channel
                        : new JsonObject { ["pool"] = poolId, ["count"] = 1, ["allowRepeat"] = false },
                    ["op"] = opLower,
                    ["amount"] = new JsonObject { ["min"] = lo, ["max"] = hi, ["roll"] = "onApply" },
                };

                var tagsObj = new JsonObject
                {
                    ["generatedFrom"] = family.SourceFile,
                    ["generator"] = "E43",
                };

                rows.Add(new AtomRow
                {
                    AtomId = AtomRow.DeriveId(family.Id, variant, t),
                    KindId = family.KindId,
                    FamilyId = family.Id,
                    Variant = variant,
                    Tier = t,
                    Name = $"{family.Name} T{t}",
                    WhenJson = "{}",
                    ParamsJson = paramsObj.ToJsonString(),
                    TagsJson = tagsObj.ToJsonString(),
                    Enabled = true,
                });
            }
        }

        return new FamilyExpansionResult(rows, refusals);
    }

    static string StemOf(string familyId) =>
        familyId.StartsWith("atom.", StringComparison.Ordinal) ? familyId["atom.".Length..] : familyId;

    /// <summary>
    /// <c>m1 = round_legible(sharePermille × referenceBase / 1000)</c> — <paramref name="referenceBase"/>
    /// is a real <c>BattleRuleset</c> curve value for a Flat op (game units), or the identity 1000‰ for
    /// Increased/More (bands.v1.json's own unit note: "Increased or More -> integer per-mille, a ratio
    /// against the identity 1000‰" — no game curve is read at all for those two ops). Any other op
    /// (<c>Replace</c>/<c>Flag</c>) carries no tier-band magnitude by the same document's own words and
    /// is refused rather than silently zeroed.
    /// </summary>
    static bool TryReferenceBaseM1(
        FamilyEntryInput family, long sharePermille, Func<string, long?> flatReferenceBaseGameUnits,
        out long m1, out string? refuseReason)
    {
        m1 = 0;
        refuseReason = null;
        var op = family.Op ?? "";

        if (string.Equals(op, "Flat", StringComparison.Ordinal))
        {
            var referenceBase = flatReferenceBaseGameUnits(family.Channel);
            if (referenceBase is null)
            {
                refuseReason = $"no referenceBaseGameUnits for channel '{family.Channel}' (op Flat) — " +
                                "no BattleRuleset curve is shipped for this channel yet";
                return false;
            }

            checked { m1 = RoundLegible(sharePermille * referenceBase.Value, 1000); }
            return true;
        }

        if (op is "Increased" or "More")
        {
            checked { m1 = RoundLegible(sharePermille * IdentityPermille, 1000); }
            return true;
        }

        refuseReason = $"op '{op}' has no supported tier-magnitude formula in E43's primaryChannel/" +
                        "flatDerivedChannel path — Replace/Flag carry no tier-band magnitude (bands.v1.json)";
        return false;
    }
}
