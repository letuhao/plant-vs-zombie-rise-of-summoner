namespace FusionRpg.Core.Stats;

/// <summary>Immutable game baseline Y0 for one entity instance.</summary>
public sealed class EntityBaseline
{
    public long Hp { get; init; }
    public long MaxHp { get; init; }
    public long Atk { get; init; }
    public long Arm1 { get; init; }
    public long Arm1Max { get; init; }
    public long Arm2 { get; init; }
    public long Arm2Max { get; init; }

    /// <summary>
    /// E16's three, as the game reports them. Zero means "this entity has no such stat" — a zombie
    /// has no produce interval — and composing from a zero baseline yields zero, which the writer
    /// reads as "leave the field alone".
    /// </summary>
    public double AttackInterval { get; init; }
    public double ProduceInterval { get; init; }
    public double ZombieSpeed { get; init; }
}

/// <summary>Resolved final Y — write-only to the game/sim.</summary>
public sealed class EntityFinal
{
    public long Hp { get; init; }
    public long MaxHp { get; init; }
    public long Atk { get; init; }
    public long Arm1 { get; init; }
    public long Arm1Max { get; init; }
    public long Arm2 { get; init; }
    public long Arm2Max { get; init; }

    /// <summary>Composed defense view for ScaleIncoming (percent like legacy StatMod, flat).</summary>
    public float DefensePercent { get; init; } = 1f;
    public long DefenseFlat { get; init; }

    /// <summary>
    /// E16's three. Composed like any other channel, then floored: an interval of zero is a
    /// divide-by-zero or an infinite fire rate depending on where it is read.
    /// </summary>
    public double AttackInterval { get; init; }
    public double ProduceInterval { get; init; }
    public double ZombieSpeed { get; init; }

    public IReadOnlyList<StatModifier> Contributions { get; init; } = Array.Empty<StatModifier>();
}
