using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Eligibility;
using FusionRpg.Core.Actions.Unlock;
using FusionRpg.Core.Effects.Atoms;
using Xunit;

namespace FusionRpg.Core.Tests.Actions;

/// <summary>
/// A-E1 (spec-eligibility-axis.md): the founding gap this program named — "nothing in the code can
/// express who may hold an action." Tests 1-8 mirror the spec's own §5 numbering; test 3 is written
/// first per the spec's own instruction ("the difference between a working eligibility system and one
/// that silently grants every species action to everybody").
/// </summary>
public class EligibilityAxisTests
{
    static ActionRow Row(string id, EligibilityScope scope, string? scopeKey) => new()
    {
        ActionId = id,
        Name = id,
        Kind = ActionKind.Skill,
        ContainerId = "",
        Scope = scope,
        ScopeKey = scopeKey,
    };

    static readonly IReadOnlyDictionary<string, string> FamilyOf = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["cherrybomb"] = "cherry",
        ["cactus"] = "cactus",
    };

    /// <summary>Test 3, written first per the spec's own instruction: a species-scoped row with a
    /// null <see cref="ActionRow.ScopeKey"/> must never appear for an actor whose own key is ALSO
    /// null/empty — the exact "two nulls compare equal" accident that would make a mis-authored row
    /// universal.</summary>
    [Fact]
    public void PlantedViolation_a_species_row_with_a_null_scopeKey_never_matches_an_actor_with_no_key()
    {
        var rows = new[] { Row("action.species.broken.001", EligibilityScope.Species, scopeKey: null) };

        var candidatesForNullActor = ActionEligibility.Candidates(rows, actorSpeciesKey: null, FamilyOf);
        var candidatesForEmptyActor = ActionEligibility.Candidates(rows, actorSpeciesKey: "", FamilyOf);
        var candidatesForRealActor = ActionEligibility.Candidates(rows, actorSpeciesKey: "cherrybomb", FamilyOf);

        Assert.Empty(candidatesForNullActor);
        Assert.Empty(candidatesForEmptyActor);
        Assert.Empty(candidatesForRealActor);
    }

    [Fact]
    public void Candidates_returns_general_plus_the_actors_family_and_species_rows_and_nothing_else()
    {
        var rows = new[]
        {
            Row("action.general.0001", EligibilityScope.General, null),
            Row("action.general.0002", EligibilityScope.General, null),
            Row("action.family.cherry.001", EligibilityScope.Family, "cherry"),
            Row("action.family.cactus.001", EligibilityScope.Family, "cactus"), // a DIFFERENT family
            Row("action.species.cherrybomb.001", EligibilityScope.Species, "cherrybomb"),
            Row("action.species.other.001", EligibilityScope.Species, "somethingelse"), // a DIFFERENT species
        };

        var result = ActionEligibility.Candidates(rows, actorSpeciesKey: "cherrybomb", FamilyOf);

        Assert.Equal(
            new[]
            {
                "action.family.cherry.001",
                "action.general.0001",
                "action.general.0002",
                "action.species.cherrybomb.001",
            },
            result.Select(r => r.ActionId));
    }

    [Fact]
    public void An_actor_with_an_unknown_family_gets_general_tier_rows_only()
    {
        var rows = new[]
        {
            Row("action.general.0001", EligibilityScope.General, null),
            Row("action.family.cherry.001", EligibilityScope.Family, "cherry"),
        };

        // "unassigned-species" is not a key in FamilyOf at all (§7: the family tier reaches 53/84),
        // so familyOf(actor) misses and only the general tier is reachable — the species tier is a
        // separate, direct match this actor also has none of.
        var result = ActionEligibility.Candidates(rows, actorSpeciesKey: "unassigned-species", FamilyOf);

        Assert.Equal(new[] { "action.general.0001" }, result.Select(r => r.ActionId));
    }

    [Fact]
    public void The_candidate_set_is_ordinally_sorted_and_stable_across_two_calls()
    {
        var rows = new[]
        {
            Row("action.general.0003", EligibilityScope.General, null),
            Row("action.general.0001", EligibilityScope.General, null),
            Row("action.general.0002", EligibilityScope.General, null),
        };

        var first = ActionEligibility.Candidates(rows, actorSpeciesKey: null, FamilyOf);
        var second = ActionEligibility.Candidates(rows, actorSpeciesKey: null, FamilyOf);

        var expected = new[] { "action.general.0001", "action.general.0002", "action.general.0003" };
        Assert.Equal(expected, first.Select(r => r.ActionId));
        Assert.Equal(expected, second.Select(r => r.ActionId));
    }

    /// <summary>Test 5: <see cref="UnlockState.TryAccept"/> driven from a REAL candidate set, not a
    /// hardcoded string literal — the ladder stops being reachable only from a test fixture.</summary>
    [Fact]
    public void UnlockState_TryAccept_is_driven_from_a_real_candidate_set()
    {
        var rows = new[]
        {
            Row("action.family.cherry.001", EligibilityScope.Family, "cherry"),
            Row("action.species.other.001", EligibilityScope.Species, "somethingelse"),
        };
        var candidates = ActionEligibility.Candidates(rows, actorSpeciesKey: "cherrybomb", FamilyOf);
        Assert.Single(candidates); // only the family row — proves this is really filtered, not a fixture

        var tuning = new UnlockTuning(P1Milli: 1000, DeltaMilli: 500, FloorMilli: 1, HeldCap: 10, RungCap: 10, DiscardTaxCoeffMilli: 100);
        var state = UnlockState.Empty();
        var outcome = state.TryAccept(candidates[0].ActionId, tuning, new AlwaysHitRng());

        Assert.True(outcome.Accepted);
        Assert.Equal("action.family.cherry.001", state.Held[0].UnlockId);
    }

    /// <summary>Test 6's mirror case (the load-time refusal itself is A-C1's, per §6): a
    /// family-scoped row naming a family with no assignment appears in NO candidate set — inert
    /// rather than wrong, with no schema coupling introduced to check it here.</summary>
    [Fact]
    public void A_family_scoped_row_naming_an_unassigned_family_joins_no_candidate_set()
    {
        var rows = new[] { Row("action.family.ghost.001", EligibilityScope.Family, "no-such-family") };

        var result = ActionEligibility.Candidates(rows, actorSpeciesKey: "cherrybomb", FamilyOf);

        Assert.Empty(result);
    }

    /// <summary>Test 7: A1's closure held mechanically — exactly three scope values exist, and every
    /// string outside them is refused by the parser rather than silently accepted.</summary>
    [Fact]
    public void PlantedViolation_a_fourth_scope_value_is_refused_by_the_parser()
    {
        var all = Enum.GetValues<EligibilityScope>();
        Assert.Equal(3, all.Length);

        Assert.False(EligibilityScopes.TryParse("guild", out _));
        Assert.False(EligibilityScopes.TryParse("global", out _));
        Assert.False(EligibilityScopes.TryParse(null, out _));
    }

    /// <summary>Test 8: <see cref="ActionEffectScope"/> — a different concept sharing a similar word
    /// (§4) — is untouched by this module.</summary>
    [Fact]
    public void ActionEffectScope_is_unchanged()
    {
        Assert.Equal(4, Enum.GetValues<ActionEffectScope>().Length);
        Assert.Equal("caster", ActionEffectScopes.Name(ActionEffectScope.Caster));
        Assert.Equal("primaryTarget", ActionEffectScopes.Name(ActionEffectScope.PrimaryTarget));
        Assert.Equal("eachTarget", ActionEffectScopes.Name(ActionEffectScope.EachTarget));
        Assert.Equal("casterAllies", ActionEffectScopes.Name(ActionEffectScope.CasterAllies));
    }

    [Fact]
    public void RungBand_collapses_to_its_ceiling_not_its_floor()
    {
        var band = new RungBand(Floor: 1, Ceiling: 10);
        Assert.Equal(10, band.Collapse());

        var signature = new RungBand(Floor: 1, Ceiling: 10); // the corrected [1,10] window, not [5,10]
        Assert.Equal(10, signature.Collapse());
    }

    // ---- FamilyMap -----------------------------------------------------------------------------

    [Fact]
    public void FamilyMap_parses_a_flat_speciesKey_to_familyId_object()
    {
        var map = FamilyMap.Parse("""{"cherrybomb":"cherry","cactus":"cactus"}""");
        Assert.Equal(2, map.Count);
        Assert.Equal("cherry", map["cherrybomb"]);
    }

    [Fact]
    public void FamilyMap_parse_of_empty_string_yields_an_empty_map()
    {
        Assert.Empty(FamilyMap.Parse(""));
    }

    [Fact]
    public void FamilyMap_refuses_a_non_string_value()
    {
        Assert.Throws<InvalidOperationException>(() => FamilyMap.Parse("""{"cherrybomb":["cherry"]}"""));
    }

    /// <summary>The real committed projection (§3.2, decided 2026-09-03): 53 entries, every key an
    /// exact lowercase <c>SpeciesId</c> from the 84-row species catalog, every value one of the 19
    /// families <c>family-assignments.json</c> names — read from disk, not asserted from memory.</summary>
    [Fact]
    public void The_real_family_map_json_has_53_entries_and_matches_its_source()
    {
        var repoRoot = FindRepoRoot();
        var mapPath = Path.Combine(repoRoot, "data", "seed", "actions", "_generated", "family-map.json");
        var sourcePath = Path.Combine(repoRoot, "data", "seed", "demons", "_generated", "family-assignments.json");

        var map = FamilyMap.Parse(File.ReadAllText(mapPath));
        Assert.Equal(53, map.Count);

        var source = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(sourcePath))!;
        Assert.Equal(source.Count, map.Count);
        foreach (var (speciesKey, families) in source)
        {
            Assert.Single(families); // the relation is a function — the projection refuses otherwise
            Assert.Equal(families[0], map[speciesKey]);
        }
    }

    sealed class AlwaysHitRng : IAtomRandom
    {
        public int NextInclusive(int min, int max) => min;
        public int NextPerMille() => 0;
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "data", "seed", "actions"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);
    }
}
