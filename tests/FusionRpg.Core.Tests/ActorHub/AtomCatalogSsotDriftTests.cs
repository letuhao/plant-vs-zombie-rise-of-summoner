using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Status;
using Xunit;

namespace FusionRpg.Core.Tests.ActorHub;

/// <summary>
/// `atom-catalog-ssot.md` is the document that answers "what is in the atom effect pool". It had no
/// test, and it drifted badly: it claimed **99** derived channels against a real **267**, **8** primary
/// channels against a real **11**, and **7** triggers against a real **8** — the derived figure stale
/// across three separate expansions (T2 element widening 99→256, `poise-resource` →259,
/// `turn.speed`/`turn.haste` →261, `resource.restore` →267). Measured and corrected 2026-09-02
/// (`docs/research/atom-effect-pool-audit-2026-09-02.md`).
///
/// <para><b>Why the sibling document never drifted:</b> `spec-derived-stat-sheet.md` carries the same
/// numbers and is pinned by <c>ElementHubDocDriftTests.StatSheetCountsMatchGeneration</c>. This suite is
/// that same shape, applied to the file that actually calls itself the SSOT — including the planted-drift
/// companion, because a drift test that cannot fail is not a guard.</para>
/// </summary>
public class AtomCatalogSsotDriftTests
{
    const string Doc = "atom-catalog-ssot.md";

    static string ReadSsot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var path = Path.Combine(dir.FullName, "docs", "architecture", "effect-atom", Doc);
            if (File.Exists(path)) return File.ReadAllText(path);
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"could not find docs/architecture/effect-atom/{Doc}");
    }

    [Fact]
    public void ChannelAndVocabularyCountsMatchCode()
    {
        var text = ReadSsot();
        var registry = DerivedStatRegistry.CreateDefault();

        // Sanity anchors: these ARE today's generated values, not guesses. If one of these Assert.Equal
        // lines fails, code moved and the doc needs updating — that is the point.
        Assert.Equal(267, registry.AllRegistered.Count);
        Assert.Equal(11, StatChannels.All.Length);
        Assert.Equal(8, AtomTriggers.All.Length);
        Assert.Equal(12, AtomKindRegistry.KindCount);
        Assert.Equal(5, AtomKindRegistry.AttachPointCount);

        Assert.Contains($"Derived — {registry.AllRegistered.Count} registered", text);
        Assert.Contains($"Primary — {StatChannels.All.Length}, and only these", text);
        Assert.Contains($"Trigger vocabulary — {AtomTriggers.All.Length}", text);
        Assert.Contains($"The closed kind list — {AtomKindRegistry.KindCount}", text);

        // Every primary channel is named in the doc's own §4.1 list, so the list cannot go stale by
        // omission the way it did when attackInterval/produceInterval/zombieSpeed were "pending".
        foreach (var channel in StatChannels.All)
            Assert.Contains($"`{channel}`", text);

        // Same for triggers — OnActivate was missing entirely until 2026-09-02.
        foreach (var trigger in AtomTriggers.All)
            Assert.Contains($"`{trigger}`", text);
    }

    [Fact]
    public void ChannelAndVocabularyCountsMatchCode_failsOnAPlantedDrift()
    {
        var text = ReadSsot();

        // The exact pre-correction wording. If any of these ever comes back the doc has regressed to a
        // number that was wrong by 168 channels, and the test above would still pass on a partial edit.
        Assert.DoesNotContain("Derived — 99 pre-registered", text);
        Assert.DoesNotContain("Primary — 8, and only these", text);
        Assert.DoesNotContain("Trigger vocabulary — 7", text);
        Assert.DoesNotContain("| **Triggers** | **7** |", text);

        // The 2026-08-22 sweep's status claims, all re-measured 2026-09-02 and all wrong: every one of
        // the 21 statuses has an executing consumer in at least one runtime. "13 functional" was the
        // number that made ember/jala/kelp and the four ModifyStat statuses look unauthorable.
        Assert.DoesNotContain("## 5. Status catalog — 21 declared, 13 functional", text);
    }

    /// <summary>
    /// The status catalog is the largest enumerable vocabulary behind any atom param (`status.apply`
    /// takes one of these ids), so its size is part of "what is in the pool". Pinned to the bootstrap
    /// rather than to a literal.
    /// </summary>
    [Fact]
    public void StatusCountMatchesTheCatalog()
    {
        var text = ReadSsot();
        var declared = StatusCatalogBootstrap.CreateDefault().All().Count;

        Assert.Equal(21, declared); // sanity anchor: today's generated value
        Assert.Contains($"Status catalog — {declared} declared", text);
    }

    /// <summary>
    /// The audit's load-bearing distinction: the vocabulary is BUILT while the pool is EMPTY. A reader
    /// who takes this file as an inventory of authorable content gets it exactly backwards, which is why
    /// §0 exists — and why it must keep existing.
    /// </summary>
    [Fact]
    public void SsotStatesThatRegisteredIsNotAuthored()
    {
        var text = ReadSsot();
        Assert.Contains("The POOL is empty", text);
        Assert.Contains("Registered ≠ authorable", text);
    }
}
