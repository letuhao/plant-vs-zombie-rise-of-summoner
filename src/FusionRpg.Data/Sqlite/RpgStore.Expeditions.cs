using System.Text.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Expeditions;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

public sealed record ExpeditionRow(
    long Id, long PlayerId, string CorrelationId, string State, string TierId,
    string SquadJson, ulong Seed, string DispatchedUtc, string DueUtc, string? CollectedUtc)
{
    public List<string> SquadInstanceIds =>
        JsonSerializer.Deserialize<List<string>>(SquadJson) ?? new List<string>();
}

public sealed record DemonMaterialRow(string MaterialId, long Qty);

public static class ExpeditionStates
{
    public const string Dispatched = "Dispatched";
    public const string Collected = "Collected";
    public const string Recalled = "Recalled";
}

public sealed partial class RpgStore
{
    /// <summary>
    /// Dispatch (spec-expeditions.md): validates tier/slots/ownership and the cross-mode
    /// soft-lock, seals the squad + seed, writes expedition + membership rows in one
    /// transaction. Correlation replay returns the stored expedition untouched.
    /// </summary>
    public (bool Ok, string Reason, ExpeditionRow? Expedition) DispatchExpedition(
        long playerId, string correlationId, string tierId, IReadOnlyList<string> squadInstanceIds,
        ulong seed, DateTimeOffset? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return (false, "correlation.missing", null);
        var corr = correlationId.Trim();
        if (!ExpeditionTierCatalog.IsKnown(tierId)) return (false, "tier.unknown", null);
        var tier = ExpeditionTierCatalog.Get(tierId);
        if (squadInstanceIds.Count == 0) return (false, "squad.empty", null);
        if (squadInstanceIds.Count > tier.SquadSlots) return (false, "squad.toolarge", null);
        if (squadInstanceIds.Distinct(StringComparer.Ordinal).Count() != squadInstanceIds.Count)
            return (false, "squad.duplicate", null);

        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null) return (false, "player.unknown", null);

            var existing = ReadExpeditionByCorrelationUnlocked(db, playerId, corr);
            if (existing != null)
                return (true, "replay", existing);

            foreach (var id in squadInstanceIds)
            {
                var actor = ReadUniqueActorUnlocked(db, id);
                if (actor is null || actor.PlayerId != playerId || ReadDemonProfileUnlocked(db, id) is null)
                    return (false, "squad.unknown-specimen", null);
                if (!string.Equals(actor.Phase, UniqueActorPhases.Roster, StringComparison.Ordinal))
                    return (false, "specimen.deployed", null);
                if (HasActiveExpeditionMembershipUnlocked(db, id))
                    return (false, "specimen.on-expedition", null);
                // Contracts gate every path that fields a demon (spec-demon-contracts.md).
                var contract = ContractViewUnlocked(db, playerId, id);
                if (!contract.Bound) return (false, "specimen.unbound", null);
                if (!contract.Deployable) return (false, "specimen.insubordinate", null);
            }

            var now = utcNow ?? DateTimeOffset.UtcNow;
            var dispatched = now.UtcDateTime.ToString("o");
            var due = now.AddMinutes(tier.DurationMinutes).UtcDateTime.ToString("o");

            using var tx = db.BeginTransaction();
            long expeditionId;
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO rpg_expeditions(
                      player_id, correlation_id, state, tier_id, squad_json, seed,
                      dispatched_utc, due_utc)
                    VALUES($p,$c,$s,$tier,$squad,$seed,$dt,$due);
                    SELECT last_insert_rowid();
                    """;
                cmd.Parameters.AddWithValue("$p", playerId);
                cmd.Parameters.AddWithValue("$c", corr);
                cmd.Parameters.AddWithValue("$s", ExpeditionStates.Dispatched);
                cmd.Parameters.AddWithValue("$tier", tier.TierId);
                cmd.Parameters.AddWithValue("$squad", JsonSerializer.Serialize(squadInstanceIds));
                cmd.Parameters.AddWithValue("$seed", seed.ToString());
                cmd.Parameters.AddWithValue("$dt", dispatched);
                cmd.Parameters.AddWithValue("$due", due);
                expeditionId = (long)(cmd.ExecuteScalar() ?? 0L);
            }

            foreach (var id in squadInstanceIds)
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO rpg_expedition_members(expedition_id, instance_id, active)
                    VALUES($e,$i,1);
                    """;
                cmd.Parameters.AddWithValue("$e", expeditionId);
                cmd.Parameters.AddWithValue("$i", id);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            return (true, "", ReadExpeditionByIdUnlocked(db, expeditionId));
        }
    }

    public List<ExpeditionRow> ListExpeditions(long playerId, int limit = 50)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = SelectExpedition + " WHERE player_id=$p ORDER BY id DESC LIMIT $l;";
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$l", limit);
            using var r = cmd.ExecuteReader();
            var list = new List<ExpeditionRow>();
            while (r.Read())
                list.Add(MapExpedition(r));
            return list;
        }
    }

    public ExpeditionRow? TryGetExpedition(long expeditionId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return ReadExpeditionByIdUnlocked(db, expeditionId);
        }
    }

    public bool HasActiveExpeditionMembership(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return false;
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return HasActiveExpeditionMembershipUnlocked(db, instanceId.Trim());
        }
    }

    /// <summary>Collect/recall terminal transition: state + collected stamp + lock release.</summary>
    public bool TryCloseExpedition(long expeditionId, string state, DateTimeOffset? utcNow = null)
    {
        if (state is not (ExpeditionStates.Collected or ExpeditionStates.Recalled))
            throw new ArgumentException($"Invalid terminal expedition state '{state}'.");
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var closed = CloseExpeditionUnlocked(db, expeditionId, state, utcNow ?? DateTimeOffset.UtcNow);
            tx.Commit();
            return closed;
        }
    }

    internal bool CloseExpeditionUnlocked(SqliteConnection db, long expeditionId, string state, DateTimeOffset utcNow)
    {
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE rpg_expeditions SET state=$s, collected_utc=$t
                WHERE id=$id AND state=$open;
                """;
            cmd.Parameters.AddWithValue("$s", state);
            cmd.Parameters.AddWithValue("$t", utcNow.UtcDateTime.ToString("o"));
            cmd.Parameters.AddWithValue("$id", expeditionId);
            cmd.Parameters.AddWithValue("$open", ExpeditionStates.Dispatched);
            if (cmd.ExecuteNonQuery() == 0)
                return false;
        }

        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "UPDATE rpg_expedition_members SET active=0 WHERE expedition_id=$e;";
            cmd.Parameters.AddWithValue("$e", expeditionId);
            cmd.ExecuteNonQuery();
        }

        return true;
    }

    /// <summary>SIM-only timer rewind — endpoints gate this behind FUSIONRPG_SIM=1.</summary>
    public bool ForceExpeditionDue(long expeditionId, DateTimeOffset dueUtc)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "UPDATE rpg_expeditions SET due_utc=$d WHERE id=$id;";
            cmd.Parameters.AddWithValue("$d", dueUtc.UtcDateTime.ToString("o"));
            cmd.Parameters.AddWithValue("$id", expeditionId);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public void AddDemonMaterials(long playerId, IReadOnlyList<(string MaterialId, long Qty)> drops)
    {
        if (drops.Count == 0) return;
        foreach (var (materialId, qty) in drops)
        {
            if (!DemonMaterialCatalog.IsKnown(materialId))
                throw new ArgumentException($"Unknown demon material id '{materialId}'.");
            if (qty <= 0)
                throw new ArgumentException($"Material qty must be positive ({materialId}).");
        }

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            AddDemonMaterialsUnlocked(db, playerId, drops);
            tx.Commit();
        }
    }

    internal void AddDemonMaterialsUnlocked(
        SqliteConnection db, long playerId, IReadOnlyList<(string MaterialId, long Qty)> drops)
    {
        var now = DateTime.UtcNow.ToString("o");
        foreach (var (materialId, qty) in drops)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO rpg_demon_materials(player_id, material_id, qty, updated_utc)
                VALUES($p,$m,$q,$t)
                ON CONFLICT(player_id, material_id)
                DO UPDATE SET qty = qty + $q, updated_utc = $t;
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$m", materialId);
            cmd.Parameters.AddWithValue("$q", qty);
            cmd.Parameters.AddWithValue("$t", now);
            cmd.ExecuteNonQuery();
        }
    }

    public List<DemonMaterialRow> ListDemonMaterials(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT material_id, qty FROM rpg_demon_materials
                WHERE player_id=$p AND qty > 0 ORDER BY material_id;
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            using var r = cmd.ExecuteReader();
            var list = new List<DemonMaterialRow>();
            while (r.Read())
                list.Add(new DemonMaterialRow(r.GetString(0), r.GetInt64(1)));
            return list;
        }
    }

    public sealed record ExpeditionRewardApply(
        long EventSouls,
        IReadOnlyList<(string MaterialId, long Qty)> Materials,
        IReadOnlyList<(string InstanceId, double Xp)> SpecimenXp,
        IReadOnlyList<DemonMintSpec> WildMints);

    /// <summary>
    /// Exactly-once reward application: ONE transaction gated on the Dispatched→terminal state
    /// transition. If the expedition is already closed, nothing applies and nothing is written —
    /// a crashed collect retries safely (battle ingests replay by correlation upstream).
    /// </summary>
    public (bool Applied, string Reason, List<DemonSpecimenDto> Minted) ApplyExpeditionRewards(
        long expeditionId, long playerId, string state, ExpeditionRewardApply rewards,
        DateTimeOffset? utcNow = null)
    {
        if (state is not (ExpeditionStates.Collected or ExpeditionStates.Recalled))
            throw new ArgumentException($"Invalid terminal expedition state '{state}'.");
        foreach (var (materialId, qty) in rewards.Materials)
        {
            if (!DemonMaterialCatalog.IsKnown(materialId))
                throw new ArgumentException($"Unknown demon material id '{materialId}'.");
            if (qty <= 0)
                throw new ArgumentException($"Material qty must be positive ({materialId}).");
        }

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var now = utcNow ?? DateTimeOffset.UtcNow;

            if (!CloseExpeditionUnlocked(db, expeditionId, state, now))
                return (false, "expedition.closed", new List<DemonSpecimenDto>());

            if (rewards.EventSouls > 0)
            {
                AppendSoulLedgerUnlocked(db, playerId, 0,
                    Math.Min(rewards.EventSouls, MaxSoulAward),
                    Core.Demons.SoulEarnPolicy.Reasons.Expedition, null, null,
                    "exp:" + expeditionId, now.UtcDateTime.ToString("o"));
            }

            if (rewards.Materials.Count > 0)
                AddDemonMaterialsUnlocked(db, playerId, rewards.Materials);

            foreach (var (instanceId, xp) in rewards.SpecimenXp)
            {
                if (xp > 0)
                    AwardUniqueActorXpUnlocked(db, instanceId, xp);
            }

            var minted = new List<DemonSpecimenDto>();
            foreach (var spec in rewards.WildMints)
            {
                var stamp = now.UtcDateTime.ToString("o");
                minted.Add(MintDemonUnlocked(db, playerId, spec, stamp, out var speciesNewlyDiscovered));
                // One discovery policy across acquisition paths (2026-08-21 review S5): a species
                // first met on an expedition pays the same bonus a summon would; the shared
                // `species:{id}` dedupe keeps it once-ever no matter which path lands first.
                if (speciesNewlyDiscovered
                    && Core.Demons.DemonRarityIds.TryParse(spec.Rarity, out var mintRarity))
                {
                    var reward = Core.Demons.SoulEarnPolicy.DiscoveryDelta(mintRarity);
                    if (reward > 0)
                        AppendSoulLedgerUnlocked(db, playerId, 0, reward,
                            Core.Demons.SoulEarnPolicy.Reasons.Discovery,
                            "species", spec.SpeciesId, "species:" + spec.SpeciesId, stamp);
                }
            }

            tx.Commit();
            return (true, "", minted);
        }
    }

    internal bool HasActiveExpeditionMembershipUnlocked(SqliteConnection db, string instanceId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT 1 FROM rpg_expedition_members WHERE instance_id=$i AND active=1 LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$i", instanceId);
        return cmd.ExecuteScalar() != null;
    }

    const string SelectExpedition = """
        SELECT id, player_id, correlation_id, state, tier_id, squad_json, seed,
               dispatched_utc, due_utc, collected_utc
        FROM rpg_expeditions
        """;

    ExpeditionRow? ReadExpeditionByIdUnlocked(SqliteConnection db, long id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = SelectExpedition + " WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? MapExpedition(r) : null;
    }

    ExpeditionRow? ReadExpeditionByCorrelationUnlocked(SqliteConnection db, long playerId, string correlationId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = SelectExpedition + " WHERE player_id=$p AND correlation_id=$c;";
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$c", correlationId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? MapExpedition(r) : null;
    }

    static ExpeditionRow MapExpedition(SqliteDataReader r) => new(
        r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetString(4),
        r.GetString(5),
        ParseSeed(Convert.ToString(r.GetValue(6), System.Globalization.CultureInfo.InvariantCulture)),
        r.GetString(7), r.GetString(8),
        r.IsDBNull(9) ? null : r.GetString(9));
}
