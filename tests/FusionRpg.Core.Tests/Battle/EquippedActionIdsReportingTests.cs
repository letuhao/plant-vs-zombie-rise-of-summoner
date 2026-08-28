using System.Text.Json;
using FusionRpg.Core.Battle;
using Xunit;

namespace FusionRpg.Core.Tests.Battle;

/// <summary>
/// T22 (action-todo.md, Checkpoint 5): "the auto-equipped set appears in the battle report."
/// `BattleEngine` itself has no notion of actions/skills at all — its round loop always runs the one
/// fixed basic attack (confirmed by search, not assumed) — so this is pure observability: a value that
/// rides from <see cref="BattleActorSetup"/> to <see cref="BattleActorResult"/> unread by anything in
/// between. `WebMatchService`'s own real wiring (populating the field from
/// <c>RpgStore.GetLoadoutOrAutoEquip</c>) is proven separately in `BuildSquadEquippedActionsTests.cs`
/// (FusionRpg.Server.Tests) — this file proves the engine's OWN half: it carries what it is given, and
/// carries nothing when it is given nothing.
/// </summary>
public class EquippedActionIdsReportingTests
{
    static BattleActorSetup Actor(string key, string side, IReadOnlyList<string>? equipped = null) => new()
    {
        Key = key,
        Side = side,
        SpeciesId = "test-species",
        TypeId = 10_001,
        Level = 3,
        MaxHp = BattleRuleset.BaseHp(3),
        Atk = BattleRuleset.BaseAtk(3),
        Defense = BattleRuleset.BaseDefense(3),
        EquippedActionIds = equipped,
    };

    [Fact]
    public void A_populated_EquippedActionIds_rides_from_setup_to_the_final_result()
    {
        var setup = new BattleSetup
        {
            WaveId = "test-wave",
            Squad = new[] { Actor("squad:0", "squad", new[] { "skill.fireball", "skill.heal" }) },
            Wave = new[] { Actor("wave:0", "wave") },
        };

        var report = BattleEngine.Resolve(setup, seed: 1);

        var squadActor = report.Actors.Single(a => a.Key == "squad:0");
        Assert.Equal(new[] { "skill.fireball", "skill.heal" }, squadActor.EquippedActionIds);
    }

    [Fact]
    public void An_unset_EquippedActionIds_reaches_the_result_as_null_not_an_empty_list()
    {
        var setup = new BattleSetup
        {
            WaveId = "test-wave",
            Squad = new[] { Actor("squad:0", "squad") },
            Wave = new[] { Actor("wave:0", "wave") },
        };

        var report = BattleEngine.Resolve(setup, seed: 1);

        Assert.Null(report.Actors.Single(a => a.Key == "squad:0").EquippedActionIds);
    }

    [Fact]
    public void Nothing_in_the_round_loop_reads_it_two_actors_with_different_sets_fight_identically()
    {
        // Pure observability, proven by the strongest test available: swap what each actor carries
        // and the resulting combat outcome (damage, kills, hp) must not change at all -- if anything
        // in BattleEngine ever started reading this field, this is the test that would catch it.
        BattleSetup SetupWith(IReadOnlyList<string>? squadEquip) => new()
        {
            WaveId = "test-wave",
            Squad = new[] { Actor("squad:0", "squad", squadEquip) },
            Wave = new[] { Actor("wave:0", "wave") },
        };

        var withNone = BattleEngine.Resolve(SetupWith(null), seed: 7);
        var withSomething = BattleEngine.Resolve(SetupWith(new[] { "skill.anything" }), seed: 7);

        // Strip the field itself before comparing everything else byte-for-byte.
        string Canonical(BattleReport r) => JsonSerializer.Serialize(r with
        {
            Actors = r.Actors.Select(a => a with { EquippedActionIds = null }).ToList(),
        });

        Assert.Equal(Canonical(withNone), Canonical(withSomething));
    }

    [Fact]
    public void An_unset_field_serializes_as_absent_not_as_a_null_or_empty_key()
    {
        // The exact hazard this field could reintroduce (spec-value-spec-and-curve.md's own sibling
        // lesson, BattleReport.ContentHash's doc comment): a golden that never populates this field
        // must see the SAME bytes as before it existed. Proven directly against the raw JSON, not just
        // "the object round-trips."
        var setup = new BattleSetup
        {
            WaveId = "test-wave",
            Squad = new[] { Actor("squad:0", "squad") },
            Wave = new[] { Actor("wave:0", "wave") },
        };

        var json = JsonSerializer.Serialize(BattleEngine.Resolve(setup, seed: 1));

        Assert.DoesNotContain("EquippedActionIds", json, StringComparison.Ordinal);
    }
}
