using System.Runtime.CompilerServices;
using FusionRpg.Core.Actions.Rungs;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Server.Tests;

/// <summary>
/// tunables-ssot.md §7.2: "Tests → construct one inline; no fixture files." Mirrors
/// `FusionRpg.Core.Tests`' `ContractTuningTestBootstrap` (a different, internal class in a different
/// assembly, so not directly reusable) — just the hubs this assembly's tests actually touch today
/// (class-system-todo.md P2.5, <c>WebMatchService.AptitudeChannelMods</c>, which transitively needs
/// <see cref="DerivedStatPolicy"/> too: <c>DerivedStatRegistry.CreateDefault()</c> reads
/// <see cref="DerivedStatPolicy.CategoryResistCap"/> in its constructor; T22 (action-todo.md) added
/// <see cref="RungPolicy"/> — <c>WebMatchService.BuildSquad</c> now resolves each specimen's equipped
/// action set via <c>RpgStore.GetLoadoutOrAutoEquip</c>, which ranks candidates through
/// <c>RungPolicy.Table</c>).
/// </summary>
internal static class PowerAndAptitudeTuningTestBootstrap
{
    [ModuleInitializer]
    public static void Init()
    {
        PowerTuningHub.Configure(DefaultPower);
        AptitudeTuningHub.Configure(DefaultAptitudes);
        DerivedStatPolicy.Configure(DefaultDerivedStats);
        RungPolicy.Configure(DefaultRungs);
    }

    public static readonly DerivedStatTuning DefaultDerivedStats = new(
        SchemaVersion: 1, Version: 1, CategoryResistCap: 0.95);

    // Minimal, hand-authored -- not the shipped data/tuning/action-rungs.v1.json (tunables-ssot.md's
    // "construct one inline" convention). Every specimen in this assembly's tests holds zero action
    // grants today (T22's own note: no production caller grants actions to a demon instance yet), so
    // AutoEquip.Select never actually ranks a real candidate against this table -- it only needs to be
    // a STRUCTURALLY valid one-rung table so RungPolicy.Table does not throw "not configured".
    public static readonly RungTable DefaultRungs = new(
        cap: 1, rows: new[] { new RungRow(1, 1, 1, 1, 1000, 1000, 1000, Array.Empty<string>()) });

    // Same working values as data/tuning/power-scale.v2.json (T4.2: bMilli=400) — the shipped dial,
    // not the historical bMilli=0 baseline ContractTuningTestBootstrap pins for its own reasons.
    // cMilli/pinIndex/pinValue are literal here, not PowerTuning.FixedCMilli/FixedPinIndex/FixedPinValue
    // (mirrors them exactly) -- those are `internal`, visible only to FusionRpg.Core.Tests
    // (FusionRpg.Core.csproj's one InternalsVisibleTo grant), not this assembly.
    public static readonly PowerTuning DefaultPower = PowerTuning.Build(
        schemaVersion: 1, version: 2,
        cMilli: 80_000, bMilli: 400, pinIndex: 20, pinValue: 680,
        wdMilli: 1000, waMilli: 25000, wrMilli: 250, wzMilli: 1000, wmMilli: 5000, wwMilli: 5000, wfMilli: 25000,
        channels: new Dictionary<string, PowerChannelTuning>
        {
            ["atk"] = new PowerChannelTuning(CMilli: 12_000, PinValue: 92),
            ["defense"] = new PowerChannelTuning(CMilli: 2_000, PinValue: 22),
        });

    // Minimal, hand-authored -- not the 486-edge shipped file (tunables-ssot.md's "construct one
    // inline" convention). Enough to exercise AptitudeChannelMods' plumbing; the real coefficients are
    // proven elsewhere (AptitudeTuningTests.ParsesTheShippedFile in FusionRpg.Core.Tests).
    public static readonly AptitudeTuning DefaultAptitudes = AptitudeTuningLoader.Parse("""
        {
          "schemaVersion": 1, "version": 1,
          "grant": { "aptitudePointsPerTheta": 3, "skillPointsPerTheta": 1 },
          "pointEconomy": { "aptitudePointsPerThetaMilliByScope": { "commander": 3, "demonType": 4, "aspect": 4, "uniqueDemon": 6 }, "respecPrice": 10 }, "guardEconomy": { "flatCommitCost": 50, "absorbDrainSharePermille": 300, "riposteShareCapPermille": 400 }, "mitigation": { "scaleMilli": 1000, "families": ["combat.defense", "combat.dodge", "combat.parry", "combat.block", "combat.absorption", "combat.heal"] },
          "read": { "contest": { "spanPoints": 100.0, "shareExponentMilli": 1000 }, "magnitude": { "shareExponentMilli": 1000 } },
          "recovery": { "scaleMilli": 374, "targetRecoveryShareMilli": 670, "families": ["resource.regen"] },
          "familyRead": { "combat.power": "magnitude" },
          "edges": [ { "channel": "combat.power.omni", "source": "Might", "kMilli": 2200 } ]
        }
        """);
}
