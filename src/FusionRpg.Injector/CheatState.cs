using System.Collections.Concurrent;
using System.Text.Json;
using FusionRpg.CheatCore;
using FusionRpg.Contracts;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Derived;

using FusionRpg.Injector.Host;
using FusionRpg.Injector.Lawn;
using FusionRpg.Injector.Stats;

namespace FusionRpg.Injector;

/// <summary>Session cheat registry keyed by coverage ids from cheat-menu-coverage.md.</summary>
public static class CheatState
{
    static readonly object Gate = new();
    static readonly Dictionary<string, CheatEntry> Entries = new(StringComparer.Ordinal);
    static string? _persistPath;
    public static bool MenuOpen;
    /// <summary>Always false — in-game F8 menu retired; web FE is SSOT.</summary>
    public static bool MenuEnabled;
    public static bool EmitProof = true;
    public static bool PersistEnabled;
    public static bool LocalStatsOverride;
    public static bool BoardConfigLocked;
    public static StatsConfig LocalStats { get; } = new();
    /// <summary>Shared StatSystem — plugins compose Y0 + Xi → Y. Cheats feed cheat.scale / cheat.absolute only.</summary>
    public static StatSystem Stats { get; } = StatSystemBootstrap.CreateDefault();
    public static InjectorProgressionPowerProvider ProgressionPower { get; } = new();
    /// <summary>Derived snapshot compose — wraps Stats; Writer uses AppliedCombat.</summary>
    public static ActorHub ActorHub { get; } = ActorHubBootstrap.CreateDefault(Stats, ProgressionPower);
    public static IntPtr SelectedPtr;
    public static string SelectedSide = "";
    static int _spawnCol = 3;
    static int _spawnRow = 2;
    public static int SpawnCol
    {
        get => _spawnCol;
        set => _spawnCol = LawnCoords.ClampCol(value);
    }
    public static int SpawnRow
    {
        get => _spawnRow;
        set => _spawnRow = LawnCoords.ClampRow(value);
    }
    public static int ManualTypeId;
    public static string ManualSide = "plant";
    public static int LastAlmanacType = -1;
    public static string LastAlmanacSide = "";
    public static float TimeScale = 1f;
    public static string LastError = "";
    public static string LastNote = "";
    public static int TabIndex;
    public static int SelectedCatalogIndex;

    /// <summary>Active live probe session (web pack or F8). Cleared on end or timeout.</summary>
    public static string? ActiveProbeId;
    public static string? ActivePackId;
    public static string? ActiveCorrelationId;
    public static DateTime ActiveProbeUtc;

    /// <summary>Loaded PvzStats modifiers for current player (injector hydrate; DB-free).</summary>
    public static List<StatModifier> PvzStatsMods { get; private set; } = new();
    public static long PvzStatsRevision { get; private set; }
    public static long AppliedPvzStatsRevision { get; private set; } = -1;
    public static long PvzStatsPlayerId { get; private set; }

    /// <summary>One-shot dump source override for next plant/zombie spawn emit (PvzIntent).</summary>
    public static string? PendingSpawnSourceTag;
    static readonly ConcurrentDictionary<IntPtr, string> SpawnSourceByPtr = new();

    const double ProbeTimeoutMinutes = 10;

    public static void Init(string pluginDir)
    {
        _persistPath = Path.Combine(pluginDir, "cheat-state.json");
        EnsureDefaults();
        if (PersistEnabled) TryLoad();
    }

    public static void EnsureDefaults()
    {
        lock (Gate)
        {
            void T(string id, bool v = false) => Put(id, new CheatEntry { Id = id, Kind = "toggle", Enabled = v, IsSet = false });
            void F(string id, double v) => Put(id, new CheatEntry { Id = id, Kind = "slider", FloatValue = v, Enabled = true, IsSet = false });
            void N(string id, double v) => Put(id, new CheatEntry { Id = id, Kind = "number", FloatValue = v, Enabled = true, IsSet = false });

            T("A-APPLY", true);
            F("A-P-HP%", 1f); N("A-P-HP+", 0);
            F("A-P-ATK%", 1f); N("A-P-ATK+", 0);
            F("A-P-DEF%", 1f); N("A-P-DEF+", 0);
            F("A-Z-HP%", 1f); N("A-Z-HP+", 0);
            F("A-Z-ATK%", 1f); N("A-Z-ATK+", 0);
            F("A-Z-DEF%", 1f); N("A-Z-DEF+", 0);

            foreach (var id in new[]
                     {
                         "P-GOD", "P-GOD-DIE", "P-DEF-REAL", "P-MOD-HP", "P-MOD-ATK",
                         "Z-GOD", "Z-DEF-BODY", "Z-DEF-APPLY", "Z-REAPPLY-RC",
                         "D-PROBE-PLANT", "D-PROBE-BULLET", "D-HOMING",
                         "F-WAVE-FREEZE", "G-TIMEFREEZE", "G-AUTOCOLLECT", "G-FREE-SET",
                         "H-ANYWHERE", "H-NOCD-CARD", "H-NOCD-GLOVE", "H-NOCD-HAMMER", "H-NOCD-WHEEL", "H-MOWER-INF",
                         "SYS-EMIT-PROOF", "SYS-DAMAGE-FX", "SYS-ELEMENT-FX",
                         "SYS-LIMHEALTH-GATE", "SYS-LIMHEALTH-OBSERVE",
                         "OVERLAY-COMBAT", "DEBUG-LEVEL-ENTRY"
                     })
                T(id);

            F("D-DMG-%", 1f); N("D-DMG-SET", -1);
            N("D-TYPE-SWAP", -1);
            F("G-TIMESCALE", 1f);

            foreach (var id in new[] { "E-ZH", "E-ZD", "E-ZS", "E-ZC" })
                F(id, 1f);
            N("E-ZARM", 0);
            F("E-PMIN", 0.2f); F("E-PMAX", 6f);
            F("E-ZMIN", 0.1f); F("E-ZMAX", 10f);
            N("E-WAVE-I", 30); N("E-CONV-I", 6);

            N("P-HP", -1); N("P-MAXHP", -1); N("P-SHIELD", -1); N("P-ATK", -1);
            N("P-ATK-INT", -1); N("P-ATK-CD", -1); N("P-ATK-ADD", -1);
            N("P-PROD-INT", -1); N("P-PROD-CD", -1); N("P-SPEED", -1); N("P-MOVE", -1);
            N("P-LEVEL", -1); N("P-SHOOTLVL", -1); N("P-LIMDMG", -1);

            N("Z-HP", -1); N("Z-MAXHP", -1); N("Z-ARM1", -1); N("Z-ARM1MAX", -1);
            N("Z-ARM2", -1); N("Z-ARM2MAX", -1); N("Z-ATK", -1); N("Z-ARMOR-F", -1);
            N("Z-TAKEMULT", -1); N("Z-SPD-U", -1); N("Z-SPD", -1); N("Z-SPD-O", -1);
            N("Z-SLOW-FREEZE", -1); N("Z-SLOW-COLD", -1); N("Z-SLOW-BUTTER", -1);

            Get("SYS-EMIT-PROOF").Enabled = true;
            Get("SYS-DAMAGE-FX").Enabled = true;
            Get("SYS-ELEMENT-FX").Enabled = true;
            // Schema defaults are not user-set; Effective* applies display defaults when IsSet=false.
        }
        SyncLocalStatsFromEntries();
    }

    static void Put(string id, CheatEntry e)
    {
        if (!Entries.ContainsKey(id)) Entries[id] = e;
    }

    public static CheatEntry Get(string id)
    {
        lock (Gate)
        {
            if (!Entries.TryGetValue(id, out var e))
            {
                e = new CheatEntry { Id = id, Kind = "toggle" };
                Entries[id] = e;
            }
            return e;
        }
    }

    public static long DocumentRevision;
    public static long AppliedRevision;

    public static bool On(string id)
    {
        var e = Get(id);
        return CheatSchema.EffectiveToggle(id, e.IsSet, e.Enabled);
    }

    public static float FVal(string id)
    {
        var e = Get(id);
        return (float)CheatSchema.EffectiveFloat(id, e.IsSet, e.FloatValue);
    }

    public static int IVal(string id)
    {
        var v = FVal(id);
        if (v >= int.MaxValue) return int.MaxValue;
        if (v <= int.MinValue) return int.MinValue;
        return (int)Math.Round(v);
    }

    public static bool IsUserSet(string id)
    {
        lock (Gate) return Entries.TryGetValue(id, out var e) && e.IsSet;
    }

    public static void SetToggle(string id, bool on, string source = "web", bool emitInject = true)
    {
        var e = Get(id);
        e.Enabled = on;
        e.IsSet = true;
        if (id == "SYS-EMIT-PROOF") EmitProof = on;
        if (id.StartsWith("A-", StringComparison.Ordinal)) SyncLocalStatsFromEntries();
        if (emitInject) EmitInject(source, "toggle", id, enabled: on);
        if (id.StartsWith("A-", StringComparison.Ordinal))
            Stats.Invalidate();
        MaybeSave();
    }

    public static void SetFloat(string id, double v, string source = "web", bool emitInject = true, bool forceApplyMaster = true)
    {
        var e = Get(id);
        e.FloatValue = v;
        e.Enabled = true;
        e.IsSet = true;
        // Tab A scales only apply when A-APPLY is on — auto-enable so FE Set is not a no-op.
        if (forceApplyMaster
            && (id.StartsWith("A-P-", StringComparison.Ordinal) || id.StartsWith("A-Z-", StringComparison.Ordinal)))
            SetToggle("A-APPLY", true, source, emitInject: false);
        if (id.StartsWith("A-", StringComparison.Ordinal)) SyncLocalStatsFromEntries();
        if (id.StartsWith("E-", StringComparison.Ordinal)) BoardConfigLocked = true;
        if (emitInject) EmitInject(source, "set-float", id, value: v);
        if (id.StartsWith("A-", StringComparison.Ordinal) || id.StartsWith("P-", StringComparison.Ordinal) || id.StartsWith("Z-", StringComparison.Ordinal))
            Stats.Invalidate();
        MaybeSave();
    }

    /// <summary>Pull StatsConfig without marking identity scales as user-set.</summary>
    static void PullScaleFloat(string id, double value, bool enabled)
    {
        if (!enabled || CheatSchema.IsUnsetOrIdentity(id, true, value))
        {
            // Leave unset — do not reify identity into Snapshot.
            lock (Gate)
            {
                if (Entries.TryGetValue(id, out var e))
                {
                    e.IsSet = false;
                    if (CheatSchema.TryGet(id, out var meta))
                    {
                        e.FloatValue = meta.DisplayDefault;
                        e.Enabled = meta.ToggleDefault;
                    }
                }
            }
            return;
        }
        SetFloat(id, value, "web", emitInject: false, forceApplyMaster: false);
    }

    public static void ClearField(string id, string source = "web")
    {
        lock (Gate)
        {
            if (Entries.TryGetValue(id, out var e))
            {
                e.IsSet = false;
                if (CheatSchema.TryGet(id, out var meta))
                {
                    e.FloatValue = meta.DisplayDefault;
                    e.Enabled = meta.ToggleDefault;
                    e.Kind = meta.Kind;
                }
            }
        }
        if (id.StartsWith("A-", StringComparison.Ordinal)) SyncLocalStatsFromEntries();
        if (id.StartsWith("E-", StringComparison.Ordinal) && !HasAnySetWithPrefix("E-"))
            BoardConfigLocked = false;
        EmitInject(source, "clear-field", id);
        if (id.StartsWith("A-", StringComparison.Ordinal) || id.StartsWith("P-", StringComparison.Ordinal) || id.StartsWith("Z-", StringComparison.Ordinal))
            Stats.Invalidate();
        MaybeSave();
        Note("cleared " + id);
    }

    static bool HasAnySetWithPrefix(string prefix)
    {
        lock (Gate)
        {
            foreach (var e in Entries.Values)
            {
                if (e.IsSet && e.Id.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }

    public static void SetFloatQuiet(string id, double v)
    {
        var e = Get(id);
        e.FloatValue = v;
        e.Enabled = true;
        e.IsSet = true;
    }

    public static void BeginProbe(string probeId, string? packId = null)
    {
        ActiveProbeId = probeId;
        ActivePackId = packId;
        ActiveCorrelationId = Guid.NewGuid().ToString("N");
        ActiveProbeUtc = DateTime.UtcNow;
    }

    public static void EndProbe(string? reason = null)
    {
        var pid = ActiveProbeId;
        var pack = ActivePackId;
        ActiveProbeId = null;
        ActivePackId = null;
        ActiveCorrelationId = null;
        if (!string.IsNullOrEmpty(pid) && EmitProof && On("SYS-EMIT-PROOF"))
        {
            GameHooks.Emit("probe.end", new Dictionary<string, object>
            {
                ["probeId"] = pid,
                ["packId"] = pack ?? "",
                ["reason"] = reason ?? "end"
            });
        }
    }

    public static void RefreshProbeTimeout()
    {
        if (string.IsNullOrEmpty(ActiveProbeId)) return;
        if ((DateTime.UtcNow - ActiveProbeUtc).TotalMinutes > ProbeTimeoutMinutes)
            EndProbe("timeout");
    }

    public static void EmitInject(
        string source,
        string op,
        string? id = null,
        string? action = null,
        bool? enabled = null,
        double? value = null)
    {
        RefreshProbeTimeout();
        if (!(EmitProof && On("SYS-EMIT-PROOF"))) return;
        var corr = ActiveCorrelationId ?? Guid.NewGuid().ToString("N");
        if (string.IsNullOrEmpty(ActiveCorrelationId)) ActiveCorrelationId = corr;
        var payload = new Dictionary<string, object>
        {
            ["source"] = source,
            ["op"] = op,
            ["correlationId"] = corr
        };
        if (!string.IsNullOrEmpty(ActiveProbeId)) payload["probeId"] = ActiveProbeId!;
        if (!string.IsNullOrEmpty(ActivePackId)) payload["packId"] = ActivePackId!;
        if (!string.IsNullOrEmpty(id)) payload["id"] = id!;
        if (!string.IsNullOrEmpty(action)) payload["action"] = action!;
        if (enabled is { } en) payload["enabled"] = en;
        if (value is { } v) payload["value"] = v;
        GameHooks.Emit("cheat.inject", payload);
    }

    public static void EmitActionInject(string action, string source = "web")
    {
        EmitInject(source, "action", action: action);
    }

    /// <summary>Attach active probe ids onto outcome payloads.</summary>
    public static void TagProbe(Dictionary<string, object> payload)
    {
        RefreshProbeTimeout();
        if (string.IsNullOrEmpty(ActiveProbeId)) return;
        payload["probeId"] = ActiveProbeId!;
        if (!string.IsNullOrEmpty(ActiveCorrelationId))
            payload["correlationId"] = ActiveCorrelationId!;
        if (!string.IsNullOrEmpty(ActivePackId))
            payload["packId"] = ActivePackId!;
    }

    public static void SyncLocalStatsFromEntries()
    {
        LocalStatsOverride = true;
        LocalStats.ApplyStats = On("A-APPLY");
        // Unset scales use identity (1 / 0) — never treat missing as 0%.
        LocalStats.Plants.HpPercent = ScalePct("A-P-HP%");
        LocalStats.Plants.HpFlat = ScaleFlat("A-P-HP+");
        LocalStats.Plants.AttackPercent = ScalePct("A-P-ATK%");
        LocalStats.Plants.AttackFlat = ScaleFlat("A-P-ATK+");
        LocalStats.Plants.DefensePercent = ScalePct("A-P-DEF%");
        LocalStats.Plants.DefenseFlat = ScaleFlat("A-P-DEF+");
        LocalStats.Zombies.HpPercent = ScalePct("A-Z-HP%");
        LocalStats.Zombies.HpFlat = ScaleFlat("A-Z-HP+");
        LocalStats.Zombies.AttackPercent = ScalePct("A-Z-ATK%");
        LocalStats.Zombies.AttackFlat = ScaleFlat("A-Z-ATK+");
        LocalStats.Zombies.DefensePercent = ScalePct("A-Z-DEF%");
        LocalStats.Zombies.DefenseFlat = ScaleFlat("A-Z-DEF+");
    }

    static float ScalePct(string id) => IsUserSet(id) ? FVal(id) : 1f;
    static int ScaleFlat(string id) => IsUserSet(id) ? IVal(id) : 0;

    /// <summary>True when Tab A has at least one non-identity user-set scale for plants.</summary>
    public static bool HasPlantScaleMods()
    {
        if (!On("A-APPLY")) return false;
        return NonIdentityScale("A-P-HP%", "A-P-HP+")
               || NonIdentityScale("A-P-ATK%", "A-P-ATK+")
               || NonIdentityScale("A-P-DEF%", "A-P-DEF+");
    }

    public static bool HasZombieScaleMods()
    {
        if (!On("A-APPLY")) return false;
        return NonIdentityScale("A-Z-HP%", "A-Z-HP+")
               || NonIdentityScale("A-Z-ATK%", "A-Z-ATK+")
               || NonIdentityScale("A-Z-DEF%", "A-Z-DEF+");
    }

    static bool NonIdentityScale(string pctId, string flatId)
    {
        if (IsUserSet(pctId) && Math.Abs(FVal(pctId) - 1f) > 0.0001f) return true;
        if (IsUserSet(flatId) && IVal(flatId) != 0) return true;
        return false;
    }

    public static bool HasPlantExtrasSet()
    {
        foreach (var id in new[]
                 {
                     "P-SHIELD", "P-ATK-INT", "P-ATK-CD", "P-ATK-ADD", "P-PROD-INT", "P-PROD-CD",
                     "P-SPEED", "P-MOVE", "P-LEVEL", "P-SHOOTLVL", "P-MOD-HP", "P-MOD-ATK"
                 })
        {
            if (IsUserSet(id)) return true;
        }
        return false;
    }

    public static bool HasZombieExtrasSet()
    {
        foreach (var id in new[]
                 {
                     "Z-ARMOR-F", "Z-TAKEMULT", "Z-SPD-U", "Z-SPD", "Z-SPD-O",
                     "Z-SLOW-FREEZE", "Z-SLOW-COLD", "Z-SLOW-BUTTER"
                 })
        {
            if (IsUserSet(id)) return true;
        }
        return false;
    }

    /// <summary>Tab B plant overrides — only user-set positive values.</summary>
    public static Dictionary<string, int> BuildPlantAbsolute()
    {
        var d = new Dictionary<string, int>(StringComparer.Ordinal);
        void Put(string channel, string id)
        {
            if (!IsUserSet(id)) return;
            var v = IVal(id);
            if (v > 0) d[channel] = v;
        }
        Put(StatChannels.Hp, "P-HP");
        Put(StatChannels.MaxHp, "P-MAXHP");
        Put(StatChannels.Atk, "P-ATK");
        return d;
    }

    /// <summary>
    /// The real-valued absolutes (E16): fire rate, sun rate, creep speed.
    ///
    /// <para>These three used to be written straight to the Unity field from the extras path,
    /// bypassing the modifier bag — which is why no effect could ever reach them, and why "shoots
    /// faster" was unauthorable. They are Overrides now, the same shape <c>P-HP</c> and <c>P-ATK</c>
    /// have always had, so the operator surface is unchanged and there is one path to the field.</para>
    ///
    /// <para>Separate from <see cref="BuildPlantAbsolute"/> because these are fractions and that map
    /// is <c>int</c>: an attack interval of 1.5 seconds would truncate to 1.</para>
    /// </summary>
    public static Dictionary<string, double> BuildPlantAbsoluteReal()
    {
        var d = new Dictionary<string, double>(StringComparer.Ordinal);
        void Put(string channel, string id)
        {
            if (!IsUserSet(id)) return;
            var v = FVal(id);
            if (v > 0) d[channel] = v;
        }
        Put(StatChannels.AttackInterval, "P-ATK-INT");
        Put(StatChannels.ProduceInterval, "P-PROD-INT");
        return d;
    }

    public static Dictionary<string, double> BuildZombieAbsoluteReal()
    {
        var d = new Dictionary<string, double>(StringComparer.Ordinal);
        if (IsUserSet("Z-SPD-U") && FVal("Z-SPD-U") > 0)
            d[StatChannels.ZombieSpeed] = FVal("Z-SPD-U");
        return d;
    }

    public static Dictionary<string, int> BuildZombieAbsolute()
    {
        var d = new Dictionary<string, int>(StringComparer.Ordinal);
        void Put(string channel, string id)
        {
            if (!IsUserSet(id)) return;
            var v = IVal(id);
            if (v > 0) d[channel] = v;
        }
        Put(StatChannels.Hp, "Z-HP");
        Put(StatChannels.MaxHp, "Z-MAXHP");
        Put(StatChannels.Atk, "Z-ATK");
        Put(StatChannels.Arm1, "Z-ARM1");
        Put(StatChannels.Arm1Max, "Z-ARM1MAX");
        Put(StatChannels.Arm2, "Z-ARM2");
        Put(StatChannels.Arm2Max, "Z-ARM2MAX");
        return d;
    }

    public static void PullFromServer(StatsConfig s)
    {
        LocalStats.LogDamage = s.LogDamage;
        SetToggle("A-APPLY", s.ApplyStats, "web", emitInject: false);
        PullScaleFloat("A-P-HP%", s.Plants.HpPercent, s.ApplyStats);
        PullScaleFloat("A-P-HP+", s.Plants.HpFlat, s.ApplyStats);
        PullScaleFloat("A-P-ATK%", s.Plants.AttackPercent, s.ApplyStats);
        PullScaleFloat("A-P-ATK+", s.Plants.AttackFlat, s.ApplyStats);
        PullScaleFloat("A-P-DEF%", s.Plants.DefensePercent, s.ApplyStats);
        PullScaleFloat("A-P-DEF+", s.Plants.DefenseFlat, s.ApplyStats);
        PullScaleFloat("A-Z-HP%", s.Zombies.HpPercent, s.ApplyStats);
        PullScaleFloat("A-Z-HP+", s.Zombies.HpFlat, s.ApplyStats);
        PullScaleFloat("A-Z-ATK%", s.Zombies.AttackPercent, s.ApplyStats);
        PullScaleFloat("A-Z-ATK+", s.Zombies.AttackFlat, s.ApplyStats);
        PullScaleFloat("A-Z-DEF%", s.Zombies.DefensePercent, s.ApplyStats);
        PullScaleFloat("A-Z-DEF+", s.Zombies.DefenseFlat, s.ApplyStats);
        SyncLocalStatsFromEntries();
        Stats.Invalidate();
        Note("pulled stats from server");
        EmitInject("web", "action", action: "pull-stats");
    }

    public static void ApplyPvzStatsModifiers(long playerId, long revision, IEnumerable<PvzStatModifierDto> modifiers)
    {
        var list = new List<StatModifier>();
        foreach (var m in modifiers)
        {
            if (m == null || !m.Enabled) continue;
            if (string.IsNullOrWhiteSpace(m.Channel)) continue;
            list.Add(PvzStatsSheetComposer.ToStatModifier(
                m.PluginId, m.SourceKind, m.SourceId, m.Channel, m.Op, m.Value, m.Priority));
        }
        PvzStatsMods = list;
        PvzStatsPlayerId = playerId;
        PvzStatsRevision = revision;
        Stats.Invalidate();
        Note($"pvz.stats loaded player={playerId} rev={revision} n={list.Count}");
    }

    public static void RegisterSpawnSourceTag(IntPtr ptr, string source)
    {
        if (ptr == IntPtr.Zero || string.IsNullOrWhiteSpace(source)) return;
        SpawnSourceByPtr[ptr] = source;
    }

    public static string? ConsumeSpawnSourceTag(IntPtr ptr)
    {
        if (ptr != IntPtr.Zero && SpawnSourceByPtr.TryRemove(ptr, out var tagged))
            return tagged;
        var pending = PendingSpawnSourceTag;
        PendingSpawnSourceTag = null;
        return string.IsNullOrWhiteSpace(pending) ? null : pending;
    }

    public static void ClearPendingSpawnSourceTag() => PendingSpawnSourceTag = null;

    public static bool HasPvzStatsMods() => PvzStatsMods.Count > 0;

    /// <summary>True when dirty reapply should run (cheat doc, PvzStats revision, or Tab A scales).</summary>
    public static bool ShouldPushScalesOnDirty() =>
        PvzStatsApplyGate.ShouldPushOnDirty(
            DocumentRevision,
            AppliedRevision,
            PvzStatsRevision,
            AppliedPvzStatsRevision,
            HasPlantScaleMods(),
            HasZombieScaleMods());

    public static void MarkAppliedRevision() => AppliedRevision = DocumentRevision;

    public static void MarkAppliedPvzStatsRevision() => AppliedPvzStatsRevision = PvzStatsRevision;

    static void CopyMod(StatMod from, StatMod to)
    {
        to.HpPercent = from.HpPercent; to.HpFlat = from.HpFlat;
        to.AttackPercent = from.AttackPercent; to.AttackFlat = from.AttackFlat;
        to.DefensePercent = from.DefensePercent; to.DefenseFlat = from.DefenseFlat;
    }

    public static StatsConfig EffectiveStats()
    {
        // Feeds cheat.scale plugin input only — apply path uses StatSystem.Resolve.
        SyncLocalStatsFromEntries();
        return LocalStats;
    }

    public static void ResetAll()
    {
        lock (Gate) Entries.Clear();
        EnsureDefaults();
        LocalStatsOverride = false;
        BoardConfigLocked = false;
        DocumentRevision++;
        AppliedRevision = DocumentRevision;
        Note("reset all cheats");
        MaybeSave();
    }

    public static void ResetGroup(string prefix)
    {
        lock (Gate)
        {
            foreach (var key in Entries.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
                Entries.Remove(key);
        }
        EnsureDefaults();
        if (prefix.StartsWith("E-", StringComparison.Ordinal) || prefix == "E-")
            BoardConfigLocked = false;
        else if (!HasAnySetWithPrefix("E-"))
            BoardConfigLocked = false;
        DocumentRevision++;
        AppliedRevision = DocumentRevision;
        Note("reset group " + prefix);
        MaybeSave();
    }

    public static void Select(IntPtr ptr, string side)
    {
        SelectedPtr = ptr;
        SelectedSide = side;
    }

    public static void ClearSelection()
    {
        SelectedPtr = IntPtr.Zero;
        SelectedSide = "";
    }

    public static void Note(string msg)
    {
        LastNote = msg;
        try { RpgHost.Log.Info("[cheat] " + msg); } catch { }
        if (EmitProof && On("SYS-EMIT-PROOF"))
        {
            var payload = new Dictionary<string, object> { ["note"] = msg };
            TagProbe(payload);
            GameHooks.Emit("cheat.apply", payload);
        }
    }

    public static void Error(string msg)
    {
        LastError = msg;
        RpgHost.Log.Warning("[cheat] " + msg);
        Note("ERR " + msg);
    }

    public static Dictionary<string, object> Snapshot()
    {
        lock (Gate)
        {
            // SSOT: only user-set entries (absence = unset). Schema holds display defaults.
            var setEntries = Entries.Values.Where(e => e.IsSet).Select(e => new Dictionary<string, object>
            {
                ["id"] = e.Id,
                ["kind"] = e.Kind,
                ["enabled"] = e.Enabled,
                ["floatValue"] = e.FloatValue,
                ["isSet"] = true
            }).ToList();
            return new Dictionary<string, object>
            {
                ["menuEnabled"] = false,
                ["revision"] = DocumentRevision,
                ["persist"] = PersistEnabled,
                ["emitProof"] = EmitProof,
                ["localOverride"] = LocalStatsOverride,
                ["boardConfigLocked"] = BoardConfigLocked,
                ["selectedPtr"] = SelectedPtr.ToString("X"),
                ["selectedSide"] = SelectedSide,
                ["spawnCol"] = SpawnCol,
                ["spawnRow"] = SpawnRow,
                ["catalogPlants"] = SpawnCatalog.PlantCount,
                ["catalogZombies"] = SpawnCatalog.ZombieCount,
                ["note"] = LastNote,
                ["activeProbeId"] = ActiveProbeId ?? "",
                ["activePackId"] = ActivePackId ?? "",
                ["entries"] = setEntries
            };
        }
    }

    public static void ApplySnapshot(JsonElement root)
    {
        if (root.TryGetProperty("persist", out var p)) PersistEnabled = p.GetBoolean();
        if (root.TryGetProperty("emitProof", out var ep)) EmitProof = ep.GetBoolean();
        MenuEnabled = false;
        MenuOpen = false;
        if (root.TryGetProperty("revision", out var rev) && rev.TryGetInt64(out var r))
            DocumentRevision = r;

        // Replace set flags: snapshot entries are the only IsSet=true values.
        lock (Gate)
        {
            foreach (var e in Entries.Values)
                e.IsSet = false;
        }

        if (root.TryGetProperty("entries", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                var id = item.GetProperty("id").GetString() ?? "";
                if (string.IsNullOrEmpty(id)) continue;
                var enabled = item.TryGetProperty("enabled", out var en) && en.GetBoolean();
                var fv = item.TryGetProperty("floatValue", out var fvel) && fvel.TryGetDouble(out var d) ? d : 0d;
                if (CheatSchema.ShouldStripFromDocument(id, enabled, fv,
                        item.TryGetProperty("kind", out var k) ? k.GetString() : null))
                    continue;
                var e = Get(id);
                e.Enabled = enabled;
                e.FloatValue = fv;
                e.IsSet = true;
                if (item.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String)
                    e.Kind = kind.GetString() ?? e.Kind;
            }
        }
        SyncLocalStatsFromEntries();
        LocalStatsOverride = true;
        if (root.TryGetProperty("boardConfigLocked", out var bcl))
            BoardConfigLocked = bcl.GetBoolean();
        else
            BoardConfigLocked = HasAnySetWithPrefix("E-");
        AppliedRevision = DocumentRevision;
        Stats.Invalidate();
    }

    public static void MaybeSave()
    {
        if (!PersistEnabled || string.IsNullOrEmpty(_persistPath)) return;
        try
        {
            var json = JsonSerializer.Serialize(Snapshot());
            File.WriteAllText(_persistPath!, json);
        }
        catch (Exception ex) { RpgHost.Log.Warning("cheat save: " + ex.Message); }
    }

    public static void TryLoad()
    {
        if (string.IsNullOrEmpty(_persistPath) || !File.Exists(_persistPath)) return;
        try
        {
            var json = File.ReadAllText(_persistPath!);
            using var doc = JsonDocument.Parse(json);
            ApplySnapshot(doc.RootElement);
            Note("loaded cheat-state.json");
        }
        catch (Exception ex) { RpgHost.Log.Warning("cheat load: " + ex.Message); }
    }
}

public sealed class CheatEntry
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "toggle";
    public bool Enabled { get; set; }
    public double FloatValue { get; set; }
    /// <summary>False = unset (absent for apply). True = user/web explicitly set.</summary>
    public bool IsSet { get; set; }
}
