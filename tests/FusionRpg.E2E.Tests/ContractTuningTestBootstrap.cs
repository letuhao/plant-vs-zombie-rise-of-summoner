using System.Runtime.CompilerServices;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Combat.Shield;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Contracts;
using FusionRpg.Core.Demons.Fusion;
using FusionRpg.Core.Demons.Patron;
using FusionRpg.Core.Expeditions;
using FusionRpg.Core.Overlay;
using FusionRpg.Core.Status;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.World;
using FusionRpg.Core.World.Loam;

namespace FusionRpg.E2E.Tests;

/// <summary>
/// tunables-ssot.md §7.2: "Tests → construct one inline; no fixture files." Every test in this
/// assembly that reaches a migrated Policy class (<see cref="ContractPolicy"/>, <see cref="LoamPolicy"/>,
/// <see cref="WorldTuningHub"/>, <see cref="SoulEarnPolicy"/>, <see cref="PatronPolicy"/>,
/// <see cref="ShieldPolicy"/>, <see cref="CombatPolicy"/>, <see cref="StarPolicy"/>,
/// <see cref="StatusPolicy"/>, <see cref="OverlayTuningHub"/>, <see cref="StatsTuningHub"/>,
/// <see cref="ExpeditionTuningHub"/>) needs it configured first; a module initializer does that once,
/// so no individual test class has to repeat the boilerplate. Values are the same working set as the
/// matching <c>data/tuning/*.v1.json</c> — kept as literal C# objects here rather than a file read,
/// so tests exercise construction, not file I/O.
/// </summary>
internal static class ContractTuningTestBootstrap
{
    [ModuleInitializer]
    public static void Init()
    {
        ContractPolicy.Configure(DefaultContracts);
        LoamPolicy.Configure(DefaultLoam);
        WorldTuningHub.Configure(DefaultWorld);
        SoulEarnPolicy.Configure(DefaultSouls);
        PatronPolicy.Configure(DefaultPatron);
        ShieldPolicy.Configure(DefaultShield);
        CombatPolicy.Configure(DefaultCombat);
        StarPolicy.Configure(DefaultFusion);
        StatusPolicy.Configure(DefaultStatus);
        OverlayTuningHub.Configure(DefaultOverlay);
        StatsTuningHub.Configure(DefaultStats);
        ExpeditionTuningHub.Configure(DefaultExpeditions);
    }

    public static readonly ContractTuning DefaultContracts = new(
        SchemaVersion: 1,
        Version: 1,
        Loyalty: new ContractLoyaltyTuning(
            Max: 1000, DeployFloor: 200, BindLoyalty: 300,
            SwornThreshold: 400, TrustedThreshold: 600, DevotedThreshold: 800,
            WinGain: 15, LossPenalty: 10, DailyGainCap: 60, DecayPerDay: 25, RitualGain: 100,
            RankBonusSwornMilli: 15, RankBonusTrustedMilli: 35, RankBonusDevotedMilli: 60),
        Slots: new ContractSlotsTuning(BaseSlots: 12, MaxSlots: 48, SlotPriceStep: 300),
        Settlement: new ContractSettlementTuning(MaxSettleDays: 30),
        PersonalityRates: new Dictionary<DemonPersonality, PersonalityRateTuning>
        {
            [DemonPersonality.Loyal] = new(120, 80, 100),
            [DemonPersonality.Stoic] = new(90, 60, 100),
            [DemonPersonality.Proud] = new(100, 100, 130),
            [DemonPersonality.Calculating] = new(100, 90, 110),
            [DemonPersonality.Feral] = new(80, 150, 70),
        },
        BaseUpkeepPerDay: new Dictionary<DemonRarity, int>
        {
            [DemonRarity.Common] = 2,
            [DemonRarity.Rare] = 5,
            [DemonRarity.Epic] = 12,
            [DemonRarity.Legendary] = 25,
        },
        RitualPriceSouls: new Dictionary<DemonRarity, long>
        {
            [DemonRarity.Common] = 50,
            [DemonRarity.Rare] = 100,
            [DemonRarity.Epic] = 200,
            [DemonRarity.Legendary] = 400,
        });

    public static readonly LoamTuning DefaultLoam = new(
        SchemaVersion: 1,
        Version: 1,
        Upkeep: new LoamUpkeepTuning(
            SeepPerTurn: 50, LoamCapacity: 300, BaseUpkeepPerSector: 10,
            GarrisonUpkeepPerMember: 2, DevelopmentUpkeepPerLevel: 5, DangerUpkeepPerBand: 3),
        Fade: new LoamFadeTuning(
            RecoveryMilli: 20, BaseDecayMilli: 40, DecayPerDeficitUnitMilli: 1,
            DecayScaleDivisor: 5, MaxDecayMilli: 300, AbandonmentHorizonTurns: 3),
        LegionSupply: new LoamLegionSupplyTuning(CarryPerBearer: 200, BurnPerMember: 10),
        Structures: new LoamStructuresTuning(
            WellYieldMultiplierMilli: 2000, WellCostMilli: 200, WaystationCostMilli: 300,
            WellBuildTurns: 2, WaystationBuildTurns: 4, WaystationRangeHops: 3,
            GranaryCostMilli: 150, GranaryCapacityBonus: 300, GranaryBuildTurns: 2),
        Texture: new LoamTextureTuning(
            ContagionPressurePerTurn: 60, MaxPressureMilli: 300, PressureDecayPerTurn: 40,
            SurgeDecayMultiplierMilli: 1500, UnmadeSpawnAfterTurns: 5,
            UnmadeMemberHp: 120, UnmadeMemberCount: 2));

    public static readonly WorldTuning DefaultWorld = new(
        SchemaVersion: 1,
        Version: 1,
        LaneCostMultiplierMilli: new Dictionary<string, int>
        {
            ["rift"] = 1000, ["corridor"] = 700, ["ley"] = 900,
            ["deep"] = 1600, ["one-way"] = 800, ["gated"] = 1000,
        },
        WorldSizeNodes: new Dictionary<string, WorldSizeNodeRange>
        {
            ["small"] = new(6, 10), ["medium"] = new(14, 18), ["large"] = new(28, 36),
            ["huge"] = new(56, 72), ["giant"] = new(112, 144),
        },
        StrengthBands: new[]
        {
            new StrengthBandTuning(0, 0, 0),
            new StrengthBandTuning(1, 499, 250),
            new StrengthBandTuning(500, 1_499, 1_000),
            new StrengthBandTuning(1_500, 3_999, 2_750),
            new StrengthBandTuning(4_000, 9_999, 7_000),
            new StrengthBandTuning(10_000, 20_000, 20_000),
        },
        PlaceholderBattle: new PlaceholderBattleTuning(
            DefenderBonusMilli: 1250, WipeoutRatioMilli: 250, RoutWoundMilli: 750, GuardWoundMilli: 100),
        Calendar: new WorldCalendarTuning(
            DaysPerWeek: 7, WeeksPerMonth: 4,
            SpecialWeekChanceMilli: 250, SpecialMonthChanceMilli: 400, PlagueChanceMilli: 100));

    public static readonly SoulEarnTuning DefaultSouls = new(
        SchemaVersion: 1,
        Version: 1,
        Kill: new SoulKillTuning(KillDelta: 1, KillCapPerMatch: 50),
        MatchEnd: new SoulMatchEndTuning(VictoryDelta: 100, VictoryFullPerDay: 3, DefeatDelta: 25),
        DiscoveryDelta: new Dictionary<DemonRarity, int>
        {
            [DemonRarity.Common] = 25,
            [DemonRarity.Rare] = 75,
            [DemonRarity.Epic] = 200,
            [DemonRarity.Legendary] = 500,
        },
        Codex: new SoulCodexTuning(HalfMilestone: 500, FullMilestone: 1500));

    public static readonly PatronTuning DefaultPatron = new(
        SchemaVersion: 1,
        Version: 1,
        SwitchCostSouls: 100,
        AuraClampMilli: 150,
        PerStarMilli: 10,
        KillSoulCap: 50,
        RarityBaseMilli: new Dictionary<DemonRarity, int>
        {
            [DemonRarity.Common] = 20,
            [DemonRarity.Rare] = 30,
            [DemonRarity.Epic] = 45,
            [DemonRarity.Legendary] = 60,
        });

    public static readonly ShieldTuning DefaultShield = new(
        SchemaVersion: 1,
        Version: 1,
        MatchupShareKPm: 250,
        ChipFloorKPm: 100,
        PenCapKPm: 3000,
        MaxShieldsPerActor: 3,
        DrainPriority: new ShieldDrainPriorityTuning(Aura: 30, Skill: 20, Innate: 10));

    public static readonly CombatTuning DefaultCombat = new(
        SchemaVersion: 1,
        Version: 1,
        ProcDepthLimit: 6,
        DefaultMaxTargets: 8,
        AreaDefaultSquareSize: 3,
        AreaDefaultRectangleWidth: 3,
        AreaDefaultRectangleHeight: 3,
        DotDefaultPeriodMs: 1000,
        DotDefaultDurationMs: 5000);

    public static readonly FusionTuning DefaultFusion = new(
        SchemaVersion: 1,
        Version: 1,
        PerStarPowerMilli: 30,
        PerStarDefenseMilli: 30,
        StarCap: new Dictionary<DemonRarity, int>
        {
            [DemonRarity.Common] = 3,
            [DemonRarity.Rare] = 4,
            [DemonRarity.Epic] = 5,
            [DemonRarity.Legendary] = 5,
        },
        StarMergeCost: new FusionCostTuning(Souls: 50, ShardCount: 1, EssenceCount: 1),
        PromotionCost: new FusionCostTuning(Souls: 200, ShardCount: 3, EssenceCount: 3),
        RecipeCost: new Dictionary<DemonRarity, RecipeCostTuning>
        {
            [DemonRarity.Rare] = new(Souls: 150, ShardRarity: DemonRarity.Common, ShardCount: 2, EssenceCount: 2),
            [DemonRarity.Epic] = new(Souls: 400, ShardRarity: DemonRarity.Rare, ShardCount: 3, EssenceCount: 4),
            [DemonRarity.Legendary] = new(Souls: 1000, ShardRarity: DemonRarity.Epic, ShardCount: 4, EssenceCount: 8),
        });

    public static readonly StatusTuning DefaultStatus = new(
        SchemaVersion: 1,
        Version: 1,
        CategoryResistCap: 0.95,
        ApplyScaleK: 100.0,
        ApplyScaleFloor: 1.0,
        ResistFromPowerRatio: 0.0,
        MinNetFactor: 0.0,
        MaxNetFactor: 10_000.0,
        ProgressionPowerStubDefault: 1.0,
        ProcDepthLimitDefault: 6,
        ApplySteepnessDefault: 1.0);

    public static readonly OverlayTuning DefaultOverlay = new(
        SchemaVersion: 1,
        Version: 1,
        Pause: new OverlayPauseTuning(PausedTimeScale: 0f, MaxResumeScale: 10f),
        SwitchLayout: new OverlaySwitchLayoutTuning(
            BaseButtonW: 72f, BaseButtonH: 28f, BaseMargin: 16f, ReferenceHeight: 1080f,
            MinScale: 1f, MaxScale: 3f),
        SwitchState: new OverlaySwitchStateTuning(DebounceMs: 300, ProbeIntervalMs: 30_000, SendTimeoutMs: 3_000));

    public static readonly StatsTuning DefaultStats = new(
        SchemaVersion: 1,
        Version: 1,
        MinimumInterval: 0.01,
        MatchupShareK: 0.25,
        AccuracyScale: 100.0,
        CritRateScale: 100.0,
        CritDamageScale: 100.0,
        Steepness: 1.0);

    public static readonly ExpeditionTuning DefaultExpeditions = new(
        SchemaVersion: 1,
        Version: 1,
        Tiers: new Dictionary<string, ExpeditionTierNumbers>
        {
            ["scout-30m"] = new(DurationMinutes: 30, TickCount: 6, BattleCount: 1, SquadSlots: 2),
            ["forage-4h"] = new(DurationMinutes: 240, TickCount: 8, BattleCount: 2, SquadSlots: 3),
            ["hunt-8h"] = new(DurationMinutes: 480, TickCount: 8, BattleCount: 3, SquadSlots: 4),
            ["warpath-20h"] = new(DurationMinutes: 1200, TickCount: 10, BattleCount: 4, SquadSlots: 5),
        },
        EventRoll: new ExpeditionEventRollTuning(
            QuietCeilMilli: 400, FoundSoulsCeilMilli: 750, WildCeilMilli: 900, WildJoinMilli: 250,
            ShinyDie: 64, InjuryPowerDivisor: 4));
}
