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

/// <summary>POC level→power curve — ADR P1.</summary>
public static class ProgressionPowerCurve
{
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
