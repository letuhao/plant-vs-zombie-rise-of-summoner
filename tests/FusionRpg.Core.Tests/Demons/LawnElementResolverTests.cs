using FusionRpg.Core.Demons;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

/// <summary>
/// E27 acceptance (spec-lawn-element-bind.md). The lawn never passed <c>elementTypes:</c> into its
/// `StatContextFactory` calls, so every lawn actor resolved <c>ActorElementTypes.Neutral</c> and 196 of
/// the 267 registered derived channels were inert there — a wiring gap, not an architectural one
/// (`StatContextFactory.cs:33,61`, `InjectorCombatBridge.cs:69-83`). <see cref="LawnElementIndex"/> and
/// <see cref="LawnElementResolver"/> are the Core-testable half of the fix: the `(side, typeId) ->
/// species -> element` lookup and its per-actor-per-match cache. The Injector-side wiring (passing the
/// resolved value into <c>ForPlant</c>/<c>ForZombie</c>) needs a local build and an owner-run live check
/// — this suite proves the piece that CI can actually exercise.
/// </summary>
public class LawnElementResolverTests
{
    static DemonSpeciesDef Species(
        string id, string side, int gameTypeId,
        ElementTypeId primary = ElementTypeId.Fire, ElementTypeId? secondary = null) => new()
    {
        SpeciesId = id,
        Name = id,
        Side = side,
        GameTypeId = gameTypeId,
        DemonTypeId = DemonSpeciesCatalog.DemonTypeIdFloor + gameTypeId,
        ElementPrimary = primary,
        ElementSecondary = secondary,
        BaseRarity = DemonRarity.Chaff,
        DeployMode = DemonDeployMode.PlantAvatar,
        Acquisition = DemonAcquisition.Summonable,
    };

    // ---- the index -----------------------------------------------------------------------------------

    [Fact]
    public void The_side_and_typeId_pair_is_the_key_typeId_alone_is_not_unique()
    {
        var index = new LawnElementIndex(new[]
        {
            Species("polevaulterzombie", "zombie", 3),
            Species("wallnut", "plant", 3, ElementTypeId.Earth),
        });

        Assert.True(index.TryGet("zombie", 3, out var z));
        Assert.Equal("polevaulterzombie", z.SpeciesId);
        Assert.True(index.TryGet("plant", 3, out var p));
        Assert.Equal("wallnut", p.SpeciesId);
    }

    [Fact]
    public void A_miss_returns_false_not_a_default_species()
    {
        var index = new LawnElementIndex(new[] { Species("a", "plant", 1) });

        Assert.False(index.TryGet("plant", 99, out _));
    }

    [Fact]
    public void A_duplicate_pair_keeps_the_lowest_speciesId_and_reports_the_collision()
    {
        var index = new LawnElementIndex(new[]
        {
            Species("zzz-later", "plant", 5),
            Species("aaa-earlier", "plant", 5),
        });

        Assert.True(index.TryGet("plant", 5, out var kept));
        Assert.Equal("aaa-earlier", kept.SpeciesId);

        var line = Assert.Single(index.Collisions);
        Assert.Contains("aaa-earlier", line);
        Assert.Contains("zzz-later", line);
    }

    // ---- the resolver: element mapping ----------------------------------------------------------------

    [Fact]
    public void A_known_species_resolves_its_primary_element()
    {
        var index = new LawnElementIndex(new[] { Species("s1", "plant", 10, ElementTypeId.Fire) });
        var resolver = new LawnElementResolver(index);

        var (side, elements) = resolver.Resolve("m1", "0xA", () => ("plant", 10));

        Assert.Equal("plant", side);
        Assert.Equal(ElementTypeId.Fire, elements.Primary);
        Assert.Null(elements.Secondary);
        Assert.False(elements.IsNeutral);
    }

    [Fact]
    public void Secondary_equal_to_primary_collapses_to_null_matching_BattleEngine_exactly()
    {
        // DemonSpeciesCatalog.Validate already refuses this at import, so this is belt-and-braces —
        // spec-lawn-element-bind.md §2.4 asks for it anyway: the two runtimes must construct
        // identically, corner case included, or they drift apart the way they did before E27.
        var index = new LawnElementIndex(new[]
        {
            Species("s1", "plant", 10, ElementTypeId.Fire, secondary: ElementTypeId.Fire),
        });
        var resolver = new LawnElementResolver(index);

        var (_, elements) = resolver.Resolve("m1", "0xA", () => ("plant", 10));

        Assert.Equal(ElementTypeId.Fire, elements.Primary);
        Assert.Null(elements.Secondary);
    }

    [Fact]
    public void A_real_secondary_survives()
    {
        var index = new LawnElementIndex(new[]
        {
            Species("s1", "plant", 10, ElementTypeId.Fire, secondary: ElementTypeId.Ice),
        });
        var resolver = new LawnElementResolver(index);

        var (_, elements) = resolver.Resolve("m1", "0xA", () => ("plant", 10));

        Assert.Equal(ElementTypeId.Fire, elements.Primary);
        Assert.Equal(ElementTypeId.Ice, elements.Secondary);
    }

    // ---- absent is Neutral, never a throw, never a guess ----------------------------------------------

    [Fact]
    public void No_species_for_the_pair_resolves_Neutral_not_a_throw()
    {
        var index = new LawnElementIndex(Array.Empty<DemonSpeciesDef>());
        var resolver = new LawnElementResolver(index);

        var (_, elements) = resolver.Resolve("m1", "0xA", () => ("plant", 404));

        Assert.True(elements.IsNeutral);
    }

    [Fact]
    public void A_miss_is_reported_once_per_typeId_per_match_not_once_per_actor()
    {
        var reports = new List<string>();
        var index = new LawnElementIndex(Array.Empty<DemonSpeciesDef>());
        var resolver = new LawnElementResolver(index, reports.Add);

        resolver.Resolve("m1", "0xA", () => ("plant", 404));
        resolver.Resolve("m1", "0xB", () => ("plant", 404)); // different actor, same typeId
        resolver.Resolve("m1", "0xC", () => ("plant", 405)); // a different miss

        Assert.Equal(2, reports.Count);
    }

    [Fact]
    public void Planted_violation_an_undefined_ElementPrimary_resolves_Neutral_and_reports()
    {
        // Simulates corrupted/unparseable import data: a species row whose ElementPrimary is not one
        // of the six defined ElementTypeId members. ActorElementTypes.Create performs no such check
        // itself (an enum cast is never range-checked at runtime), so the resolver owns this guard.
        var bad = Species("s1", "plant", 10) with { ElementPrimary = (ElementTypeId)99 };
        var index = new LawnElementIndex(new[] { bad });
        var reports = new List<string>();
        var resolver = new LawnElementResolver(index, reports.Add);

        var (_, elements) = resolver.Resolve("m1", "0xA", () => ("plant", 10));

        Assert.True(elements.IsNeutral);
        Assert.Single(reports);
        Assert.Contains("s1", reports[0]);
    }

    // ---- the cache: once per actor per match, cleared on match change ---------------------------------

    [Fact]
    public void A_repeat_resolve_for_the_same_actor_in_the_same_match_never_calls_boardLookup_again()
    {
        var index = new LawnElementIndex(new[] { Species("s1", "plant", 10) });
        var resolver = new LawnElementResolver(index);
        var lookups = 0;

        (string, int) BoardLookup() { lookups++; return ("plant", 10); }

        resolver.Resolve("m1", "0xA", BoardLookup);
        resolver.Resolve("m1", "0xA", BoardLookup);
        resolver.Resolve("m1", "0xA", BoardLookup);

        Assert.Equal(1, lookups);
        Assert.Equal(3, resolver.ResolveCallCount);
        Assert.Equal(1, resolver.BoardLookupCount);
    }

    [Fact]
    public void A_different_actor_in_the_same_match_gets_its_own_board_lookup()
    {
        var index = new LawnElementIndex(new[]
        {
            Species("s1", "plant", 10),
            Species("s2", "zombie", 20),
        });
        var resolver = new LawnElementResolver(index);

        resolver.Resolve("m1", "0xA", () => ("plant", 10));
        resolver.Resolve("m1", "0xB", () => ("zombie", 20));

        Assert.Equal(2, resolver.BoardLookupCount);
    }

    [Fact]
    public void A_match_key_change_clears_the_cache_a_pointer_can_be_reused_by_a_different_entity()
    {
        var index = new LawnElementIndex(new[]
        {
            Species("s1", "plant", 10, ElementTypeId.Fire),
            Species("s2", "zombie", 20, ElementTypeId.Ice),
        });
        var resolver = new LawnElementResolver(index);

        var first = resolver.Resolve("m1", "0xA", () => ("plant", 10));
        Assert.Equal(ElementTypeId.Fire, first.Elements.Primary);

        // Same pointer, a new match, a different entity behind it — must NOT reuse m1's cached fire.
        var second = resolver.Resolve("m2", "0xA", () => ("zombie", 20));

        Assert.Equal(ElementTypeId.Ice, second.Elements.Primary);
        Assert.Equal(2, resolver.BoardLookupCount);
    }

    [Fact]
    public void A_miss_reported_in_one_match_is_reported_again_in_the_next()
    {
        // The report-once dedup is per match, not process-lifetime — a genuinely missing species stays
        // visible across every match it recurs in, not just the first.
        var reports = new List<string>();
        var index = new LawnElementIndex(Array.Empty<DemonSpeciesDef>());
        var resolver = new LawnElementResolver(index, reports.Add);

        resolver.Resolve("m1", "0xA", () => ("plant", 404));
        resolver.Resolve("m2", "0xA", () => ("plant", 404));

        Assert.Equal(2, reports.Count);
    }
}
