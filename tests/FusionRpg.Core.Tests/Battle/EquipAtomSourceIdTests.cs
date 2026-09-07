using FusionRpg.Core.Battle;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Stats.Derived.Subsystems;
using Xunit;

namespace FusionRpg.Core.Tests.Battle;

public class EquipAtomSourceIdTests
{
    [Fact]
    public void DerivedAtomsFor_uses_equip_role_item_SourceId()
    {
        var atom = new AtomRow
        {
            AtomId = AtomRow.DeriveId("atom.equip-test", "", 1),
            KindId = "stat.derived",
            FamilyId = "atom.equip-test",
            Variant = "",
            Tier = 1,
            Name = "Equip Test",
            ParamsJson = $"{{\"channel\":\"{DerivedStatChannels.CombatPowerFire}\",\"op\":\"flat\",\"amount\":200}}",
        };
        var source = EquipAtomSource.FromEquippedResolver(_ => new[]
        {
            new EquippedAtomInput("armament-primary", "item-stem", atom)
        });

        var bound = Assert.Single(source.DerivedAtomsFor("spec-1"));
        Assert.Equal(ContributionSourceIds.Equip("armament-primary", "item-stem"), bound.SourceId);
        Assert.Equal(200L, bound.Amount);
    }

    [Fact]
    public void FromResolver_legacy_mints_equip_unknown_atomId_not_bare_atom_id()
    {
        var atom = new AtomRow
        {
            AtomId = AtomRow.DeriveId("atom.equip-test", "", 1),
            KindId = "stat.derived",
            FamilyId = "atom.equip-test",
            Variant = "",
            Tier = 1,
            Name = "Equip Test",
            ParamsJson = $"{{\"channel\":\"{DerivedStatChannels.CombatPowerFire}\",\"op\":\"flat\",\"amount\":30}}",
        };
        var source = EquipAtomSource.FromResolver(_ => new[] { atom });
        var bound = Assert.Single(source.DerivedAtomsFor("s42"));
        Assert.Equal(ContributionSourceIds.Equip("unknown", atom.AtomId), bound.SourceId);
        Assert.StartsWith("equip:unknown:", bound.SourceId, StringComparison.Ordinal);
    }
}
