namespace FusionRpg.Core.Effects.Atoms.Power;

/// <summary>
/// What an actor is worth, memoized (spec-power-vector.md, E9).
///
/// <para><b>It lives in E9, not E10, because the spawn recursion needs it to terminate.</b> An atom
/// that spawns a body prices that body's actor power; without memoization a chain of summoners
/// prices forever, and depth-1 truncation alone would still redo the same actor once per spawn atom
/// that names it.</para>
///
/// <para><b>Actor power aggregates channel totals and prices the composition</b> — it is not the sum
/// of per-atom prices (definitions §7, closing D2). Adding <c>+10 atk</c> twice is one
/// <c>+20 atk</c> actor, and pricing them separately would double-count a channel that composes
/// once.</para>
///
/// <para><b>Base stats contribute nothing.</b> That is what makes E10's "marginal on an empty actor
/// ≈ stored power" true, and it keeps actor power a measure of <i>what was granted</i> rather than of
/// the level curve.</para>
/// </summary>
public sealed class ActorPowerCache
{
    readonly Dictionary<string, PowerVector> _memo = new(StringComparer.Ordinal);
    readonly PowerTables _tables;

    public ActorPowerCache(PowerTables? tables = null) => _tables = tables ?? PowerTables.Current;

    /// <summary>How many times a price was actually computed — the memo, observable.</summary>
    public int Computations { get; private set; }

    public int Entries => _memo.Count;

    /// <summary>
    /// The actor's price, memoized on <c>(actor, catalog_revision, binding-set hash)</c>.
    ///
    /// <para>The catalog revision is in the key because the same atoms priced against a different
    /// catalog are a different answer, and the binding set is in it because that is what actually
    /// changed when an actor gains an item.</para>
    /// </summary>
    public PowerVector Of(string actorKey, long catalogRevision, IReadOnlyList<AtomRow> atoms)
    {
        var key = Key(actorKey, catalogRevision, atoms);
        if (_memo.TryGetValue(key, out var cached)) return cached;

        Computations++;
        var value = Compose(atoms, _tables);
        _memo[key] = value;
        return value;
    }

    /// <summary>
    /// Price a set of atoms as one actor: totals per channel first, then one price per channel.
    /// </summary>
    public static PowerVector Compose(IReadOnlyList<AtomRow> atoms, PowerTables? tables = null)
    {
        var t = tables ?? PowerTables.Current;
        var total = PowerVector.Zero;

        // Channel-writing kinds are aggregated; everything else is priced per atom, because two
        // status applications are two effects rather than one bigger one.
        var byChannel = new Dictionary<(string Kind, string Channel), int>();

        foreach (var atom in atoms)
        {
            var kind = AtomKindRegistry.Get(atom.KindId);
            if (kind is null) continue;

            var pars = CostFunction.Read(atom.ParamsJson);
            var channel = pars.TryGetValue("channel", out var chEl)
                          && chEl.ValueKind == System.Text.Json.JsonValueKind.String
                ? chEl.GetString() ?? ""
                : null;

            if (channel is null)
            {
                var priced = CostFunction.Price(atom, t);
                if (priced.Ok) total += priced.Power;
                continue;
            }

            var key = (atom.KindId, channel);
            // Absolute, for the same reason Price is: the sign is direction, not worth. Two atoms
            // writing opposite directions on one channel are two effects, not a cancellation.
            byChannel[key] = byChannel.GetValueOrDefault(key)
                             + Math.Abs(CostFunction.MeanMagnitude(atom, kind, pars));
        }

        foreach (var ((kindId, channel), magnitude) in byChannel)
        {
            var coeff = t.Find(kindId, channel);
            var kind = AtomKindRegistry.Get(kindId);
            if (coeff is null || kind is null) continue;

            var normalisedMilli =
                PowerMath.DivRound((long)magnitude * PowerMath.One, Math.Max(1, coeff.ReferenceScale));
            total += PowerVector.FromCategory(
                kind.Categories, PowerMath.MulMilli(normalisedMilli, coeff.CoeffMilli));
        }

        return total;
    }

    /// <summary>
    /// A spawned body, priced from the stats it is given.
    ///
    /// <para>Not "base stats are worth nothing" — that rule is about the <i>actor being measured</i>,
    /// not about a body an effect conjures. A 5000 hp summon is worth 5000 hp of survivability to
    /// whoever summoned it, and treating it as base would price the whole spawn at zero (D3).</para>
    /// </summary>
    public static PowerVector PriceBody(int hp, int atk, PowerTables? tables = null)
    {
        var t = tables ?? PowerTables.Current;
        var body = PowerVector.Zero;

        if (hp > 0 && t.Find("stat.modify", "maxHp") is { } hpCoeff)
            body = body.With(PowerCategory.Survivability,
                PowerMath.MulMilli(
                    PowerMath.DivRound((long)hp * PowerMath.One, Math.Max(1, hpCoeff.ReferenceScale)),
                    hpCoeff.CoeffMilli));

        if (atk > 0 && t.Find("stat.modify", "atk") is { } atkCoeff)
            body = body.With(PowerCategory.Offense,
                PowerMath.MulMilli(
                    PowerMath.DivRound((long)atk * PowerMath.One, Math.Max(1, atkCoeff.ReferenceScale)),
                    atkCoeff.CoeffMilli));

        return body;
    }

    static string Key(string actorKey, long revision, IReadOnlyList<AtomRow> atoms)
    {
        // Content-derived and order-independent: the same binding set reached in a different order is
        // the same actor, and a generated id in the key would defeat the memo entirely.
        var ids = atoms.Select(a => a.AtomId).OrderBy(a => a, StringComparer.Ordinal);
        return actorKey + "|" + revision + "|" + string.Join(",", ids);
    }
}
