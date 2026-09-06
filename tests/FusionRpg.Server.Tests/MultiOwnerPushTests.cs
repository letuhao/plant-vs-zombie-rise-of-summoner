using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Power;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>
/// item-ideal.md, equip-runtime (module 5) — the live lawn push's missing half, named explicit and
/// deferred at P1.5, now closed: <see cref="AtomPushService.Build(System.Collections.Generic.IReadOnlyList{OwnerScope}, BindContext, ulong, string, long?, int?, int?)"/>
/// merges a player's own grants with every deployed <see cref="OwnerKind.UniqueActor"/> specimen's
/// equipped-item atoms into ONE compiled push — the shape <c>RpgHub.BuildApplyCommand</c> now sends.
///
/// <para>Same law <see cref="CompiledPushTests"/> already proves for a single owner: nothing here is a
/// live-game claim. It proves the compiled push a live match would receive is correct BEFORE reaching
/// the lawn, which is what module 5 itself named as "genuinely server-side C#, testable via
/// FusionRpg.Server.Tests without a live game."</para>
/// </summary>
public class MultiOwnerPushTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;
    readonly AtomPushService _push;

    public MultiOwnerPushTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-multiowner-push-" + Guid.NewGuid().ToString("N"));
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

    void Seed()
    {
        Add("atom.vitality", "stat.modify", """{"channel":"maxHp","op":"flat","amount":45}""");
        Add("atom.might", "stat.modify", """{"channel":"atk","op":"flat","amount":12}""");
        Add("atom.searing", "resource.delta",
            """{"amount":{"min":-120,"max":-80,"roll":"onApply"},"element":"fire"}""",
            """{"trigger":"OnDamageDealt","chance":250,"icd_ms":500}""");

        Container("trait.foundation", ContainerKind.Trait, "atom.vitality.t1");
        Container("item.gear", ContainerKind.Item, "atom.might.t1");
        Container("item.blade", ContainerKind.Item, "atom.searing.t1");

        void Add(string family, string kind, string paramsJson, string whenJson = "{}")
        {
            var result = _store.UpsertAtom(new AtomRow
            {
                AtomId = AtomRow.DeriveId(family, "", 1),
                KindId = kind, FamilyId = family, Variant = "", Tier = 1,
                Name = family, ParamsJson = paramsJson, WhenJson = whenJson,
            });
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

    static readonly PowerTuning Tuning = PowerTuning.Build(
        1, 1, 80_000, 0, 20, 680,
        1000, 25000, 250, 1000, 5000, 5000, 25000);

    string Bind(string containerId, OwnerKind ownerKind, string ownerKey, int priority = 0)
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
            OwnerKind = ownerKind,
            OwnerKey = ownerKey,
            Priority = priority,
            Source = "test",
        }, bindingId).IsOk);

        return bindingId;
    }

    static BindContext Lawn() => new(RuntimeId.Lawn);

    [Fact]
    public void A_deployed_specimens_runner_atom_travels_alongside_the_players_own()
    {
        Bind("trait.foundation", OwnerKind.Player, "1");
        Bind("item.blade", OwnerKind.UniqueActor, "specimen-abc");

        var payload = _push.Build(
            new[] { new OwnerScope(OwnerKind.Player, "1"), new OwnerScope(OwnerKind.UniqueActor, "specimen-abc") },
            Lawn(), matchSeed: 7);

        // trait.foundation (stat.modify, no trigger) compiles to a Foundation grant; item.blade
        // (resource.delta, OnDamageDealt) needs the runner. Both must be present at all -- proving
        // the specimen's binding reached the payload alongside the player's own.
        Assert.NotEmpty(payload.Grants);
        Assert.NotEmpty(payload.RunnerBindings);
        Assert.All(payload.RunnerBindings, b => Assert.Equal("specimen-abc", b.OwnerKey));
    }

    [Fact]
    public void A_specimens_runner_atom_carries_its_own_ownerkey_not_the_players()
    {
        Bind("item.blade", OwnerKind.UniqueActor, "specimen-xyz");

        var payload = _push.Build(
            new[] { new OwnerScope(OwnerKind.Player, "1"), new OwnerScope(OwnerKind.UniqueActor, "specimen-xyz") },
            Lawn(), matchSeed: 7);

        // RunnerBinding.OwnerKey is stamped per (binding, atom) directly off BindingRow.OwnerKey --
        // this is the half of the push that already carries per-owner identity correctly today.
        Assert.NotEmpty(payload.RunnerBindings);
        Assert.All(payload.RunnerBindings, b => Assert.Equal("specimen-xyz", b.OwnerKey));
    }

    [Fact]
    public void Two_owners_sharing_the_same_atom_compile_to_one_catalog_entry_not_two()
    {
        // Player and specimen both wear an item.gear-equivalent atom -- the SAME atom id, resolved
        // through two different owner scopes. The union-then-compile design means this must produce
        // exactly one Defs entry for atom.might.t1, not two independently-compiled copies (which
        // would otherwise silently double the modifier if the client ever summed by def id).
        Bind("item.gear", OwnerKind.Player, "1");
        Bind("item.gear", OwnerKind.UniqueActor, "specimen-shared");

        var payload = _push.Build(
            new[] { new OwnerScope(OwnerKind.Player, "1"), new OwnerScope(OwnerKind.UniqueActor, "specimen-shared") },
            Lawn(), matchSeed: 7);

        var mightDefs = payload.Defs.Where(d => d.EffectId == AtomRow.DeriveId("atom.might", "", 1)).ToList();
        Assert.Single(mightDefs);

        // ONE def, and now one GRANT PER OWNER pointing at it -- see the block below. The dedup this
        // test exists for is a DEF property (the compiled content), never a grant property: a grant is
        // "this owner holds this effect", and two owners holding it is two of those.
        var mightGrants = payload.Grants
            .Where(g => g.EffectId == AtomRow.DeriveId("atom.might", "", 1)).ToList();
        Assert.Equal(2, mightGrants.Count);
        Assert.Equal(
            new[] { "instance:specimen-shared", FusionRpg.Contracts.EffectOwnerKeys.Match },
            mightGrants.Select(g => g.OwnerKey).OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(2, mightGrants.Select(g => g.GrantId).Distinct(StringComparer.Ordinal).Count());
    }

    // ---- the compiled-grant gap this suite found, now closed server-side --------------------------

    [Fact]
    public void A_specimens_compiled_grant_is_scoped_to_that_specimen_not_to_the_whole_match()
    {
        // ⭐ item-ideal.md, equip-runtime (module 5), P1.5. This test previously PINNED the defect --
        // it asserted the grant came back at EffectOwnerKeys.Match -- because AtomCompiler.
        // EmitDefAndGrant never stamped an OwnerKey at all, so every COMPILED (passive, non-runner)
        // grant took the DTO default whichever owner's binding produced it. Harmless for a Player (no
        // single live entity to scope a passive buff to) and silently wrong for a UniqueActor: one
        // specimen's passive stat.derived/stat.modify gear reached every plant and zombie on the lawn.
        //
        // Closed server-side: AtomPushService now derives each binding's durable grant key from the
        // BINDING's own scope (UniqueOwnerBinder.OwnerKeyForDurableGrant) and hands the map to
        // AtomCompiler, which emits one grant per owner of an ICD group while leaving the DEF deduped
        // exactly as before.
        Bind("trait.foundation", OwnerKind.UniqueActor, "specimen-abc");

        var payload = _push.Build(
            new[] { new OwnerScope(OwnerKind.UniqueActor, "specimen-abc") },
            Lawn(), matchSeed: 7);

        var grant = Assert.Single(payload.Grants);
        Assert.Equal("instance:specimen-abc", grant.OwnerKey);
        Assert.Equal(FusionRpg.Contracts.EffectOwnerKeys.InstanceKind, grant.OwnerKind);
        Assert.NotEqual(FusionRpg.Contracts.EffectOwnerKeys.Match, grant.OwnerKey);
    }

    [Fact]
    public void A_players_own_compiled_grant_is_unchanged_still_match_scoped()
    {
        // Non-regression for the pre-existing, already-shipped Player-only push: match-wide is the
        // CORRECT scope for a player's own passive buffs, so this number must not move.
        Bind("trait.foundation", OwnerKind.Player, "1");

        var payload = _push.Build(
            new[] { new OwnerScope(OwnerKind.Player, "1") }, Lawn(), matchSeed: 7);

        var grant = Assert.Single(payload.Grants);
        Assert.Equal(FusionRpg.Contracts.EffectOwnerKeys.Match, grant.OwnerKey);
        Assert.Equal("match", grant.OwnerKind);
        Assert.Equal("atom:" + AtomRow.DeriveId("atom.vitality", "", 1), grant.GrantId);
    }

    [Fact]
    public void The_stamped_grant_is_what_UniqueOwnerBinder_turns_into_a_live_entity_key()
    {
        // ⛔ THE BOUNDARY, stated exactly. What is proven here is that the two halves CONNECT: a real
        // grant off a real push, handed to the shipped binder, comes back scoped to one live pointer.
        // What is NOT proven -- and cannot be, by anything CI runs -- is the CALLER: an injector-side
        // BindGrant at the moment a specimen's ptr becomes known, mirroring UniqueLoadoutSpec.
        // BindToPtr (UniqueLoadoutSpec.cs:91) / UniqueBoundLoadout.TryApply. The injector targets
        // net6.0 against BepInEx/Il2Cpp interop and needs a real PVZ Fusion install to build at all
        // (GrantedDerivedAtomReader's own doc comment says so, and ci.yml names ten test projects,
        // none of them the injector).
        Bind("trait.foundation", OwnerKind.UniqueActor, "specimen-abc");

        var payload = _push.Build(
            new[] { new OwnerScope(OwnerKind.UniqueActor, "specimen-abc") },
            Lawn(), matchSeed: 7);
        var stamped = Assert.Single(payload.Grants);

        var bound = FusionRpg.Core.Match.UniqueOwnerBinder.BindGrant(stamped, "0x7ffab0");

        // Upper-cased hex, no `0x`: MatchUniqueBindingsFacet.NormalizePtr's shape, shipped since W5-B.
        // See CompiledGrantOwnerScopeTests for why that is asserted rather than corrected here.
        Assert.Equal("entity:7FFAB0", bound.OwnerKey);
        Assert.False(FusionRpg.Core.Match.UniqueOwnerBinder.WouldRejectOnHot(bound.OwnerKey));
        Assert.True(FusionRpg.Core.Stats.StatApplyScope.Matches(
            bound.OwnerKey, FusionRpg.Core.Stats.StatSide.Plant, typeId: 0, entityKey: "7ffab0"));
        Assert.Equal(stamped.GrantId, bound.GrantId);
        Assert.Equal(stamped.EffectId, bound.EffectId);
    }

    [Fact]
    public void An_unbound_specimen_grant_is_refused_by_the_hot_path_never_applied_match_wide()
    {
        // The fail-closed half of the boundary above. If the injector-side call is never wired, the
        // stamped grant is REFUSED (EffectBag.Grant throws on `instance:`, StatApplyScope.Matches
        // returns false, and the injector's own grant loop logs "instance: forbidden in Hot") -- it
        // does not silently fall back to the match-wide apply this whole change exists to stop.
        Bind("trait.foundation", OwnerKind.UniqueActor, "specimen-abc");

        var payload = _push.Build(
            new[] { new OwnerScope(OwnerKind.UniqueActor, "specimen-abc") },
            Lawn(), matchSeed: 7);
        var stamped = Assert.Single(payload.Grants);

        Assert.True(FusionRpg.Core.Match.UniqueOwnerBinder.WouldRejectOnHot(stamped.OwnerKey));
        Assert.False(FusionRpg.Core.Stats.StatApplyScope.IsMatchWide(stamped.OwnerKey));
        Assert.False(FusionRpg.Core.Stats.StatApplyScope.Matches(
            stamped.OwnerKey, FusionRpg.Core.Stats.StatSide.Plant, typeId: 0, entityKey: "7ffab0"));
    }

    [Fact]
    public void An_undeployed_specimen_that_is_never_passed_contributes_nothing()
    {
        // A roster-only specimen (not ActiveBound) is simply never in the owners list -- RpgHub's own
        // job, proven separately. Here: an owner scope that IS passed but has no bindings at all
        // must not error and must not appear in the payload.
        Bind("trait.foundation", OwnerKind.Player, "1");

        var payload = _push.Build(
            new[] { new OwnerScope(OwnerKind.Player, "1"), new OwnerScope(OwnerKind.UniqueActor, "roster-only-specimen") },
            Lawn(), matchSeed: 7);

        Assert.DoesNotContain(payload.Grants, g => g.OwnerKey == "roster-only-specimen");
        Assert.DoesNotContain(payload.RunnerBindings, b => b.OwnerKey == "roster-only-specimen");
    }

    [Fact]
    public void The_single_owner_overload_is_unchanged_and_still_delegates_correctly()
    {
        // Regression pin: the pre-existing single-owner Build(...) call sites (CompiledPushTests'
        // whole suite) must keep working byte-identically now that it forwards to the multi-owner one.
        Bind("item.blade", OwnerKind.Player, "1");

        var single = _push.Build(new OwnerScope(OwnerKind.Player, "1"), Lawn(), matchSeed: 7);
        var multi = _push.Build(new[] { new OwnerScope(OwnerKind.Player, "1") }, Lawn(), matchSeed: 7);

        Assert.Equal(single.RunnerBindings.Count, multi.RunnerBindings.Count);
        Assert.Equal(
            single.RunnerBindings.Select(b => b.BindingId).OrderBy(x => x),
            multi.RunnerBindings.Select(b => b.BindingId).OrderBy(x => x));
    }

    [Fact]
    public void Zero_owners_is_refused_rather_than_silently_returning_an_empty_payload()
    {
        Assert.Throws<ArgumentException>(() => _push.Build(Array.Empty<OwnerScope>(), Lawn(), matchSeed: 7));
    }
}
