using FusionRpg.Core.Status;
using FusionRpg.Core.Stats;

namespace FusionRpg.Core.Stats.Derived;

/// <summary>Hot progression read — Core stays DB-free; injector/server hydrate levels.</summary>
public interface IProgressionPowerProvider
{
    int GetLevel(StatContext ctx);
    double GetPower(StatContext ctx);
    double GetRealm(StatContext ctx);
}

/// <summary>POC level→power curve — ADR P1, retired (decisions.md, power/ssot-power-scale.md §6.0/§9).
/// Not a live tunable: the only caller reaching MaxExponent is InjectorProgressionPowerProvider.GetPower,
/// whose GetLevel always returns 0 (SetLevel has zero callers), so PowerFromLevel always short-circuits
/// to 1.0 and Math.Min(level, MaxExponent) never runs. Migrating this constant to config would tune a
/// value nothing reads; power-plan.md T3.2/T3.3 deletes this whole class once Phase 3 is authorized.</summary>
public static class ProgressionPowerCurve
{
    // Retired, provably unreachable — see the class doc above.
    public const int MaxExponent = 12;

    public static double PowerFromLevel(int level) =>
        level <= 0 ? 1.0 : Math.Pow(2, Math.Min(level, MaxExponent));
}

public sealed class StubProgressionPowerProvider : IProgressionPowerProvider
{
    public int GetLevel(StatContext ctx) => 0;
    public double GetPower(StatContext ctx) => StatusPolicy.ProgressionPowerStubDefault;
    public double GetRealm(StatContext ctx) => StatusPolicy.ProgressionPowerStubDefault;
}
