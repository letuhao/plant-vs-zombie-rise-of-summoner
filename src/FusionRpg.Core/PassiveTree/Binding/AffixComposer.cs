using System.Text.Json;
using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.PassiveTree.Binding;

public sealed record ResolvedAtom(string KindId, string ChannelId, string Op, string? Trigger, string? WhenJson);

/// <summary>
/// Affix id -> atom rows (task B4, spec-tree-binder.md §1). A node is an affix inside a `skill`
/// container, never a bare atom — an affix is a named bundle of atom refs drawn together, which is
/// why a reflect node (two `stat.derived` atoms that must arrive together) needs an affix as its
/// unit rather than one row per atom. Resolves against the shipped seed content
/// (`AtomSeedFile.Collect`'s own `SeedContent`) — the same source `tools/ElementEnumGen` and every
/// other seed-reading tool already reads, never a second parser.
/// </summary>
public static class AffixComposer
{
    /// <summary>Resolves 1..3 affix ids, in the order given, to their atom refs in `seq` order
    /// within each affix. Refuses (never silently drops) a missing affix id, a missing atom id, or
    /// an atom whose kind is not one of the 17 registered — including the 18th, conversion, kind
    /// D16 never shipped (renumbered from 16/17th 2026-09-07: `AtomKindRegistry.KindCount` grew to
    /// 17 for unrelated reasons since this was first written, per `spec-element-conversion.md`/D56):
    /// any atom claiming to write an element-conversion payload is refused by name here, not bound
    /// as if it were ordinary content.</summary>
    public static IReadOnlyList<ResolvedAtom> Resolve(
        IReadOnlyList<string> affixIds,
        IReadOnlyDictionary<string, AffixRow> affixesById,
        IReadOnlyDictionary<string, AtomRow> atomsById)
    {
        if (affixIds.Count is < 1 or > 3)
            throw new BindRefusal($"a node's affixIds must be 1..3, got {affixIds.Count} (R6)");

        var resolved = new List<ResolvedAtom>();
        foreach (var affixId in affixIds)
        {
            if (!affixesById.TryGetValue(affixId, out var affix))
                throw new BindRefusal($"affix '{affixId}' does not exist in the shipped seed content");

            foreach (var reference in affix.Refs.OrderBy(r => r.Seq))
            {
                if (reference.AtomId is null)
                    throw new BindRefusal($"affix '{affixId}' ref seq {reference.Seq} names no atom id " +
                                          "(a slot-pattern ref — not a resolvable atom for a passive node)");
                if (!atomsById.TryGetValue(reference.AtomId, out var atomRow))
                    throw new BindRefusal($"affix '{affixId}' ref seq {reference.Seq} names atom " +
                                          $"'{reference.AtomId}', which does not exist in the shipped seed content");

                resolved.Add(ParseAtom(affixId, atomRow));
            }
        }
        return resolved;
    }

    static ResolvedAtom ParseAtom(string affixId, AtomRow row)
    {
        // The 18th atom kind (D16; renumbered from "17th" 2026-09-07 — AtomKindRegistry.KindCount
        // grew to 17 for unrelated reasons since this was written, so a still-unbuilt conversion
        // kind is now the 18th, not the 17th; see spec-element-conversion.md/D56): conversion is
        // not implemented anywhere in AtomKindRegistry's 17 rows, and quotas.exclusionForm/
        // conversionState allocate it zero nodes upstream (tree-plan/tree-language) — this is the
        // defensive backstop if one ever reaches here anyway. Checked BEFORE the registry lookup so
        // the message names the real reason, not a generic "unregistered kind".
        if (row.KindId.Contains("convert", StringComparison.OrdinalIgnoreCase))
            throw new BindRefusal(
                $"affix '{affixId}' atom '{row.AtomId}' has kind '{row.KindId}' — the 18th atom kind " +
                "(element conversion, D16) is not implemented; conversion nodes are refused by design, " +
                "never silently bound");

        if (AtomKindRegistry.Get(row.KindId) is null)
            throw new BindRefusal($"affix '{affixId}' atom '{row.AtomId}' has unregistered kind '{row.KindId}'");

        using var doc = JsonDocument.Parse(row.ParamsJson);
        var root = doc.RootElement;
        var channel = root.TryGetProperty("channel", out var chEl) ? chEl.GetString() ?? "" : "";
        var op = root.TryGetProperty("op", out var opEl) ? opEl.GetString() ?? "" : "";

        string? trigger = null;
        if (!string.IsNullOrEmpty(row.WhenJson) && row.WhenJson != "{}")
        {
            using var whenDoc = JsonDocument.Parse(row.WhenJson);
            if (whenDoc.RootElement.TryGetProperty("trigger", out var trigEl))
                trigger = trigEl.GetString();
        }

        return new ResolvedAtom(row.KindId, channel, op, trigger, null);
    }
}
