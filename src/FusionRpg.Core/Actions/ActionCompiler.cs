using System.Linq;
using System.Text.Json;
using FusionRpg.Core.Actions.Rungs;
using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Actions;

/// <summary>
/// T30 (spec-action-catalog.md §2): rows → runtime form, once, at load. Validate (reject, never
/// coerce) → structure-budget (R1) → compile (targeting, condition, cost bounds). Every stage can
/// fail; the first failure wins and nothing partial is ever handed back — the same "validate
/// everything before committing anything" shape this whole program already uses (T17's
/// <c>CostLedger</c>, T20's <c>UnlockDiscardService</c>).
/// </summary>
public static class ActionCompiler
{
    public static (ActionRejection Rejection, CompiledAction? Compiled) Compile(
        ActionRow row,
        IReadOnlyList<ActionCostRow> costs,
        IReadOnlyList<ActionScopeRow> scopes,
        IReadOnlyCollection<string>? containerAtomIds,
        bool boardAvailable,
        RungTable rungTable,
        Func<string, int>? statusBit = null,
        Func<string, int>? elementId = null,
        Func<string, int>? stockBit = null,
        ActionBindMode mode = ActionBindMode.Battle)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(costs);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(rungTable);

        var actionCheck = ActionValidator.ValidateAction(row, containerAtomIds, boardAvailable);
        if (!actionCheck.IsOk) return (actionCheck, null);

        foreach (var cost in costs)
        {
            var costCheck = ActionValidator.ValidateCost(cost);
            if (!costCheck.IsOk) return (costCheck, null);
        }

        foreach (var scope in scopes)
        {
            // containerAtomIds is non-null here -- ValidateAction above already rejected a null one.
            var scopeCheck = ActionValidator.ValidateScope(scope, containerAtomIds!);
            if (!scopeCheck.IsOk) return (scopeCheck, null);
        }

        var structureCheck = StructureBudgetGuard.Check(row, costs, scopes, rungTable);
        if (!structureCheck.IsOk) return (structureCheck, null);

        rungTable.TryGet(row.Rung, out var rungRow); // already proven present by the structure check above

        var conditionResult = CompileCondition(row, statusBit, elementId, stockBit, mode);
        if (!conditionResult.Rejection.IsOk) return (conditionResult.Rejection, null);

        var scaledCosts = new List<CompiledActionCost>(costs.Count);
        foreach (var cost in costs)
            scaledCosts.Add(new CompiledActionCost(cost.ResourceId, cost.AmountSpec.Scaled(rungRow.CostMulti), cost.When));

        var compiled = new CompiledAction(
            row.ActionId, row.Kind, row.Rung, row.Tags, row.Enabled, row.Revision, row.Grantable, row.DefaultAttackEligible,
            row.ContainerId, row.Envelope, TargetSpecCompiler.Compile(row.Targeting),
            row.MinRange, row.MaxRange, row.RangeChannel, row.RequiresLineOfSight,
            conditionResult.Compiled, scaledCosts, scopes, row.Category);

        return (ActionRejection.Ok, compiled);
    }

    static (ActionRejection Rejection, ICompiledPredicate Compiled) CompileCondition(
        ActionRow row, Func<string, int>? statusBit, Func<string, int>? elementId, Func<string, int>? stockBit, ActionBindMode mode)
    {
        if (string.IsNullOrWhiteSpace(row.ConditionsJson))
            return (ActionRejection.Ok, PredicateCompiler.Always);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(row.ConditionsJson); }
        catch (JsonException ex)
        {
            return (Fail(row.ActionId, ex.Message), PredicateCompiler.Always);
        }

        using (doc)
        {
            var readRejection = AtomJson.TryReadPredicate(doc.RootElement, out var tree);
            if (!readRejection.IsOk)
                return (Fail(row.ActionId, readRejection.Detail), PredicateCompiler.Always);

            if (tree is null)
                return (ActionRejection.Ok, PredicateCompiler.Always);

            // T10 mode matrix: PvZ lawn is a stateless observer and never reads current inventory --
            // a consumable action (one whose condition authors holdsStock) is simply not bindable
            // there. Checked on the parsed TREE, before compiling, so the refusal names the mode
            // rather than silently compiling a leaf that could never answer true.
            if (mode == ActionBindMode.Lawn && ContainsHoldsStock(tree))
                return (ActionRejection.Fail(ActionRejectionReason.ConsumableUnsupportedInMode,
                    $"{row.ActionId}: holdsStock is not bindable in {mode} mode -- the overlay never reads current inventory"), PredicateCompiler.Always);

            var compileRejection = PredicateCompiler.TryCompile(tree, statusBit, out var compiled, elementId, stockBit);
            return !compileRejection.IsOk
                ? (Fail(row.ActionId, compileRejection.Detail), PredicateCompiler.Always)
                : (ActionRejection.Ok, compiled);
        }
    }

    static bool ContainsHoldsStock(PredicateNode node) => node switch
    {
        PredicateNode.And a => a.Children.Any(ContainsHoldsStock),
        PredicateNode.Or o => o.Children.Any(ContainsHoldsStock),
        PredicateNode.Not n => ContainsHoldsStock(n.Child),
        PredicateNode.Leaf l => l.Id == LeafId.HoldsStock,
        _ => false,
    };

    static ActionRejection Fail(string actionId, string detail) =>
        ActionRejection.Fail(ActionRejectionReason.BadConditionsJson, $"{actionId}: {detail}");
}
