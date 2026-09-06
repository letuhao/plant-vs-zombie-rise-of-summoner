using FusionRpg.Core.Items.Materials;
using FusionRpg.Core.Items.Mutation;
using FusionRpg.Core.Items.Sockets;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>One material line a workbench operation MINTS rather than spends (upcycle's output, a
/// salvage yield). Deliberately its own type so a grant can never be handed to a spend by mistake.</summary>
public readonly record struct WorkbenchGrant(string MaterialId, long Qty);

/// <summary>One fungible-container stock movement — the insert an operation consumed.</summary>
public readonly record struct WorkbenchStockDelta(string ContainerId, int Delta);

/// <summary>
/// The persistent half of one workbench operation, already <b>decided</b> by Core. Every field is a
/// materialised result; nothing here is a formula, a tuning read or an RNG draw, which is what keeps
/// D2 clause 4 (<i>record the result, never the recipe</i>) true at the storage boundary rather than
/// only in the policy.
/// </summary>
/// <param name="Sockets">Module 16's <c>item_socket</c> SSOT write, or <c>null</c> when the verb
/// touches no socket. D2 clause 13: socket ops are appended for audit and idempotency only, so this
/// is the state and the op row is the receipt — never the other way round.</param>
/// <param name="PityCounter">Module 15's counter, or <c>null</c> when unchanged. Separate from the
/// append because a failed attempt moves the counter without moving the level.</param>
public sealed record WorkbenchMutation(
    string InstanceId,
    MutationOpKind Kind,
    MutationResult Result,
    string? StateHash,
    string? OriginValuesJson,
    string AppliedUtc,
    long CatalogRevision = 0,
    int RulesVersion = 0,
    IReadOnlyList<SocketSlot>? Sockets = null,
    int? PityCounter = null);

/// <summary>What one workbench operation did. <c>Replayed</c> is the idempotent retry.</summary>
public sealed record WorkbenchApplyResult(bool Ok, string Reason, string OutcomeRef, int OpSeq, bool Replayed);

/// <summary>What a salvage returned, and whether it happened at all.</summary>
public sealed record WorkbenchSalvageResult(bool Ok, string Reason, IReadOnlyList<MaterialCostLine> Granted);

/// <summary>
/// ⭐ <b>The workbench executor's atomic write.</b> item modules 14, 15 and 16 each shipped their own
/// half of one loop — module 14's pricing and <see cref="TrySpendRecipe"/>, module 15's
/// <see cref="AppendMutationOp"/>, module 16's <see cref="SetSockets"/> — and each recorded the same
/// blocker: <i>nothing calls them against a stored item</i>. This file is the missing joint at the
/// storage layer; <c>ItemWorkbench</c> in the server is the decision half that drives it.
///
/// <para><b>Why the composite lives here rather than in the caller.</b> <c>spec-salvage-craft.md</c>
/// §"The spend transaction" is one six-step, gate-serialised transaction whose step 5 is <i>"the
/// owning module's mutation or mint, in the SAME transaction"</i>, and
/// <c>spec-enhance-reroll.md</c>'s Boundaries repeat it: <i>"commit op row, material debit and head
/// rewrite in one transaction"</i>. A caller outside <c>FusionRpg.Data</c> cannot hold that
/// transaction — it would need a <c>SqliteConnection</c>, which <c>guard-dal.ps1</c> forbids, and
/// re-entering the store from inside <c>perform</c> would open a second connection against a database
/// its own write transaction already holds. So the transaction is here and the <b>decision</b> arrives
/// as data.</para>
///
/// <para>⛔ <b>No policy runs in this file.</b> Nothing here reads a tuning, resolves a cost, rolls a
/// die or decides an outcome — those are Core's (<c>MaterialRecipeCatalog</c>, <c>EnhancePolicy</c>,
/// <c>SalvagePolicy</c>, <c>SocketOperations</c>) and the executor's. This is persistence with the
/// atomicity the three specs ask for and nothing else.</para>
/// </summary>
public sealed partial class RpgStore
{
    /// <summary>
    /// Debit module 14's resolved cost and apply the owning module's write, <b>in one transaction</b>.
    /// Runs entirely inside <see cref="TrySpendRecipe"/>'s own six steps, so every property that path
    /// already proves — replay returns the recorded outcome and spends nothing, a reused correlation
    /// with different arguments is <c>correlation.mismatch</c>, a refusal writes nothing, the spend
    /// order is fixed — holds for the whole operation and not merely for its cost legs.
    ///
    /// <para>A refusal from the mutation half (an unknown instance, a correlation collision on the op
    /// ledger, the <c>mutation_seq</c> overflow) <b>throws out of <c>perform</c></b> and rolls the
    /// debit back with it. That direction is deliberate: D2 clause 11 — <i>"a spent cost with no op is
    /// theft; an op with no cost is duplication"</i>.</para>
    /// </summary>
    public WorkbenchApplyResult TrySpendAndApply(
        long playerId,
        string recipeId,
        IReadOnlyList<MaterialCostLine> lines,
        string correlationId,
        WorkbenchMutation? mutation = null,
        IReadOnlyList<WorkbenchGrant>? grants = null,
        IReadOnlyList<WorkbenchStockDelta>? stock = null,
        string? playerKey = null)
    {
        if (lines is null) throw new ArgumentNullException(nameof(lines));

        // Clause 11's record of the spend, in module 14's vocabulary — built from the SAME lines the
        // debit uses, so the op's cost_json cannot drift from what was actually taken.
        var costJson = CostJson(lines);
        var owner = playerKey ?? playerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var nowUtc = mutation?.AppliedUtc ?? DateTime.UtcNow.ToString("O");

        var opSeq = 0;
        var replayedOp = false;

        try
        {
            var spend = TrySpendRecipe(playerId, recipeId, lines, correlationId, perform: db =>
        {
            if (mutation is { } m)
            {
                var appended = AppendMutationOpUnlocked(
                    db, m.InstanceId, m.Kind, correlationId, DeriveOpSeed(m.InstanceId, correlationId),
                    m.Result, m.StateHash, m.OriginValuesJson, m.AppliedUtc,
                    m.CatalogRevision, m.RulesVersion, costJson);

                if (!appended.Ok)
                    throw new WorkbenchApplyRefused(appended.Reason);

                opSeq = appended.Seq;
                replayedOp = appended.Replayed;

                if (m.PityCounter is { } pity) SetInstancePityCounterUnlocked(db, m.InstanceId, pity);
                if (m.Sockets is { } sockets) SetSocketsUnlocked(db, m.InstanceId, sockets);
            }

            if (grants is { Count: > 0 })
                GrantMaterialsUnlocked(db, playerId, grants.Select(g => (g.MaterialId, g.Qty)).ToList(), nowUtc);

            foreach (var delta in stock ?? Array.Empty<WorkbenchStockDelta>())
                AdjustStockUnlocked(db, owner, delta.ContainerId, delta.Delta, nowUtc);

            return mutation?.InstanceId ?? recipeId;
            });

            return new WorkbenchApplyResult(
                spend.Ok, spend.Reason, spend.OutcomeRef, opSeq, replayedOp || spend.Reason == "replay");
        }
        catch (WorkbenchApplyRefused refused)
        {
            // The debit rolled back with it (the `using var tx` in TrySpendRecipe), so this is a
            // refusal the caller can render, not a fault. Nothing was spent.
            return new WorkbenchApplyResult(false, refused.Reason, "", 0, false);
        }
    }

    /// <summary>
    /// ⭐ Module 14's converter, committed. Salvage has <b>no debit</b> — it is the credit side — so it
    /// does not run through <see cref="TrySpendRecipe"/> and does not write a spend-log row; putting a
    /// zero-cost row in a table named for spends would smear the two directions together.
    ///
    /// <para><b>Its idempotency is the item's own disposition</b>, which is stronger than a log: the
    /// grant is gated on <c>UPDATE … WHERE disposition = 'owned'</c> inside the same transaction, so a
    /// second salvage of the same item updates zero rows and returns
    /// <c>item.not-owned</c> having granted nothing — no new table, no new <c>op_kind</c> (the
    /// namespace is closed at ten and adding one is ask-first), and no correlation the caller has to
    /// remember. <c>RpgItemRow.Disposition</c>'s own contract already says <i>"deleting the underlying
    /// instance is always a disposition, never a side effect of another operation"</i>.</para>
    /// </summary>
    public WorkbenchSalvageResult TrySalvageItem(
        long playerId, string playerKey, string instanceId,
        IReadOnlyList<MaterialCostLine> yield, string appliedUtc, string eventId)
    {
        if (yield is null) throw new ArgumentNullException(nameof(yield));

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();

            int changed;
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    UPDATE rpg_item SET disposition = 'salvaged', revision = revision + 1
                    WHERE instance_id = $id AND player_id = $p AND disposition = 'owned' AND locked = 0;
                    """;
                cmd.Parameters.AddWithValue("$id", instanceId);
                cmd.Parameters.AddWithValue("$p", playerKey);
                changed = cmd.ExecuteNonQuery();
            }

            if (changed == 0)
            {
                tx.Rollback();
                return new WorkbenchSalvageResult(false, "item.not-owned", Array.Empty<MaterialCostLine>());
            }

            // Salvage never mints currency, so a souls line would be a bug upstream rather than a
            // number to pay out. Refused here as well as in SalvagePolicy: the yield crosses a
            // process boundary between the two, and this is the last place it can be checked.
            foreach (var line in yield)
                if (line.Class == MaterialClass.Souls)
                    throw new ArgumentException(
                        "a salvage yield may not contain a souls line — salvage is a converter, not a faucet " +
                        "(spec-salvage-craft.md §Salvage)", nameof(yield));

            GrantMaterialsUnlocked(db, playerId,
                yield.Select(l => (l.MaterialId, l.Qty)).ToList(), appliedUtc);

            SaveItemEventUnlocked(db, new RpgItemEventRow(
                eventId, instanceId, playerKey, "salvaged", CostJson(yield), appliedUtc));

            tx.Commit();
            return new WorkbenchSalvageResult(true, "", yield);
        }
    }

    /// <summary>
    /// The op's recorded seed. Deterministic in <c>(instance, correlation)</c> so the same retried
    /// operation decides the same thing, and domain-separated per op kind by
    /// <c>MutationOpKinds.StreamName</c> at the point it is drawn from.
    /// </summary>
    public static long DeriveOpSeed(string instanceId, string correlationId)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(instanceId + "|" + correlationId));
        return BitConverter.ToInt64(bytes, 0);
    }

    /// <summary>Run one statement on the caller's connection. <c>db.CreateCommand()</c> inherits the
    /// connection's active transaction, which is what lets an unlocked write join the caller's.</summary>
    static void ExecOn(SqliteConnection db, string sql, params (string Name, object Value)[] args)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }
}

/// <summary>
/// Raised from inside <see cref="RpgStore.TrySpendRecipe"/>'s <c>perform</c> step when the owning
/// module's write refuses. Throwing is the ONLY way to roll a debit back from step 5, and D2 clause 11
/// requires exactly that: <i>"a spent cost with no op is theft"</i>.
/// </summary>
public sealed class WorkbenchApplyRefused : Exception
{
    public WorkbenchApplyRefused(string reason) : base(reason) => Reason = reason;

    public string Reason { get; }
}
