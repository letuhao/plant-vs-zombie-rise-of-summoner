using FusionRpg.Core.Effects.Atoms.Power;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>
/// The price tables (spec-power-vector.md, E9): <c>power_coefficient</c>,
/// <c>power_trigger_frequency</c>, and the sweep's side table <c>power_coefficient_proposal</c>.
///
/// <para><b>A sweep never overwrites a shipped number.</b> It writes proposals; a test reports the
/// gap; humans decide what ships. That is what makes "hand-authored now, fitted later" mechanical
/// rather than aspirational — and it is why the proposal table is deliberately <b>not</b> covered by
/// the content hash. A sweep running must not move a stamp, or every replay verdict downstream would
/// report a mismatch for a number nobody adopted.</para>
/// </summary>
public sealed partial class RpgStore
{
    void EnsurePowerSchemaUnlocked(SqliteConnection db)
    {
        Exec(db, """
            CREATE TABLE IF NOT EXISTS power_coefficient (
              kind_id         TEXT    NOT NULL,
              channel         TEXT    NOT NULL DEFAULT '',
              coeff_milli     INTEGER NOT NULL,
              reference_scale INTEGER NOT NULL,
              PRIMARY KEY (kind_id, channel)
            );

            CREATE TABLE IF NOT EXISTS power_trigger_frequency (
              trigger_id TEXT    NOT NULL PRIMARY KEY,
              per_minute INTEGER NOT NULL
            );

            -- The sweep's output. Uncovered by the content hash on purpose: a proposal is a
            -- suggestion, and a suggestion must not move a stamp.
            CREATE TABLE IF NOT EXISTS power_coefficient_proposal (
              kind_id         TEXT    NOT NULL,
              channel         TEXT    NOT NULL DEFAULT '',
              coeff_milli     INTEGER NOT NULL,
              reference_scale INTEGER NOT NULL,
              note            TEXT    NOT NULL DEFAULT '',
              PRIMARY KEY (kind_id, channel)
            );
            """);
    }

    /// <summary>The authored tables, or the hand-authored defaults when nothing has been imported.</summary>
    public PowerTables GetPowerTables()
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();

            var coefficients = ReadCoefficients(db, "power_coefficient");
            if (coefficients.Count == 0) return PowerTables.Authored();

            return new PowerTables(coefficients, ReadFrequencies(db));
        }
    }

    /// <summary>Replace both authored tables. Whole-table, in one transaction.</summary>
    public (bool Ok, string Reason) UpsertPowerTables(PowerTables tables)
    {
        if (tables is null) throw new ArgumentNullException(nameof(tables));

        foreach (var c in tables.Coefficients)
            if (c.ReferenceScale <= 0)
                return (false,
                    $"{c.KindId}/{(c.Channel.Length == 0 ? "*" : c.Channel)}: reference scale " +
                    $"{c.ReferenceScale} — normalisation divides by it, and a zero scale prices " +
                    "every magnitude alike, which is the units trap this column exists to close");

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            WritePowerTablesUnlocked(db, tx, tables);
            // C4 (completeness-audit.md): this direct API has no production caller today and no
            // skip-when-identical tracking to bump conditionally on, unlike the import path — bump
            // unconditionally so an E19 receiver actually re-negotiates after a policy edit through it.
            ExecIn(db, tx, "UPDATE content_meta SET catalog_revision = catalog_revision + 1 WHERE id = 1;");
            tx.Commit();
            return (true, "");
        }
    }

    void WritePowerTablesUnlocked(SqliteConnection db, SqliteTransaction tx, PowerTables tables)
    {
        ExecIn(db, tx, "DELETE FROM power_coefficient;");
        foreach (var c in tables.Coefficients)
            ExecIn(db, tx,
                "INSERT INTO power_coefficient (kind_id, channel, coeff_milli, reference_scale) " +
                "VALUES ($k, $c, $m, $r);",
                ("$k", c.KindId), ("$c", c.Channel ?? ""), ("$m", c.CoeffMilli), ("$r", c.ReferenceScale));

        ExecIn(db, tx, "DELETE FROM power_trigger_frequency;");
        foreach (var f in tables.Frequencies)
            ExecIn(db, tx,
                "INSERT INTO power_trigger_frequency (trigger_id, per_minute) VALUES ($t, $p);",
                ("$t", f.Trigger), ("$p", f.PerMinute));
    }

    /// <summary>
    /// Record what a sweep would like the coefficients to be. Never touches the authored table.
    /// </summary>
    public void UpsertCoefficientProposals(IReadOnlyList<(PowerCoefficientRow Row, string Note)> proposals)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();

            foreach (var (row, note) in proposals)
                ExecIn(db, tx, """
                    INSERT INTO power_coefficient_proposal
                      (kind_id, channel, coeff_milli, reference_scale, note)
                    VALUES ($k, $c, $m, $r, $n)
                    ON CONFLICT(kind_id, channel) DO UPDATE SET
                      coeff_milli = excluded.coeff_milli,
                      reference_scale = excluded.reference_scale,
                      note = excluded.note;
                    """,
                    ("$k", row.KindId), ("$c", row.Channel ?? ""), ("$m", row.CoeffMilli),
                    ("$r", row.ReferenceScale), ("$n", note ?? ""));

            tx.Commit();
        }
    }

    /// <summary>What the sweep proposed, beside what ships — the gap a test reports.</summary>
    public IReadOnlyList<(PowerCoefficientRow Proposed, string Note)> ListCoefficientProposals()
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText =
                "SELECT kind_id, channel, coeff_milli, reference_scale, note " +
                "FROM power_coefficient_proposal ORDER BY kind_id, channel;";
            using var r = cmd.ExecuteReader();

            var list = new List<(PowerCoefficientRow, string)>();
            while (r.Read())
                list.Add((new PowerCoefficientRow(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3)),
                    r.GetString(4)));
            return list;
        }
    }

    /// <summary>
    /// Price every atom and store the result in <c>power_json</c>.
    ///
    /// <para><b>An override is never overwritten.</b> <c>power_override_json</c> is a designer's
    /// decision with a required note; the computed base sits beside it, and E14b's drift test is what
    /// reports the gap. A backfill that clobbered overrides would erase exactly the record of where
    /// the cost function is bad.</para>
    ///
    /// <para><c>power_json</c> is a hashed column, so this moves the content hash — correctly, and
    /// attributably: prices are content.</para>
    /// </summary>
    /// <returns>How many atoms were priced, and the ids of those that could not be.</returns>
    public (int Priced, IReadOnlyList<string> Unpriced) BackfillAtomPower(PowerTables? tables = null)
    {
        var t = tables ?? GetPowerTables();
        var atoms = ListAtoms();
        var unpriced = new List<string>();
        var priced = 0;

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();

            foreach (var atom in atoms)
            {
                var result = CostFunction.Price(atom, t);
                if (!result.Ok)
                {
                    // Left NULL rather than written as zero. An unpriced atom must look unpriced, or
                    // a budget would happily accept a whole family that costs nothing.
                    unpriced.Add(atom.AtomId);
                    continue;
                }

                ExecIn(db, tx,
                    "UPDATE effect_atom SET power_json = $p, revision = revision + 1 " +
                    "WHERE atom_id = $id AND power_json IS NOT $p;",
                    ("$p", result.Power.ToJson()), ("$id", atom.AtomId));
                priced++;
            }

            tx.Commit();
        }

        return (priced, unpriced);
    }

    static List<PowerCoefficientRow> ReadCoefficients(SqliteConnection db, string table)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            $"SELECT kind_id, channel, coeff_milli, reference_scale FROM {table} ORDER BY kind_id, channel;";
        using var r = cmd.ExecuteReader();

        var list = new List<PowerCoefficientRow>();
        while (r.Read())
            list.Add(new PowerCoefficientRow(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3)));
        return list;
    }

    static List<TriggerFrequencyRow> ReadFrequencies(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT trigger_id, per_minute FROM power_trigger_frequency ORDER BY trigger_id;";
        using var r = cmd.ExecuteReader();

        var list = new List<TriggerFrequencyRow>();
        while (r.Read()) list.Add(new TriggerFrequencyRow(r.GetString(0), r.GetInt32(1)));
        return list;
    }
}
