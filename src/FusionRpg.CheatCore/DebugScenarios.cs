namespace FusionRpg.CheatCore;

public sealed class DebugScenarioStep
{
    public string Name { get; init; } = "";
    public object Payload { get; init; } = new { };
}

/// <summary>Named debug effect-test scenarios (server expands → injector <c>debug.run-steps</c>).</summary>
public static class DebugScenarios
{
    public const int PeaTypeId = 0; // Peashooter — confirm via /api/types live
    public const int SunflowerTypeId = 1;
    public const int WallNutTypeId = 3;
    public const int BasicZombieTypeId = 0;

    /// <summary>Step names allowed in Expand output (unit-tested).</summary>
    public static readonly HashSet<string> AllowedStepNames = new(StringComparer.Ordinal)
    {
        "debug.reset-mods",
        "debug.session",
        "debug.wave-freeze",
        "debug.set-mods",
        "debug.reset-board",
        "debug.spawn-plant",
        "debug.spawn-zombie",
        "debug.spawn-bullet",
        "debug.apply-status",
        "debug.apply-status-float",
        "debug.clear-status",
        "debug.arm",
        "debug.disarm",
        "debug.kill",
        "debug.kill-plant",
        "debug.reapply",
        "debug.ensure-sun",
        "debug.select",
        "debug.spawn-cell",
        "debug.snapshot",
        "debug.economy",
        "debug.board-stats",
        "debug.board-config",
        "debug.board-action",
        "debug.spawn-grid",
        "debug.clear-grid",
        "debug.set-box",
        "debug.grid-query",
        "debug.ice-road",
        "debug.effect.grant",
        "debug.effect.withdraw",
        "debug.effect.clear",
        "debug.effect.list",
        "debug.effect.fire-synthetic",
        "debug.effect.enqueue-delta",
        "pvz.spawn.extra"
    };

    public const int GraveGridTypeId = 7;
    public const int IceBlockGridTypeId = 8;
    public const int DriverZombieTypeId = 16;

    public static IReadOnlyList<DebugScenarioStep> Expand(string id, string scenarioId)
    {
        var steps = new List<DebugScenarioStep>();
        void Cmd(string name, object payload) =>
            steps.Add(new DebugScenarioStep { Name = name, Payload = payload });

        // P0: clear Tab A / absolutes / probe pollution before every scenario.
        Cmd("debug.reset-mods", new { });

        switch (id.ToLowerInvariant())
        {
            case "p1-baseline":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new
                {
                    probePlant = false,
                    probeBullet = false,
                    plant = new { attackPercent = 1.0, defensePercent = 1.0 },
                    zombie = new { defensePercent = 1.0 },
                    bullet = new { damageSet = -1, damagePercent = 1.0 },
                    logDamage = true
                });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 5000, maxHp = 5000 });
                break;

            case "p1-plant":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new
                {
                    probePlant = true,
                    probeBullet = false,
                    plant = new { attackPercent = 5.0 },
                    bullet = new { damageSet = -1, damagePercent = 1.0 },
                    logDamage = true
                });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2, attackPercent = 5.0 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 20000, maxHp = 20000 });
                break;

            case "p1-bullet":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new
                {
                    probePlant = false,
                    probeBullet = true,
                    plant = new { attackPercent = 1.0 },
                    bullet = new { damageSet = 999, damagePercent = 1.0 },
                    logDamage = true
                });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 20000, maxHp = 20000 });
                break;

            case "hit-capture":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 10000, maxHp = 10000 });
                break;

            case "hit-capture-plant":
                // Wall-nut + close BasicZ — prove plant-side combat.hit (melee damageFrom=Zombie or Bullet).
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { logDamage = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = WallNutTypeId, col = 6, row = 2, hp = 5000, maxHp = 5000 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, x = 7.8f, hp = 8000, maxHp = 8000 });
                break;

            case "status-butter":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 5000, maxHp = 5000 });
                Cmd("debug.apply-status", new { target = "all-zombies", status = "butter", duration = 8 });
                break;

            case "status-freeze":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 5000, maxHp = 5000 });
                Cmd("debug.apply-status", new { target = "all-zombies", status = "freeze", duration = 5, level = 1 });
                break;

            case "status-cold":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 5000, maxHp = 5000 });
                Cmd("debug.apply-status", new { target = "all-zombies", status = "cold", duration = 5, level = 1 });
                break;

            case "status-poison":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 5000, maxHp = 5000 });
                Cmd("debug.apply-status", new { target = "all-zombies", status = "poison", duration = 5 });
                break;

            case "status-float-butter":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 5000, maxHp = 5000 });
                Cmd("debug.apply-status-float", new { target = "all-zombies", status = "butter" });
                break;

            case "status-clear":
                Cmd("debug.clear-status", new { target = "all-zombies" });
                break;

            case "def-plant":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { plant = new { defensePercent = 5.0 }, logDamage = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = WallNutTypeId, col = 6, row = 2, hp = 5000, maxHp = 5000, defensePercent = 5.0 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, x = 7.8f, hp = 8000, maxHp = 8000 });
                break;

            case "def-zombie":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { zombie = new { defensePercent = 5.0 }, logDamage = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 20000, maxHp = 20000, defensePercent = 5.0 });
                break;

            case "def-alt-paths":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { logDamage = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 15000, maxHp = 15000 });
                break;

            case "onkilled-extra":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 50, maxHp = 50 });
                Cmd("debug.arm", new { kind = "onkill-extra", typeId = BasicZombieTypeId, once = true });
                Cmd("debug.kill", new { target = "all-zombies" });
                break;

            case "onhit-extra":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 20000, maxHp = 20000 });
                Cmd("debug.arm", new { kind = "onhit-extra", typeId = BasicZombieTypeId, maxTriggers = 1 });
                break;

            case "onhit-status":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 20000, maxHp = 20000 });
                Cmd("debug.arm", new { kind = "onhit-status", status = "butter", maxTriggers = 1 });
                break;

            case "onkill-status":
                // Victim last so "selected" oneshot hits the 50 HP zombie, not the tank.
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 5000, maxHp = 5000 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 1, hp = 50, maxHp = 50 });
                Cmd("debug.arm", new { kind = "onkill-status", status = "butter" });
                Cmd("debug.kill", new { target = "selected" });
                break;

            case "kill-signal":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 100, maxHp = 100 });
                Cmd("debug.kill", new { target = "all-zombies" });
                break;

            case "kill-plant":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2 });
                Cmd("debug.kill-plant", new { target = "all-plants" });
                break;

            case "spawn-matrix":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 1, row = 2, atk = 77, hp = 300, maxHp = 300 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 888, maxHp = 888 });
                break;

            case "spawn-bullet-hit":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, x = 5f, hp = 10000, maxHp = 10000 });
                Cmd("debug.spawn-bullet", new { bulletType = 0, row = 2, x = 3f, y = 0f, damage = 50, fromType = PeaTypeId });
                break;

            case "wave-freeze-check":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                break;

            case "hitland-butter":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                // Butter bullet type may vary by pack — operator can override via spawn-bullet.
                Cmd("debug.spawn-bullet", new { bulletType = 0, row = 2, x = 4f, y = 0f, damage = 1 });
                break;

            case "econ-sun-set":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.economy", new { which = "sun", value = 777, add = false });
                break;

            case "econ-sun-add":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.economy", new { which = "sun", value = 100, add = false });
                Cmd("debug.economy", new { which = "sun", value = 50, add = true });
                break;

            case "econ-money-set":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.economy", new { which = "money", value = 888, add = false });
                break;

            case "econ-money-add":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.economy", new { which = "money", value = 200, add = false });
                Cmd("debug.economy", new { which = "money", value = 25, add = true });
                break;

            case "econ-points-set":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.economy", new { which = "points", value = 42, add = false });
                break;

            case "zombie-speed-slow":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { zombie = new { uniqueSpeed = 0.3 } });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, x = 8f, hp = 20000, maxHp = 20000 });
                Cmd("debug.reapply", new { });
                break;

            case "zombie-speed-fast":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { zombie = new { uniqueSpeed = 2.0 } });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, x = 8f, hp = 20000, maxHp = 20000 });
                Cmd("debug.reapply", new { });
                break;

            case "onspawn-inspect":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2, atk = 55, hp = 400, maxHp = 400 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 1234, maxHp = 1234 });
                break;

            case "ondeath-inspect":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 80, maxHp = 80 });
                Cmd("debug.kill-plant", new { target = "all-plants" });
                Cmd("debug.kill", new { target = "all-zombies" });
                break;

            case "zombie-atk-bite":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { zombie = new { attackPercent = 5.0 }, logDamage = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = WallNutTypeId, col = 6, row = 2, hp = 8000, maxHp = 8000 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, x = 7.8f, hp = 20000, maxHp = 20000, attackPercent = 5.0 });
                Cmd("debug.reapply", new { });
                break;

            case "plant-produce":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { plant = new { produceInterval = 1.0 } });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = SunflowerTypeId, col = 2, row = 2 });
                Cmd("debug.reapply", new { });
                break;

            case "board-config-speed":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { board = new { zombieSpeedMultiplier = 0.4 } });
                Cmd("debug.board-config", new { });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, x = 8f, hp = 20000, maxHp = 20000 });
                break;

            case "spawn-mc":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, x = 6f, hp = 5000, maxHp = 5000, mindControl = true });
                break;

            case "env-freeze":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, x = 5.5f, hp = 20000, maxHp = 20000 });
                Cmd("debug.board-action", new { op = "freeze", col = 5, row = 2, timer = 3f });
                break;

            case "env-doom":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, x = 5.5f, hp = 20000, maxHp = 20000 });
                Cmd("debug.board-action", new { op = "doom", col = 5, row = 2, damage = 1800 });
                break;

            case "env-fireline":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, x = 6f, hp = 20000, maxHp = 20000 });
                Cmd("debug.board-action", new { op = "fireline", row = 2, damage = 1800 });
                break;

            case "env-cherry":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, x = 5.5f, hp = 20000, maxHp = 20000 });
                Cmd("debug.board-action", new { op = "cherry", col = 5, row = 2, damage = 1800 });
                break;

            case "env-grave":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-grid", new { typeId = GraveGridTypeId, col = 4, row = 2, graveType = 0 });
                break;

            case "tile-grave":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-grid", new { typeId = GraveGridTypeId, col = 4, row = 2, graveType = 0 });
                Cmd("debug.grid-query", new { col = 4, row = 2 });
                break;

            case "tile-grave-clear":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-grid", new { typeId = GraveGridTypeId, col = 4, row = 2, graveType = 0 });
                Cmd("debug.clear-grid", new { typeId = GraveGridTypeId, col = 4, row = 2 });
                Cmd("debug.grid-query", new { col = 4, row = 2 });
                break;

            case "tile-iceblock":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-grid", new { typeId = IceBlockGridTypeId, col = 5, row = 2 });
                break;

            case "tile-box-water":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-box", new { col = 3, row = 2, boxType = "Water" });
                Cmd("debug.grid-query", new { col = 3, row = 2 });
                break;

            case "tile-box-grass":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-box", new { col = 3, row = 2, boxType = "Water" });
                Cmd("debug.set-box", new { col = 3, row = 2, boxType = "Grass" });
                Cmd("debug.grid-query", new { col = 3, row = 2 });
                break;

            case "tile-box-lava":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-box", new { col = 3, row = 2, boxType = "Lava" });
                Cmd("debug.grid-query", new { col = 3, row = 2 });
                break;

            case "tile-box-dirt":
                // Nuclear / doom-scorched grass → Dirt (+ optional crater pit).
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-box", new { col = 4, row = 2, boxType = "nuclear", withPit = true });
                Cmd("debug.grid-query", new { col = 4, row = 2 });
                break;

            case "tile-ice-road":
                // Sledge / DriverZombie ice trail on the lawn.
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.ice-road", new { row = 2, x = 8f, typeId = DriverZombieTypeId, keepDriver = true });
                break;

            case "onkill-grave":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, x = 5.5f, hp = 50, maxHp = 50 });
                Cmd("debug.arm", new { kind = "onkill-grave", col = 4 });
                Cmd("debug.kill", new { target = "all-zombies" });
                break;

            case "onkill-clear-grave":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-grid", new { typeId = GraveGridTypeId, col = 3, row = 2, graveType = 0 });
                Cmd("debug.spawn-grid", new { typeId = GraveGridTypeId, col = 5, row = 2, graveType = 0 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, x = 6f, hp = 50, maxHp = 50 });
                Cmd("debug.arm", new { kind = "onkill-clear-grave" });
                Cmd("debug.kill", new { target = "all-zombies" });
                break;

            // --- Foundation Effect LIVE L1–L14 (no debug on-hit arms; EffectBag owns apply) ---
            case "effect-butter-hit":
                EffectHitBoard(Cmd, scenarioId, "fx.butter_on_hit", icdMs: 0);
                break;
            case "effect-freeze-hit":
                EffectHitBoard(Cmd, scenarioId, "fx.freeze_on_hit", icdMs: 0);
                break;
            case "effect-cold-hit":
                EffectHitBoard(Cmd, scenarioId, "fx.cold_on_hit", icdMs: 0);
                break;
            case "effect-clear-butter":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.effect.clear", new { });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { logDamage = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 20000, maxHp = 20000 });
                Cmd("debug.apply-status", new { target = "all-zombies", status = "butter", duration = 30 });
                Cmd("debug.effect.grant", new
                {
                    grantId = "live-clear",
                    effectId = "fx.clear_butter",
                    ownerKey = "match",
                    overlay = new { icd_ms = 0 }
                });
                break;
            case "effect-passive-atk":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.effect.clear", new { });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { logDamage = true, plant = new { attackPercent = 1.0 } });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2 });
                Cmd("debug.effect.grant", new
                {
                    grantId = "live-passive",
                    effectId = "fx.passive_atk_flat",
                    ownerKey = "match",
                    overlay = new { flat = 10.0 }
                });
                Cmd("debug.board-stats", new { });
                break;
            case "effect-spawn-ondeath":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.effect.clear", new { });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 40, maxHp = 40 });
                Cmd("debug.effect.grant", new
                {
                    grantId = "live-death",
                    effectId = "fx.spawn_zombie_ondeath",
                    ownerKey = "match",
                    overlay = new { icd_ms = 0 }
                });
                Cmd("debug.kill", new { target = "all-zombies" });
                break;
            case "effect-spawn-plant-bullet":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.effect.clear", new { });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { logDamage = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 20000, maxHp = 20000 });
                Cmd("debug.effect.grant", new
                {
                    grantId = "live-spawn-pb",
                    effectId = "fx.spawn_plant_bullet",
                    ownerKey = "match",
                    overlay = new { icd_ms = 0 }
                });
                break;
            case "effect-board-cherry":
                EffectHitBoard(Cmd, scenarioId, "fx.board_cherry", icdMs: 0);
                break;
            case "effect-grid-cycle":
                EffectHitBoard(Cmd, scenarioId, "fx.grid_item_cycle", icdMs: 0);
                break;
            case "effect-set-dirt":
                EffectHitBoard(Cmd, scenarioId, "fx.set_dirt_box", icdMs: 0);
                break;
            case "effect-economy-sun":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.effect.clear", new { });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { logDamage = true });
                Cmd("debug.economy", new { which = "sun", value = 100, add = false });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 20000, maxHp = 20000 });
                Cmd("debug.effect.grant", new
                {
                    grantId = "live-sun",
                    effectId = "fx.economy_sun",
                    ownerKey = "match",
                    overlay = new { icd_ms = 0 }
                });
                break;
            case "effect-icd-butter":
                EffectHitBoard(Cmd, scenarioId, "fx.icd_butter", icdMs: null);
                break;
            case "effect-withdraw":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.effect.clear", new { });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { logDamage = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 20000, maxHp = 20000 });
                Cmd("debug.effect.grant", new
                {
                    grantId = "live-withdraw",
                    effectId = "fx.butter_on_hit",
                    ownerKey = "match",
                    overlay = new { icd_ms = 0 }
                });
                Cmd("debug.effect.withdraw", new { grantId = "live-withdraw" });
                break;
            case "effect-spawn-filter":
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.effect.clear", new { });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.reset-board", new { });
                Cmd("debug.effect.grant", new
                {
                    grantId = "live-spawn-ft",
                    effectId = "fx.spawn_butter",
                    ownerKey = "plant:" + PeaTypeId,
                    overlay = new { icd_ms = 0 }
                });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 5000, maxHp = 5000 });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2 });
                break;

            case "effect-entity-atk":
                // Two peas; FA1 flat ATK only on SelectedPtr after first spawn (entity:selected).
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.effect.clear", new { });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { logDamage = true, plant = new { attackPercent = 1.0 } });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 1, row = 2 });
                Cmd("debug.effect.grant", new
                {
                    grantId = "live-entity-atk",
                    effectId = "fx.passive_atk_flat",
                    ownerKey = "entity:selected",
                    overlay = new { flat = 50.0 }
                });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 3, row = 2 });
                Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 20000, maxHp = 20000 });
                Cmd("debug.board-stats", new { });
                break;

            case "effect-plant-type-atk":
                // Pea + Wall-nut; grant plant:0 → only pea ATK rises.
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.effect.clear", new { });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { logDamage = true, plant = new { attackPercent = 1.0 } });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 1, row = 2 });
                Cmd("debug.spawn-plant", new { typeId = WallNutTypeId, col = 3, row = 2 });
                Cmd("debug.effect.grant", new
                {
                    grantId = "live-plant-type-atk",
                    effectId = "fx.passive_atk_flat",
                    ownerKey = "plant:" + PeaTypeId,
                    overlay = new { flat = 40.0 }
                });
                Cmd("debug.board-stats", new { });
                break;

            case "effect-match-midspawn":
                // Grant match flat → spawn second pea → both elevated.
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.effect.clear", new { });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { logDamage = true, plant = new { attackPercent = 1.0 } });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 1, row = 2 });
                Cmd("debug.effect.grant", new
                {
                    grantId = "live-match-mid",
                    effectId = "fx.passive_atk_flat",
                    ownerKey = "match",
                    overlay = new { flat = 15.0 }
                });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 3, row = 2 });
                Cmd("debug.board-stats", new { });
                break;

            case "effect-entity-midspawn":
                // Spawn A → entity grant → spawn B → board-stats; withdraw → board-stats again.
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.effect.clear", new { });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { logDamage = true, plant = new { attackPercent = 1.0 } });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 1, row = 2 });
                Cmd("debug.effect.grant", new
                {
                    grantId = "live-entity-mid",
                    effectId = "fx.passive_atk_flat",
                    ownerKey = "entity:selected",
                    overlay = new { flat = 50.0 }
                });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 3, row = 2 });
                Cmd("debug.board-stats", new { tag = "after-grant" });
                Cmd("debug.effect.withdraw", new { grantId = "live-entity-mid" });
                Cmd("debug.board-stats", new { tag = "after-withdraw" });
                break;

            case "effect-spawn-then-grant":
                // Spawn A+B → select A by col → entity grant → only A elevated.
                Cmd("debug.session", new { op = "start", scenarioId });
                Cmd("debug.effect.clear", new { });
                Cmd("debug.wave-freeze", new { enabled = true });
                Cmd("debug.set-mods", new { logDamage = true, plant = new { attackPercent = 1.0 } });
                Cmd("debug.reset-board", new { });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 1, row = 2 });
                Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 3, row = 2 });
                Cmd("debug.select", new { side = "plant", col = 1, row = 2 });
                Cmd("debug.effect.grant", new
                {
                    grantId = "live-spawn-then-grant",
                    effectId = "fx.passive_atk_flat",
                    ownerKey = "entity:selected",
                    overlay = new { flat = 50.0 }
                });
                Cmd("debug.board-stats", new { });
                break;

            default:
                throw new ArgumentException("unknown scenario: " + id);
        }

        return steps;
    }

    static void EffectHitBoard(Action<string, object> Cmd, string scenarioId, string effectId, int? icdMs)
    {
        Cmd("debug.session", new { op = "start", scenarioId });
        Cmd("debug.effect.clear", new { });
        Cmd("debug.wave-freeze", new { enabled = true });
        Cmd("debug.set-mods", new { logDamage = true });
        Cmd("debug.reset-board", new { });
        Cmd("debug.spawn-plant", new { typeId = PeaTypeId, col = 2, row = 2 });
        Cmd("debug.spawn-zombie", new { typeId = BasicZombieTypeId, row = 2, hp = 20000, maxHp = 20000 });
        object overlay = icdMs.HasValue ? new { icd_ms = icdMs.Value } : new { };
        Cmd("debug.effect.grant", new
        {
            grantId = "live-" + effectId.Replace('.', '-'),
            effectId,
            ownerKey = "match",
            overlay
        });
    }

    public static IReadOnlyList<string> AllIds { get; } = new[]
    {
        "p1-baseline", "p1-plant", "p1-bullet", "hit-capture", "hit-capture-plant",
        "status-butter", "status-freeze", "status-cold", "status-poison", "status-float-butter", "status-clear",
        "def-plant", "def-zombie", "def-alt-paths",
        "onkilled-extra", "onhit-extra", "onhit-status", "onkill-status",
        "kill-signal", "kill-plant", "spawn-matrix", "spawn-bullet-hit", "wave-freeze-check", "hitland-butter",
        "econ-sun-set", "econ-sun-add", "econ-money-set", "econ-money-add", "econ-points-set",
        "zombie-speed-slow", "zombie-speed-fast",
        "onspawn-inspect", "ondeath-inspect", "zombie-atk-bite", "plant-produce", "board-config-speed", "spawn-mc",
        "env-freeze", "env-doom", "env-fireline", "env-cherry", "env-grave",
        "tile-grave", "tile-grave-clear", "tile-iceblock",
        "tile-box-water", "tile-box-grass", "tile-box-lava", "tile-box-dirt", "tile-ice-road",
        "onkill-grave", "onkill-clear-grave",
        "effect-butter-hit", "effect-freeze-hit", "effect-cold-hit", "effect-clear-butter",
        "effect-passive-atk", "effect-spawn-ondeath", "effect-spawn-plant-bullet",
        "effect-board-cherry", "effect-grid-cycle", "effect-set-dirt", "effect-economy-sun",
        "effect-icd-butter", "effect-withdraw", "effect-spawn-filter", "effect-entity-atk",
        "effect-plant-type-atk", "effect-match-midspawn", "effect-entity-midspawn", "effect-spawn-then-grant"
    };
}
