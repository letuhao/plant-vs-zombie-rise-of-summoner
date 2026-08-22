using System.Collections.Generic;
using FusionRpg.Contracts;
using FusionRpg.Core;
using FusionRpg.Core.Diagnostics;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Stats;
using FusionRpg.Injector.Stats;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

using FusionRpg.Injector.Host;

namespace FusionRpg.Injector;

public static class GameHooks
{
    public static Board? Board;
    public static string? MatchKey;
    public static int LastWave = -1;
    public static int CatalogPlantCount;
    public static readonly HashSet<IntPtr> Applied = new();
    public static readonly HashSet<IntPtr> DeadZombies = new();
    static readonly HashSet<IntPtr> MowerStarted = new();
    static int _lastSun = int.MinValue;
    static int _lastMoney = int.MinValue;
    static float _lastPoints = float.MinValue;
    static string? _lastLevelName;
    static bool _recipesDumped;
    static bool _conveyDumped;
    static bool _catalogRetried;
    static int _catalogPending;

    public static void ClearMatch()
    {
        // Backstop: any records still pending at a match edge drain before state clears.
        try { Effects.EventDrainHost.FlushAllAndReset(); } catch { }
        Effects.InjectorEntityRegistry.Clear();
        Effects.InjectorBoardSnapshot.Invalidate();
        Applied.Clear();
        EntityStatWriter.Clear();
        CheatState.Stats.ClearBaselines();
        // Effect ClearAll is owned by MatchHost after board.start/end Apply (W2-C).
        DeadZombies.Clear();
        MowerStarted.Clear();
        LastWave = -1;
        _lastSun = int.MinValue;
        _lastMoney = int.MinValue;
        _lastPoints = float.MinValue;
        _lastLevelName = null;
        _conveyDumped = false;
        _catalogRetried = false;
    }

    internal static bool Ready() => Board != null && RpgHost.Client != null;

    internal static void Emit(string kind, object payload)
    {
        PerfProbe.CountEmit(kind);
        Dictionary<string, object>? dict = null;
        try
        {
            dict = CoercePayloadDict(payload);
            if (dict != null &&
                string.Equals(kind, "board.start", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(MatchKey) &&
                !dict.ContainsKey("matchKey"))
            {
                dict["matchKey"] = MatchKey!;
            }

            using var _perf = PerfProbe.Measure(PerfSection.MatchApply);
            Match.MatchHost.Apply(kind, dict);
        }
        catch (Exception ex)
        {
            try { CheatState.Error("match apply: " + ex.Message); } catch { }
        }

        try
        {
            if (dict != null)
                Effects.EffectRuntime.OnCapture(kind, dict);
        }
        catch (Exception ex)
        {
            try { CheatState.Error("effect capture: " + ex.Message); } catch { }
        }

        RpgHost.Client?.Enqueue(kind, payload, MatchKey);
    }

    static Dictionary<string, object>? CoercePayloadDict(object payload)
    {
        if (payload is Dictionary<string, object> d) return d;
        if (payload is IReadOnlyDictionary<string, object> ro)
            return new Dictionary<string, object>(ro);
        if (payload is IDictionary<string, object> id)
            return new Dictionary<string, object>(id);
        return null;
    }

    public static void RequestTypeCatalog() => Interlocked.Exchange(ref _catalogPending, 1);

    public static void PumpMainThread()
    {
        if (Interlocked.CompareExchange(ref _catalogPending, 0, 1) != 1) return;
        try { EnqueueTypeCatalog(); }
        catch (Exception ex) { RpgHost.Log.Warning("type catalog: " + ex.Message); }
    }

    public static void EnqueueTypeCatalog()
    {
        CatalogPlantCount = 0;
        try { CatalogPlantCount += EnqueueEnumSide("plant", typeof(PlantType)); }
        catch (Exception ex) { RpgHost.Log.Warning("catalog plants: " + ex.Message); }
        try { EnqueueEnumSide("zombie", typeof(ZombieType)); }
        catch (Exception ex) { RpgHost.Log.Warning("catalog zombies: " + ex.Message); }
        try { EnqueueEnumSide("pet", typeof(PetType)); }
        catch (Exception ex) { RpgHost.Log.Warning("catalog pets: " + ex.Message); }
        try { EnqueueEnumSide("grid", typeof(GridItemType)); }
        catch (Exception ex) { RpgHost.Log.Warning("catalog grid: " + ex.Message); }
        try { EnqueueEnumSide("mower", typeof(MowerType)); }
        catch (Exception ex) { RpgHost.Log.Warning("catalog mowers: " + ex.Message); }
        EnqueueRecipes();
    }

    static int EnqueueEnumSide(string side, Type enumType)
    {
        const int chunk = 400;
        var entries = new List<Dictionary<string, object>>(chunk);
        var n = 0;
        foreach (var (id, name) in EnumerateEnum(enumType))
        {
            if (string.Equals(name, "Nothing", StringComparison.Ordinal)) continue;
            entries.Add(new Dictionary<string, object>
            {
                ["type"] = id,
                ["typeName"] = name
            });
            n++;
            if (entries.Count >= chunk)
            {
                EmitCatalogChunk(side, entries);
                entries = new List<Dictionary<string, object>>(chunk);
            }
        }
        if (entries.Count > 0)
            EmitCatalogChunk(side, entries);
        return n;
    }

    static IEnumerable<(int id, string name)> EnumerateEnum(Type enumType)
    {
        var seen = new HashSet<int>();
        List<(int id, string name)>? il2 = null;
        try { il2 = Il2CppEnumValues(enumType); }
        catch { il2 = null; }
        if (il2 != null)
        {
            foreach (var v in il2)
            {
                if (!seen.Add(v.id)) continue;
                yield return v;
            }
        }

        if (seen.Count > 0) yield break;

        for (var i = -1; i <= 6000; i++)
        {
            object boxed;
            try { boxed = Enum.ToObject(enumType, i); }
            catch { continue; }
            string name;
            try { name = boxed.ToString() ?? ""; }
            catch { continue; }
            if (string.IsNullOrEmpty(name) || name == i.ToString()) continue;
            if (!seen.Add(i)) continue;
            yield return (i, name);
        }
    }

    static List<(int id, string name)> Il2CppEnumValues(Type enumType)
    {
        var list = new List<(int id, string name)>();
        var native = NativeClassPtr(enumType);
        if (native == IntPtr.Zero) return list;
        var il2Type = Il2CppSystem.Type.internal_from_handle(IL2CPP.il2cpp_class_get_type(native));
        var values = Il2CppSystem.Enum.GetValues(il2Type);
        if (values == null || values.Length == 0) return list;
        for (var i = 0; i < values.Length; i++)
        {
            object? raw;
            try { raw = values.GetValue(i); }
            catch { continue; }
            int id;
            try { id = Convert.ToInt32(raw); }
            catch
            {
                try { id = Convert.ToInt32(raw?.ToString()); }
                catch { continue; }
            }
            string name;
            try { name = Enum.ToObject(enumType, id).ToString() ?? id.ToString(); }
            catch { name = id.ToString(); }
            list.Add((id, name));
        }
        return list;
    }

    static IntPtr NativeClassPtr(Type enumType)
    {
        if (enumType == typeof(PlantType)) return Il2CppClassPointerStore<PlantType>.NativeClassPtr;
        if (enumType == typeof(ZombieType)) return Il2CppClassPointerStore<ZombieType>.NativeClassPtr;
        if (enumType == typeof(PetType)) return Il2CppClassPointerStore<PetType>.NativeClassPtr;
        if (enumType == typeof(GridItemType)) return Il2CppClassPointerStore<GridItemType>.NativeClassPtr;
        if (enumType == typeof(MowerType)) return Il2CppClassPointerStore<MowerType>.NativeClassPtr;
        return IntPtr.Zero;
    }

    public static void EnqueueRecipes()
    {
        if (_recipesDumped) return;
        try
        {
            PlantMixTreeManager.Init();
            var dict = PlantMixTreeManager.ChildToParents;
            if (dict == null) return;
            const int chunk = 200;
            var entries = new List<Dictionary<string, object>>(chunk);
            foreach (var pair in dict)
            {
                var list = pair.Value;
                if (list == null) continue;
                foreach (var info in list)
                {
                    int a = 0, b = 0, r = 0;
                    try { a = (int)info.ParentA; b = (int)info.ParentB; r = (int)info.Result; }
                    catch { continue; }
                    entries.Add(new Dictionary<string, object>
                    {
                        ["parentA"] = a,
                        ["parentAName"] = GameDumps.EnumName(info.ParentA),
                        ["parentB"] = b,
                        ["parentBName"] = GameDumps.EnumName(info.ParentB),
                        ["result"] = r,
                        ["resultName"] = GameDumps.EnumName(info.Result)
                    });
                    if (entries.Count >= chunk)
                    {
                        RpgHost.Client?.Enqueue("catalog.recipes", new Dictionary<string, object> { ["entries"] = entries });
                        entries = new List<Dictionary<string, object>>(chunk);
                    }
                }
            }
            if (entries.Count > 0)
                RpgHost.Client?.Enqueue("catalog.recipes", new Dictionary<string, object> { ["entries"] = entries });
            _recipesDumped = true;
        }
        catch (Exception ex)
        {
            RpgHost.Log.Warning("catalog recipes: " + ex.Message);
        }
    }

    static void EmitCatalogChunk(string side, List<Dictionary<string, object>> entries)
    {
        RpgHost.Client?.Enqueue("catalog.types", new Dictionary<string, object>
        {
            ["side"] = side,
            ["entries"] = entries
        });
    }

    public static void PollBoard()
    {
        var board = Board;
        if (board == null) return;
        try
        {
            var wave = board.theWave;
            if (wave != LastWave)
            {
                LastWave = wave;
                var payload = GameDumps.LiveBoard(board);
                payload["wave"] = wave;
                payload["maxWave"] = board.theMaxWave;
                Emit("wave.change", payload);
            }
        }
        catch { }

        try
        {
            var sun = board.theSun;
            var money = board.theMoney;
            var points = board.thePoints;
            if (sun != _lastSun || money != _lastMoney || !Mathf.Approximately(points, _lastPoints))
            {
                _lastSun = sun;
                _lastMoney = money;
                _lastPoints = points;
                var eco = GameDumps.LiveBoard(board);
                CheatState.TagProbe(eco);
                Emit("board.economy", eco);
            }
        }
        catch { }

        try
        {
            var name = LevelName();
            if (!string.IsNullOrWhiteSpace(name) && name != "Unknown" && name != _lastLevelName)
            {
                _lastLevelName = name;
                Emit("level.name", new Dictionary<string, object> { ["levelName"] = name });
            }
        }
        catch { }

        try
        {
            var arr = board.mowerArray;
            if (arr != null)
            {
                foreach (var m in arr)
                {
                    if (m == null || !m.started) continue;
                    if (!MowerStarted.Add(m.Pointer)) continue;
                    Emit("mower.start", new Dictionary<string, object>
                    {
                        ["ptr"] = GameDumps.Ptr(m),
                        ["type"] = (int)m.theMowerType,
                        ["typeName"] = m.theMowerType.ToString(),
                        ["polled"] = true
                    });
                }
            }
        }
        catch { }

        if (!_conveyDumped)
        {
            try
            {
                var cm = ConveyManager.Instance;
                if (cm != null)
                {
                    var pool = cm.GetCardPool();
                    var types = new List<Dictionary<string, object>>();
                    if (pool != null)
                    {
                        foreach (var item in pool)
                        {
                            try
                            {
                                var tid = Convert.ToInt32(item);
                                var tname = item.ToString() ?? "";
                                SpawnCatalog.Note("plant", tid, tname, "convey.pool");
                                types.Add(new Dictionary<string, object>
                                {
                                    ["type"] = tid,
                                    ["typeName"] = tname
                                });
                            }
                            catch { }
                        }
                    }
                    Emit("convey.pool", new Dictionary<string, object> { ["types"] = types });
                    _conveyDumped = true;
                }
            }
            catch { _conveyDumped = true; }
        }
    }

    internal static string LevelName()
    {
        try
        {
            if (InGameUI.Instance != null)
            {
                var ui = InGameUI.Instance;
                if (ui.LevelName1 != null && !string.IsNullOrWhiteSpace(ui.LevelName1.text)) return ui.LevelName1.text;
                if (ui.LevelName2 != null && !string.IsNullOrWhiteSpace(ui.LevelName2.text)) return ui.LevelName2.text;
                if (ui.LevelName3 != null && !string.IsNullOrWhiteSpace(ui.LevelName3.text)) return ui.LevelName3.text;
            }
        }
        catch { }
        return "Unknown";
    }

    [HarmonyPatch(typeof(Board), nameof(Board.Awake))]
    public static class BoardAwake
    {
        public static void Postfix(Board __instance)
        {
            Board = __instance;
            ClearMatch();
            MatchKey = Guid.NewGuid().ToString();
            try { LastWave = __instance.theWave; }
            catch { LastWave = -1; }
            Dictionary<string, object> modifiers;
            try { modifiers = GameDumps.BoardConfig(__instance.config); }
            catch { modifiers = new Dictionary<string, object>(); }
            var start = new Dictionary<string, object>
            {
                ["matchKey"] = MatchKey!,
                ["levelName"] = LevelName(),
                ["modifiers"] = modifiers
            };
            try { start["boardLevel"] = GameAPP.theBoardLevel; } catch { }
            try { start["levelType"] = GameAPP.theBoardType.ToString(); } catch { }
            try { start["theTotalNumOfZombie"] = __instance.theTotalNumOfZombie; } catch { }
            Emit("board.start", start);
            try { Hud.OverlaySwitch.OnMatchStart(); } catch { }
            Emit("board.modifiers", modifiers);
            try { InjectorBootstrap.EnsureCombatHitPatches(); } catch (Exception ex) { RpgHost.Log.Warning("EnsureCombatHitPatches: " + ex.Message); }
            try
            {
                if (CheatState.BoardConfigLocked)
                    CheatActions.ApplyBoardConfig();
                else
                    CheatActions.LoadBoardConfigIntoCheats();
            }
            catch { }
            if (CatalogPlantCount == 0 && !_catalogRetried)
            {
                _catalogRetried = true;
                RequestTypeCatalog();
            }
        }
    }

    [HarmonyPatch(typeof(Board), nameof(Board.OnDestroy))]
    public static class BoardDestroy
    {
        public static void Postfix(Board __instance)
        {
            if (Board != null && Board.Pointer == __instance.Pointer)
                Board = null;
        }
    }

    [HarmonyPatch(typeof(Board), nameof(Board.Die))]
    public static class BoardDie
    {
        public static void Postfix()
        {
            // v2 lifecycle barrier: drain everything before board.end triggers ClearAll (SSOT §A1).
            try { Effects.EventDrainHost.FlushAllAndReset(); } catch { }
            Dictionary<string, object> summary;
            try { summary = GameDumps.BoardStats(Board, Board?.boardStatistics); }
            catch { summary = new Dictionary<string, object>(); }
            Emit("board.end", new Dictionary<string, object>
            {
                ["levelName"] = LevelName(),
                ["summary"] = summary
            });
            try { Hud.OverlaySwitch.OnMatchEnd(); } catch { }
            MatchKey = null;
            ClearMatch();
            try { Fx.VfxDirector.ClearAll(); } catch { }
        }
    }

    [HarmonyPatch(typeof(BoardStatistics), nameof(BoardStatistics.GameOver))]
    public static class StatsGameOver
    {
        public static void Postfix(BoardStatistics __instance, GameResult result)
        {
            if (result == GameResult.None) return;
            var name = result.ToString().ToLowerInvariant();
            Emit("match.result", new Dictionary<string, object> { ["result"] = name });
            Emit("board.snapshot", GameDumps.BoardStats(Board, __instance));
        }
    }

    [HarmonyPatch(typeof(GameLose), nameof(GameLose.HandleGameLose))]
    public static class GameLoseHook
    {
        public static void Postfix()
        {
            Emit("match.lose", new Dictionary<string, object>());
        }
    }

    [HarmonyPatch(typeof(BoardVictory), nameof(BoardVictory.Win))]
    public static class BoardVictoryWin
    {
        public static void Postfix()
        {
            Emit("match.win", new Dictionary<string, object>());
        }
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.Start))]
    public static class PlantStart
    {
        public static void Postfix(Plant __instance)
        {
            if (__instance == null) return;
            Effects.InjectorEntityRegistry.Add(__instance);
            Effects.InjectorBoardSnapshot.Invalidate();
            if (!Ready()) return;
            if (__instance.thePlantType == PlantType.Nothing) return;
            ApplyPlant(__instance, "start");
        }
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.Die))]
    public static class PlantDie
    {
        public static void Postfix(Plant __instance, Plant.DieReason reason)
        {
            if (__instance == null) return;
            // v2 barrier: pending hit records for this plant drain before its grants withdraw.
            try { Effects.EventDrainHost.FlushForPtr(__instance.Pointer); } catch { }
            try { Effects.InjectorEntityRegistry.Remove(__instance.Pointer); } catch { }
            Effects.InjectorBoardSnapshot.Invalidate();
            // OnDeath must see entity grants; ForgetEntity withdraws after Emit.
            Emit("plant.die", new Dictionary<string, object>
            {
                ["type"] = (int)__instance.thePlantType,
                ["typeName"] = GameDumps.EnumName(__instance.thePlantType),
                ["ptr"] = GameDumps.Ptr(__instance),
                ["reason"] = (int)reason,
                ["reasonName"] = GameDumps.EnumName(reason)
            });
            ForgetEntity(__instance.Pointer);
        }
    }

    // v5 hotfix (v5-600z-war finding): a full StatSystem.Resolve ran per TakeDamage —
    // 2,100+/s under war load, a top allocator. The defense scale only changes with the
    // cheat document or pvz-mod revisions, so resolve once per revision pair per side.
    static long _dmgScaleDocRev = long.MinValue;
    static long _dmgScalePvzRev = long.MinValue;
    static bool _dmgScaleActive;
    static float _plantDefPct = 1f;
    static int _plantDefFlat;
    static float _zombieDefPct = 1f;
    static int _zombieDefFlat;

    static void EnsureDamageScaleCache(StatsConfig s)
    {
        if (_dmgScaleDocRev == CheatState.DocumentRevision && _dmgScalePvzRev == CheatState.PvzStatsRevision)
            return;
        _dmgScaleDocRev = CheatState.DocumentRevision;
        _dmgScalePvzRev = CheatState.PvzStatsRevision;
        var hasPvz = CheatState.HasPvzStatsMods();
        _dmgScaleActive = s.ApplyStats || hasPvz;
        if (!_dmgScaleActive) return;
        var baseline = new EntityBaseline { Hp = 1, MaxHp = 1, Atk = 1 };
        var pf = CheatState.Stats.Resolve(CheatState.Stats.Contexts.ForPlant(
            "dmg", baseline, cheatScale: s, applyStats: true, pvzStatsMods: CheatState.PvzStatsMods));
        _plantDefPct = pf.DefensePercent;
        _plantDefFlat = pf.DefenseFlat;
        var zf = CheatState.Stats.Resolve(CheatState.Stats.Contexts.ForZombie(
            "dmg", baseline, cheatScale: s, applyStats: true, pvzStatsMods: CheatState.PvzStatsMods));
        _zombieDefPct = zf.DefensePercent;
        _zombieDefFlat = zf.DefenseFlat;
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.TakeDamage))]
    public static class PlantTakeDamage
    {
        // Plant signature: (int damage, IDamageMaker damageFrom, DamageType damageType, PlantType reportType, bool fix)
        public static void Prefix(Plant __instance, ref int damage, IDamageMaker damageFrom, DamageType damageType, PlantType reportType, bool fix)
        {
            using var _perf = PerfProbe.Measure(PerfSection.TakeDamagePrefix);
            if (CheatState.On("P-GOD")) { damage = 0; return; }
            if (OverlayApplyGuard.IsActive) return;
            var s = CheatState.EffectiveStats();
            var before = damage;
            EnsureDamageScaleCache(s);
            if (_dmgScaleActive)
                damage = StatMath.ScaleIncoming(damage, _plantDefPct, _plantDefFlat);
            // v2 record path (Task 9) — mirrors ZombieTakeDamage; melee bites consume the
            // AttackPlant prefix's pending pair id inside TryRecordTaken.
            var telemetry = s.LogDamage || (RpgHost.Client?.Stats.LogDamage ?? false) || DebugRuntime.SessionActive;
            if (telemetry)
            {
                var payload = new Dictionary<string, object>
                {
                    ["ptr"] = GameDumps.Ptr(__instance),
                    ["damage"] = damage,
                    ["before"] = before,
                    ["after"] = damage,
                    ["path"] = "take",
                    ["reportType"] = (int)reportType,
                    ["damageType"] = (int)damageType
                };
                TryStampDamageFrom(payload, damageFrom);
                DebugRuntime.Stamp(payload);
                Emit("plant.damage", payload);
            }
            else if (Effects.EventDrainHost.Enabled)
            {
                var pType = 0;
                try { pType = (int)__instance.thePlantType; } catch { }
                var actorPtr = IntPtr.Zero;
                try { if (damageFrom is Il2CppObjectBase dfObj) actorPtr = dfObj.Pointer; } catch { }
                Effects.EventDrainHost.TryRecordDealtFromBullet(
                    Core.Events.GameEventSide.Plant, damageFrom as Il2CppObjectBase, __instance.Pointer, pType, damage, out var wasBullet);
                if (!wasBullet)
                    Effects.EventDrainHost.TryRecordTaken(
                        Core.Events.GameEventSide.Plant, __instance.Pointer, pType, actorPtr, damage);
            }
            else if (Effects.EffectRuntime.HasOnDamageTakenGrant())
            {
                var payload = new Dictionary<string, object>
                {
                    ["ptr"] = GameDumps.Ptr(__instance),
                    ["damage"] = damage,
                    ["path"] = "take"
                };
                TryStampDamageFrom(payload, damageFrom);
                Emit("plant.damage", payload);
            }
            // Hit* Harmony skipped — drive on-hit arms + combat.hit identity from TakeDamage.
            try { DebugRuntime.OnCombatHit("plant", null, __instance); } catch (Exception ex) { CheatState.Error("debug onhit plant: " + ex.Message); }
            if (!Effects.EventDrainHost.Enabled || telemetry)
                TryEmitCombatHitFromBullet("plant", damageFrom, plantTarget: __instance, zombieTarget: null, fallbackDamage: damage);
        }
    }

    [HarmonyPatch(typeof(Zombie), nameof(Zombie.Start))]
    public static class ZombieStart
    {
        public static void Postfix(Zombie __instance)
        {
            if (__instance == null) return;
            Effects.InjectorEntityRegistry.Add(__instance);
            Effects.InjectorBoardSnapshot.Invalidate();
            if (!Ready() || __instance.theZombieType == ZombieType.Nothing) return;
            ApplyZombie(__instance, "start");
        }
    }

    [HarmonyPatch(typeof(Zombie), nameof(Zombie.InitHealth))]
    public static class ZombieInitHealth
    {
        public static void Postfix(Zombie __instance)
        {
            Effects.InjectorEntityRegistry.Add(__instance);
            Effects.InjectorBoardSnapshot.Invalidate();
            if (!Ready() || __instance == null || __instance.theZombieType == ZombieType.Nothing) return;
            ApplyZombie(__instance, "initHealth");
        }
    }

    [HarmonyPatch(typeof(Zombie), nameof(Zombie.Die))]
    public static class ZombieDie
    {
        public static void Prefix(Zombie __instance, int reason)
        {
            NoteZombieDead(__instance, reason);
        }
    }

    [HarmonyPatch(typeof(Zombie), nameof(Zombie.DestoryZombie))]
    public static class ZombieDestroy
    {
        public static void Prefix(Zombie __instance)
        {
            NoteZombieDead(__instance, -1);
        }
    }

    [HarmonyPatch(typeof(Zombie), nameof(Zombie.TakeDamage))]
    public static class ZombieTakeDamage
    {
        public static void Prefix(Zombie __instance, ref int theDamage, IDamageMaker damageFrom, DamageType theDamageType, PlantType reportType, bool fix)
        {
            using var _perf = PerfProbe.Measure(PerfSection.TakeDamagePrefix);
            if (CheatState.On("Z-GOD")) { theDamage = 0; return; }
            if (OverlayApplyGuard.IsActive) return;
            var s = CheatState.EffectiveStats();
            var before = theDamage;
            EnsureDamageScaleCache(s);
            if (_dmgScaleActive)
                theDamage = StatMath.ScaleIncoming(theDamage, _zombieDefPct, _zombieDefFlat);
            // v2 record path (Task 9): telemetry/session keeps the legacy dict path for
            // fidelity; otherwise grants get compact records via the drain host.
            var telemetry = s.LogDamage || (RpgHost.Client?.Stats.LogDamage ?? false) || DebugRuntime.SessionActive;
            if (telemetry)
            {
                var payload = new Dictionary<string, object>
                {
                    ["ptr"] = GameDumps.Ptr(__instance),
                    ["damage"] = theDamage,
                    ["before"] = before,
                    ["after"] = theDamage,
                    ["path"] = "take",
                    ["reportType"] = (int)reportType,
                    ["damageType"] = (int)theDamageType
                };
                TryStampDamageFrom(payload, damageFrom);
                DebugRuntime.Stamp(payload);
                Emit("zombie.damage", payload);
            }
            else if (Effects.EventDrainHost.Enabled)
            {
                var zType = 0;
                try { zType = (int)__instance.theZombieType; } catch { }
                var actorPtr = IntPtr.Zero;
                try { if (damageFrom is Il2CppObjectBase dfObj) actorPtr = dfObj.Pointer; } catch { }
                // Bullet damage suppresses the taken record at source, matching v1 policy —
                // including on ring overflow (I2: a dropped bullet-dealt must not turn into
                // a spurious taken).
                Effects.EventDrainHost.TryRecordDealtFromBullet(
                    Core.Events.GameEventSide.Zombie, damageFrom as Il2CppObjectBase, __instance.Pointer, zType, theDamage, out var wasBullet);
                if (!wasBullet)
                    Effects.EventDrainHost.TryRecordTaken(
                        Core.Events.GameEventSide.Zombie, __instance.Pointer, zType, actorPtr, theDamage);
            }
            else if (Effects.EffectRuntime.HasOnDamageTakenGrant())
            {
                // v2 host off: preserve the audit-4c.2 fix via the legacy path.
                var payload = new Dictionary<string, object>
                {
                    ["ptr"] = GameDumps.Ptr(__instance),
                    ["damage"] = theDamage,
                    ["path"] = "take"
                };
                TryStampDamageFrom(payload, damageFrom);
                Emit("zombie.damage", payload);
            }
            // Hit* Harmony skipped — drive on-hit arms + combat.hit identity from TakeDamage.
            try { DebugRuntime.OnCombatHit("zombie", __instance, null); } catch (Exception ex) { CheatState.Error("debug onhit zombie: " + ex.Message); }
            if (!Effects.EventDrainHost.Enabled || telemetry)
                TryEmitCombatHitFromBullet("zombie", damageFrom, plantTarget: null, zombieTarget: __instance, fallbackDamage: theDamage);
        }
    }

    static void TryStampDamageFrom(Dictionary<string, object> payload, IDamageMaker? damageFrom)
    {
        if (damageFrom == null)
        {
            payload["damageFromNull"] = true;
            return;
        }
        try
        {
            if (damageFrom is not Il2CppObjectBase obj) return;
            payload["damageFrom"] = GameDumps.Ptr(obj);
            try
            {
                var sysObj = obj.TryCast<Il2CppSystem.Object>();
                if (sysObj != null) payload["damageFromClass"] = sysObj.GetIl2CppType().Name;
            }
            catch { }
            try { payload["damageFromIsBullet"] = obj.TryCast<Bullet>() != null; } catch { payload["damageFromIsBullet"] = false; }
            try { payload["damageFromIsZombie"] = obj.TryCast<Zombie>() != null; } catch { payload["damageFromIsZombie"] = false; }
            try { payload["damageFromIsPlant"] = obj.TryCast<Plant>() != null; } catch { payload["damageFromIsPlant"] = false; }
        }
        catch
        {
            /* optional enrich */
        }
    }

    /// <summary>
    /// Safe <c>combat.hit</c> identity without base <c>Bullet.Hit*</c> Harmony.
    /// Bullet attackers (pea→zombie, projectile→plant): cast <see cref="Bullet"/>.
    /// Melee bite (zombie→plant): cast <see cref="Zombie"/> — no fake bullet fields.
    /// </summary>
    static void TryEmitCombatHitFromBullet(
        string side,
        IDamageMaker? damageFrom,
        Plant? plantTarget,
        Zombie? zombieTarget,
        int fallbackDamage)
    {
        if (!Effects.EffectRuntime.ShouldEmitCombatHit()) return;
        if (damageFrom == null) return;
        try
        {
            var obj = damageFrom as Il2CppObjectBase;
            if (obj == null) return;

            Bullet? bullet = null;
            try { bullet = obj.TryCast<Bullet>(); } catch { }
            if (bullet != null)
            {
                var payload = new Dictionary<string, object>
                {
                    ["side"] = side,
                    ["source"] = "takeDamage",
                    ["attackerKind"] = "bullet",
                    ["bulletPtr"] = GameDumps.Ptr(bullet),
                    // TakeDamage amount is SSOT (pea follows plant ATK); Bullet.Damage may differ.
                    ["damage"] = fallbackDamage
                };
                try { payload["bulletType"] = (int)bullet.theBulletType; } catch { }
                try { payload["bulletDamage"] = bullet.Damage; } catch { }
                try
                {
                    payload["fromType"] = (int)bullet.fromType;
                    payload["fromTypeName"] = GameDumps.EnumName(bullet.fromType);
                }
                catch { }
                StampCombatHitTarget(payload, side, plantTarget, zombieTarget);
                DebugRuntime.Stamp(payload);
                Emit("combat.hit", payload);
                return;
            }

            // Plant-side melee combat.hit is AttackPlant-only (avoid double with TakeDamage).
        }
        catch (Exception ex) { CheatState.Error("combat.hit take: " + ex.Message); }
    }

    static void StampCombatHitTarget(
        Dictionary<string, object> payload,
        string side,
        Plant? plantTarget,
        Zombie? zombieTarget)
    {
        if (side == "zombie" && zombieTarget != null)
        {
            payload["targetPtr"] = GameDumps.Ptr(zombieTarget);
            try { payload["targetType"] = (int)zombieTarget.theZombieType; } catch { }
        }
        else if (side == "plant" && plantTarget != null)
        {
            payload["targetPtr"] = GameDumps.Ptr(plantTarget);
            try { payload["targetType"] = (int)plantTarget.thePlantType; } catch { }
        }
    }

    [HarmonyPatch(typeof(CreateMower), nameof(CreateMower.SetMower))]
    public static class MowerPlace
    {
        public static void Postfix(MowerType mowerType, float x, int row, Mower __result)
        {
            if (__result == null) return;
            Emit("mower.place", new Dictionary<string, object>
            {
                ["ptr"] = GameDumps.Ptr(__result),
                ["type"] = (int)mowerType,
                ["typeName"] = mowerType.ToString(),
                ["row"] = row
            });
        }
    }

    /// <summary>
    /// Melee bite identity: plant TakeDamage often passes the plant itself as <c>damageFrom</c>,
    /// so Bullet/Zombie casts miss. <see cref="Zombie.AttackPlant"/> has the real attacker (≈1 override).
    /// Applied deferred with HitLand — not at chainloader.
    /// </summary>
    [HarmonyPatch(typeof(Zombie), nameof(Zombie.AttackPlant))]
    public static class ZombieAttackPlant
    {
        public static void Prefix(Zombie __instance, Plant plant)
        {
            if (__instance == null || plant == null) return;
            try
            {
                if (__instance.Pointer == IntPtr.Zero || plant.Pointer == IntPtr.Zero) return;
            }
            catch { return; }
            // v2 record path (Task 9): melee dealt as a compact record with a pair id that the
            // plant's TakeDamage taken record consumes.
            if (Effects.EventDrainHost.Enabled && !DebugRuntime.SessionActive)
            {
                var dmgRec = 0;
                try { dmgRec = __instance.theAttackDamage; } catch { }
                var zTypeRec = 0;
                try { zTypeRec = (int)__instance.theZombieType; } catch { }
                var pTypeRec = 0;
                try { pTypeRec = (int)plant.thePlantType; } catch { }
                Effects.EventDrainHost.TryRecordMeleeDealt(__instance.Pointer, zTypeRec, plant.Pointer, pTypeRec, dmgRec);
                return;
            }
            if (!Effects.EffectRuntime.ShouldEmitCombatHit()) return;
            try
            {
                var damage = 0;
                try { damage = __instance.theAttackDamage; } catch { }
                var payload = new Dictionary<string, object>
                {
                    ["side"] = "plant",
                    ["source"] = "attackPlant",
                    ["attackerKind"] = "zombie",
                    ["attackerPtr"] = GameDumps.Ptr(__instance),
                    ["damage"] = damage
                };
                try
                {
                    payload["fromType"] = (int)__instance.theZombieType;
                    payload["fromTypeName"] = GameDumps.EnumName(__instance.theZombieType);
                }
                catch { }
                StampCombatHitTarget(payload, "plant", plant, null);
                DebugRuntime.Stamp(payload);
                Emit("combat.hit", payload);
            }
            catch (Exception ex) { CheatState.Error("combat.hit attackPlant: " + ex.Message); }
        }
    }

    [HarmonyPatch(typeof(Mower), nameof(Mower.StartMove))]
    public static class MowerStart
    {
        public static void Postfix(Mower __instance)
        {
            if (__instance == null) return;
            MowerStarted.Add(__instance.Pointer);
            Emit("mower.start", new Dictionary<string, object>
            {
                ["ptr"] = GameDumps.Ptr(__instance),
                ["type"] = (int)__instance.theMowerType,
                ["typeName"] = __instance.theMowerType.ToString()
            });
        }
    }

    [HarmonyPatch(typeof(Mower), nameof(Mower.Die))]
    public static class MowerDie
    {
        public static void Postfix(Mower __instance)
        {
            if (__instance == null) return;
            Emit("mower.die", new Dictionary<string, object> { ["ptr"] = GameDumps.Ptr(__instance) });
        }
    }

    [HarmonyPatch(typeof(Bullet), nameof(Bullet.InitData))]
    public static class BulletInit
    {
        public static void Postfix(Bullet __instance)
        {
            // BumpBullet is the authoritative bullets_spawned source; always count.
            RpgHost.Client?.BumpBullet();
            if (__instance == null) return;
            // Highest-rate kind (~per pea). Emit only when something consumes it: an OnSpawn
            // grant or a debug session. MatchHost ignores it; the server metric is redundant;
            // the web strips it (v2 audit §4c.2).
            if (!Effects.EffectRuntime.HasOnSpawnGrant() && !DebugRuntime.SessionActive) return;
            // v2 record path (Task 9): compact record instead of dict when the host is live.
            if (Effects.EventDrainHost.Enabled && !DebugRuntime.SessionActive)
            {
                var fromTypeRec = 0;
                try { fromTypeRec = (int)__instance.fromType; } catch { }
                Effects.EventDrainHost.TryRecordBulletSpawn(__instance.Pointer, fromTypeRec);
                return;
            }
            var payload = new Dictionary<string, object> { ["ptr"] = GameDumps.Ptr(__instance) };
            try { payload["damage"] = __instance.Damage; } catch { }
            try
            {
                payload["plantType"] = (int)__instance.fromType;
                payload["plantTypeName"] = GameDumps.EnumName(__instance.fromType);
            }
            catch { }
            Emit("bullet.init", payload);
        }
    }

    static void NoteZombieDead(Zombie z, int reason)
    {
        if (z == null) return;
        var p = z.Pointer;
        Effects.InjectorEntityRegistry.Remove(p);
        Effects.InjectorBoardSnapshot.Invalidate();
        if (!DeadZombies.Add(p)) return;
        // v2 barrier: pending hit records for this zombie drain before its grants withdraw.
        try { Effects.EventDrainHost.FlushForPtr(p); } catch { }
        var diePayload = new Dictionary<string, object>
        {
            ["type"] = (int)z.theZombieType,
            ["typeName"] = GameDumps.EnumName(z.theZombieType),
            ["ptr"] = p.ToString("X"),
            ["reason"] = reason
        };
        DebugRuntime.Stamp(diePayload);
        // OnDeath must see entity grants; ForgetEntity withdraws after Emit.
        Emit("zombie.die", diePayload);
        ForgetEntity(p);
        try { DebugRuntime.OnZombieDying(z); } catch (Exception ex) { CheatState.Error("debug onkill: " + ex.Message); }
    }

    [HarmonyPatch(typeof(Bullet), nameof(Bullet.HitZombie))]
    public static class BulletHitZombie
    {
        public static void Prefix(Bullet __instance, Zombie zombie)
        {
            if (__instance == null || zombie == null) return;
            try
            {
                if (__instance.Pointer == IntPtr.Zero || zombie.Pointer == IntPtr.Zero) return;
            }
            catch { return; }
            if (!DebugRuntime.ShouldEmitHit()) return;
            try
            {
                var payload = new Dictionary<string, object>
                {
                    ["side"] = "zombie",
                    ["source"] = "hitZombie",
                    ["bulletPtr"] = GameDumps.Ptr(__instance),
                    ["bulletType"] = (int)__instance.theBulletType,
                    ["damage"] = __instance.Damage,
                    ["targetPtr"] = GameDumps.Ptr(zombie),
                    ["targetType"] = (int)zombie.theZombieType
                };
                try
                {
                    payload["fromType"] = (int)__instance.fromType;
                    payload["fromTypeName"] = GameDumps.EnumName(__instance.fromType);
                }
                catch { }
                DebugRuntime.Stamp(payload);
                Emit("combat.hit", payload);
                // Arms stay on TakeDamage only — avoid double-fire if this patch is enabled.
            }
            catch (Exception ex) { CheatState.Error("combat.hit zombie: " + ex.Message); }
        }
    }

    [HarmonyPatch(typeof(Bullet), nameof(Bullet.HitPlant))]
    public static class BulletHitPlant
    {
        public static void Prefix(Bullet __instance, Plant plant)
        {
            if (__instance == null || plant == null) return;
            try
            {
                if (__instance.Pointer == IntPtr.Zero || plant.Pointer == IntPtr.Zero) return;
            }
            catch { return; }
            if (!DebugRuntime.ShouldEmitHit()) return;
            try
            {
                var payload = new Dictionary<string, object>
                {
                    ["side"] = "plant",
                    ["source"] = "hitPlant",
                    ["bulletPtr"] = GameDumps.Ptr(__instance),
                    ["bulletType"] = (int)__instance.theBulletType,
                    ["damage"] = __instance.Damage,
                    ["targetPtr"] = GameDumps.Ptr(plant),
                    ["targetType"] = (int)plant.thePlantType
                };
                try
                {
                    payload["fromType"] = (int)__instance.fromType;
                    payload["fromTypeName"] = GameDumps.EnumName(__instance.fromType);
                }
                catch { }
                DebugRuntime.Stamp(payload);
                Emit("combat.hit", payload);
            }
            catch (Exception ex) { CheatState.Error("combat.hit plant: " + ex.Message); }
        }
    }

    [HarmonyPatch(typeof(Bullet), nameof(Bullet.HitLand))]
    public static class BulletHitLand
    {
        public static void Prefix(Bullet __instance)
        {
            if (__instance == null) return;
            try
            {
                if (__instance.Pointer == IntPtr.Zero) return;
            }
            catch { return; }
            if (!DebugRuntime.ShouldEmitHit()) return;
            try
            {
                var payload = new Dictionary<string, object>
                {
                    ["source"] = "hitLand",
                    ["bulletPtr"] = GameDumps.Ptr(__instance)
                };
                try { payload["bulletType"] = (int)__instance.theBulletType; } catch { }
                try { payload["row"] = __instance.theBulletRow; } catch { }
                try { payload["damage"] = __instance.Damage; } catch { }
                try { payload["fromType"] = (int)__instance.fromType; } catch { }
                DebugRuntime.Stamp(payload);
                Emit("combat.hitland", payload);
            }
            catch (Exception ex) { CheatState.Error("combat.hitland: " + ex.Message); }
        }
    }

    /// <summary>Delegates to EntityApply — sole Resolve→Writer path.</summary>
    internal static void ApplyPlant(Plant p, string source, bool includeAbsolute = true)
    {
        using var _perf = PerfProbe.Measure(PerfSection.EntityApply);
        EntityApply.RunPlant(p, source, includeAbsolute);
    }

    /// <summary>Delegates to EntityApply — sole Resolve→Writer path.</summary>
    internal static void ApplyZombie(Zombie z, string source, bool includeAbsolute = true)
    {
        using var _perf = PerfProbe.Measure(PerfSection.EntityApply);
        EntityApply.RunZombie(z, source, includeAbsolute);
    }

    /// <summary>
    /// Leave-board cleanup: withdraw <c>entity:{ptr}</c> grants (OnRemoved) then clear baselines.
    /// Call after die Emit so OnDeath still sees entity grants.
    /// </summary>
    public static void ForgetEntity(IntPtr ptr)
    {
        try { Effects.EffectRuntime.WithdrawEntity(ptr.ToString("X")); } catch { }
        Applied.Remove(ptr);
        EntityStatWriter.Forget(ptr);
        CheatState.Stats.ForgetEntity(ptr.ToString("X"));
    }

    internal static void RecapturePlant(Plant p, string source)
    {
        if (p == null) return;
        var dump = GameDumps.Plant(p, source, p.thePlantHealth, p.thePlantMaxHealth, p.attackDamage);
        dump["side"] = "plant";
        Emit("entity.stats", dump);
        if (CheatState.On("Z-REAPPLY-RC"))
        {
            Applied.Remove(p.Pointer);
            EntityApply.RunPlant(p, "recapture:" + source);
        }
    }

    internal static void RecaptureZombie(Zombie z, string source)
    {
        if (z == null) return;
        var dump = GameDumps.Zombie(z, source, Bridges.ZombieCombatFields.GetHp(z), Bridges.ZombieCombatFields.GetMaxHp(z), z.theAttackDamage, z.theFirstArmorHealth, z.theFirstArmorMaxHealth);
        dump["side"] = "zombie";
        Emit("entity.stats", dump);
        if (CheatState.On("Z-REAPPLY-RC"))
        {
            Applied.Remove(z.Pointer);
            EntityApply.RunZombie(z, "recapture:" + source);
        }
    }
}
