using FusionRpg.Core.Actions.Rungs;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Actions.Corpus;

public sealed class ActionCorpusComposeRejection : Exception
{
    public ActionCorpusComposeRejection(string message) : base(message) { }
}

/// <summary>One atom candidate dropped from the pool because it did not match the container's own
/// single roll-class (mirroring <c>UniqueContainerBuild.DroppedCandidate</c> exactly — measured and
/// reported, never a hard refusal, since the rest of the pool is still perfectly valid).</summary>
public readonly record struct DroppedAtomCandidate(string Family, string AtomId, string Reason);

public sealed record ActionCorpusComposeResult(
    ActionRow Row,
    IReadOnlyList<ActionCostRow> Costs,
    ContainerRow Container,
    IReadOnlyList<DroppedAtomCandidate> Dropped);

/// <summary>
/// T59.3 (spec-action-instance-and-grant.md §1, corrected during BUILD): brief → concrete row. Pure —
/// no `RpgStore` call anywhere in this file, matching this module's own stated boundary. The atom-pool
/// half of this method is `UniqueContainerBuild.From`'s own established shape, generalized from one
/// family to the brief's `atomFamilies` list — reused, not reinvented (see the spec's own ⛔ note for
/// the full trace of why `ActionSeeder.Generate` does NOT apply here).
/// </summary>
public static class ActionCorpusComposer
{
    public static ActionCorpusComposeResult Compose(
        ActionCorpusBrief brief,
        ActionCorpusCostTemplate costTemplate,
        RungTable rungTable,
        Func<string, IReadOnlyList<AtomRow>> atomsInFamily,
        Func<string, AtomRow?> lookupAtom)
    {
        ArgumentNullException.ThrowIfNull(brief);
        ArgumentNullException.ThrowIfNull(costTemplate);
        ArgumentNullException.ThrowIfNull(rungTable);
        ArgumentNullException.ThrowIfNull(atomsInFamily);
        ArgumentNullException.ThrowIfNull(lookupAtom);

        if (!ActionCategories.TryParse(brief.Category, out var category))
            throw new ActionCorpusComposeRejection($"brief '{brief.Id}': unknown category '{brief.Category}'");

        if (!EligibilityScopes.TryParse(brief.Scope, out var scope))
            throw new ActionCorpusComposeRejection($"brief '{brief.Id}': unknown scope '{brief.Scope}'");

        if (!ActionTargetModes.TryParse(brief.TargetMode, out var targetMode))
            throw new ActionCorpusComposeRejection($"brief '{brief.Id}': unknown targetMode '{brief.TargetMode}'");

        if (!ActionRelations.TryParse(brief.Relation, out var relation))
            throw new ActionCorpusComposeRejection($"brief '{brief.Id}': unknown relation '{brief.Relation}'");

        var rungBand = new RungBand(brief.RungFloor, brief.RungCeiling);
        var rung = rungBand.Collapse();
        if (!rungTable.TryGet(rung, out var rungRow))
            throw new ActionCorpusComposeRejection(
                $"brief '{brief.Id}': rungBand ceiling {rung} has no row in the loaded rung table");

        // Reject naming the category, never default silently (T59.1's own contract) -- this module's
        // own acceptance criterion, exercised for real rather than assumed from CategoryOf's doc comment.
        var costTemplateRow = costTemplate.CategoryOf(category);

        // ---- pool composition: UniqueContainerBuild.From's shape, generalized to N families ----
        var candidates = new List<(string Family, AtomRow Atom)>();
        foreach (var family in brief.AtomFamilies)
            foreach (var atom in atomsInFamily(family))
                candidates.Add((family, atom));

        if (candidates.Count == 0)
            throw new ActionCorpusComposeRejection(
                $"brief '{brief.Id}': none of its atomFamilies ({string.Join(", ", brief.AtomFamilies)}) resolved any atom");

        var rollClass = AffixValidator.AffixClassOfAtom(candidates[0].Atom);
        var pool = new List<ContainerPoolRow>();
        var dropped = new List<DroppedAtomCandidate>();
        // Every pool candidate is a single-atom affix THIS method generates itself
        // (AffixLibraryGenerator.SingleAtomAffix is a pure, zero-model-call function of the atom
        // alone) -- built here, in memory, rather than requiring a caller-supplied affix lookup to
        // already hold it. Confirmed by a real run against a real RpgStore: `store.GetAffix` only
        // resolves an affix someone already persisted via UpsertAffix, which nothing does for a
        // generated single-atom affix -- there is no reason this module should depend on that ever
        // having happened, since it can reconstruct the exact same affix deterministically itself.
        var generatedAffixes = new Dictionary<string, AffixRow>(StringComparer.Ordinal);
        foreach (var (family, atom) in candidates)
        {
            var atomClass = AffixValidator.AffixClassOfAtom(atom);
            if (atomClass != rollClass)
            {
                dropped.Add(new DroppedAtomCandidate(family, atom.AtomId,
                    $"family '{family}' mixes {rollClass} and {atomClass} atoms; the container's one roll is {rollClass}-side"));
                continue;
            }
            var affix = AffixLibraryGenerator.SingleAtomAffix(atom);
            generatedAffixes[affix.AffixId] = affix;
            pool.Add(new ContainerPoolRow(affix.AffixId, Weight: 1));
        }

        // ContainerValidator's own grammar is `^(item|trait|skill|...)\.[a-z0-9-]+$` -- ONE dot after
        // the kind prefix, no more. A brief's own id is dot-segmented ("action.family.cactus.001"),
        // so the dots become dashes here -- found by running this composer against the REAL
        // ContainerValidator for the first time (T59.4), not caught by T59.3's own unit tests, which
        // never called UpsertContainer at all.
        var containerId = ContainerRow.PrefixOf(ContainerKind.Skill) + "." + brief.Id.Replace('.', '-');
        var draftContainer = new ContainerRow
        {
            ContainerId = containerId,
            Kind = ContainerKind.Skill,
            PrefixRolls = rollClass == AffixClass.Prefix ? rungRow.PoolRolls : 0,
            SuffixRolls = rollClass == AffixClass.Suffix ? rungRow.PoolRolls : 0,
            Pool = pool,
        };

        // Content-seeded, never per-player (spec §Objective point 3) -- the brief's own stable id is
        // the ONLY input, so two servers importing the same brief draw byte-identical atoms.
        var rollSeed = unchecked((long)Fnv1a64(brief.Id));
        AffixRow? LookupGeneratedAffix(string id) => generatedAffixes.TryGetValue(id, out var a) ? a : null;
        var atomIds = Instantiator.Draw(draftContainer, lookupAtom, LookupGeneratedAffix, rollSeed);
        if (atomIds.Count == 0)
            throw new ActionCorpusComposeRejection($"brief '{brief.Id}': drew zero atoms from its own composed pool");

        // The roll happens ONCE, here -- the persisted container carries the drawn atoms as its
        // PERMANENT fixed core, pool discarded, matching "a granted action has no instance and no
        // rolls" exactly: nothing will ever call Instantiator.Draw on this container again.
        var finalContainer = draftContainer with
        {
            Atoms = atomIds.Select((id, i) => new ContainerAtomRow(i, id)).ToList(),
            Pool = Array.Empty<ContainerPoolRow>(),
            PrefixRolls = 0,
            SuffixRolls = 0,
        };

        var row = new ActionRow
        {
            ActionId = brief.Id,
            Name = brief.Name,
            // item-content `granted-action-text` (T14): carried through verbatim, never derived.
            // A key the corpus authored is the one the string catalog is authored against; a key
            // this composer invented from the id would silently miss the catalog row.
            DescriptionKey = brief.DescriptionKey,
            Kind = ActionKind.Skill,
            Rung = rung,
            RungBand = rungBand,
            Enabled = true,
            Grantable = true,
            DefaultAttackEligible = false,
            ContainerId = containerId,
            Envelope = ActionEnvelope.NoOp with
            {
                ActionId = brief.Id,
                CooldownChannel = ActionCategoryChannels.CooldownChannelFor(category),
                EffectivenessChannel = ActionCategoryChannels.EffectivenessChannelFor(category),
            },
            Targeting = new ActionTargetSpec { Mode = targetMode, Relation = relation },
            MinRange = 0,
            MaxRange = int.MaxValue,
            Scope = scope,
            ScopeKey = brief.ScopeKey,
            Category = category,
        };

        var costs = new[]
        {
            new ActionCostRow(brief.Id, costTemplateRow.ResourceId, ValueSpec.Of(costTemplateRow.BaseAmountAtRung1), costTemplateRow.Timing),
        };

        return new ActionCorpusComposeResult(row, costs, finalContainer, dropped);
    }

    /// <summary>Standard FNV-1a 64-bit — a public-domain hash, not app-specific logic, so mirroring
    /// (not calling) `SeededRng`'s own private "seed XOR stable name" shape is correct here: that
    /// method is private to a different concern (a battle's live RNG stream), while this seeds a
    /// one-time, content-only roll with no relationship to any battle seed.</summary>
    static ulong Fnv1a64(string text)
    {
        var h = 14695981039346656037UL;
        foreach (var ch in text)
        {
            h ^= ch;
            h *= 1099511628211UL;
        }
        return h;
    }
}
