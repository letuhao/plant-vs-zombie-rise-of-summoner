using FusionRpg.Core.Battle;
using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Stats;

namespace FusionRpg.Core.Stats.Derived.Subsystems;

/// <summary>
/// battle-hub-fuse T5 — Hub twin of <c>BattleStatComposer.AddAffinity</c>: the actor's own element
/// channels carry Atk/Defense shares by the same divisors. Re-home only, including the integer
/// division (a <c>long / int</c> computed as <c>long</c> first, exactly like the composer).
///
/// <para>Reads the same <c>BattleStatComposer</c> tuning statics the composer reads; T6 relocates
/// them with the composer's deletion (tracked follow-up). Order 100 — FlatSum-commutative.</para>
/// </summary>
public sealed class BattleAffinitySubsystem : IActorStatSubsystem
{
    readonly Func<StatContext, (long Atk, long Defense, ElementTypeId? Primary, ElementTypeId? Secondary)> _affinity;

    public BattleAffinitySubsystem(
        Func<StatContext, (long Atk, long Defense, ElementTypeId? Primary, ElementTypeId? Secondary)>? affinity = null) =>
        _affinity = affinity ?? (_ => (0, 0, null, null));

    public string SubsystemId => "rpg.battle.affinity";
    public int Order => 100;

    public void ContributeDerived(StatContext ctx, ICollection<DerivedModifier> mods)
    {
        var (atk, defense, primary, secondary) = _affinity(ctx);
        const string source = ContributionSourceIds.BattleBaseline;
        if (primary is { } p)
        {
            mods.Add(new DerivedModifier($"combat.power.{p.ToElementId()}", DerivedModifierOp.Flat, atk / BattleStatComposer.PrimaryAffinityDivisor, SourceId: source));
            mods.Add(new DerivedModifier($"combat.defense.{p.ToElementId()}", DerivedModifierOp.Flat, defense / BattleStatComposer.PrimaryAffinityDivisor, SourceId: source));
        }
        if (secondary is { } s)
        {
            mods.Add(new DerivedModifier($"combat.power.{s.ToElementId()}", DerivedModifierOp.Flat, atk / BattleStatComposer.SecondaryAffinityDivisor, SourceId: source));
            mods.Add(new DerivedModifier($"combat.defense.{s.ToElementId()}", DerivedModifierOp.Flat, defense / BattleStatComposer.SecondaryAffinityDivisor, SourceId: source));
        }
    }
}
