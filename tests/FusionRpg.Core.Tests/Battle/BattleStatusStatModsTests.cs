using FusionRpg.Core.Battle;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Status;
using Xunit;

namespace FusionRpg.Core.Tests.Battle;

/// <summary>
/// status-rail B2 — battle projects status-instance StatMods into
/// <see cref="BattleStatModifierLedger"/> on apply and withdraws on end (same seam as
/// <c>BattleRunState</c> OnApplied/OnEnded; nested BattleRunState is not constructible from tests).
/// </summary>
public class BattleStatusStatModsTests
{
    [Fact]
    public void Apply_ModifyStat_status_adds_channel_delta_withdraw_on_end_clears_it()
    {
        var ledger = new BattleStatModifierLedger();
        var rt = new StatusRuntime(
            StatusCatalogBootstrap.CreateDefault(),
            (_, attackerLess) =>
                attackerLess ? ActorDerivedSnapshot.AttackerLess() : ActorDerivedSnapshot.StubNeutral());

        // Mirror BattleRunState status-rail B2 hooks.
        rt.OnApplied = inst =>
        {
            if (inst.StatMods.Count == 0) return;
            foreach (var mod in StatusStatPayload.ToModifiers(inst))
                ledger.Add(inst.HostPtr, mod.Channel, StatusStatPayload.SourceIdOf(inst), mod);
        };
        rt.OnEnded = inst =>
        {
            if (inst.StatMods.Count == 0) return;
            ledger.RemoveBySource(inst.HostPtr, StatusStatPayload.SourceIdOf(inst));
        };

        var now = DateTimeOffset.UtcNow;
        var outcome = rt.Apply(
            new StatusApplyInput(
                "expose",
                HostPtr: "squad:0",
                AttackerPtr: "wave:0",
                GrantId: "g-expose",
                BaseMagnitude: 1,
                BaseDuration: 5000,
                DurationMs: 5000,
                StatMods: new[] { new StatusStatMod("atk", "more", -0.1) }),
            new FixedStatusRng(0.0),
            now);

        Assert.True(outcome.Applied);
        Assert.NotNull(outcome.Instance);
        var live = ledger.For("squad:0", "atk");
        Assert.Single(live);
        Assert.Equal(ModifierOp.More, live[0].Op);
        Assert.Equal(-0.1, live[0].Value);

        // WithdrawEntity does not fire OnEnded; ClearGrant / expire does (BattleRunState mirrors that).
        rt.ClearGrant("g-expose");
        Assert.Empty(ledger.For("squad:0", "atk"));
    }
}
