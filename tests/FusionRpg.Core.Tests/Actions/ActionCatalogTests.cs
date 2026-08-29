using System.Linq;
using FusionRpg.Contracts;
using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Rungs;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Effects.Atoms;
using Xunit;

namespace FusionRpg.Core.Tests.Actions;

/// <summary>
/// T30 (action-todo.md, spec-action-catalog.md): load, compile, cache, hash. The content-hash half
/// (both directions, a real database) lives in <c>ActionCatalogStoreTests</c> (Data.Tests) per the
/// spec's own split; this file covers the Core-side compile pipeline, the R1 structure-budget guard,
/// and the immutable catalog's atomic revision swap.
/// </summary>
public class ActionCatalogTests
{
    static readonly HashSet<string> OneAtomContainer = new(StringComparer.Ordinal) { "atom.strike" };

    static ActionRow BaseRow(string id = "skill.test", int rung = 1, string? conditionsJson = null,
        ActionEnvelope? envelope = null, IReadOnlyList<ActionTag>? tags = null) => new()
    {
        ActionId = id,
        Name = "Test",
        Kind = ActionKind.Skill,
        ContainerId = "item.test",
        Rung = rung,
        Envelope = envelope ?? ActionEnvelope.NoOp with { ActionId = id },
        Targeting = new ActionTargetSpec(),
        ConditionsJson = conditionsJson,
        Tags = tags ?? Array.Empty<ActionTag>(),
    };

    /// <summary>A contiguous 1..rung table (RungTable indexes by array, rungs 1..cap) whose LAST row
    /// (the one under test) carries <paramref name="structureBudget"/>; every earlier rung budgets
    /// nothing.</summary>
    static RungTable OneRung(int rung, IReadOnlyList<string> structureBudget, int costMulti = 1000)
    {
        var rows = new RungRow[rung];
        for (var r = 1; r < rung; r++) rows[r - 1] = new RungRow(r, 1, 1, 1, 1000, 1000, 1000, Array.Empty<string>());
        rows[rung - 1] = new RungRow(rung, 1, 1, 1, 1000, costMulti, 1000, structureBudget);
        return new RungTable(cap: rung, rows);
    }

    static RungTable TenRungShippedShape() => new(cap: 3, new[]
    {
        new RungRow(1, 1, 1, 1, 1000, 1000, 1000, Array.Empty<string>()),
        new RungRow(2, 1, 1, 1, 1000, 1000, 1000, Array.Empty<string>()),
        new RungRow(3, 1, 1, 1, 1000, 1000, 1000, new[] { StructureAxes.Condition, StructureAxes.ScopeSplit }),
    });

    // ---- StructureBudgetGuard.SpentAxes ------------------------------------------------------

    [Fact]
    public void ConditionAxisIsSpentWhenConditionsJsonIsAuthored()
    {
        var row = BaseRow(conditionsJson: """{"leaf":"sideIs","subject":"target","value":"zombie"}""");
        var spent = StructureBudgetGuard.SpentAxes(row, Array.Empty<ActionCostRow>(), Array.Empty<ActionScopeRow>());
        Assert.Contains(StructureAxes.Condition, spent);
    }

    [Fact]
    public void ConditionAxisIsNotSpentWithNoConditionsJson()
    {
        var row = BaseRow(conditionsJson: null);
        var spent = StructureBudgetGuard.SpentAxes(row, Array.Empty<ActionCostRow>(), Array.Empty<ActionScopeRow>());
        Assert.DoesNotContain(StructureAxes.Condition, spent);
    }

    [Fact]
    public void SequenceAxisIsSpentWithMoreThanOneResolveOffset()
    {
        var row = BaseRow(envelope: ActionEnvelope.NoOp with { ActionId = "skill.test", ResolveOffsets = new long[] { 0, 5 } });
        var spent = StructureBudgetGuard.SpentAxes(row, Array.Empty<ActionCostRow>(), Array.Empty<ActionScopeRow>());
        Assert.Contains(StructureAxes.Sequence, spent);
    }

    [Fact]
    public void SequenceAxisIsNotSpentByASingleOffset()
    {
        var row = BaseRow();
        var spent = StructureBudgetGuard.SpentAxes(row, Array.Empty<ActionCostRow>(), Array.Empty<ActionScopeRow>());
        Assert.DoesNotContain(StructureAxes.Sequence, spent);
    }

    [Fact]
    public void ConsumptionAxisIsSpentByAnyPerTickCost()
    {
        var costs = new[] { new ActionCostRow("skill.test", "stamina", new ValueSpec(1, 1, RollPolicy.Fixed), ActionCostTiming.PerTick) };
        var spent = StructureBudgetGuard.SpentAxes(BaseRow(), costs, Array.Empty<ActionScopeRow>());
        Assert.Contains(StructureAxes.Consumption, spent);
    }

    [Fact]
    public void ConsumptionAxisIsNotSpentByAnOnCommitCost()
    {
        var costs = new[] { new ActionCostRow("skill.test", "stamina", new ValueSpec(1, 1, RollPolicy.Fixed), ActionCostTiming.OnCommit) };
        var spent = StructureBudgetGuard.SpentAxes(BaseRow(), costs, Array.Empty<ActionScopeRow>());
        Assert.DoesNotContain(StructureAxes.Consumption, spent);
    }

    [Fact]
    public void ScopeSplitIsSpentWhenScopeRowsSpanMoreThanOneDistinctScope()
    {
        var scopes = new[]
        {
            new ActionScopeRow("skill.test", "atom.strike", ActionEffectScope.PrimaryTarget),
            new ActionScopeRow("skill.test", "atom.heal", ActionEffectScope.Caster),
        };
        var spent = StructureBudgetGuard.SpentAxes(BaseRow(), Array.Empty<ActionCostRow>(), scopes);
        Assert.Contains(StructureAxes.ScopeSplit, spent);
        Assert.DoesNotContain(StructureAxes.RiderStatus, spent); // one atom per scope -- no rider
    }

    [Fact]
    public void RiderStatusIsSpentWhenTwoAtomsShareOneScope()
    {
        var scopes = new[]
        {
            new ActionScopeRow("skill.test", "atom.strike", ActionEffectScope.PrimaryTarget),
            new ActionScopeRow("skill.test", "atom.poison-rider", ActionEffectScope.PrimaryTarget),
        };
        var spent = StructureBudgetGuard.SpentAxes(BaseRow(), Array.Empty<ActionCostRow>(), scopes);
        Assert.Contains(StructureAxes.RiderStatus, spent);
        Assert.DoesNotContain(StructureAxes.ScopeSplit, spent); // one scope only -- no split
    }

    [Fact]
    public void NoScopeRowsSpendsNeitherScopeAxis()
    {
        var spent = StructureBudgetGuard.SpentAxes(BaseRow(), Array.Empty<ActionCostRow>(), Array.Empty<ActionScopeRow>());
        Assert.DoesNotContain(StructureAxes.ScopeSplit, spent);
        Assert.DoesNotContain(StructureAxes.RiderStatus, spent);
    }

    [Fact]
    public void ReactionIsNeverSpendableTodayNoActionKindIsReactionShaped()
    {
        // spec's own honest-gap boundary: reaction cannot be authored today at all, verified by
        // reading the enum rather than assumed -- exactly three members, none reaction-shaped.
        var names = Enum.GetNames(typeof(ActionKind));
        Assert.Equal(new[] { "Basic", "Innate", "Skill" }, names.OrderBy(n => n, StringComparer.Ordinal));
        // Every SpentAxes call above already never produced "reaction" -- there is no input shape
        // that could, since nothing in ActionRow/ActionCostRow/ActionScopeRow encodes a reaction.
    }

    // ---- StructureBudgetGuard.Check (rung lookup + rejection) --------------------------------

    [Fact]
    public void UnknownRungIsRejectedNamingTheRung()
    {
        var table = OneRung(1, Array.Empty<string>());
        var rejection = StructureBudgetGuard.Check(BaseRow(rung: 99), Array.Empty<ActionCostRow>(), Array.Empty<ActionScopeRow>(), table);
        Assert.Equal(ActionRejectionReason.UnknownRung, rejection.Reason);
        Assert.Contains("99", rejection.Detail);
    }

    [Fact]
    public void APlantedActionSpendingAnUnbudgetedAxisIsRejectedNamingRungAndAxis()
    {
        var table = OneRung(1, Array.Empty<string>()); // rung 1 budgets nothing
        var row = BaseRow(rung: 1, conditionsJson: """{"leaf":"sideIs","subject":"target","value":"zombie"}""");

        var rejection = StructureBudgetGuard.Check(row, Array.Empty<ActionCostRow>(), Array.Empty<ActionScopeRow>(), table);

        Assert.Equal(ActionRejectionReason.StructureExceedsBudget, rejection.Reason);
        Assert.Contains("rung 1", rejection.Detail);
        Assert.Contains(StructureAxes.Condition, rejection.Detail);
    }

    [Fact]
    public void TheSameStructureIsAcceptedOnceTheRungBudgetsForIt()
    {
        var table = OneRung(3, new[] { StructureAxes.Condition });
        var row = BaseRow(rung: 3, conditionsJson: """{"leaf":"sideIs","subject":"target","value":"zombie"}""");

        var rejection = StructureBudgetGuard.Check(row, Array.Empty<ActionCostRow>(), Array.Empty<ActionScopeRow>(), table);

        Assert.True(rejection.IsOk, rejection.ToString());
    }

    [Fact]
    public void TheShippedTenRowShapeRejectsALowRungCarryingHighStructure()
    {
        // A rung-1 action authored with a rung-3-and-up structure (condition) is exactly the defect
        // R1 exists to prevent -- "a rung-2 action carrying a reaction would price above its rung
        // while the content lied."
        var table = TenRungShippedShape();
        var row = BaseRow(rung: 1, conditionsJson: """{"leaf":"sideIs","subject":"target","value":"zombie"}""");

        var rejection = StructureBudgetGuard.Check(row, Array.Empty<ActionCostRow>(), Array.Empty<ActionScopeRow>(), table);

        Assert.Equal(ActionRejectionReason.StructureExceedsBudget, rejection.Reason);
    }

    // ---- ActionCompiler.Compile -----------------------------------------------------------------

    [Fact]
    public void APlantedMalformedRowFailsAtCompileNamingTheRow()
    {
        var row = BaseRow(id: "skill.bad") with { MinRange = 10, MaxRange = 1 }; // planted InvalidRange
        var table = OneRung(1, Array.Empty<string>());

        var (rejection, compiled) = ActionCompiler.Compile(
            row, Array.Empty<ActionCostRow>(), Array.Empty<ActionScopeRow>(), OneAtomContainer, boardAvailable: false, table);

        Assert.Null(compiled);
        Assert.Equal(ActionRejectionReason.InvalidRange, rejection.Reason);
        Assert.Contains("skill.bad", rejection.Detail);
    }

    [Fact]
    public void APlantedUnknownContainerFailsAtCompile()
    {
        var row = BaseRow();
        var table = OneRung(1, Array.Empty<string>());

        var (rejection, compiled) = ActionCompiler.Compile(
            row, Array.Empty<ActionCostRow>(), Array.Empty<ActionScopeRow>(), containerAtomIds: null, boardAvailable: false, table);

        Assert.Null(compiled);
        Assert.Equal(ActionRejectionReason.UnknownContainer, rejection.Reason);
    }

    [Fact]
    public void APlantedBadConditionsJsonFailsAtCompileRatherThanSilentlyDefaultingToAlways()
    {
        var row = BaseRow(conditionsJson: "{ not json");
        var table = OneRung(1, new[] { StructureAxes.Condition });

        var (rejection, compiled) = ActionCompiler.Compile(
            row, Array.Empty<ActionCostRow>(), Array.Empty<ActionScopeRow>(), OneAtomContainer, boardAvailable: false, table);

        Assert.Null(compiled);
        Assert.Equal(ActionRejectionReason.BadConditionsJson, rejection.Reason);
    }

    [Fact]
    public void APlantedStructureOverspendFailsAtCompileNamingRungAndAxis()
    {
        var row = BaseRow(rung: 1, conditionsJson: """{"leaf":"sideIs","subject":"target","value":"zombie"}""");
        var table = OneRung(1, Array.Empty<string>());

        var (rejection, compiled) = ActionCompiler.Compile(
            row, Array.Empty<ActionCostRow>(), Array.Empty<ActionScopeRow>(), OneAtomContainer, boardAvailable: false, table);

        Assert.Null(compiled);
        Assert.Equal(ActionRejectionReason.StructureExceedsBudget, rejection.Reason);
    }

    [Fact]
    public void ASuccessfulCompileProducesTargetSpecTwoAndAScaledCost()
    {
        var row = BaseRow(rung: 1);
        var costs = new[] { new ActionCostRow("skill.test", "stamina", new ValueSpec(10, 20, RollPolicy.OnApply), ActionCostTiming.OnCommit) };
        var table = OneRung(1, Array.Empty<string>(), costMulti: 2000); // 2x

        var (rejection, compiled) = ActionCompiler.Compile(
            row, costs, Array.Empty<ActionScopeRow>(), OneAtomContainer, boardAvailable: false, table);

        Assert.True(rejection.IsOk, rejection.ToString());
        Assert.NotNull(compiled);
        Assert.False(compiled!.Targeting.IsSelf);
        Assert.Equal(2, compiled.Targeting.PerSide.Length); // one per caster side (A2 S2)
        Assert.Single(compiled.Costs);
        Assert.Equal(20, compiled.Costs[0].ScaledAmount.Min); // 10 * 2000milli
        Assert.Equal(40, compiled.Costs[0].ScaledAmount.Max); // 20 * 2000milli
    }

    [Fact]
    public void ASuccessfulCompileWithARealConditionProducesAWorkingPredicate()
    {
        var row = BaseRow(rung: 3,
            conditionsJson: """{"leaf":"sideIs","subject":"target","value":"zombie"}""");
        var table = OneRung(3, new[] { StructureAxes.Condition });

        var (rejection, compiled) = ActionCompiler.Compile(
            row, Array.Empty<ActionCostRow>(), Array.Empty<ActionScopeRow>(), OneAtomContainer, boardAvailable: false, table);

        Assert.True(rejection.IsOk, rejection.ToString());
        Assert.NotSame(PredicateCompiler.Always, compiled!.Condition); // a real compiled leaf, not the no-op
    }

    [Fact]
    public void NoJsonIsParsedAfterLoadEvaluatingTheCompiledConditionAllocatesZeroBytes()
    {
        var row = BaseRow(rung: 3,
            conditionsJson: """{"leaf":"sideIs","subject":"target","value":"zombie"}""");
        var table = OneRung(3, new[] { StructureAxes.Condition });
        var (rejection, compiled) = ActionCompiler.Compile(
            row, Array.Empty<ActionCostRow>(), Array.Empty<ActionScopeRow>(), OneAtomContainer, boardAvailable: false, table);
        Assert.True(rejection.IsOk, rejection.ToString());

        var self = new EntityFacts(0, 1, 1000, -1, -1, -1, false, false, 0);
        var target = new EntityFacts(1, 2, 1000, -1, -1, -1, false, false, 0);

        for (var i = 0; i < 1000; i++) { var f = new FactReader(self, target); compiled!.Condition.Evaluate(ref f); } // warm

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100_000; i++) { var f = new FactReader(self, target); compiled!.Condition.Evaluate(ref f); }
        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    // ---- ActionCatalog / ActionCatalogHost ---------------------------------------------------

    [Fact]
    public void GetReturnsNullForAnUnknownActionId()
    {
        var catalog = ActionCatalog.Build(Array.Empty<CompiledAction>());
        Assert.Null(catalog.Get("skill.nope"));
    }

    static CompiledAction Compiled(string id, long revision = 1) => new(
        id, ActionKind.Skill, 1, Array.Empty<ActionTag>(), true, revision, false, false, "item.test",
        ActionEnvelope.NoOp with { ActionId = id }, new CompiledTargetSpec(true, Array.Empty<TargetSpec>()),
        0, 0, null, false, PredicateCompiler.Always, Array.Empty<CompiledActionCost>(), Array.Empty<ActionScopeRow>());

    [Fact]
    public void BuildIndexesByActionId()
    {
        var catalog = ActionCatalog.Build(new[] { Compiled("skill.a"), Compiled("skill.b") });
        Assert.Equal(2, catalog.Count);
        Assert.Equal("skill.a", catalog.Get("skill.a")!.ActionId);
        Assert.Equal("skill.b", catalog.Get("skill.b")!.ActionId);
    }

    [Fact]
    public void RevisionSwapIsAtomicABattleInFlightKeepsItsCatalog()
    {
        var host = new ActionCatalogHost();
        host.Swap(ActionCatalog.Build(new[] { Compiled("skill.a", revision: 1) }));

        var battleInFlightCatalog = host.Current; // captured BEFORE the swap, like a running battle would

        host.Swap(ActionCatalog.Build(new[] { Compiled("skill.a", revision: 2) }));

        Assert.Equal(1, battleInFlightCatalog.Get("skill.a")!.Revision); // the old reference never changes
        Assert.Equal(2, host.Current.Get("skill.a")!.Revision); // new readers see the new one
    }
}
