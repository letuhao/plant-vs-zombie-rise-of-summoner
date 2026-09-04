using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Mutation;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>
/// The mutation head as the store holds it — the five columns this module adds to
/// <c>effect_instance</c>.
/// </summary>
public sealed record InstanceMutationHead(
    string InstanceId, int EnhanceLevel, int PityCounter, int MutationSeq, string? StateHash, string? OriginValuesJson);

/// <summary>What an append did. <c>Replayed</c> is clause 8's idempotent retry.</summary>
public sealed record MutationAppendResult(bool Ok, bool Replayed, int Seq, string Reason);

/// <summary>
/// <c>effect_instance_op</c> — the mutation ledger (D2 §9 clause 2), plus the five head columns and
/// <c>effect_instance_atom.suppressed</c> (clause 9). The only schema module 15 owns.
///
/// <para>⚠ <c>origin_catalog_revision</c> is NOT a new column: it already exists as
/// <c>effect_instance.catalog_revision</c>, and D2 §7.1 granted it as a <b>semantic lock</b> —
/// origin-only, no operation rewrites it. I6 §5.1's request for a new column was refused.</para>
/// </summary>
public sealed partial class RpgStore
{
    void EnsureInstanceOpSchemaUnlocked(SqliteConnection db)
    {
        // The head columns. CREATE TABLE IF NOT EXISTS is a no-op against a database created before
        // this module, so the additions have to be explicit -- the same migration shape T3.4 used.
        EnsureColumn(db, "effect_instance", "enhance_level", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "effect_instance", "enhance_pity_counter", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "effect_instance", "mutation_seq", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "effect_instance", "state_hash", "TEXT");
        // D2 rung 1', written LAZILY at first mutation (D2 §11.3's lean): an item that is never
        // mutated never pays for a second copy of its own numbers.
        EnsureColumn(db, "effect_instance", "origin_values_json", "TEXT");
        // D2 clause 9 -- an identity change is suppress-then-append. The row stays, seq is never
        // renumbered and no op row is ever deleted.
        EnsureColumn(db, "effect_instance_atom", "suppressed", "INTEGER NOT NULL DEFAULT 0");

        Exec(db, """
            -- D2 §9 clause 2: the ledger. UNIQUE(instance_id, correlation_id) is the second net
            -- under the caller's own check, exactly as rpg_material_spend_log is under the recipe
            -- gate: a race that slips past the read still cannot double-apply an operation.
            CREATE TABLE IF NOT EXISTS effect_instance_op (
              instance_id    TEXT NOT NULL,
              op_seq         INTEGER NOT NULL,
              op_kind        TEXT NOT NULL,
              correlation_id TEXT NOT NULL,
              op_seed        INTEGER NOT NULL,
              result_json    TEXT NOT NULL,
              applied_utc    TEXT NOT NULL,
              -- D2 clause 5: the op stamps its OWN catalog revision and rules version. Neither is
              -- effect_instance.catalog_revision, which is origin-only and which no operation rewrites.
              catalog_revision INTEGER NOT NULL DEFAULT 0,
              rules_version    INTEGER NOT NULL DEFAULT 0,
              -- D2 clause 11: the spend, in module 14's material vocabulary. "A spent cost with no op
              -- is theft; an op with no cost is duplication."
              cost_json        TEXT NOT NULL DEFAULT '{}',
              PRIMARY KEY (instance_id, op_seq),
              FOREIGN KEY (instance_id) REFERENCES effect_instance(instance_id) ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_effect_instance_op_correlation
              ON effect_instance_op(instance_id, correlation_id);
            """);
    }

    public InstanceMutationHead? GetInstanceMutationHead(string instanceId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT enhance_level, enhance_pity_counter, mutation_seq, state_hash, origin_values_json
                FROM effect_instance WHERE instance_id = $id;
                """;
            cmd.Parameters.AddWithValue("$id", instanceId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new InstanceMutationHead(
                instanceId,
                r.GetInt32(0), r.GetInt32(1), r.GetInt32(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4));
        }
    }

    /// <summary>
    /// Append one op and rewrite the head, <b>in one transaction</b> (the Boundaries: "commit op row,
    /// material debit and head rewrite in one transaction" — the debit is the caller's
    /// <see cref="TrySpendRecipe"/>, which runs its own `perform` delegate inside its own).
    ///
    /// <para>Clause 8 — a replayed <c>correlation_id</c> returns the RECORDED result rather than
    /// applying anything; a reused one carrying different parameters is refused, never silently
    /// applied.</para>
    /// </summary>
    public MutationAppendResult AppendMutationOp(
        string instanceId, MutationOpKind kind, string correlationId, long opSeed,
        MutationResult result, string? newStateHash, string? originValuesJson, string appliedUtc,
        long catalogRevision = 0, int rulesVersion = 0, string costJson = "{}")
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("a mutation op needs a correlation id — it is what makes a retry idempotent", nameof(correlationId));

        var resultJson = MutationCanonical.WriteResult(result);
        var kindId = MutationOpKinds.Id(kind);

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();

            using (var existing = db.CreateCommand())
            {
                existing.Transaction = tx;
                existing.CommandText = """
                    SELECT op_seq, op_kind, result_json FROM effect_instance_op
                    WHERE instance_id = $id AND correlation_id = $cid;
                    """;
                existing.Parameters.AddWithValue("$id", instanceId);
                existing.Parameters.AddWithValue("$cid", correlationId);
                using var r = existing.ExecuteReader();
                if (r.Read())
                {
                    var seq = r.GetInt32(0);
                    var sameKind = string.Equals(r.GetString(1), kindId, StringComparison.Ordinal);
                    var sameResult = string.Equals(r.GetString(2), resultJson, StringComparison.Ordinal);
                    return sameKind && sameResult
                        ? new MutationAppendResult(true, Replayed: true, seq, "replay")
                        : new MutationAppendResult(false, Replayed: false, seq,
                            $"correlation '{correlationId}' was already applied to '{instanceId}' with different parameters — refused, never silently applied");
                }
            }

            int nextSeq;
            using (var head = db.CreateCommand())
            {
                head.Transaction = tx;
                head.CommandText = "SELECT mutation_seq, enhance_level FROM effect_instance WHERE instance_id = $id;";
                head.Parameters.AddWithValue("$id", instanceId);
                using var r = head.ExecuteReader();
                if (!r.Read())
                    return new MutationAppendResult(false, false, 0, $"no effect_instance '{instanceId}'");
                nextSeq = r.GetInt32(0) + 1;
                var level = r.GetInt32(1) + result.EnhanceLevelDelta;
                if (level < 0)
                    return new MutationAppendResult(false, false, nextSeq,
                        "the op takes the enhancement level below zero");
            }

            // The one legal ceiling in the module, and it THROWS -- an absolute bound derived from
            // the arithmetic, never a silent clamp (AGENTS.md). It bounds a retry loop and a log's
            // length, not how strong an item may become.
            if (nextSeq > MutationLimits.MutationSeqCap)
                throw new OverflowException(
                    $"instance '{instanceId}' would reach mutation_seq {nextSeq}, past the structural cap of " +
                    $"{MutationLimits.MutationSeqCap}. This is a retry-loop bound, not a design ceiling — it refuses rather than wrapping");

            ExecIn(db, tx, """
                INSERT INTO effect_instance_op
                  (instance_id, op_seq, op_kind, correlation_id, op_seed, result_json, applied_utc,
                   catalog_revision, rules_version, cost_json)
                VALUES ($id, $seq, $kind, $cid, $seed, $json, $utc, $rev, $rules, $cost);
                """,
                ("$id", instanceId), ("$seq", nextSeq), ("$kind", kindId), ("$cid", correlationId),
                ("$seed", opSeed), ("$json", resultJson), ("$utc", appliedUtc),
                ("$rev", catalogRevision), ("$rules", rulesVersion), ("$cost", costJson));

            ExecIn(db, tx, """
                UPDATE effect_instance
                SET mutation_seq = $seq,
                    enhance_level = enhance_level + $delta,
                    state_hash = $hash,
                    origin_values_json = COALESCE(origin_values_json, $origin)
                WHERE instance_id = $id;
                """,
                ("$seq", nextSeq), ("$delta", result.EnhanceLevelDelta), ("$hash", (object?)newStateHash ?? DBNull.Value),
                ("$origin", (object?)originValuesJson ?? DBNull.Value), ("$id", instanceId));

            foreach (var seq in result.Suppressed)
                ExecIn(db, tx, "UPDATE effect_instance_atom SET suppressed = 1 WHERE instance_id = $id AND seq = $seq;",
                    ("$id", instanceId), ("$seq", seq));

            tx.Commit();
            return new MutationAppendResult(true, Replayed: false, nextSeq, "");
        }
    }

    /// <summary>Set the pity counter. Separate from the append because a failed attempt moves the
    /// counter without appending a value delta, and a guarantee resets it.</summary>
    public void SetInstancePityCounter(string instanceId, int counter)
    {
        if (counter < 0) throw new ArgumentOutOfRangeException(nameof(counter), counter, "a pity counter cannot be negative");
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            ExecIn(db, tx, "UPDATE effect_instance SET enhance_pity_counter = $c WHERE instance_id = $id;",
                ("$c", counter), ("$id", instanceId));
            tx.Commit();
        }
    }

    /// <summary>The transcript, dense and in order.</summary>
    public IReadOnlyList<MutationOp> ReadMutationOps(string instanceId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT op_seq, op_kind, correlation_id, op_seed, result_json, applied_utc,
                       catalog_revision, rules_version, cost_json
                FROM effect_instance_op WHERE instance_id = $id ORDER BY op_seq;
                """;
            cmd.Parameters.AddWithValue("$id", instanceId);
            using var r = cmd.ExecuteReader();

            var ops = new List<MutationOp>();
            while (r.Read())
            {
                if (!MutationOpKinds.TryParse(r.GetString(1), out var kind))
                    throw new InvalidOperationException(
                        $"instance '{instanceId}' op {r.GetInt32(0)} carries op_kind '{r.GetString(1)}', which is not in the closed namespace");
                ops.Add(new MutationOp(instanceId, r.GetInt32(0), kind, r.GetString(2), r.GetInt64(3),
                    MutationCanonical.ReadResult(r.GetString(4)), r.GetString(5),
                    r.GetInt64(6), r.GetInt32(7), r.GetString(8)));
            }

            return ops;
        }
    }

    /// <summary>
    /// Seed <c>reroll_cost_mult</c>'s rung leg for every rung. Deliberately its own method rather
    /// than folded into <see cref="SeedRarityLadder"/>, so module 7's seeding never grows a
    /// dependency on a later module's tuning file — the precedent module 14 set with
    /// <c>SeedSalvageYield</c>. Idempotent: safe on every boot.
    /// </summary>
    public void SeedRerollCostMult(EnhancementTuning tuning)
    {
        for (var i = 0; i < RarityLadder.RungIds.Count; i++)
            SetRarityBudget(RarityLadder.RungIds[i], "reroll_cost_mult", RerollPolicy.RungLegMilli(i, tuning));
    }
}
