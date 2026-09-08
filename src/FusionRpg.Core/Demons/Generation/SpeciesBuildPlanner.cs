using FusionRpg.Core.Stats.Aptitudes;

namespace FusionRpg.Core.Demons.Generation;

/// <summary>
/// `redistribution-plan` (module 4) — turns each species' classified build favour
/// (<see cref="AnchorRow.AptitudePrimary"/>/<see cref="AnchorRow.AptitudeSecondary"/>) into a full
/// twelve-aptitude share vector, deterministically, in one closed-form pass (spec-redistribution-plan.md
/// "The algorithm — closed-form, not a search"). Pure and Core-only: no file IO, no `RpgStore`, no
/// model call ever — the tool (`tools/DemonBuildPlanGen`) reads anchors and writes the plan; this type
/// only computes.
/// </summary>
public static class SpeciesBuildPlanner
{
    public static SpeciesBuildResult Plan(IReadOnlyList<AnchorRow> species, SpeciesBuildTuning tuning)
    {
        if (species is null) throw new ArgumentNullException(nameof(species));
        if (tuning is null) throw new ArgumentNullException(nameof(tuning));
        if (species.Count == 0)
            throw new ArgumentException("species must not be empty", nameof(species));

        foreach (var s in species)
        {
            if (!AptitudeCatalog.IsAptitudeId(s.AptitudePrimary))
                throw new ArgumentException($"'{s.SpeciesId}': unknown primary aptitude '{s.AptitudePrimary}'");
            if (s.AptitudeSecondary is { } sec && !AptitudeCatalog.IsAptitudeId(sec))
                throw new ArgumentException($"'{s.SpeciesId}': unknown secondary aptitude '{sec}'");
        }

        var aptitudeIds = AptitudeCatalog.All
            .Select(a => a.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var speciesCount = species.Count;

        // Phase 1 — one lean per PRIMARY, from that primary's own corpus-wide crowding. Every species
        // sharing a primary shares its lean by construction (spec §"Phase 1").
        var primaryCounts = species
            .GroupBy(s => s.AptitudePrimary, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (long)g.Count(), StringComparer.Ordinal);

        var leanByPrimary = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var (primary, count) in primaryCounts)
        {
            long crowdingPermille;
            checked { crowdingPermille = count * 1000 / speciesCount; }
            long contribution;
            checked { contribution = tuning.CrowdingFactor * crowdingPermille / 1000; }
            var lean = Math.Clamp(
                tuning.LeanMaxPermille - contribution, tuning.LeanMinPermille, tuning.LeanMaxPermille);
            leanByPrimary[primary] = lean;
        }

        // Phase 2 — ordinal speciesId order; the running corpus total per aptitude is what "the
        // running corpus deficit" (spec §"Phase 2") is measured against, recomputed as the pass
        // proceeds so early species fill the rarest aptitudes and later ones spread wider.
        var runningTotal = aptitudeIds.ToDictionary(id => id, _ => 0L, StringComparer.Ordinal);
        var vectors = new List<SpeciesBuildVector>(speciesCount);
        long processed = 0;

        foreach (var s in species.OrderBy(s => s.SpeciesId, StringComparer.Ordinal))
        {
            processed++;
            var vector = new Dictionary<string, long>(StringComparer.Ordinal);
            var usedSlots = new HashSet<string>(StringComparer.Ordinal);

            var lean = leanByPrimary[s.AptitudePrimary];
            vector[s.AptitudePrimary] = lean;
            usedSlots.Add(s.AptitudePrimary);

            var remainder = 1000 - lean;
            var leftover = remainder;

            // `Pure` is the anchor's own authority on "no distinct secondary" — matching
            // `SpeciesExpander.Expand`'s identical `!anchor.Pure && anchor.AptitudeSecondary is not
            // null` check. Some real classified anchors set `pure: true` yet still echo the primary
            // back as `aptitudeSecondary` (verified: `HypnoCattailGirl`/`ObsidianWallNut`, both
            // primary==secondary=="Focus"/"Retribution") rather than the "none" sentinel — trusting
            // `AptitudeSecondary is not null` alone would silently overwrite `vector[primary]` with a
            // SMALLER secondary share (same dictionary key), corrupting the vector's sum below 1000.
            // The `!= s.AptitudePrimary` guard is defense in depth against the same corruption from
            // any other bad-data shape, not just this one observed on the real corpus.
            if (!s.Pure && s.AptitudeSecondary is { } secondary && secondary != s.AptitudePrimary)
            {
                long secondaryShare;
                checked { secondaryShare = remainder * tuning.SecondarySharePermille / 1000; }
                vector[secondary] = secondaryShare;
                usedSlots.Add(secondary);
                leftover = remainder - secondaryShare;
            }

            if (leftover > 0)
            {
                var remainingAptitudes = aptitudeIds.Length - usedSlots.Count;
                var wantSlots = Math.Max(1, tuning.MaxAptitudesPerSpecies - usedSlots.Count);
                var slots = Math.Min(wantSlots, remainingAptitudes);

                // Largest current deficit against the band floor, ordinal tiebreak (spec §"Phase 2").
                // A negative "deficit" (already past the floor for this many species processed) sorts
                // last, so an over-represented aptitude naturally stops absorbing further remainder.
                var candidates = aptitudeIds
                    .Where(id => !usedSlots.Contains(id))
                    .Select(id => (Id: id, Deficit: tuning.ParityFloorPermille * processed - runningTotal[id]))
                    .OrderByDescending(x => x.Deficit)
                    .ThenBy(x => x.Id, StringComparer.Ordinal)
                    .Take(slots)
                    .Select(x => x.Id)
                    .ToArray();

                // Largest-remainder split of `leftover` across the chosen slots: base share to every
                // slot, then one extra permille each to the first `extra` (already deficit-ordered) so
                // the sum is exactly `leftover` — never lost to integer-division truncation.
                var baseShare = leftover / candidates.Length;
                var extra = leftover % candidates.Length;
                for (var i = 0; i < candidates.Length; i++)
                {
                    var share = baseShare + (i < extra ? 1 : 0);
                    vector[candidates[i]] = share;
                    usedSlots.Add(candidates[i]);
                }
            }

            foreach (var (id, share) in vector)
                checked { runningTotal[id] += share; }

            vectors.Add(new SpeciesBuildVector(s.SpeciesId, vector));
        }

        // Phase 3 — verify, and refuse. Corpus share is the mean vector value per aptitude across the
        // corpus (decision 11: parity over TOTAL ALLOCATED POINTS — every vector already sums to 1000,
        // so this mean is exactly "this aptitude's share of the corpus' total points").
        var corpusShare = aptitudeIds.ToDictionary(
            id => id, id => runningTotal[id] / speciesCount, StringComparer.Ordinal);

        var offending = corpusShare
            .Where(kv => kv.Value < tuning.ParityFloorPermille || kv.Value > tuning.ParityCeilingPermille)
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        if (offending.Count > 0)
        {
            var detail = string.Join(", ", offending.Select(kv =>
                $"{kv.Key}={kv.Value}‰ (band [{tuning.ParityFloorPermille},{tuning.ParityCeilingPermille}]‰)"));
            throw new SpeciesBuildRefusal(
                $"species-build plan out of band for {offending.Count} aptitude(s): {detail}", offending);
        }

        return new SpeciesBuildResult(vectors, corpusShare);
    }
}
