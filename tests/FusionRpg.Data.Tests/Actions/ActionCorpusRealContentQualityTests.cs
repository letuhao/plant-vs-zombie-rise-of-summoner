using System.Linq;
using System.Runtime.CompilerServices;
using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Corpus;
using FusionRpg.Core.Actions.Rungs;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Data.Sqlite;
using Xunit;

namespace FusionRpg.Data.Tests.Actions;

/// <summary>
/// T59.5 (spec-action-instance-and-grant.md, criterion 2): the REAL shipped
/// `data/seed/actions/committed-round-{1,2}.json` (24 rows), imported through T59.4's real importer
/// against a real atom catalog seeded from the REAL `data/seed/atoms/` files — not a hand-built
/// fixture, matching `AuthoredEligibilityResolvesTests.cs`'s own precedent of reading real files.
///
/// <para><b>Real, measured finding (2026-09-06), not assumed</b>: only 2 of the 30 unique
/// `atomFamilies` the real corpus names (`atom.fortitude`, `atom.vitality`) exist anywhere under
/// `data/seed/atoms/` — 28 do not (`atom.sporing`, `atom.volley`, `atom.cherry-bloom`, etc.), confirmed
/// by a direct cross-check of every seed file. This mirrors the already-documented item-unique-corpus
/// atom-family gap (144 anchors name 68 families, only 28 real) — the SAME small, early-stage atom
/// catalog, a different content type hitting the identical wall. Exactly 3 of 24 briefs reference at
/// least one of the two resolvable families, so exactly 3 import; the other 21 correctly refuse,
/// naming why. This is a real content-authoring gap in a sibling pipeline (the atom/family generator),
/// not a defect in A21's own import machinery — fixing it is out of this module's scope, matching how
/// the item-unique gap was left "correctly refusing" rather than force-fixed.</para>
/// </summary>
public class ActionCorpusRealContentQualityTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public ActionCorpusRealContentQualityTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-action-corpus-real-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();

        // The one real atom seed file backing the two families the real corpus can actually reach.
        var atomsPath = RepoPath("data", "seed", "atoms", "generated", "family-expand.g-life.json");
        var collect = AtomSeedFile.Collect(new[] { (atomsPath, File.ReadAllText(atomsPath)) });
        Assert.True(collect.IsOk, string.Join("; ", collect.Errors));
        var atomResult = _store.UpsertAtoms(collect.Content.Atoms);
        Assert.Empty(atomResult.Rejected);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    static string RepoRoot([CallerFilePath] string here = "")
    {
        var testsDir = Path.GetDirectoryName(here)!;                       // tests/.../Actions
        return Path.GetFullPath(Path.Combine(testsDir, "..", "..", "..")); // repo root
    }

    static string RepoPath(params string[] parts) => Path.Combine(new[] { RepoRoot() }.Concat(parts).ToArray());

    static ActionCorpusCostTemplate CostTemplate() => new(new Dictionary<ActionCategory, ActionCorpusCostTemplateRow>
    {
        [ActionCategory.Attack] = new("qi", 20, ActionCostTiming.OnCommit),
        [ActionCategory.Defense] = new("qi", 30, ActionCostTiming.OnCommit),
        [ActionCategory.Support] = new("qi", 40, ActionCostTiming.OnCommit),
        [ActionCategory.Movement] = new("qi", 15, ActionCostTiming.OnCommit),
        [ActionCategory.Status] = new("qi", 35, ActionCostTiming.OnCommit),
    });

    static IReadOnlyList<ActionCorpusBrief> RealBriefs()
    {
        var briefs = new List<ActionCorpusBrief>();
        foreach (var f in new[] { "committed-round-1.json", "committed-round-2.json" })
            briefs.AddRange(ActionCorpusBriefJson.Parse(File.ReadAllText(RepoPath("data", "seed", "actions", f))));
        return briefs;
    }

    [Fact]
    public void ImportingTheRealShippedCorpusSucceedsExactlyWhereItsFamiliesResolveAndRejectsHonestlyElsewhere()
    {
        var briefs = RealBriefs();
        Assert.Equal(24, briefs.Count); // liveness -- the real files still have 24 rows between them

        var result = ActionCorpusImporter.Import(_store, briefs, CostTemplate(), RungPolicy.Table);

        Assert.Equal(3, result.ImportedCount);
        Assert.Equal(21, result.RejectedCount);
        Assert.All(result.Outcomes.Where(o => !o.Imported),
            o => Assert.Contains("resolved any atom", o.Rejection ?? "", StringComparison.Ordinal));
    }

    /// <summary>Criterion 2, exercised for real: every row that DOES import must clear
    /// `StructureBudgetGuard.Check` at its own authored rung — a content-quality check, not just a
    /// schema check.</summary>
    [Fact]
    public void EveryImportedRowClearsItsOwnStructureBudgetAtItsAuthoredRung()
    {
        var result = ActionCorpusImporter.Import(_store, RealBriefs(), CostTemplate(), RungPolicy.Table);
        var imported = result.Outcomes.Where(o => o.Imported).ToList();
        Assert.NotEmpty(imported); // liveness -- if this ever hits zero, the test below is vacuous

        foreach (var outcome in imported)
        {
            var row = _store.GetAction(outcome.BriefId);
            Assert.NotNull(row);
            var costs = _store.ListCosts(outcome.BriefId);
            var scopes = _store.ListScopes(outcome.BriefId);

            var check = StructureBudgetGuard.Check(row!, costs, scopes, RungPolicy.Table);
            Assert.True(check.IsOk, $"{outcome.BriefId} (rung {row!.Rung}): {check.Detail}");
        }
    }
}
