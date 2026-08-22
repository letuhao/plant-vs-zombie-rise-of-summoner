using FusionRpg.Core.Demons;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.World.Movement;

/// <summary>How far a legion can march in one turn before anything modifies it.</summary>
public static class MovementPolicy
{
    public const string March = "march";
    public const string Scout = "scout";
    public const string Hold = "hold";

    public static readonly IReadOnlyList<string> Stances = new[] { March, Scout, Hold };

    public static bool IsKnownStance(string? stance) =>
        stance != null && Stances.Contains(stance, StringComparer.Ordinal);

    /// <summary>
    /// A turn's march budget, in the same per-mille units lanes are measured in. Uniform in wave 1:
    /// specimens carry no speed yet, so "set by the slowest member" has nothing to read. When they
    /// do, this becomes the base and the legion's slowest member modifies it.
    /// </summary>
    public const int PointsPerTurn = 1000;

    /// <summary>Half a march, for twice the sight (world-intel). Total War prices sight the same way.</summary>
    public const int ScoutPointsPerTurn = PointsPerTurn / 2;

    /// <summary>
    /// What a garrison recovers each turn, per member, as a fraction of its health — and only in
    /// supply. Against attrition's 50‰ that is roughly seven turns from near-death to whole.
    ///
    /// This is the only healing in the game. Before it, wounds accumulated from battle and from
    /// starvation and nothing ever removed them, so every legion was on a one-way trip.
    /// </summary>
    public const int RecoveryMilli = 150;

    /// <summary>A turn's march budget for a given posture. Holding gives it up entirely.</summary>
    public static int BudgetFor(string stance) => stance switch
    {
        Hold => 0,
        Scout => ScoutPointsPerTurn,
        _ => PointsPerTurn
    };
}

/// <summary>
/// A legion's banner: the element most of it carries. Computed from the members' species every time
/// it is needed and never stored, so it cannot drift away from the roster that produced it.
/// </summary>
public static class BannerElement
{
    public static ElementTypeId? Of(WorldEntity entity)
    {
        var counts = new Dictionary<ElementTypeId, int>();
        foreach (var member in entity.Members)
        {
            if (!DemonSpeciesCatalog.IsKnown(member.SpeciesId)) continue;
            var element = DemonSpeciesCatalog.Get(member.SpeciesId).ElementPrimary;
            counts[element] = counts.TryGetValue(element, out var n) ? n + 1 : 1;
        }

        if (counts.Count == 0) return null;

        // Ties break by the ring's declared order, never by member order — otherwise shuffling a
        // legion's roster would silently change what it costs to march.
        ElementTypeId? best = null;
        var bestCount = 0;
        foreach (var element in Enum.GetValues<ElementTypeId>())
        {
            if (!counts.TryGetValue(element, out var count) || count <= bestCount) continue;
            best = element;
            bestCount = count;
        }

        return best;
    }
}

/// <summary>
/// What one lane costs to march. Integer per-mille throughout: `length × type × hazard`, with the
/// ley discount for a matching banner.
///
/// A ley lane's element is not stored on the lane — it is the climate of the sectors it joins, so a
/// ley lane rewards the banner that belongs to the ground at either end. Matching *either* end
/// keeps the cost symmetric, which is what lets two legions crossing in opposite directions agree
/// on where they meet.
/// </summary>
public static class LaneCost
{
    /// <summary>A matched banner marches a ley lane at this fraction of the cost.</summary>
    public const int LeyDiscountMilli = 800;

    /// <summary>
    /// The truth-side reading: climates come from the world, so every ley discount that applies is
    /// found.
    /// </summary>
    public static int For(WorldState world, WorldLane lane, ElementTypeId? bannerElement) =>
        For(lane, bannerElement, sectorId => ClimateOf(world, sectorId));

    static ElementTypeId? ClimateOf(WorldState world, string sectorId)
    {
        foreach (var sector in world.Sectors)
            if (string.Equals(sector.SectorId, sectorId, StringComparison.Ordinal))
                return sector.Climate;

        return null;
    }

    /// <summary>
    /// What a lane costs, given a way to look up a sector's climate.
    ///
    /// The lookup is a parameter rather than the world because the *believed* answer differs: a
    /// faction that has never scouted a ley lane's endpoints does not know its climate, so the
    /// discount does not apply and the march is over-priced. That is fog reaching into route
    /// planning, and it is the behaviour we want — an army plans with what it knows.
    /// </summary>
    public static int For(WorldLane lane, ElementTypeId? bannerElement, Func<string, ElementTypeId?> climateOf)
    {
        var type = LaneTypeCatalog.Get(lane.TypeId);

        long cost = Math.Max(1, lane.Length);
        cost = cost * type.CostMultiplierMilli / 1000;
        cost = cost * (1000 + Math.Max(0, lane.HazardMilli)) / 1000;

        if (type.Ley && bannerElement is { } banner
            && (climateOf(lane.FromSectorId) == banner || climateOf(lane.ToSectorId) == banner))
            cost = cost * LeyDiscountMilli / 1000;

        return (int)Math.Max(1, Math.Min(int.MaxValue, cost));
    }
}
