using System.Text.Json;
using FusionRpg.Core.Delve.Domains;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>One `dungeon_domain` row's own storage-only fields (`DomainRow`, D4.15, deliberately
/// carries none of these — provenance/staleness are storage concerns, not catalog-validation ones,
/// per that type's own doc comment). Read-only today: D4.16 left the import WRITE arm honestly
/// unbuilt (no real anchor JSON exists anywhere yet to validate a parser against), so nothing
/// populates this table in production — this read exists so D4.22's endpoint has something real to
/// call the day content lands, proven against a manually-seeded row rather than a fabricated writer.</summary>
public sealed record DomainStorageRow(DomainRow Domain, string ProvenanceJson, string ValidatedJson, long Revision);

/// <summary>One `rpg_domain_progress.clears_json` entry — `{rungId, oath, delveId}`, spec-domain-catalog.md
/// §5's exact shape. `RungId` also carries a tail step's own label (`rungIdOrTailLabel`, §5a) — the
/// column is untyped text either way, matching `PlayerClears`' own `RungIds`/`TailSteps` split being a
/// Core-side, not a storage-side, distinction.</summary>
public sealed record DomainClearJsonRow(string RungId, bool Oath, long DelveId);

/// <summary>One `rpg_domain_progress` row — discovery plus every clear. "Highest rung cleared is
/// derived from `clears_json`, never stored" (spec §5, verbatim) — there is no separate column for it
/// here either.</summary>
public sealed record DomainProgressRow(
    long PlayerId, string DomainId, string FoundVia, string FoundRef,
    IReadOnlyList<DomainClearJsonRow> Clears, long Revision);

public sealed partial class RpgStore
{
    /// <summary>D4.18 (spec-domain-catalog.md §1, §5) — the three domain tables. `dungeon_domain` /
    /// `dungeon_domain_pool` land here so the schema exists ahead of the import write arm that targets
    /// them (D4.16 left that arm honestly unbuilt — no anchor JSON exists anywhere yet to validate a
    /// parser against); `rpg_domain_progress` is this table's own, fully wired below. Called from
    /// <c>EnsureWorldSchemaUnlocked</c> right after <c>EnsureDelveSchemaUnlocked</c> — a domain is what
    /// a delve is entered into, the same "schema setup lives beside the sibling it composes with"
    /// reasoning <see cref="EnsureDelveSchemaUnlocked"/>'s own doc comment states.</summary>
    void EnsureDomainsSchemaUnlocked(SqliteConnection db)
    {
        Exec(db, """
            CREATE TABLE IF NOT EXISTS dungeon_domain (
              domain_id TEXT PRIMARY KEY,
              name TEXT NOT NULL, flavor TEXT NOT NULL,
              theme TEXT NOT NULL, climate TEXT NOT NULL, danger_band TEXT NOT NULL,
              entry TEXT NOT NULL,
              layout_template_id TEXT NOT NULL, boss_species_ref TEXT NOT NULL, retinue_family TEXT,
              entrance_hint TEXT NOT NULL,
              permadeath_from_rung TEXT,
              provenance_json TEXT NOT NULL, validated_json TEXT NOT NULL,
              revision INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS dungeon_domain_pool (
              domain_id TEXT NOT NULL, pool TEXT NOT NULL,
              seq INTEGER NOT NULL, key TEXT NOT NULL DEFAULT '', ref_id TEXT NOT NULL,
              PRIMARY KEY (domain_id, pool, seq)
            );

            CREATE TABLE IF NOT EXISTS rpg_domain_progress (
              player_id INTEGER NOT NULL, domain_id TEXT NOT NULL,
              found_via TEXT NOT NULL, found_ref TEXT NOT NULL,
              clears_json TEXT NOT NULL DEFAULT '[]',
              revision INTEGER NOT NULL DEFAULT 0,
              PRIMARY KEY (player_id, domain_id)
            );
            """);
    }

    /// <summary>D4.18 (spec §5b) — first discovery only: `found_via`/`found_ref` are set once and never
    /// overwritten (`ON CONFLICT DO NOTHING`, the `item_first_clear` idiom applied to an upsert instead
    /// of a plain insert-if-absent, since this row also carries `clears_json` for the SAME primary key).
    /// Public, self-contained transaction — no real caller composes this into a larger one yet
    /// (`DomainDiscovery`/`ApplyExpeditionRewards` wiring is D4.20's own task).</summary>
    public void RecordFound(long playerId, string domainId, string foundVia, string foundRef)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            RecordFoundUnlocked(db, tx, playerId, domainId, foundVia, foundRef);
            tx.Commit();
        }
    }

    /// <summary>The tx-scoped primitive spec §5b cites — `internal` (this project's own
    /// `InternalsVisibleTo("FusionRpg.Data.Tests")`, the same test-only seam
    /// <c>RpgStore.WorldGraphDiff.cs</c> already documents) so it is directly testable ahead of having
    /// a real composing caller, and so `DomainDiscovery`'s own transaction (D4.20) can call it without
    /// opening a second one.</summary>
    internal static void RecordFoundUnlocked(SqliteConnection db, SqliteTransaction tx, long playerId, string domainId, string foundVia, string foundRef)
    {
        using var cmd = Prepared(db, tx,
            """
            INSERT INTO rpg_domain_progress (player_id, domain_id, found_via, found_ref, clears_json, revision)
            VALUES ($p, $d, $fv, $fr, '[]', 0)
            ON CONFLICT(player_id, domain_id) DO NOTHING;
            """,
            "$p", "$d", "$fv", "$fr");
        ExecuteWith(cmd, playerId, domainId, foundVia, foundRef);
    }

    /// <summary>D4.18 (spec §5a) — appends to `clears_json` iff no entry already carries that
    /// `rungId` — "exactly once per (player, domain, rung)" even under a replayed caller. Public,
    /// self-contained transaction; see <see cref="RecordFound"/>'s own doc comment for why a public
    /// wrapper exists ahead of a real composing caller.</summary>
    public void RecordDomainClear(long playerId, string domainId, string rungIdOrTailLabel, bool oath, long delveId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            RecordDomainClearUnlocked(db, tx, playerId, domainId, rungIdOrTailLabel, oath, delveId);
            tx.Commit();
        }
    }

    /// <summary>The tx-scoped primitive spec §5a cites verbatim: "Inside `RpgStore.Delve.CloseDelve`
    /// — the writer every sibling names — ... `RecordDomainClearUnlocked(db, tx, playerId, domainId,
    /// rungIdOrTailLabel, oath, delveId)` appends to `clears_json` iff no entry carries that `rungId`."
    /// Wiring this INTO `CloseDelve`'s own already-open transaction is D4.20/D4.21's task, not this
    /// one's (this file's own Files line names only `DomainStaleness.cs` and this file) — a domain
    /// must be found before it can be entered (`DelveStart.Run`'s own `domain.not-found` refusal,
    /// D4.21), so a missing progress row here is unreachable in production and is left as a silent
    /// no-op rather than a defensive throw for a caller this module does not yet have.</summary>
    internal static void RecordDomainClearUnlocked(SqliteConnection db, SqliteTransaction tx, long playerId, string domainId, string rungIdOrTailLabel, bool oath, long delveId)
    {
        var current = ReadDomainProgressClearsJsonUnlocked(db, tx, playerId, domainId);
        if (current is null) return;

        var clears = JsonSerializer.Deserialize<List<DomainClearJsonRow>>(current) ?? new List<DomainClearJsonRow>();
        if (clears.Any(c => string.Equals(c.RungId, rungIdOrTailLabel, StringComparison.Ordinal))) return;

        clears.Add(new DomainClearJsonRow(rungIdOrTailLabel, oath, delveId));
        var json = JsonSerializer.Serialize(clears);
        using var cmd = Prepared(db, tx,
            "UPDATE rpg_domain_progress SET clears_json = $j, revision = revision + 1 WHERE player_id = $p AND domain_id = $d;",
            "$j", "$p", "$d");
        ExecuteWith(cmd, json, playerId, domainId);
    }

    static string? ReadDomainProgressClearsJsonUnlocked(SqliteConnection db, SqliteTransaction tx, long playerId, string domainId)
    {
        using var cmd = Prepared(db, tx, "SELECT clears_json FROM rpg_domain_progress WHERE player_id = $p AND domain_id = $d;", "$p", "$d");
        cmd.Parameters["$p"].Value = playerId;
        cmd.Parameters["$d"].Value = domainId;
        using var r = cmd.ExecuteReader();
        return r.Read() ? r.GetString(0) : null;
    }

    /// <summary>One player's one domain-progress row, or null if never found — read-back for
    /// <c>DomainOffers.For</c> (D4.19's own first parameter).</summary>
    public DomainProgressRow? ReadDomainProgress(long playerId, string domainId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT found_via, found_ref, clears_json, revision FROM rpg_domain_progress WHERE player_id = $p AND domain_id = $d;";
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$d", domainId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            var clears = JsonSerializer.Deserialize<List<DomainClearJsonRow>>(r.GetString(2)) ?? new List<DomainClearJsonRow>();
            return new DomainProgressRow(playerId, domainId, r.GetString(0), r.GetString(1), clears, r.GetInt64(3));
        }
    }

    /// <summary>Every domain this player has found, ordinal by `domainId` — <c>DomainOffers.For</c>'s
    /// own "per **found** domain, in `domainId` ordinal order" (spec §4).</summary>
    public IReadOnlyList<DomainProgressRow> ReadDomainProgressForPlayer(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT domain_id, found_via, found_ref, clears_json, revision FROM rpg_domain_progress WHERE player_id = $p ORDER BY domain_id;";
            cmd.Parameters.AddWithValue("$p", playerId);
            using var r = cmd.ExecuteReader();
            var rows = new List<DomainProgressRow>();
            while (r.Read())
            {
                var clears = JsonSerializer.Deserialize<List<DomainClearJsonRow>>(r.GetString(3)) ?? new List<DomainClearJsonRow>();
                rows.Add(new DomainProgressRow(playerId, r.GetString(0), r.GetString(1), r.GetString(2), clears, r.GetInt64(4)));
            }
            return rows;
        }
    }

    /// <summary>D4.22 — every `dungeon_domain` row, ordinal by `domainId`. Closes D4.18's own named
    /// gap (schema-only, no read/write) now that an endpoint actually needs one; still no WRITE side
    /// — nothing populates this table until D4.16's import arm lands.</summary>
    public IReadOnlyList<DomainStorageRow> ReadDomains()
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT domain_id, name, flavor, theme, climate, danger_band, entry, layout_template_id,
                       boss_species_ref, retinue_family, entrance_hint, permadeath_from_rung,
                       provenance_json, validated_json, revision
                FROM dungeon_domain ORDER BY domain_id;
                """;
            using var r = cmd.ExecuteReader();
            var rows = new List<DomainStorageRow>();
            while (r.Read())
            {
                var domain = new DomainRow(
                    r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
                    r.GetString(5), r.GetString(6), r.GetString(7), r.GetString(8),
                    r.IsDBNull(9) ? null : r.GetString(9), r.GetString(10), r.IsDBNull(11) ? null : r.GetString(11));
                rows.Add(new DomainStorageRow(domain, r.GetString(12), r.GetString(13), r.GetInt64(14)));
            }
            return rows;
        }
    }
}
