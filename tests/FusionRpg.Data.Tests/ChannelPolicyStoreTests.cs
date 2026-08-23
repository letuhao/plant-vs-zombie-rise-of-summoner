using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Stats;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// <c>effect_channel_policy</c> (E16) — the table that was tracked for weeks with no owning module.
///
/// <para>Its assignment was settled by <b>E1's own code-or-data rule</b>: a thing may be data if
/// adding a row changes behaviour without new code. Changing a cap or default on an existing channel
/// is a value change with a live consumer, so it is data. Adding a <i>channel</i> needs a composer
/// case and a writer case — a new reader — so it stays code, and this table refuses to pretend
/// otherwise.</para>
/// </summary>
public class ChannelPolicyStoreTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public ChannelPolicyStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-chanpol-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    [Fact]
    public void An_empty_table_reports_the_shipped_policy_for_every_channel()
    {
        var policies = _store.GetChannelPolicies();

        Assert.Equal(StatChannels.All.Length, policies.Count);
        Assert.All(policies, p => Assert.Equal(-1, p.CapMilli)); // uncapped until authored
    }

    [Fact]
    public void The_shipped_direction_matches_the_code_that_composes_it()
    {
        // Two sources of truth about which way is better would be the same defect the documented
        // nine channels were: a doc promising something the code does not do.
        foreach (var policy in _store.GetChannelPolicies())
            Assert.Equal(
                (int)StatChannels.DirectionOf(policy.ChannelId),
                policy.Direction);
    }

    [Fact]
    public void A_cap_can_be_authored_and_reads_back()
    {
        var edited = new[]
        {
            RpgStore.ShippedPolicy(StatChannels.Defense) with { CapMilli = 950 },
        };

        Assert.True(_store.UpsertChannelPolicies(edited).Ok);

        var stored = _store.GetChannelPolicies().Single(p => p.ChannelId == StatChannels.Defense);
        Assert.Equal(950, stored.CapMilli);
    }

    [Fact]
    public void A_row_may_not_invent_a_channel()
    {
        // The whole reason channel identity stayed code. A row naming a channel nothing composes
        // would be accepted and then do nothing — the silent no-op this program exists to refuse.
        var verdict = _store.UpsertChannelPolicies(new[]
        {
            RpgStore.ShippedPolicy("fireRate"),
        });

        Assert.False(verdict.Ok);
        Assert.Contains("code-or-data rule", verdict.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_direction_outside_the_two_is_refused()
    {
        var verdict = _store.UpsertChannelPolicies(new[]
        {
            RpgStore.ShippedPolicy(StatChannels.Atk) with { Direction = 7 },
        });

        Assert.False(verdict.Ok);
    }

    // ---- the hash --------------------------------------------------------------------------------

    [Fact]
    public void The_registry_is_at_version_four_and_covers_the_policy_table()
    {
        Assert.Equal(4, ContentHashRegistry.CurrentSchemaVersion);
        Assert.Contains("effect_channel_policy",
            ContentHashRegistry.Current.Select(t => t.TableName));
    }

    [Fact]
    public void Editing_a_cap_moves_the_content_hash()
    {
        // The reason the table and the registry bump ship together. The 0.95 resist cap was a code
        // constant: editing it moved every battle golden while the stamp stood still — acceptable
        // only because a constant is visible in a diff, which stops being true once it is a row.
        var before = _store.ComputeContentHash().Hash;

        _store.UpsertChannelPolicies(new[]
        {
            RpgStore.ShippedPolicy(StatChannels.Defense) with { CapMilli = 950 },
        });

        Assert.NotEqual(before, _store.ComputeContentHash().Hash);
    }

    [Fact]
    public void Writing_the_same_policy_twice_does_not_move_the_hash()
    {
        var policy = new[] { RpgStore.ShippedPolicy(StatChannels.Defense) with { CapMilli = 950 } };
        _store.UpsertChannelPolicies(policy);
        var hash = _store.ComputeContentHash().Hash;

        _store.UpsertChannelPolicies(policy);

        Assert.Equal(hash, _store.ComputeContentHash().Hash);
    }
}
