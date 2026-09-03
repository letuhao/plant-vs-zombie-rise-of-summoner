using System.Linq;
using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Rungs;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Effects.Atoms.Power;

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
    ///
    /// <para><b>A-G1 (spec-tier-access-gate.md §3.2) adds a power-budget stage after compile
    /// succeeds</b> — the rung-keyed sibling of `ContentValidation.Budget`, checked here because this
    /// is the one real, production, bulk-scale path every authored action already passes through
    /// (`WebMatchService`'s battle-resolve calls). An over-budget container is treated the same as a
    /// compile failure: skipped, reported through <paramref name="onRejected"/>, never silently
    /// included and never clamped.</para>
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

            ContainerRow? container = null;
            IReadOnlyCollection<string>? containerAtomIds;
            if (string.IsNullOrEmpty(row.ContainerId))
            {
                containerAtomIds = Array.Empty<string>();
            }
            else
            {
                container = GetContainer(row.ContainerId);
                containerAtomIds = container?.Atoms.Select(a => a.AtomId).ToHashSet();
            }

            var (rejection, action) = ActionCompiler.Compile(
                row, costs, scopes, containerAtomIds, boardAvailable: false, rungTable);

            if (action is null)
            {
                onRejected?.Invoke(actionId, rejection);
                continue;
            }

            // A-G1 (spec-tier-access-gate.md §3.2): this is the real production caller for the
            // rung-keyed power budget -- every action this store holds passes through here on the
            // way to a battle-usable catalog (WebMatchService's own three call sites). Only the FIXED
            // core (`container.Atoms`) is priced, matching the atom set this method already uses for
            // scope validation above: action content is authored/generated as a fixed set (A-S1's
            // distribution planner bakes `poolRolls` atoms into the container at generation time),
            // never a runtime-weighted draw the way an item's `Pool` is. A container the loaded rung
            // table cannot price (no `powerBudgetMilli` column -- e.g. `action-rungs.v1.json`) is
            // skipped, not failed, the same "skip, do not guess" rule the check itself already uses
            // for a missing ceiling.
            if (container is not null)
            {
                var localContainer = container;
                var budget = ContentValidation.Budget(
                    new[] { localContainer },
                    _ => localContainer.Atoms
                        .Select(a => GetAtom(a.AtomId))
                        .Where(a => a is not null)
                        .Select(a => a!)
                        .ToList(),
                    _ => row.Rung,
                    rung => rungTable.TryGet(rung, out var rr) ? rr.PowerBudgetMilli : null);

                if (!budget.Ok)
                {
                    // ContentFinding.ToString() carries the container id (its own `Subject`), never
                    // just `Detail` alone -- the whole point of §3.2's "a finding naming the
                    // container id" is that the id survives into whatever reads the rejection.
                    onRejected?.Invoke(actionId, ActionRejection.Fail(
                        ActionRejectionReason.PowerBudgetExceeded, budget.Failures.First().ToString()));
                    continue;
                }
            }

            compiled.Add(action);
        }

        return ActionCatalog.Build(compiled);
    }
}
