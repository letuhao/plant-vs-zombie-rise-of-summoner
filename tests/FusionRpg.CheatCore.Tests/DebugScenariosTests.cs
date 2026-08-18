using FusionRpg.CheatCore;
using Xunit;

namespace FusionRpg.CheatCore.Tests;

public class DebugScenariosTests
{
    [Fact]
    public void AllIds_non_empty()
    {
        Assert.NotEmpty(DebugScenarios.AllIds);
        Assert.Contains("p1-baseline", DebugScenarios.AllIds);
    }

    [Fact]
    public void Every_id_expands_without_throw()
    {
        foreach (var id in DebugScenarios.AllIds)
        {
            var steps = DebugScenarios.Expand(id, "test-scenario");
            Assert.NotEmpty(steps);
        }
    }

    [Fact]
    public void Every_expand_starts_with_reset_mods_then_session_when_applicable()
    {
        foreach (var id in DebugScenarios.AllIds)
        {
            var steps = DebugScenarios.Expand(id, "sid");
            Assert.Equal("debug.reset-mods", steps[0].Name);
            if (!string.Equals(id, "status-clear", StringComparison.OrdinalIgnoreCase))
                Assert.Equal("debug.session", steps[1].Name);
        }
    }

    [Fact]
    public void P1_plant_sets_probe_via_set_mods()
    {
        var steps = DebugScenarios.Expand("p1-plant", "sid");
        Assert.Contains(steps, s => s.Name == "debug.set-mods");
        var mods = steps.First(s => s.Name == "debug.set-mods").Payload;
        var json = System.Text.Json.JsonSerializer.SerializeToElement(mods);
        Assert.True(json.GetProperty("probePlant").GetBoolean());
    }

    [Fact]
    public void Onkilled_extra_has_arm_and_kill()
    {
        var steps = DebugScenarios.Expand("onkilled-extra", "sid");
        Assert.Contains(steps, s => s.Name == "debug.arm");
        Assert.Contains(steps, s => s.Name == "debug.kill");
        var arm = System.Text.Json.JsonSerializer.SerializeToElement(
            steps.First(s => s.Name == "debug.arm").Payload);
        Assert.Equal("onkill-extra", arm.GetProperty("kind").GetString());
    }

    [Fact]
    public void Onkill_status_spawns_victim_last_before_selected_kill()
    {
        var steps = DebugScenarios.Expand("onkill-status", "sid");
        var spawnIdx = steps
            .Select((s, i) => (s, i))
            .Where(x => x.s.Name == "debug.spawn-zombie")
            .Select(x => x.i)
            .ToList();
        Assert.True(spawnIdx.Count >= 2);
        var lastSpawn = System.Text.Json.JsonSerializer.SerializeToElement(steps[spawnIdx[^1]].Payload);
        Assert.Equal(50, lastSpawn.GetProperty("hp").GetInt32());
        Assert.Contains(steps, s => s.Name == "debug.kill");
    }

    [Fact]
    public void No_unknown_step_names()
    {
        foreach (var id in DebugScenarios.AllIds)
        {
            foreach (var step in DebugScenarios.Expand(id, "sid"))
                Assert.True(DebugScenarios.AllowedStepNames.Contains(step.Name),
                    $"scenario {id} has unknown step {step.Name}");
        }
    }

    [Fact]
    public void Expansion_econ_sun_set_has_economy_step()
    {
        var steps = DebugScenarios.Expand("econ-sun-set", "sid");
        Assert.Contains(steps, s => s.Name == "debug.economy");
        var eco = System.Text.Json.JsonSerializer.SerializeToElement(
            steps.First(s => s.Name == "debug.economy").Payload);
        Assert.Equal("sun", eco.GetProperty("which").GetString());
        Assert.Equal(777, eco.GetProperty("value").GetInt32());
    }

    [Fact]
    public void Expansion_zombie_speed_slow_sets_unique_speed()
    {
        var steps = DebugScenarios.Expand("zombie-speed-slow", "sid");
        var mods = System.Text.Json.JsonSerializer.SerializeToElement(
            steps.First(s => s.Name == "debug.set-mods").Payload);
        Assert.Equal(0.3, mods.GetProperty("zombie").GetProperty("uniqueSpeed").GetDouble());
    }

    [Fact]
    public void Expansion_spawn_mc_sets_mind_control()
    {
        var steps = DebugScenarios.Expand("spawn-mc", "sid");
        var spawn = System.Text.Json.JsonSerializer.SerializeToElement(
            steps.First(s => s.Name == "debug.spawn-zombie").Payload);
        Assert.True(spawn.GetProperty("mindControl").GetBoolean());
    }

    [Fact]
    public void Expansion_env_freeze_has_board_action()
    {
        var steps = DebugScenarios.Expand("env-freeze", "sid");
        Assert.Contains(steps, s => s.Name == "debug.board-action");
        var act = System.Text.Json.JsonSerializer.SerializeToElement(
            steps.First(s => s.Name == "debug.board-action").Payload);
        Assert.Equal("freeze", act.GetProperty("op").GetString());
    }

    [Fact]
    public void Expansion_env_grave_spawns_grid()
    {
        var steps = DebugScenarios.Expand("env-grave", "sid");
        Assert.Contains(steps, s => s.Name == "debug.spawn-grid");
        var g = System.Text.Json.JsonSerializer.SerializeToElement(
            steps.First(s => s.Name == "debug.spawn-grid").Payload);
        Assert.Equal(DebugScenarios.GraveGridTypeId, g.GetProperty("typeId").GetInt32());
    }

    [Fact]
    public void Expansion_tile_grave_clear_has_clear_grid()
    {
        var steps = DebugScenarios.Expand("tile-grave-clear", "sid");
        Assert.Contains(steps, s => s.Name == "debug.clear-grid");
        Assert.Contains(steps, s => s.Name == "debug.spawn-grid");
    }

    [Fact]
    public void Expansion_tile_box_water_sets_box()
    {
        var steps = DebugScenarios.Expand("tile-box-water", "sid");
        var box = System.Text.Json.JsonSerializer.SerializeToElement(
            steps.First(s => s.Name == "debug.set-box").Payload);
        Assert.Equal("Water", box.GetProperty("boxType").GetString());
    }

    [Fact]
    public void Expansion_onkill_grave_arms_and_kills()
    {
        var steps = DebugScenarios.Expand("onkill-grave", "sid");
        Assert.Contains(steps, s => s.Name == "debug.arm");
        Assert.Contains(steps, s => s.Name == "debug.kill");
        var arm = System.Text.Json.JsonSerializer.SerializeToElement(
            steps.First(s => s.Name == "debug.arm").Payload);
        Assert.Equal("onkill-grave", arm.GetProperty("kind").GetString());
    }

    [Fact]
    public void Expansion_tile_box_dirt_uses_nuclear_alias()
    {
        var steps = DebugScenarios.Expand("tile-box-dirt", "sid");
        var box = System.Text.Json.JsonSerializer.SerializeToElement(
            steps.First(s => s.Name == "debug.set-box").Payload);
        Assert.Equal("nuclear", box.GetProperty("boxType").GetString());
        Assert.True(box.GetProperty("withPit").GetBoolean());
    }

    [Fact]
    public void Expansion_tile_ice_road_has_ice_road_step()
    {
        var steps = DebugScenarios.Expand("tile-ice-road", "sid");
        Assert.Contains(steps, s => s.Name == "debug.ice-road");
        var ice = System.Text.Json.JsonSerializer.SerializeToElement(
            steps.First(s => s.Name == "debug.ice-road").Payload);
        Assert.Equal(DebugScenarios.DriverZombieTypeId, ice.GetProperty("typeId").GetInt32());
    }

    [Fact]
    public void Unknown_id_throws()
    {
        Assert.Throws<ArgumentException>(() => DebugScenarios.Expand("no-such-scenario", "sid"));
    }

    [Fact]
    public void Effect_scenarios_include_effect_clear_after_session()
    {
        Assert.Contains("debug.effect.clear", DebugScenarios.AllowedStepNames);
        Assert.Contains("debug.effect.enqueue-delta", DebugScenarios.AllowedStepNames);
        Assert.Contains("debug.fx.probe-shaders", DebugScenarios.AllowedStepNames);
        Assert.Contains("debug.fx.world-flash", DebugScenarios.AllowedStepNames);
        foreach (var id in DebugScenarios.AllIds.Where(i => i.StartsWith("effect-", StringComparison.Ordinal)))
        {
            var steps = DebugScenarios.Expand(id, "sid").ToList();
            Assert.Contains(steps, s => s.Name == "debug.effect.clear");
            var sessionIdx = steps.FindIndex(s => s.Name == "debug.session");
            var clearIdx = steps.FindIndex(s => s.Name == "debug.effect.clear");
            Assert.True(sessionIdx >= 0 && clearIdx > sessionIdx, id);
        }
    }

    [Fact]
    public void Combat_scenarios_arm_with_select_and_synthetic()
    {
        foreach (var id in new[]
                 {
                     "combat-area-row", "combat-counter-target", "combat-counter-actor",
                     "combat-dot", "combat-heal", "combat-random"
                 })
        {
            Assert.Contains(id, DebugScenarios.AllIds);
            var steps = DebugScenarios.Expand(id, "sid");
            Assert.Contains(steps, s => s.Name == "debug.select");
            Assert.Contains(steps, s => s.Name == "debug.effect.fire-synthetic");
        }

        var area = DebugScenarios.Expand("combat-area-row", "sid");
        var grant = area.First(s => s.Name == "debug.effect.grant").Payload;
        var json = System.Text.Json.JsonSerializer.SerializeToElement(grant);
        Assert.Equal("Area", json.GetProperty("overlay").GetProperty("target").GetProperty("mode").GetString());
        Assert.Contains(area, s => s.Name == "debug.spawn-plant");
        Assert.True(DebugScenarios.Expand("combat-counter-target", "sid")
            .Count(s => s.Name == "debug.effect.fire-synthetic") >= 5);
    }

    [Fact]
    public void Status_l2_scenarios_pin_derived_and_use_statusId()
    {
        Assert.Contains("debug.actor-derived", DebugScenarios.AllowedStepNames);
        Assert.Contains("debug.status.apply", DebugScenarios.AllowedStepNames);
        foreach (var id in new[]
                 {
                     "status-l2-wither", "status-l2-snapshot", "status-l2-resist",
                     "status-l2-blight-row", "status-l2-butter", "status-l2-freeze", "status-l2-poison"
                 })
        {
            var steps = DebugScenarios.Expand(id, "sid");
            Assert.Contains(steps, s => s.Name == "debug.spawn-zombie");
            Assert.Contains(steps, s => s.Name == "debug.effect.grant");
            Assert.Contains(steps, s => s.Name == "debug.status");
            var grant = System.Text.Json.JsonSerializer.SerializeToElement(
                steps.First(s => s.Name == "debug.effect.grant").Payload);
            Assert.True(grant.GetProperty("overlay").TryGetProperty("statusId", out _));
            var z = System.Text.Json.JsonSerializer.SerializeToElement(
                steps.First(s => s.Name == "debug.spawn-zombie").Payload);
            Assert.True(z.TryGetProperty("derivedProfile", out _));
        }

        var blight = DebugScenarios.Expand("status-l2-blight-row", "sid");
        Assert.Equal(3, blight.Count(s => s.Name == "debug.spawn-zombie"));
        var resist = System.Text.Json.JsonSerializer.SerializeToElement(
            DebugScenarios.Expand("status-l2-resist", "sid")
                .First(s => s.Name == "debug.spawn-zombie").Payload);
        Assert.Equal("iron-dot", resist.GetProperty("derivedProfile").GetString());
    }
}
