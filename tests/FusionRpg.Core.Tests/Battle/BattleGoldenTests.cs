using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Battle;

/// <summary>
/// C3b: the blessed goldens (after ALL of C2 — trait/status/funnel semantics locked). Three
/// canonical battles + a 32-seed hash sweep. A diff here is a determinism break or a balance
/// change and MUST be a conscious RulesetVersion/EngineVersion bump, never a silent re-bless.
/// </summary>
public class BattleGoldenTests
{
    // Re-blessed 2026-08-21 at RulesetVersion 2 (combat-unification battle-adoption): the SSOT
    // resolver + re-tuned baselines + shields + report shape changed every byte, ONCE, by
    // design. Predicted-delta review: outcomes/shapes held (stomp Victory, wipe Defeat with
    // the coward retreating — verified pre-bless); mirror-match symmetry 48–56%.
    // v1 hashes (EngineVersion 1, RulesetVersion 1) remain decodable by their stamps.
    //
    // Re-blessed AGAIN, same day, for one reason: the hash input no longer includes the
    // platform stamp (see Hash). The previous values had baked in "X64/.NET 8.0.30", so they
    // were only ever green on the machine that blessed them — CI or any teammate would have
    // read a portability failure as a determinism break. Battle MATH did not move: the only
    // failures in the re-bless run were these two hash tests, while every shape, rate, shield,
    // and expedition test stayed green with no seed re-selection.
    const string StompHash = "CDCE257B7257186D3E56845ABA0DC38873A218816095D52217728ADE64D74B1D";
    const string CloseHash = "C1ACC76319EA0A4CE9FA653936DB57324117410E7E0E5DD712A469740266E5E0";
    const string WipeHash = "54860127B4D05F4F5310E271AD1855ACDE97F66716FBA1DA3E7D32BD59F26B14";
    const string SeedSweepHash = "CF42E75B71E3845593133E2DE99DCC39C3869554621179D47A6C115A4268198D";

    static BattleActorSetup Actor(string key, string side, int level,
        ElementTypeId? elem = null, params string[] traits) => new()
    {
        Key = key,
        Side = side,
        SpeciesId = "golden-species",
        TypeId = 10_001,
        Level = level,
        ElementPrimary = elem,
        TraitIds = traits,
        MaxHp = BattleRuleset.BaseHp(level),
        Atk = BattleRuleset.BaseAtk(level),
        Defense = BattleRuleset.BaseDefense(level)
    };

    /// <summary>Overleveled elemental squad stomps a small wave.</summary>
    internal static BattleSetup StompSetup() => new()
    {
        WaveId = "golden-stomp",
        Squad = new[]
        {
            Actor("squad:0", "squad", 10, ElementTypeId.Fire, "berserker", "swift"),
            Actor("squad:1", "squad", 10, ElementTypeId.Light, "critical-hunter")
        },
        Wave = new[]
        {
            Actor("wave:0", "wave", 2, ElementTypeId.Ice),
            Actor("wave:1", "wave", 2, ElementTypeId.Earth),
            Actor("wave:2", "wave", 2)
        }
    };

    /// <summary>Even match exercising guardian/loyal/regenerator vs bloodthirsty/soul-eater.</summary>
    internal static BattleSetup CloseSetup() => new()
    {
        WaveId = "golden-close",
        Squad = new[]
        {
            Actor("squad:0", "squad", 5, ElementTypeId.Air, "regenerator"),
            Actor("squad:1", "squad", 5, ElementTypeId.Earth, "guardian", "loyal")
        },
        Wave = new[]
        {
            Actor("wave:0", "wave", 5, ElementTypeId.Dark, "bloodthirsty"),
            Actor("wave:1", "wave", 5, ElementTypeId.Fire, "soul-eater")
        }
    };

    /// <summary>Hopeless squad wiped by an elite wave; the coward walks away.</summary>
    internal static BattleSetup WipeSetup() => new()
    {
        WaveId = "golden-wipe",
        Squad = new[]
        {
            Actor("squad:0", "squad", 1, null, "coward"),
            Actor("squad:1", "squad", 1, ElementTypeId.Ice)
        },
        Wave = new[]
        {
            Actor("wave:0", "wave", 6, ElementTypeId.Fire, "void-touched"),
            Actor("wave:1", "wave", 6, ElementTypeId.Dark, "immortal")
        }
    };

    /// <summary>
    /// Hashes the report with the platform stamp blanked. The stamp is a property of the
    /// MACHINE, not of the battle — leaving it in would bind every golden to whatever
    /// architecture/OS/runtime blessed it, so a CI runner or a `dotnet` upgrade would fire the
    /// "determinism break" alarm for a non-reason. Locked by Goldens_do_not_depend_on_the_platform.
    /// </summary>
    static string Hash(BattleReport report)
    {
        var json = JsonSerializer.Serialize(report with { EnvironmentStamp = "" });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Goldens_do_not_depend_on_the_platform()
    {
        // The hash input must not carry the machine stamp — that is what makes these hashes
        // portable across CI, teammates, and runtime patches.
        var report = BattleEngine.Resolve(StompSetup(), 1001);
        var hashed = JsonSerializer.Serialize(report with { EnvironmentStamp = "" });
        Assert.DoesNotContain(BattleEnvironment.Stamp, hashed);

        // ...and a report that differs ONLY by stamp must hash identically.
        Assert.Equal(Hash(report), Hash(report with { EnvironmentStamp = "X64/other/net99" }));

        // The stamp still reaches the report itself — this decouples the golden, not the guard.
        Assert.Equal(BattleEnvironment.Stamp, report.EnvironmentStamp);
    }

    [Fact]
    public void Golden_battles_are_locked()
    {
        var actual =
            $"stomp:{Hash(BattleEngine.Resolve(StompSetup(), 1001))}\n" +
            $"close:{Hash(BattleEngine.Resolve(CloseSetup(), 2002))}\n" +
            $"wipe:{Hash(BattleEngine.Resolve(WipeSetup(), 3003))}";
        var expected = $"stomp:{StompHash}\nclose:{CloseHash}\nwipe:{WipeHash}";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Golden_outcomes_hold_their_shapes()
    {
        Assert.Equal(BattleOutcome.Victory, BattleEngine.Resolve(StompSetup(), 1001).Outcome);
        var wipe = BattleEngine.Resolve(WipeSetup(), 3003);
        Assert.Equal(BattleOutcome.Defeat, wipe.Outcome);
        Assert.True(wipe.Actors.Single(a => a.Key == "squad:0").Retreated, "the golden coward must walk away");
    }

    [Fact]
    public void Thirty_two_seed_sweep_is_locked()
    {
        var sb = new StringBuilder();
        for (ulong seed = 0; seed < 32; seed++)
            sb.Append(Hash(BattleEngine.Resolve(CloseSetup(), seed)));
        var aggregate = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
        Assert.Equal(SeedSweepHash, aggregate);
    }
}
