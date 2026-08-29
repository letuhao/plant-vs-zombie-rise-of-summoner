using FusionRpg.Core.Battle;
using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Derived;
using Xunit;
using Xunit.Abstractions;

namespace FusionRpg.Core.Tests.Stats;

/// <summary>
/// spec-catalog-extension.md §6.3 — <c>PvzStatsSheetComposer</c> used to call
/// <see cref="DerivedStatRegistry.CreateDefault"/> fresh on every call (99 allocations before this
/// program, 256 after); <c>BattleStatComposer</c> had the opposite defect, a bare <c>static readonly</c>
/// that never refreshed. Both now cache by reference identity against <see cref="ElementTable.Current"/>,
/// matching <see cref="DerivedStatChannels.AllCombatChannelIds"/>'s own E25 idiom.
/// </summary>
public class SheetComposerCacheTests
{
    readonly ITestOutputHelper _out;

    public SheetComposerCacheTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void SheetComposerAllocatesOnce()
    {
        using var scope = ElementTable.UseScoped(ElementTable.Shipped());

        // Warm: first call in this scope always allocates (cache miss for this table reference).
        _ = PvzStatsSheetComposer.TryCanonicalizeOrDerivedChannel(DerivedStatChannels.CombatPowerOmni);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var before = GC.GetAllocatedBytesForCurrentThread();

        var hits = 0;
        for (var i = 0; i < 10_000; i++)
            if (PvzStatsSheetComposer.TryCanonicalizeOrDerivedChannel(DerivedStatChannels.CombatPowerOmni) != null) hits++;

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var perCall = allocated / 10_000.0;

        _out.WriteLine($"allocated over 10^4 cached validations: {allocated} bytes ({perCall:F2} B/call, hits {hits})");
        Assert.Equal(10_000, hits);

        // A single registry rebuild (256 defs + a Dictionary) is on the order of tens of KB; if the
        // registry were rebuilt every call this would be ~256 allocations * 10^4 calls instead of ~0.
        Assert.True(perCall < 96,
            $"{perCall:F2} bytes/call suggests the registry is being rebuilt per call — expected close to 0");
    }

    [Fact]
    public void ScopedRosterStillHonoured_PvzStatsSheetComposer()
    {
        var shipped = ElementTable.Shipped();
        var withVoid = shipped.Elements.Append(new ElementRow("void", "Void", 6)).ToList();

        // Baseline: outside the scope, the 7th element's channel is unknown.
        Assert.Null(PvzStatsSheetComposer.TryCanonicalizeOrDerivedChannel("combat.power.void"));

        using (ElementTable.UseScoped(new ElementTable(withVoid, shipped.CombatRows, shipped.ShieldRows)))
        {
            Assert.Equal("combat.power.void", PvzStatsSheetComposer.TryCanonicalizeOrDerivedChannel("combat.power.void"));
        }

        // A bare static cache (rather than one keyed on ElementTable.Current) would leak the scoped
        // roster past the `using` block's Dispose — prove it reverts.
        Assert.Null(PvzStatsSheetComposer.TryCanonicalizeOrDerivedChannel("combat.power.void"));
    }

    [Fact]
    public void ScopedRosterStillHonoured_BattleStatComposer()
    {
        // This is the regression the bare `static readonly HashSet<string> KnownChannels` had: a
        // ChannelMod naming a channel that only exists in a roster swapped in AFTER the type's static
        // constructor ran was rejected as "unknown" even though DerivedStatChannels had already
        // registered it. Proven by actually exercising Compose(), not just the channel-id lookup.
        var shipped = ElementTable.Shipped();
        var withVoid = shipped.Elements.Append(new ElementRow("void", "Void", 6)).ToList();

        using var scope = ElementTable.UseScoped(new ElementTable(withVoid, shipped.CombatRows, shipped.ShieldRows));

        var setup = new BattleActorSetup
        {
            Level = 1,
            Atk = 10,
            Defense = 10,
            ChannelMods = new[] { new BattleChannelMod("combat.power.void", 5) }
        };

        var snap = BattleStatComposer.Compose(setup);
        Assert.Equal(5, snap.Get("combat.power.void"));
    }

    [Fact]
    public void ComposerAllocationAt196()
    {
        // 196 is 2.3x the 84 the E25 cache was originally measured against (spec-catalog-extension.md
        // open question §6.3 — a measurement, not an assumption). Re-run at the real, current roster
        // size rather than trust the old measurement still holds.
        Assert.Equal(196, DerivedStatChannels.AllCombatChannelIds.Count);

        using var scope = ElementTable.UseScoped(ElementTable.Shipped());
        var setup = new BattleActorSetup { Level = 1, Atk = 10, Defense = 10 };

        // Warm every relevant cache (BattleStatComposer.KnownChannels, PvzStatsSheetComposer's
        // registry, DerivedStatChannels' own cache) for this ElementTable reference.
        _ = BattleStatComposer.Compose(setup);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 1_000; i++)
            _ = BattleStatComposer.Compose(setup);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var perCall = allocated / 1_000.0;

        _out.WriteLine($"BattleStatComposer.Compose allocated over 10^3 calls at 196 combat channels: {allocated} bytes ({perCall:F2} B/call)");

        // Compose() itself legitimately allocates per call (a fresh ActorDerivedSnapshot, its backing
        // dictionary, KeyValuePair array) -- this is not a zero-allocation budget. The claim under
        // test is narrower: that cost must not scale with catalog size (no per-call channel-set
        // rebuild), so the budget is generous headroom over Compose()'s own genuine per-call cost,
        // not "should be ~0" the way the pure cache-lookup tests above are.
        Assert.True(perCall < 4096,
            $"{perCall:F2} bytes/call at 196 channels suggests a per-call rebuild crept back in");
    }
}
