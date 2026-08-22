namespace FusionRpg.Core.Effects.Atoms.Power;

/// <summary>
/// One authored price coefficient. <paramref name="Channel"/> is empty for a kind priced the same
/// whatever it writes.
/// </summary>
/// <param name="CoeffMilli">Points per reference unit, per-mille.</param>
/// <param name="ReferenceScale">
/// What "one unit" means for this channel — the part that cannot be skipped. <c>+10 hp</c> is ten
/// hit points; <c>+10 fire power</c> is ten <i>resolver</i> points at 0.1 sigmoid units. A
/// coefficient table without normalisation prices those alike and is wrong by an order of magnitude.
/// </param>
public sealed record PowerCoefficientRow(
    string KindId, string Channel, int CoeffMilli, int ReferenceScale);

/// <summary>Expected fires per battle-minute for one trigger.</summary>
/// <remarks>
/// Data rather than a constant, deliberately: it is a balance number, the sweep must be able to
/// propose against it, and as a code constant it would move every golden with <b>no content-hash
/// change</b> — the one outcome E8 exists to prevent.
/// </remarks>
public sealed record TriggerFrequencyRow(string Trigger, int PerMinute);

/// <summary>
/// The coefficients and trigger frequencies a price is computed from.
///
/// <para><b>Authored values live in rows; a sweep writes proposals to a side table and never touches
/// these.</b> That is what makes "hand-authored now, fitted later" mechanically possible rather than
/// aspirational — humans decide what ships.</para>
/// </summary>
public sealed class PowerTables
{
    readonly Dictionary<(string Kind, string Channel), PowerCoefficientRow> _coefficients;
    readonly Dictionary<string, int> _frequency;

    public PowerTables(
        IReadOnlyList<PowerCoefficientRow> coefficients, IReadOnlyList<TriggerFrequencyRow> frequencies)
    {
        Coefficients = coefficients;
        Frequencies = frequencies;
        _coefficients = new Dictionary<(string, string), PowerCoefficientRow>();
        foreach (var c in coefficients) _coefficients[(c.KindId, c.Channel)] = c;
        _frequency = frequencies.ToDictionary(f => f.Trigger, f => f.PerMinute, StringComparer.Ordinal);
    }

    public IReadOnlyList<PowerCoefficientRow> Coefficients { get; }
    public IReadOnlyList<TriggerFrequencyRow> Frequencies { get; }

    /// <summary>
    /// The row for a kind and channel, falling back to the kind's channel-less row.
    ///
    /// <para>Null when neither exists — and the caller must treat that as <b>unpriced</b>, not as
    /// zero. A missing coefficient silently pricing at zero is how a whole family becomes free.</para>
    /// </summary>
    public PowerCoefficientRow? Find(string kindId, string? channel)
    {
        if (!string.IsNullOrEmpty(channel)
            && _coefficients.TryGetValue((kindId, channel!), out var exact))
            return exact;

        return _coefficients.TryGetValue((kindId, ""), out var generic) ? generic : null;
    }

    /// <summary>Fires per battle-minute. Zero for an unlisted trigger, which the ICD factor handles.</summary>
    public int FrequencyOf(string? trigger) =>
        trigger is not null && _frequency.TryGetValue(trigger, out var f) ? f : 0;

    // ---- the authored defaults --------------------------------------------------------------------

    /// <summary>
    /// The hand-authored starting values, which a host with no database runs on.
    ///
    /// <para>Calibration behind the combat numbers: <c>critical-hunter</c> grants +150 crit-rate
    /// points and moves crit from ~7.6% to ~26.9%, and the patron aura divides ‰ by ten, so its 150‰
    /// clamp is +15 points. Those two facts are why a resolver point is worth roughly ten times a hit
    /// point here, and why the reference scales differ by an order of magnitude rather than by a
    /// rounding.</para>
    /// </summary>
    public static PowerTables Authored()
    {
        var coefficients = new List<PowerCoefficientRow>
        {
            // Primary stat channels: reference scale is the raw stat unit.
            new("stat.modify", "hp", 1000, 10),
            new("stat.modify", "maxHp", 1000, 10),
            new("stat.modify", "atk", 1000, 2),
            new("stat.modify", "defense", 1000, 2),
            new("stat.modify", "arm1", 1000, 10),
            new("stat.modify", "arm1Max", 1000, 10),
            new("stat.modify", "arm2", 1000, 10),
            new("stat.modify", "arm2Max", 1000, 10),
            new("stat.modify", "", 1000, 10),

            // Derived combat channels are resolver points: ten times denser than a hit point.
            new("stat.derived", "", 1000, 1),

            new("resource.delta", "", 1000, 10),
            new("resource.economy", "", 1000, 25),
            new("status.apply", "", 1000, 1),
            new("status.clear", "", 1000, 1),
            new("shield.grant", "", 1000, 10),
            new("spawn.entity", "", 1000, 1),
            new("board.action", "", 1000, 1),
            new("grid.spawn", "", 1000, 1),
            new("grid.clear", "", 1000, 1),
            new("box.set", "", 1000, 1),
        };

        var frequencies = new List<TriggerFrequencyRow>
        {
            new(AtomTriggers.OnDamageDealt, 60),
            new(AtomTriggers.OnDamageTaken, 40),
            new(AtomTriggers.OnSpawn, 4),
            new(AtomTriggers.OnDeath, 6),
            new(AtomTriggers.OnTimer, 12),
        };

        return new PowerTables(coefficients, frequencies);
    }

    static PowerTables _current = Authored();
    static readonly AsyncLocal<PowerTables?> Scoped = new();

    /// <summary>What this context prices with. Same shape as the element roster, for the same reason.</summary>
    public static PowerTables Current => Scoped.Value ?? _current;

    public static void Use(PowerTables tables) =>
        _current = tables ?? throw new ArgumentNullException(nameof(tables));

    public static void ResetToAuthored() => _current = Authored();

    /// <summary>Swap for this async context only, so a test cannot disturb one running beside it.</summary>
    public static IDisposable UseScoped(PowerTables tables)
    {
        if (tables is null) throw new ArgumentNullException(nameof(tables));
        var previous = Scoped.Value;
        Scoped.Value = tables;
        return new Restore(previous);
    }

    sealed class Restore : IDisposable
    {
        readonly PowerTables? _previous;
        public Restore(PowerTables? previous) => _previous = previous;
        public void Dispose() => Scoped.Value = _previous;
    }
}
