using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Power;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// E6's bind gate, on the path that actually runs it.
///
/// <para><b>Why this file exists.</b> <c>BindGate</c> shipped with 34 passing tests and was called
/// from nowhere but those tests — the same defect as E4's validator not wiring E2 and E3. A gate
/// exercised only by its own unit tests protects nothing.</para>
///
/// <para>The separation these pin: <c>Bind</c> is <b>persistence</b> and checks grammar and
/// existence, because a durable binding outlives any one runtime. The runtime and scope gate belongs
/// to <c>ResolveBindings</c>, where a host asks "what may execute here" — the same stored binding is
/// legal on the lawn and refused in battle, which is correct rather than a bug.</para>
/// </summary>
public class BindResolutionTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public BindResolutionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-bindres-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        Seed();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    void Seed()
    {
        Add("atom.warding", "stat.modify", "{\"channel\":\"defense\",\"op\":\"flat\",\"amount\":10}");
        Add("atom.vitality", "stat.modify", "{\"channel\":\"maxHp\",\"op\":\"flat\",\"amount\":45}");
        Add("atom.cherry", "board.action", "{\"op\":\"cherry\"}", "{\"trigger\":\"OnDeath\"}");

        Container("trait.warded", "atom.warding.t1");
        Container("trait.stalwart", "atom.vitality.t1");
        Container("trait.groundskeeper", "atom.cherry.t1");

        void Add(string family, string kind, string paramsJson, string whenJson = "{}") =>
            Assert.True(_store.UpsertAtom(new AtomRow
            {
                AtomId = AtomRow.DeriveId(family, "", 1),
                KindId = kind, FamilyId = family, Variant = "", Tier = 1,
                ParamsJson = paramsJson, WhenJson = whenJson,
            }).IsOk, family);

        void Container(string id, string atomId) =>
            Assert.True(_store.UpsertContainer(new ContainerRow
            {
                ContainerId = id, Kind = ContainerKind.Trait,
                Atoms = new[] { new ContainerAtomRow(1, atomId) },
            }).IsOk, id);
    }

    // T3.4 (content-scale): 20 is the pin -- contentScale(20) == 1.000 exactly.
    static readonly PowerTuning Tuning = PowerTuning.Build(
        1, 1, 80_000, 0, 20, 680, // fixed anchor (Fixed* consts are `internal` to Core+Core.Tests only)
        1000, 25000, 250, 1000, 5000, 5000, 25000);

    string BindOf(string containerId, OwnerKind kind, string key, string source = "test")
    {
        var container = _store.GetContainer(containerId)!;
        var atoms = _store.ListAtoms().ToDictionary(a => a.AtomId, StringComparer.Ordinal);

        Assert.True(Instantiator.TryInstantiate(container,
            id => atoms.TryGetValue(id, out var a) ? a : null, _store.GetAffix, 1, 20, Tuning, out var inst).IsOk);

        var instanceId = _store.SaveInstance(inst!);
        var bindingId = Guid.NewGuid().ToString("N");

        Assert.True(_store.Bind(new BindingRow
        {
            InstanceId = instanceId, OwnerKind = kind, OwnerKey = key, Source = source,
        }, bindingId).IsOk);

        return bindingId;
    }

    // ---- G8 on the real path -------------------------------------------------------------------

    [Fact]
    public void A_defense_binding_at_a_narrow_scope_is_refused_by_resolution()
    {
        // The TakeDamage prefix reads one side-wide cached value, so this binding would apply to
        // nothing at all. It must not reach a host.
        BindOf("trait.warded", OwnerKind.Plant, "7");

        var resolved = _store.ResolveBindings(new OwnerScope(OwnerKind.Plant, "7"),
            new BindContext(RuntimeId.Lawn));

        Assert.Empty(resolved.Bindings);
        Assert.Single(resolved.Refused);
        Assert.Equal(AtomRejectionReason.ScopeUnsupported, resolved.Refused[0].Reason);
    }

    [Fact]
    public void The_same_defense_binding_resolves_at_match_scope()
    {
        BindOf("trait.warded", OwnerKind.Match, "");

        var resolved = _store.ResolveBindings(OwnerScope.Match, new BindContext(RuntimeId.Lawn));

        Assert.Single(resolved.Bindings);
        Assert.Empty(resolved.Refused);
    }

    // ---- the runtime matrix on the real path ------------------------------------------------------

    [Fact]
    public void A_board_kind_binding_does_not_resolve_in_battle()
    {
        BindOf("trait.groundskeeper", OwnerKind.Match, "");

        var lawn = _store.ResolveBindings(OwnerScope.Match, new BindContext(RuntimeId.Lawn));
        var battle = _store.ResolveBindings(OwnerScope.Match, new BindContext(RuntimeId.Battle));

        Assert.Single(lawn.Bindings);
        Assert.Empty(battle.Bindings);
        Assert.Equal(AtomRejectionReason.RuntimeUnsupported, battle.Refused[0].Reason);
    }

    [Fact]
    public void A_plan_only_kind_resolves_only_for_a_planner_host()
    {
        BindOf("trait.stalwart", OwnerKind.Match, "");

        Assert.Empty(_store.ResolveBindings(OwnerScope.Match, new BindContext(RuntimeId.Sim)).Bindings);
        Assert.Single(_store.ResolveBindings(OwnerScope.Match,
            new BindContext(RuntimeId.Sim, IsPlanner: true)).Bindings);
    }

    [Fact]
    public void One_refused_binding_does_not_hide_the_others()
    {
        // Whole-binding rejection, not whole-owner: a bad trait must not silently disarm a good one.
        BindOf("trait.warded", OwnerKind.Plant, "7", "bad");
        BindOf("trait.stalwart", OwnerKind.Plant, "7", "good");

        var resolved = _store.ResolveBindings(new OwnerScope(OwnerKind.Plant, "7"),
            new BindContext(RuntimeId.Lawn));

        Assert.Equal(new[] { "good" }, resolved.Bindings.Select(b => b.Source));
        Assert.Single(resolved.Refused);
    }

    [Fact]
    public void Resolution_preserves_the_effect_list_order()
    {
        var owner = new OwnerScope(OwnerKind.Player, "1");
        BindOf("trait.stalwart", OwnerKind.Player, "1", "low");
        BindOf("trait.stalwart", OwnerKind.Player, "1", "high");

        // Priority is equal here, so the content tiebreak decides — and resolution must not reorder.
        var stored = _store.ListBindings(owner).Select(b => b.BindingId).ToList();
        var resolved = _store.ResolveBindings(owner, new BindContext(RuntimeId.Lawn))
            .Bindings.Select(b => b.BindingId).ToList();

        Assert.Equal(stored, resolved);
    }

    // ---- catalog_revision: what makes StaleInstance detectable ------------------------------------

    [Fact]
    public void An_instance_records_the_catalog_revision_it_was_rolled_against()
    {
        var revision = _store.GetCatalogRevision();
        var id = BindOf("trait.stalwart", OwnerKind.Match, "");

        var binding = _store.ListBindings(OwnerScope.Match).Single(b => b.BindingId == id);

        Assert.Equal(revision, _store.GetInstance(binding.InstanceId)!.CatalogRevision);
    }

    [Fact]
    public void A_binding_rolled_against_an_older_catalog_is_refused_as_stale()
    {
        // Reproducibility is claimed over (container, catalog_revision, roll_seed). Without the
        // revision on the instance there is nothing to compare, and a content edit silently changes
        // what an owned item means.
        BindOf("trait.stalwart", OwnerKind.Match, "");
        _store.BumpCatalogRevision();

        var resolved = _store.ResolveBindings(OwnerScope.Match, new BindContext(RuntimeId.Lawn));

        Assert.Empty(resolved.Bindings);
        Assert.Equal(AtomRejectionReason.StaleInstance, resolved.Refused[0].Reason);
    }

    [Fact]
    public void A_current_instance_is_not_stale()
    {
        BindOf("trait.stalwart", OwnerKind.Match, "");

        Assert.Single(_store.ResolveBindings(OwnerScope.Match, new BindContext(RuntimeId.Lawn)).Bindings);
    }

    // ---- orphan instances ---------------------------------------------------------------------

    [Fact]
    public void Clearing_session_bindings_does_not_leave_orphan_instances()
    {
        // entity: bindings are dropped at match end. The instances they pointed at have no other
        // owner and can never be reached again -- so every match would leak rows into a durable
        // database forever.
        for (var i = 0; i < 5; i++)
            BindOf("trait.stalwart", OwnerKind.Entity, "abc" + i);

        _store.ClearSessionScopedBindings();

        Assert.Equal(0, _store.CountOrphanInstances());
    }

    [Fact]
    public void Withdrawing_the_last_binding_does_not_leave_an_orphan_instance()
    {
        var id = BindOf("trait.stalwart", OwnerKind.Player, "1");

        _store.Withdraw(id);

        Assert.Equal(0, _store.CountOrphanInstances());
    }

    [Fact]
    public void An_instance_with_a_surviving_binding_is_never_collected()
    {
        BindOf("trait.stalwart", OwnerKind.Player, "1", "keep");
        var drop = BindOf("trait.stalwart", OwnerKind.Player, "1", "drop");

        _store.Withdraw(drop);

        Assert.Single(_store.ListBindings(new OwnerScope(OwnerKind.Player, "1")));
        Assert.Equal(0, _store.CountOrphanInstances());
    }
}
