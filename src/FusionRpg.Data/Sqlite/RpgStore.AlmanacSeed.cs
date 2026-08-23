using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FusionRpg.Contracts;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

public sealed class AlmanacSeedDto
{
    public string Side { get; set; } = "";
    public int TypeId { get; set; }
    public string? TypeName { get; set; }
    public string? DisplayName { get; set; }
    public string? FlavorInfo { get; set; }
    public string? FlavorIntroduce { get; set; }
    public int? SunCost { get; set; }
    public double? CooldownSec { get; set; }
    public string CostStatus { get; set; } = "absent";
    public int? Hp { get; set; }
    public int? Attack { get; set; }
    public int? Armor { get; set; }
    public int? ArmorMax { get; set; }
    public bool StatsObserved { get; set; }
    public int ContractVersion { get; set; }
    public string RebuiltUtc { get; set; } = "";
    public AlmanacSeedEnrichmentDto? Enrichment { get; set; }
}

public sealed class AlmanacSeedRebuildSummary
{
    public int Built { get; set; }
    public int PlantsBuilt { get; set; }
    public int ZombiesBuilt { get; set; }
    public int CostAbsent { get; set; }
    public int CostParsed { get; set; }
    public int CostUnparsed { get; set; }
    public int StatsObserved { get; set; }
    public int StatsUnobserved { get; set; }
    public int StaleRemoved { get; set; }
}

public sealed partial class RpgStore
{
    public const int AlmanacSeedContractVersion = 1;

    static readonly Regex SunCostRx = new(@"花费[:：]\s*<color=(?:red|#[0-9A-Fa-f]{6,8})>(\d+)</color>", RegexOptions.Compiled);
    static readonly Regex CooldownRx = new(@"冷却时间[:：]\s*<color=(?:red|#[0-9A-Fa-f]{6,8})>(\d+(?:\.\d+)?)秒</color>", RegexOptions.Compiled);
    static readonly Regex ColorTagRx = new(@"</?color[^>]*>", RegexOptions.Compiled);

    public AlmanacSeedRebuildSummary RebuildAlmanacSeed()
    {
        lock (_gate)
        {
            using var media = OpenMediaUnlocked();
            var dumps = new List<(string Side, int TypeId, string FieldsJson, string CapturedUtc)>();
            using (var cmd = media.CreateCommand())
            {
                cmd.CommandText = "SELECT side, type_id, fields_json, captured_utc FROM type_almanac_dump;";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    dumps.Add((r.GetString(0), r.GetInt32(1), r.GetString(2), r.GetString(3)));
            }

            using var hot = OpenUnlocked();
            using var tx = hot.BeginTransaction();
            var summary = new AlmanacSeedRebuildSummary();
            var nowUtc = DateTime.UtcNow.ToString("o");
            var seen = new HashSet<(string Side, int TypeId)>();

            try
            {
                foreach (var d in dumps)
                {
                    seen.Add((d.Side, d.TypeId));
                    UpsertOneAlmanacSeedRowUnlocked(hot, tx, d.Side, d.TypeId, d.FieldsJson, d.CapturedUtc, nowUtc, summary);
                }

                summary.StaleRemoved = DeleteStaleAlmanacSeedRowsUnlocked(hot, tx, seen);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            summary.Built = summary.PlantsBuilt + summary.ZombiesBuilt;
            return summary;
        }
    }

    void UpsertOneAlmanacSeedRowUnlocked(
        SqliteConnection hot, SqliteTransaction tx,
        string side, int typeId, string fieldsJson, string almanacCapturedUtc, string nowUtc,
        AlmanacSeedRebuildSummary summary)
    {
        var rawFields = JsonSerializer.Deserialize<Dictionary<string, string?>>(fieldsJson, AlmanacJson)
                        ?? throw new InvalidOperationException($"almanac dump {side}/{typeId}: fields_json did not deserialize to an object");
        var fields = ToIgnoreCaseFields(rawFields);

        static string? F(Dictionary<string, string?> map, string key)
        {
            if (!map.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v)) return null;
            return v.Trim();
        }

        var displayName = F(fields, "name") ?? F(fields, "displayName");
        var typeName = F(fields, "enumName");
        var flavorInfo = StripColorMarkup(F(fields, "info"));
        var flavorIntroduce = side == "zombie" ? StripColorMarkup(F(fields, "introduce")) : null;
        var costText = F(fields, "cost");

        string costStatus;
        int? sunCost = null;
        double? cooldownSec = null;
        if (string.IsNullOrWhiteSpace(costText))
        {
            costStatus = "absent";
            summary.CostAbsent++;
        }
        else
        {
            var costMatch = SunCostRx.Match(costText);
            var cooldownMatch = CooldownRx.Match(costText);
            if (costMatch.Success && cooldownMatch.Success
                && int.TryParse(costMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCost)
                && double.TryParse(cooldownMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedCooldown))
            {
                costStatus = "parsed";
                sunCost = parsedCost;
                cooldownSec = parsedCooldown;
                summary.CostParsed++;
            }
            else
            {
                costStatus = "unparsed";
                summary.CostUnparsed++;
            }
        }

        var (hp, attack, armor, armorMax, statsObserved, statsSampleUtc) = ReadCombatBaselineUnlocked(hot, side, typeId);
        if (statsObserved) summary.StatsObserved++; else summary.StatsUnobserved++;
        if (side == "plant") summary.PlantsBuilt++; else if (side == "zombie") summary.ZombiesBuilt++;

        using var cmd = hot.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO almanac_seed(
              side, type_id, type_name, display_name, flavor_info, flavor_introduce,
              sun_cost, cooldown_sec, cost_status, hp, attack, armor, armor_max,
              stats_observed, stats_sample_utc, almanac_captured_utc, contract_version, rebuilt_utc)
            VALUES(
              $side,$type,$tn,$dn,$fi,$fintro,
              $sc,$cd,$cs,$hp,$atk,$arm,$armMax,
              $so,$ssu,$acu,$cv,$ru)
            ON CONFLICT(side, type_id) DO UPDATE SET
              type_name=excluded.type_name, display_name=excluded.display_name,
              flavor_info=excluded.flavor_info, flavor_introduce=excluded.flavor_introduce,
              sun_cost=excluded.sun_cost, cooldown_sec=excluded.cooldown_sec, cost_status=excluded.cost_status,
              hp=excluded.hp, attack=excluded.attack, armor=excluded.armor, armor_max=excluded.armor_max,
              stats_observed=excluded.stats_observed, stats_sample_utc=excluded.stats_sample_utc,
              almanac_captured_utc=excluded.almanac_captured_utc,
              contract_version=excluded.contract_version, rebuilt_utc=excluded.rebuilt_utc;
            """;
        cmd.Parameters.AddWithValue("$side", side);
        cmd.Parameters.AddWithValue("$type", typeId);
        cmd.Parameters.AddWithValue("$tn", (object?)typeName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$dn", (object?)displayName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$fi", (object?)flavorInfo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$fintro", (object?)flavorIntroduce ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$sc", (object?)sunCost ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cd", (object?)cooldownSec ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cs", costStatus);
        cmd.Parameters.AddWithValue("$hp", (object?)hp ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$atk", (object?)attack ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$arm", (object?)armor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$armMax", (object?)armorMax ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$so", statsObserved ? 1 : 0);
        cmd.Parameters.AddWithValue("$ssu", (object?)statsSampleUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$acu", almanacCapturedUtc);
        cmd.Parameters.AddWithValue("$cv", AlmanacSeedContractVersion);
        cmd.Parameters.AddWithValue("$ru", nowUtc);
        cmd.ExecuteNonQuery();
    }

    (int? Hp, int? Attack, int? Armor, int? ArmorMax, bool Observed, string? SampleUtc) ReadCombatBaselineUnlocked(
        SqliteConnection hot, string side, int typeId)
    {
        using var cmd = hot.CreateCommand();
        cmd.CommandText = side == "plant"
            ? "SELECT stats_json, captured_utc FROM spawn_stats WHERE side='plant' AND type=$t AND source='start' ORDER BY captured_utc ASC LIMIT 1;"
            : "SELECT stats_json, captured_utc FROM spawn_stats WHERE side='zombie' AND type=$t AND source IN ('start','initHealth') ORDER BY captured_utc ASC LIMIT 1;";
        cmd.Parameters.AddWithValue("$t", typeId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return (null, null, null, null, false, null);

        var statsJson = r.GetString(0);
        var sampleUtc = r.GetString(1);
        var hp = TryInt(statsJson, "hpBase");
        var attack = TryInt(statsJson, "attackBase");
        int? armor = null, armorMax = null;
        if (side == "zombie")
        {
            armor = TryInt(statsJson, "armorBase");
            armorMax = TryInt(statsJson, "armorMaxBase");
        }
        return (hp, attack, armor, armorMax, true, sampleUtc);
    }

    static int DeleteStaleAlmanacSeedRowsUnlocked(SqliteConnection hot, SqliteTransaction tx, HashSet<(string Side, int TypeId)> keep)
    {
        var toDelete = new List<(string Side, int TypeId)>();
        using (var cmd = hot.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT side, type_id FROM almanac_seed;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var key = (r.GetString(0), r.GetInt32(1));
                if (!keep.Contains(key)) toDelete.Add(key);
            }
        }
        foreach (var (side, typeId) in toDelete)
        {
            using var cmd = hot.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM almanac_seed WHERE side=$s AND type_id=$t;";
            cmd.Parameters.AddWithValue("$s", side);
            cmd.Parameters.AddWithValue("$t", typeId);
            cmd.ExecuteNonQuery();
        }
        return toDelete.Count;
    }

    static string? StripColorMarkup(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return ColorTagRx.Replace(text, "");
    }

    public AlmanacSeedDto? GetAlmanacSeed(string side, int typeId)
    {
        side = NormSide(side);
        lock (_gate)
        {
            using var hot = OpenUnlocked();
            return ReadAlmanacSeedUnlocked(hot, side, typeId);
        }
    }

    public List<AlmanacSeedDto> ListAlmanacSeed(string? side = null)
    {
        lock (_gate)
        {
            using var hot = OpenUnlocked();
            using var cmd = hot.CreateCommand();
            if (string.IsNullOrWhiteSpace(side))
                cmd.CommandText = "SELECT side, type_id FROM almanac_seed ORDER BY side, type_id;";
            else
            {
                cmd.CommandText = "SELECT side, type_id FROM almanac_seed WHERE side=$s ORDER BY type_id;";
                cmd.Parameters.AddWithValue("$s", NormSide(side));
            }
            var keys = new List<(string Side, int TypeId)>();
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    keys.Add((r.GetString(0), r.GetInt32(1)));
            }
            return keys.Select(k => ReadAlmanacSeedUnlocked(hot, k.Side, k.TypeId)!).Where(x => x != null).ToList()!;
        }
    }

    AlmanacSeedDto? ReadAlmanacSeedUnlocked(SqliteConnection hot, string side, int typeId)
    {
        using var cmd = hot.CreateCommand();
        cmd.CommandText = """
            SELECT type_name, display_name, flavor_info, flavor_introduce, sun_cost, cooldown_sec,
                   cost_status, hp, attack, armor, armor_max, stats_observed, contract_version, rebuilt_utc
            FROM almanac_seed WHERE side=$s AND type_id=$t;
            """;
        cmd.Parameters.AddWithValue("$s", side);
        cmd.Parameters.AddWithValue("$t", typeId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        var dto = new AlmanacSeedDto
        {
            Side = side,
            TypeId = typeId,
            TypeName = r.IsDBNull(0) ? null : r.GetString(0),
            DisplayName = r.IsDBNull(1) ? null : r.GetString(1),
            FlavorInfo = r.IsDBNull(2) ? null : r.GetString(2),
            FlavorIntroduce = r.IsDBNull(3) ? null : r.GetString(3),
            SunCost = r.IsDBNull(4) ? null : r.GetInt32(4),
            CooldownSec = r.IsDBNull(5) ? null : r.GetDouble(5),
            CostStatus = r.GetString(6),
            Hp = r.IsDBNull(7) ? null : r.GetInt32(7),
            Attack = r.IsDBNull(8) ? null : r.GetInt32(8),
            Armor = r.IsDBNull(9) ? null : r.GetInt32(9),
            ArmorMax = r.IsDBNull(10) ? null : r.GetInt32(10),
            StatsObserved = r.GetInt32(11) != 0,
            ContractVersion = r.GetInt32(12),
            RebuiltUtc = r.GetString(13)
        };
        r.Close();

        // Naming falls back to the live `types` row on read — types is the naming SSOT,
        // this table's type_name/display_name are only a rebuild-time snapshot.
        using (var tcmd = hot.CreateCommand())
        {
            tcmd.CommandText = "SELECT type_name, display_name FROM types WHERE game=$g AND side=$s AND type=$t;";
            tcmd.Parameters.AddWithValue("$g", RpgConstants.GameId);
            tcmd.Parameters.AddWithValue("$s", side);
            tcmd.Parameters.AddWithValue("$t", typeId);
            using var tr = tcmd.ExecuteReader();
            if (tr.Read())
            {
                // `types` is the naming SSOT (data-architecture.md §3) — a correction landing there
                // must be visible on the next read, not just when the rebuild-time snapshot is empty.
                var liveTypeName = tr.IsDBNull(0) ? null : tr.GetString(0);
                var liveDisplayName = tr.IsDBNull(1) ? null : tr.GetString(1);
                if (!string.IsNullOrWhiteSpace(liveTypeName)) dto.TypeName = liveTypeName;
                if (!string.IsNullOrWhiteSpace(liveDisplayName)) dto.DisplayName = liveDisplayName;
            }
        }

        dto.Enrichment = ReadAlmanacSeedEnrichmentUnlocked(hot, side, typeId);
        return dto;
    }
}
