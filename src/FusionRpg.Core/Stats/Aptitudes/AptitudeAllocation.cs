namespace FusionRpg.Core.Stats.Aptitudes;

/// <summary>class-system-map.md §2aa — the four scopes an actor's allocation is the SUM of, commander
/// smallest through unique largest (a commander allocation replicates across the whole roster, so a
/// dominant one is the worst case). The relative BUDGET each scope gets is `point-economy`'s call
/// (P6.1); this type only needs the four buckets to sum into, in the order the decision states them.
/// Append-only, like every other ordinal roster in this codebase — never reorder.</summary>
public enum AllocationScope { Commander, DemonType, Aspect, UniqueDemon }

/// <summary>
/// An actor's aptitude allocation — immutable, `long`-valued points per (scope, aptitude id).
///
/// <para><b>Scopes sum before share, never the reverse</b> (decisions.md "Class system" row):
/// <see cref="Total"/> adds the four scopes for one aptitude; <see cref="Share"/> divides that sum by
/// the grand total across all twelve. A per-scope share, later combined, is a different (and wrong)
/// number — it would let a small scope's 100%-in-one-aptitude allocation outweigh a large scope's
/// broad spread.</para>
///
/// <para><b>Empty means all-zero shares, never <c>1/12</c> each.</b> An actor with no allocation has
/// no build; treating "nothing chosen" as "chose evenly" would silently invent a default nobody set
/// (tunables-ssot.md's own rule against exactly that, applied here to a runtime value rather than a
/// config key).</para>
///
/// <para><b>No aptitude cap and no respec cap</b> (PS-8, AGENTS.md) — nothing here bounds
/// <see cref="Total"/> or <see cref="GrandTotal"/>; overflow throws via checked arithmetic rather than
/// clamping, so a build that would silently overflow fails loudly instead of quietly capping.</para>
/// </summary>
public sealed class AptitudeAllocation
{
    readonly IReadOnlyDictionary<(AllocationScope Scope, string AptitudeId), long> _points;

    public static readonly AptitudeAllocation Empty = new(new Dictionary<(AllocationScope, string), long>());

    AptitudeAllocation(IReadOnlyDictionary<(AllocationScope, string), long> points) => _points = points;

    public static AptitudeAllocation Single(AllocationScope scope, string aptitudeId, long points)
    {
        if (!AptitudeCatalog.IsAptitudeId(aptitudeId))
            throw new ArgumentException($"unknown aptitude id '{aptitudeId}'", nameof(aptitudeId));
        if (points < 0)
            throw new ArgumentOutOfRangeException(nameof(points), "allocation points cannot be negative");
        return points == 0
            ? Empty
            : new AptitudeAllocation(new Dictionary<(AllocationScope, string), long> { [(scope, aptitudeId)] = points });
    }

    public long PointsAt(AllocationScope scope, string aptitudeId) => _points.GetValueOrDefault((scope, aptitudeId));

    /// <summary>The sum across all four scopes for one aptitude — the quantity <see cref="Share"/>
    /// is taken over, per the decision that scopes sum before share.</summary>
    public long Total(string aptitudeId)
    {
        long sum = 0;
        foreach (var scope in AllScopes)
            checked { sum += PointsAt(scope, aptitudeId); }
        return sum;
    }

    public long GrandTotal()
    {
        long sum = 0;
        foreach (var apt in AptitudeCatalog.All)
            checked { sum += Total(apt.Id); }
        return sum;
    }

    /// <summary>The orthogonal sum to <see cref="Total"/>: one scope, across all twelve aptitudes —
    /// what `point-economy` (P6.1) checks against that scope's own budget. "Each scope draws from its
    /// own budget" (spec-point-economy.md §7 test 2) starts here: this is scope-local spend, never
    /// combined with another scope's before the comparison.</summary>
    public long TotalForScope(AllocationScope scope)
    {
        long sum = 0;
        foreach (var apt in AptitudeCatalog.All)
            checked { sum += PointsAt(scope, apt.Id); }
        return sum;
    }

    /// <summary>Bounded [0,1] ratio — exempt from the long-magnitude rule (CLAUDE.md: "bounded ratios
    /// are exempt"). Empty allocation reads 0 for every aptitude, never a uniform 1/12.</summary>
    public double Share(string aptitudeId)
    {
        var grand = GrandTotal();
        return grand == 0 ? 0.0 : (double)Total(aptitudeId) / grand;
    }

    public IReadOnlyDictionary<string, double> Shares() =>
        AptitudeCatalog.All.ToDictionary(a => a.Id, a => Share(a.Id), StringComparer.Ordinal);

    /// <summary>Commutative and associative — <c>a + b == b + a</c> and grouping never matters, so
    /// merging allocations from independent scopes can happen in any order.</summary>
    public static AptitudeAllocation operator +(AptitudeAllocation a, AptitudeAllocation b)
    {
        var merged = new Dictionary<(AllocationScope, string), long>(a._points);
        foreach (var (key, points) in b._points)
        {
            var existing = merged.GetValueOrDefault(key);
            checked { merged[key] = existing + points; }
        }
        return merged.Count == 0 ? Empty : new AptitudeAllocation(merged);
    }

    static readonly AllocationScope[] AllScopes = Enum.GetValues<AllocationScope>();
}
