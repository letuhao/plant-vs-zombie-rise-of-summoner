using System.Runtime.CompilerServices;
using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Corpus;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Actions;

/// <summary>
/// T59.1 (spec-action-instance-and-grant.md §2, narrowed during BUILD 2026-09-06): the ONE
/// per-category number `ActionTimingTuning` does not already own — a default onCommit resource cost
/// per `ActionCategory` for the corpus importer (T59.3) to fall back on. Mirrors
/// `ActionTimingTuningLoader`'s own established shape and this same convention's inline-JSON unit-test
/// style (tunables-ssot.md §7.2: "construct one inline; no fixture files").
/// </summary>
public class ActionCorpusCostTemplateTests
{
    // Mirrors the real data/tuning/action-corpus-cost-templates.v1.json byte-for-byte at the time
    // this test was written.
    const string FullTemplate = """
    {
      "schemaVersion": 1, "version": 1,
      "categories": {
        "attack": { "resourceId": "qi", "baseAmountAtRung1": 20, "timing": "onCommit" },
        "defense": { "resourceId": "qi", "baseAmountAtRung1": 30, "timing": "onCommit" },
        "support": { "resourceId": "qi", "baseAmountAtRung1": 40, "timing": "onCommit" },
        "movement": { "resourceId": "qi", "baseAmountAtRung1": 15, "timing": "onCommit" },
        "status": { "resourceId": "qi", "baseAmountAtRung1": 35, "timing": "onCommit" }
      }
    }
    """;

    static string RepoRoot([CallerFilePath] string here = "")
    {
        var testsDir = Path.GetDirectoryName(here)!;                      // tests/.../Actions
        return Path.GetFullPath(Path.Combine(testsDir, "..", "..", ".."));  // repo root
    }

    /// <summary>The inline mirror above proves the loader's own contract; this proves the actually-
    /// shipped file has no typo the mirror wouldn't catch (matching `UnlockLadderTests.cs`'s own
    /// `Shipped` real-file pattern).</summary>
    [Fact]
    public void TheRealShippedFileLoadsAndCoversAllFiveCategories()
    {
        var path = Path.Combine(RepoRoot(), "data", "tuning", "action-corpus-cost-templates.v1.json");
        var template = ActionCorpusCostTemplateLoader.Parse(File.ReadAllText(path));

        foreach (var category in new[] { ActionCategory.Attack, ActionCategory.Defense, ActionCategory.Support, ActionCategory.Movement, ActionCategory.Status })
        {
            var row = template.CategoryOf(category);
            Assert.False(string.IsNullOrWhiteSpace(row.ResourceId));
            Assert.True(row.BaseAmountAtRung1 > 0);
        }
    }

    [Fact]
    public void EveryCategoryLoadsItsOwnCompleteRow()
    {
        var template = ActionCorpusCostTemplateLoader.Parse(FullTemplate);

        var attack = template.CategoryOf(ActionCategory.Attack);
        Assert.Equal("qi", attack.ResourceId);
        Assert.Equal(20, attack.BaseAmountAtRung1);
        Assert.Equal(ActionCostTiming.OnCommit, attack.Timing);

        // Every one of the 5 categories resolves without throwing -- the exhaustive-coverage half of
        // the acceptance bar.
        foreach (var category in new[] { ActionCategory.Attack, ActionCategory.Defense, ActionCategory.Support, ActionCategory.Movement, ActionCategory.Status })
            template.CategoryOf(category); // must not throw
    }

    [Theory]
    [InlineData("attack")]
    [InlineData("defense")]
    [InlineData("support")]
    [InlineData("movement")]
    [InlineData("status")]
    public void AMissingCategoryRejectsNamingWhichOne(string missingKey)
    {
        var withoutOne = FullTemplate.Replace($"\"{missingKey}\": {{ \"resourceId\": \"qi\"", "\"__removed__\": { \"resourceId\": \"qi\"");
        Assert.NotEqual(FullTemplate, withoutOne); // the replace actually took effect

        var ex = Assert.Throws<ActionCorpusCostTemplateRejection>(() => ActionCorpusCostTemplateLoader.Parse(withoutOne));
        Assert.Contains(missingKey, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANonPositiveBaseAmountIsRejected()
    {
        var zeroed = FullTemplate.Replace("\"baseAmountAtRung1\": 20", "\"baseAmountAtRung1\": 0");
        var ex = Assert.Throws<ActionCorpusCostTemplateRejection>(() => ActionCorpusCostTemplateLoader.Parse(zeroed));
        Assert.Contains("baseAmountAtRung1", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownTimingStringIsRejected()
    {
        var badTiming = FullTemplate.Replace("\"timing\": \"onCommit\"", "\"timing\": \"wheneverIFeelLikeIt\"");
        Assert.Throws<ActionCorpusCostTemplateRejection>(() => ActionCorpusCostTemplateLoader.Parse(badTiming));
    }

    /// <summary>T59.1's other genuine gap: neither `ActionCompiler.Compile` nor
    /// `ActionTimingDerivation.Derive` ever sets `CooldownChannel`/`EffectivenessChannel` — confirmed
    /// by reading both during BUILD. `ActionCategoryChannels` is the structural (non-tunable) mapping
    /// this module adds instead; it must cover all 5 categories with the exact
    /// `skill.cooldown.*`/`skill.effectiveness.*` strings `DerivedStatChannels` already defines.</summary>
    [Fact]
    public void ActionCategoryChannelsCoversAllFiveCategoriesWithTheRealChannelStrings()
    {
        Assert.Equal(DerivedStatChannels.SkillCooldown("attack"), ActionCategoryChannels.CooldownChannelFor(ActionCategory.Attack));
        Assert.Equal(DerivedStatChannels.SkillCooldown("defense"), ActionCategoryChannels.CooldownChannelFor(ActionCategory.Defense));
        Assert.Equal(DerivedStatChannels.SkillCooldown("support"), ActionCategoryChannels.CooldownChannelFor(ActionCategory.Support));
        Assert.Equal(DerivedStatChannels.SkillCooldown("movement"), ActionCategoryChannels.CooldownChannelFor(ActionCategory.Movement));
        Assert.Equal(DerivedStatChannels.SkillCooldown("status"), ActionCategoryChannels.CooldownChannelFor(ActionCategory.Status));

        Assert.Equal(DerivedStatChannels.SkillEffectiveness("attack"), ActionCategoryChannels.EffectivenessChannelFor(ActionCategory.Attack));
        Assert.Equal(DerivedStatChannels.SkillEffectiveness("defense"), ActionCategoryChannels.EffectivenessChannelFor(ActionCategory.Defense));
        Assert.Equal(DerivedStatChannels.SkillEffectiveness("support"), ActionCategoryChannels.EffectivenessChannelFor(ActionCategory.Support));
        Assert.Equal(DerivedStatChannels.SkillEffectiveness("movement"), ActionCategoryChannels.EffectivenessChannelFor(ActionCategory.Movement));
        Assert.Equal(DerivedStatChannels.SkillEffectiveness("status"), ActionCategoryChannels.EffectivenessChannelFor(ActionCategory.Status));
    }
}
