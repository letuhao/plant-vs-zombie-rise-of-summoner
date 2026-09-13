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

    static ActionCorpusCostTemplate CostTemplate() => new(new Dictionary<ActionCategory, ActionCorpusCostTemplateRow>
    {
        [ActionCategory.Attack] = new("qi", 20, ActionCostTiming.OnCommit),
        [ActionCategory.Defense] = new("qi", 30, ActionCostTiming.OnCommit),
        [ActionCategory.Support] = new("qi", 40, ActionCostTiming.OnCommit),
        [ActionCategory.Movement] = new("qi", 15, ActionCostTiming.OnCommit),
        [ActionCategory.Status] = new("qi", 35, ActionCostTiming.OnCommit),
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
}
