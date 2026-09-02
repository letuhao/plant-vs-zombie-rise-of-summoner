using System.Text.Json;

namespace FusionRpg.Core.Effects.Atoms;

/// <summary>
/// Judges one <see cref="AffixRow"/> before it reaches the tables (`affix-schema`, T3.1,
/// `definitions.md` §4a). Same law as <see cref="ContainerValidator"/> — a bad affix is rejected
/// <b>whole</b>, with its id and reason.
/// </summary>
public static class AffixValidator
{
    /// <param name="lookupAtom">Resolves a concrete atom id against the loaded catalog.</param>
    /// <param name="domainMembers">
    /// A slot's domain (e.g. `"element"`) to its concrete members (e.g. the six elements) — supplied
    /// by the caller, which owns the real vocabularies (<c>ElementRoster</c>, etc.); this validator
    /// stays free of I/O and of a hardcoded domain list.
    /// </param>
    /// <param name="familyVariantHasAnyTier">
    /// True when at least one atom row exists for the given family+variant pair, at any tier — a
    /// slot's pattern names a family/variant, never a concrete tier (tier resolves later, module 2),
    /// so "the domain member resolves" means "some tier of this variant exists," not "this exact id
    /// exists."
    /// </param>
    public static AtomRejection Validate(
        AffixRow affix, Func<string, AtomRow?> lookupAtom,
        Func<string, IReadOnlyList<string>>? domainMembers = null,
        Func<string, string, bool>? familyVariantHasAnyTier = null)
    {
        if (affix is null) return AtomRejection.Fail(AtomRejectionReason.BadParamValue, "null affix");
        if (string.IsNullOrWhiteSpace(affix.AffixId))
            return Fail(affix, AtomRejectionReason.BadParamValue, "affix_id is empty");
        if (affix.Refs.Count == 0)
            return Fail(affix, AtomRejectionReason.BadParamValue, "an affix bundle needs at least one ref");

        var seqs = new HashSet<int>();
        var concreteAtomsSeen = new HashSet<string>(StringComparer.Ordinal);
        var derivedKinds = new HashSet<AffixClass>();

        foreach (var r in affix.Refs)
        {
            if (!seqs.Add(r.Seq))
                return Fail(affix, AtomRejectionReason.DuplicateSeq, $"seq {r.Seq} appears twice");

            var isConcrete = r.AtomId is not null;
            var isSlot = r.IsSlot;

            if (isConcrete == isSlot)
                // Both null/both set — never a legal ref (record invariant, checked here because the
                // C# type does not enforce the exclusivity by itself).
                return Fail(affix, AtomRejectionReason.BadParamValue,
                    $"seq {r.Seq}: exactly one of AtomId or (SlotName, SlotDomain) must be set");

            if (isConcrete)
            {
                var atom = lookupAtom(r.AtomId!);
                if (atom is null)
                    return Fail(affix, AtomRejectionReason.UnknownAtom, r.AtomId!);
                if (!concreteAtomsSeen.Add(r.AtomId!))
                    return Fail(affix, AtomRejectionReason.DuplicateAtomInContainer,
                        $"{r.AtomId} appears twice in one bundle");

                derivedKinds.Add(AffixClassOfAtom(atom));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(r.SlotAtomPattern) || !r.SlotAtomPattern!.Contains("$" + r.SlotName))
                    return Fail(affix, AtomRejectionReason.BadParamValue,
                        $"seq {r.Seq}: slot_atom_pattern must reference '${r.SlotName}'");
                if (r.SlotPick <= 0)
                    return Fail(affix, AtomRejectionReason.BadParamValue,
                        $"seq {r.Seq}: slot '{r.SlotName}' pick count must be positive, got {r.SlotPick}");

                // A patterned ref must resolve for EVERY member of its domain at load — a missing
                // element row is a load-time rejection, never a roll-time surprise (definitions §4a).
                if (domainMembers is not null && familyVariantHasAnyTier is not null)
                {
                    var members = domainMembers(r.SlotDomain!);
                    if (members.Count == 0)
                        return Fail(affix, AtomRejectionReason.BadParamValue,
                            $"seq {r.Seq}: unknown slot domain '{r.SlotDomain}'");

                    foreach (var member in members)
                    {
                        var (family, variant) = SubstitutePattern(r.SlotAtomPattern!, r.SlotName!, member);
                        if (!familyVariantHasAnyTier(family, variant))
                            return Fail(affix, AtomRejectionReason.UnknownAtom,
                                $"seq {r.Seq}: slot '{r.SlotName}' domain member '{member}' has no atom row " +
                                $"(pattern '{r.SlotAtomPattern}' -> family '{family}' variant '{variant}')");
                    }
                }

                // A slot ref's class cannot be derived until a concrete atom is chosen (module 2) —
                // it never forces the bundle's class on its own; only concrete refs do.
            }
        }

        var expectedClass = derivedKinds.Count switch
        {
            0 => affix.Class,          // an all-slot bundle: class was authored ahead of resolution;
                                        // trusted here, re-derivable once module 2 resolves a slot.
            1 => derivedKinds.Single(),
            _ => AffixClass.Mixed,     // A1: a bundle spanning both kinds consumes one of each roll.
        };
        if (derivedKinds.Count > 0 && affix.Class != expectedClass)
            return Fail(affix, AtomRejectionReason.BadParamValue,
                $"affix_class '{affix.Class}' does not match its refs' derived class '{expectedClass}' — " +
                "affixClass is derived, never authored (seed-contract.md §2.1)");

        return AtomRejection.Ok;
    }

    /// <summary>Splits <c>"atom.elemental-power.$E1"</c> + member <c>"fire"</c> into
    /// <c>("atom.elemental-power", "fire")</c> — family is everything before the placeholder,
    /// variant is the substituted domain member.</summary>
    static (string Family, string Variant) SubstitutePattern(string pattern, string slotName, string member)
    {
        var placeholder = "$" + slotName;
        var idx = pattern.IndexOf(placeholder, StringComparison.Ordinal);
        var family = pattern[..idx].TrimEnd('.');
        return (family, member);
    }

    /// <summary>An atom with no trigger of its own is a permanent modifier and derives `Prefix`; one
    /// that declares a trigger is triggered and derives `Suffix` — `seed-contract.md` §2.1's rule.
    /// Reads the ATOM's own <see cref="AtomRow.WhenJson"/> (whether this specific instance carries a
    /// trigger), never <c>AtomKindRegistry</c> (which only says what triggers a KIND may accept, not
    /// whether one is present — <c>stat.modify</c> permits a trigger and is still a permanent
    /// modifier when a given atom doesn't use one). Mirrors <c>AtomCompiler.TriggerOf</c>'s own
    /// `when_json` reading exactly. <c>internal</c> (T3.5, `affix-library`) — a THIRD caller
    /// (<see cref="AffixLibraryGenerator"/>) is what tipped this from "kept local" to "widen without
    /// breaking existing callers," the same precedent <c>Instantiator.Draw</c>'s own visibility
    /// widening set at T31.</summary>
    internal static AffixClass AffixClassOfAtom(AtomRow atom) =>
        TriggerOf(atom.WhenJson) is null ? AffixClass.Prefix : AffixClass.Suffix;

    static string? TriggerOf(string? whenJson)
    {
        if (string.IsNullOrWhiteSpace(whenJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(whenJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("trigger", out var t)
                && t.ValueKind == JsonValueKind.String
                ? t.GetString()
                : null;
        }
        catch (JsonException) { return null; }
    }

    static AtomRejection Fail(AffixRow affix, AtomRejectionReason reason, string detail) =>
        AtomRejection.Fail(reason, $"{affix.AffixId}: {detail}");
}
