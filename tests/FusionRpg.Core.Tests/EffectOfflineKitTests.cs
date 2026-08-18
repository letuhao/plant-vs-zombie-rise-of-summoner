using FusionRpg.Contracts;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Effects.Plugins;
using Xunit;

namespace FusionRpg.Core.Tests;

public class EffectOfflineKitTests
{
    [Fact]
    public void ForOwner_filters_by_key_and_kind()
    {
        var h = new FoundationHarness();
        h.Grant(new EffectGrantDto
        {
            GrantId = "a",
            EffectId = "fx.butter_on_hit",
            OwnerKind = "match",
            OwnerKey = EffectOwnerKeys.Match,
            Overlay = new Dictionary<string, object?> { ["icd_ms"] = 0 }
        });
        h.Grant(new EffectGrantDto
        {
            GrantId = "b",
            EffectId = "fx.freeze_on_hit",
            OwnerKind = "plant",
            OwnerKey = EffectOwnerKeys.PlantType(0),
            Overlay = new Dictionary<string, object?> { ["icd_ms"] = 0 }
        });

        Assert.Single(h.Bag.ForOwner(null, EffectOwnerKeys.Match));
        Assert.Single(h.Bag.ForOwner("plant", EffectOwnerKeys.PlantType(0)));
        Assert.Empty(h.Bag.ForOwner("zombie", EffectOwnerKeys.PlantType(0)));
    }

    [Fact]
    public void ForOwner_matches_entity_key_with_0x_normalize()
    {
        var h = new FoundationHarness();
        h.Grant(new EffectGrantDto
        {
            GrantId = "e",
            EffectId = "fx.passive_atk_flat",
            OwnerKey = EffectOwnerKeys.Entity("0xAAA")
        });
        Assert.Single(h.Bag.ForOwner(null, EffectOwnerKeys.Entity("AAA")));
        Assert.Single(h.Bag.ForOwner(null, EffectOwnerKeys.Entity("aaa")));
    }

    [Fact]
    public void WithdrawForOwner_removes_entity_grants_only()
    {
        var h = new FoundationHarness();
        h.Grant(new EffectGrantDto
        {
            GrantId = "aaa",
            EffectId = "fx.passive_atk_flat",
            OwnerKey = EffectOwnerKeys.Entity("AAA")
        });
        h.Grant(new EffectGrantDto
        {
            GrantId = "bbb",
            EffectId = "fx.passive_atk_flat",
            OwnerKey = EffectOwnerKeys.Entity("BBB")
        });
        h.Grant(new EffectGrantDto
        {
            GrantId = "m",
            EffectId = "fx.butter_on_hit",
            OwnerKey = EffectOwnerKeys.Match,
            Overlay = new Dictionary<string, object?> { ["icd_ms"] = 0 }
        });

        var r0 = h.Snapshot().Revision;
        Assert.Equal(1, h.Bag.WithdrawForOwner(null, EffectOwnerKeys.Entity("AAA")));
        Assert.True(h.Snapshot().Revision > r0);
        Assert.DoesNotContain(h.Snapshot().Grants, g => g.GrantId == "aaa");
        Assert.Contains(h.Snapshot().Grants, g => g.GrantId == "bbb");
        Assert.Contains(h.Snapshot().Grants, g => g.GrantId == "m");
        Assert.Contains(h.Sink.Items, i =>
            i.Action == EffectActions.ModifyStat && JsonOverlay.GetBool(i.Params, "remove"));
    }

    [Fact]
    public void WithdrawForOwner_removes_all_grants_on_same_entity_leaves_plant_and_match()
    {
        var h = new FoundationHarness();
        h.Grant(new EffectGrantDto
        {
            GrantId = "e1",
            EffectId = "fx.passive_atk_flat",
            OwnerKey = EffectOwnerKeys.Entity("DEAD")
        });
        h.Grant(new EffectGrantDto
        {
            GrantId = "e2",
            EffectId = "fx.butter_on_hit",
            OwnerKey = EffectOwnerKeys.Entity("0xDEAD"),
            Overlay = new Dictionary<string, object?> { ["icd_ms"] = 0 }
        });
        h.Grant(new EffectGrantDto
        {
            GrantId = "plant",
            EffectId = "fx.freeze_on_hit",
            OwnerKind = "plant",
            OwnerKey = EffectOwnerKeys.PlantType(0),
            Overlay = new Dictionary<string, object?> { ["icd_ms"] = 0 }
        });
        h.Grant(new EffectGrantDto
        {
            GrantId = "m",
            EffectId = "fx.cold_on_hit",
            OwnerKey = EffectOwnerKeys.Match,
            Overlay = new Dictionary<string, object?> { ["icd_ms"] = 0 }
        });

        Assert.Equal(2, h.Bag.WithdrawForOwner(null, EffectOwnerKeys.Entity("DEAD")));
        Assert.DoesNotContain(h.Snapshot().Grants, g => g.GrantId is "e1" or "e2");
        Assert.Contains(h.Snapshot().Grants, g => g.GrantId == "plant");
        Assert.Contains(h.Snapshot().Grants, g => g.GrantId == "m");
    }

    [Fact]
    public void WithdrawForOwner_respects_ownerKind_filter()
    {
        var h = new FoundationHarness();
        h.Grant(new EffectGrantDto
        {
            GrantId = "keep",
            EffectId = "fx.passive_atk_flat",
            OwnerKind = "specimen",
            OwnerKey = EffectOwnerKeys.Entity("AAA")
        });
        h.Grant(new EffectGrantDto
        {
            GrantId = "drop",
            EffectId = "fx.butter_on_hit",
            OwnerKind = "entity",
            OwnerKey = EffectOwnerKeys.Entity("AAA"),
            Overlay = new Dictionary<string, object?> { ["icd_ms"] = 0 }
        });

        Assert.Equal(1, h.Bag.WithdrawForOwner("entity", EffectOwnerKeys.Entity("AAA")));
        Assert.Contains(h.Snapshot().Grants, g => g.GrantId == "keep");
        Assert.DoesNotContain(h.Snapshot().Grants, g => g.GrantId == "drop");
    }

    [Fact]
    public void WithdrawForOwner_matches_entity_key_ignore_case_and_0x()
    {
        var h = new FoundationHarness();
        h.Grant(new EffectGrantDto
        {
            GrantId = "a",
            EffectId = "fx.passive_atk_flat",
            OwnerKey = EffectOwnerKeys.Entity("aaa")
        });
        Assert.Equal(1, h.Bag.WithdrawForOwner(null, EffectOwnerKeys.Entity("AAA")));
        Assert.Empty(h.Snapshot().Grants);

        h.Grant(new EffectGrantDto
        {
            GrantId = "b",
            EffectId = "fx.passive_atk_flat",
            OwnerKey = EffectOwnerKeys.Entity("0xBBB")
        });
        Assert.Equal(1, h.Bag.WithdrawForOwner(null, EffectOwnerKeys.Entity("BBB")));
        Assert.Empty(h.Snapshot().Grants);
    }

    [Fact]
    public void WithdrawForOwner_null_or_empty_returns_zero()
    {
        var h = new FoundationHarness();
        h.Grant(new EffectGrantDto
        {
            GrantId = "m",
            EffectId = "fx.passive_atk_flat",
            OwnerKey = EffectOwnerKeys.Match
        });
        Assert.Equal(0, h.Bag.WithdrawForOwner(null, null));
        Assert.Equal(0, h.Bag.WithdrawForOwner(null, "  "));
        Assert.Single(h.Snapshot().Grants);
    }

    [Fact]
    public void MatchesEvent_entity_key_normalizes_0x_ptr()
    {
        var grant = EffectGrant.FromDto(new EffectGrantDto
        {
            GrantId = "g",
            EffectId = "fx.spawn_zombie_ondeath",
            OwnerKey = EffectOwnerKeys.Entity("AAA")
        });
        var ev = new EffectEventDto
        {
            Trigger = EffectTriggers.OnDeath,
            Side = "zombie",
            ActorPtr = "0xAAA",
            TargetPtr = "0xAAA",
            TypeId = 0,
            MatchKey = "m1",
            Tick = 1
        };
        Assert.True(EffectOwnerKey.MatchesEvent(grant, ev));
    }

    [Fact]
    public void MatchesEvent_instance_and_unknown_owner_never_match()
    {
        var instanceGrant = EffectGrant.FromDto(new EffectGrantDto
        {
            GrantId = "gi",
            EffectId = "fx.butter_on_hit",
            OwnerKey = "instance:guid-1"
        });
        var unknownGrant = EffectGrant.FromDto(new EffectGrantDto
        {
            GrantId = "gu",
            EffectId = "fx.butter_on_hit",
            OwnerKey = "foo:bar"
        });
        var dealt = new EffectEventDto
        {
            Trigger = EffectTriggers.OnDamageDealt,
            Side = "plant",
            ActorPtr = "0xA",
            TargetPtr = "0xB",
            TypeId = 0,
            MatchKey = "m1",
            Tick = 1
        };
        var death = new EffectEventDto
        {
            Trigger = EffectTriggers.OnDeath,
            Side = "zombie",
            ActorPtr = "guid-1",
            TargetPtr = "guid-1",
            TypeId = 0,
            MatchKey = "m1",
            Tick = 2
        };
        Assert.False(EffectOwnerKey.MatchesEvent(instanceGrant, dealt));
        Assert.False(EffectOwnerKey.MatchesEvent(instanceGrant, death));
        Assert.False(EffectOwnerKey.MatchesEvent(unknownGrant, dealt));
    }

    [Fact]
    public void Grant_rejects_instance_ownerKey()
    {
        var h = new FoundationHarness();
        var ex = Assert.Throws<InvalidOperationException>(() => h.Grant(new EffectGrantDto
        {
            GrantId = "bad",
            EffectId = "fx.passive_atk_flat",
            OwnerKey = "instance:guid-1"
        }));
        Assert.Contains("instance:", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(h.Snapshot().Grants);

        var exCase = Assert.Throws<InvalidOperationException>(() => h.Grant(new EffectGrantDto
        {
            GrantId = "bad2",
            EffectId = "fx.passive_atk_flat",
            OwnerKey = "INSTANCE:GUID"
        }));
        Assert.Contains("instance:", exCase.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MatchesEvent_player_owner_still_matches()
    {
        var grant = EffectGrant.FromDto(new EffectGrantDto
        {
            GrantId = "gp",
            EffectId = "fx.butter_on_hit",
            OwnerKey = EffectOwnerKeys.Player(1),
            Overlay = new Dictionary<string, object?> { ["icd_ms"] = 0 }
        });
        var dealt = new EffectEventDto
        {
            Trigger = EffectTriggers.OnDamageDealt,
            Side = "plant",
            ActorPtr = "0xA",
            TargetPtr = "0xB",
            TypeId = 0,
            MatchKey = "m1",
            Tick = 1
        };
        Assert.True(EffectOwnerKey.MatchesEvent(grant, dealt));
    }

    [Fact]
    public void OnEvent_ignores_store_bypassed_instance_grant()
    {
        var h = new FoundationHarness();
        // Bypass EffectBag.Grant ingress — residual store path must still fail-closed on events.
        h.Bag.Grants.Upsert(EffectGrant.FromDto(new EffectGrantDto
        {
            GrantId = "forced-instance",
            EffectId = "fx.butter_on_hit",
            OwnerKey = "instance:guid-1",
            Overlay = new Dictionary<string, object?> { ["icd_ms"] = 0 }
        }));
        Assert.Contains(h.Snapshot().Grants, g => g.GrantId == "forced-instance");
        Assert.Empty(h.OnEvent(new EffectEventDto
        {
            Trigger = EffectTriggers.OnDamageDealt,
            Side = "plant",
            ActorPtr = "0xA",
            TargetPtr = "0xB",
            TypeId = 0,
            MatchKey = "m1",
            Tick = 1
        }).Actions);
    }

    [Fact]
    public void FromDto_coalesces_null_or_whitespace_OwnerKey_to_match()
    {
        var g = EffectGrant.FromDto(new EffectGrantDto
        {
            GrantId = "n",
            EffectId = "fx.butter_on_hit",
            OwnerKey = null!,
            OwnerKind = null!
        });
        Assert.Equal(EffectOwnerKeys.Match, g.OwnerKey);
        Assert.Equal("match", g.OwnerKind);
    }

    [Fact]
    public void Grant_still_accepts_entity_plant_match()
    {
        var h = new FoundationHarness();
        h.Grant(new EffectGrantDto
        {
            GrantId = "e",
            EffectId = "fx.passive_atk_flat",
            OwnerKey = EffectOwnerKeys.Entity("AAA")
        });
        h.Grant(new EffectGrantDto
        {
            GrantId = "p",
            EffectId = "fx.butter_on_hit",
            OwnerKey = EffectOwnerKeys.PlantType(0),
            Overlay = new Dictionary<string, object?> { ["icd_ms"] = 0 }
        });
        h.Grant(new EffectGrantDto
        {
            GrantId = "m",
            EffectId = "fx.freeze_on_hit",
            OwnerKey = EffectOwnerKeys.Match,
            Overlay = new Dictionary<string, object?> { ["icd_ms"] = 0 }
        });
        Assert.Equal(3, h.Snapshot().Grants.Count);
    }

    [Fact]
    public void SimEffectHost_Die_fires_entity_OnDeath_then_withdraws()
    {
        var host = new SimEffectHost();
        host.Grant(new EffectGrantDto
        {
            GrantId = "entity-death",
            EffectId = "fx.spawn_zombie_ondeath",
            OwnerKey = EffectOwnerKeys.Entity("DEAD"),
            Overlay = new Dictionary<string, object?> { ["icd_ms"] = 0 }
        });
        host.Grant(new EffectGrantDto
        {
            GrantId = "match-butter",
            EffectId = "fx.butter_on_hit",
            OwnerKey = EffectOwnerKeys.Match,
            Overlay = new Dictionary<string, object?> { ["icd_ms"] = 0 }
        });

        var plan = host.Die(side: "zombie", ptr: "0xDEAD", typeId: 0);
        Assert.Single(plan.Actions);
        Assert.Equal(EffectActions.SpawnEntity, plan.Actions[0].Action);
        Assert.Equal("entity-death", plan.Actions[0].GrantId);
        Assert.Empty(host.Bag.ForOwner(null, EffectOwnerKeys.Entity("DEAD")));
        Assert.Single(host.Bag.ForOwner(null, EffectOwnerKeys.Match));

        var hit = host.HitDealt();
        Assert.Single(hit.Actions);
        Assert.Equal("match-butter", hit.Actions[0].GrantId);
    }

    [Fact]
    public void Snapshot_revision_bumps_on_grant_and_catalog()
    {
        var h = new FoundationHarness();
        var r0 = h.Snapshot().Revision;
        h.Grant(new EffectGrantDto
        {
            GrantId = "r1",
            EffectId = "fx.butter_on_hit",
            Overlay = new Dictionary<string, object?> { ["icd_ms"] = 0 }
        });
        var snap = h.Snapshot();
        Assert.True(snap.Revision > r0);
        Assert.Contains(snap.Grants, g => g.GrantId == "r1");
        var r1 = snap.Revision;
        h.ClearAll();
        Assert.True(h.Snapshot().Revision > r1);
        Assert.Empty(h.Snapshot().Grants);
    }

    [Fact]
    public void MatchButterSecondaryPlugin_grants_butter_and_plans_on_hit()
    {
        var h = new FoundationHarness();
        var plugins = new EffectPluginHost(h.Bag);
        plugins.Register(new MatchButterSecondaryPlugin());
        plugins.NotifyMatchStart("m-plugin");

        Assert.True(h.Bag.HasGrantForEffect("fx.butter_on_hit"));
        var plan = h.OnEvent(new EffectEventDto
        {
            Trigger = EffectTriggers.OnDamageDealt,
            MatchKey = "m-plugin",
            Side = "plant",
            ActorPtr = "0xA",
            TargetPtr = "0xB",
            TypeId = 0,
            TargetTypeId = 0,
            Damage = 10,
            Tick = 1
        });
        Assert.Single(plan.Actions);
        Assert.Equal(EffectActions.ApplyStatus, plan.Actions[0].Action);
        Assert.Equal("butter", plan.Actions[0].Params["status"]?.ToString());
    }

    [Fact]
    public void SimEffectHost_BeginMatch_grants_via_plugin_then_hit_plans()
    {
        var host = new SimEffectHost();
        host.BeginMatch("m-sim");
        Assert.True(host.Bag.HasGrantForEffect("fx.butter_on_hit"));
        var plan = host.HitDealt(typeId: 0, targetTypeId: 0);
        Assert.Single(plan.Actions);
        Assert.Equal(EffectActions.ApplyStatus, plan.Actions[0].Action);
    }

    [Fact]
    public void SimEffectHost_EndMatch_withdraws_plugin_grants()
    {
        var host = new SimEffectHost();
        host.BeginMatch("m-sim");
        Assert.True(host.Bag.HasGrantForEffect("fx.butter_on_hit"));
        host.EndMatch();
        Assert.False(host.Bag.HasGrantForEffect("fx.butter_on_hit"));
        var plan = host.HitDealt(typeId: 0, targetTypeId: 0);
        Assert.Empty(plan.Actions);
    }

    [Fact]
    public void SimEffectHost_ClearAll_skips_plugin_end_hook()
    {
        var host = new SimEffectHost();
        host.BeginMatch("m-sim");
        Assert.True(host.Bag.HasGrantForEffect("fx.butter_on_hit"));
        host.ClearAll();
        Assert.False(host.Bag.HasGrantForEffect("fx.butter_on_hit"));
    }

    [Fact]
    public void SimEffectHost_FireFromCapture_maps_combat_hit()
    {
        var host = new SimEffectHost();
        host.Grant(new EffectGrantDto
        {
            GrantId = "cap",
            EffectId = "fx.butter_on_hit",
            OwnerKey = EffectOwnerKeys.Match,
            Overlay = new Dictionary<string, object?> { ["icd_ms"] = 0 }
        });
        var plan = host.FireFromCapture("combat.hit", new Dictionary<string, object>
        {
            ["side"] = "zombie",
            ["attackerPtr"] = "0xA",
            ["targetPtr"] = "0xB",
            ["fromType"] = 0,
            ["targetType"] = 0,
            ["damage"] = 20
        });
        Assert.NotNull(plan);
        Assert.Single(plan!.Actions);
        Assert.Equal(EffectTriggers.OnDamageDealt, plan.Trigger);
    }
}
