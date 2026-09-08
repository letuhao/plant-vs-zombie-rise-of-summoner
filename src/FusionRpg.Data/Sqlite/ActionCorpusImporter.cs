using FusionRpg.Core.Actions.Corpus;
using FusionRpg.Core.Actions.Rungs;

namespace FusionRpg.Data;

/// <summary>
/// T59.4 (spec-action-instance-and-grant.md §1): wires T59.3's pure composition to real persistence.
/// Given already-parsed briefs (file I/O is `FusionRpg.Server`'s startup step's job, never this
/// class's — tunables-ssot.md §7.2), composes each and writes it through the three already-built,
/// already-idempotent upserts (`UpsertContainer`/`UpsertAction`/`UpsertCost` each skip the write, and
/// the revision bump, when nothing differs — T30/E14a's own guard, exercised here, not re-implemented).
/// One brief's rejection is reported and skipped, never fatal to the rest of the batch — the same
/// "whole-row rejection, never partial" posture `RpgStore.BuildActionCatalog` already uses one level
/// up.
/// </summary>
public static class ActionCorpusImporter
{
    public readonly record struct BriefOutcome(string BriefId, bool Imported, string? Rejection);

    public sealed record ImportResult(IReadOnlyList<BriefOutcome> Outcomes)
    {
        public int ImportedCount => Outcomes.Count(o => o.Imported);
        public int RejectedCount => Outcomes.Count(o => !o.Imported);
    }

    public static ImportResult Import(
        RpgStore store,
        IReadOnlyList<ActionCorpusBrief> briefs,
        ActionCorpusCostTemplate costTemplate,
        RungTable rungTable)
    {
        if (store is null) throw new ArgumentNullException(nameof(store));
        if (briefs is null) throw new ArgumentNullException(nameof(briefs));

        var outcomes = new List<BriefOutcome>(briefs.Count);
        foreach (var brief in briefs)
        {
            ActionCorpusComposeResult composed;
            try
            {
                composed = ActionCorpusComposer.Compose(
                    brief, costTemplate, rungTable,
                    store.ListAtomsByFamily, store.GetAtom);
            }
            catch (Exception ex) when (ex is ActionCorpusComposeRejection or ActionCorpusCostTemplateRejection)
            {
                outcomes.Add(new BriefOutcome(brief.Id, Imported: false, ex.Message));
                continue;
            }

            var containerCheck = store.UpsertContainer(composed.Container);
            if (!containerCheck.IsOk)
            {
                outcomes.Add(new BriefOutcome(brief.Id, Imported: false, $"container: {containerCheck.Detail}"));
                continue;
            }

            var actionCheck = store.UpsertAction(composed.Row);
            if (!actionCheck.IsOk)
            {
                outcomes.Add(new BriefOutcome(brief.Id, Imported: false, $"action: {actionCheck.Detail}"));
                continue;
            }

            var costFailure = (string?)null;
            foreach (var cost in composed.Costs)
            {
                var costCheck = store.UpsertCost(cost);
                if (!costCheck.IsOk) costFailure = $"cost: {costCheck.Detail}";
            }

            outcomes.Add(costFailure is null
                ? new BriefOutcome(brief.Id, Imported: true, null)
                : new BriefOutcome(brief.Id, Imported: false, costFailure));
        }

        return new ImportResult(outcomes);
    }
}
