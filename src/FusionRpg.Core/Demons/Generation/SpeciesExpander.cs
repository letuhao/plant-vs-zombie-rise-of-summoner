using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Demons.Generation;

/// <summary>
/// `species-generator` (T4.4, `spec-species-generator.md`, demon-seed module 12, build order 12 of
/// 16). Expands one enum-only anchor into a concrete species — every magnitude, every interval,
/// derived from the SAME shared functions the player's own stat system reads, never a private
/// `f(level)`. <b>Model calls: none, ever</b> — this module is the entire reason no model ever picks
/// a number.
///
/// <para><b>Numeric rules, non-negotiable</b> (CLAUDE.md, binding harder here than anywhere else in
/// the program): <c>long</c> for every magnitude, never <c>float</c>; widen before multiplying;
/// divide by 1000 last, exactly once; overflow throws, never clamps.
/// <see cref="AptitudeReadFunctions.Magnitude"/> already honours all four — calling it is how this
/// module complies, reimplementing any part of it is how it stops complying.</para>
/// </summary>
public static class SpeciesExpander
{
    /// <summary>
    /// Expand one anchor. <paramref name="statedIntervalMs"/> is `power-parse`'s own extracted
    /// interval when one exists (spec §4: a stated interval always wins over the classified
    /// `attackTempo` table) — null when none was extracted for this species.
    /// </summary>
    public static ConcreteSpecies Expand(
        AnchorRow anchor,
        AptitudeTuning aptitudeTuning,
        PowerTuning powerTuning,
        DemonShapeTuning shapeTuning,
        DemonThreatTuning threatTuning,
        long? statedIntervalMs = null)
    {
        if (anchor is null) throw new ArgumentNullException(nameof(anchor));

        if (!DemonRarityIds.TryParse(anchor.Rarity, out var rarity))
            throw new InvalidOperationException(
                $"'{anchor.SpeciesId}': rarity '{anchor.Rarity}' is not a known DemonRarity");

        // ---- theta: species' own base + the threat rung's offset, additive, before P(Theta) -------
        var thetaOffset = threatTuning.OffsetFor(anchor.ThreatBand);
        var theta = checked(shapeTuning.SpeciesBaseTheta + thetaOffset);
        var pTheta = new PowerLadder(powerTuning).Value(theta); // the one ladder — PS-3

        // ---- allocation share: pure = 100% primary; impure splits per demon-shape's own dial -------
        var hasSecondary = !anchor.Pure && anchor.AptitudeSecondary is not null;
        var primaryShareMilli = hasSecondary ? 1000L - shapeTuning.ImpureSecondaryShareMilli : 1000L;
        var secondaryShareMilli = hasSecondary ? shapeTuning.ImpureSecondaryShareMilli : 0L;

        // ---- every Magnitude-mode edge either aptitude reaches, read off the SAME pTheta ------------
        var shareExponentMilli = aptitudeTuning.Read.Magnitude.ShareExponentMilli;
        var magnitudes = new Dictionary<string, long>(StringComparer.Ordinal);

        void ApplyAptitude(string family, long shareMilli)
        {
            if (shareMilli <= 0) return;
            var share = shareMilli / 1000.0;

            foreach (var edge in aptitudeTuning.Edges)
            {
                if (!string.Equals(edge.Source, family, StringComparison.Ordinal)) continue;
                if (edge.Mode != AptitudeReadMode.Magnitude) continue; // Contest is a bounded point value, not a magnitude

                var value = AptitudeReadFunctions.Magnitude(edge.KMilli, share, shareExponentMilli, pTheta);
                // A channel BOTH aptitudes reach sums their contributions — never overwrites — the
                // same reason two atoms in one container add rather than replace.
                magnitudes[edge.Channel] = checked(magnitudes.GetValueOrDefault(edge.Channel) + value);
            }
        }

        ApplyAptitude(anchor.AptitudePrimary, primaryShareMilli);
        if (anchor.AptitudeSecondary is { } secondary) ApplyAptitude(secondary, secondaryShareMilli);

        // ---- tempo: a stated interval always wins (spec §4) -----------------------------------------
        var (intervalMs, intervalSource) = statedIntervalMs is { } stated
            ? (stated, "stated")
            : (LookupOrThrow(shapeTuning.AttackTempoIntervalMs, anchor.AttackTempo, anchor.SpeciesId, "attackTempo"), "classified");

        var rangeCells = LookupOrThrow(shapeTuning.ReachRangeCells, anchor.Reach, anchor.SpeciesId, "reach");

        // ---- catalog-runtime pass-through: copied from the anchor, never derived --------------------
        if (!ElementRoster.TryParse(anchor.ElementPrimary, out var elementPrimary))
            throw new InvalidOperationException(
                $"'{anchor.SpeciesId}': elementPrimary '{anchor.ElementPrimary}' is not a known element");
        ElementTypeId? elementSecondary = null;
        if (anchor.ElementSecondary is { } secEl)
        {
            if (!ElementRoster.TryParse(secEl, out var parsedSec))
                throw new InvalidOperationException(
                    $"'{anchor.SpeciesId}': elementSecondary '{secEl}' is not a known element");
            elementSecondary = parsedSec;
        }
        if (!Enum.TryParse<DemonDeployMode>(anchor.DeployMode, ignoreCase: false, out var deployMode))
            throw new InvalidOperationException(
                $"'{anchor.SpeciesId}': deployMode '{anchor.DeployMode}' is not a known DemonDeployMode");
        var acquisition = DemonAcquisition.None;
        foreach (var flag in anchor.Acquisition)
        {
            if (!Enum.TryParse<DemonAcquisition>(flag, ignoreCase: false, out var parsedFlag))
                throw new InvalidOperationException(
                    $"'{anchor.SpeciesId}': acquisition '{flag}' is not a known DemonAcquisition");
            acquisition |= parsedFlag;
        }

        return new ConcreteSpecies
        {
            SpeciesId = anchor.SpeciesId,
            Rarity = rarity,
            Theta = theta,
            PTheta = pTheta,
            AttackIntervalMs = intervalMs,
            AttackIntervalSource = intervalSource,
            RangeCells = rangeCells,
            VariantCount = anchor.Variants.Count,
            Magnitudes = magnitudes,
            Side = anchor.Side,
            GameTypeId = anchor.GameTypeId,
            ElementPrimary = elementPrimary,
            ElementSecondary = elementSecondary,
            DeployMode = deployMode,
            Acquisition = acquisition,
            Variants = anchor.Variants,
            TraitPool = anchor.Traits,
        };
    }

    static long LookupOrThrow(IReadOnlyDictionary<string, long> table, string key, string speciesId, string field)
    {
        if (table.TryGetValue(key, out var v)) return v;
        throw new InvalidOperationException($"'{speciesId}': {field} '{key}' has no entry in demon-shape.v1.json");
    }
}
