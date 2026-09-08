using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Stats;

public class ContributionSourceIdsTests
{
    [Fact]
    public void Equip_grammar_and_fiction_label()
    {
        var id = ContributionSourceIds.Equip("armament-primary", "item-42");
        Assert.Equal("equip:armament-primary:item-42", id);
        Assert.Equal("Equip · armament-primary (item-42)", ContributionSourceIds.FictionLabel(id));
    }

    [Fact]
    public void Aptitude_tree_status_grant_primary_labels()
    {
        Assert.Equal("Aptitude · Might",
            ContributionSourceIds.FictionLabel(ContributionSourceIds.Aptitude("Might")));
        Assert.Equal("Tree · might/skill/n0",
            ContributionSourceIds.FictionLabel(ContributionSourceIds.Tree("might", "skill.n0")));
        Assert.Equal("Status · abc",
            ContributionSourceIds.FictionLabel(ContributionSourceIds.Status("abc")));
        Assert.Equal("Grant · fx.1",
            ContributionSourceIds.FictionLabel(ContributionSourceIds.Grant("fx.1")));
        Assert.Equal("Primary · cheat|tab-a",
            ContributionSourceIds.FictionLabel(ContributionSourceIds.Primary("cheat", "tab-a")));
        Assert.Equal("Progression",
            ContributionSourceIds.FictionLabel(ContributionSourceIds.Progression));
    }

    [Fact]
    public void Empty_source_is_unattributed_not_silent()
    {
        Assert.Equal("(unattributed)", ContributionSourceIds.FictionLabel(""));
    }
}
