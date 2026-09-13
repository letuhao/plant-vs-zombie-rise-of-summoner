using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Combat.Observability;

/// <summary>
/// One captured hit, as reported by <see cref="LawnCombatObserver"/> — Task 0 (`lawn-combat-wire`)
/// "the ruler". The vanilla half (<see cref="VanillaAmount"/>, <see cref="AttackerPtr"/>,
/// <see cref="VictimPtr"/>, <see cref="SwingId"/>) is populated on every real vanilla hit,
/// unconditionally. The RPG half (<see cref="RpgDelta"/>, the two element fields,
/// <see cref="MatchupRelation"/>) is populated only once a real overlay breakdown for the SAME swing
/// is observed — see <see cref="RpgDeltaObserved"/>. Today (before `basic-attack-grant`/T10 lands) no
/// grant exists anywhere on the lawn, so <see cref="RpgDeltaObserved"/> is always <c>false</c> and
/// <see cref="RpgDelta"/> is an explicit <c>0</c> — a real reading, never a missing value. That
/// distinction is the whole point of this record shape.
/// </summary>
public readonly record struct LawnCombatHitRecord(
    long Seq,
    int Frame,
    string SwingId,
    string AttackerPtr,
    string VictimPtr,
    string AttackerSide,
    long VanillaAmount,
    long RpgDelta,
    bool RpgDeltaObserved,
    ElementTypeId? AttackerElement,
    ElementTypeId? VictimElement,
    ElementMatchupRelation? MatchupRelation);

/// <summary>
/// Aggregate + sampled-record snapshot for one reporting window — same "read and reset the rolling
/// window" shape as <c>EventDrainHost.SnapshotStats()</c> / <c>PerfProbe.SnapshotAndReset()</c>, so it
/// rides the same already-shipped ~5s <c>/api/perf</c> flush (see <c>PerfReporter.cs</c>) rather than
/// inventing a new transport.
/// </summary>
public sealed class LawnCombatObserverSnapshot
{
    public long TotalHits { get; init; }
    public long TotalSwings { get; init; }
    /// <summary>D8: measured, never argued. Once `basic-attack-grant` lands this must equal
    /// <see cref="TotalSwings"/>, never <see cref="TotalHits"/> (a piercing shot is one trigger, N
    /// damage applications). Zero today — no `ActionKind.Basic` grant exists yet to trigger.</summary>
    public long ActionTriggers { get; init; }
    public long StaminaSpent { get; init; }
    public long RegenAccrued { get; init; }
    public long ExhaustionEvents { get; init; }
    /// <summary>How many recorded hits this window ended up with a real overlay breakdown merged in
    /// (<see cref="LawnCombatHitRecord.RpgDeltaObserved"/> true). Zero today, by construction.</summary>
    public long RpgDeltaMergedHits { get; init; }
    /// <summary>D9-shaped self-check: THIS observer's own ring overflow counter for the window — never
    /// silently discards a record, always counts what it drops. Distinct from
    /// `EventDrainHost.SnapshotStats()`'s own drop counters, which this snapshot does not duplicate.</summary>
    public long DroppedRecords { get; init; }
    public IReadOnlyList<LawnCombatHitRecord> RecentHits { get; init; } = Array.Empty<LawnCombatHitRecord>();
}

/// <summary>
/// Task 0 (`lawn-combat-wire`, "Phase 0 — the ruler") — the instrument every later phase in the
/// program reports through. Unity-free so it is unit-testable in Core.Tests without a live game.
///
/// <para><b>Why this exists as a separate, always-on sink instead of reusing `EmitOverlayBreakdown` /
/// `combat.hit` telemetry:</b> both of those are gated — `EmitOverlayBreakdown` only emits inside a
/// debug session (`InjectorCombatBridge.cs`), and the legacy `combat.hit`/`plant.damage`/
/// `zombie.damage` telemetry only emits when `LogDamage`/`SessionActive` is on, which (see
/// `GameHooks.cs`'s `PlantTakeDamage`/`ZombieTakeDamage` prefixes) actually ROUTES THE HIT DOWN A
/// DIFFERENT BRANCH than the shipped v2 record path (`EventDrainHost.TryRecordDealtFromBullet` /
/// `TryRecordTaken`) — flipping either one would perturb exactly what this ruler exists to measure.
/// This class has no such gate: every `Record*` method runs unconditionally from the injector's
/// already-existing hot-path hooks (see `LawnCombatObserverBridge.cs` for the call sites), so it
/// changes no branch anywhere in the pipeline it observes.</para>
///
/// <para><b>Threading:</b> every `Record*` call happens on the Unity main thread only — the same
/// assumption every other hot-path recorder in this codebase already makes
/// (<c>EventStringInterner</c>'s own doc comment: "Main-thread only, like every producer"). No lock.</para>
/// </summary>
public static class LawnCombatObserver
{
    /// <summary>Structural cap on one reporting window's own memory (tunables-ssot.md: a balance pass
    /// would never touch this) — never a progression ceiling. Exceeding it drops the OLDEST-unrecorded
    /// slot's worth of NEW records for the rest of the window and counts every drop; it never silently
    /// discards without counting.</summary>
    public const int MaxRecentHitsPerWindow = 512;

    static readonly List<LawnCombatHitRecord> Recent = new();
    static readonly Dictionary<string, int> IndexBySwingVictim = new(StringComparer.Ordinal);
    static readonly HashSet<string> SwingIds = new(StringComparer.Ordinal);

    static long _totalHits;
    static long _totalSwings;
    static long _actionTriggers;
    static long _staminaSpent;
    static long _regenAccrued;
    static long _exhaustionEvents;
    static long _dropped;
    static long _rpgDeltaMerged;
    static long _seq;

    /// <summary>Unconditional vanilla-hit capture — called once per `Plant.TakeDamage`/
    /// `Zombie.TakeDamage` prefix, after any vanilla defense scaling and before any RPG contribution
    /// could exist. <paramref name="swingId"/>: the bullet's own ptr for a projectile hit, or
    /// `attackerPtr:frame` for a direct/melee hit — a provisional shape (T6/`lawn-hit-attribution`
    /// formalises real swing identity); good enough to make D8's "triggers == swings" measurable once
    /// there is anything to measure.</summary>
    public static void RecordVanillaHit(
        string swingId, string attackerPtr, string victimPtr, string attackerSide, long vanillaAmount, int frame)
    {
        if (string.IsNullOrEmpty(swingId)) swingId = attackerPtr;
        if (string.IsNullOrEmpty(victimPtr)) victimPtr = "";

        _totalHits++;
        if (SwingIds.Add(swingId)) _totalSwings++;

        if (Recent.Count >= MaxRecentHitsPerWindow)
        {
            _dropped++;
            return; // counted, never silent — this window's sample is full, next window resumes.
        }

        var record = new LawnCombatHitRecord(
            Seq: ++_seq, Frame: frame, SwingId: swingId,
            AttackerPtr: attackerPtr, VictimPtr: victimPtr, AttackerSide: attackerSide,
            VanillaAmount: vanillaAmount, RpgDelta: 0, RpgDeltaObserved: false,
            AttackerElement: null, VictimElement: null, MatchupRelation: null);
        Recent.Add(record);
        IndexBySwingVictim[Key(swingId, victimPtr)] = Recent.Count - 1;
    }

    /// <summary>Unconditional RPG-delta capture — called alongside (never instead of)
    /// `InjectorCombatBridge.EmitOverlayBreakdown` from the SAME `WireCombatMath` callback, so it fires
    /// whenever an overlay breakdown is computed for a real hit, debug session or not. Merges into the
    /// matching vanilla record for the same swing+victim when one exists this window; otherwise still
    /// records the RPG-side observation on its own rather than dropping it (the two halves can miss
    /// each other at a window boundary — see the merge-miss branch below).</summary>
    public static void RecordRpgDelta(
        string swingId, string attackerPtr, string victimPtr, long rpgDelta,
        ElementTypeId? attackerElement, ElementTypeId? victimElement, ElementMatchupRelation? matchupRelation)
    {
        _rpgDeltaMerged++;
        if (IndexBySwingVictim.TryGetValue(Key(swingId, victimPtr), out var idx) && idx < Recent.Count)
        {
            var r = Recent[idx];
            Recent[idx] = r with
            {
                RpgDelta = rpgDelta,
                RpgDeltaObserved = true,
                AttackerElement = attackerElement,
                VictimElement = victimElement,
                MatchupRelation = matchupRelation
            };
            return;
        }

        if (Recent.Count >= MaxRecentHitsPerWindow) { _dropped++; return; }
        Recent.Add(new LawnCombatHitRecord(
            Seq: ++_seq, Frame: 0, SwingId: swingId,
            AttackerPtr: attackerPtr, VictimPtr: victimPtr, AttackerSide: "",
            VanillaAmount: 0, RpgDelta: rpgDelta, RpgDeltaObserved: true,
            AttackerElement: attackerElement, VictimElement: victimElement, MatchupRelation: matchupRelation));
    }

    /// <summary>D8 hook — not yet called from anywhere (`basic-attack-grant`/T10 is the real caller).
    /// Ready so this task does not need to be revisited to wire it.</summary>
    public static void RecordActionTrigger(string swingId) => _actionTriggers++;

    /// <summary>`basic-attack-cost`/T12 hook — not yet called from anywhere.</summary>
    public static void RecordStaminaSpent(long amount) => _staminaSpent += amount;

    /// <summary>`resource-subtick`/T5 + `basic-attack-cost`/T12 hook — not yet called from anywhere.</summary>
    public static void RecordRegenAccrued(long amount) => _regenAccrued += amount;

    /// <summary>`basic-attack-cost`/T12 hook — not yet called from anywhere.</summary>
    public static void RecordExhaustion() => _exhaustionEvents++;

    public static LawnCombatObserverSnapshot SnapshotAndReset()
    {
        var snap = new LawnCombatObserverSnapshot
        {
            TotalHits = _totalHits,
            TotalSwings = _totalSwings,
            ActionTriggers = _actionTriggers,
            StaminaSpent = _staminaSpent,
            RegenAccrued = _regenAccrued,
            ExhaustionEvents = _exhaustionEvents,
            RpgDeltaMergedHits = _rpgDeltaMerged,
            DroppedRecords = _dropped,
            RecentHits = Recent.ToArray()
        };

        _totalHits = 0;
        _totalSwings = 0;
        SwingIds.Clear();
        _actionTriggers = 0;
        _staminaSpent = 0;
        _regenAccrued = 0;
        _exhaustionEvents = 0;
        _dropped = 0;
        _rpgDeltaMerged = 0;
        Recent.Clear();
        IndexBySwingVictim.Clear();
        return snap;
    }

    /// <summary>Test-only full reset — also zeroes the monotonic <c>Seq</c>, which production never
    /// needs (a live process only ever calls <see cref="SnapshotAndReset"/>).</summary>
    internal static void ResetForTest()
    {
        SnapshotAndReset();
        _seq = 0;
    }

    static string Key(string swingId, string victimPtr) => swingId + "|" + victimPtr;
}
