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
    /// an atom whose kind is not registered in <see cref="AtomKindRegistry"/> (18 kinds as of D56,
    /// 2026-09-07 — `element.convert` included: the conversion refusal this comment used to describe
    /// was removed the day the kind was actually built, per `spec-element-conversion.md` §2d's own
    /// "clears with zero code change in `tree-binder`" claim — checked directly and found to need one
    /// real line removed, since the old refusal keyed on the STRING "convert", never on the registry,
    /// so it would have kept refusing this kind forever even once registered).</summary>
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
        // D16/D56: `element.convert` is now a real, registered kind (spec-element-conversion.md,
        // built 2026-09-07) -- REMOVED 2026-09-07 the string-keyed "row.KindId.Contains('convert')"
        // refusal this comment used to describe. That check fired on the LITERAL SUBSTRING, never on
        // registry membership, so it would have kept refusing element.convert forever even after
        // registration -- the exact opposite of spec-element-conversion.md §2d's own claim ("clears
        // itself with zero code change... never keys on a hardcoded [count]"). The registry check
        // below is now the ONLY gate, matching that claim for real.
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
