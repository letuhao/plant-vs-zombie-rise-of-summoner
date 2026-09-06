using System.Linq;
using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Battle.Adoption;

/// <summary>
/// A18f (spec-action-dispatch-generalization.md) — T55.2/T55.3/T55.4's real integration proof. Built
/// 2026-09-06 during the A19 audit, AFTER discovering the todo's own claimed evidence for this file
/// cited tests that were never actually written (a stale/fabricated evidence record from an earlier
/// compacted context — caught via `git log --all` showing zero history for this path, corrected here
/// rather than left standing). The production fix itself
/// (`TimelineDispatch.cs`'s `runner.CurrentEnvelope(ev.OwnerKey) ?? state.BasicAttackEnvelopeCompiled`)
/// was real and already in the tree; only the test proving it was missing.
///
/// <para>Built on `BattleGoldenTests.CloseSetup()`, the same proven-reliable base
/// `ActionCostsCooldownsAdoptionTests.cs` (A19) uses — a hand-rolled actor pair risks stalemating or
/// resolving in one swing before the observable ever gets a chance to matter.</para>
/// </summary>
public class ActionDispatchGeneralizationTests
{
    /// <summary>A basic-attack-shaped Skill whose `EffectivenessChannel` is the SUPPORT category's own
    /// channel (`skill.effectiveness.support`) — deliberately NOT the attack category's channel
    /// (`skill.effectiveness.attack`) the hardcoded `BasicAttackCompiled` envelope reads. If the
    /// resolve branch ever silently fell back to the hardcoded envelope instead of this committed one,
    /// a boost on THIS channel would have no effect and a boost on attack's channel would — the exact
    /// inversion this file's tests are built to catch.</summary>
    static CompiledAction SupportSkill(string actionId = "skill.support") => new(
        ActionId: actionId, Kind: ActionKind.Skill, Rung: 1, Tags: new[] { ActionTag.Offensive },
        Enabled: true, Revision: 0, Grantable: false, DefaultAttackEligible: false, ContainerId: "",
        Envelope: ActionEnvelope.NoOp with
        {
            ActionId = actionId,
            EffectivenessChannel = DerivedStatChannels.SkillEffectiveness(DerivedStatChannels.ActionCategorySupport),
        },
        Targeting: TargetSpecCompiler.Compile(new ActionTargetSpec()),
        MinRange: 0, MaxRange: int.MaxValue, RangeChannel: null, RequiresLineOfSight: false,
        Condition: PredicateCompiler.Always,
        Costs: System.Array.Empty<CompiledActionCost>(),
        Scopes: System.Array.Empty<ActionScopeRow>());

    static BattleSetup EquipSquadZero(string actionId, params BattleChannelMod[] channelMods) =>
        BattleGoldenTests.CloseSetup() with
        {
            Squad = BattleGoldenTests.CloseSetup().Squad.Select((a, i) => i == 0
                ? a with { EquippedActionIds = new[] { actionId }, ChannelMods = channelMods }
                : a).ToArray(),
        };

    static BattleReport Run(BattleSetup setup, ActionCatalog catalog, BattleTrace? trace = null) =>
        BattleEngine.Resolve(setup, seed: 5501, trace: trace, actionCatalog: catalog);

    /// <summary>Forces the ATOMIC dispatch path (`RunBasicAttackStep`) — every shipped profile has
    /// `UsesTimelineDispatch = true` today (`BattleModeProfile.cs:221,235,251`), so this synthetic
    /// override is the only way to reach the atomic path at all, exactly as `TimelineDispatch.cs`'s
    /// own file header says this shape exists to let a test do.</summary>
    static BattleReport RunAtomic(BattleSetup setup, ActionCatalog catalog, BattleTrace? trace = null) =>
        BattleEngine.Resolve(setup, seed: 5501, trace: trace, actionCatalog: catalog,
            profile: BattleModeProfileCatalog.ClassicRound with { UsesTimelineDispatch = false });

    [Fact]
    public void The_fixture_itself_neither_stalemates_nor_ends_in_one_swing()
    {
        var catalog = ActionCatalog.Build(new[] { SupportSkill() });
        var setup = EquipSquadZero("skill.support");
        var trace = new BattleTrace();

        Run(setup, catalog, trace);

        var squadZeroHits = trace.Targets.Count(t => t.Contains(" squad:0->", System.StringComparison.Ordinal));
        Assert.True(squadZeroHits > 1, $"expected more than one declared attack; got {squadZeroHits}");
    }

    [Fact]
    public void A_boost_on_the_actions_own_support_effectiveness_channel_changes_the_battle()
    {
        var boostedChannel = DerivedStatChannels.SkillEffectiveness(DerivedStatChannels.ActionCategorySupport);
        var catalog = ActionCatalog.Build(new[] { SupportSkill() });

        var baselineTrace = new BattleTrace();
        Run(EquipSquadZero("skill.support"), catalog, baselineTrace);

        var boostedTrace = new BattleTrace();
        Run(EquipSquadZero("skill.support", new BattleChannelMod(boostedChannel, 5000)), catalog, boostedTrace);

        // Round 1's apply against squad:0's own declared target must differ once the committed
        // skill's own channel is boosted -- if the resolve branch silently used the hardcoded
        // basic-attack envelope instead, this channel would never be read at all and the two traces
        // would be byte-identical.
        Assert.NotEqual(string.Join("|", baselineTrace.Applies), string.Join("|", boostedTrace.Applies));
    }

    [Fact]
    public void A_boost_on_the_basic_attacks_own_attack_effectiveness_channel_stays_inert()
    {
        // The channel the HARDCODED BasicAttackCompiled envelope reads -- NOT the equipped skill's
        // own channel. If the resolve branch ever silently fell back to that hardcoded envelope, this
        // boost (not the one in the test above) would be the one that moved the battle.
        var attackChannel = DerivedStatChannels.SkillEffectiveness(DerivedStatChannels.ActionCategoryAttack);
        var catalog = ActionCatalog.Build(new[] { SupportSkill() });

        var baselineTrace = new BattleTrace();
        Run(EquipSquadZero("skill.support"), catalog, baselineTrace);

        var boostedTrace = new BattleTrace();
        Run(EquipSquadZero("skill.support", new BattleChannelMod(attackChannel, 5000)), catalog, boostedTrace);

        Assert.Equal(string.Join("|", baselineTrace.Applies), string.Join("|", boostedTrace.Applies));
    }

    [Fact]
    public void T55_3_RunBasicAttackStep_still_threads_its_own_envelope_never_the_hardcoded_one()
    {
        // Same proof as the timeline-dispatch test above, run through the ATOMIC path instead --
        // `RunBasicAttackStep`/`DeclareBasicAttack` were verified by direct code reading to already
        // thread their own local `envelope` correctly (never had T55.2's bug), and this pins that
        // fact as a real, running regression guard rather than leaving it as an unverified claim.
        var boostedChannel = DerivedStatChannels.SkillEffectiveness(DerivedStatChannels.ActionCategorySupport);
        var catalog = ActionCatalog.Build(new[] { SupportSkill() });

        var baselineTrace = new BattleTrace();
        RunAtomic(EquipSquadZero("skill.support"), catalog, baselineTrace);

        var boostedTrace = new BattleTrace();
        RunAtomic(EquipSquadZero("skill.support", new BattleChannelMod(boostedChannel, 5000)), catalog, boostedTrace);

        Assert.NotEqual(string.Join("|", baselineTrace.Applies), string.Join("|", boostedTrace.Applies));
    }

    [Fact]
    public void T55_4_a_non_attack_category_action_still_deals_attack_shaped_damage_today()
    {
        // The named, deliberate limitation (spec-action-dispatch-generalization.md's own "⛔ Real,
        // load-bearing gap" section): ApplyBasicAttack is attack-shaped throughout regardless of the
        // committed action's own Category. A Support-category action still rolls a hit and deals
        // damage exactly like an Attack-category one -- proven here as TODAY'S REAL BEHAVIOR, not
        // endorsed as correct, so a future change that silently assumes this gap is closed (e.g. a
        // category branch skipping the attack roll for Support) fails this test rather than shipping
        // an un-audited behavior change unnoticed.
        var supportAction = SupportSkill() with { Category = ActionCategory.Support };
        var catalog = ActionCatalog.Build(new[] { supportAction });
        var setup = EquipSquadZero("skill.support");
        var trace = new BattleTrace();

        Run(setup, catalog, trace);

        Assert.Contains(trace.Targets, t => t.Contains(" squad:0->", System.StringComparison.Ordinal));
        Assert.Contains(trace.Applies, a => a.StartsWith("1 ", System.StringComparison.Ordinal));
    }
}
