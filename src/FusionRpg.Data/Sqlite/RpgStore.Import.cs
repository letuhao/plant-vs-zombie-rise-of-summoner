using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Data;

/// <summary>What one import did, or refused to do.</summary>
/// <param name="Committed">False means nothing was written — see <paramref name="Errors"/>.</param>
/// <param name="RowsChanged">
/// How many writes actually altered a row. Zero on a repeat import of unchanged files, which is what
/// makes the content hash stand still and the catalog revision hold.
/// </param>
public sealed record ImportOutcome(
    bool Committed,
    IReadOnlyList<SeedError> Errors,
    int Atoms,
    int Containers,
    int Curves,
    int Rarities,
    int Elements,
    int ChannelPolicies,
    int RowsChanged,
    long CatalogRevision,
    ContentHashStamp? ContentHash)
{
    public bool IsOk => Errors.Count == 0;
}

/// <summary>
/// The seed import (E14a) — validate everything, then write everything, in one transaction.
///
/// <para><b>All or nothing, and validate-first is not the same guarantee.</b> Validating every row
/// before the first write stops a known-bad file from landing half a catalog. The single transaction
/// stops a crash, a locked database or a constraint nobody predicted from doing the same thing. Both
/// are needed: a partial import produces a content hash for a state nobody authored, and the hash is
/// what every downstream replay verdict trusts.</para>
///
/// <para><b>The revision is bumped once, and only when something changed.</b> Once per transaction
/// rather than per row, or a fifty-row file would move it fifty times. Not at all on a no-op import,
/// because <c>catalog_revision</c> is what E6 reproduces against and what E19 negotiates on — a bump
/// for content that did not change makes every connected receiver re-download the full push.</para>
///
/// <para>It lives here rather than in the importer tool for two reasons that are not the same. The
/// write path needs this class's private connection, gate and unlocked writers, so it could not live
/// anywhere else without opening them. And <c>guard-dal.ps1</c> scans only <c>src/</c> — SQL under
/// <c>tools/</c> is unguarded, so the rule that keeps SQL in one project would stop applying exactly
/// where a tool was tempted to write its own. (A tool <i>can</i> be tested: <c>ItemSeedValidator</c>
/// has a test project and so does this one. Untestability was not the reason.)</para>
/// </summary>
public sealed partial class RpgStore
{
    /// <param name="dryRun">
    /// Run the whole thing and roll back. Not the same as validating the files: this resolves every
    /// cross-table reference against the real catalog and lets the database itself refuse a write,
    /// which is what an author wants to know before an import lands.
    /// </param>
    public ImportOutcome ImportContent(SeedContent content, bool dryRun = false)
    {
        if (content is null) throw new ArgumentNullException(nameof(content));

        var errors = new List<SeedError>();

        lock (_gate)
        {
            // ---- read what is already stored, before any write ---------------------------------
            //
            // Every read below opens and closes its own connection. They must all finish before the
            // write transaction begins: reading through a second connection while this one holds the
            // write lock would block rather than answer.

            var atomsById = ListAtoms().ToDictionary(a => a.AtomId, StringComparer.Ordinal);
            var curvesById = new Dictionary<string, CurveTable>(StringComparer.Ordinal);
            var ordinalOwner = ListRarities().ToDictionary(r => r.Ordinal, r => r.RarityId);

            // ---- validate ----------------------------------------------------------------------

            // The reader refuses an id authored in two files. This catches the same mistake in a
            // hand-built batch, where keeping the last one would make the import order-dependent —
            // and it runs first, because a duplicate makes every count below a lie.
            RefuseDuplicates(content, errors);

            ValidateCurves(content, curvesById, errors);

            // Incoming atoms overlay the stored ones so a container may reference an atom authored
            // in the same import — the common case for a new item and its affixes.
            ValidateAtoms(content, atomsById, curvesById, errors);
            ValidateRarities(content, ordinalOwner, errors);

            // Containers whose stored copy is byte-identical are skipped, not rewritten: `revision`
            // is a hashed column and an identical rewrite would move the content hash.
            var containersToWrite = ValidateContainers(content, atomsById, errors);
            var elementTable = ValidateElements(content, errors);
            var rosterToWrite = elementTable is not null && !SameRoster(GetElementTable(), elementTable)
                ? elementTable
                : null;
            var policyRows = ValidateChannelPolicyContent(content, errors);

            if (errors.Count > 0)
                return new ImportOutcome(false, errors, 0, 0, 0, 0, 0, 0, 0, GetCatalogRevision(), null);

            // ---- write --------------------------------------------------------------------------

            var changed = 0;

            using (var db = OpenUnlocked())
            using (var tx = db.BeginTransaction())
            {
                // Curves first: an atom that scales through one is only meaningful once it exists.
                foreach (var c in content.Curves)
                    changed += WriteCurveUnlocked(db, tx, c.CurveId, c.Input, c.Points);

                foreach (var r in content.Rarities)
                {
                    UpsertRarityUnlocked(db, tx, r, out var rows);
                    changed += rows;
                }

                foreach (var a in content.Atoms)
                    changed += UpsertAtomUnlocked(db, a, tx);

                foreach (var c in containersToWrite)
                {
                    WriteContainerUnlocked(db, tx, c);
                    changed++;
                }

                // The roster is replaced whole, and only when the import carries one AND it differs:
                // a file set with no element rows must not wipe the roster the database holds, and an
                // unchanged roster must not be rewritten. The matrices are delete-then-insert, so
                // "did anything change" cannot be read off the row counts — it is decided before the
                // write, against the stored table.
                if (rosterToWrite is not null)
                {
                    WriteElementTableUnlocked(db, tx, rosterToWrite);
                    changed++;
                }

                foreach (var row in policyRows)
                    changed += UpsertChannelPolicyRowUnlocked(db, tx, row);

                if (changed > 0)
                    ExecIn(db, tx,
                        "UPDATE content_meta SET catalog_revision = catalog_revision + 1 WHERE id = 1;");

                // A dry run rolls back by disposing without committing — the writes still happened,
                // so anything the database itself would have refused has already thrown.
                if (!dryRun) tx.Commit();
            }

            // The counts are what the import was GIVEN, not what it wrote — a container skipped as
            // already-identical is still content this import covered. `RowsChanged` is the one that
            // says whether anything moved, and reporting the two differently would make an idempotent
            // re-import look like content going missing.
            return new ImportOutcome(
                !dryRun, errors,
                content.Atoms.Count, content.Containers.Count, content.Curves.Count, content.Rarities.Count,
                content.Elements.Count, content.ChannelPolicies.Count, changed,
                GetCatalogRevision(), ComputeContentHash());
        }
    }

    // ---- validation ---------------------------------------------------------------------------

    static void ValidateCurves(
        SeedContent content, Dictionary<string, CurveTable> into, List<SeedError> errors)
    {
        foreach (var c in content.Curves)
        {
            var check = CurveTable.TryCreate(c.CurveId, c.Input, c.Points, out var table);
            if (!check.IsOk)
            {
                errors.Add(Error(content, c.CurveId, check));
                continue;
            }
            into[c.CurveId] = table!;
        }
    }

    void ValidateAtoms(
        SeedContent content,
        Dictionary<string, AtomRow> atomsById,
        Dictionary<string, CurveTable> incomingCurves,
        List<SeedError> errors)
    {
        // A curve authored in this import resolves before a stored one of the same id: the file is
        // what is being imported, so its shape is the one the atom is being validated against.
        CurveInput? CurveInputOfBatch(string id) =>
            incomingCurves.TryGetValue(id, out var c) ? c.Input : CurveInputOf(id);

        foreach (var a in content.Atoms)
        {
            var check = AtomRowValidator.Validate(a, CurveInputOfBatch, ComposeKindOf);
            if (!check.IsOk)
            {
                errors.Add(Error(content, a.AtomId, check));
                continue;
            }
            atomsById[a.AtomId] = a;
        }
    }

    static void ValidateRarities(
        SeedContent content, Dictionary<int, string> ordinalOwner, List<SeedError> errors)
    {
        foreach (var r in content.Rarities)
        {
            if (r.MinTier > r.MaxTier)
            {
                errors.Add(Error(content, r.RarityId, AtomRejection.Fail(
                    AtomRejectionReason.BadParamValue,
                    $"tier window [{r.MinTier}, {r.MaxTier}] is inverted")));
                continue;
            }

            // Append-only ordinals, checked against the batch as well as the table: two new bands
            // claiming one ordinal would otherwise be decided by insertion order.
            if (ordinalOwner.TryGetValue(r.Ordinal, out var owner) &&
                !string.Equals(owner, r.RarityId, StringComparison.Ordinal))
            {
                errors.Add(Error(content, r.RarityId, AtomRejection.Fail(
                    AtomRejectionReason.DuplicateKey,
                    $"ordinal {r.Ordinal} already belongs to '{owner}' — ordinals are append-only")));
                continue;
            }

            ordinalOwner[r.Ordinal] = r.RarityId;
        }
    }

    List<ContainerRow> ValidateContainers(
        SeedContent content, Dictionary<string, AtomRow> atomsById, List<SeedError> errors)
    {
        var write = new List<ContainerRow>();

        foreach (var c in content.Containers)
        {
            var check = ContainerValidator.Validate(
                c, id => atomsById.TryGetValue(id, out var a) ? a : null);
            if (!check.IsOk)
            {
                errors.Add(Error(content, c.ContainerId, check));
                continue;
            }

            if (!SameContent(GetContainer(c.ContainerId), c)) write.Add(c);
        }

        return write;
    }

    /// <summary>
    /// One id, one row — across all four kinds, in one namespace.
    ///
    /// <para>Four namespaces that only overlap by accident is the more expensive rule to hold, and a
    /// container named after an atom is a mistake either way.</para>
    /// </summary>
    static void RefuseDuplicates(SeedContent content, List<SeedError> errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in content.Atoms.Select(a => a.AtomId)
                     .Concat(content.Containers.Select(c => c.ContainerId))
                     .Concat(content.Curves.Select(c => c.CurveId))
                     .Concat(content.Rarities.Select(r => r.RarityId)))
        {
            if (!seen.Add(id))
                errors.Add(Error(content, id, AtomRejection.Fail(
                    AtomRejectionReason.DuplicateKey, "the same id appears twice in one import")));
        }
    }

    /// <summary>
    /// The roster an import carries, or null when it carries none.
    ///
    /// <para>Absent means "leave the roster alone", never "empty it" — the four content folders are
    /// swept independently and a run that touched only atoms must not retire every element.</para>
    /// </summary>
    ElementTable? ValidateElements(SeedContent content, List<SeedError> errors)
    {
        if (content.Elements.Count == 0 && content.ElementMatrix.Count == 0) return null;

        if (content.Elements.Count == 0)
        {
            errors.Add(Error(content, "(elements)", AtomRejection.Fail(
                AtomRejectionReason.MissingParam,
                "matchup cells were authored with no roster; the cells name elements that would not exist")));
            return null;
        }

        var known = content.Elements.Select(e => e.ElementId).ToHashSet(StringComparer.Ordinal);
        foreach (var (matrix, row) in content.ElementMatrix)
        {
            foreach (var side in new[] { row.Attacker, row.Defender })
                if (!known.Contains(side))
                    errors.Add(Error(content, $"{matrix}:{row.Attacker}>{row.Defender}",
                        AtomRejection.Fail(AtomRejectionReason.BadParamValue,
                            $"'{side}' is not in the roster this import carries")));
        }

        // The append-only ordinal rules live in UpsertElementTable, which reads the stored roster.
        return new ElementTable(
            content.Elements,
            content.ElementMatrix.Where(m => m.Matrix == "combat").Select(m => m.Row).ToList(),
            content.ElementMatrix.Where(m => m.Matrix == "shield").Select(m => m.Row).ToList());
    }

    /// <summary>
    /// The channel policy rows an import carries (E22), converted to the Data-side row and checked
    /// against the same rule <c>UpsertChannelPolicies</c> enforces outside an import.
    /// </summary>
    static List<ChannelPolicyRow> ValidateChannelPolicyContent(SeedContent content, List<SeedError> errors)
    {
        var rows = content.ChannelPolicies
            .Select(r => new ChannelPolicyRow(r.ChannelId, r.Direction))
            .ToList();

        var reason = ValidateChannelPolicyRows(rows);
        if (reason is not null)
            errors.Add(Error(content, "(channel-policy)", AtomRejection.Fail(AtomRejectionReason.BadParamValue, reason)));

        return rows;
    }

    /// <summary>
    /// Whether a re-imported roster says anything new.
    ///
    /// <para>The matrices are written delete-then-insert, so the row counts always look like a change
    /// and cannot answer this. Importing an unchanged roster twice was bumping
    /// <c>catalog_revision</c> — with the content hash correctly standing still, which is the pair of
    /// symptoms that says a change counter is lying rather than that content moved.</para>
    /// </summary>
    static bool SameRoster(ElementTable stored, ElementTable incoming) =>
        stored.Elements.SequenceEqual(incoming.Elements)
        && Same(stored.CombatRows, incoming.CombatRows)
        && Same(stored.ShieldRows, incoming.ShieldRows);

    static bool Same(IReadOnlyList<ElementMatrixRow> a, IReadOnlyList<ElementMatrixRow> b) =>
        a.Count == b.Count
        && a.OrderBy(r => r.Attacker, StringComparer.Ordinal).ThenBy(r => r.Defender, StringComparer.Ordinal)
            .SequenceEqual(
                b.OrderBy(r => r.Attacker, StringComparer.Ordinal).ThenBy(r => r.Defender, StringComparer.Ordinal));

    /// <summary>A rejection, told where it came from. The file path is the half an author can act on.</summary>
    static SeedError Error(SeedContent content, string id, AtomRejection rejection) =>
        new(content.SourceOf.TryGetValue(id, out var path) ? path : "(caller-supplied)",
            id, rejection.Reason, rejection.Detail);
}
