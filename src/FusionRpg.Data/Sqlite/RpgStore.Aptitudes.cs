using FusionRpg.Core.Stats.Aptitudes;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>
/// class-system-todo.md P6.2 — <c>AllocationStore</c> (spec-point-economy.md, read in full this
/// session). Persists P6.1's <see cref="AptitudeAllocation"/>, one <c>(scope, scopeKey)</c> at a time
/// — INPUTS only, never a resolved channel value (spec-point-economy.md §6: "Save inputs... not
/// computed totals," `stat-system.md`'s own invariant, applied here — a stored channel value would be
/// a second SSOT that goes stale the moment a coefficient moves).
///
/// <para><b>Joins the existing <see cref="RpgStore"/> partial-class convention</b>
/// (<c>RpgStore.ChannelPolicy.cs</c> is the template this file follows) rather than the standalone
/// class spec-point-economy.md §5 literally names. That file listing predates this session's own
/// survey of `FusionRpg.Data`'s real conventions: every other feature — souls, unique actors, demons,
/// contracts — is a partial-class slice sharing ONE connection/lock (<c>_gate</c>), ONE
/// <c>EnsureHotSchema</c> dispatch, and ONE <c>Reset()</c>. A standalone class with its own connection
/// would fork that pipeline and silently drop out of <c>Reset()</c> — corrected in the spec in place,
/// not a silent rewrite, matching this session's own established "code beats docs" precedent.</para>
///
/// <para><b>Scope key shape</b> mirrors the existing <c>effect_binding</c> precedent
/// (<c>owner_kind</c>+<c>owner_key</c>, `src/FusionRpg.Core/Effects/Atoms/OwnerScope.cs`): a
/// <c>scope</c> TEXT column plus a <c>scope_key</c> TEXT column, since the four
/// <see cref="AllocationScope"/> values key on four different things (spec-point-economy.md §2) — a
/// bare <c>player_id</c> or <c>instance_id</c> column alone fits none of the four uniformly. Commander
/// stringifies its `long` player id; the others carry their own natural string key
/// (<c>typeId</c> / <c>typeId:element</c> / <c>instanceId</c>) — this store does not interpret which,
/// only persists what the caller passes.</para>
/// </summary>
public sealed partial class RpgStore
{
    void EnsureAptitudeAllocationSchemaUnlocked(SqliteConnection db)
    {
        Exec(db, """
            CREATE TABLE IF NOT EXISTS rpg_aptitude_allocation (
              scope        TEXT    NOT NULL,
              scope_key    TEXT    NOT NULL,
              aptitude_id  TEXT    NOT NULL,
              points       INTEGER NOT NULL,
              PRIMARY KEY (scope, scope_key, aptitude_id)
            );
            """);
    }

    /// <summary>class-system-todo.md P6.2's own "unknown scope rejects" (§7 test 7) — the
    /// TEXT&lt;-&gt;<see cref="AllocationScope"/> boundary every row crosses in both directions. Throws
    /// naming the bad value rather than defaulting; tunables-ssot.md §7.2's "no built-in default"
    /// discipline, applied to a persistence key rather than a tuning value.</summary>
    public static string ScopeToText(AllocationScope scope) => scope switch
    {
        AllocationScope.Commander => "commander",
        AllocationScope.DemonType => "demonType",
        AllocationScope.Aspect => "aspect",
        AllocationScope.UniqueDemon => "uniqueDemon",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "unknown AllocationScope"),
    };

    public static AllocationScope ScopeFromText(string text) => text switch
    {
        "commander" => AllocationScope.Commander,
        "demonType" => AllocationScope.DemonType,
        "aspect" => AllocationScope.Aspect,
        "uniqueDemon" => AllocationScope.UniqueDemon,
        _ => throw new ArgumentException(
            $"'{text}' is not a known allocation scope (commander/demonType/aspect/uniqueDemon)", nameof(text)),
    };

    /// <summary>Persists ONLY this <c>(scope, scopeKey)</c>'s own points — one row per aptitude with a
    /// nonzero spend. A full upsert-and-prune (deletes this key's prior rows first, inside the same
    /// transaction), not an additive merge: the store holds the CURRENT allocation, not a change log,
    /// so a respec that zeroes an aptitude must actually remove its row, not leave a stale one behind.</summary>
    public void SaveAllocation(AllocationScope scope, string scopeKey, AptitudeAllocation allocation)
    {
        if (string.IsNullOrWhiteSpace(scopeKey))
            throw new ArgumentException("scopeKey must not be empty", nameof(scopeKey));
        if (allocation is null) throw new ArgumentNullException(nameof(allocation));

        var scopeText = ScopeToText(scope);

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();

            ExecIn(db, tx, "DELETE FROM rpg_aptitude_allocation WHERE scope = $scope AND scope_key = $key;",
                ("$scope", scopeText), ("$key", scopeKey));

            foreach (var apt in AptitudeCatalog.All)
            {
                var points = allocation.PointsAt(scope, apt.Id);
                if (points == 0) continue; // no row for an unspent aptitude -- nothing to persist.

                ExecIn(db, tx, """
                    INSERT INTO rpg_aptitude_allocation (scope, scope_key, aptitude_id, points)
                    VALUES ($scope, $key, $apt, $points);
                    """,
                    ("$scope", scopeText), ("$key", scopeKey), ("$apt", apt.Id), ("$points", points));
            }

            tx.Commit();
        }
    }

    /// <summary>Reconstructs an <see cref="AptitudeAllocation"/> from ONLY this
    /// <c>(scope, scopeKey)</c>'s own persisted rows — empty (not null, not a thrown error) if nothing
    /// was ever saved for it, matching <see cref="AptitudeAllocation"/>'s own "empty means all-zero
    /// shares, never invent a default" contract.</summary>
    public AptitudeAllocation LoadAllocation(AllocationScope scope, string scopeKey)
    {
        if (string.IsNullOrWhiteSpace(scopeKey))
            throw new ArgumentException("scopeKey must not be empty", nameof(scopeKey));

        var scopeText = ScopeToText(scope);

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText =
                "SELECT aptitude_id, points FROM rpg_aptitude_allocation " +
                "WHERE scope = $scope AND scope_key = $key;";
            cmd.Parameters.AddWithValue("$scope", scopeText);
            cmd.Parameters.AddWithValue("$key", scopeKey);
            using var r = cmd.ExecuteReader();

            var allocation = AptitudeAllocation.Empty;
            while (r.Read())
                allocation += AptitudeAllocation.Single(scope, r.GetString(0), r.GetInt64(1));
            return allocation;
        }
    }
}
