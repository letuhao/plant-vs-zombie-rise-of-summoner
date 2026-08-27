using System.Text.Json;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.ClassSystem;

/// <summary>class-system-todo.md P2.1 — AptitudeTuning parser. Pure parser (tunables-ssot.md §7.2):
/// no file I/O inside Core, so this test class owns reading `data/tuning/aptitudes.v2.json` itself,
/// the same way `FusionRpg.Guard.Tests`/`FusionRpg.ElementEnumGen.Tests` read `data/seed/*` directly
/// rather than through the library under test.</summary>
public class AptitudeTuningTests
{
    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "guard-class-system.ps1")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("could not locate repo root (scripts/guard-class-system.ps1 not found above " + AppContext.BaseDirectory + ")");
    }

    static string ShippedJson() =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), "data", "tuning", "aptitudes.v2.json"));

    // ── the real shipped file ───────────────────────────────────────────────────────────────────

    [Fact]
    public void ParsesTheShippedFile()
    {
        var tuning = AptitudeTuningLoader.Parse(ShippedJson());

        Assert.Equal(1, tuning.SchemaVersion);
        Assert.Equal(2, tuning.Version); // class-system-todo.md P8.2/P8.3, published aptitudes.v2.json 2026-08-27
        Assert.Equal(3, tuning.Grant.AptitudePointsPerThetaMilli);
        Assert.Equal(1, tuning.Grant.SkillPointsPerThetaMilli);
        Assert.Equal(100_000, tuning.Read.Contest.SpanPointsMilli); // 100.0 spanPoints * 1000
        Assert.Equal(1000, tuning.Read.Contest.ShareExponentMilli); // gamma = 1.0
        Assert.Equal(1000, tuning.Read.Magnitude.ShareExponentMilli); // gamma = 1.0
        Assert.Equal(374, tuning.Recovery.ScaleMilli);
        Assert.Equal(670, tuning.Recovery.TargetRecoveryShareMilli);
        Assert.Equal(new[] { "resource.regen", "combat.shield.regen" }, tuning.Recovery.Families);
        Assert.Equal(300, tuning.Mitigation.ScaleMilli); // class-system-todo.md P8.3, published v2 2026-08-27
        Assert.Equal(
            new[] { "combat.defense", "combat.dodge", "combat.parry", "combat.block", "combat.absorption", "combat.heal" },
            tuning.Mitigation.Families);
        Assert.Equal(48, tuning.FamilyRead.Count);
    }

    [Fact]
    public void GroupDividersAreSkipped_486RealEdgesNot490RawEntries()
    {
        // The authored file has 490 array entries in `edges`; 4 are `_group` section dividers with
        // no `channel` key. class-system P1.5/P1.6's reader census counted 486 real edges.
        var tuning = AptitudeTuningLoader.Parse(ShippedJson());
        Assert.Equal(486, tuning.Edges.Count);
    }

    [Fact]
    public void Edge_carriesResolvedReadMode()
    {
        var tuning = AptitudeTuningLoader.Parse(ShippedJson());
        var edge = Assert.Single(tuning.Edges, e => e.Channel == "combat.power.omni" && e.Source == "Might");
        Assert.Equal(2200, edge.KMilli);
        Assert.Equal(AptitudeReadMode.Magnitude, edge.Mode);
    }

    [Fact]
    public void FamilyOf_exactMatch()
    {
        var tuning = AptitudeTuningLoader.Parse(ShippedJson());
        Assert.Equal("combat.power", tuning.FamilyOf("combat.power"));
    }

    [Fact]
    public void FamilyOf_stripsOneAxisSuffix()
    {
        var tuning = AptitudeTuningLoader.Parse(ShippedJson());
        Assert.Equal("combat.power", tuning.FamilyOf("combat.power.omni"));
    }

    [Fact]
    public void FamilyOf_noMatchReturnsNull()
    {
        var tuning = AptitudeTuningLoader.Parse(ShippedJson());
        Assert.Null(tuning.FamilyOf("not.a.real.channel.at.all"));
    }

    [Fact]
    public void EveryEdgeChannel_isRegistered_inDerivedStatRegistry()
    {
        // spec-aptitude-tuning.md §6 test 4: a typo'd channel must not silently read zero forever --
        // it must fail to resolve against the SAME registry the resolver (P2.4/P2.5) will read from.
        var tuning = AptitudeTuningLoader.Parse(ShippedJson());
        var registry = DerivedStatRegistry.CreateDefault();
        var unresolved = tuning.Edges
            .Select(e => e.Channel)
            .Distinct(StringComparer.Ordinal)
            .Where(ch => !registry.TryResolveChannel(ch, out _))
            .ToList();
        Assert.True(unresolved.Count == 0, "unregistered edge channel(s): " + string.Join(", ", unresolved));
    }

    // ── rejection: every missing key names itself, never a default ────────────────────────────────

    [Theory]
    [InlineData("grant")]
    [InlineData("pointEconomy")]
    [InlineData("guardEconomy")]
    [InlineData("mitigation")]
    [InlineData("read")]
    [InlineData("recovery")]
    [InlineData("familyRead")]
    [InlineData("edges")]
    public void MissingTopLevelBlock_rejectsNamingIt(string key)
    {
        var doc = MinimalValidDoc();
        doc.Remove(key);
        var ex = Assert.Throws<AptitudeTuningRejection>(() => AptitudeTuningLoader.Parse(Serialize(doc)));
        Assert.Contains(key, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingGrantSubKey_rejectsNamingIt()
    {
        var doc = MinimalValidDoc();
        ((Dictionary<string, object>)doc["grant"]).Remove("aptitudePointsPerTheta");
        var ex = Assert.Throws<AptitudeTuningRejection>(() => AptitudeTuningLoader.Parse(Serialize(doc)));
        Assert.Contains("aptitudePointsPerTheta", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingPointEconomyScope_rejectsNamingIt()
    {
        // class-system P6.1 — mirrors MissingGrantSubKey_rejectsNamingIt above: each of the four
        // per-scope rates is required on its own, not defaulted if absent (tunables-ssot.md §7.2).
        var doc = MinimalValidDoc();
        var byScope = (Dictionary<string, object>)((Dictionary<string, object>)doc["pointEconomy"])["aptitudePointsPerThetaMilliByScope"];
        byScope.Remove("aspect");
        var ex = Assert.Throws<AptitudeTuningRejection>(() => AptitudeTuningLoader.Parse(Serialize(doc)));
        Assert.Contains("aspect", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingContestSpanPoints_rejectsNamingIt()
    {
        var doc = MinimalValidDoc();
        var read = (Dictionary<string, object>)doc["read"];
        var contest = (Dictionary<string, object>)read["contest"];
        contest.Remove("spanPoints");
        var ex = Assert.Throws<AptitudeTuningRejection>(() => AptitudeTuningLoader.Parse(Serialize(doc)));
        Assert.Contains("spanPoints", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingRecoveryFamilies_rejects()
    {
        var doc = MinimalValidDoc();
        ((Dictionary<string, object>)doc["recovery"]).Remove("families");
        Assert.Throws<AptitudeTuningRejection>(() => AptitudeTuningLoader.Parse(Serialize(doc)));
    }

    [Fact]
    public void MissingMitigationFamilies_rejects()
    {
        // class-system P8.3 -- mirrors MissingRecoveryFamilies_rejects above: Mitigation is
        // Recovery's own sibling dial (AptitudeMitigation's own doc comment) and required the same way.
        var doc = MinimalValidDoc();
        ((Dictionary<string, object>)doc["mitigation"]).Remove("families");
        Assert.Throws<AptitudeTuningRejection>(() => AptitudeTuningLoader.Parse(Serialize(doc)));
    }

    [Fact]
    public void UnknownReadMode_rejectsNamingIt()
    {
        var doc = MinimalValidDoc();
        ((Dictionary<string, object>)doc["familyRead"])["combat.power"] = "sideways";
        var ex = Assert.Throws<AptitudeTuningRejection>(() => AptitudeTuningLoader.Parse(Serialize(doc)));
        Assert.Contains("sideways", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EdgeWithNoFamilyReadRow_rejectsNamingChannel()
    {
        var doc = MinimalValidDoc();
        var edges = (List<object>)doc["edges"];
        edges.Add(new Dictionary<string, object>
        {
            ["channel"] = "totally.unclassified.channel",
            ["source"] = "Might",
            ["kMilli"] = 100,
        });
        var ex = Assert.Throws<AptitudeTuningRejection>(() => AptitudeTuningLoader.Parse(Serialize(doc)));
        Assert.Contains("totally.unclassified.channel", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EdgeWithNoSource_rejects()
    {
        var doc = MinimalValidDoc();
        var edges = (List<object>)doc["edges"];
        edges.Add(new Dictionary<string, object> { ["channel"] = "combat.power.omni", ["kMilli"] = 100 });
        Assert.Throws<AptitudeTuningRejection>(() => AptitudeTuningLoader.Parse(Serialize(doc)));
    }

    [Fact]
    public void GroupDividerWithNoChannel_isSkipped_notARejection()
    {
        var doc = MinimalValidDoc();
        var edges = (List<object>)doc["edges"];
        edges.Insert(0, new Dictionary<string, object> { ["_group"] = "=== a section heading ===" });
        var tuning = AptitudeTuningLoader.Parse(Serialize(doc));
        Assert.Single(tuning.Edges); // the divider contributed nothing; the one real edge still parsed
    }

    [Fact]
    public void EmptyDocument_rejects()
    {
        Assert.Throws<AptitudeTuningRejection>(() => AptitudeTuningLoader.Parse(""));
    }

    [Fact]
    public void MalformedJson_rejects()
    {
        Assert.Throws<AptitudeTuningRejection>(() => AptitudeTuningLoader.Parse("{ not json"));
    }

    [Fact]
    public void EmptyEdgesArray_rejects()
    {
        var doc = MinimalValidDoc();
        doc["edges"] = new List<object>();
        Assert.Throws<AptitudeTuningRejection>(() => AptitudeTuningLoader.Parse(Serialize(doc)));
    }

    // ── fixtures ─────────────────────────────────────────────────────────────────────────────────

    static string Serialize(object doc) => JsonSerializer.Serialize(doc);

    static Dictionary<string, object> MinimalValidDoc() => new()
    {
        ["schemaVersion"] = 1,
        ["version"] = 1,
        ["grant"] = new Dictionary<string, object> { ["aptitudePointsPerTheta"] = 3, ["skillPointsPerTheta"] = 1 },
        ["pointEconomy"] = new Dictionary<string, object>
        {
            ["aptitudePointsPerThetaMilliByScope"] = new Dictionary<string, object>
            {
                ["commander"] = 3, ["demonType"] = 4, ["aspect"] = 4, ["uniqueDemon"] = 6,
            },
            ["respecPrice"] = 10,
        },
        ["guardEconomy"] = new Dictionary<string, object>
        {
            ["flatCommitCost"] = 50, ["absorbDrainSharePermille"] = 300, ["riposteShareCapPermille"] = 400,
        },
        ["mitigation"] = new Dictionary<string, object>
        {
            ["scaleMilli"] = 1000, ["families"] = new List<object> { "combat.defense" },
        },
        ["read"] = new Dictionary<string, object>
        {
            ["contest"] = new Dictionary<string, object> { ["spanPoints"] = 100.0, ["shareExponentMilli"] = 1000 },
            ["magnitude"] = new Dictionary<string, object> { ["shareExponentMilli"] = 1000 },
        },
        ["recovery"] = new Dictionary<string, object>
        {
            ["scaleMilli"] = 374,
            ["targetRecoveryShareMilli"] = 670,
            ["families"] = new List<object> { "resource.regen" },
        },
        ["familyRead"] = new Dictionary<string, object> { ["combat.power"] = "magnitude" },
        ["edges"] = new List<object>
        {
            new Dictionary<string, object> { ["channel"] = "combat.power.omni", ["source"] = "Might", ["kMilli"] = 2200 },
        },
    };
}
