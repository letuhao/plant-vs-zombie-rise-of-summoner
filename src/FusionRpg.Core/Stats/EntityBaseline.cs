namespace FusionRpg.Core.Stats;

/// <summary>Immutable game baseline Y0 for one entity instance.</summary>
public sealed class EntityBaseline
{
    public long Hp { get; init; }
    public long MaxHp { get; init; }
    public int Atk { get; init; }
    public int Arm1 { get; init; }
    public int Arm1Max { get; init; }
    public int Arm2 { get; init; }
    public int Arm2Max { get; init; }
}

/// <summary>Resolved final Y — write-only to the game/sim.</summary>
public sealed class EntityFinal
{
    public long Hp { get; init; }
    public long MaxHp { get; init; }
    public int Atk { get; init; }
    public int Arm1 { get; init; }
    public int Arm1Max { get; init; }
    public int Arm2 { get; init; }
    public int Arm2Max { get; init; }

    /// <summary>Composed defense view for ScaleIncoming (percent like legacy StatMod, flat).</summary>
    public float DefensePercent { get; init; } = 1f;
    public int DefenseFlat { get; init; }

    public IReadOnlyList<StatModifier> Contributions { get; init; } = Array.Empty<StatModifier>();
}
