namespace FusionRpg.Core.Demons;

/// <summary>
/// Souls earn policy v2 (spec-soul-economy.md, locked 2026-08-21). Pure functions — the store
/// applies them inside the fact-append transaction. Targets ~5–8 pulls/hour of active play.
/// </summary>
public static class SoulEarnPolicy
{
    public const int PolicyVersion = 2;

    static SoulEarnTuning? _tuning;

    /// <summary>Host-only (Injector/Server startup, or a test's inline construction).</summary>
    public static void Configure(SoulEarnTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    static SoulEarnTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "SoulEarnPolicy.Configure(...) has not run. Every soul-earn rule reads data/tuning/" +
        "souls.v{n}.json (tunables-ssot.md T5) — there is no built-in default to fall back to.");

    /// <summary>+1 per kill, capped — kills the stall-farm exploit (uncapped pay rewards match length).
    /// killCapPerMatch is named for deletion by caps-reconcile (power-plan.md T3.6, not yet
    /// authorized; SSOT §11.7a — audit F11 covers `victoryFullPerDay` alongside it).</summary>
    public static int KillDelta => Tuning.Kill.KillDelta;
    public static int KillCapPerMatch => Tuning.Kill.KillCapPerMatch;

    /// <summary>Victory pays full for the first wins of the (UTC) day, then decays 50%.</summary>
    public static int VictoryDelta => Tuning.MatchEnd.VictoryDelta;
    public static int VictoryFullPerDay => Tuning.MatchEnd.VictoryFullPerDay;
    public static int DefeatDelta => Tuning.MatchEnd.DefeatDelta;

    /// <summary>Codex discovery faucet — first-ever discovery of a species, by rarity.</summary>
    public static int DiscoveryDelta(DemonRarity rarity) =>
        Tuning.DiscoveryDelta.TryGetValue(rarity, out var v) ? v : 0;

    public static int CodexHalfMilestone => Tuning.Codex.HalfMilestone;   // 50 % of the catalog discovered
    public static int CodexFullMilestone => Tuning.Codex.FullMilestone;  // 100 % (claimable at 90 % once capture-exclusives exist)

    public static class Reasons
    {
        public const string Kill = "kill";
        public const string Victory = "victory";
        public const string Defeat = "defeat";
        public const string Discovery = "discovery";
        public const string Milestone = "milestone";
        public const string Summon = "summon";
        public const string Expedition = "expedition";
        public const string Fusion = "fusion";
        public const string Patron = "patron";
        /// <summary>Daily contract tribute, one row per settled UTC day.</summary>
        public const string Upkeep = "upkeep";
        public const string ContractSlot = "contract-slot";
        public const string ContractRitual = "contract-ritual";
        /// <summary>Test/dev bankrolls only — keeps the discovery namespace clean for analytics.</summary>
        public const string Seed = "seed";
    }

    /// <summary>Delta for one kill given how many kills this match already earned. 0 past the cap.</summary>
    public static int KillEarn(int killsAlreadyCounted) =>
        killsAlreadyCounted < KillCapPerMatch ? KillDelta : 0;

    /// <summary>Delta for a match result given prior victory earns today (UTC).</summary>
    public static int MatchEndEarn(bool victory, int priorVictoriesToday) =>
        victory
            ? priorVictoriesToday < VictoryFullPerDay ? VictoryDelta : VictoryDelta / 2
            : DefeatDelta;
}
