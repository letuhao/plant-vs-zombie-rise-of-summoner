using FusionRpg.Core.Power;
using FusionRpg.Core.Status;
using FusionRpg.Core.Stats;

namespace FusionRpg.Core.Stats.Derived.Subsystems;

/// <summary>
/// Progression tier power + combat bonus flats — P1/P2 ADR, amended by power-plan.md T3.2.
///
/// <para><c>progression.power</c> reads Θ from <see cref="IPowerIndexProvider.ActorIndex"/> — the
/// POC curve (<c>2^min(level,12)</c>) is retired (deleted, <c>ProgressionPowerCurve.cs</c> is gone).
/// Defaults to <see cref="StubPowerIndexProvider"/> (Θ=0), matching every other un-hydrated
/// power-index consumer's "no data yet" contract — a real behaviour change from the old stub's 1.0,
/// which was itself only ever an artifact of the retired curve's own <c>level&lt;=0 → 1.0</c> branch.
/// </para>
///
/// <para><c>progression.realm</c> stays the stub constant permanently (SSOT: "realm advancement is
/// additive in Θ, never a contest multiplier" — not this subsystem's concern to change).</para>
///
/// <para><see cref="_level"/> keeps only the one thing that was ever meaningfully exercised: the
/// level-gated bonus-mod path (<c>ActorHubTests.Applied_combat_includes_progression_bonus_flats</c>).
/// A bare per-context delegate, not a re-creation of the deleted <c>IProgressionPowerProvider</c> —
/// and deliberately separate from <see cref="_powerIndex"/>: bonus-mod "level" and Θ are different
/// wiring questions that happen to share a number today, not one concept with two names.</para>
/// </summary>
public sealed class RpgProgressionSubsystem : IActorStatSubsystem
{
    readonly Func<StatContext, int>? _level;
    readonly IPowerIndexProvider _powerIndex;

    public RpgProgressionSubsystem(Func<StatContext, int>? level = null, IPowerIndexProvider? powerIndex = null)
    {
        _level = level;
        _powerIndex = powerIndex ?? new StubPowerIndexProvider();
    }

    public string SubsystemId => "rpg.progression";
    public int Order => 100;

    public void ContributeDerived(StatContext ctx, ICollection<DerivedModifier> mods)
    {
        mods.Add(new DerivedModifier(
            DerivedStatChannels.ProgressionPower,
            DerivedModifierOp.Replace,
            _powerIndex.ActorIndex(ctx),
            SourceId: SubsystemId));
        mods.Add(new DerivedModifier(
            DerivedStatChannels.ProgressionRealm,
            DerivedModifierOp.Replace,
            StatusPolicy.ProgressionPowerStubDefault,
            SourceId: SubsystemId));

        var level = _level?.Invoke(ctx) ?? 0;
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
