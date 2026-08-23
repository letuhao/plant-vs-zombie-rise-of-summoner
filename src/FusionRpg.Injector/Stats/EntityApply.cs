using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Injector.Bridges;
using FusionRpg.Injector.Stats;

using FusionRpg.Injector.Host;

namespace FusionRpg.Injector;

/// <summary>
/// Single entry for Resolve → EntityStatWriter. Spawn, PushScales, reapply, and Tab B Apply all call Run*.
/// </summary>
public static class EntityApply
{
    public static void RunPlant(Plant p, string source, bool includeAbsolute = true)
    {
        if (p == null) return;
        try
        {
            var s = CheatState.EffectiveStats();
            var ptr = p.Pointer;
            var key = ptr.ToString("X");
            // Living membership SSOT = MatchRuntime BoardProjection via Emit Apply (W2-D).
            // Do not steal selection during bulk PushScales / reapply.
            if (!source.Contains("pushScales", StringComparison.Ordinal)
                && !source.Contains("reapply", StringComparison.Ordinal)
                && !source.Contains("absolute", StringComparison.Ordinal))
                CheatState.Select(ptr, "plant");

            try
            {
                SpawnCatalog.Note("plant", (int)p.thePlantType, GameDumps.EnumName(p.thePlantType), "spawn:" + source,
                    GameDumps.PlantName(p.thePlantType));
            }
            catch { /* catalog must not block apply */ }

            var baseline = CheatState.Stats.CaptureOrGet(key, () => new EntityBaseline
            {
                Hp = p.thePlantHealth,
                MaxHp = p.thePlantMaxHealth,
                Atk = p.attackDamage,
                // E16: the intervals become real composed channels, so their game values have to be
                // captured as a baseline like every other channel. Captured ONCE, on first sight —
                // capturing later would bake an already-modified value in as if it were the original.
                AttackInterval = p.thePlantAttackInterval,
                ProduceInterval = p.thePlantProduceInterval
            });

            if (!GameHooks.Applied.Add(ptr)) return;

            var prevHp = p.thePlantHealth;
            var prevMax = p.thePlantMaxHealth;
            var preserveRatio = StatSystem.PreserveLiveCurrentHp(source);
            var abs = includeAbsolute ? CheatState.BuildPlantAbsolute() : null;
            var applyScales = s.ApplyStats && !CheatState.On("D-PROBE-BULLET");
            var hasAbsolute = abs != null && abs.Count > 0;
            var hasScaleMods = applyScales && CheatState.HasPlantScaleMods();
            var hasPvz = CheatState.HasPvzStatsMods();
            var hasEffectMods = CheatState.Stats.HasSessionModsBySourceKind("effect");
            var hasExtras = includeAbsolute && CheatState.HasPlantExtrasSet();
            // Empty bag = no write on spawn; pushScales/reapply writes baseline after clear.
            var forceReapply = source.Contains("pushScales", StringComparison.Ordinal)
                               || source.Contains("reapply", StringComparison.Ordinal);
            var shouldWrite = PvzStatsApplyGate.ShouldWrite(hasScaleMods, hasAbsolute, hasPvz)
                              || hasEffectMods
                              || forceReapply;

            var ctx = CheatState.Stats.Contexts.ForPlant(
                key, baseline, (int)p.thePlantType, GameHooks.MatchKey,
                playerId: CheatState.PvzStatsPlayerId > 0 ? CheatState.PvzStatsPlayerId : null,
                cheatScale: s, cheatAbsolute: abs,
                applyStats: PvzStatsApplyGate.ShouldComposeScales(hasScaleMods, hasPvz, hasEffectMods),
                cheatAbsoluteReal: includeAbsolute ? CheatState.BuildPlantAbsoluteReal() : null,
                pvzStatsMods: CheatState.PvzStatsMods);
            var final = CheatState.ActorHub.Resolve(ctx).AppliedCombat;

            if (shouldWrite)
                EntityStatWriter.WritePlant(p, final, prevHp, prevMax, preserveRatio, source);

            if (hasExtras)
                EntityStatWriter.WritePlantExtras(p);

            if (shouldWrite && RpgHost.Client != null)
            {
                try
                {
                    GameHooks.Emit("stat.applied", Tag(new Dictionary<string, object>
                    {
                        ["side"] = "plant",
                        ["type"] = (int)p.thePlantType,
                        ["typeName"] = GameDumps.EnumName(p.thePlantType),
                        ["ptr"] = key,
                        ["source"] = source,
                        ["includeAbsolute"] = includeAbsolute,
                        ["hpBefore"] = baseline.Hp,
                        ["hpAfter"] = p.thePlantHealth,
                        ["maxAfter"] = p.thePlantMaxHealth,
                        ["attackBefore"] = baseline.Atk,
                        ["attackAfter"] = p.attackDamage,
                        ["hpPercent"] = s.Plants.HpPercent,
                        ["hpFlat"] = s.Plants.HpFlat
                    }));
                }
                catch { }
            }

            if (RpgHost.Client != null
                && !source.Contains("pushScales", StringComparison.Ordinal)
                && !source.Contains("absolute", StringComparison.Ordinal))
            {
                try
                {
                    GameHooks.Emit("plant.spawn",
                        Tag(GameDumps.Plant(p, CheatState.ConsumeSpawnSourceTag(ptr) ?? source, baseline.Hp, baseline.MaxHp, baseline.Atk)));
                }
                catch { }
            }
        }
        catch (Exception ex) { CheatState.Error("EntityApply.plant: " + ex.Message); }
    }

    public static void RunZombie(Zombie z, string source, bool includeAbsolute = true)
    {
        if (z == null) return;
        try
        {
            var s = CheatState.EffectiveStats();
            var ptr = z.Pointer;
            var key = ptr.ToString("X");
            // Living membership SSOT = MatchRuntime BoardProjection via Emit Apply (W2-D).
            if (!source.Contains("pushScales", StringComparison.Ordinal)
                && !source.Contains("reapply", StringComparison.Ordinal)
                && !source.Contains("absolute", StringComparison.Ordinal))
                CheatState.Select(ptr, "zombie");

            try
            {
                SpawnCatalog.Note("zombie", (int)z.theZombieType, GameDumps.EnumName(z.theZombieType), "spawn:" + source,
                    GameDumps.ZombieName(z.theZombieType));
            }
            catch { }

            var baseline = CheatState.Stats.CaptureOrGet(key, () => new EntityBaseline
            {
                Hp = ZombieCombatFields.GetHp(z),
                MaxHp = ZombieCombatFields.GetMaxHp(z),
                Atk = z.theAttackDamage,
                Arm1 = z.theFirstArmorHealth,
                Arm1Max = z.theFirstArmorMaxHealth,
                Arm2 = z.theSecondArmorHealth,
                Arm2Max = z.theSecondArmorMaxHealth,
                ZombieSpeed = z.uniqueSpeed
            });

            if (!GameHooks.Applied.Add(ptr))
            {
                if (CheatState.On("Z-REAPPLY-RC") &&
                    (source.Contains("reinforce", StringComparison.Ordinal)
                     || source.Contains("setZombie", StringComparison.Ordinal)
                     || source.Contains("setHealth", StringComparison.Ordinal)))
                {
                    GameHooks.Applied.Remove(ptr);
                    if (!GameHooks.Applied.Add(ptr)) return;
                }
                else return;
            }

            var prevHp = ZombieCombatFields.GetHp(z);
            var prevMax = ZombieCombatFields.GetMaxHp(z);
            var preserveRatio = StatSystem.PreserveLiveCurrentHp(source);
            var abs = includeAbsolute ? CheatState.BuildZombieAbsolute() : null;
            var applyScales = s.ApplyStats;
            var hasAbsolute = abs != null && abs.Count > 0;
            var hasScaleMods = applyScales && CheatState.HasZombieScaleMods();
            var hasPvz = CheatState.HasPvzStatsMods();
            var hasEffectMods = CheatState.Stats.HasSessionModsBySourceKind("effect");
            var hasExtras = includeAbsolute && CheatState.HasZombieExtrasSet();
            var forceReapply = source.Contains("pushScales", StringComparison.Ordinal)
                               || source.Contains("reapply", StringComparison.Ordinal);
            var shouldWrite = PvzStatsApplyGate.ShouldWrite(hasScaleMods, hasAbsolute, hasPvz)
                              || hasEffectMods
                              || forceReapply;

            var ctx = CheatState.Stats.Contexts.ForZombie(
                key, baseline, (int)z.theZombieType, GameHooks.MatchKey,
                playerId: CheatState.PvzStatsPlayerId > 0 ? CheatState.PvzStatsPlayerId : null,
                cheatScale: s, cheatAbsolute: abs,
                applyStats: PvzStatsApplyGate.ShouldComposeScales(hasScaleMods, hasPvz, hasEffectMods),
                cheatAbsoluteReal: includeAbsolute ? CheatState.BuildZombieAbsoluteReal() : null,
                pvzStatsMods: CheatState.PvzStatsMods);
            var final = CheatState.ActorHub.Resolve(ctx).AppliedCombat;

            if (shouldWrite)
                EntityStatWriter.WriteZombie(z, final, prevHp, prevMax, preserveRatio, source);

            if (hasExtras)
                EntityStatWriter.WriteZombieExtras(z);

            if (shouldWrite && RpgHost.Client != null)
            {
                try
                {
                    GameHooks.Emit("stat.applied", Tag(new Dictionary<string, object>
                    {
                        ["side"] = "zombie",
                        ["type"] = (int)z.theZombieType,
                        ["typeName"] = GameDumps.EnumName(z.theZombieType),
                        ["ptr"] = key,
                        ["source"] = source,
                        ["includeAbsolute"] = includeAbsolute,
                        ["hpBefore"] = baseline.Hp,
                        ["hpAfter"] = ZombieCombatFields.GetHp(z),
                        ["attackBefore"] = baseline.Atk,
                        ["attackAfter"] = z.theAttackDamage
                    }));
                }
                catch { }
            }

            if (RpgHost.Client != null
                && !source.Contains("pushScales", StringComparison.Ordinal)
                && !source.Contains("absolute", StringComparison.Ordinal))
            {
                try
                {
                    GameHooks.Emit("zombie.spawn",
                        Tag(GameDumps.Zombie(z, CheatState.ConsumeSpawnSourceTag(ptr) ?? source, baseline.Hp, baseline.MaxHp, baseline.Atk, baseline.Arm1,
                            baseline.Arm1Max)));
                }
                catch { }
            }
        }
        catch (Exception ex) { CheatState.Error("EntityApply.zombie: " + ex.Message); }
    }

    static Dictionary<string, object> Tag(Dictionary<string, object> payload)
    {
        CheatState.TagProbe(payload);
        return payload;
    }
}
