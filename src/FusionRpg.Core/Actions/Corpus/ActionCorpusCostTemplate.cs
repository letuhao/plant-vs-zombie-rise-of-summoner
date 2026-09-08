using System.Text.Json;

namespace FusionRpg.Core.Actions.Corpus;

/// <summary>One category's default onCommit cost row (spec-action-instance-and-grant.md §2, T59.1 —
/// narrowed during BUILD: the only per-category number `ActionTimingTuning` does not already own).
/// Used by the corpus importer (T59.3) only when a brief itself carries no cost data of its own.</summary>
public readonly record struct ActionCorpusCostTemplateRow(string ResourceId, int BaseAmountAtRung1, ActionCostTiming Timing);

public sealed class ActionCorpusCostTemplateRejection : Exception
{
    public ActionCorpusCostTemplateRejection(string message) : base(message) { }
}

/// <summary>Every `ActionCategory` must carry a row — a missing one is a REJECTION naming which
/// category, never a silent default (tunables-ssot.md's own reject-not-default discipline, the same
/// shape `ActionTimingTuning.CategoryOf` already uses for envelope timing).</summary>
public sealed record ActionCorpusCostTemplate(IReadOnlyDictionary<ActionCategory, ActionCorpusCostTemplateRow> Categories)
{
    public ActionCorpusCostTemplateRow CategoryOf(ActionCategory category) =>
        Categories.TryGetValue(category, out var row) ? row : throw new ActionCorpusCostTemplateRejection(
            $"action-corpus-cost-templates tuning: no categories entry for '{category}'. Every ActionCategory " +
            "value must carry a cost row in config — there is no built-in default to fall back to.");
}

/// <summary>Pure parser over `data/tuning/action-corpus-cost-templates.v1.json` — no file I/O
/// (tunables-ssot.md §7.2: "Core never reads a file. Hosts load and inject."), mirroring
/// `ActionTimingTuningLoader`'s own established shape exactly.</summary>
public static class ActionCorpusCostTemplateLoader
{
    static readonly IReadOnlyDictionary<ActionCategory, string> CategoryKeys = new Dictionary<ActionCategory, string>
    {
        [ActionCategory.Attack] = "attack",
        [ActionCategory.Defense] = "defense",
        [ActionCategory.Support] = "support",
        [ActionCategory.Movement] = "movement",
        [ActionCategory.Status] = "status",
    };

    public static ActionCorpusCostTemplate Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ActionCorpusCostTemplateRejection("action-corpus-cost-templates tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new ActionCorpusCostTemplateRejection($"action-corpus-cost-templates tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("categories", out var categoriesEl) || categoriesEl.ValueKind != JsonValueKind.Object)
                throw new ActionCorpusCostTemplateRejection("action-corpus-cost-templates tuning: missing or non-object 'categories'");

            var categories = new Dictionary<ActionCategory, ActionCorpusCostTemplateRow>();
            foreach (var (category, key) in CategoryKeys)
            {
                if (!categoriesEl.TryGetProperty(key, out var catEl) || catEl.ValueKind != JsonValueKind.Object)
                    throw new ActionCorpusCostTemplateRejection($"action-corpus-cost-templates tuning: missing categories.{key}");

                if (!catEl.TryGetProperty("resourceId", out var resourceIdEl) || resourceIdEl.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(resourceIdEl.GetString()))
                    throw new ActionCorpusCostTemplateRejection($"action-corpus-cost-templates tuning: missing or empty categories.{key}.resourceId");

                if (!catEl.TryGetProperty("baseAmountAtRung1", out var amountEl) || amountEl.ValueKind != JsonValueKind.Number
                    || !amountEl.TryGetInt32(out var amount))
                    throw new ActionCorpusCostTemplateRejection($"action-corpus-cost-templates tuning: missing or non-integer categories.{key}.baseAmountAtRung1");
                if (amount <= 0)
                    throw new ActionCorpusCostTemplateRejection($"action-corpus-cost-templates tuning: categories.{key}.baseAmountAtRung1 must be > 0; got {amount}");

                if (!catEl.TryGetProperty("timing", out var timingEl) || timingEl.ValueKind != JsonValueKind.String
                    || !TryParseTiming(timingEl.GetString(), out var timing))
                    throw new ActionCorpusCostTemplateRejection(
                        $"action-corpus-cost-templates tuning: categories.{key}.timing must be 'onCommit' or 'perTick'");

                categories[category] = new ActionCorpusCostTemplateRow(resourceIdEl.GetString()!, amount, timing);
            }

            return new ActionCorpusCostTemplate(categories);
        }
    }

    static bool TryParseTiming(string? raw, out ActionCostTiming timing)
    {
        switch (raw)
        {
            case "onCommit": timing = ActionCostTiming.OnCommit; return true;
            case "perTick": timing = ActionCostTiming.PerTick; return true;
            default: timing = default; return false;
        }
    }
}
