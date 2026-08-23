using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Stats;
using FusionRpg.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FusionRpg.E2E.Tests;

/// <summary>
/// E22's seam, run against the real chain (completeness-audit.md finding B1): a JSON seed file, the
/// real <see cref="AtomSeedFile.Collect"/> parser, the real <see cref="RpgStore.ImportContent"/>
/// transaction, and the real <see cref="RpgStore.LoadContentIntoRuntime"/> — proving an imported
/// direction row is what <see cref="StatChannels.DirectionOf"/> reflects, not the code switch.
/// </summary>
[Collection("e2e")]
public class ChannelPolicyE2ETests
{
    readonly RpgStore _store;

    public ChannelPolicyE2ETests(RpgApiFactory factory) => _store = factory.Services.GetRequiredService<RpgStore>();

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "data", "seed", "channel-policy"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("data/seed/channel-policy");
    }

    [Fact]
    public void The_shipped_channel_policy_file_imports_clean_and_changes_no_behaviour()
    {
        // defaults.json restates the code defaults on purpose (documentation-as-data, zero design
        // decision) — safe to run against the shared fixture's real store.
        var root = RepoRoot();
        var files = Directory.GetFiles(Path.Combine(root, "data", "seed", "channel-policy"), "*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (f, File.ReadAllText(f)))
            .ToArray();

        var collected = AtomSeedFile.Collect(files);
        Assert.True(collected.IsOk, string.Join("; ", collected.Errors));
        Assert.Equal(2, collected.Content.ChannelPolicies.Count);

        var outcome = _store.ImportContent(collected.Content);
        Assert.True(outcome.IsOk, string.Join("; ", outcome.Errors));

        try
        {
            _store.LoadContentIntoRuntime();

            Assert.Equal(ChannelDirection.LowerIsBetter, StatChannels.DirectionOf(StatChannels.AttackInterval));
            Assert.Equal(ChannelDirection.LowerIsBetter, StatChannels.DirectionOf(StatChannels.ProduceInterval));
            Assert.Equal(ChannelDirection.HigherIsBetter, StatChannels.DirectionOf(StatChannels.Atk));
        }
        finally
        {
            ChannelPolicyTable.ResetToEmpty();
        }
    }

    [Fact]
    public void A_seeded_direction_flip_the_code_default_does_not_have_survives_the_real_import_chain()
    {
        // A throwaway store, not the shared fixture — this flips "atk" to lower-is-better, which is
        // fictional test content, not a real balance change, and must not leak into the other 28
        // classes sharing the fixture's database.
        var tempDir = Path.Combine(Path.GetTempPath(), "fusionrpg-e22-seam-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            const string seedJson = """
                {
                  "schemaVersion": 1,
                  "kind": "channel-policy",
                  "entries": [
                    { "channel": "atk", "direction": 1 }
                  ]
                }
                """;
            var collected = AtomSeedFile.Collect(new[] { ("audit-e22.json", seedJson) });
            Assert.True(collected.IsOk, string.Join("; ", collected.Errors));

            var tempStore = new RpgStore(tempDir);
            tempStore.Init();
            var outcome = tempStore.ImportContent(collected.Content);
            Assert.True(outcome.IsOk, string.Join("; ", outcome.Errors));

            tempStore.LoadContentIntoRuntime();

            Assert.Equal(ChannelDirection.LowerIsBetter, StatChannels.DirectionOf(StatChannels.Atk));
            Assert.True(StatChannels.IsLowerBetter(StatChannels.Atk));
        }
        finally
        {
            ChannelPolicyTable.ResetToEmpty();
            try { Directory.Delete(tempDir, recursive: true); } catch { /* temp dir */ }
        }
    }

    [Fact]
    public void An_unknown_channel_is_refused_by_the_real_import_not_silently_accepted()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "fusionrpg-e22-refuse-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            const string seedJson = """
                {
                  "schemaVersion": 1,
                  "kind": "channel-policy",
                  "entries": [
                    { "channel": "fireRate", "direction": 0 }
                  ]
                }
                """;
            var collected = AtomSeedFile.Collect(new[] { ("audit-e22-bad.json", seedJson) });
            Assert.True(collected.IsOk, string.Join("; ", collected.Errors));

            var tempStore = new RpgStore(tempDir);
            tempStore.Init();
            var outcome = tempStore.ImportContent(collected.Content);

            Assert.False(outcome.IsOk);
            Assert.Contains(outcome.Errors, e => e.ToString().Contains("fireRate", StringComparison.Ordinal));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* temp dir */ }
        }
    }
}
