namespace FusionRpg.Core.World.Intel;

/// <summary>
/// One tier of "how big is that force". <see cref="Midpoint"/> and <see cref="Ceiling"/> are the two
/// readings a decision takes: the ceiling when being wrong is fatal, the midpoint when it is merely
/// expensive.
/// </summary>
public sealed record StrengthBand
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public long Floor { get; init; }

    /// <summary>Inclusive. The open-ended top band still names a number, because a plan needs one.</summary>
    public long Ceiling { get; init; }

    public long Midpoint { get; init; }
}

/// <summary>
/// What a glimpse is allowed to say about a force (spec-world-intel.md §Strength bands).
///
/// Never an exact roster — that would make fog cosmetic — and never a bare "something is there",
/// which gives neither the player nor the AI anything to decide with. A band gives enough to act on
/// and enough room to be wrong, which is the entire point of fog. It also needs no RNG, so it costs
/// nothing in determinism.
///
/// Bands are content, validated at bootstrap like every other catalog, so a table with a hole in it
/// is a startup error rather than a force that silently reads as nothing.
/// </summary>
public static class StrengthBandCatalog
{
    static IReadOnlyList<StrengthBand>? _all;

    public static IReadOnlyList<StrengthBand> All => _all ??= Validate(Seed);

    static readonly IReadOnlyList<StrengthBand> Seed = new StrengthBand[]
    {
        new() { Index = 0, Name = "empty",    Floor = 0,      Ceiling = 0,      Midpoint = 0 },
        new() { Index = 1, Name = "skirmish", Floor = 1,      Ceiling = 499,    Midpoint = 250 },
        new() { Index = 2, Name = "warband",  Floor = 500,    Ceiling = 1_499,  Midpoint = 1_000 },
        new() { Index = 3, Name = "host",     Floor = 1_500,  Ceiling = 3_999,  Midpoint = 2_750 },
        new() { Index = 4, Name = "legion",   Floor = 4_000,  Ceiling = 9_999,  Midpoint = 7_000 },
        // Open-ended: anything this large is simply "more than you want to meet", and the ceiling is
        // twice the floor so a defender still has a number to plan against.
        new() { Index = 5, Name = "horde",    Floor = 10_000, Ceiling = 20_000, Midpoint = 20_000 }
    };

    public static StrengthBand Of(long strength)
    {
        if (strength <= 0) return All[0];

        foreach (var band in All)
            if (strength <= band.Ceiling)
                return band;

        return All[^1];   // past the top band's nominal ceiling is still the top band
    }

    public static StrengthBand ByIndex(int index) =>
        index >= 0 && index < All.Count
            ? All[index]
            : throw new ArgumentOutOfRangeException(nameof(index), index, "No such strength band.");

    /// <summary>Catalog discipline — a bad band table is a startup error, never a runtime surprise.</summary>
    public static IReadOnlyList<StrengthBand> Validate(IReadOnlyList<StrengthBand> bands)
    {
        if (bands.Count == 0)
            throw new InvalidOperationException("The strength band table is empty.");
        if (bands[0].Floor != 0)
            throw new InvalidOperationException($"The first strength band must start at 0; it starts at {bands[0].Floor}.");

        for (var i = 0; i < bands.Count; i++)
        {
            var band = bands[i];

            if (string.IsNullOrWhiteSpace(band.Name))
                throw new InvalidOperationException($"Strength band {i} has no name.");
            if (band.Index != i)
                throw new InvalidOperationException($"Strength band '{band.Name}' is at position {i} but claims index {band.Index}.");
            if (band.Ceiling < band.Floor)
                throw new InvalidOperationException($"Strength band '{band.Name}' ends ({band.Ceiling}) before it starts ({band.Floor}).");
            if (band.Midpoint < band.Floor || band.Midpoint > band.Ceiling)
                throw new InvalidOperationException(
                    $"Strength band '{band.Name}' has a midpoint {band.Midpoint} outside its own range {band.Floor}..{band.Ceiling}.");

            // No gaps: a strength that falls between two bands would have no band at all, and the
            // lookup would quietly return the top one.
            if (i > 0 && band.Floor != bands[i - 1].Ceiling + 1)
                throw new InvalidOperationException(
                    $"Strength band '{band.Name}' starts at {band.Floor}, leaving a hole after '{bands[i - 1].Name}' which ends at {bands[i - 1].Ceiling}.");
        }

        return bands;
    }
}
