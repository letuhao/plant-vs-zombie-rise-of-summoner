using System.Text.Json;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Power;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>
/// E19 (spec-compiled-push.md), server half: what leaves the server for a connected injector.
///
/// <para>The guarantee is the one that makes the Cold/Hot split legal — <b>the injector never holds
/// content rows</b>. Everything here is already compiled: predicates as flat int ops, values as
/// curve-scaled bounds, names interned away.</para>
/// </summary>
public class CompiledPushTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;
    readonly AtomPushService _push;

    static readonly JsonSerializerOptions Wire = new();

    public CompiledPushTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-push-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        _push = new AtomPushService(_store);
        Seed();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    /// <summary>Two runner atoms (an OnApply range and a capped economy) plus one that compiles.</summary>
    void Seed()
    {
        Add("atom.searing", "resource.delta",
            """{"amount":{"min":-120,"max":-80,"roll":"onApply"},"element":"fire"}""",
            """{"trigger":"OnDamageDealt","chance":250,"icd_ms":500}""");

        Add("atom.sun-tap", "resource.economy",
            """{"currency":"sun","op":"add","amount":25,"capPerMatch":3}""",
            """{"trigger":"OnDamageDealt"}""");

        Add("atom.vitality", "stat.modify", """{"channel":"maxHp","op":"flat","amount":45}""");

        Container("item.blade", ContainerKind.Item, "atom.searing.t1", "atom.sun-tap.t1");
        Container("trait.stalwart", ContainerKind.Trait, "atom.vitality.t1");

        void Add(string family, string kind, string paramsJson, string whenJson = "{}")
        {
            var result = _store.UpsertAtom(new AtomRow
            {
                AtomId = AtomRow.DeriveId(family, "", 1),
                KindId = kind, FamilyId = family, Variant = "", Tier = 1,
                Name = family, ParamsJson = paramsJson, WhenJson = whenJson,
            });
            // The reason, not just the id: a seed rejected for an unrelated schema change should
            // say so rather than fail every test in the class with a bare family name.
            Assert.True(result.IsOk, family + ": " + result);
        }

        void Container(string id, ContainerKind kind, params string[] atomIds) =>
            Assert.True(_store.UpsertContainer(new ContainerRow
            {
                ContainerId = id,
                Kind = kind,
                Atoms = atomIds.Select((a, i) => new ContainerAtomRow(i + 1, a)).ToArray(),
            }).IsOk, id);
    }

    // T3.4 (content-scale): 20 is the pin -- contentScale(20) == 1.000 exactly.
    static readonly PowerTuning Tuning = PowerTuning.Build(
        1, 1, 80_000, 0, 20, 680, // fixed anchor (Fixed* consts are `internal` to Core+Core.Tests only)
        1000, 25000, 250, 1000, 5000, 5000, 25000);

    string Bind(string containerId, string ownerKey, int priority = 0)
    {
        var container = _store.GetContainer(containerId)!;
        var atoms = _store.ListAtoms().ToDictionary(a => a.AtomId, StringComparer.Ordinal);

        Assert.True(Instantiator.TryInstantiate(container,
            id => atoms.TryGetValue(id, out var a) ? a : null, _store.GetAffix, 1, 20, Tuning, out var inst).IsOk);

        var instanceId = _store.SaveInstance(inst! with { CatalogRevision = _store.GetCatalogRevision() });
        var bindingId = Guid.NewGuid().ToString("N");

        Assert.True(_store.Bind(new BindingRow
        {
            InstanceId = instanceId,
            OwnerKind = OwnerKind.Player,
            OwnerKey = ownerKey,
            Priority = priority,
            Source = "test",
        }, bindingId).IsOk);

        return bindingId;
    }

    static OwnerScope Owner(string key = "1") => new(OwnerKind.Player, key);
    static BindContext Lawn() => new(RuntimeId.Lawn);

    // ---- delivery ----------------------------------------------------------------------------------

    [Fact]
    public void A_cold_start_receives_the_full_set()
    {
        Bind("item.blade", "1");

        var payload = _push.Build(Owner(), Lawn(), matchSeed: 7, matchKey: "m1");

        Assert.False(payload.UpToDate);
        Assert.NotEmpty(payload.RunnerBindings);
        Assert.Equal(_store.GetCatalogRevision(), payload.CatalogRevision);
    }

    [Fact]
    public void A_receiver_already_on_this_revision_gets_an_empty_apply()
    {
        Bind("item.blade", "1");
        var revision = _store.GetCatalogRevision();

        // E26: the short-circuit is two-term now (spec-runner-def-emit.md §3.3) — the receiver must
        // also echo the current EmitterVersion, or a compiler-code change with no revision bump would
        // never re-push.
        var payload = _push.Build(Owner(), Lawn(), matchSeed: 7,
            receiverRevision: revision, receiverEmitterVersion: AtomPushCodec.EmitterVersion);

        Assert.True(payload.UpToDate);
        Assert.Empty(payload.RunnerBindings);
        Assert.Empty(payload.Grants);
        Assert.Empty(payload.Defs);
    }

    [Fact]
    public void An_up_to_date_reply_still_carries_the_content_hash_and_the_seed()
    {
        // A reconnect that delivers no content must still make a content mismatch visible, and the
        // injector still needs the seed to roll reproducibly.
        Bind("item.blade", "1");

        var payload = _push.Build(
            Owner(), Lawn(), matchSeed: 99, matchKey: "m1",
            receiverRevision: _store.GetCatalogRevision(), receiverEmitterVersion: AtomPushCodec.EmitterVersion);

        Assert.True(payload.UpToDate);
        Assert.False(string.IsNullOrEmpty(payload.ContentHash));
        Assert.Equal(99UL, payload.MatchSeed);
        Assert.Equal("m1", payload.MatchKey);
    }

    [Fact]
    public void A_stale_revision_gets_the_full_set_again_not_a_delta()
    {
        Bind("item.blade", "1");

        var payload = _push.Build(Owner(), Lawn(), matchSeed: 7, receiverRevision: -1);

        Assert.False(payload.UpToDate);
        Assert.NotEmpty(payload.RunnerBindings);
    }

    [Fact]
    public void An_owner_with_no_bindings_gets_a_payload_not_an_error()
    {
        // A match with no pushed bindings runs with none. That is a normal state, not an error.
        var payload = _push.Build(Owner("does-not-exist"), Lawn(), matchSeed: 7);

        Assert.Empty(payload.RunnerBindings);
        Assert.False(payload.UpToDate);
    }

    [Fact]
    public void Only_the_requested_owners_bindings_travel()
    {
        Bind("item.blade", "1");
        Bind("item.blade", "2");

        var mine = _push.Build(Owner("1"), Lawn(), matchSeed: 7);

        Assert.NotEmpty(mine.RunnerBindings);
        Assert.All(mine.RunnerBindings, b => Assert.Equal("1", b.OwnerKey));
    }

    // ---- the (binding, atom) identity ---------------------------------------------------------------

    [Fact]
    public void A_container_with_two_runner_atoms_yields_two_independently_keyed_bindings()
    {
        // Two runner atoms in one container need two ICD clocks and two caps. A shared id would
        // merge them AND tie the evaluation sort, making order depend on how rows happened to arrive.
        var bindingId = Bind("item.blade", "1");

        var payload = _push.Build(Owner(), Lawn(), matchSeed: 7);

        Assert.Equal(2, payload.RunnerBindings.Count);
        Assert.Equal(2, payload.RunnerBindings.Select(b => b.BindingId).Distinct().Count());
        Assert.All(payload.RunnerBindings, b => Assert.StartsWith(bindingId + "#", b.BindingId));
    }

    [Fact]
    public void The_delivered_set_builds_a_trigger_index_with_no_duplicate_slots()
    {
        Bind("item.blade", "1");

        var payload = _push.Build(Owner(), Lawn(), matchSeed: 7);
        var index = TriggerIndex.Build(AtomPushCodec.DecodeBindings(payload));

        Assert.Equal(2, index.Count);
        Assert.Equal(2, index.SlotsFor(AtomTriggers.OnDamageDealt).Length);
    }

    [Fact]
    public void The_cap_that_routed_an_atom_to_the_runner_arrives_intact()
    {
        Bind("item.blade", "1");

        var payload = _push.Build(Owner(), Lawn(), matchSeed: 7);
        var entries = AtomPushCodec.DecodeBindings(payload).Select(b => b.Entry).ToList();

        var economy = Assert.Single(entries.Where(e => e.KindId == "resource.economy"));
        Assert.Equal(3, economy.Limits.CapPerMatch);
    }

    [Fact]
    public void A_compiled_atom_travels_as_a_grant_not_as_a_runner_entry()
    {
        // The compile/run split, end to end: a permanent modifier is an ordinary Foundation grant
        // with zero runtime cost, and only what Foundation cannot express reaches the runner.
        Bind("trait.stalwart", "1");

        var payload = _push.Build(Owner(), Lawn(), matchSeed: 7);

        Assert.Empty(payload.RunnerBindings);
        Assert.NotEmpty(payload.Grants);
        Assert.NotEmpty(payload.Defs);
    }

    // ---- the guarantee -------------------------------------------------------------------------------

    [Fact]
    public void The_payload_carries_no_content_row_of_any_kind()
    {
        Bind("item.blade", "1");
        Bind("trait.stalwart", "1");

        var json = JsonSerializer.Serialize(_push.Build(Owner(), Lawn(), matchSeed: 7, matchKey: "m1"), Wire);

        foreach (var column in new[]
                 {
                     "when_json", "params_json", "tags_json", "points_json", "overrides_json",
                     "family_id", "container_id", "curve_id", "rarity_id", "group_key",
                     "instance_id", "roll_seed", "power_json",
                 })
            Assert.DoesNotContain(column, json, StringComparison.OrdinalIgnoreCase);
    }

    // ---- the per-match seed (D5) ---------------------------------------------------------------------

    [Fact]
    public void The_seed_for_a_match_key_is_stable_across_calls()
    {
        Assert.Equal(MatchSeed.For("m-123"), MatchSeed.For("m-123"));
    }

    [Fact]
    public void Different_match_keys_get_different_seeds()
    {
        Assert.NotEqual(MatchSeed.For("m-123"), MatchSeed.For("m-124"));
    }

    [Fact]
    public void The_seed_is_a_named_hash_not_the_randomised_runtime_one()
    {
        // String.GetHashCode is randomised per process: using it would make "same match key, same
        // rolls" false on every restart, silently. FNV-1a of "m-123", computed independently here.
        var expected = 14695981039346656037UL;
        foreach (var ch in "m-123")
        {
            expected ^= ch;
            expected *= 1099511628211UL;
        }

        Assert.Equal(expected, MatchSeed.For("m-123"));
    }

    [Fact]
    public void An_absent_match_key_seeds_zero_rather_than_throwing()
    {
        Assert.Equal(0UL, MatchSeed.For(null));
        Assert.Equal(0UL, MatchSeed.For(""));
    }
}
