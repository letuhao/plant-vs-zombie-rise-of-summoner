using FusionRpg.Core.Power;

namespace FusionRpg.Core.Demons;

/// <summary>
/// Souls earn policy v2 (spec-soul-economy.md, locked 2026-08-21). Pure functions — the store
/// applies them inside the fact-append transaction. Targets ~5–8 pulls/hour of active play.
///
/// <para><b>T3.6 (spec-caps-reconcile.md §2.3, SSOT §11.7/§11.7a, 2026-08-24):</b> the flat
/// per-match kill cap and the daily-victory decay are gone — a flat faucet facing a scaling sink is
/// starvation with a delay fuse, not a throttle (audit F11 covers the victory decay specifically: it
/// refuses nothing and reads as a threshold, not a ceiling, which is why three earlier cap sweeps
/// missed it). <see cref="KillEarn"/>/<see cref="MatchEndEarn"/> now multiply the same unchanged
/// constants by <see cref="ContentScale"/> instead — byte-identical at Θ=20 (the pin), scaling with
/// the run's depth thereafter, so the faucet tracks the sink the same way every other magnitude does.
/// </para>
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

    /// <summary>Base souls per earning kill before contentScale — T3.6 deleted the per-match cap
    /// this used to answer to (SSOT §11.7a: farming weak enemies now pays little on its own, at the
    /// root, rather than being throttled by a count).</summary>
    public static int KillDelta => Tuning.Kill.KillDelta;

    /// <summary>Base souls per match result before contentScale — T3.6 deleted the daily-victory
    /// decay (audit F11).</summary>
    public static int VictoryDelta => Tuning.MatchEnd.VictoryDelta;
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

    /// <summary>
    /// Souls for one earning kill: <c>KillDelta × contentScale(thetaEnemy)</c> (SSOT §11.7a).
    /// <c>thetaEnemy</c> is required, not defaulted (same discipline as content-scale's own
    /// <c>Instantiator</c>, T3.4) — a caller with no real depth signal must say so explicitly at the
    /// pin (Θ=20), never silently.
    /// </summary>
    public static long KillEarn(int thetaEnemy, PowerTuning tuning) =>
        ContentScale.Apply(KillDelta, ContentScale.Milli(thetaEnemy, tuning));

    /// <summary>Souls for a match result: <c>(victory ? VictoryDelta : DefeatDelta) ×
    /// contentScale(thetaRun)</c> (SSOT §11.7a).</summary>
    public static long MatchEndEarn(bool victory, int thetaRun, PowerTuning tuning) =>
        ContentScale.Apply(victory ? VictoryDelta : DefeatDelta, ContentScale.Milli(thetaRun, tuning));
}
