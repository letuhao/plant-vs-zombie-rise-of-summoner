using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Stats.Derived.Subsystems;

/// <summary>Progression tier power + combat bonus flats — P1/P2 ADR.</summary>
public sealed class RpgProgressionSubsystem : IActorStatSubsystem
{
    readonly IProgressionPowerProvider _power;

    public RpgProgressionSubsystem(IProgressionPowerProvider? power = null) =>
        _power = power ?? new StubProgressionPowerProvider();

    public string SubsystemId => "rpg.progression";
    public int Order => 100;

    public void ContributeDerived(StatContext ctx, ICollection<DerivedModifier> mods)
    {
        var power = _power.GetPower(ctx);
        var realm = _power.GetRealm(ctx);
        mods.Add(new DerivedModifier(
            DerivedStatChannels.ProgressionPower,
            DerivedModifierOp.Replace,
            power,
            SourceId: SubsystemId));
        mods.Add(new DerivedModifier(
            DerivedStatChannels.ProgressionRealm,
            DerivedModifierOp.Replace,
            realm,
            SourceId: SubsystemId));

        var level = _power.GetLevel(ctx);
        if (level <= 0) return;

        mods.Add(new DerivedModifier(
            DerivedStatChannels.ProgressionBonusMaxHp,
            DerivedModifierOp.Flat,
            level * 10,
            SourceId: SubsystemId));
        mods.Add(new DerivedModifier(
            DerivedStatChannels.ProgressionBonusAtk,
            DerivedModifierOp.Flat,
            level,
            SourceId: SubsystemId));
        mods.Add(new DerivedModifier(
            DerivedStatChannels.ProgressionBonusDefense,
            DerivedModifierOp.Flat,
            level * 0.5,
            SourceId: SubsystemId));
    }
}
