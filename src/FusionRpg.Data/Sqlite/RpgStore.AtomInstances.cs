using FusionRpg.Core.Effects.Atoms;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>One binding a host refused, and why. Never dropped silently.</summary>
public readonly record struct BindRefusal(string BindingId, AtomRejectionReason Reason, string Detail)
{
    public override string ToString() => $"{BindingId}: {Reason} — {Detail}";
}

/// <summary>What a host may execute, and what it would not.</summary>
/// <param name="AtomsByBinding">
/// The atom rows behind each accepted binding, keyed by <c>binding_id</c>. Carried rather than left
/// for the caller to fetch: resolving already loads every one of them, and a caller that re-queried
/// would reopen the N+1 this method was rewritten to close (E19 is the first caller that needs them).
/// </param>
public sealed record BindResolution(
    IReadOnlyList<BindingRow> Bindings,
    IReadOnlyList<BindRefusal> Refused,
    IReadOnlyDictionary<string, IReadOnlyList<AtomRow>>? AtomsByBinding = null);

/// <summary>
/// An instance attached to an owner. Replaces the logical `foundation_effect_grant`.
/// </summary>
public sealed record BindingRow
{
    public string BindingId { get; init; } = "";
    public string InstanceId { get; init; } = "";
    public OwnerKind OwnerKind { get; init; }
    public string OwnerKey { get; init; } = "";
    public string? Slot { get; init; }

    /// <summary>
    /// The primary sort key of the actor effect list. The one execution-order guarantee in the
    /// program needs a column, not just a sentence.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>Plugin or feature id — what a withdraw matches on.</summary>
    public string Source { get; init; } = "";

    public string BoundUtc { get; init; } = "";
    public long Revision { get; init; }

    public OwnerScope Scope => new(OwnerKind, OwnerKey);
}

/// <summary>
/// <c>effect_instance</c> / <c>effect_instance_atom</c> / <c>effect_binding</c>
/// (spec-instance-and-binding.md, E6).
///
/// <para><b>Runtime state stays in RAM.</b> ICD clocks, stacks, counters and status instances live in
/// session memory exactly as they do now — there is no durable runtime table here. `entity:` grants
/// are meaningless across a restart, and per-match counters are E15's.</para>
/// </summary>
public sealed partial class RpgStore
{
    void EnsureAtomInstanceSchemaUnlocked(SqliteConnection db)
    {
        Exec(db, """
            CREATE TABLE IF NOT EXISTS effect_instance (
              instance_id TEXT NOT NULL PRIMARY KEY,
              container_id TEXT NOT NULL,
              roll_seed INTEGER NOT NULL,
              catalog_revision INTEGER NOT NULL DEFAULT 0,
              created_utc TEXT NOT NULL,
              origin TEXT NOT NULL DEFAULT 'drop',
              theta_content INTEGER NOT NULL DEFAULT 0,
              content_scale_milli INTEGER NOT NULL DEFAULT 1000
            );
            CREATE INDEX IF NOT EXISTS ix_effect_instance_container ON effect_instance(container_id);

            CREATE TABLE IF NOT EXISTS effect_instance_atom (
              instance_id TEXT NOT NULL,
              seq INTEGER NOT NULL,
              atom_id TEXT NOT NULL,
              values_json TEXT NOT NULL DEFAULT '{}',
              power_json TEXT,
              PRIMARY KEY (instance_id, seq)
            );

            CREATE TABLE IF NOT EXISTS effect_binding (
              binding_id TEXT NOT NULL PRIMARY KEY,
              instance_id TEXT NOT NULL,
              owner_kind TEXT NOT NULL,
              owner_key TEXT NOT NULL DEFAULT '',
              slot TEXT,
              priority INTEGER NOT NULL DEFAULT 0,
              source TEXT NOT NULL DEFAULT '',
              bound_utc TEXT NOT NULL,
              revision INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_effect_binding_owner
              ON effect_binding(owner_kind, owner_key);
            CREATE INDEX IF NOT EXISTS ix_effect_binding_instance ON effect_binding(instance_id);
            CREATE INDEX IF NOT EXISTS ix_effect_binding_source ON effect_binding(source);
            """);

        // T3.4 (content-scale): a database created before this migration has effect_instance without
        // these two columns — CREATE TABLE IF NOT EXISTS is a no-op against it, so the addition has
        // to be explicit. Defaults (Theta=0, scale=1000=x1.000) only ever apply to pre-migration rows
        // read back after this point; every row written through SaveInstance from here on always
        // supplies real values (TryInstantiate requires them, spec-content-scale.md §2.4).
        EnsureColumn(db, "effect_instance", "theta_content", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "effect_instance", "content_scale_milli", "INTEGER NOT NULL DEFAULT 1000");
    }

    /// <summary>
    /// Persist an instance. The id is generated here — it is excluded from the reproducibility
    /// comparison precisely because it is.
    /// </summary>
    public string SaveInstance(InstanceRow instance, string? instanceId = null, string? createdUtc = null)
    {
        var id = string.IsNullOrWhiteSpace(instanceId) ? Guid.NewGuid().ToString("N") : instanceId!;
        var utc = createdUtc ?? DateTime.UtcNow.ToString("O");

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();

            ExecIn(db, tx, """
                INSERT INTO effect_instance
                  (instance_id, container_id, roll_seed, catalog_revision, created_utc, origin,
                   theta_content, content_scale_milli)
                VALUES ($id, $c, $seed, $rev, $utc, $origin, $theta, $scale)
                ON CONFLICT(instance_id) DO UPDATE SET
                  container_id = excluded.container_id, roll_seed = excluded.roll_seed,
                  catalog_revision = excluded.catalog_revision, origin = excluded.origin,
                  theta_content = excluded.theta_content, content_scale_milli = excluded.content_scale_milli;
                """,
                ("$id", id), ("$c", instance.ContainerId), ("$seed", instance.RollSeed),
                ("$rev", instance.CatalogRevision), ("$utc", utc),
                ("$origin", instance.Origin.ToString().ToLowerInvariant()),
                ("$theta", instance.ThetaContent), ("$scale", instance.ContentScaleMilli));

            ExecIn(db, tx, "DELETE FROM effect_instance_atom WHERE instance_id = $id;", ("$id", id));

            foreach (var a in instance.Atoms)
                ExecIn(db, tx,
                    "INSERT INTO effect_instance_atom (instance_id, seq, atom_id, values_json, power_json) " +
                    "VALUES ($id, $seq, $atom, $vals, $power);",
                    ("$id", id), ("$seq", a.Seq), ("$atom", a.AtomId), ("$vals", a.ValuesJson),
                    ("$power", (object?)a.PowerJson ?? DBNull.Value));

            tx.Commit();
        }

        return id;
    }

    public InstanceRow? GetInstance(string instanceId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();

            InstanceRow? head;
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT instance_id, container_id, roll_seed, created_utc, origin, catalog_revision, " +
                    "theta_content, content_scale_milli " +
                    "FROM effect_instance WHERE instance_id = $id;";
                cmd.Parameters.AddWithValue("$id", instanceId);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return null;

                head = new InstanceRow
                {
                    InstanceId = r.GetString(0),
                    ContainerId = r.GetString(1),
                    RollSeed = r.GetInt64(2),
                    CreatedUtc = r.GetString(3),
                    Origin = Enum.TryParse<InstanceOrigin>(r.GetString(4), true, out var o)
                        ? o : InstanceOrigin.Drop,
                    CatalogRevision = r.GetInt64(5),
                    ThetaContent = r.GetInt32(6),
                    ContentScaleMilli = r.GetInt64(7),
                };
            }

            var atoms = new List<InstanceAtomRow>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT seq, atom_id, values_json, power_json FROM effect_instance_atom " +
                    "WHERE instance_id = $id ORDER BY seq;";
                cmd.Parameters.AddWithValue("$id", instanceId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    atoms.Add(new InstanceAtomRow(r.GetInt32(0), r.GetString(1), r.GetString(2),
                        r.IsDBNull(3) ? null : r.GetString(3)));
            }

            return head with { Atoms = atoms };
        }
    }

    /// <summary>
    /// Bind an instance to an owner. The owner key is validated against its scope grammar first —
    /// two spellings of one pointer means two bindings the withdraw path cannot match.
    /// </summary>
    public AtomRejection Bind(BindingRow binding, string? bindingId = null, string? boundUtc = null)
    {
        var scope = OwnerScope.Validate(binding.OwnerKind, binding.OwnerKey, out _);
        if (!scope.IsOk) return scope;

        if (GetInstance(binding.InstanceId) is null)
            return AtomRejection.Fail(AtomRejectionReason.StaleInstance,
                $"instance '{binding.InstanceId}' does not exist");

        var id = string.IsNullOrWhiteSpace(bindingId)
            ? (string.IsNullOrWhiteSpace(binding.BindingId) ? Guid.NewGuid().ToString("N") : binding.BindingId)
            : bindingId!;

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO effect_binding
                  (binding_id, instance_id, owner_kind, owner_key, slot, priority, source, bound_utc, revision)
                VALUES ($id, $inst, $kind, $key, $slot, $prio, $src, $utc, 1)
                ON CONFLICT(binding_id) DO UPDATE SET
                  instance_id = excluded.instance_id, owner_kind = excluded.owner_kind,
                  owner_key = excluded.owner_key, slot = excluded.slot,
                  priority = excluded.priority, source = excluded.source,
                  revision = effect_binding.revision + 1;
                """;
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$inst", binding.InstanceId);
            cmd.Parameters.AddWithValue("$kind", OwnerScope.Name(binding.OwnerKind));
            cmd.Parameters.AddWithValue("$key", binding.OwnerKey ?? "");
            cmd.Parameters.AddWithValue("$slot", (object?)binding.Slot ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$prio", binding.Priority);
            cmd.Parameters.AddWithValue("$src", binding.Source ?? "");
            cmd.Parameters.AddWithValue("$utc", boundUtc ?? DateTime.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
        }

        return AtomRejection.Ok;
    }

    /// <summary>
    /// Bindings on one owner, in the order the actor effect list uses.
    ///
    /// <para><b>`priority DESC`, then content-derived keys</b> — never `binding_id`, which is
    /// generated: two runs of the same container would sort differently and consume the value stream
    /// in a different order, so identical inputs would produce different trace bytes.</para>
    /// </summary>
    public IReadOnlyList<BindingRow> ListBindings(OwnerScope owner)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT b.binding_id, b.instance_id, b.owner_kind, b.owner_key, b.slot,
                       b.priority, b.source, b.bound_utc, b.revision
                FROM effect_binding b
                JOIN effect_instance i ON i.instance_id = b.instance_id
                WHERE b.owner_kind = $kind AND b.owner_key = $key
                ORDER BY b.priority DESC, i.container_id ASC, b.instance_id ASC;
                """;
            cmd.Parameters.AddWithValue("$kind", OwnerScope.Name(owner.Kind));
            cmd.Parameters.AddWithValue("$key", owner.Key ?? "");
            using var r = cmd.ExecuteReader();

            var list = new List<BindingRow>();
            while (r.Read()) list.Add(ReadBinding(r));
            return list;
        }
    }

    /// <summary>
    /// What a host may actually execute, and what it refused. This is where <see cref="BindGate"/>
    /// runs — <see cref="Bind"/> is persistence, and a durable binding outlives any one runtime, so
    /// the runtime and scope questions can only be answered when a host asks them.
    ///
    /// <para>Refusal is per binding, not per owner: one bad trait must not silently disarm a good
    /// one. The refusals come back rather than being dropped, because an effect that does nothing
    /// with no explanation is the failure this whole layer exists to remove.</para>
    /// </summary>
    public BindResolution ResolveBindings(OwnerScope owner, BindContext ctx, int? ownerLevel = null)
    {
        var current = GetCatalogRevision();
        var atoms = ListAtoms().ToDictionary(a => a.AtomId, StringComparer.Ordinal);
        var bindings = ListBindings(owner);

        // Hoisted, not per binding. The first cut called GetInstance and GetContainer inside the
        // loop, each opening its own connection: ~2N connections and ~5N queries for N bindings, on
        // a path a host runs at match start. These are two queries total.
        var instances = LoadInstances(bindings.Select(b => b.InstanceId));
        var levelReqs = LoadContainerLevelReqs();

        var ok = new List<BindingRow>();
        var refused = new List<BindRefusal>();
        var atomsByBinding = new Dictionary<string, IReadOnlyList<AtomRow>>(StringComparer.Ordinal);

        foreach (var binding in bindings)
        {
            if (!instances.TryGetValue(binding.InstanceId, out var instance))
            {
                refused.Add(new BindRefusal(binding.BindingId, AtomRejectionReason.StaleInstance,
                    $"instance '{binding.InstanceId}' is gone"));
                continue;
            }

            // An instance rolled against an older catalog no longer means what it meant. Reproducing
            // it would need the catalog it was rolled against, which we do not keep.
            if (instance.CatalogRevision != current)
            {
                refused.Add(new BindRefusal(binding.BindingId, AtomRejectionReason.StaleInstance,
                    $"rolled against catalog revision {instance.CatalogRevision}, current is {current}"));
                continue;
            }

            var rows = new List<AtomRow>();
            var missing = false;
            foreach (var a in instance.Atoms)
            {
                if (atoms.TryGetValue(a.AtomId, out var row)) { rows.Add(row); continue; }
                refused.Add(new BindRefusal(binding.BindingId, AtomRejectionReason.StaleInstance,
                    $"{a.AtomId} is no longer in the catalog"));
                missing = true;
                break;
            }
            if (missing) continue;

            levelReqs.TryGetValue(instance.ContainerId, out var levelReq);
            var gate = BindGate.Check(rows, owner, ctx, levelReq, null);
            if (!gate.IsOk)
            {
                refused.Add(new BindRefusal(binding.BindingId, gate.Reason, gate.Detail));
                continue;
            }

            ok.Add(binding);
            atomsByBinding[binding.BindingId] = rows;
        }

        return new BindResolution(ok, refused, atomsByBinding);
    }

    /// <summary>Every named instance with its atoms, in two queries rather than two per instance.</summary>
    Dictionary<string, InstanceRow> LoadInstances(IEnumerable<string> instanceIds)
    {
        var wanted = new HashSet<string>(instanceIds, StringComparer.Ordinal);
        var byId = new Dictionary<string, InstanceRow>(StringComparer.Ordinal);
        if (wanted.Count == 0) return byId;

        lock (_gate)
        {
            using var db = OpenUnlocked();

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT instance_id, container_id, roll_seed, created_utc, origin, catalog_revision, " +
                    "theta_content, content_scale_milli " +
                    "FROM effect_instance;";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var id = r.GetString(0);
                    if (!wanted.Contains(id)) continue;
                    byId[id] = new InstanceRow
                    {
                        InstanceId = id,
                        ContainerId = r.GetString(1),
                        RollSeed = r.GetInt64(2),
                        CreatedUtc = r.GetString(3),
                        Origin = Enum.TryParse<InstanceOrigin>(r.GetString(4), true, out var o)
                            ? o : InstanceOrigin.Drop,
                        CatalogRevision = r.GetInt64(5),
                        ThetaContent = r.GetInt32(6),
                        ContentScaleMilli = r.GetInt64(7),
                    };
                }
            }

            var atomsById = new Dictionary<string, List<InstanceAtomRow>>(StringComparer.Ordinal);
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT instance_id, seq, atom_id, values_json, power_json " +
                    "FROM effect_instance_atom ORDER BY instance_id, seq;";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var id = r.GetString(0);
                    if (!byId.ContainsKey(id)) continue;
                    if (!atomsById.TryGetValue(id, out var list))
                        atomsById[id] = list = new List<InstanceAtomRow>();
                    list.Add(new InstanceAtomRow(r.GetInt32(1), r.GetString(2), r.GetString(3),
                        r.IsDBNull(4) ? null : r.GetString(4)));
                }
            }

            foreach (var (id, inst) in byId.ToList())
                byId[id] = inst with
                {
                    Atoms = atomsById.TryGetValue(id, out var list)
                        ? list
                        : (IReadOnlyList<InstanceAtomRow>)Array.Empty<InstanceAtomRow>(),
                };
        }

        return byId;
    }

    /// <summary>Container id to its `level_req`, in one query. Containers repeat across bindings.</summary>
    Dictionary<string, int?> LoadContainerLevelReqs()
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT container_id, level_req FROM effect_container;";
            using var r = cmd.ExecuteReader();

            var map = new Dictionary<string, int?>(StringComparer.Ordinal);
            while (r.Read()) map[r.GetString(0)] = r.IsDBNull(1) ? null : r.GetInt32(1);
            return map;
        }
    }

    /// <summary>Withdraw one binding. Used on unequip and on entity death.</summary>
    public bool Withdraw(string bindingId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "DELETE FROM effect_binding WHERE binding_id = $id;";
            cmd.Parameters.AddWithValue("$id", bindingId);
            var removed = cmd.ExecuteNonQuery() > 0;

            if (removed) CollectOrphanInstancesUnlocked(db);
            return removed;
        }
    }

    /// <summary>
    /// Drop every session-scoped binding. <c>entity:</c> bindings are never durable — IL2CPP reuses
    /// the pointer, so one surviving a match would attach to whatever object took its address.
    /// </summary>
    public int ClearSessionScopedBindings()
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "DELETE FROM effect_binding WHERE owner_kind = $kind;";
            cmd.Parameters.AddWithValue("$kind", OwnerScope.Name(OwnerKind.Entity));
            var removed = cmd.ExecuteNonQuery();
            CollectOrphanInstancesUnlocked(db);
            return removed;
        }
    }

    /// <summary>
    /// Instances no binding points at. An instance is reachable only through a binding, so once the
    /// last one goes the rows are unreachable — and a durable database would grow by one instance per
    /// entity binding per match, forever.
    /// </summary>
    public int CountOrphanInstances()
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(*) FROM effect_instance i
                WHERE NOT EXISTS (SELECT 1 FROM effect_binding b WHERE b.instance_id = i.instance_id);
                """;
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }
    }

    /// <summary>
    /// Delete unreachable instances and their atom rows. Called after a withdraw and after the
    /// session sweep — the two moments an instance can lose its last owner.
    /// </summary>
    static void CollectOrphanInstancesUnlocked(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            DELETE FROM effect_instance_atom WHERE instance_id IN (
              SELECT i.instance_id FROM effect_instance i
              WHERE NOT EXISTS (SELECT 1 FROM effect_binding b WHERE b.instance_id = i.instance_id));
            DELETE FROM effect_instance WHERE instance_id IN (
              SELECT i.instance_id FROM effect_instance i
              WHERE NOT EXISTS (SELECT 1 FROM effect_binding b WHERE b.instance_id = i.instance_id));
            """;
        cmd.ExecuteNonQuery();
    }

    static BindingRow ReadBinding(SqliteDataReader r) => new()
    {
        BindingId = r.GetString(0),
        InstanceId = r.GetString(1),
        OwnerKind = ParseOwnerKind(r.GetString(2)),
        OwnerKey = r.GetString(3),
        Slot = r.IsDBNull(4) ? null : r.GetString(4),
        Priority = r.GetInt32(5),
        Source = r.GetString(6),
        BoundUtc = r.GetString(7),
        Revision = r.GetInt64(8),
    };

    static OwnerKind ParseOwnerKind(string name)
    {
        foreach (OwnerKind k in Enum.GetValues(typeof(OwnerKind)))
            if (string.Equals(OwnerScope.Name(k), name, StringComparison.Ordinal))
                return k;
        return OwnerKind.Match;
    }
}
