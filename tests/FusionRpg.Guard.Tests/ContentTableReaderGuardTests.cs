using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>
/// E20/E22 (completeness-audit.md finding B1, and the audit's §7 standing-guard proposal): a table
/// registered in <c>ContentHashRegistry</c> is hashed, versioned, and content-checked — none of which
/// requires anything to ever read the table for gameplay. <c>effect_channel_policy</c> shipped that
/// way at E16: registered at registry v4, zero production readers, unauthorable. This guard makes that
/// class of gap loud instead of silent.
///
/// <para><b>Reads source as text</b>, matching every other guard in this project (`guard-dal.ps1` and
/// friends run the same way) — Guard.Tests carries no project reference to Core or Data, on purpose,
/// so a guard cannot accidentally start exercising the thing it is meant to police from the outside.</para>
///
/// <para><b>Scope, honestly stated.</b> This covers the six tables <c>RpgStore
/// .LoadContentIntoRuntime</c> makes live: the element roster, both matchup matrices, both power
/// tables (E20), and <c>effect_channel_policy</c>'s <c>direction</c> column (E22) — the one column of
/// four with an existing consumer (<c>StatChannels.IsLowerBetter</c>); see
/// <c>ChannelPolicyTable</c>'s doc comment for why <c>default_value</c>/<c>cap_milli</c>/
/// <c>compose_kind</c> are not claimed here. The five container/atom/curve/rarity tables are
/// deliberately <b>not</b> asserted — <c>AtomPushService</c> already reads them for a real compiled
/// push, a different, already-closed gap (E19); the registry's remaining entry, <c>rarity</c>, is read
/// only by its own import's validation, never by a gameplay consumer, which is the runtime-producer
/// gap tracked as A4 and explicitly out of wave 6.</para>
/// </summary>
public class ContentTableReaderGuardTests
{
    [Fact]
    public void Every_table_ContentHashRegistry_covers_that_wave_6_promised_a_reader_for_has_one()
    {
        var loader = ReadDataFile("RpgStore.ContentBoot.cs");

        // The claim: LoadContentIntoRuntime calls ElementTable.Use / PowerTables.Use / ChannelPolicy-
        // Table.Use, and those statics are what BattleStatComposer, ElementRingMatrix,
        // ShieldElementMatrix, ActorPowerCache, CostFunction and StatChannels.DirectionOf already read
        // in production — those consumers are proven elsewhere (BattleResolverParityTests,
        // PowerReadsTests, TraitMigrationParityTests, ChannelPolicyTableTests). This test only proves
        // the loader itself makes the connection the audit found missing.
        Assert.Contains("ElementTable.Use(", loader, StringComparison.Ordinal);
        Assert.Contains("PowerTables.Use(", loader, StringComparison.Ordinal);
        Assert.Contains("ChannelPolicyTable.Use(", loader, StringComparison.Ordinal);
        Assert.Contains("GetElementTable()", loader, StringComparison.Ordinal);
        Assert.Contains("GetPowerTables()", loader, StringComparison.Ordinal);
        Assert.Contains("GetChannelPolicies()", loader, StringComparison.Ordinal);
    }

    [Fact]
    public void The_registry_names_exactly_the_twelve_tables_this_guard_accounts_for()
    {
        // A regression trip-wire for the registry itself: if a thirteenth table joins
        // ContentHashRegistry.Current, this guard's scope claim above goes stale silently unless
        // something notices. It should fail loudly instead.
        var registry = ReadCore("Effects", "Atoms", "ContentHashRegistry.cs");

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (System.Text.RegularExpressions.Match m in
                 System.Text.RegularExpressions.Regex.Matches(registry, "new ContentHashTable\\(\"([a-z_]+)\""))
            names.Add(m.Groups[1].Value);

        var expected = new[]
        {
            "effect_atom", "effect_container", "effect_container_atom", "effect_container_pool",
            "effect_curve", "rarity",
            "effect_element", "effect_element_matrix_combat", "effect_element_matrix_shield",
            "power_coefficient", "power_trigger_frequency",
            "effect_channel_policy",
        };

        Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal), names.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void StatChannels_DirectionOf_reads_the_table_before_the_code_switch()
    {
        var text = ReadCore("Stats", "ModifierOp.cs");

        Assert.Contains("ChannelPolicyTable.Current.TryGetDirection(channel, out var stored)", text,
            StringComparison.Ordinal);
    }

    static string ReadCore(params string[] relativeUnderCore)
    {
        var path = Path.Combine(new[] { FindRepoRoot(), "src", "FusionRpg.Core" }.Concat(relativeUnderCore).ToArray());
        Assert.True(File.Exists(path), "missing " + path);
        return File.ReadAllText(path);
    }

    static string ReadDataFile(string relative)
    {
        var path = Path.Combine(FindRepoRoot(), "src", "FusionRpg.Data", "Sqlite", relative);
        Assert.True(File.Exists(path), "missing " + path);
        return File.ReadAllText(path);
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }
}
