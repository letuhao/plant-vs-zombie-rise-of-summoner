namespace FusionRpg.Core.Stats.Aptitudes;

/// <summary>
/// `demon-type-allocation` (module 5) — makes <see cref="AllocationScope.DemonType"/> real
/// (spec-demon-type-allocation.md). Pure math only: the DB-facing compose-at-read entry point lives in
/// `RpgStore.Aptitudes.cs` (`EffectiveSpeciesAllocation`), which calls <see cref="Baseline"/> here —
/// this type never touches a store, matching `SpeciesBuildPlanner`'s own Core-only discipline.
/// </summary>
public static class SpeciesAllocation
{
    /// <summary>The one place the DemonType `scope_key` is encoded (spec's own "one place beside the
    /// Commander encoding" rule) — mirrors `AptitudeEndpoints.ScopeKey(playerId)`'s
    /// <c>"player:{id}"</c> shape, extended with the species so two players (decision 10) or two
    /// species never collide.</summary>
    public static string ScopeKey(long playerId, string speciesId) => $"player:{playerId}:species:{speciesId}";

    /// <summary>
    /// The baseline — computed, never persisted (audit finding A9): the plan's share vector (permille,
    /// summing to 1000) scaled by the DemonType budget at this species level. **Zero shares →
    /// <see cref="AptitudeAllocation.Empty"/>, zero budget → <see cref="AptitudeAllocation.Empty"/>**
    /// (never a thrown error) — a species missing from the plan, or a never-levelled species
    /// (`speciesLevel &lt;= 1` ⇒ `PointBudget.DemonTypeSourceFromLevel` = 0 ⇒ budget = 0), both
    /// legitimately have no baseline yet. Widened before multiplying, largest-remainder rounding (same
    /// rule `SpeciesBuildPlanner` uses) so the twelve shares' points sum to exactly the budget rather
    /// than losing a few points to integer-division truncation on every read.
    /// </summary>
    public static AptitudeAllocation Baseline(
        IReadOnlyDictionary<string, long> planSharePermille, long speciesLevel, AptitudeTuning tuning)
    {
        if (planSharePermille is null) throw new ArgumentNullException(nameof(planSharePermille));
        if (tuning is null) throw new ArgumentNullException(nameof(tuning));
        if (planSharePermille.Count == 0) return AptitudeAllocation.Empty;

        var source = PointBudget.DemonTypeSourceFromLevel(speciesLevel);
        var budget = PointBudget.PointsFor(AllocationScope.DemonType, source, tuning);
        if (budget == 0) return AptitudeAllocation.Empty;

        var baseShares = new Dictionary<string, long>(StringComparer.Ordinal);
        var remainders = new Dictionary<string, long>(StringComparer.Ordinal);
        long allocated = 0;
        foreach (var (aptId, sharePermille) in planSharePermille)
        {
            if (!AptitudeCatalog.IsAptitudeId(aptId))
                throw new ArgumentException($"plan share names unknown aptitude id '{aptId}'", nameof(planSharePermille));
            long product;
            checked { product = budget * sharePermille; }
            var baseShare = product / 1000;
            baseShares[aptId] = baseShare;
            remainders[aptId] = product % 1000;
            checked { allocated += baseShare; }
        }

        var leftover = budget - allocated; // always in [0, planSharePermille.Count) by construction
        var allocation = AptitudeAllocation.Empty;
        foreach (var aptId in remainders.Keys
                     .OrderByDescending(id => remainders[id])
                     .ThenBy(id => id, StringComparer.Ordinal))
        {
            var points = baseShares[aptId];
            if (leftover > 0) { points++; leftover--; }
            if (points > 0)
                allocation += AptitudeAllocation.Single(AllocationScope.DemonType, aptId, points);
        }
        return allocation;
    }
}
