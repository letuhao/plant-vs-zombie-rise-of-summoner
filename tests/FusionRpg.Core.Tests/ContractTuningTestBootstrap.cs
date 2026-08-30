using System.Runtime.CompilerServices;
using FusionRpg.Core;
using FusionRpg.Core.Actions.Rungs;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Combat.Shield;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Contracts;
using FusionRpg.Core.Demons.Fusion;
using FusionRpg.Core.Demons.Patron;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Expeditions;
using FusionRpg.Core.Match;
using FusionRpg.Core.Overlay;
using FusionRpg.Core.Power;
using FusionRpg.Core.Progression;
using FusionRpg.Core.Status;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Vfx;
using FusionRpg.Core.World;
using FusionRpg.Core.World.Ai;
using FusionRpg.Core.World.Loam;

namespace FusionRpg.Core.Tests;

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
        DerivedStatPolicy.Configure(DefaultDerivedStats);
        OverlayTuningHub.Configure(DefaultOverlay);
        StatsTuningHub.Configure(DefaultStats);
        ExpeditionTuningHub.Configure(DefaultExpeditions);
        MatchTuningPolicy.Configure(DefaultMatch);
        EffectsTuningHub.Configure(DefaultEffects);
        SimDefaults.Configure(DefaultSim);
        ProgressionTuningHub.Configure(DefaultProgression);
        BattleTuningHub.Configure(DefaultBattle);
        SummoningTuningHub.Configure(DefaultSummoning);
        WorldAiPolicy.Configure(DefaultAi);
        VfxTuningHub.Configure(DefaultVfx);
        PowerTuningHub.Configure(DefaultPower);
        RungPolicy.Configure(DefaultActionRungs);
    }

    public static readonly ContractTuning DefaultContracts = new(
        SchemaVersion: 1,
        Version: 1,
        Loyalty: new ContractLoyaltyTuning(
            Max: 1000, DeployFloor: 200, BindLoyalty: 300,
            SwornThreshold: 400, TrustedThreshold: 600, DevotedThreshold: 800,
            WinGain: 15, LossPenalty: 10, DailyGainCap: 60, DecayPerDay: 25, RitualGain: 100,
            RankBonusSwornMilli: 15, RankBonusTrustedMilli: 35, RankBonusDevotedMilli: 60),
        Slots: new ContractSlotsTuning(BaseSlots: 12, SlotPriceStep: 300),
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
        Kill: new SoulKillTuning(KillDelta: 1),
        MatchEnd: new SoulMatchEndTuning(VictoryDelta: 100, DefeatDelta: 25),
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
        PThetaKMilli: 220, // matches the real shipped patron.v1.json (aura-skill T22)
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
        DotDefaultDurationMs: 5000,
        PierceScale: 10.0,
        AmpScale: 10.0,
        BlockCapPermille: 950,
        ParryCapPermille: 950,
        AvoidanceBandCapPermille: 950,
        ReflectRateScale: 10.0,
        ReflectShareScale: 100.0,
        ParryNeutralShareKPm: 500,
        DefenseShape: DefenseShape.Divisive,
        DefenseDivisorK: 0.45,
        ReflectReadsPostShield: true,
        AmpShape: AmpShape.Reciprocal);

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

    public static readonly DerivedStatTuning DefaultDerivedStats = new(
        SchemaVersion: 1, Version: 1, CategoryResistCap: 0.95);

    public static readonly StatusTuning DefaultStatus = new(
        SchemaVersion: 1,
        Version: 1,
        ApplyScaleK: 100.0,
        ApplyScaleFloor: 1.0,
        ResistFromPowerRatio: 1.0, // T3.1 (power-plan.md, done 2026-08-24): 0 -> 1.0, matched pair contests at delta=0
        MinNetFactor: 0.0,
        MaxNetFactor: 10_000.0,
        NetFactorScale: 10.0, // T3.2 (power-plan.md, done 2026-08-24): netFactor = 1 + delta/NetFactorScale (audit F4)
        ProgressionPowerStubDefault: 1.0,
        ProcDepthLimitDefault: 6,
        ApplySteepnessDefault: 1.0,
        ApplyShape: StatusApplyShape.Sigmoid,
        ApplyOffsetK: 0.0);

    public static readonly OverlayTuning DefaultOverlay = new(
        SchemaVersion: 1,
        Version: 1,
        Pause: new OverlayPauseTuning(PausedTimeScale: 0f, MaxResumeScale: 10f),
        SwitchLayout: new OverlaySwitchLayoutTuning(
            BaseButtonW: 72f, BaseButtonH: 28f, BaseMargin: 16f, ReferenceHeight: 1080f,
            MinScale: 1f, MaxScale: 3f),
        SwitchState: new OverlaySwitchStateTuning(DebounceMs: 300, ProbeIntervalMs: 30_000, SendTimeoutMs: 3_000),
        SettingsGui: new OverlaySettingsGuiTuning(PanelW: 280f, PanelH: 196f));

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

    public static readonly MatchTuning DefaultMatch = new(
        SchemaVersion: 1, Version: 1, MaxLivingPlants: 50, MaxLivingZombies: 80);

    public static readonly EffectsTuning DefaultEffects = new(
        SchemaVersion: 1, Version: 1,
        MatchupReadSlotShareMilli: 250,
        DamageFxFloater: new DamageFxFloaterTuning(Cap: 64, LifeSeconds: 0.9, RisePixels: 56));

    public static readonly SimTuning DefaultSim = new(
        SchemaVersion: 1, Version: 1,
        PlantHp: 300, PlantAttack: 20, ZombieHp: 270, ZombieAttack: 50, HitDamage: 50);

    public static readonly ProgressionTuning DefaultProgression = new(
        SchemaVersion: 1, Version: 1,
        PlantCurve: new XpCurveParams(80.0, 32.0),
        ZombieCurve: new XpCurveParams(70.0, 28.0),
        PlayerCurve: new XpCurveParams(100.0, 45.0),
        Awards: new XpAwardsTuning(Kill: 12, Defeat: -100, Mower: -30, PlantPlace: 8, ZombieSpawn: 9));

    public static readonly BattleTuning DefaultBattle = new(
        SchemaVersion: 1, Version: 1,
        RoundDurationMs: 1000, MaxRounds: 50,
        PrimaryAffinityDivisor: 4, SecondaryAffinityDivisor: 8,
        Traits: new Dictionary<string, TraitMagnitudes>(StringComparer.Ordinal)
        {
            ["berserker"] = new(BerserkRampHalfMilli: 250, BerserkRampQuarterMilli: 500),
            ["regenerator"] = new(RegenPerRoundMilli: 20),
            ["soul-eater"] = new(OnKillHealMilli: 100),
            ["guardian"] = new(GuardShareMilli: 250),
            ["swift"] = new(InitiativeBonusMilli: 1000),
            ["immortal"] = new(DeathRefusalCharges: 1),
            ["coward"] = new(RetreatBelowMilli: 250),
            ["greedy"] = new(SoulLootBonusMilli: 250),
            ["genius"] = new(SpecimenXpBonusMilli: 250),
            ["void-touched"] = new(EssenceProcMilli: 100, EssenceRiderMilli: 150),
            ["chaos-marked"] = new(EssenceProcMilli: 100, EssenceRiderMilli: 150),
        });

    public static readonly SummoningTuning DefaultSummoning = new(
        SchemaVersion: 1, Version: 1,
        Banners: new Dictionary<string, BannerTuning>(StringComparer.Ordinal)
        {
            ["standard-rift"] = new(CostPerPull: 100, CostPerTen: 900, FocusWeightMultiplier: 1.0),
            ["element-focus"] = new(CostPerPull: 120, CostPerTen: 1080, FocusWeightMultiplier: 3.0),
        },
        Roller: new RollerTuning(
            EpicHardPity: 25, LegendarySoftStart: 41, LegendaryHardPity: 55,
            LegendaryBasePerMille: 10, LegendaryRampPerMille: 60, EpicPerMille: 50,
            RarePerMille: 200, ShinyOneIn: 64));

    public static readonly WorldAiTuning DefaultAi = new(
        SchemaVersion: 1, Version: 1,
        FrontierRules: new FrontierRulesTuning(RecoverAtMilli: 400, ExploreTurns: 3, SeveranceThresholdCost: 10_000),
        ThreatMap: new ThreatMapTuning(StaleDecayPerTurn: 150, MaxSpreadHops: 4, ProximityFalloffPerHop: 400),
        ValueMap: new ValueMapTuning(
            OptimismMilli: 700, OverextensionPenaltyMilli: 1400, HabitabilityPenaltyMilli: 1400,
            DefaultWeights: new ValueWeightsTuning(
                Yield: 1000, Strategic: 800, Defensibility: 500, Cost: 700, Risk: 900, Curiosity: 600)));

    public static readonly VfxTuning DefaultVfx = new(
        SchemaVersion: 1, Version: 1,
        TintMaxStrength: 0.35,
        BurstConeHalfAngle: 0.6, BurstRisingSideFactor: 0.4, BurstDirectionalSideFactor: 0.5,
        Rules: new VfxRulesTuning(
            BurstCap: 24, BurstLifeSeconds: 0.55, FloaterRateLimitSeconds: 0.05, BurstRateLimitSeconds: 0.15,
            GlobalCuePerTickCap: 32, CueQueueCap: 256, CritFontScale: 1.25, CritPopStartScale: 1.5,
            CritPopSettleT: 0.3, AmountTierSmallBelow: 50, AmountTierBigFrom: 200,
            AmountTierSmallScale: 0.9, AmountTierBigScale: 1.15),
        Sustained: new VfxSustainedTuning(
            GlobalCap: 24, PerHostCap: 2, TtlGraceSeconds: 2.0, InfiniteTtlSeconds: 60.0,
            AuraPulseSeconds: 0.3, AuraMaxParticles: 6, SpanScale: 1.5),
        Render: new VfxRenderTuning(
            BurstParticles: 28, ParticleSortingOrder: 80, SortOffsetAboveUnit: 0,
            ParticleTextureSize: 64, MarkerEdgeSoftness: 0.1,
            ShieldBar: new VfxShieldBarTuning(
                BarWorldWidth: 0.95, BarWorldHeight: 0.12, WorldYOffset: -0.35,
                MaxSegments: 3, Cap: 32, MaxPips: 3),
            TintReassertSeconds: 0.25),
        Identity: new VfxIdentityTuning(SimilarRgbDistanceThreshold: 45, SimilarApplyRgbDistanceThreshold: 35));

    // bMilli=0 matches the shipped power-scale.v1.json exactly (plan.md "B=0 first, dial second") —
    // BattleRuleset.BaseHp/Atk/Defense must stay byte-identical to their pre-T2.1 literal formulas
    // for every test in this assembly, the same zero-golden-movement guarantee production gets.
    // Matches data/tuning/action-rungs.v1.json exactly (spec-rung-table.md, A12).
    public static readonly RungTable DefaultActionRungs = new(10, new[]
    {
        new RungRow(1,  1, 1, 1, 1000,  1000,  1000, Array.Empty<string>()),
        new RungRow(2,  1, 1, 1, 1323,  1380,  1150, Array.Empty<string>()),
        new RungRow(3,  2, 2, 1, 1750,  1904,  1322, new[] { "scopeSplit", "riderStatus" }),
        new RungRow(4,  2, 2, 1, 2315,  2628,  1521, new[] { "scopeSplit", "riderStatus" }),
        new RungRow(5,  3, 3, 2, 3062,  3627,  1749, new[] { "scopeSplit", "riderStatus", "condition" }),
        new RungRow(6,  3, 3, 2, 4051,  5005,  2011, new[] { "scopeSplit", "riderStatus", "condition" }),
        new RungRow(7,  4, 4, 2, 5359,  6907,  2313, new[] { "scopeSplit", "riderStatus", "condition", "sequence", "consumption" }),
        new RungRow(8,  4, 4, 2, 7090,  9531,  2660, new[] { "scopeSplit", "riderStatus", "condition", "sequence", "consumption" }),
        new RungRow(9,  5, 5, 3, 9379,  13153, 3059, new[] { "scopeSplit", "riderStatus", "condition", "sequence", "consumption", "reaction", "restriction" }),
        new RungRow(10, 5, 5, 3, 12407, 18151, 3518, new[] { "scopeSplit", "riderStatus", "condition", "sequence", "consumption", "reaction", "restriction" }),
    });

    public static readonly PowerTuning DefaultPower = PowerTuning.Build(
        schemaVersion: 1, version: 1,
        cMilli: PowerTuning.FixedCMilli, bMilli: 400, pinIndex: PowerTuning.FixedPinIndex, pinValue: PowerTuning.FixedPinValue, // T4.2 (power-dial, 2026-08-24): 0 -> 400, matches shipped power-scale.v2.json
        wdMilli: 1000, waMilli: 25000, wrMilli: 250, wzMilli: 1000, wmMilli: 5000, wwMilli: 5000, wfMilli: 25000,
        channels: new Dictionary<string, PowerChannelTuning>
        {
            ["atk"] = new PowerChannelTuning(CMilli: 12_000, PinValue: 92),
            ["defense"] = new PowerChannelTuning(CMilli: 2_000, PinValue: 22),
        });
}
