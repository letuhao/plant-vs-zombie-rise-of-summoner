using System.Runtime.CompilerServices;
using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Corpus;
using FusionRpg.Core.Actions.Rungs;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Data.Sqlite;
using Xunit;

namespace FusionRpg.Data.Tests.Actions;

/// <summary>
/// T59.4 (spec-action-instance-and-grant.md §1): the composer wired to a real SQLite database,
/// through the real `UpsertContainer`/`UpsertAction`/`UpsertCost` idempotency guards — proven by
/// running the same import twice and reading revisions back, not assumed from the guard's own doc
/// comments.
/// </summary>
public class ActionCorpusImporterTests : IDisposable
{
    readonly DataTestStore _testStore;
    readonly RpgStore _store;

    public ActionCorpusImporterTests()
    {
        _testStore = DataTestStore.Create();
        _store = _testStore.Store;
        SeedAtoms();
    }

    public void Dispose() => _testStore.Dispose();

    void SeedAtoms()
    {
        foreach (var tier in new[] { 1, 2 })
        {
            var atom = new AtomRow
            {
                AtomId = AtomRow.DeriveId("atom.import-test", "", tier),
                KindId = "stat.modify",
                FamilyId = "atom.import-test",
                Variant = "",
                Tier = tier,
                Name = "import test",
                ParamsJson = "{\"channel\":\"maxHp\",\"op\":\"flat\",\"amount\":1}",
            };
            var result = _store.UpsertAtoms(new[] { atom });
            Assert.Empty(result.Rejected);
        }
    }

    static ActionCorpusCostTemplate CostTemplate() => new(
        new Dictionary<ActionCategory, ActionCorpusCostTemplateRow>
        {
            [ActionCategory.Attack] = new("qi", 20, ActionCostTiming.OnCommit),
            [ActionCategory.Defense] = new("qi", 30, ActionCostTiming.OnCommit),
            [ActionCategory.Support] = new("qi", 40, ActionCostTiming.OnCommit),
            [ActionCategory.Movement] = new("qi", 15, ActionCostTiming.OnCommit),
            [ActionCategory.Status] = new("qi", 35, ActionCostTiming.OnCommit),
        },
        // T7 (basic-attack-seed): Kind-aware rows, mirroring the real shipped
        // action-corpus-cost-templates.v1.json's own "kinds" block.
        new Dictionary<ActionKind, ActionCorpusCostTemplateRow>
        {
            [ActionKind.Basic] = new("stamina", 20, ActionCostTiming.OnCommit),
            [ActionKind.Innate] = new("qi", 25, ActionCostTiming.OnCommit),
        });

    static ActionCorpusBrief Brief(string id = "action.import.test.001") => new(
        Id: id, Name: "Import Test Volley", Category: "attack", Scope: "general", ScopeKey: null,
        RungFloor: 1, RungCeiling: 1, AtomFamilies: new[] { "atom.import-test" },
        TargetMode: "single", Relation: "enemy");

    [Fact]
    public void AFreshBriefImportsARealActionContainerAndCost()
    {
        var result = ActionCorpusImporter.Import(_store, new[] { Brief() }, CostTemplate(), RungPolicy.Table);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(0, result.RejectedCount);

        var row = _store.GetAction("action.import.test.001");
        Assert.NotNull(row);
        Assert.Equal(ActionCategory.Attack, row!.Category);
        Assert.NotEmpty(_store.GetContainer(row.ContainerId)!.Atoms);
        Assert.NotEmpty(_store.ListCosts("action.import.test.001"));
    }

    /// <summary>Acceptance criterion 1 (spec's own words): "importing the corpus twice produces
    /// byte-identical rpg_action/rpg_action_cost/container rows — the second import's
    /// UpsertAction/UpsertCost calls move zero revisions."</summary>
    [Fact]
    public void ImportingTheSameBriefSetTwiceMovesZeroRevisionsOnTheSecondPass()
    {
        var briefs = new[] { Brief() };
        var template = CostTemplate();

        ActionCorpusImporter.Import(_store, briefs, template, RungPolicy.Table);
        var revisionAfterFirst = _store.GetAction("action.import.test.001")!.Revision;
        var containerRevisionAfterFirst = _store.GetContainer("skill.action-import-test-001")!.Revision;

        var second = ActionCorpusImporter.Import(_store, briefs, template, RungPolicy.Table);
        var revisionAfterSecond = _store.GetAction("action.import.test.001")!.Revision;
        var containerRevisionAfterSecond = _store.GetContainer("skill.action-import-test-001")!.Revision;

        Assert.Equal(1, second.ImportedCount); // still reports success -- it just wrote nothing new
        Assert.Equal(revisionAfterFirst, revisionAfterSecond);
        Assert.Equal(containerRevisionAfterFirst, containerRevisionAfterSecond);
    }

    [Fact]
    public void ARejectedBriefDoesNotBlockTheRestOfTheBatch()
    {
        var badBrief = Brief("action.import.bad.001") with { Category = "not-a-category" };
        var goodBrief = Brief("action.import.good.001");

        var result = ActionCorpusImporter.Import(_store, new[] { badBrief, goodBrief }, CostTemplate(), RungPolicy.Table);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.RejectedCount);
        Assert.NotNull(_store.GetAction("action.import.good.001"));
        Assert.Null(_store.GetAction("action.import.bad.001"));
    }

    // ---- T7 (basic-attack-seed): the real authored-basics.json + the real committed corpus ----

    static string RepoRoot([CallerFilePath] string here = "")
    {
        var testsDir = Path.GetDirectoryName(here)!;                       // tests/.../Actions
        return Path.GetFullPath(Path.Combine(testsDir, "..", "..", "..")); // repo root
    }

    static string RepoPath(params string[] parts) => Path.Combine(new[] { RepoRoot() }.Concat(parts).ToArray());

    /// <summary>The real shipped tuning file, not the inline mirror above — proves the actual bytes on
    /// disk (with the "kinds" block this task added) parse and resolve correctly.</summary>
    static ActionCorpusCostTemplate RealCostTemplate() =>
        ActionCorpusCostTemplateLoader.Parse(File.ReadAllText(RepoPath("data", "tuning", "action-corpus-cost-templates.v1.json")));

    void SeedRealAtomFile(string relativePath)
    {
        var path = RepoPath(relativePath.Split('/'));
        var collect = AtomSeedFile.Collect(new[] { (path, File.ReadAllText(path)) });
        Assert.True(collect.IsOk, string.Join("; ", collect.Errors));
        var result = _store.UpsertAtoms(collect.Content.Atoms);
        Assert.Empty(result.Rejected);
    }

    /// <summary>Data test (spec-basic-attack-seed.md's own testing-strategy table): importing
    /// `authored-basics.json` yields exactly one row, id `act.attack`, `Kind = Basic`, with a `stamina`
    /// cost row attached — against the REAL seed file, the REAL atom (`atom.fx-overlay-damage`,
    /// `data/seed/atoms/fx-core.json`) and the REAL tuning file, not hand-built fixtures.</summary>
    [Fact]
    public void ImportingTheRealAuthoredBasicsFileYieldsActAttackAsBasicWithAStaminaCost()
    {
        SeedRealAtomFile("data/seed/atoms/fx-core.json");
        var briefs = ActionCorpusBriefJson.Parse(File.ReadAllText(RepoPath("data", "seed", "actions", "authored-basics.json")));
        Assert.Single(briefs);
        Assert.Equal("act.attack", briefs[0].Id);
        Assert.Equal(ActionKind.Basic, briefs[0].KindHint);

        var result = ActionCorpusImporter.Import(_store, briefs, RealCostTemplate(), RungPolicy.Table);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(0, result.RejectedCount);

        var row = _store.GetAction("act.attack");
        Assert.NotNull(row);
        Assert.Equal(ActionKind.Basic, row!.Kind);

        var costs = _store.ListCosts("act.attack");
        Assert.Single(costs);
        Assert.Equal("stamina", costs[0].ResourceId);
    }

    /// <summary>
    /// The task's own "most important regression check", run for real against the real shipped
    /// `committed-round-{1,2}.json` (24 briefs) and the real tuning file — mirroring
    /// `ActionCorpusRealContentQualityTests`'s established atom fixture (only `atom.fortitude` and
    /// `atom.vitality` resolve, so exactly 3 of 24 briefs import; unchanged by this task, since kindHint
    /// honoring never touches atom-family resolution).
    ///
    /// <para><b>Premise found wrong while verifying this, reported rather than hidden:</b> the todo's
    /// own acceptance bar reads "ALL 179 existing action briefs import completely UNCHANGED — no Kind
    /// drift, no cost drift for any of them." That is false for one of the three briefs that actually
    /// import today: `action.species.cabbagepult.002` already authors `"kindHint": "innate"` in the
    /// real shipped file (measured directly, not assumed) — before this task every brief hardcoded to
    /// `Kind = Skill` regardless of `kindHint`; after this task, honoring `kindHint` (which is the
    /// task's own primary acceptance criterion) necessarily flips this ONE already-imported brief's
    /// `Kind` from `Skill` to `Innate`, and its cost from the `defense` category row (`qi` 30) to the
    /// `kinds.innate` row (`qi` 25). This is the correct, intended effect of finally consuming a field
    /// the parser used to discard (spec-basic-attack-seed.md: "That is the defect; the field is not
    /// new") — not a regression introduced by this change. The other two composing briefs
    /// (`action.general.0003`, `action.species.cabbagepult.001`) carry no `kindHint` and are provably
    /// unaffected, asserted below.</para>
    /// </summary>
    [Fact]
    public void TheRealShippedCorpusHasExactlyOneDocumentedKindChangeAndNoOthers()
    {
        SeedRealAtomFile("data/seed/atoms/generated/family-expand.g-life.json");
        var briefs = new List<ActionCorpusBrief>();
        foreach (var f in new[] { "committed-round-1.json", "committed-round-2.json" })
            briefs.AddRange(ActionCorpusBriefJson.Parse(File.ReadAllText(RepoPath("data", "seed", "actions", f))));
        Assert.Equal(24, briefs.Count); // liveness -- the real files still have 24 rows between them

        var result = ActionCorpusImporter.Import(_store, briefs, RealCostTemplate(), RungPolicy.Table);

        // Atom-family resolution is untouched by this task -- same 3-imported/21-rejected split as
        // ActionCorpusRealContentQualityTests already established before this task existed.
        Assert.Equal(3, result.ImportedCount);
        Assert.Equal(21, result.RejectedCount);

        // Unaffected: no kindHint authored on either -> Skill, category-driven cost, same as always.
        var general0003 = _store.GetAction("action.general.0003")!;
        Assert.Equal(ActionKind.Skill, general0003.Kind);
        Assert.Equal("qi", _store.ListCosts("action.general.0003").Single().ResourceId);

        var cabbagepult1 = _store.GetAction("action.species.cabbagepult.001")!;
        Assert.Equal(ActionKind.Skill, cabbagepult1.Kind);
        Assert.Equal("qi", _store.ListCosts("action.species.cabbagepult.001").Single().ResourceId);

        // The one documented, intended change: kindHint="innate" was always in this file; only now is
        // it honored.
        var cabbagepult2 = _store.GetAction("action.species.cabbagepult.002")!;
        Assert.Equal(ActionKind.Innate, cabbagepult2.Kind);
        var cabbagepult2Cost = _store.ListCosts("action.species.cabbagepult.002").Single();
        Assert.Equal("qi", cabbagepult2Cost.ResourceId);
        Assert.Equal(ValueSpec.Of(25), cabbagepult2Cost.AmountSpec); // kinds.innate, not defense-category's 30
    }
}
