using System.Text.Json;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Status;
using FusionRpg.Core.Stats;
using FusionRpg.Injector.Bridges;
using FusionRpg.Injector.Lawn;
using FusionRpg.Injector.Stats;
using Il2CppInterop.Runtime;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace FusionRpg.Injector;

/// <summary>Deterministic debug spawn / status / mods for effect-pipeline tests.</summary>
public static class DebugActions
{
    /// <returns>False if Admit rejected, Create null, or exception; true if spawn completed.</returns>
    public static bool SpawnPlant(JsonElement p)
    {
        try
        {
            if (!Match.SpawnAdmit.TryAdmit("plant", out _))
                return false;

            var typeId = Int(p, "typeId", CheatState.ManualTypeId);
            if (p.TryGetProperty("col", out var c) && c.TryGetInt32(out var col)) CheatState.SpawnCol = LawnCoords.ClampCol(col);
            if (p.TryGetProperty("row", out var r) && r.TryGetInt32(out var row)) CheatState.SpawnRow = LawnCoords.ClampRow(row);
            ApplyScaleProps(p, plant: true);
            ApplyAbsoluteProps(p, plant: true);
            CheatState.SetToggle("G-FREE-SET", true, "debug", emitInject: false);
            CheatState.SetToggle("A-APPLY", true, "debug", emitInject: false);

            var plant = CreatePlant.Instance.SetPlant(CheatState.SpawnCol, CheatState.SpawnRow, (PlantType)typeId,
                null, default, true, true, null);
            if (plant == null)
            {
                CheatState.Error("debug.spawn-plant null");
                return false;
            }

            // Force re-apply with absolutes if Start already ran without them.
            GameHooks.Applied.Remove(plant.Pointer);
            EntityApply.RunPlant(plant, "debug.spawn", includeAbsolute: true);
            CheatState.Select(plant.Pointer, "plant");
            var dump = GameDumps.Plant(plant, "debug.spawn", plant.thePlantHealth, plant.thePlantMaxHealth, plant.attackDamage);
            dump["side"] = "plant";
            dump["typeId"] = typeId;
            dump["col"] = CheatState.SpawnCol;
            dump["row"] = CheatState.SpawnRow;
            try { dump["thePlantAttackInterval"] = plant.thePlantAttackInterval; } catch { }
            try { dump["thePlantProduceInterval"] = plant.thePlantProduceInterval; } catch { }
            DebugRuntime.Emit("debug.spawn.plant", dump);
            CheatState.Note($"debug spawn plant {typeId} @{CheatState.SpawnCol},{CheatState.SpawnRow}");
            MaybePinDerived(p, plant.Pointer.ToString("X"));
            MaybePinElement(p, plant.Pointer.ToString("X"));
            return true;
        }
        catch (Exception ex)
        {
            CheatState.Error("debug.spawn-plant: " + ex.Message);
            return false;
        }
    }

    /// <returns>False if Admit rejected, Create null, or exception; true if spawn completed.</returns>
    public static bool SpawnZombie(JsonElement p)
    {
        try
        {
            if (!Match.SpawnAdmit.TryAdmit("zombie", out _))
                return false;

            var typeId = Int(p, "typeId", 0);
            if (p.TryGetProperty("row", out var r) && r.TryGetInt32(out var row)) CheatState.SpawnRow = LawnCoords.ClampRow(row);
            var x = p.TryGetProperty("x", out var xEl) && xEl.TryGetSingle(out var xv) ? xv : 9.9f;
            var mc = p.TryGetProperty("mindControl", out var m) && m.ValueKind == JsonValueKind.True;
            var asExtra = string.Equals(Str(p, "source"), "extra", StringComparison.OrdinalIgnoreCase);
            ApplyScaleProps(p, plant: false);
            ApplyAbsoluteProps(p, plant: false);
            CheatState.SetToggle("A-APPLY", true, "debug", emitInject: false);

            Zombie? z = null;
            if (asExtra) CheatState.PendingSpawnSourceTag = "extra";
            try
            {
                z = mc
                    ? CreateZombieSpawn.SetMindControl(CheatState.SpawnRow, (ZombieType)typeId, x, true)
                    : CreateZombieSpawn.Set(CheatState.SpawnRow, (ZombieType)typeId, x, false);
            }
            finally
            {
                if (asExtra && z != null)
                    CheatState.RegisterSpawnSourceTag(z.Pointer, "extra");
                CheatState.ClearPendingSpawnSourceTag();
            }

            if (z == null)
            {
                CheatState.Error("debug.spawn-zombie null");
                return false;
            }

            GameHooks.Applied.Remove(z.Pointer);
            EntityApply.RunZombie(z, "debug.spawn", includeAbsolute: true);
            CheatState.Select(z.Pointer, "zombie");
            var dump = GameDumps.Zombie(z, "debug.spawn", Bridges.ZombieCombatFields.GetHp(z), Bridges.ZombieCombatFields.GetMaxHp(z), z.theAttackDamage,
                z.theFirstArmorHealth, z.theFirstArmorMaxHealth);
            dump["side"] = "zombie";
            dump["typeId"] = typeId;
            dump["row"] = CheatState.SpawnRow;
            dump["x"] = x;
            if (asExtra) dump["source"] = "extra";
            if (mc) dump["mindControlled"] = true;
            try { dump["uniqueSpeed"] = z.uniqueSpeed; } catch { }
            try { dump["theSpeed"] = z.theSpeed; } catch { }
            DebugRuntime.Emit("debug.spawn.zombie", dump);
            CheatState.Note($"debug spawn zombie {typeId} row={CheatState.SpawnRow}");
            MaybePinDerived(p, z.Pointer.ToString("X"));
            MaybePinElement(p, z.Pointer.ToString("X"));
            return true;
        }
        catch (Exception ex)
        {
            CheatState.ClearPendingSpawnSourceTag();
            CheatState.Error("debug.spawn-zombie: " + ex.Message);
            return false;
        }
    }

    /// <returns>False if Admit rejected, Create null, or exception; true if spawn completed.</returns>
    public static bool SpawnBullet(JsonElement p)
    {
        try
        {
            if (!Match.SpawnAdmit.TryAdmit("bullet", out _))
                return false;

            var board = GameHooks.Board;
            if (board == null) { CheatState.Error("debug.spawn-bullet: no board"); return false; }
            var bulletType = Int(p, "bulletType", Int(p, "typeId", 0));
            var row = LawnCoords.ClampRow(Int(p, "row", CheatState.SpawnRow));
            var x = p.TryGetProperty("x", out var xEl) && xEl.TryGetSingle(out var xv) ? xv : 3f;
            var y = p.TryGetProperty("y", out var yEl) && yEl.TryGetSingle(out var yv) ? yv : 0f;
            var moveWay = p.TryGetProperty("moveWay", out var mw) && mw.TryGetInt32(out var mwi)
                ? (BulletMoveWay)mwi
                : BulletMoveWay.MoveRight;
            var b = CreateBullet.Instance.SetBullet(x, y, row, (BulletType)bulletType, moveWay, false);
            if (b == null) { CheatState.Error("debug.spawn-bullet null"); return false; }
            if (p.TryGetProperty("damage", out var d) && d.TryGetInt32(out var dmg) && dmg >= 0)
                b.Damage = dmg;
            if (p.TryGetProperty("fromType", out var ft) && ft.TryGetInt32(out var fti))
            {
                try { b.fromType = (PlantType)fti; } catch { }
            }
            DebugRuntime.Emit("debug.spawn.bullet", new Dictionary<string, object>
            {
                ["ptr"] = GameDumps.Ptr(b),
                ["bulletType"] = bulletType,
                ["damage"] = b.Damage,
                ["row"] = row,
                ["x"] = x,
                ["y"] = y
            });
            return true;
        }
        catch (Exception ex)
        {
            CheatState.Error("debug.spawn-bullet: " + ex.Message);
            return false;
        }
    }

    /// <summary>Clear Tab A scales, P-/Z- absolutes, and D damage/probe knobs to identity defaults.</summary>
    public static void ResetMods()
    {
        try
        {
            CheatState.ResetGroup("A-");
            CheatState.ResetGroup("P-");
            CheatState.ResetGroup("Z-");
            CheatState.ResetGroup("E-");
            CheatState.SetFloat("D-DMG-SET", -1, "debug", emitInject: false);
            CheatState.SetFloat("D-DMG-%", 1, "debug", emitInject: false);
            CheatState.SetToggle("D-PROBE-PLANT", false, "debug", emitInject: false);
            CheatState.SetToggle("D-PROBE-BULLET", false, "debug", emitInject: false);
            CheatState.SetToggle("A-APPLY", true, "debug", emitInject: false);
            CheatActions.PushScalesNow();
            DebugRuntime.Emit("debug.mods.reset", new Dictionary<string, object> { ["ok"] = true });
        }
        catch (Exception ex) { CheatState.Error("debug.reset-mods: " + ex.Message); }
    }

    public static void SetMods(JsonElement p)
    {
        try
        {
            if (p.TryGetProperty("probePlant", out var pp))
                CheatState.SetToggle("D-PROBE-PLANT", pp.ValueKind == JsonValueKind.True, "debug");
            if (p.TryGetProperty("probeBullet", out var pb))
                CheatState.SetToggle("D-PROBE-BULLET", pb.ValueKind == JsonValueKind.True, "debug");
            if (p.TryGetProperty("logDamage", out var ld))
                CheatState.LocalStats.LogDamage = ld.ValueKind != JsonValueKind.False;

            SetFloatIf(p, "plant", "attackPercent", "A-P-ATK%");
            SetFlatIf(p, "plant", "attackFlat", "A-P-ATK+");
            SetFloatIf(p, "plant", "hpPercent", "A-P-HP%");
            SetFlatIf(p, "plant", "hpFlat", "A-P-HP+");
            SetFloatIf(p, "plant", "defensePercent", "A-P-DEF%");
            SetFlatIf(p, "plant", "defenseFlat", "A-P-DEF+");
            SetFloatIf(p, "zombie", "attackPercent", "A-Z-ATK%");
            SetFlatIf(p, "zombie", "attackFlat", "A-Z-ATK+");
            SetFloatIf(p, "zombie", "hpPercent", "A-Z-HP%");
            SetFlatIf(p, "zombie", "hpFlat", "A-Z-HP+");
            SetFloatIf(p, "zombie", "defensePercent", "A-Z-DEF%");
            SetFlatIf(p, "zombie", "defenseFlat", "A-Z-DEF+");

            if (p.TryGetProperty("bullet", out var bullet) && bullet.ValueKind == JsonValueKind.Object)
            {
                if (bullet.TryGetProperty("damageSet", out var ds) && ds.TryGetDouble(out var dsv))
                    CheatState.SetFloat("D-DMG-SET", dsv, "debug");
                if (bullet.TryGetProperty("damagePercent", out var dp) && dp.TryGetDouble(out var dpv))
                    CheatState.SetFloat("D-DMG-%", dpv, "debug");
            }

            if (p.TryGetProperty("plant", out var plant) && plant.ValueKind == JsonValueKind.Object)
            {
                if (plant.TryGetProperty("attackInterval", out var ai) && ai.TryGetDouble(out var aiv))
                    CheatState.SetFloat("P-ATK-INT", aiv, "debug");
                if (plant.TryGetProperty("produceInterval", out var pi) && pi.TryGetDouble(out var piv))
                    CheatState.SetFloat("P-PROD-INT", piv, "debug");
            }
            if (p.TryGetProperty("zombie", out var zom) && zom.ValueKind == JsonValueKind.Object)
            {
                if (zom.TryGetProperty("uniqueSpeed", out var us) && us.TryGetDouble(out var usv))
                    CheatState.SetFloat("Z-SPD-U", usv, "debug");
                if (zom.TryGetProperty("theSpeed", out var ts) && ts.TryGetDouble(out var tsv))
                    CheatState.SetFloat("Z-SPD", tsv, "debug");
                if (zom.TryGetProperty("theOriginSpeed", out var os) && os.TryGetDouble(out var osv))
                    CheatState.SetFloat("Z-SPD-O", osv, "debug");
            }

            var boardTouched = false;
            if (p.TryGetProperty("board", out var board) && board.ValueKind == JsonValueKind.Object)
            {
                if (board.TryGetProperty("zombieHealthMultiplier", out var zh) && zh.TryGetDouble(out var zhv))
                { CheatState.SetFloat("E-ZH", zhv, "debug"); boardTouched = true; }
                if (board.TryGetProperty("zombieDamageMultiplier", out var zd) && zd.TryGetDouble(out var zdv))
                { CheatState.SetFloat("E-ZD", zdv, "debug"); boardTouched = true; }
                if (board.TryGetProperty("zombieSpeedMultiplier", out var zs) && zs.TryGetDouble(out var zsv))
                { CheatState.SetFloat("E-ZS", zsv, "debug"); boardTouched = true; }
                if (board.TryGetProperty("zombieCountMultiplier", out var zc) && zc.TryGetDouble(out var zcv))
                { CheatState.SetFloat("E-ZC", zcv, "debug"); boardTouched = true; }
            }

            CheatState.SetToggle("A-APPLY", true, "debug", emitInject: false);
            CheatActions.PushScalesNow();
            if (boardTouched)
                CheatActions.ApplyBoardConfig();
            DebugRuntime.Emit("debug.mods.set", new Dictionary<string, object> { ["ok"] = true, ["boardTouched"] = boardTouched });
        }
        catch (Exception ex) { CheatState.Error("debug.set-mods: " + ex.Message); }
    }

    public static void Economy(JsonElement p)
    {
        var which = Str(p, "which") ?? "sun";
        var add = p.TryGetProperty("add", out var a) && a.ValueKind == JsonValueKind.True;
        var value = p.TryGetProperty("value", out var v) && v.TryGetDouble(out var dv) ? (float)dv : 0f;
        CheatActions.SetEconomy(which, value, add);
        DebugRuntime.Emit("debug.economy", new Dictionary<string, object>
        {
            ["which"] = which,
            ["value"] = value,
            ["add"] = add
        });
    }

    public static void BoardConfigApply(JsonElement p)
    {
        _ = p;
        CheatActions.ApplyBoardConfig();
        DebugRuntime.Emit("debug.board.config", new Dictionary<string, object> { ["ok"] = true });
    }

    /// <summary>Mid-match BoardAction AOE: freeze / doom / fireline / cherry.</summary>
    public static void BoardAction(JsonElement p)
    {
        try
        {
            var board = GameHooks.Board ?? Board.Instance;
            if (board?.boardAction == null)
            {
                CheatState.Error("debug.board-action: no boardAction");
                return;
            }

            var op = (Str(p, "op") ?? "freeze").ToLowerInvariant();
            var col = Int(p, "col", CheatState.SpawnCol);
            var row = Int(p, "row", CheatState.SpawnRow);
            col = LawnCoords.ClampCol(col);
            row = LawnCoords.ClampRow(row);
            CheatState.SpawnCol = col;
            CheatState.SpawnRow = row;

            var pos = CellPos(col, row);
            var damage = Int(p, "damage", 1800);
            var fromType = (PlantType)Int(p, "fromType", -1);
            Il2CppSystem.Action<Zombie>? zombieAction = null;

            switch (op)
            {
                case "freeze":
                {
                    var timer = p.TryGetProperty("timer", out var t) && t.TryGetSingle(out var tv) ? tv : 3f;
                    board.boardAction.CreateFreeze(pos, timer);
                    EmitBoardAction(op, col, row, damage, new Dictionary<string, object> { ["timer"] = timer, ["x"] = pos.x, ["y"] = pos.y });
                    break;
                }
                case "doom":
                {
                    var setPit = !(p.TryGetProperty("setPit", out var sp) && sp.ValueKind == JsonValueKind.False);
                    var iceDoom = p.TryGetProperty("iceDoom", out var ice) && ice.ValueKind == JsonValueKind.True;
                    var effect = Int(p, "effect", 0);
                    var existParticle = !(p.TryGetProperty("existParticle", out var ep) && ep.ValueKind == JsonValueKind.False);
                    board.boardAction.SetDoom(col, row, setPit, iceDoom, pos, damage, effect, zombieAction, existParticle, fromType);
                    EmitBoardAction(op, col, row, damage, new Dictionary<string, object>
                    {
                        ["setPit"] = setPit,
                        ["iceDoom"] = iceDoom,
                        ["x"] = pos.x,
                        ["y"] = pos.y
                    });
                    break;
                }
                case "fireline":
                case "fire":
                {
                    var fromZombie = p.TryGetProperty("fromZombie", out var fz) && fz.ValueKind == JsonValueKind.True;
                    var fix = p.TryGetProperty("fix", out var fx) && fx.ValueKind == JsonValueKind.True;
                    var shake = !(p.TryGetProperty("shake", out var sh) && sh.ValueKind == JsonValueKind.False);
                    board.boardAction.CreateFireLine(row, damage, fromZombie, fix, shake, zombieAction, fromType);
                    EmitBoardAction(op, col, row, damage, new Dictionary<string, object>
                    {
                        ["fromZombie"] = fromZombie,
                        ["fix"] = fix,
                        ["shake"] = shake
                    });
                    break;
                }
                case "cherry":
                case "cherryexplode":
                {
                    var bombType = (CherryBombType)Int(p, "bombType", (int)CherryBombType.Normal);
                    var immediately = !(p.TryGetProperty("immediately", out var im) && im.ValueKind == JsonValueKind.False);
                    board.boardAction.CreateCherryExplode(pos, row, bombType, damage, fromType, zombieAction, immediately);
                    EmitBoardAction(op, col, row, damage, new Dictionary<string, object>
                    {
                        ["bombType"] = (int)bombType,
                        ["immediately"] = immediately,
                        ["x"] = pos.x,
                        ["y"] = pos.y
                    });
                    break;
                }
                default:
                    CheatState.Error("debug.board-action unknown op: " + op);
                    break;
            }
        }
        catch (Exception ex) { CheatState.Error("debug.board-action: " + ex.Message); }
    }

    public static void SpawnGrid(JsonElement p)
    {
        try
        {
            var typeId = Int(p, "typeId", (int)GridItemType.Grave);
            if (p.TryGetProperty("col", out var c) && c.TryGetInt32(out var col))
                CheatState.SpawnCol = LawnCoords.ClampCol(col);
            if (p.TryGetProperty("row", out var r) && r.TryGetInt32(out var row))
                CheatState.SpawnRow = LawnCoords.ClampRow(row);
            var graveType = (GraveType)Int(p, "graveType", 0);
            PlaceGridItem(CheatState.SpawnCol, CheatState.SpawnRow, typeId, graveType, "debug.spawn");
        }
        catch (Exception ex) { CheatState.Error("debug.spawn-grid: " + ex.Message); }
    }

    /// <summary>Clear a grid item by cell and/or type; <c>random:true</c> picks any matching item.</summary>
    public static void ClearGrid(JsonElement p)
    {
        try
        {
            var typeId = Int(p, "typeId", (int)GridItemType.Grave);
            var random = p.TryGetProperty("random", out var rnd) && rnd.ValueKind == JsonValueKind.True;
            int? col = p.TryGetProperty("col", out var c) && c.TryGetInt32(out var cv) ? cv : null;
            int? row = p.TryGetProperty("row", out var r) && r.TryGetInt32(out var rv) ? rv : null;
            var cleared = ClearGridItem(typeId, col, row, random, emitKind: "debug.grid.clear");
            if (!cleared)
                CheatState.Note($"debug clear-grid miss type={typeId} random={random}");
        }
        catch (Exception ex) { CheatState.Error("debug.clear-grid: " + ex.Message); }
    }

    /// <summary>Paint <see cref="BoxType"/> on a cell via <c>BoardGrid.boxType</c> + <c>UpdateBox</c>.
    /// Nuclear-scorched lawn uses <see cref="BoxType.Dirt"/> (aliases: dirt, nuclear, scorched).</summary>
    public static void SetBox(JsonElement p)
    {
        try
        {
            var board = GameHooks.Board ?? Board.Instance;
            if (board == null)
            {
                CheatState.Error("debug.set-box: no board");
                return;
            }

            var col = LawnCoords.ClampCol(Int(p, "col", CheatState.SpawnCol));
            var row = LawnCoords.ClampRow(Int(p, "row", CheatState.SpawnRow));
            CheatState.SpawnCol = col;
            CheatState.SpawnRow = row;
            var boxType = ParseBoxType(p, out var boxName);
            var withPit = p.TryGetProperty("withPit", out var wp) && wp.ValueKind == JsonValueKind.True;

            BoxType before = default;
            try { before = board.GetBoxType(col, row); } catch { }

            var wrote = false;
            var path = "";
            try
            {
                var gs = board.gridSystem;
                var cell = gs?.GetGrid(col, row);
                if (cell != null)
                {
                    cell.boxType = boxType;
                    wrote = true;
                    path = "BoardGrid.set_boxType";
                }
            }
            catch (Exception ex) { CheatState.Error("set BoardGrid.boxType: " + ex.Message); }

            if (!wrote)
            {
                wrote = TryWriteRoadType(board, col, row, boxType, out var index, out var layout);
                path = layout ?? "roadType";
                if (wrote) path = "roadType:" + path + " idx=" + index;
            }

            try { board.UpdateBox(col, row); } catch (Exception ex) { CheatState.Error("UpdateBox: " + ex.Message); }

            if (withPit && board.boardAction != null)
            {
                try
                {
                    board.boardAction.SetPit(col, row);
                    path += "+SetPit";
                }
                catch (Exception ex) { CheatState.Error("SetPit: " + ex.Message); }
            }

            BoxType after = before;
            try { after = board.GetBoxType(col, row); } catch { }

            DebugRuntime.Emit("debug.box.set", new Dictionary<string, object>
            {
                ["ok"] = wrote && (int)after == (int)boxType,
                ["wrote"] = wrote,
                ["col"] = col,
                ["row"] = row,
                ["boxType"] = (int)boxType,
                ["boxName"] = boxName,
                ["before"] = (int)before,
                ["beforeName"] = before.ToString(),
                ["after"] = (int)after,
                ["afterName"] = after.ToString(),
                ["path"] = path,
                ["withPit"] = withPit
            });
            CheatState.Note($"debug set-box {boxName} @{col},{row} {before}->{after} via {path}");
        }
        catch (Exception ex) { CheatState.Error("debug.set-box: " + ex.Message); }
    }

    /// <summary>Zomboni / Sledge-style ice trail via <see cref="DriverZombie.CreateIceRoad"/>.</summary>
    public static void IceRoad(JsonElement p)
    {
        try
        {
            var row = LawnCoords.ClampRow(Int(p, "row", CheatState.SpawnRow));
            CheatState.SpawnRow = row;
            var x = p.TryGetProperty("x", out var xEl) && xEl.TryGetSingle(out var xv) ? xv : 8f;
            var typeId = Int(p, "typeId", 16); // DriverZombie
            var killAfter = !(p.TryGetProperty("keepDriver", out var kd) && kd.ValueKind == JsonValueKind.True);

            CheatState.SetToggle("A-APPLY", true, "debug", emitInject: false);
            var iceBefore = 0;
            try { iceBefore = (GameHooks.Board ?? Board.Instance)?.iceRoads?.Count ?? 0; } catch { }

            if (!Match.SpawnAdmit.TryAdmit("zombie", out _))
                return;

            var z = CreateZombieSpawn.Set(row, (ZombieType)typeId, x, false);
            if (z == null)
            {
                CheatState.Error("debug.ice-road: spawn null");
                return;
            }

            string typeName = "?";
            try { typeName = z.theZombieType.ToString(); } catch { try { typeName = z.GetIl2CppType().Name; } catch { } }

            var drove = false;
            var castPath = "";
            try
            {
                var driver = z.TryCast<DriverZombie>();
                if (driver != null)
                {
                    castPath = "TryCast<DriverZombie>";
                    driver.CreateIceRoad();
                    drove = true;
                }
                else
                {
                    var super = z.TryCast<SuperDriverZombie>();
                    if (super != null)
                    {
                        castPath = "TryCast<SuperDriverZombie>";
                        super.CreateIceRoad();
                        drove = true;
                    }
                }
            }
            catch (Exception ex) { CheatState.Error("CreateIceRoad: " + ex.Message); }

            // Fallback: call on any existing driver already on the lawn.
            if (!drove)
            {
                try
                {
                    foreach (var other in UObject.FindObjectsOfType<DriverZombie>())
                    {
                        if (other == null) continue;
                        other.CreateIceRoad();
                        drove = true;
                        castPath = "FindObjectsOfType<DriverZombie>";
                        break;
                    }
                }
                catch (Exception ex) { CheatState.Error("ice-road fallback: " + ex.Message); }
            }

            var iceAfter = iceBefore;
            try { iceAfter = (GameHooks.Board ?? Board.Instance)?.iceRoads?.Count ?? iceBefore; } catch { }

            if (killAfter)
            {
                try { z.Die(0); } catch { }
            }

            DebugRuntime.Emit("debug.ice.road", new Dictionary<string, object>
            {
                ["ok"] = drove,
                ["row"] = row,
                ["x"] = x,
                ["typeId"] = typeId,
                ["typeName"] = typeName,
                ["castPath"] = castPath,
                ["iceRoadCountBefore"] = iceBefore,
                ["iceRoadCount"] = iceAfter,
                ["keepDriver"] = !killAfter
            });
            CheatState.Note($"debug ice-road row={row} drove={drove} type={typeName} ice {iceBefore}->{iceAfter}");
        }
        catch (Exception ex) { CheatState.Error("debug.ice-road: " + ex.Message); }
    }

    public static void GridQuery(JsonElement p)
    {
        try
        {
            var board = GameHooks.Board ?? Board.Instance;
            if (board == null)
            {
                CheatState.Error("debug.grid-query: no board");
                return;
            }

            var col = LawnCoords.ClampCol(Int(p, "col", CheatState.SpawnCol));
            var row = LawnCoords.ClampRow(Int(p, "row", CheatState.SpawnRow));
            var dump = new Dictionary<string, object> { ["col"] = col, ["row"] = row };
            try
            {
                var bt = board.GetBoxType(col, row);
                dump["boxType"] = (int)bt;
                dump["boxName"] = bt.ToString();
            }
            catch (Exception ex) { dump["boxError"] = ex.Message; }

            var gs = board.gridSystem;
            if (gs != null)
            {
                try { dump["hasGrave"] = gs.HasGrave(col, row); } catch { }
                try { dump["graveNum"] = gs.GetGraveNum(col, row); } catch { }
                try { dump["hasPit"] = gs.HasPit(col, row); } catch { }
                try { dump["hasPot"] = gs.HasPot(col, row); } catch { }
                try { dump["hasLily"] = gs.HasLily(col, row); } catch { }
                try { dump["hasPlant"] = gs.HasPlant(col, row); } catch { }
            }

            DebugRuntime.Emit("debug.grid.query", dump);
        }
        catch (Exception ex) { CheatState.Error("debug.grid-query: " + ex.Message); }
    }

    public static GridItem? PlaceGridItem(int col, int row, int typeId, GraveType graveType, string source)
    {
        col = LawnCoords.ClampCol(col);
        row = LawnCoords.ClampRow(row);
        CheatState.SpawnCol = col;
        CheatState.SpawnRow = row;
        var g = GridItem.SetGridItem(col, row, (GridItemType)typeId, graveType);
        SpawnCatalog.Note("grid", typeId, ((GridItemType)typeId).ToString(), source);
        DebugRuntime.Emit("debug.spawn.grid", new Dictionary<string, object>
        {
            ["ok"] = g != null,
            ["typeId"] = typeId,
            ["typeName"] = ((GridItemType)typeId).ToString(),
            ["graveType"] = (int)graveType,
            ["col"] = col,
            ["row"] = row,
            ["source"] = source
        });
        CheatState.Note(g != null ? $"debug spawn grid {typeId} @{col},{row}" : "debug spawn grid null");
        return g;
    }

    /// <returns>true if an item was cleared.</returns>
    public static bool ClearGridItem(int typeId, int? col, int? row, bool random, string emitKind)
    {
        var matches = new List<GridItem>();
        foreach (var item in UObject.FindObjectsOfType<GridItem>())
        {
            if (item == null) continue;
            try
            {
                if ((int)item.theItemType != typeId) continue;
                if (col.HasValue && item.theItemColumn != col.Value) continue;
                if (row.HasValue && item.theItemRow != row.Value) continue;
                matches.Add(item);
            }
            catch { }
        }

        if (matches.Count == 0) return false;

        GridItem target;
        if (random)
            target = matches[UnityEngine.Random.Range(0, matches.Count)];
        else if (col.HasValue && row.HasValue)
            target = matches[0];
        else if (matches.Count == 1)
            target = matches[0];
        else
        {
            CheatState.Error("debug.clear-grid: multiple matches; pass col/row or random:true");
            return false;
        }

        var tCol = 0;
        var tRow = 0;
        var tType = typeId;
        try { tCol = target.theItemColumn; tRow = target.theItemRow; tType = (int)target.theItemType; } catch { }
        try { target.Die(); } catch (Exception ex) { CheatState.Error("grid Die: " + ex.Message); return false; }

        try
        {
            var board = GameHooks.Board ?? Board.Instance;
            var gs = board?.gridSystem;
            if (gs != null && tType == (int)GridItemType.Grave)
                gs.RemoveGrave(tCol, tRow);
        }
        catch { }

        DebugRuntime.Emit(emitKind, new Dictionary<string, object>
        {
            ["ok"] = true,
            ["typeId"] = tType,
            ["typeName"] = ((GridItemType)tType).ToString(),
            ["col"] = tCol,
            ["row"] = tRow,
            ["random"] = random
        });
        CheatState.Note($"debug clear grid {tType} @{tCol},{tRow}");
        return true;
    }

    static BoxType ParseBoxType(JsonElement p, out string name)
    {
        if (p.TryGetProperty("boxType", out var el))
        {
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
            {
                var bt = (BoxType)n;
                name = bt.ToString();
                return bt;
            }
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = (el.GetString() ?? "Grass").Trim();
                // Nuclear boom scorches grass → Dirt (classic doom-shroom / nuclear crater dirt).
                if (s.Equals("nuclear", StringComparison.OrdinalIgnoreCase)
                    || s.Equals("scorched", StringComparison.OrdinalIgnoreCase)
                    || s.Equals("destroyed", StringComparison.OrdinalIgnoreCase))
                {
                    name = nameof(BoxType.Dirt);
                    return BoxType.Dirt;
                }
                if (Enum.TryParse<BoxType>(s, ignoreCase: true, out var parsed))
                {
                    name = parsed.ToString();
                    return parsed;
                }
            }
        }
        name = nameof(BoxType.Grass);
        return BoxType.Grass;
    }

    static bool TryWriteRoadType(Board board, int col, int row, BoxType boxType, out int index, out string? layout)
    {
        index = -1;
        layout = null;
        var roads = board.roadType;
        if (roads == null) return false;

        var len = roads.Length;
        var cols = 9;
        var rows = 5;
        try { cols = Mathf.Max(1, board.columnNum); } catch { }
        try
        {
            var gs = board.gridSystem;
            if (gs != null)
            {
                try { cols = Mathf.Max(1, gs.ColumnCount); } catch { }
                try { rows = Mathf.Max(1, gs.RowCount); } catch { }
            }
        }
        catch { }

        // Prefer row-major (row * cols + col); fall back to col-major if length fits that better.
        var rowMajor = row * cols + col;
        var colMajor = col * rows + row;
        if (rowMajor >= 0 && rowMajor < len)
        {
            index = rowMajor;
            layout = $"rowMajor cols={cols} len={len}";
        }
        else if (colMajor >= 0 && colMajor < len)
        {
            index = colMajor;
            layout = $"colMajor rows={rows} len={len}";
        }
        else
        {
            layout = $"noIndex cols={cols} rows={rows} len={len}";
            return false;
        }

        roads[index] = boxType;
        try { board.roadType = roads; } catch { }
        return true;
    }

    static Vector2 CellPos(int col, int row) => LawnCoords.CellCenter(col, row);

    static void EmitBoardAction(string op, int col, int row, int damage, Dictionary<string, object> extra)
    {
        var dump = new Dictionary<string, object>
        {
            ["op"] = op,
            ["col"] = col,
            ["row"] = row,
            ["damage"] = damage
        };
        foreach (var kv in extra)
            dump[kv.Key] = kv.Value;
        DebugRuntime.Emit("debug.board.action", dump);
        CheatState.Note($"debug board-action {op} @{col},{row}");
    }

    public static void ApplyStatus(JsonElement p, bool method)
    {
        var status = (Str(p, "status") ?? "butter").ToLowerInvariant();
        var duration = p.TryGetProperty("duration", out var d) && d.TryGetSingle(out var dv) ? dv : 4f;
        var level = Int(p, "level", 1);
        var target = Str(p, "target") ?? "selected";
        var n = 0;
        var ptrs = new List<string>();
        foreach (var z in ResolveZombies(target, p))
        {
            ApplyStatusToZombie(z, status, duration, level, method);
            try { ptrs.Add(GameDumps.Ptr(z)); } catch { }
            n++;
        }
        var dump = new Dictionary<string, object>
        {
            ["status"] = status,
            ["method"] = method,
            ["count"] = n,
            ["duration"] = duration,
            ["ptrs"] = ptrs
        };
        if (ptrs.Count == 1)
            dump["ptr"] = ptrs[0];
        DebugRuntime.Emit("debug.status.applied", dump);
    }

    public static void ClearStatus(JsonElement p)
    {
        var target = Str(p, "target") ?? "selected";
        var status = Str(p, "status");
        var n = 0;
        var ptrs = new List<string>();
        foreach (var z in ResolveZombies(target, p))
        {
            try
            {
                try { z.UnButtered(); } catch { }
                try { z.Warm(); } catch { }
                try { z.KillDebuff(); } catch { }
                try { z.freezeLevel = 0; } catch { }
                try { z.freezeSpeed = 1f; } catch { }
                try { z.coldSpeed = 1f; } catch { }
                try { z.butterSpeed = 1f; } catch { }
                try { ptrs.Add(GameDumps.Ptr(z)); } catch { }
                n++;
            }
            catch { }
        }
        var dump = new Dictionary<string, object>
        {
            ["count"] = n,
            ["ptrs"] = ptrs
        };
        if (!string.IsNullOrWhiteSpace(status))
            dump["status"] = status.ToLowerInvariant();
        if (ptrs.Count == 1)
            dump["ptr"] = ptrs[0];
        DebugRuntime.Emit("debug.status.cleared", dump);
    }

    public static void ApplyStatusToZombie(Zombie z, string status, float duration, int level, bool method)
    {
        if (z == null) return;
        if (!method)
        {
            switch (status)
            {
                case "butter": z.butterSpeed = 0f; break;
                case "freeze": z.freezeSpeed = 0f; break;
                case "cold": z.coldSpeed = 0.5f; break;
            }
            return;
        }
        switch (status)
        {
            case "butter":
                z.Buttered(duration, true);
                break;
            case "freeze":
                z.SetFreeze(duration, level);
                break;
            case "cold":
                z.SetCold(duration, level, false);
                break;
            case "poison":
                try { z.SetPoison(duration); }
                catch { CheatState.Error("SetPoison unavailable"); }
                break;
            case "hypno":
                z.SetMindControl(1);
                break;
        }
    }

    public static void Kill(JsonElement p, bool plants)
    {
        var target = Str(p, "target") ?? "selected";
        if (plants)
        {
            if (target is "all" or "all-plants")
                CheatActions.DeleteAllPlants();
            else
            {
                foreach (var pl in ResolvePlants(target, p))
                {
                    try { pl.Die(Plant.DieReason.BySelf); } catch { }
                }
            }
            return;
        }

        if (target is "all" or "all-zombies")
            CheatActions.DeleteAllZombies();
        else
            CheatActions.OneShotSelected();
    }

    public static void WaveFreeze(bool enabled)
    {
        CheatState.SetToggle("F-WAVE-FREEZE", enabled, "debug");
        DebugRuntime.Emit("debug.wave.freeze", new Dictionary<string, object> { ["enabled"] = enabled });
    }

    public static void EnsureSun(float value = 9999f)
    {
        CheatActions.SetEconomy("sun", value, add: false);
        CheatState.SetToggle("G-FREE-SET", true, "debug", emitInject: false);
    }

    /// <summary>
    /// Gated LIVE probe: call <c>UIMgr.EnterGame</c>. Requires <c>FUSIONRPG_LEVEL_ENTRY=1</c>
    /// or toggle <c>DEBUG-LEVEL-ENTRY</c>. Rejects when a Board is already live unless <c>force</c>.
    /// </summary>
    public static void EnterLevel(JsonElement p)
    {
        var envOn = string.Equals(
            Environment.GetEnvironmentVariable("FUSIONRPG_LEVEL_ENTRY"), "1", StringComparison.Ordinal);
        var toggleOn = CheatState.On("DEBUG-LEVEL-ENTRY");
        var force = p.TryGetProperty("force", out var fEl) && fEl.ValueKind == JsonValueKind.True;

        var dump = new Dictionary<string, object>
        {
            ["env"] = envOn,
            ["toggle"] = toggleOn,
            ["force"] = force
        };

        if (!envOn && !toggleOn)
        {
            dump["ok"] = false;
            dump["error"] = "disabled — set FUSIONRPG_LEVEL_ENTRY=1 or enable DEBUG-LEVEL-ENTRY";
            DebugRuntime.Emit("debug.level.enter", dump);
            CheatState.Error("debug.enter-level: disabled");
            return;
        }

        if (GameHooks.Board != null && !force)
        {
            dump["ok"] = false;
            dump["error"] = "board already live — return to main menu, or pass force=true (unsafe)";
            DebugRuntime.Emit("debug.level.enter", dump);
            CheatState.Error("debug.enter-level: board already live");
            return;
        }

        LevelType levelType;
        try
        {
            levelType = ParseLevelType(p);
        }
        catch (Exception ex)
        {
            dump["ok"] = false;
            dump["error"] = ex.Message;
            DebugRuntime.Emit("debug.level.enter", dump);
            CheatState.Error("debug.enter-level: " + ex.Message);
            return;
        }

        var levelNumber = Int(p, "levelNumber", 1);
        var id = Int(p, "id", 0);
        var name = Str(p, "name") ?? "";
        dump["levelType"] = levelType.ToString();
        dump["levelTypeInt"] = (int)levelType;
        dump["levelNumber"] = levelNumber;
        dump["id"] = id;
        dump["name"] = name;

        try
        {
            UIMgr.EnterGame(levelType, levelNumber, id, name);
            dump["ok"] = true;
            DebugRuntime.Emit("debug.level.enter", dump);
            CheatState.Note($"debug.enter-level {levelType}#{levelNumber}");
        }
        catch (Exception ex)
        {
            dump["ok"] = false;
            dump["error"] = ex.Message;
            DebugRuntime.Emit("debug.level.enter", dump);
            CheatState.Error("debug.enter-level: " + ex.Message);
        }
    }

    static LevelType ParseLevelType(JsonElement p)
    {
        if (p.TryGetProperty("levelType", out var el))
        {
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
                return (LevelType)n;
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString() ?? "";
                if (int.TryParse(s, out var asInt))
                    return (LevelType)asInt;
                if (Enum.TryParse(s, ignoreCase: true, out LevelType parsed))
                    return parsed;
                // Game spelling Advanture — accept Adventure typo.
                if (string.Equals(s, "Adventure", StringComparison.OrdinalIgnoreCase))
                    return LevelType.Advanture;
                throw new ArgumentException("unknown levelType string: " + s);
            }
        }

        return LevelType.Advanture;
    }

    public static void Select(JsonElement p)
    {
        var side = Str(p, "side") ?? "zombie";
        if (p.TryGetProperty("ptr", out var ptrEl) && ptrEl.ValueKind == JsonValueKind.String)
        {
            var hex = CombatPtr.Normalize(ptrEl.GetString());
            if (long.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var v))
                CheatState.Select(new IntPtr(v), side);
            return;
        }

        // Select living plant by col/row (LIVE prove: spawn A+B then grant entity on A).
        if (string.Equals(side, "plant", StringComparison.OrdinalIgnoreCase) &&
            p.TryGetProperty("col", out var colEl) && colEl.TryGetInt32(out var col) &&
            p.TryGetProperty("row", out var rowEl) && rowEl.TryGetInt32(out var row))
        {
            foreach (var plant in UObject.FindObjectsOfType<Plant>())
            {
                try
                {
                    if (plant == null || plant.thePlantType == PlantType.Nothing) continue;
                    if (plant.thePlantColumn != col || plant.thePlantRow != row) continue;
                    CheatState.Select(plant.Pointer, "plant");
                    return;
                }
                catch { }
            }

            CheatState.Error($"debug.select: no plant at col={col} row={row}");
            return;
        }

        if (string.Equals(side, "plant", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var plant in UObject.FindObjectsOfType<Plant>())
            {
                try
                {
                    if (plant == null || plant.thePlantType == PlantType.Nothing) continue;
                    CheatState.Select(plant.Pointer, "plant");
                    return;
                }
                catch { }
            }

            CheatState.Error("debug.select: no living plant");
            return;
        }

        if (p.TryGetProperty("row", out var zRowEl) && zRowEl.TryGetInt32(out var zRow))
        {
            var wantX = p.TryGetProperty("x", out var zxEl) && zxEl.TryGetSingle(out var zxv) ? zxv : (float?)null;
            Zombie? best = null;
            var bestDx = float.MaxValue;
            foreach (var z in UObject.FindObjectsOfType<Zombie>())
            {
                try
                {
                    if (z == null || z.theZombieType == ZombieType.Nothing) continue;
                    if (z.theZombieRow != zRow) continue;
                    if (wantX == null)
                    {
                        CheatState.Select(z.Pointer, "zombie");
                        return;
                    }

                    var dx = Math.Abs(z.transform.position.x - wantX.Value);
                    if (dx >= bestDx) continue;
                    bestDx = dx;
                    best = z;
                }
                catch { }
            }

            if (best != null)
            {
                CheatState.Select(best.Pointer, "zombie");
                return;
            }

            CheatState.Error($"debug.select: no zombie at row={zRow}");
            return;
        }

        foreach (var z in UObject.FindObjectsOfType<Zombie>())
        {
            try
            {
                if (z == null || z.theZombieType == ZombieType.Nothing) continue;
                CheatState.Select(z.Pointer, "zombie");
                return;
            }
            catch { }
        }

        CheatState.Error("debug.select: no living zombie");
    }

    /// <summary>Living plant at col/row → hex ptr for synthetic OnDamageDealt actor.</summary>
    public static string? TryResolvePlantPtr(int col, int row, out int typeId)
    {
        typeId = 0;
        foreach (var plant in UObject.FindObjectsOfType<Plant>())
        {
            try
            {
                if (plant == null || plant.thePlantType == PlantType.Nothing) continue;
                if (plant.thePlantColumn != col || plant.thePlantRow != row) continue;
                typeId = (int)plant.thePlantType;
                return plant.Pointer.ToString("X");
            }
            catch { }
        }

        return null;
    }

    /// <summary>Living zombie on row (optional x pick) → hex ptr for synthetic target.</summary>
    public static string? TryResolveZombiePtr(int row, float? x, out int typeId)
    {
        typeId = 0;
        Zombie? best = null;
        var bestDx = float.MaxValue;
        foreach (var z in UObject.FindObjectsOfType<Zombie>())
        {
            try
            {
                if (z == null || z.theZombieType == ZombieType.Nothing) continue;
                if (!LawnCoords.ZombieInRow(z, row)) continue;
                if (x == null)
                {
                    typeId = (int)z.theZombieType;
                    return z.Pointer.ToString("X");
                }

                var dx = Math.Abs(z.transform.position.x - x.Value);
                if (dx >= bestDx) continue;
                bestDx = dx;
                best = z;
            }
            catch { }
        }

        if (best == null) return null;
        typeId = (int)best.theZombieType;
        return best.Pointer.ToString("X");
    }

    static IEnumerable<Zombie> ResolveZombies(string target, JsonElement p)
    {
        if (target is "all" or "all-zombies")
        {
            foreach (var z in UObject.FindObjectsOfType<Zombie>())
                if (z != null) yield return z;
            yield break;
        }
        if (p.TryGetProperty("ptr", out var ptrEl) && ptrEl.ValueKind == JsonValueKind.String)
        {
            var hex = ptrEl.GetString() ?? "";
            foreach (var z in UObject.FindObjectsOfType<Zombie>())
            {
                if (z != null && string.Equals(GameDumps.Ptr(z), hex, StringComparison.OrdinalIgnoreCase))
                {
                    yield return z;
                    yield break;
                }
            }
        }
        if (CheatState.SelectedSide == "zombie" && CheatState.SelectedPtr != IntPtr.Zero)
        {
            foreach (var z in UObject.FindObjectsOfType<Zombie>())
            {
                if (z != null && z.Pointer == CheatState.SelectedPtr)
                {
                    yield return z;
                    yield break;
                }
            }
        }
        foreach (var z in UObject.FindObjectsOfType<Zombie>())
        {
            if (z != null) { yield return z; yield break; }
        }
    }

    static IEnumerable<Plant> ResolvePlants(string target, JsonElement p)
    {
        if (target is "all" or "all-plants")
        {
            foreach (var pl in UObject.FindObjectsOfType<Plant>())
                if (pl != null) yield return pl;
            yield break;
        }
        if (CheatState.SelectedSide == "plant" && CheatState.SelectedPtr != IntPtr.Zero)
        {
            foreach (var pl in UObject.FindObjectsOfType<Plant>())
            {
                if (pl != null && pl.Pointer == CheatState.SelectedPtr)
                {
                    yield return pl;
                    yield break;
                }
            }
        }
    }

    static void ApplyScaleProps(JsonElement p, bool plant)
    {
        var prefix = plant ? "A-P-" : "A-Z-";
        if (HasNum(p, "hpPercent", out var hpP)) CheatState.SetFloat(prefix + "HP%", hpP, "debug");
        if (HasNum(p, "hpFlat", out var hpF)) CheatState.SetFloat(prefix + "HP+", hpF, "debug");
        if (HasNum(p, "attackPercent", out var atkP)) CheatState.SetFloat(prefix + "ATK%", atkP, "debug");
        if (HasNum(p, "attackFlat", out var atkF)) CheatState.SetFloat(prefix + "ATK+", atkF, "debug");
        if (HasNum(p, "defensePercent", out var defP)) CheatState.SetFloat(prefix + "DEF%", defP, "debug");
        if (HasNum(p, "defenseFlat", out var defF)) CheatState.SetFloat(prefix + "DEF+", defF, "debug");
    }

    static void ApplyAbsoluteProps(JsonElement p, bool plant)
    {
        if (plant)
        {
            if (HasInt(p, "hp", out var hp)) CheatState.SetFloat("P-HP", hp, "debug");
            if (HasInt(p, "maxHp", out var maxHp)) CheatState.SetFloat("P-MAXHP", maxHp, "debug");
            if (HasInt(p, "atk", out var atk)) CheatState.SetFloat("P-ATK", atk, "debug");
        }
        else
        {
            if (HasInt(p, "hp", out var hp)) CheatState.SetFloat("Z-HP", hp, "debug");
            if (HasInt(p, "maxHp", out var maxHp)) CheatState.SetFloat("Z-MAXHP", maxHp, "debug");
            if (HasInt(p, "armor1", out var a1)) CheatState.SetFloat("Z-ARM1", a1, "debug");
            if (HasInt(p, "armor2", out var a2)) CheatState.SetFloat("Z-ARM2", a2, "debug");
        }
    }

    static void SetFloatIf(JsonElement root, string obj, string field, string cheatId)
    {
        if (root.TryGetProperty(obj, out var o) && o.ValueKind == JsonValueKind.Object
            && o.TryGetProperty(field, out var f) && f.TryGetDouble(out var v))
            CheatState.SetFloat(cheatId, v, "debug");
    }

    static void SetFlatIf(JsonElement root, string obj, string field, string cheatId) =>
        SetFloatIf(root, obj, field, cheatId);

    static bool HasNum(JsonElement p, string name, out double v)
    {
        v = 0;
        return p.TryGetProperty(name, out var e) && e.TryGetDouble(out v);
    }

    static bool HasInt(JsonElement p, string name, out int v)
    {
        v = 0;
        return p.TryGetProperty(name, out var e) && e.TryGetInt32(out v);
    }

    static int Int(JsonElement p, string name, int dflt) =>
        p.TryGetProperty(name, out var e) && e.TryGetInt32(out var v) ? v : dflt;

    static string? Str(JsonElement p, string name) =>
        p.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

    static void MaybePinDerived(JsonElement p, string ptr)
    {
        var profile = Str(p, "derivedProfile");
        Dictionary<string, double>? overlay = null;
        if (p.TryGetProperty("derived", out var d) && d.ValueKind == JsonValueKind.Object)
        {
            overlay = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var prop in d.EnumerateObject())
            {
                if (prop.Value.TryGetDouble(out var v))
                    overlay[prop.Name] = v;
                else if (prop.Value.TryGetInt64(out var l))
                    overlay[prop.Name] = l;
            }
        }

        if (string.IsNullOrWhiteSpace(profile) && (overlay == null || overlay.Count == 0))
            return;
        InjectorDerivedOverride.PinProfile(ptr, profile, overlay);
    }

    static void MaybePinElement(JsonElement p, string ptr)
    {
        var primary = Str(p, "elementPrimary") ?? Str(p, "element.primary");
        var secondary = Str(p, "elementSecondary") ?? Str(p, "element.secondary");
        if (string.IsNullOrWhiteSpace(primary) && string.IsNullOrWhiteSpace(secondary))
            return;
        try
        {
            InjectorElementOverride.PinParse(ptr, primary, secondary);
        }
        catch (Exception ex)
        {
            CheatState.Error("debug spawn element pin: " + ex.Message);
        }
    }
}
