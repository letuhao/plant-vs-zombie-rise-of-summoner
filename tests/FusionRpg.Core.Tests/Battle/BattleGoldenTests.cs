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
    // RulesetVersion 2 hashes (bMilli=0) remain decodable by their stamp for history.
    //
    // Re-blessed 2026-08-24 at RulesetVersion 3 (T4.2, power-dial: power-scale.v2.json's
    // bMilli 0 -> 400 — the ONE golden-moving change the whole power program was built around).
    // Every actor here sits away from the Theta=20 pin (levels 1/2/5/6/10), so every magnitude
    // legitimately moved; nothing here is a rate, so PS-3 does not apply to these hashes at all.
    // Triaged BEFORE this re-bless, not after: the full CORE suite's only failures were these
    // hash goldens, the two literal RulesetVersion==2 assertions, and the three B=0-specific
    // BattleMagnitudeParityTests (reframed separately, not re-blessed) — every rate-specific
    // test (RateParityTests.cs, BattleAdoptionTests.cs's BattleRateTests) stayed green with zero
    // changes, confirming zero rate goldens moved. Golden_outcomes_hold_their_shapes (Victory/
    // Defeat/retreat) also stayed green, unchanged — the shapes held, only the numbers moved.
    const string StompHash = "A9B076C2B8C4D1AEA629C2FE20C8E3A706AA8BB05BA775925902FD78B93E76C9";
    const string CloseHash = "DEE290C1E84D57B150D2650043B538949220CFDC267DB42763BB7BD572902F5A";
    const string WipeHash = "8BD6365E32BEC3E73733147916611FE5A21AB4926D53F490EDB6592ED361C530";
    const string SeedSweepHash = "9D8F88A2B1D98E4E71F927AF9A43A2E77CF843BF98337ED4223511721B673890";

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
    /// <summary>
    /// The determinism hash. <b>Two provenance fields are blanked</b>: the platform stamp, because
    /// `Math.Exp` is not bit-identical across architectures, and the content hash (E12), because it
    /// records which content was consulted rather than what the engine computed.
    ///
    /// <para>Folding either in makes the goldens move for a reason that is not a determinism break —
    /// the platform stamp made them green only on the machine that blessed them, and the content
    /// hash would make every added row look like one.</para>
    /// </summary>
    static string Hash(BattleReport report)
    {
        var json = JsonSerializer.Serialize(report with { EnvironmentStamp = "", ContentHash = null });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Goldens_do_not_depend_on_the_content_stamp()
    {
        // E12 stamps the report with the content that produced it. If that reached the hash input,
        // adding one item row would move every battle golden and a real determinism break would be
        // indistinguishable from an author doing their job.
        var report = BattleEngine.Resolve(StompSetup(), 1001) with { ContentHash = "v4|abc|x=1" };

        Assert.Equal(Hash(report), Hash(report with { ContentHash = "v4|totally-different|y=2" }));
        Assert.Equal("v4|abc|x=1", report.ContentHash); // and it still reaches the report
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
