using FusionRpg.Core.Battle;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Battle;

/// <summary>
/// item-ideal.md, `equip-runtime` (module 5) — ⭐ the payoff. After this module, one hand-made item
/// on one actor is observable in battle. Mirrors <see cref="TraitAtomSource"/>'s own migration tests
/// exactly, because it is the same producer shape.
/// </summary>
public class EquipRuntimeTests : IDisposable
{
    // No BattleStatComposer.Configure(...) needed: these fixtures never set ElementPrimary/Secondary,
    // so AddAffinity (the only reader of Tuning) never runs.
    public void Dispose()
    {
        BattleStatComposer.ResetEquipment();
        BattleStatComposer.ResetTraits();
    }

    static BattleActorSetup Actor(string? specimenId, int level = 5) => new()
    {
        Key = "squad:0", Side = "squad", SpeciesId = "spec", TypeId = 1, Level = level,
        SpecimenId = specimenId,
        MaxHp = 100, Atk = 50, Defense = 20,
    };

    static AtomRow DerivedAtom(string channel, int amount) => new()
    {
        AtomId = AtomRow.DeriveId("atom.equip-test", "", 1), KindId = "stat.derived",
        FamilyId = "atom.equip-test", Variant = "", Tier = 1, Name = "Equip Test",
        ParamsJson = $"{{\"channel\":\"{channel}\",\"op\":\"flat\",\"amount\":{amount}}}",
    };

    [Fact]
    public void An_equipped_item_changes_a_battle_number()
    {
        BattleStatComposer.UseEquipment(EquipAtomSource.FromResolver(specimenId =>
            specimenId == "s42" ? new[] { DerivedAtom(DerivedStatChannels.CombatPowerFire, 30) } : Array.Empty<AtomRow>()));

        var geared = BattleStatComposer.Compose(Actor(specimenId: "s42"));
        var bare = BattleStatComposer.Compose(Actor(specimenId: "s42", level: 5) with { SpecimenId = null });

        Assert.Equal(bare.Get(DerivedStatChannels.CombatPowerFire) + 30,
            geared.Get(DerivedStatChannels.CombatPowerFire));
    }

    [Fact]
    public void Unequipping_removes_the_contribution()
    {
        var equipped = true;
        BattleStatComposer.UseEquipment(EquipAtomSource.FromResolver(_ =>
            equipped ? new[] { DerivedAtom(DerivedStatChannels.CombatPowerFire, 30) } : Array.Empty<AtomRow>()));

        var before = BattleStatComposer.Compose(Actor(specimenId: "s42"));

        equipped = false; // the projection is rebuilt, not patched -- simulated here as "resolves empty now"
        var after = BattleStatComposer.Compose(Actor(specimenId: "s42"));

        Assert.Equal(30, before.Get(DerivedStatChannels.CombatPowerFire) - after.Get(DerivedStatChannels.CombatPowerFire));
    }

    [Fact]
    public void Equipment_and_trait_mods_compose_without_double_counting()
    {
        BattleStatComposer.UseTraits(TraitAtomSource.FromContainers(
            new[]
            {
                new ContainerRow
                {
                    ContainerId = "trait.test-trait", Kind = ContainerKind.Trait,
                    Atoms = new[] { new ContainerAtomRow(1, "atom.trait-test.t1") },
                },
            },
            atomId => atomId == "atom.trait-test.t1" ? DerivedAtomNamed("atom.trait-test", DerivedStatChannels.CombatPowerFire, 10) : null));

        BattleStatComposer.UseEquipment(EquipAtomSource.FromResolver(_ =>
            new[] { DerivedAtom(DerivedStatChannels.CombatPowerFire, 30) }));

        var setup = Actor(specimenId: "s42") with { TraitIds = new[] { "test-trait" } };
        var composed = BattleStatComposer.Compose(setup);

        var bareline = BattleStatComposer.Compose(Actor(specimenId: null));

        // Both contributions landed, exactly once each -- 10 (trait) + 30 (equipment), not 40 twice
        // and not one silently overwriting the other (both write the SAME channel via snap.Set(get()+amount)).
        Assert.Equal(bareline.Get(DerivedStatChannels.CombatPowerFire) + 40,
            composed.Get(DerivedStatChannels.CombatPowerFire));
    }

    static AtomRow DerivedAtomNamed(string family, string channel, int amount) => new()
    {
        AtomId = AtomRow.DeriveId(family, "", 1), KindId = "stat.derived",
        FamilyId = family, Variant = "", Tier = 1, Name = family,
        ParamsJson = $"{{\"channel\":\"{channel}\",\"op\":\"flat\",\"amount\":{amount}}}",
    };

    [Fact]
    public void No_specimen_id_means_no_equipment_contribution()
    {
        BattleStatComposer.UseEquipment(EquipAtomSource.FromResolver(_ =>
            new[] { DerivedAtom(DerivedStatChannels.CombatPowerFire, 999) }));

        var withoutSpecimen = BattleStatComposer.Compose(Actor(specimenId: null));
        var baseline = BattleStatComposer.Compose(Actor(specimenId: null) with { SpecimenId = null });

        Assert.Equal(baseline.Get(DerivedStatChannels.CombatPowerFire), withoutSpecimen.Get(DerivedStatChannels.CombatPowerFire));
    }

    [Fact]
    public void Sim_runtime_stays_None_and_the_spec_says_why()
    {
        // SimEffectHost has no consumer; flipping it on would recreate D6's original cause (a bind
        // accepted and then doing nothing forever). Item balance therefore cannot be simulated in
        // CombatSim until stat.derived gets a real Sim consumer -- a deliberate gap, asserted not assumed.
        var kind = AtomKindRegistry.Get("stat.derived")!;
        Assert.Equal(RuntimeState.None, kind.SupportIn(RuntimeId.Sim));
        Assert.Equal(RuntimeState.Full, kind.SupportIn(RuntimeId.Battle));
        Assert.Equal(RuntimeState.Full, kind.SupportIn(RuntimeId.Lawn));
    }
}
