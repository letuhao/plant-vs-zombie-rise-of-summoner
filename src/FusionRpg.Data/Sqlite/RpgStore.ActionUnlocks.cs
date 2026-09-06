using FusionRpg.Core.Actions.Unlock;
using FusionRpg.Core.Effects.Atoms;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>
/// T59.2 (spec-action-instance-and-grant.md §3): the unlock ladder's own persistence — named in
/// spec-unlock-ladder.md, never built until now (confirmed by T21's own evidence in
/// action-todo.md: "RpgStore.ActionUnlocks.cs was never built"). Pure round-trip for an already-
/// fully-specified, already-tested pure class (<see cref="UnlockState"/>) — no new game logic here,
/// matching this module's own stated boundary.
/// </summary>
public sealed partial class RpgStore
{
    void EnsureActionUnlockSchemaUnlocked(SqliteConnection db)
    {
        Exec(db, """
            -- rpg_actor_unlock_state -- the ratchet's own earnCount, one row per owner
            -- (spec-unlock-ladder.md §1: "earnCount plus the held set -- no occupancy math anywhere").
            CREATE TABLE IF NOT EXISTS rpg_actor_unlock_state (
              owner_kind TEXT NOT NULL,
              owner_key  TEXT NOT NULL DEFAULT '',
              earn_count INTEGER NOT NULL DEFAULT 0,
              PRIMARY KEY (owner_kind, owner_key)
            );

            -- rpg_actor_held_unlock -- the held set, many rows per owner. earn_count_at_acceptance is
            -- the ONLY thing recorded about strength (UnlockState.cs's own doc comment: "never a
            -- resolved rung") -- the rung is always recomputed from this value on read, never stored.
            CREATE TABLE IF NOT EXISTS rpg_actor_held_unlock (
              owner_kind TEXT NOT NULL,
              owner_key  TEXT NOT NULL DEFAULT '',
              unlock_id  TEXT NOT NULL,
              earn_count_at_acceptance INTEGER NOT NULL,
              PRIMARY KEY (owner_kind, owner_key, unlock_id)
            );
            CREATE INDEX IF NOT EXISTS ix_rpg_actor_held_unlock_owner ON rpg_actor_held_unlock(owner_kind, owner_key);
            """);
    }

    /// <summary>Reads back a full <see cref="UnlockState"/> for one owner — <see cref="UnlockState.Empty"/>
    /// for an owner with no row yet (a specimen's first-ever level gain), matching
    /// <see cref="UnlockState.FromPersisted"/>'s own contract exactly. Held-set order is read in
    /// whatever order SQLite returns it — spec: "identical... across a shuffled held-set order",
    /// so no `ORDER BY` is needed for correctness.</summary>
    public UnlockState GetUnlockState(OwnerScope owner)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();

            long earnCount = 0;
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "SELECT earn_count FROM rpg_actor_unlock_state WHERE owner_kind = $kind AND owner_key = $key;";
                cmd.Parameters.AddWithValue("$kind", OwnerScope.Name(owner.Kind));
                cmd.Parameters.AddWithValue("$key", owner.Key ?? "");
                var result = cmd.ExecuteScalar();
                if (result is not null and not DBNull) earnCount = Convert.ToInt64(result);
            }

            var held = new List<HeldUnlock>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "SELECT unlock_id, earn_count_at_acceptance FROM rpg_actor_held_unlock WHERE owner_kind = $kind AND owner_key = $key;";
                cmd.Parameters.AddWithValue("$kind", OwnerScope.Name(owner.Kind));
                cmd.Parameters.AddWithValue("$key", owner.Key ?? "");
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    held.Add(new HeldUnlock(r.GetString(0), r.GetInt64(1)));
            }

            return UnlockState.FromPersisted(earnCount, held);
        }
    }

    /// <summary>Persists a full <see cref="UnlockState"/> for one owner — a full rebuild (delete then
    /// re-insert the whole held set), matching <see cref="ApplyEquippedGrants"/>'s own established
    /// "full rebuild, never a delta" convention for a small, owner-scoped set (never more than
    /// <c>UnlockTuning.HeldCap</c> rows).</summary>
    public void SaveUnlockState(OwnerScope owner, UnlockState state)
    {
        if (state is null) throw new ArgumentNullException(nameof(state));

        lock (_gate)
        {
            using var db = OpenUnlocked();

            ExecParams(db, """
                INSERT INTO rpg_actor_unlock_state (owner_kind, owner_key, earn_count)
                VALUES ($kind, $key, $earn)
                ON CONFLICT(owner_kind, owner_key) DO UPDATE SET earn_count = excluded.earn_count;
                """,
                ("$kind", OwnerScope.Name(owner.Kind)), ("$key", owner.Key ?? ""), ("$earn", state.EarnCount));

            ExecParams(db, "DELETE FROM rpg_actor_held_unlock WHERE owner_kind = $kind AND owner_key = $key;",
                ("$kind", OwnerScope.Name(owner.Kind)), ("$key", owner.Key ?? ""));

            foreach (var h in state.Held)
            {
                ExecParams(db, """
                    INSERT INTO rpg_actor_held_unlock (owner_kind, owner_key, unlock_id, earn_count_at_acceptance)
                    VALUES ($kind, $key, $uid, $earn);
                    """,
                    ("$kind", OwnerScope.Name(owner.Kind)), ("$key", owner.Key ?? ""),
                    ("$uid", h.UnlockId), ("$earn", h.EarnCountAtAcceptance));
            }
        }
    }
}
