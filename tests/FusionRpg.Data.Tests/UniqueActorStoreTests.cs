using FusionRpg.Contracts;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

public class UniqueActorStoreTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;
    readonly long _playerId;

    public UniqueActorStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-unique-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        _playerId = _store.GetCurrentPlayerId();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    [Fact]
    public void Create_starts_Roster_and_lists()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 3);
        Assert.Equal(UniqueActorPhases.Roster, a.Phase);
        Assert.Equal(3, a.TypeId);
        Assert.False(string.IsNullOrWhiteSpace(a.InstanceId));
        var list = _store.ListUniqueActors(_playerId);
        Assert.Contains(list.Items, x => x.InstanceId == a.InstanceId);
        Assert.Equal(a.InstanceId, _store.GetUniqueActor(a.InstanceId)!.InstanceId);
    }

    [Fact]
    public void Deploy_ack_idempotent_corr_then_ActiveBound()
    {
        var a = _store.CreateUniqueActor(_playerId, "zombie", 1);
        var corr = "corr-" + Guid.NewGuid().ToString("N");

        var d1 = _store.TryBeginUniqueDeploy(a.InstanceId, corr, "m-1");
        Assert.True(d1.Ok);
        Assert.True(d1.Queued);
        Assert.Equal(UniqueActorPhases.Deploying, d1.Actor!.Phase);
        Assert.Equal(corr, d1.Actor.DeployCorrelationId);

        var d2 = _store.TryBeginUniqueDeploy(a.InstanceId, corr, "m-1");
        Assert.True(d2.Ok);
        Assert.False(d2.Queued);

        var ack = _store.TryAckUniqueSpawn(corr, "0xABC", "m-1");
        Assert.True(ack.Ok);
        Assert.Equal(UniqueActorPhases.ActiveBound, ack.Actor!.Phase);
        Assert.Equal("0xABC", ack.Actor.LastPtr);
        Assert.Equal("m-1", ack.Actor.MatchKey);
    }

    [Fact]
    public void Fail_deploy_returns_Roster()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 2);
        Assert.True(_store.TryBeginUniqueDeploy(a.InstanceId, "c-fail").Ok);
        var fail = _store.TryFailUniqueDeploy(a.InstanceId);
        Assert.True(fail.Ok);
        Assert.Equal(UniqueActorPhases.Roster, fail.Actor!.Phase);
        Assert.Null(fail.Actor.DeployCorrelationId);
    }

    [Fact]
    public void Observe_die_and_board_end_recover_to_Roster()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 5);
        var corr = "corr-die";
        Assert.True(_store.TryBeginUniqueDeploy(a.InstanceId, corr, "m-die").Ok);
        Assert.True(_store.TryAckUniqueSpawn(corr, "0xDIE", "m-die").Ok);

        _store.ObserveUniqueActorEvents(new (string Kind, string? MatchKey, string PayloadJson)[]
        {
            ("plant.die", "m-die", """{"ptr":"0xDIE"}""")
        });
        Assert.Equal(UniqueActorPhases.Roster, _store.GetUniqueActor(a.InstanceId)!.Phase);

        var b = _store.CreateUniqueActor(_playerId, "zombie", 6);
        var corr2 = "corr-end";
        Assert.True(_store.TryBeginUniqueDeploy(b.InstanceId, corr2, "m-end").Ok);
        Assert.True(_store.TryAckUniqueSpawn(corr2, "0xEND", "m-end").Ok);
        _store.ObserveUniqueActorEvents(new (string Kind, string? MatchKey, string PayloadJson)[]
        {
            ("board.end", "m-end", "{}")
        });
        Assert.Equal(UniqueActorPhases.Roster, _store.GetUniqueActor(b.InstanceId)!.Phase);
        Assert.Null(_store.GetUniqueActor(b.InstanceId)!.LastPtr);
    }

    [Fact]
    public void Retire_from_Roster_and_illegal_from_Deploying()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 1);
        Assert.True(_store.TryRetireUniqueActor(a.InstanceId).Ok);
        Assert.Equal(UniqueActorPhases.Retired, _store.GetUniqueActor(a.InstanceId)!.Phase);

        var b = _store.CreateUniqueActor(_playerId, "plant", 2);
        Assert.True(_store.TryBeginUniqueDeploy(b.InstanceId, "c-ret").Ok);
        Assert.False(_store.TryRetireUniqueActor(b.InstanceId).Ok);
    }

    [Fact]
    public void Illegal_deploy_from_ActiveBound_rejected()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 9);
        var corr = "c-act";
        Assert.True(_store.TryBeginUniqueDeploy(a.InstanceId, corr).Ok);
        Assert.True(_store.TryAckUniqueSpawn(corr, "0x1").Ok);
        var again = _store.TryBeginUniqueDeploy(a.InstanceId, "c-other");
        Assert.False(again.Ok);
        Assert.Contains("phase", again.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Observe_ack_event_to_ActiveBound()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 4);
        var corr = "corr-ack-obs";
        Assert.True(_store.TryBeginUniqueDeploy(a.InstanceId, corr, "m-ack").Ok);

        _store.ObserveUniqueActorEvents(new (string Kind, string? MatchKey, string PayloadJson)[]
        {
            ("pvz.spawn.extra.ack", "m-ack", """{"correlationId":"corr-ack-obs","ptr":"0xACK"}""")
        });
        var row = _store.GetUniqueActor(a.InstanceId)!;
        Assert.Equal(UniqueActorPhases.ActiveBound, row.Phase);
        Assert.Equal("0xACK", row.LastPtr);
        Assert.Equal("m-ack", row.MatchKey);
    }

    [Fact]
    public void Observe_zombie_die_and_match_result_recover_to_Roster()
    {
        var a = _store.CreateUniqueActor(_playerId, "zombie", 7);
        var corr = "corr-zdie";
        Assert.True(_store.TryBeginUniqueDeploy(a.InstanceId, corr, "m-z").Ok);
        Assert.True(_store.TryAckUniqueSpawn(corr, "0xZDIE", "m-z").Ok);

        _store.ObserveUniqueActorEvents(new (string Kind, string? MatchKey, string PayloadJson)[]
        {
            ("zombie.die", "m-z", """{"ptr":"0xZDIE"}""")
        });
        Assert.Equal(UniqueActorPhases.Roster, _store.GetUniqueActor(a.InstanceId)!.Phase);

        var b = _store.CreateUniqueActor(_playerId, "plant", 8);
        var corr2 = "corr-mres";
        Assert.True(_store.TryBeginUniqueDeploy(b.InstanceId, corr2, "m-res").Ok);
        Assert.True(_store.TryAckUniqueSpawn(corr2, "0xMRES", "m-res").Ok);
        _store.ObserveUniqueActorEvents(new (string Kind, string? MatchKey, string PayloadJson)[]
        {
            ("match.result", "m-res", "{}")
        });
        Assert.Equal(UniqueActorPhases.Roster, _store.GetUniqueActor(b.InstanceId)!.Phase);
        Assert.Null(_store.GetUniqueActor(b.InstanceId)!.LastPtr);
    }

    [Fact]
    public void Recover_never_exposes_Recovering_revision_bumps_by_two()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 11);
        var corr = "corr-rev";
        Assert.True(_store.TryBeginUniqueDeploy(a.InstanceId, corr, "m-rev").Ok);
        Assert.True(_store.TryAckUniqueSpawn(corr, "0xREV", "m-rev").Ok);
        var active = _store.GetUniqueActor(a.InstanceId)!;
        Assert.Equal(UniqueActorPhases.ActiveBound, active.Phase);
        var revActive = active.Revision;

        _store.ObserveUniqueActorEvents(new (string Kind, string? MatchKey, string PayloadJson)[]
        {
            ("plant.die", "m-rev", """{"ptr":"0xREV"}""")
        });
        var after = _store.GetUniqueActor(a.InstanceId)!;
        Assert.Equal(UniqueActorPhases.Roster, after.Phase);
        Assert.NotEqual(UniqueActorPhases.Recovering, after.Phase);
        Assert.True(after.Revision >= revActive + 2);
    }

    [Fact]
    public void Retire_from_ActiveBound_clears_bind_fields()
    {
        var a = _store.CreateUniqueActor(_playerId, "zombie", 12);
        var corr = "corr-retire-bound";
        Assert.True(_store.TryBeginUniqueDeploy(a.InstanceId, corr, "m-ret").Ok);
        Assert.True(_store.TryAckUniqueSpawn(corr, "0xRET", "m-ret").Ok);

        var (ok, _, row) = _store.TryRetireUniqueActor(a.InstanceId);
        Assert.True(ok);
        Assert.Equal(UniqueActorPhases.Retired, row!.Phase);
        Assert.Null(row.MatchKey);
        Assert.Null(row.LastPtr);
        Assert.Null(row.DeployCorrelationId);
    }

    [Fact]
    public void Create_plant_and_zombie_sides_preserved_through_deploy_begin()
    {
        var plant = _store.CreateUniqueActor(_playerId, "plant", 1);
        var zombie = _store.CreateUniqueActor(_playerId, "zombie", 2);
        Assert.Equal("plant", plant.Side);
        Assert.Equal("zombie", zombie.Side);

        Assert.True(_store.TryBeginUniqueDeploy(plant.InstanceId, "c-p").Ok);
        Assert.True(_store.TryBeginUniqueDeploy(zombie.InstanceId, "c-z").Ok);
        Assert.Equal("plant", _store.GetUniqueActor(plant.InstanceId)!.Side);
        Assert.Equal("zombie", _store.GetUniqueActor(zombie.InstanceId)!.Side);
    }

    [Fact]
    public void FailExpired_Deploying_returns_Roster_no_second_create()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 9);
        Assert.True(_store.TryBeginUniqueDeploy(a.InstanceId, "corr-timeout", "m-to").Ok);
        Assert.Equal(UniqueActorPhases.Deploying, _store.GetUniqueActor(a.InstanceId)!.Phase);

        var future = DateTimeOffset.UtcNow.AddMinutes(10);
        var failed = _store.FailExpiredUniqueDeploys(TimeSpan.FromSeconds(30), future);
        Assert.Single(failed);
        Assert.Equal(a.InstanceId, failed[0].InstanceId);
        var after = _store.GetUniqueActor(a.InstanceId)!;
        Assert.Equal(UniqueActorPhases.Roster, after.Phase);
        Assert.Null(after.DeployCorrelationId);

        // Same specimen can deploy again (no second Create).
        Assert.True(_store.TryBeginUniqueDeploy(a.InstanceId, "corr-timeout-2").Ok);
        Assert.Equal(1, _store.ListUniqueActors(_playerId).Items.Count(x => x.InstanceId == a.InstanceId));
    }

    [Fact]
    public void Sweep_stale_ActiveBound_without_open_run_to_Roster()
    {
        var a = _store.CreateUniqueActor(_playerId, "zombie", 4);
        var corr = "corr-sweep";
        Assert.True(_store.TryBeginUniqueDeploy(a.InstanceId, corr, "m-orphan").Ok);
        Assert.True(_store.TryAckUniqueSpawn(corr, "0xSWP", "m-orphan").Ok);
        Assert.Equal(UniqueActorPhases.ActiveBound, _store.GetUniqueActor(a.InstanceId)!.Phase);

        var n = _store.SweepStaleActiveBoundUniqueActors();
        Assert.True(n >= 1);
        Assert.Equal(UniqueActorPhases.Roster, _store.GetUniqueActor(a.InstanceId)!.Phase);
        Assert.Null(_store.GetUniqueActor(a.InstanceId)!.LastPtr);
    }

    [Fact]
    public void Sweep_preserves_ActiveBound_with_open_run()
    {
        var matchKey = "m-open-" + Guid.NewGuid().ToString("N")[..8];
        _store.InsertEvents(new[]
        {
            new EventEnvelope
            {
                T = DateTime.UtcNow.ToString("o"),
                Game = RpgConstants.GameId,
                Kind = "board.start",
                MatchKey = matchKey,
                Payload = new { levelName = "open-for-unique" }
            }
        });

        var a = _store.CreateUniqueActor(_playerId, "plant", 2);
        var corr = "corr-open";
        Assert.True(_store.TryBeginUniqueDeploy(a.InstanceId, corr, matchKey).Ok);
        Assert.True(_store.TryAckUniqueSpawn(corr, "0xOPEN", matchKey).Ok);
        Assert.Equal(UniqueActorPhases.ActiveBound, _store.GetUniqueActor(a.InstanceId)!.Phase);

        Assert.Equal(0, _store.SweepStaleActiveBoundUniqueActors());
        Assert.Equal(UniqueActorPhases.ActiveBound, _store.GetUniqueActor(a.InstanceId)!.Phase);
        Assert.Equal("0xOPEN", _store.GetUniqueActor(a.InstanceId)!.LastPtr);
    }

    [Fact]
    public void HasAnyActiveBound_true_while_bound()
    {
        Assert.False(_store.HasAnyActiveBoundUniqueActors());
        var a = _store.CreateUniqueActor(_playerId, "plant", 1);
        Assert.True(_store.TryBeginUniqueDeploy(a.InstanceId, "c-ab", "m-ab").Ok);
        Assert.True(_store.TryAckUniqueSpawn("c-ab", "0xAB", "m-ab").Ok);
        Assert.True(_store.HasAnyActiveBoundUniqueActors());
    }

    [Fact]
    public void Upsert_and_get_stat_mods_json()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 1);
        Assert.Equal("{}", _store.GetUniqueStatModsJson(a.InstanceId));
        _store.UpsertUniqueStatModsJson(a.InstanceId, """{"absolutes":{"hp":12}}""");
        Assert.Contains("12", _store.GetUniqueStatModsJson(a.InstanceId), StringComparison.Ordinal);
    }

    [Fact]
    public void Equipment_upsert_rebuilds_mods_grants_preserves_absolutes()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 1);
        _store.UpsertUniqueStatModsJson(a.InstanceId, """{"absolutes":{"hp":42},"grants":[]}""");

        var eq = _store.UpsertUniqueEquipment(a.InstanceId, "weapon", "stub.atk_ring");
        Assert.Equal(a.InstanceId, eq.InstanceId);
        Assert.Contains(eq.Items, x => x.Slot == "weapon" && x.ItemId == "stub.atk_ring");
        Assert.Contains("fx.passive_atk_flat", eq.ModsJson, StringComparison.Ordinal);
        Assert.Contains("equip-stub-atk:weapon", eq.ModsJson, StringComparison.Ordinal);
        Assert.Contains("\"hp\":42", eq.ModsJson.Replace(" ", ""), StringComparison.Ordinal);

        var cleared = _store.ClearUniqueEquipmentSlot(a.InstanceId, "weapon");
        Assert.DoesNotContain(cleared.Items, x => x.Slot == "weapon");
        Assert.DoesNotContain("fx.passive_atk_flat", cleared.ModsJson, StringComparison.Ordinal);
        Assert.Contains("42", cleared.ModsJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Equipment_empty_item_clears_slot()
    {
        var a = _store.CreateUniqueActor(_playerId, "zombie", 2);
        _store.UpsertUniqueEquipment(a.InstanceId, "trinket", "stub.butter_bead");
        Assert.Single(_store.ListUniqueEquipment(a.InstanceId));
        _store.UpsertUniqueEquipment(a.InstanceId, "trinket", "");
        Assert.Empty(_store.ListUniqueEquipment(a.InstanceId));
    }

    [Fact]
    public void Equipment_rejects_unknown_item_and_bad_slot()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 1);
        var unknown = Assert.Throws<ArgumentException>(() =>
            _store.UpsertUniqueEquipment(a.InstanceId, "weapon", "stub.nope"));
        Assert.Equal("itemId", unknown.ParamName);

        var badSlot = Assert.Throws<ArgumentException>(() =>
            _store.UpsertUniqueEquipment(a.InstanceId, "hat", "stub.atk_ring"));
        Assert.Equal("slot", badSlot.ParamName);
        Assert.Empty(_store.ListUniqueEquipment(a.InstanceId));
    }

    [Fact]
    public void Equipment_equips_a_real_relic_and_rejects_wrong_slot()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 1);
        var eq = _store.UpsertUniqueEquipment(a.InstanceId, "weapon", "relic.ashen_reliquary");
        Assert.Contains(eq.Items, x => x.Slot == "weapon" && x.ItemId == "relic.ashen_reliquary");
        Assert.Contains("fx.passive_atk_flat", eq.ModsJson, StringComparison.Ordinal);

        var mismatch = Assert.Throws<ArgumentException>(() =>
            _store.UpsertUniqueEquipment(a.InstanceId, "armor", "relic.ashen_reliquary"));
        Assert.Equal("itemId", mismatch.ParamName);
        Assert.StartsWith("slot_mismatch", mismatch.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Equipment_same_stub_two_slots_unique_grantIds()
    {
        var a = _store.CreateUniqueActor(_playerId, "zombie", 2);
        _store.UpsertUniqueEquipment(a.InstanceId, "weapon", "stub.atk_ring");
        var eq = _store.UpsertUniqueEquipment(a.InstanceId, "armor", "stub.atk_ring");
        Assert.Contains("equip-stub-atk:weapon", eq.ModsJson, StringComparison.Ordinal);
        Assert.Contains("equip-stub-atk:armor", eq.ModsJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Equipment_rebuild_preserves_flat_absolutes()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 1);
        _store.UpsertUniqueStatModsJson(a.InstanceId, """{"hp":12,"atk":3}""");
        var eq = _store.UpsertUniqueEquipment(a.InstanceId, "trinket", "stub.butter_bead");
        Assert.Contains("12", eq.ModsJson, StringComparison.Ordinal);
        Assert.Contains("3", eq.ModsJson, StringComparison.Ordinal);
        Assert.Contains("fx.butter_on_hit", eq.ModsJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Award_xp_levels_up_and_refuses_retired()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 1);
        Assert.Equal(1, a.Level);
        Assert.Equal(0, a.Xp);

        var (ok, reason, after) = _store.AwardUniqueActorXp(a.InstanceId, 150, "test");
        Assert.True(ok);
        Assert.Equal("", reason);
        Assert.Equal(2, after!.Level);
        Assert.Equal(50, after.Xp);

        Assert.True(_store.TryRetireUniqueActor(a.InstanceId).Ok);
        var refuse = _store.AwardUniqueActorXp(a.InstanceId, 10, "nope");
        Assert.False(refuse.Ok);
        Assert.Equal("phase.retired", refuse.Reason);
    }

    [Fact]
    public void Award_xp_rejects_non_finite_delta()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 1);
        Assert.Equal("bad_delta", _store.AwardUniqueActorXp(a.InstanceId, double.PositiveInfinity).Reason);
        Assert.Equal("bad_delta", _store.AwardUniqueActorXp(a.InstanceId, double.NaN).Reason);
        Assert.Equal("bad_delta", _store.AwardUniqueActorXp(a.InstanceId, 0).Reason);
        Assert.Equal(1, _store.GetUniqueActor(a.InstanceId)!.Level);
    }

    [Fact]
    public void Award_xp_does_not_touch_type_progression()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 7);
        var beforeProg = _store.GetRpgActor(_playerId, "plant", a.TypeId);
        Assert.True(_store.AwardUniqueActorXp(a.InstanceId, 250, "specimen-only").Ok);
        var afterProg = _store.GetRpgActor(_playerId, "plant", a.TypeId);
        Assert.Equal(beforeProg?.Xp ?? 0, afterProg?.Xp ?? 0);
        Assert.Equal(beforeProg?.Level ?? 1, afterProg?.Level ?? 1);
    }
}
