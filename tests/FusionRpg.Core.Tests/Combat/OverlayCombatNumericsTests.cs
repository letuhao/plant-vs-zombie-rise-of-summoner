using System.Linq;
using System.Text.RegularExpressions;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Combat;

/// <summary>
/// lawn-combat-wire T4 (<c>combat-numerics</c>): the overlay damage path was <c>double</c> throughout,
/// divided by 1000 before multiplying, exited on an unchecked long-narrowing cast, and silently
/// saturated at the Unity boundary. This file proves the fixed shape:
/// <list type="bullet">
/// <item>overflow throws rather than wrapping (<see cref="Compute_throws_on_a_magnitude_past_long_range"/>);</item>
/// <item>the parry/block neutral-share arithmetic multiplies before it divides, and that ordering is
/// not cosmetic (<see cref="DivideFirst_and_divideLast_can_round_to_different_longs"/>,
/// <see cref="TheShippedCode_multipliesBeforeItDivides_forTheNeutralShare"/>);</item>
/// <item>D1 (folded into this task's acceptance): <c>ActorHub.MergeAppliedCombat</c> still folds ONLY
/// <c>progression.bonus.*</c>, never a <c>combat.*</c> channel
/// (<see cref="MergeAppliedCombat_ignores_a_combat_channel"/>);</item>
/// <item>a documented, closed allowlist of the <c>double</c>/<c>float</c> that legitimately remain in
/// each file, and everything outside it (<see cref="OverlayCombatMath_hasNoDoubleOrFloatInCode"/>,
/// <see cref="ElementHub_doubleFloat_isLimitedToTheInterfaceBoundary"/>,
/// <see cref="OverlayCombatCalculator_doubleFloat_isLimitedToTheDocumentedAllowlist"/>).</item>
/// </list>
///
/// <para><b>Why an allowlist, not a bare "no double/float remains" assertion.</b> This task's own
/// acceptance criterion asks for the latter, verbatim. Read against the actual code, that absolute
/// claim does not hold for <c>OverlayCombatCalculator.cs</c> and <c>ElementHub.cs</c> — see each file's
/// own class-doc comment for the full citation trail (in short: <c>IElementHub.cs</c> fixes
/// <c>ElementHub</c>'s public methods to <c>double</c>; <c>CombatDerivedReader.cs</c> /
/// <c>CombatPolicy.cs</c> / <c>CombatProbability.cs</c> are <c>double</c>-typed, out-of-scope
/// dependencies with an existing accepted-exception precedent
/// (<c>MitigationChainTests.LongThroughout</c>, <c>audit-overflow.py</c>'s own <c>FLOAT_OK_PATH</c>);
/// and <c>Actions/BasicAttack.cs</c> is a live production caller of
/// <c>OverlayCombatRequest.EffectivenessMultiplier</c>/<c>MultiplierFromPerMille</c>'s <c>double</c>
/// shape). None of those three files are on this task's permitted file list. So instead of a claim
/// that does not survive reading the code, this suite pins the actual, closed set of remaining
/// double/float sites with a named reason for each — which is the guardrail this repo's own hard rule
/// asks for ("validate the CONTRACT ... never a population count or generated text"): a NEW,
/// undocumented double/float anywhere in these files fails this suite immediately.</para>
/// </summary>
public class OverlayCombatNumericsTests
{
    // ── Overflow throws, never wraps ─────────────────────────────────────────────────────────────

    [Fact]
    public void Compute_throws_on_a_magnitude_past_long_range()
    {
        var calc = new OverlayCombatCalculator();
        // Omni fallback (no element components) with an attacker power so large that powerAdjusted /
        // finalDamage lands far past long.MaxValue (~9.22e18) — ForceHit/ForceCrit make the outcome
        // fully deterministic, no RNG draw needed to reach the mitigation math.
        var hugeAttacker = ActorDerivedSnapshot.StubNeutral().Overlay(new[]
        {
            new System.Collections.Generic.KeyValuePair<string, double>(DerivedStatChannels.CombatPowerOmni, 1e30),
        });
        var request = new OverlayCombatRequest
        {
            BaseOverlayDamage = 100,
            Components = System.Array.Empty<ElementPayloadComponent>(),
            Attacker = new CombatActorSnapshot(hugeAttacker, ActorElementTypes.Neutral),
            Defender = new CombatActorSnapshot(ActorDerivedSnapshot.StubNeutral(), ActorElementTypes.Neutral),
            ForceHit = true,
            ForceCrit = false
        };

        Assert.Throws<System.OverflowException>(() => calc.Compute(request, new SeededCombatRng(1)));
    }

    // ── Divide-last, not divide-first ────────────────────────────────────────────────────────────

    [Fact]
    public void DivideFirst_and_divideLast_can_round_to_different_longs()
    {
        // Demonstrates the defect the task named at (the former) OverlayCombatCalculator.cs:252 is
        // real, not cosmetic: for at least one per-mille share, "divide then multiply" and "multiply
        // then divide" round to a DIFFERENT long, even though they are the same expression in exact
        // (real-number) arithmetic. Searched rather than hand-picked, so this does not depend on
        // guessing a specific IEEE rounding boundary.
        const double effectiveBaseDamage = 100.0;
        long? differingShare = null;
        for (long share = 1; share < 1000; share++)
        {
            var divideFirst = (long)System.Math.Round(effectiveBaseDamage * (share / 1000.0), System.MidpointRounding.AwayFromZero);
            var multiplyFirst = (long)System.Math.Round(effectiveBaseDamage * share / 1000.0, System.MidpointRounding.AwayFromZero);
            if (divideFirst != multiplyFirst)
            {
                differingShare = share;
                break;
            }
        }

        Assert.True(differingShare.HasValue,
            "expected at least one per-mille share in [1,999) where divide-first and multiply-first " +
            "round to different longs at baseDamage=100 -- if this ever fails, the two orderings have " +
            "become equivalent for every share and the historical defect this test guards is gone, " +
            "which would need a different base value to keep demonstrating the property, not a weaker assertion");
    }

    [Fact]
    public void TheShippedCode_multipliesBeforeItDivides_forTheNeutralShare()
    {
        // Structural half of the divide-last proof: the SHIPPED source text uses the corrected
        // multiply-then-divide order for the parry/block neutral-share computation, not the
        // divide-then-multiply shape the task named as the defect. Same technique
        // MitigationChainTests.LongThroughout already uses for this exact file.
        var text = ReadCoreFile("Combat", "OverlayCombatCalculator.cs");
        Assert.DoesNotContain("effectiveBaseDamage * (CombatPolicy.Default.ParryNeutralShareKPm / 1000.0)", text, System.StringComparison.Ordinal);
        Assert.Contains("effectiveBaseDamage * CombatPolicy.Default.ParryNeutralShareKPm / 1000.0", text, System.StringComparison.Ordinal);
    }

    // ── D1: MergeAppliedCombat folds progression.bonus.* only, never combat.* ───────────────────────

    [Fact]
    public void MergeAppliedCombat_ignores_a_combat_channel()
    {
        // D1 (lawn-combat-wire): a combat.* channel must never reach AppliedCombat -- that would
        // double-dip RPG power into a single hit, once through OverlayCombatCalculator's own read and
        // once through the Unity-field bridge EntityStatWriter consumes. Reinforces (does not replace)
        // AptitudeMatrixTests.Progression_bonus_is_the_only_edge_family_that_can_reach_a_pvz_unity_field
        // with a minimal, direct repro that does not depend on the shipped aptitude corpus.
        // Fully qualified: this test's own namespace (FusionRpg.Core.Tests.Combat) sits under
        // FusionRpg.Core.Tests, which also contains a sibling namespace literally named "ActorHub"
        // (tests/FusionRpg.Core.Tests/ActorHub/**) -- that enclosing-namespace lookup wins over the
        // `using FusionRpg.Core.Stats.Derived;` class import, so the bare name resolves to the
        // namespace, not the class ("'ActorHub' is a namespace but is used like a type").
        var hub = new FusionRpg.Core.Stats.Derived.ActorHub(StatSystemBootstrap.CreateDefault());
        hub.Register(new CombatChannelOnlySubsystem());

        var result = hub.Resolve(new StatContext { Side = StatSide.Plant, TypeId = 1, EntityKey = "0xTEST-D1" });

        Assert.Equal(result.RuntimePrimary.Atk, result.AppliedCombat.Atk);
        Assert.Equal(result.RuntimePrimary.MaxHp, result.AppliedCombat.MaxHp);
        Assert.Equal(result.RuntimePrimary.Hp, result.AppliedCombat.Hp);
        Assert.Equal(result.RuntimePrimary.DefenseFlat, result.AppliedCombat.DefenseFlat);
        Assert.Same(result.RuntimePrimary, result.AppliedCombat); // no bridge channel present -> same instance
    }

    sealed class CombatChannelOnlySubsystem : IActorStatSubsystem
    {
        public string SubsystemId => "test.combatChannelOnly";
        public int Order => 999;
        public void ContributeDerived(StatContext ctx, System.Collections.Generic.ICollection<DerivedModifier> mods) =>
            mods.Add(new DerivedModifier(DerivedStatChannels.CombatPowerOmni, DerivedModifierOp.Flat, 999_999, SourceId: "test"));
    }

    // ── Source scan: the closed allowlist of remaining double/float, per file ───────────────────────

    [Fact]
    public void OverlayCombatMath_hasNoDoubleOrFloatInCode()
    {
        var code = StripComments(ReadCoreFile("Combat", "OverlayCombatMath.cs"));
        AssertNoDoubleOrFloat(code, "OverlayCombatMath.cs");
    }

    [Fact]
    public void ElementHub_doubleFloat_isLimitedToTheInterfaceBoundary()
    {
        var code = StripComments(ReadCoreFile("Combat", "Element", "ElementHub.cs"));
        // IElementHub.cs (out of this task's scope) fixes both method signatures to double in/out --
        // see ElementHub.cs's own class doc for the full citation.
        code = Regex.Replace(code,
            @"public double ResolveComponentBonus\(.*?double baseOverlayDamage\)", "", RegexOptions.Singleline);
        code = Regex.Replace(code,
            @"public double ResolvePayloadBonus\(.*?double baseOverlayDamage\)", "", RegexOptions.Singleline);
        AssertNoDoubleOrFloat(code, "ElementHub.cs (outside the two IElementHub-mandated method signatures)");
    }

    [Fact]
    public void OverlayCombatCalculator_doubleFloat_isLimitedToTheDocumentedAllowlist()
    {
        var code = StripComments(ReadCoreFile("Combat", "OverlayCombatCalculator.cs"));

        // Every remaining double/float code site, verbatim, each with its own reason recorded in the
        // file's own class-doc / member-doc comments:
        //  - EffectivenessMultiplier / MultiplierFromPerMille: a live production caller
        //    (Actions/BasicAttack.cs, out of scope) depends on the double shape.
        //  - "double finalDamage": assigned across three branches -- two produce exact-integral
        //    doubles from long computations, the crit/amp branch is genuinely continuous.
        //  - ResolveBand / CapAvoidanceBand / PierceFactor / AmpFactor / AmpFactorReciprocal /
        //    DivisiveMitigation: the continuous mitigation-chain math, reading double-typed
        //    CombatDerivedReader/CombatPolicy/CombatProbability channels (out of scope, already an
        //    accepted exception -- see the class doc above).
        var allowed = new[]
        {
            "public double EffectivenessMultiplier { get; init; } = 1.0;",
            "public static double MultiplierFromPerMille(long perMille) => 1.0 + perMille / 1000.0;",
            "double finalDamage;",
            "public static (bool Miss, bool Parried, bool Blocked) ResolveBand(double r, double pHitFinal, double pParry, double pBlock)",
            "public static (double Parry, double Block) CapAvoidanceBand(double pHitFinal, double pParryRaw, double pBlockRaw, double avoidanceBandCap)",
            "public static double PierceFactor(double penDelta, double pierceScale) =>",
            "public static double AmpFactor(double ampDelta, double ampScale) =>",
            "public static double AmpFactorReciprocal(double ampDelta, double ampScale)",
            "public static double DivisiveMitigation(double offense, double defense, double k, double ladderScale)",
        };

        foreach (var snippet in allowed)
        {
            // Fails loudly (not silently) if a declaration in the allowlist no longer matches the
            // shipped source verbatim -- e.g. after a reformat -- rather than let the removal below
            // silently no-op and the test pass vacuously.
            Assert.Contains(snippet, code, System.StringComparison.Ordinal);
            code = code.Replace(snippet, "");
        }

        AssertNoDoubleOrFloat(code, "OverlayCombatCalculator.cs (outside the documented allowlist above)");
    }

    static void AssertNoDoubleOrFloat(string code, string label)
    {
        var matches = Regex.Matches(code, @"\bdouble\b|\bfloat\b");
        Assert.True(matches.Count == 0,
            $"{label}: found {matches.Count} double/float token(s) outside the documented allowlist -- " +
            "either the allowlist is stale (a declaration reformatted) or a new floating-point " +
            "magnitude crept in and needs its own justification or a long/per-mille rewrite.");
    }

    /// <summary>Strips <c>//</c> / <c>///</c> line comments and <c>/* */</c> block comments before a
    /// token scan, so this task's own explanatory prose (which necessarily uses the words "double" and
    /// "float" many times) is never mistaken for a code-level declaration. None of these three files
    /// put <c>//</c> or <c>/*</c> inside a string literal, so this is exact for them.</summary>
    static string StripComments(string src)
    {
        src = Regex.Replace(src, @"/\*.*?\*/", "", RegexOptions.Singleline);
        src = Regex.Replace(src, "//.*", "");
        return src;
    }

    static string ReadCoreFile(params string[] relativeUnderCore)
    {
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = System.IO.Path.Combine(new[] { dir.FullName, "src", "FusionRpg.Core" }.Concat(relativeUnderCore).ToArray());
            if (System.IO.File.Exists(candidate)) return System.IO.File.ReadAllText(candidate);
            dir = dir.Parent;
        }
        throw new System.IO.FileNotFoundException("could not find " + string.Join("/", relativeUnderCore));
    }
}
