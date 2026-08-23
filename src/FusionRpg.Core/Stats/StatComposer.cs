namespace FusionRpg.Core.Stats;

public interface IComposeStrategy
{
    double ComposeChannel(double baseline, IEnumerable<StatModifier> channelMods);
}

/// <summary>
/// Flat → Increased(sum) → More(product) → Override(highest priority).
/// Override replaces the entire channel result (no leftover Flat).
/// </summary>
public sealed class PhasedComposeStrategy : IComposeStrategy
{
    public double ComposeChannel(double baseline, IEnumerable<StatModifier> channelMods)
    {
        var list = channelMods.ToList();
        var overrides = list.Where(m => m.Op == ModifierOp.Override)
            .OrderByDescending(m => m.Priority)
            .ThenBy(m => m.SourceId, StringComparer.Ordinal)
            .ToList();
        if (overrides.Count > 0)
            return overrides[0].Value;

        var flat = list.Where(m => m.Op == ModifierOp.Flat).Sum(m => m.Value);
        var increased = list.Where(m => m.Op == ModifierOp.Increased).Sum(m => m.Value);
        var more = list.Where(m => m.Op == ModifierOp.More).ToList();

        var afterFlat = baseline + flat;
        var afterInc = afterFlat * (1.0 + increased);
        var afterMore = afterInc;
        foreach (var m in more)
            afterMore *= 1.0 + m.Value;
        return afterMore;
    }
}

public sealed class StatComposer
{
    readonly IComposeStrategy _strategy;

    public StatComposer(IComposeStrategy? strategy = null) =>
        _strategy = strategy ?? new PhasedComposeStrategy();

    public EntityFinal Compose(EntityBaseline baseline, IModifierBagReader bag, bool applyStats)
    {
        var all = bag.All;
        if (!applyStats)
        {
            // Scales off: still honor Override mods (Tab B absolute). Ignore Flat/Increased/More.
            var overrides = all.Where(m => m.Op == ModifierOp.Override).ToList();
            if (overrides.Count == 0)
            {
                return new EntityFinal
                {
                    Hp = Math.Max(1L, baseline.Hp),
                    MaxHp = Math.Max(1L, baseline.MaxHp),
                    Atk = Math.Max(1L, baseline.Atk),
                    Arm1 = Math.Max(0L, baseline.Arm1),
                    Arm1Max = Math.Max(0L, baseline.Arm1Max),
                    Arm2 = Math.Max(0L, baseline.Arm2),
                    Arm2Max = Math.Max(0L, baseline.Arm2Max),
                    DefensePercent = 1f,
                    DefenseFlat = 0,
                    AttackInterval = baseline.AttackInterval,
                    ProduceInterval = baseline.ProduceInterval,
                    ZombieSpeed = baseline.ZombieSpeed,
                    Contributions = all
                };
            }

            all = overrides;
        }

        long ChanHp(string channel, long y0, long min = 1) =>
            Math.Max(min, (long)Math.Round(_strategy.ComposeChannel(y0, all.Where(m => m.Channel == channel))));

        long Chan(string channel, long y0, long min = 1) =>
            Math.Max(min, (long)Math.Round(_strategy.ComposeChannel(y0, all.Where(m => m.Channel == channel))));

        double Real(string channel, double y0, double min)
        {
            // A zero baseline means the entity has no such stat — a zombie has no produce interval.
            // Composing modifiers onto it would invent one out of nothing.
            if (y0 <= 0) return 0;
            return Math.Max(min, _strategy.ComposeChannel(y0, all.Where(m => m.Channel == channel)));
        }

        // Floored above zero, not at it. `More` −100% on an interval is ordinary content, and the
        // result is a divide-by-zero or an infinite fire rate depending on which call site reads it.
        double Interval(string channel, double y0) =>
            Real(channel, y0, StatChannels.MinimumInterval);

        ComposeDefense(all, out var defPct, out var defFlat);

        return new EntityFinal
        {
            Hp = ChanHp(StatChannels.Hp, baseline.Hp),
            MaxHp = ChanHp(StatChannels.MaxHp, baseline.MaxHp),
            Atk = Chan(StatChannels.Atk, baseline.Atk),
            Arm1 = Chan(StatChannels.Arm1, baseline.Arm1, min: 0),
            Arm1Max = Chan(StatChannels.Arm1Max, baseline.Arm1Max, min: 0),
            Arm2 = Chan(StatChannels.Arm2, baseline.Arm2, min: 0),
            Arm2Max = Chan(StatChannels.Arm2Max, baseline.Arm2Max, min: 0),
            DefensePercent = defPct,
            DefenseFlat = defFlat,
            AttackInterval = Interval(StatChannels.AttackInterval, baseline.AttackInterval),
            ProduceInterval = Interval(StatChannels.ProduceInterval, baseline.ProduceInterval),
            ZombieSpeed = Real(StatChannels.ZombieSpeed, baseline.ZombieSpeed, min: 0),
            Contributions = bag.All
        };
    }

    /// <summary>
    /// DEF for ScaleIncoming: percent from Increased/More/Override on baseline 1 via strategy;
    /// Flat summed separately. Override replaces the whole defense view (percent = value, flat = 0).
    /// </summary>
    void ComposeDefense(IReadOnlyList<StatModifier> all, out float defensePercent, out long defenseFlat)
    {
        var def = all.Where(m => m.Channel == StatChannels.Defense).ToList();
        var ov = def.Where(m => m.Op == ModifierOp.Override)
            .OrderByDescending(m => m.Priority)
            .ThenBy(m => m.SourceId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (ov != null)
        {
            defensePercent = (float)Math.Max(0.0001, ov.Value);
            defenseFlat = 0;
            return;
        }

        defenseFlat = (long)Math.Round(def.Where(m => m.Op == ModifierOp.Flat).Sum(m => m.Value));
        var pctMods = def.Where(m => m.Op is ModifierOp.Increased or ModifierOp.More);
        var pct = _strategy.ComposeChannel(1.0, pctMods);
        defensePercent = (float)Math.Max(0.0001, pct);
    }
}
