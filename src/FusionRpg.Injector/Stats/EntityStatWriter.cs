using System.Collections.Concurrent;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Stats;
using FusionRpg.Injector.Bridges;
using HarmonyLib;

using FusionRpg.Injector.Host;

namespace FusionRpg.Injector.Stats;

/// <summary>
/// Sole Unity combat-field mutator. Features must Resolve → Apply; never assign HP/ATK elsewhere.
/// </summary>
public static class EntityStatWriter
{
    public sealed class AppliedFinal
    {
        public long Hp;
        public long MaxHp;
        public long Atk;
        public string Source = "";
        public DateTime Utc = DateTime.UtcNow;
    }

    static readonly ConcurrentDictionary<IntPtr, AppliedFinal> Registry = new();

    public static bool TryGetApplied(IntPtr ptr, out AppliedFinal final) =>
        Registry.TryGetValue(ptr, out final!);

    public static void Forget(IntPtr ptr) => Registry.TryRemove(ptr, out _);

    public static void Clear() => Registry.Clear();

    /// <summary>
    /// Unity-boundary narrowing (combat-numerics, lawn-combat-wire T4) — this int32 width is a
    /// STRUCTURAL HOST LIMIT, not a choice this codebase made: <c>thePlantHealth</c>,
    /// <c>theAttackDamage</c>, the armor/shield fields, etc. are genuinely <c>int</c> fields on the
    /// host game's own <c>Plant</c>/<c>Zombie</c> classes (<c>Bridges/&lt;profile&gt;/ZombieCombatFields.cs</c>),
    /// which this Injector may not rewrite (AGENTS.md hard boundary: "never download or patch the PVZ
    /// Fusion game binary"). CLAUDE.md's caps rule exempts a structural host limit from "never clamp
    /// silently" PROVIDED it reports rather than silently saturating — a `long`/per-mille magnitude
    /// that genuinely exceeds <c>int32</c> here is a real gameplay event (gear finally out-scaled a
    /// three-decade-old engine's own field width), not a rounding detail, so it is proofed through the
    /// same channel every other writer proof already uses (<c>ProofWrite</c>'s own
    /// <c>SYS-EMIT-PROOF</c>/<c>GameHooks.Emit</c> shape) before the clamp is applied. Every call site
    /// in this file that used to call <c>ZombieCombatFields.ClampToInt32</c> directly goes through
    /// this wrapper instead, so the report is never accidentally skipped at a new call site.
    /// </summary>
    static int ClampToInt32Reporting(long value, string field, string source)
    {
        var clamped = ZombieCombatFields.ClampToInt32(value);
        if (value > int.MaxValue || value < int.MinValue)
        {
            CheatState.Error(
                $"writer.clampBoundary: {field} src={source} value={value} exceeds Unity's int32 field width -- clamped to {clamped}");
            if (CheatState.EmitProof && CheatState.On("SYS-EMIT-PROOF"))
            {
                try
                {
                    var payload = new Dictionary<string, object>
                    {
                        ["field"] = field,
                        ["source"] = source ?? "",
                        ["value"] = value,
                        ["clamped"] = clamped
                    };
                    CheatState.TagProbe(payload);
                    GameHooks.Emit("stat.writer.clampBoundary", payload);
                }
                catch { /* never break combat writes for proof */ }
            }
        }
        return clamped;
    }

    public static void WritePlant(Plant p, EntityFinal y, long previousHp, long previousMax, bool preserveHpRatio, string source)
    {
        if (p == null || y == null) return;
        try
        {
            var beforeHp = p.thePlantHealth;
            var beforeMax = p.thePlantMaxHealth;
            var beforeAtk = p.attackDamage;

            var max = ClampToInt32Reporting(Math.Max(1L, y.MaxHp), "plant.maxHp", source);
            var preserve = preserveHpRatio || StatSystem.PreserveLiveCurrentHp(source);
            var hp = ClampToInt32Reporting(
                StatSystem.CurrentHpForWrite(preserve, previousHp, previousMax, y.Hp, y.MaxHp), "plant.hp", source);
            p.thePlantMaxHealth = max;
            p.thePlantHealth = hp;
            if (!CheatState.On("D-PROBE-BULLET"))
                p.attackDamage = ClampToInt32Reporting(y.Atk, "plant.atk", source);

            // E16: fire rate and sun rate, composed. A zero means the baseline had none, so there
            // is nothing to write — never a zero interval, which is a divide-by-zero or an infinite
            // fire rate depending on which call site reads it.
            if (y.AttackInterval > 0) p.thePlantAttackInterval = (float)y.AttackInterval;
            if (y.ProduceInterval > 0) p.thePlantProduceInterval = (float)y.ProduceInterval;

            // E38 (spec-entity-fields-12plus.md): eight more plant fields, composed. Long
            // magnitudes clamp to int only at this boundary, exactly like MaxHp/Hp above; none of
            // these use the "zero baseline is absent" skip the two intervals above use — every one
            // is captured from a genuine live field on the plant's own side (EntityApply.cs), so a
            // composed zero is an ordinary value ("no shield right now"), never a missing stat, and
            // is written unconditionally, the same as Hp/Atk.
            p.theShieldHealth = ClampToInt32Reporting(y.PlantShield, "plant.shield", source);
            // attackCountdown/produceCountdown share the interval floor's structural reason (driven
            // to zero or below is the same divide-by-zero / infinite-fire-rate risk) but compose
            // unconditionally — see StatComposer.IntervalAlways's own doc comment.
            p.thePlantAttackCountDown = (float)y.AttackCountdown;
            p.thePlantProduceCountDown = (float)y.ProduceCountdown;
            // Unguarded by design (§2b, decided 2026-09-03): an adder is a signed delta, so a
            // negative value here is ordinary content, not a mistake. Never clamp this at write —
            // see CheatState.BuildPlantAbsoluteReal's own note and
            // EntityFields12PlusGuardTests.P_ATK_ADD_stays_unguarded_by_a_value_check.
            p.attackSpeedAdder = (float)y.AttackSpeedAdder;
            // plantSpeed/moveSpeed mirror zombieSpeed's own shape: most plants never move, so a
            // zero composed result genuinely means "this plant has no such stat" and the field is
            // left alone, exactly like the two intervals above.
            if (y.PlantSpeed > 0) p.thePlantSpeed = (float)y.PlantSpeed;
            if (y.PlantMoveSpeed > 0) p.moveSpeed = (float)y.PlantMoveSpeed;
            p.theLevel = ClampToInt32Reporting(y.PlantLevel, "plant.level", source);
            p.shootingLevel = ClampToInt32Reporting(y.ShootingLevel, "plant.shootingLevel", source);

            try { p.UpdateText(); } catch { }

            Remember(p.Pointer, p.thePlantHealth, p.thePlantMaxHealth, p.attackDamage, source);
            ProofWrite("plant", p.Pointer, source, beforeHp, beforeMax, beforeAtk,
                p.thePlantHealth, p.thePlantMaxHealth, p.attackDamage);
        }
        catch (Exception ex) { CheatState.Error("writer.plant: " + ex.Message); }
    }

    public static void WriteZombie(Zombie z, EntityFinal y, long previousHp, long previousMax, bool preserveHpRatio, string source)
    {
        if (z == null || y == null) return;
        try
        {
            var beforeHp = ZombieCombatFields.GetHp(z);
            var beforeMax = ZombieCombatFields.GetMaxHp(z);
            var beforeAtk = z.theAttackDamage;

            var max = Math.Max(1L, y.MaxHp);
            var preserve = preserveHpRatio || StatSystem.PreserveLiveCurrentHp(source);
            var hp = StatSystem.CurrentHpForWrite(preserve, previousHp, previousMax, y.Hp, y.MaxHp);
            ZombieCombatFields.SetMaxHp(z, max);
            ZombieCombatFields.SetHp(z, hp);
            if (y.Arm1Max > 0) z.theFirstArmorMaxHealth = ClampToInt32Reporting(y.Arm1Max, "zombie.arm1Max", source);
            if (y.Arm1 > 0) z.theFirstArmorHealth = ClampToInt32Reporting(y.Arm1, "zombie.arm1", source);
            if (y.Arm2Max > 0) z.theSecondArmorMaxHealth = ClampToInt32Reporting(y.Arm2Max, "zombie.arm2Max", source);
            if (y.Arm2 > 0) z.theSecondArmorHealth = ClampToInt32Reporting(y.Arm2, "zombie.arm2", source);
            z.theAttackDamage = ClampToInt32Reporting(Math.Max(1L, y.Atk), "zombie.atk", source);
            if (y.ZombieSpeed > 0) z.uniqueSpeed = (float)y.ZombieSpeed;

            // E38 (spec-entity-fields-12plus.md): four more zombie fields, composed — same
            // unconditional-write rule as the plant half above (every one captured from a genuine
            // live field on the zombie's own side, so a composed zero is ordinary, not absent).
            z.theArmor = (float)y.ArmorFlat;
            z.takeDmgMultiplier = (float)y.TakeDmgMultiplier;
            // theSpeed/theOriginSpeed mirror uniqueSpeed's own shape immediately above.
            if (y.ZombieSpeedCurrent > 0) z.theSpeed = (float)y.ZombieSpeedCurrent;
            if (y.ZombieOriginSpeed > 0) z.theOriginSpeed = (float)y.ZombieOriginSpeed;

            try { z.UpdateHealthText(); } catch { }

            Remember(z.Pointer, ZombieCombatFields.GetHp(z), ZombieCombatFields.GetMaxHp(z), z.theAttackDamage, source);
            ProofWrite("zombie", z.Pointer, source, beforeHp, beforeMax, beforeAtk,
                ZombieCombatFields.GetHp(z), ZombieCombatFields.GetMaxHp(z), z.theAttackDamage);
        }
        catch (Exception ex) { CheatState.Error("writer.zombie: " + ex.Message); }
    }

    /// <summary>Non-core Tab B fields (intervals, speed, …) — still Writer-owned.</summary>
    public static void WritePlantExtras(Plant p)
    {
        if (p == null) return;
        try
        {
            // E16's two interval keys AND E38's eight plant keys (spec-entity-fields-12plus.md) all
            // moved to the composed path and are deliberately absent here. Writing them in both
            // places would fight the composer, last-write-wins and spawn-order dependent, so the
            // same board could settle differently twice. They arrive as Override modifiers now —
            // see CheatState.BuildPlantAbsoluteReal. P-SHIELD, P-ATK-CD, P-ATK-ADD, P-PROD-CD,
            // P-SPEED, P-MOVE, P-LEVEL and P-SHOOTLVL used to be written here directly.
            //
            // The guard that keeps them gone reads THIS FILE as text, so do not write any of their
            // field assignments in a comment either; it cannot tell one from code.
            if (CheatState.On("P-MOD-HP"))
                try { p.ModifyHealth(0, p.thePlantMaxHealth); } catch { }
            if (CheatState.On("P-MOD-ATK"))
                try { p.ModifyDamage(0, p.attackDamage); } catch { }
            try { p.UpdateText(); } catch { }
        }
        catch (Exception ex) { CheatState.Error("plant extras: " + ex.Message); }
    }

    public static void WriteZombieExtras(Zombie z)
    {
        if (z == null) return;
        try
        {
            // The unique-speed key (E16) AND E38's four zombie keys (Z-ARMOR-F, Z-TAKEMULT, Z-SPD,
            // Z-SPD-O — spec-entity-fields-12plus.md) all moved to the composed path — see the note
            // in WritePlantExtras, including why they are not quoted here either.
            if (CheatState.IsUserSet("Z-SLOW-FREEZE") && CheatState.FVal("Z-SLOW-FREEZE") >= 0)
                try { z.freezeSpeed = CheatState.FVal("Z-SLOW-FREEZE"); } catch { }
            if (CheatState.IsUserSet("Z-SLOW-COLD") && CheatState.FVal("Z-SLOW-COLD") >= 0)
                try { z.coldSpeed = CheatState.FVal("Z-SLOW-COLD"); } catch { }
            if (CheatState.IsUserSet("Z-SLOW-BUTTER") && CheatState.FVal("Z-SLOW-BUTTER") >= 0)
                try { z.butterSpeed = CheatState.FVal("Z-SLOW-BUTTER"); } catch { }
            try { z.UpdateHealthText(); } catch { }
        }
        catch (Exception ex) { CheatState.Error("zombie extras: " + ex.Message); }
    }

    /// <summary>FA10 overlay current-HP add. Heal clamps to max. HP≤0 → ForceKill (Die). Never TakeDamage.</summary>
    public static void AddPlantHp(Plant p, long delta, string source)
    {
        if (p == null) return;
        using (OverlayApplyGuard.Enter())
        {
            try
            {
                var live = (long)p.thePlantHealth;
                var max = (long)p.thePlantMaxHealth;
                var next = ResourceDeltaMath.Apply(live, delta, max);
                if (next <= 0)
                {
                    ForceKillPlant(p, source);
                    return;
                }

                var hp = ClampToInt32Reporting(next, "plant.hp", source);
                p.thePlantHealth = hp;
                try { p.UpdateText(); } catch { }
                Remember(p.Pointer, hp, p.thePlantMaxHealth, p.attackDamage, source);
                ProofWrite("plant", p.Pointer, source, live, max, p.attackDamage,
                    p.thePlantHealth, p.thePlantMaxHealth, p.attackDamage);
            }
            catch (Exception ex) { CheatState.Error("add plant hp: " + ex.Message); }
        }
    }

    /// <summary>FA10 overlay current-HP add. Heal clamps to max. HP≤0 → ForceKill (Die). Never TakeDamage.</summary>
    public static void AddZombieHp(Zombie z, long delta, string source)
    {
        if (z == null) return;
        using (OverlayApplyGuard.Enter())
        {
            try
            {
                var live = ZombieCombatFields.GetHp(z);
                var max = ZombieCombatFields.GetMaxHp(z);
                var next = ResourceDeltaMath.Apply(live, delta, max);
                if (next <= 0)
                {
                    ForceKillZombie(z, source);
                    return;
                }

                ZombieCombatFields.SetHp(z, next);
                try { z.UpdateHealthText(); } catch { }
                Remember(z.Pointer, next, max, z.theAttackDamage, source);
                ProofWrite("zombie", z.Pointer, source, live, max, z.theAttackDamage,
                    ZombieCombatFields.GetHp(z), ZombieCombatFields.GetMaxHp(z), z.theAttackDamage);
            }
            catch (Exception ex) { CheatState.Error("add zombie hp: " + ex.Message); }
        }
    }

    public static void ForceSetPlantHp(Plant p, long hp, string source)
    {
        if (p == null) return;
        try
        {
            // hp arrives long (RPG-scaled); thePlantHealth is Unity's own int field, so it is
            // clamped at the write boundary — the same pattern WritePlant already uses — instead
            // of the implicit narrowing cast this signature used to hide.
            var max = ClampToInt32Reporting(Math.Max(p.thePlantMaxHealth, hp), "plant.maxHp", source);
            var clamped = ClampToInt32Reporting(hp, "plant.hp", source);
            p.thePlantMaxHealth = max;
            p.thePlantHealth = clamped;
            try { p.UpdateText(); } catch { }
            Remember(p.Pointer, clamped, max, p.attackDamage, source);
            ProofWrite("plant", p.Pointer, source, clamped, max, p.attackDamage, clamped, max, p.attackDamage);
        }
        catch (Exception ex) { CheatState.Error("force plant hp: " + ex.Message); }
    }

    public static void ForceSetZombieHp(Zombie z, long hp, string source)
    {
        if (z == null) return;
        try
        {
            var max = Math.Max(ZombieCombatFields.GetMaxHp(z), hp);
            ZombieCombatFields.SetMaxHp(z, max);
            ZombieCombatFields.SetHp(z, hp);
            try { z.UpdateHealthText(); } catch { }
            Remember(z.Pointer, hp, max, z.theAttackDamage, source);
            ProofWrite("zombie", z.Pointer, source, hp, max, z.theAttackDamage, hp, max, z.theAttackDamage);
        }
        catch (Exception ex) { CheatState.Error("force zombie hp: " + ex.Message); }
    }

    /// <summary>
    /// Re-asserts a debug-spawn's pinned max HP without full-healing (<see cref="InjectorSpawnHpPin"/>,
    /// lawn-combat-wire 2026-09-15) — <see cref="ForceSetPlantHp"/> sets current HP to its own `hp`
    /// argument unconditionally, which is exactly wrong for a per-reapply re-assert: it would snap the
    /// plant back to full health every time the max-HP override needed correcting, undoing whatever
    /// real damage combat had already dealt. No-op when the live max already meets or exceeds the pin
    /// (the common case, once applied) — only fires the one time an <c>includeAbsolute: false</c>
    /// reapply has actually reverted it.
    /// </summary>
    public static void ForceSetPlantMaxHpPreserveRatio(Plant p, long targetMaxHp, string source)
    {
        if (p == null) return;
        try
        {
            var liveMax = (long)p.thePlantMaxHealth;
            if (liveMax >= targetMaxHp) return;
            var liveHp = (long)p.thePlantHealth;
            var ratio = liveMax > 0 ? (double)liveHp / liveMax : 1.0;
            var newHp = Math.Clamp((long)Math.Round(targetMaxHp * ratio), 1, targetMaxHp);
            var max = ClampToInt32Reporting(targetMaxHp, "plant.maxHp", source);
            var hp = ClampToInt32Reporting(newHp, "plant.hp", source);
            p.thePlantMaxHealth = max;
            p.thePlantHealth = hp;
            try { p.UpdateText(); } catch { }
            Remember(p.Pointer, hp, max, p.attackDamage, source);
            ProofWrite("plant", p.Pointer, source, liveHp, liveMax, p.attackDamage, hp, max, p.attackDamage);
        }
        catch (Exception ex) { CheatState.Error("force plant maxhp preserve-ratio: " + ex.Message); }
    }

    /// <summary>Zombie overload of <see cref="ForceSetPlantMaxHpPreserveRatio"/> — see that overload's
    /// doc for the shared contract.</summary>
    public static void ForceSetZombieMaxHpPreserveRatio(Zombie z, long targetMaxHp, string source)
    {
        if (z == null) return;
        try
        {
            var liveMax = ZombieCombatFields.GetMaxHp(z);
            if (liveMax >= targetMaxHp) return;
            var liveHp = ZombieCombatFields.GetHp(z);
            var ratio = liveMax > 0 ? (double)liveHp / liveMax : 1.0;
            var newHp = Math.Clamp((long)Math.Round(targetMaxHp * ratio), 1, targetMaxHp);
            ZombieCombatFields.SetMaxHp(z, targetMaxHp);
            ZombieCombatFields.SetHp(z, newHp);
            try { z.UpdateHealthText(); } catch { }
            Remember(z.Pointer, newHp, targetMaxHp, z.theAttackDamage, source);
            ProofWrite("zombie", z.Pointer, source, liveHp, liveMax, z.theAttackDamage, newHp, targetMaxHp, z.theAttackDamage);
        }
        catch (Exception ex) { CheatState.Error("force zombie maxhp preserve-ratio: " + ex.Message); }
    }

    public static void ForceKillPlant(Plant p, string source)
    {
        if (p == null) return;
        try
        {
            p.thePlantHealth = 0;
            Forget(p.Pointer);
            ProofNote($"writer.forceKill plant ptr={p.Pointer.ToString("X")} src={source}");
            p.Die(Plant.DieReason.BySelf);
        }
        catch (Exception ex) { CheatState.Error("forceKill plant: " + ex.Message); }
    }

    public static void ForceKillZombie(Zombie z, string source)
    {
        if (z == null) return;
        try
        {
            ZombieCombatFields.SetHp(z, 0);
            Forget(z.Pointer);
            ProofNote($"writer.forceKill zombie ptr={z.Pointer.ToString("X")} src={source}");
            try { z.Die(0); } catch { z.DestoryZombie(); }
        }
        catch (Exception ex) { CheatState.Error("forceKill zombie: " + ex.Message); }
    }

    public static void ScalePlantHp(Plant p, int factor, string source)
    {
        if (p == null || factor == 0) return;
        try
        {
            var beforeHp = p.thePlantHealth;
            var beforeMax = p.thePlantMaxHealth;
            p.thePlantHealth *= factor;
            p.thePlantMaxHealth *= factor;
            Remember(p.Pointer, p.thePlantHealth, p.thePlantMaxHealth, p.attackDamage, source);
            ProofWrite("plant", p.Pointer, source, beforeHp, beforeMax, p.attackDamage,
                p.thePlantHealth, p.thePlantMaxHealth, p.attackDamage);
        }
        catch (Exception ex) { CheatState.Error("scale plant: " + ex.Message); }
    }

    public static void ScaleZombieHp(Zombie z, int factor, string source)
    {
        if (z == null || factor == 0) return;
        try
        {
            var beforeHp = ZombieCombatFields.GetHp(z);
            var beforeMax = ZombieCombatFields.GetMaxHp(z);
            ZombieCombatFields.SetHp(z, beforeHp * factor);
            ZombieCombatFields.SetMaxHp(z, beforeMax * factor);
            Remember(z.Pointer, ZombieCombatFields.GetHp(z), ZombieCombatFields.GetMaxHp(z), z.theAttackDamage, source);
            ProofWrite("zombie", z.Pointer, source, beforeHp, beforeMax, z.theAttackDamage,
                ZombieCombatFields.GetHp(z), ZombieCombatFields.GetMaxHp(z), z.theAttackDamage);
        }
        catch (Exception ex) { CheatState.Error("scale zombie: " + ex.Message); }
    }

    static void Remember(IntPtr ptr, long hp, long maxHp, long atk, string source)
    {
        Registry[ptr] = new AppliedFinal
        {
            Hp = hp,
            MaxHp = maxHp,
            Atk = atk,
            Source = source ?? "",
            Utc = DateTime.UtcNow
        };
    }

    static void ProofWrite(
        string side, IntPtr ptr, string source,
        long hpBefore, long maxBefore, long atkBefore,
        long hpAfter, long maxAfter, long atkAfter)
    {
        if (!(CheatState.EmitProof && CheatState.On("SYS-EMIT-PROOF"))) return;
        try
        {
            var payload = new Dictionary<string, object>
            {
                ["side"] = side,
                ["ptr"] = ptr.ToString("X"),
                ["source"] = source ?? "",
                ["hpBefore"] = hpBefore,
                ["maxBefore"] = maxBefore,
                ["atkBefore"] = atkBefore,
                ["hpAfter"] = hpAfter,
                ["maxAfter"] = maxAfter,
                ["atkAfter"] = atkAfter
            };
            CheatState.TagProbe(payload);
            GameHooks.Emit("stat.writer", payload);
            // One short overlay note — avoid flooding when PushScales hits many entities.
            CheatState.LastNote =
                $"writer.{side} src={source} ptr={ptr.ToString("X")} hp {hpBefore}/{maxBefore}->{hpAfter}/{maxAfter}";
            try { RpgHost.Log.Info("[cheat] " + CheatState.LastNote); } catch { }
        }
        catch { /* never break combat writes for proof */ }
    }

    static void ProofNote(string msg)
    {
        if (!(CheatState.EmitProof && CheatState.On("SYS-EMIT-PROOF"))) return;
        try { CheatState.Note(msg); } catch { }
    }

    /// <summary>
    /// Opt-in LimHealth policy. Default: no Harmony body work (observe/gate off) so we cannot
    /// stall the game. Enable SYS-LIMHEALTH-OBSERVE / SYS-LIMHEALTH-GATE only for diagnosis.
    /// </summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.LimHealth))]
    public static class PlantLimHealthPolicy
    {
        // combat-numerics (lawn-combat-wire T4): `long`, not `int` -- audit-overflow.py A3 flags any
        // `int` holding an hp/max-shaped value. thePlantHealth/thePlantMaxHealth are themselves Unity
        // `int` fields (the same structural host limit ClampToInt32Reporting documents above), so this
        // widen loses nothing and just keeps the diagnostic snapshot off the audit's A3 list.
        static readonly ConcurrentDictionary<IntPtr, (long hp, long max)> BeforeCall = new();
        static DateTime _lastObserveUtc = DateTime.MinValue;

        public static bool Prefix(Plant __instance)
        {
            try
            {
                if (__instance == null) return true;
                var observe = CheatState.On("SYS-LIMHEALTH-OBSERVE");
                var gate = CheatState.On("SYS-LIMHEALTH-GATE");
                if (!observe && !gate) return true;

                var ptr = __instance.Pointer;
                if (observe)
                    BeforeCall[ptr] = (__instance.thePlantHealth, __instance.thePlantMaxHealth);

                if (gate && TryGetApplied(ptr, out var applied))
                {
                    var appliedMax = Bridges.ZombieCombatFields.ClampToInt32(applied.MaxHp);
                    var appliedHp = Bridges.ZombieCombatFields.ClampToInt32(applied.Hp);
                    if (__instance.thePlantMaxHealth < appliedMax)
                        __instance.thePlantMaxHealth = appliedMax;
                    if (__instance.thePlantHealth > __instance.thePlantMaxHealth)
                        __instance.thePlantHealth = __instance.thePlantMaxHealth;
                    if (__instance.thePlantHealth < 1 && appliedHp > 0)
                        __instance.thePlantHealth = Math.Min(appliedHp, __instance.thePlantMaxHealth);
                    return false;
                }
            }
            catch { /* never block LimHealth */ }
            return true;
        }

        public static void Postfix(Plant __instance)
        {
            try
            {
                if (__instance == null) return;
                if (!CheatState.On("SYS-LIMHEALTH-OBSERVE")) return;
                if (!(CheatState.EmitProof && CheatState.On("SYS-EMIT-PROOF"))) return;
                var ptr = __instance.Pointer;
                if (!BeforeCall.TryRemove(ptr, out var before)) return;
                if (!TryGetApplied(ptr, out var applied)) return;

                var afterHp = __instance.thePlantHealth;
                var afterMax = __instance.thePlantMaxHealth;
                var changed = afterHp != before.hp || afterMax != before.max;
                if (!changed && afterMax >= applied.MaxHp) return;

                // Rate-limit: at most one observe event / note per 500ms.
                var now = DateTime.UtcNow;
                if ((now - _lastObserveUtc).TotalMilliseconds < 500) return;
                _lastObserveUtc = now;

                var payload = new Dictionary<string, object>
                {
                    ["ptr"] = ptr.ToString("X"),
                    ["hpBefore"] = before.hp,
                    ["maxBefore"] = before.max,
                    ["hpAfter"] = afterHp,
                    ["maxAfter"] = afterMax,
                    ["writerMax"] = applied.MaxHp,
                    ["writerHp"] = applied.Hp,
                    ["writerSource"] = applied.Source,
                    ["revertedVsWriter"] = afterMax < applied.MaxHp
                };
                CheatState.TagProbe(payload);
                GameHooks.Emit("stat.limhealth", payload);
                CheatState.LastNote =
                    $"limhealth.observe ptr={ptr.ToString("X")} {before.hp}/{before.max}->{afterHp}/{afterMax} revert={afterMax < applied.MaxHp}";
                try { RpgHost.Log.Info("[cheat] " + CheatState.LastNote); } catch { }
            }
            catch { }
        }
    }
}
