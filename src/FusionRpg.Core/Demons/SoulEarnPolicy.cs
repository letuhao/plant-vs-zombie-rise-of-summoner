namespace FusionRpg.Core.Demons;

/// <summary>
/// Souls earn policy v2 (spec-soul-economy.md, locked 2026-08-21). Pure functions — the store
/// applies them inside the fact-append transaction. Targets ~5–8 pulls/hour of active play.
/// </summary>
public static class SoulEarnPolicy
{
    public const int PolicyVersion = 2;

    /// <summary>+1 per kill, capped — kills the stall-farm exploit (uncapped pay rewards match length).</summary>
    public const int KillDelta = 1;
    public const int KillCapPerMatch = 50;

    /// <summary>Victory pays full for the first wins of the (UTC) day, then decays 50%.</summary>
    public const int VictoryDelta = 100;
    public const int VictoryFullPerDay = 3;
    public const int DefeatDelta = 25;

    /// <summary>Codex discovery faucet — first-ever discovery of a species, by rarity.</summary>
    public static int DiscoveryDelta(DemonRarity rarity) => rarity switch
    {
        DemonRarity.Common => 25,
        DemonRarity.Rare => 75,
        DemonRarity.Epic => 200,
        DemonRarity.Legendary => 500,
        _ => 0
    };

    public const int CodexHalfMilestone = 500;   // 50 % of the catalog discovered
    public const int CodexFullMilestone = 1500;  // 100 % (claimable at 90 % once capture-exclusives exist)

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
