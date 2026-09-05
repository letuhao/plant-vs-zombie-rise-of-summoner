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

        // And exactly one compiled GRANT too -- see the finding below for why this is one grant
        // representing both owners, not two.
        Assert.Single(payload.Grants.Where(g => g.EffectId == AtomRow.DeriveId("atom.might", "", 1)));
    }

    // ---- a real gap, found while building this: compiled grants are not owner-scoped today --------

    [Fact]
    public void FINDING_a_specimens_compiled_grant_is_not_scoped_to_it_it_reaches_match_scope()
    {
        // item-ideal.md, equip-runtime (module 5), P1.5's "live lawn push" deferral said the missing
        // piece was purely "extend AtomPushService to merge owner scopes" -- verified here to be only
        // PART of the picture. AtomCompiler.EmitDefAndGrant (AtomCompiler.cs) never stamps an
        // EffectGrantDto's OwnerKey at all, so every COMPILED (non-runner) grant defaults to
        // EffectOwnerKeys.Match regardless of which owner's binding produced it -- true for the
        // pre-existing Player-only push too, not something this pass introduced.
        //
        // For a Player this is harmless (a player has no single "live entity" to scope to, so
        // match-wide is the correct scope for their own passive buffs). For a UniqueActor SPECIMEN --
        // a specific live entity on the lawn -- this means a passive stat.derived/stat.modify item
        // (no trigger, so it compiles rather than routing to the runner) would apply MATCH-WIDE
        // instead of to that one specimen: silently wrong, not merely incomplete.
        //
        // The fix exists in shipped code but has never been wired to this push: UniqueOwnerBinder
        // (src/FusionRpg.Core/Match/UniqueOwnerBinder.cs) rewrites a durable "instance:{guid}" owner
        // key to a live "entity:{ptr}" one -- but nothing in the codebase has ever constructed an
        // "instance:" key (confirmed by a whole-repo grep, src/ only), and UniqueOwnerBinder.BindGrant
        // is only ever called from UniqueLoadoutSpec (a specimen's own innate-kit grants, bound at
        // spawn) -- never from anything equip-runtime related. Wiring this for equipped items needs:
        // (1) this server-side push to stamp "instance:" + specimenId on a UniqueActor-sourced
        // compiled grant, and (2) an INJECTOR-side call to UniqueOwnerBinder.BindGrant at the moment a
        // specimen's live ptr becomes known, mirroring UniqueLoadoutSpec's own pattern -- an Injector
        // change GrantedDerivedAtomReader's own doc comment says cannot be verified by any test CI
        // runs (net6.0 + BepInEx/Il2Cpp interop, needs a real PVZ Fusion install). Named here as the
        // real next concrete step, not "not attempted" -- the earlier claim that no Injector edit is
        // needed holds for the RUNNER path (proven above) but not this one.
        Bind("trait.foundation", OwnerKind.UniqueActor, "specimen-abc");

        var payload = _push.Build(
            new[] { new OwnerScope(OwnerKind.UniqueActor, "specimen-abc") },
            Lawn(), matchSeed: 7);

        var grant = Assert.Single(payload.Grants);
        Assert.Equal(FusionRpg.Contracts.EffectOwnerKeys.Match, grant.OwnerKey);
        Assert.NotEqual("specimen-abc", grant.OwnerKey);
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
