using System.Text.Json;
using FusionRpg.Core.Delve.Events;
using FusionRpg.Core.Dungeon.Registry;
using FusionRpg.Core.Tests.Dungeon;
using Xunit;

namespace FusionRpg.Core.Tests.Delve.Events;

/// <summary>
/// D1.10's real remaining scope, event half (2026-09-07) — proves whatever real, seedsmith-authored
/// event content exists at `data/seed/dungeon/events/*.json` against the REAL, authoritative
/// `EventCatalog.Load` validator, the same "run the real validator" discipline `QuestSeedContentTests`
/// already established for quest content. `BandCatalog`/`OverrideTagCatalog` are already configured
/// for the whole assembly by `Dungeon.DungeonHubTestBootstrap`'s module initializer.
///
/// <para>Correct-by-construction against an empty directory (no content shipped yet is a real,
/// honest, zero-rejection state) — this test becomes load-bearing the moment real content lands,
/// without needing its own edit.</para>
/// </summary>
public class EventSeedContentTests
{
    static readonly IReadOnlyList<string> EventKinds = BandCatalog.Get("eventKind").Members;
    static readonly IReadOnlyList<string> RepeatScopes = BandCatalog.Get("repeatScope").Members;
    static readonly IReadOnlyList<string> OutcomeOrdinals = BandCatalog.Get("outcomeOrdinal").Members;
    static readonly IReadOnlyList<string> OverrideTags = OverrideTagCatalog.All;
    static readonly IReadOnlyList<string> DropBands = ReadRealItemDropBands();

    static IReadOnlyList<string> ReadRealItemDropBands()
    {
        var path = Path.Combine(DungeonTestFiles.RepoRoot(), "data", "seed", "items", "_registry", "bands.v1.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("dropBand").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!).ToList();
    }

    static int NoStatus(string id) => -1; // this batch's own eligibility is always null -- no status leaf is ever compiled

    [Fact]
    public void Every_real_shipped_event_anchor_round_trips_with_zero_rejections()
    {
        var rows = EventSeedFile.LoadAll(DungeonTestFiles.EventsDir());
        var result = EventCatalog.Load(rows, EventKinds, RepeatScopes, OutcomeOrdinals, DropBands, OverrideTags, NoStatus);

        Assert.Empty(result.Rejections);
        Assert.Equal(rows.Count, result.Catalog.Count);
    }

    /// <summary>D3.9's own `OverrideTagUnsupplied` rule (spec §9), real content: all 52 shipped events
    /// carry `supplyOverride: "none"` (zero real tags to check coverage for) — a genuine vacuous pass,
    /// not a bug in the check (`EventDeckPreflightTests.cs` proves the check itself catches a real
    /// violation via a hand-built fixture).</summary>
    [Fact]
    public void Real_shipped_events_carry_no_supplyOverride_tag_so_coverage_passes_vacuously()
    {
        var rows = EventSeedFile.LoadAll(DungeonTestFiles.EventsDir());
        var catalog = EventCatalog.Load(rows, EventKinds, RepeatScopes, OutcomeOrdinals, DropBands, OverrideTags, NoStatus).Catalog;
        var suppliesDir = Path.Combine(DungeonTestFiles.RepoRoot(), "data", "seed", "dungeon", "supplies");
        var tagsCarried = SupplyOverrideTagSeedFile.LoadAllOverrideTags(suppliesDir);

        Assert.Empty(catalog.All.Where(e => e.SupplyOverride is not null));
        Assert.Empty(EventDeckPreflight.CheckSupplyOverrideCoverage(catalog, tagsCarried));
    }

    [Fact]
    public void Shipped_story_kind_events_each_carry_a_real_non_empty_chainRef()
    {
        // SUPERSEDES the earlier "no story kind shipped" premise: a real, standalone-but-honestly-
        // chained pair now exists (2026-09-07, same session) — event A's `chainRef` names event B
        // (a real, resolvable sibling); event B's own forward link honestly names a plausible next
        // chapter that does not exist yet (`EventDeckPreflight.CheckChainRefs`'s own "an unresolved
        // chainRef... is silently skipped" comment, confirmed by reading that file directly, is what
        // makes this legal — never a cycle, which `HasCycleFrom` WOULD catch). Every story event's
        // own `ChainRef` must be non-empty regardless (`EventRules.ChainRefRequiredForStory`,
        // enforced by `EventCatalog.Load` itself, proven by the round-trip test above already).
        var rows = EventSeedFile.LoadAll(DungeonTestFiles.EventsDir());
        var storyRows = rows.Where(r => r.Kind == "story").ToList();
        Assert.NotEmpty(storyRows);
        foreach (var row in storyRows)
            Assert.False(string.IsNullOrWhiteSpace(row.ChainRef), row.EventId);
    }

    [Fact]
    public void LoadAll_on_a_missing_directory_returns_empty_not_throws()
    {
        var rows = EventSeedFile.LoadAll(Path.Combine(DungeonTestFiles.RepoRoot(), "data", "seed", "dungeon", "does-not-exist"));
        Assert.Empty(rows);
    }

    [Fact]
    public void LoadAll_null_argument_throws()
    {
        Assert.Throws<ArgumentNullException>(() => EventSeedFile.LoadAll(null!));
    }
}
