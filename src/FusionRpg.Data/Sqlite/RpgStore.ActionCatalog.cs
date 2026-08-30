using System.Linq;
using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Rungs;

namespace FusionRpg.Data;

public sealed partial class RpgStore
{
    /// <summary>
    /// aura-skill T19 (audit D3 part two): the correctness half of the owner's "both" answer —
    /// equipped actions resolve properly rather than always hitting T3's degrade path. Compiles every
    /// authored `ActionRow` this store holds into a real `ActionCatalog`, the same
    /// validate-then-compile pipeline `ActionCompiler.Compile` already implements (T30) — this method
    /// adds no new compilation logic, only the bulk "load every row, compile it, collect what
    /// succeeds" loop nothing in production has ever run before.
    ///
    /// <para><b>A row that fails to compile is skipped, not fatal.</b> One bad row (an authoring
    /// mistake, a container that got deleted after the action referenced it) must not take down every
    /// OTHER action's ability to resolve — the same "whole-row rejection, never partial" discipline
    /// `AtomRowValidator` already uses, applied at the catalog-assembly level instead of the
    /// single-row level. <paramref name="onRejected"/> is the caller's own visibility into what got
    /// skipped and why (never silently swallowed).</para>
    ///
    /// <para><c>boardAvailable: false</c> throughout — battle is squad-vs-wave, not cell-based; `A10`
    /// (a real board) has not landed for this bind mode, matching `ActionValidator.ValidateAction`'s
    /// own documented default.</para>
    /// </summary>
    public ActionCatalog BuildActionCatalog(RungTable rungTable, Action<string, ActionRejection>? onRejected = null)
    {
        var compiled = new List<CompiledAction>();

        foreach (var actionId in ListActionIds())
        {
            var row = GetAction(actionId);
            if (row is null) continue; // raced with a delete between ListActionIds and GetAction

            var costs = ListCosts(actionId);
            var scopes = ListScopes(actionId);

            IReadOnlyCollection<string>? containerAtomIds;
            if (string.IsNullOrEmpty(row.ContainerId))
            {
                containerAtomIds = Array.Empty<string>();
            }
            else
            {
                var container = GetContainer(row.ContainerId);
                containerAtomIds = container?.Atoms.Select(a => a.AtomId).ToHashSet();
            }

            var (rejection, action) = ActionCompiler.Compile(
                row, costs, scopes, containerAtomIds, boardAvailable: false, rungTable);

            if (action is not null)
                compiled.Add(action);
            else
                onRejected?.Invoke(actionId, rejection);
        }

        return ActionCatalog.Build(compiled);
    }
}
