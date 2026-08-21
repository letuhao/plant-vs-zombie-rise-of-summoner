using FusionRpg.Core.Battle;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Battle;

public class BattleStatComposerTests
{
    static BattleActorSetup Actor(string key, string side, int level = 5,
        ElementTypeId? elem = null, ElementTypeId? elemSecondary = null,
        IReadOnlyList<BattleChannelMod>? mods = null) => new()
    {
        Key = key,
        Side = side,
        SpeciesId = "test-species",
        TypeId = 10_001,
        Level = level,
        ElementPrimary = elem,
        ElementSecondary = elemSecondary,
        MaxHp = BattleRuleset.BaseHp(level),
        Atk = BattleRuleset.BaseAtk(level),
        Defense = BattleRuleset.BaseDefense(level),
        ChannelMods = mods ?? Array.Empty<BattleChannelMod>()
    };

    [Fact]
    public void Composed_channel_reads_match_CombatDerivedReader()
    {
        var setup = Actor("squad:0", "squad", level: 4, elem: ElementTypeId.Fire, elemSecondary: ElementTypeId.Ice);
        var snap = BattleStatComposer.Compose(setup);

        // battle-adoption mapping: Atk is the resolver's BASE, so power.omni is 0 (double-count
        // ban); Defense KEEPS its omni channel; affinity shares stay on both sides.
        Assert.Equal(0, (int)snap.Get(DerivedStatChannels.CombatPowerOmni));
        Assert.Equal(setup.Atk / 4, (int)CombatDerivedReader.Power(snap, ElementTypeId.Fire));
        Assert.Equal(setup.Atk / 8, (int)CombatDerivedReader.Power(snap, ElementTypeId.Ice));
        Assert.Equal(0, (int)CombatDerivedReader.Power(snap, ElementTypeId.Earth));
        Assert.Equal(setup.Defense + setup.Defense / 4, (int)CombatDerivedReader.Defense(snap, ElementTypeId.Fire));
        Assert.Equal(setup.Defense + setup.Defense / 8, (int)CombatDerivedReader.Defense(snap, ElementTypeId.Ice));

        // Accuracy family baselines come from level formulas (omni only — no element affinity).
        Assert.Equal(BattleRuleset.BaseAccuracy(4), (int)CombatDerivedReader.Accuracy(snap, ElementTypeId.Fire));
        Assert.Equal(BattleRuleset.BaseDodge(4), (int)CombatDerivedReader.Dodge(snap, ElementTypeId.Fire));
        Assert.Equal(BattleRuleset.BaseCritRate(4), (int)CombatDerivedReader.CritRate(snap, ElementTypeId.Fire));
        Assert.Equal(BattleRuleset.BaseCritResist(4), (int)CombatDerivedReader.CritResist(snap, ElementTypeId.Fire));
    }

    [Fact]
    public void Untyped_actor_reads_omni_only()
    {
        var setup = Actor("squad:0", "squad", level: 3);
        var snap = BattleStatComposer.Compose(setup);
        Assert.Equal(0, (int)snap.Get(DerivedStatChannels.CombatPowerOmni));   // Atk = request base
        Assert.Equal(0, (int)CombatDerivedReader.Power(snap, ElementTypeId.Fire));
        Assert.Equal(setup.Defense, (int)CombatDerivedReader.Defense(snap, ElementTypeId.Dark));
    }

    [Fact]
    public void Channel_mods_overlay_additively()
    {
        var setup = Actor("squad:0", "squad", level: 5, mods: new[]
        {
            new BattleChannelMod(DerivedStatChannels.CombatDodgeOmni, 200),
            new BattleChannelMod(DerivedStatChannels.CombatDodgeOmni, 50),
            new BattleChannelMod(DerivedStatChannels.CombatPowerFire, 30)
        });
        var snap = BattleStatComposer.Compose(setup);
        Assert.Equal(BattleRuleset.BaseDodge(5) + 250, (int)CombatDerivedReader.Dodge(snap, ElementTypeId.Fire));
        Assert.Equal(30, (int)CombatDerivedReader.Power(snap, ElementTypeId.Fire));   // mods only — no Atk base
    }

    [Fact]
    public void Unknown_mod_channel_rejects()
    {
        var setup = Actor("squad:0", "squad", mods: new[] { new BattleChannelMod("combat.power.plasma", 10) });
        Assert.Throws<ArgumentException>(() => BattleStatComposer.Compose(setup));
    }

    [Fact]
    public void Dodge_mod_swings_fixed_battles()
    {
        long withDodge = 0, without = 0;
        for (ulong seed = 0; seed < 20; seed++)
        {
            withDodge += SquadDamageDealt(seed, defenderMods: new[]
                { new BattleChannelMod(DerivedStatChannels.CombatDodgeOmni, 300) });
            without += SquadDamageDealt(seed, defenderMods: null);
        }

        Assert.True(withDodge < without,
            $"squad dealt {withDodge} vs dodgy defender, {without} vs plain defender");
    }

    [Fact]
    public void Crit_mod_swings_fixed_battles()
    {
        long withCrit = 0, without = 0;
        for (ulong seed = 0; seed < 20; seed++)
        {
            withCrit += SquadDamageDealt(seed, attackerMods: new[]
                { new BattleChannelMod(DerivedStatChannels.CombatCritRateOmni, 300) });
            without += SquadDamageDealt(seed, attackerMods: null);
        }

        Assert.True(withCrit > without,
            $"critty squad dealt {withCrit}, plain squad dealt {without}");
    }

    long SquadDamageDealt(ulong seed,
        IReadOnlyList<BattleChannelMod>? attackerMods = null,
        IReadOnlyList<BattleChannelMod>? defenderMods = null)
    {
        var report = BattleEngine.Resolve(new BattleSetup
        {
            WaveId = "stat-swing",
            Squad = new[] { Actor("squad:0", "squad", mods: attackerMods) },
            Wave = new[] { Actor("wave:0", "wave", mods: defenderMods) }
        }, seed);
        return report.Actors.Single(a => a.Side == "squad").DamageDealt;
    }
}
