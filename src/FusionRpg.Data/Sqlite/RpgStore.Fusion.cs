using System.Text.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Fusion;
using FusionRpg.Core.Stats.Derived;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

public static class FusionModes
{
    public const string StarMerge = "star-merge";
    public const string Promotion = "promotion";
    public const string Recipe = "recipe";
}

public sealed record FusionRequest(
    string Mode,
    string? BaseInstanceId,
    IReadOnlyList<string> SacrificeInstanceIds,
    string? PickedTraitId);

public sealed record FusionOutcome(
    bool Replayed,
    string Mode,
    DemonSpecimenDto? Base,
    DemonSpecimenDto? Minted,
    string? RecipeId,
    bool NewlyDiscovered,
    long DiscoverySouls,
    SoulBalanceDto Balance);

public sealed record DemonLineageRow(long Id, string InstanceId, string Event, string DetailJson, string T);

public sealed partial class RpgStore
{
    /// <summary>Test-only: throws after sacrifices are consumed to prove transaction atomicity.</summary>
    internal Action? FusionMidTestHook;

    /// <summary>
    /// The fusion transaction (spec-demon-fusion.md): replay-check → validate → spend Souls +
    /// materials → consume sacrifices → mutate base or mint output → lineage → log — ONE
    /// gate-serialized transaction, refusals write nothing (ExecuteSummon discipline).
    /// </summary>
    public (bool Ok, string Reason, FusionOutcome? Outcome) ExecuteFusion(
        long playerId, string correlationId, FusionRequest request, ulong seed)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return (false, "correlation.missing", null);
        var corr = correlationId.Trim();

        // Normalize ids ONCE — actor reads trim but the is-base/duplicate guards compare strings,
        // so an untrimmed " abc " base could pass its own trimmed id as a sacrifice and consume
        // itself (2026-08-21 review, Important 1). One canonical form kills the class.
        request = request with
        {
            BaseInstanceId = string.IsNullOrWhiteSpace(request.BaseInstanceId)
                ? null
                : request.BaseInstanceId.Trim(),
            SacrificeInstanceIds = request.SacrificeInstanceIds
                .Select(s => (s ?? "").Trim())
                .ToList()
        };

        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null) return (false, "player.unknown", null);
            using var tx = db.BeginTransaction();
            var now = DateTime.UtcNow.ToString("o");

            // Replay: validate the stored request, rebuild the outcome from current rows,
            // spend nothing (the atomic log row is the gate — no check-then-act window).
            var stored = ReadFusionLogUnlocked(db, playerId, corr);
            if (stored != null)
            {
                var storedRequest = JsonSerializer.Deserialize<FusionRequest>(stored.Value.InputsJson);
                if (storedRequest is null || !SameRequest(storedRequest, request))
                    return (false, "correlation.mismatch", null);
                tx.Commit();
                return (true, "replay", RebuildOutcomeUnlocked(db, playerId, stored.Value));
            }

            var result = request.Mode switch
            {
                FusionModes.StarMerge => StarMergeUnlocked(db, playerId, corr, request, seed, now),
                FusionModes.Promotion => PromotionUnlocked(db, playerId, corr, request, seed, now),
                FusionModes.Recipe => RecipeUnlocked(db, playerId, corr, request, seed, now),
                _ => (false, "mode.unknown", null)
            };

            if (!result.Ok)
                return result; // tx disposes → rollback; refusals write nothing

            tx.Commit();
            return result;
        }
    }

    (bool Ok, string Reason, FusionOutcome? Outcome) StarMergeUnlocked(
        SqliteConnection db, long playerId, string corr, FusionRequest request, ulong seed, string now)
    {
        var (ok, reason, baseActor, baseProfile, baseRarity) =
            ReadFusionBaseUnlocked(db, playerId, request.BaseInstanceId);
        if (!ok) return (false, reason, null);

        var targetStar = baseProfile!.Star + 1;
        if (targetStar > StarPolicy.StarCap(baseRarity))
            return (false, "star.maxed", null);
        var validation = ValidateSacrificesUnlocked(
            db, playerId, request, StarPolicy.SacrificesForStar(targetStar), requireRarity: baseRarity);
        if (validation != "") return (false, validation, null);

        var cost = FusionCostTable.StarMerge(baseRarity);
        var spendReason = SpendFusionCostsUnlocked(db, playerId, corr, cost, baseProfile.ElementPrimary, now);
        if (spendReason != "") return (false, spendReason, null);

        ConsumeSacrificesUnlocked(db, request.SacrificeInstanceIds, baseActor!.InstanceId, now);
        FusionMidTestHook?.Invoke();

        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE rpg_demon_profiles SET star = $s, revision = revision + 1
                WHERE instance_id = $id;
                """;
            cmd.Parameters.AddWithValue("$s", targetStar);
            cmd.Parameters.AddWithValue("$id", baseActor.InstanceId);
            cmd.ExecuteNonQuery();
        }

        AppendLineageUnlocked(db, baseActor.InstanceId, "star-merge",
            JsonSerializer.Serialize(new { star = targetStar, sacrifices = request.SacrificeInstanceIds }), now);
        // Merges consume no randomness, but the log records the seed the caller actually sent —
        // an auditor diffing the log against request traces must see the truth.
        AppendFusionLogUnlocked(db, playerId, corr, FusionModes.StarMerge, request,
            JsonSerializer.Serialize(new { baseInstanceId = baseActor.InstanceId, star = targetStar }), seed, now);

        return (true, "", new FusionOutcome(
            false, FusionModes.StarMerge,
            ReadSpecimenUnlocked(db, baseActor.InstanceId), null, null, false, 0,
            ReadSoulBalanceUnlocked(db, playerId)));
    }

    (bool Ok, string Reason, FusionOutcome? Outcome) PromotionUnlocked(
        SqliteConnection db, long playerId, string corr, FusionRequest request, ulong seed, string now)
    {
        var (ok, reason, baseActor, baseProfile, baseRarity) =
            ReadFusionBaseUnlocked(db, playerId, request.BaseInstanceId);
        if (!ok) return (false, reason, null);
        if (request.SacrificeInstanceIds.Count != 0) return (false, "sacrifices.count", null);
        if (!StarPolicy.CanPromote(baseRarity, baseProfile!.Star, baseProfile.Promoted))
            return (false, "promotion.not-ready", null);

        var newRarity = DemonRarityLadder.OneRungAbove(baseRarity);
        var cost = FusionCostTable.Promotion(newRarity);
        var spendReason = SpendFusionCostsUnlocked(db, playerId, corr, cost, baseProfile.ElementPrimary, now);
        if (spendReason != "") return (false, spendReason, null);
        FusionMidTestHook?.Invoke();

        var species = DemonSpeciesCatalog.Get(baseProfile.SpeciesId);
        var traits = FusionRoller.RollPromotionTraits(
            species, baseProfile.TraitIds, FusionRoller.SlotsFor(newRarity), seed);
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE rpg_demon_profiles SET
                  rarity = $r, star = 0, promoted = 1, traits_json = $traits, revision = revision + 1
                WHERE instance_id = $id;
                """;
            cmd.Parameters.AddWithValue("$r", newRarity.ToId());
            cmd.Parameters.AddWithValue("$traits", JsonSerializer.Serialize(traits));
            cmd.Parameters.AddWithValue("$id", baseActor!.InstanceId);
            cmd.ExecuteNonQuery();
        }

        AppendLineageUnlocked(db, baseActor.InstanceId, "promotion",
            JsonSerializer.Serialize(new { from = baseRarity.ToId(), to = newRarity.ToId() }), now);
        AppendFusionLogUnlocked(db, playerId, corr, FusionModes.Promotion, request,
            JsonSerializer.Serialize(new { baseInstanceId = baseActor.InstanceId, rarity = newRarity.ToId() }),
            seed, now);

        return (true, "", new FusionOutcome(
            false, FusionModes.Promotion,
            ReadSpecimenUnlocked(db, baseActor.InstanceId), null, null, false, 0,
            ReadSoulBalanceUnlocked(db, playerId)));
    }

    (bool Ok, string Reason, FusionOutcome? Outcome) RecipeUnlocked(
        SqliteConnection db, long playerId, string corr, FusionRequest request, ulong seed, string now)
    {
        // Recipe mode has no base — refusing a stray base id beats silently ignoring a demon the
        // caller thought was participating (2026-08-21 review S4).
        if (request.BaseInstanceId != null) return (false, "base.unexpected", null);
        var validation = ValidateSacrificesUnlocked(db, playerId, request, requiredCount: 2, requireRarity: null);
        if (validation != "") return (false, validation, null);

        var profileA = ReadDemonProfileUnlocked(db, request.SacrificeInstanceIds[0])!;
        var profileB = ReadDemonProfileUnlocked(db, request.SacrificeInstanceIds[1])!;
        var recipe = DemonRecipeCatalog.TryMatch(profileA.SpeciesId, profileB.SpeciesId);
        if (recipe is null) return (false, "recipe.unknown", null);

        var combinedTraits = profileA.TraitIds.Concat(profileB.TraitIds)
            .Distinct(StringComparer.Ordinal).ToList();
        if (string.IsNullOrWhiteSpace(request.PickedTraitId)) return (false, "trait.missing", null);
        if (!combinedTraits.Contains(request.PickedTraitId, StringComparer.Ordinal))
            return (false, "trait.not-on-inputs", null);

        var output = DemonSpeciesCatalog.Get(recipe.OutputSpeciesId);
        var cost = FusionCostTable.Recipe(output.BaseRarity);
        var spendReason = SpendFusionCostsUnlocked(
            db, playerId, corr, cost, output.ElementPrimary.ToElementId(), now);
        if (spendReason != "") return (false, spendReason, null);

        ConsumeSacrificesUnlocked(db, request.SacrificeInstanceIds, "recipe:" + recipe.RecipeId, now);
        FusionMidTestHook?.Invoke();

        var roll = FusionRoller.Roll(output, output.BaseRarity, request.PickedTraitId!, combinedTraits, seed);
        var minted = MintDemonUnlocked(db, playerId, new DemonMintSpec
        {
            SpeciesId = output.SpeciesId,
            Side = output.Side,
            GameTypeId = output.GameTypeId,
            Rarity = output.BaseRarity.ToId(),
            Variant = roll.Variant,
            ElementPrimary = output.ElementPrimary.ToElementId(),
            ElementSecondary = output.ElementSecondary?.ToElementId(),
            TraitIds = roll.TraitIds.ToList(),
            Origin = "fusion"
        }, now, out var speciesNewlyDiscovered);

        // Recipe discovery: first-ever success pays the output rarity's discovery bonus once.
        // The ledger append's return is the last word — a hand-edited discovery table must not
        // announce Souls that deduped away (2026-08-21 review).
        bool newlyDiscovered;
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                INSERT OR IGNORE INTO rpg_fusion_discovery(player_id, recipe_id, t)
                VALUES($p, $r, $t);
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$r", recipe.RecipeId);
            cmd.Parameters.AddWithValue("$t", now);
            newlyDiscovered = cmd.ExecuteNonQuery() > 0;
        }

        long discoverySouls = 0;
        if (newlyDiscovered)
        {
            discoverySouls = SoulEarnPolicy.DiscoveryDelta(output.BaseRarity);
            if (!AppendSoulLedgerUnlocked(db, playerId, 0, discoverySouls,
                    SoulEarnPolicy.Reasons.Discovery, null, null, "recipe:" + recipe.RecipeId, now))
            {
                newlyDiscovered = false;
                discoverySouls = 0;
            }
        }

        // One discovery policy across every acquisition path: a species first obtained via
        // fusion pays the same species bonus a summon would (dedupe `species:{id}` makes it
        // once-ever regardless of which path got there first — 2026-08-21 review S5).
        var speciesReward = SoulEarnPolicy.DiscoveryDelta(output.BaseRarity);
        if (speciesNewlyDiscovered && speciesReward > 0
            && AppendSoulLedgerUnlocked(db, playerId, 0, speciesReward,
                SoulEarnPolicy.Reasons.Discovery, "species", output.SpeciesId,
                "species:" + output.SpeciesId, now))
        {
            discoverySouls += speciesReward;
        }

        // Update lineage: the newborn knows its ingredients; consumed-by rows point at it.
        AppendLineageUnlocked(db, minted.Actor.InstanceId, "recipe-birth",
            JsonSerializer.Serialize(new
            {
                recipeId = recipe.RecipeId,
                inputs = request.SacrificeInstanceIds
            }), now);
        AppendFusionLogUnlocked(db, playerId, corr, FusionModes.Recipe, request,
            JsonSerializer.Serialize(new
            {
                mintedInstanceId = minted.Actor.InstanceId,
                recipeId = recipe.RecipeId,
                // Stored so a replay reproduces the discovery reveal — a client that lost the
                // original response must still see its banner (2026-08-21 review, Important 2).
                newlyDiscovered,
                discoverySouls
            }), seed, now);

        return (true, "", new FusionOutcome(
            false, FusionModes.Recipe, null, minted, recipe.RecipeId,
            newlyDiscovered, discoverySouls, ReadSoulBalanceUnlocked(db, playerId)));
    }

    public List<string> ListFusionDiscoveries(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT recipe_id FROM rpg_fusion_discovery WHERE player_id = $p ORDER BY recipe_id;
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            using var r = cmd.ExecuteReader();
            var ids = new List<string>();
            while (r.Read())
                ids.Add(r.GetString(0));
            return ids;
        }
    }

    // ---- shared validation + spend helpers ----

    (bool Ok, string Reason, UniqueActorDto? Actor, DemonProfileDto? Profile, DemonRarity Rarity)
        ReadFusionBaseUnlocked(SqliteConnection db, long playerId, string? instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return (false, "base.missing", null, null, default);
        var actor = ReadUniqueActorUnlocked(db, instanceId.Trim());
        var profile = actor == null ? null : ReadDemonProfileUnlocked(db, actor.InstanceId);
        if (actor is null || actor.PlayerId != playerId || profile is null)
            return (false, "base.missing", null, null, default);
        if (!string.Equals(actor.Phase, UniqueActorPhases.Roster, StringComparison.Ordinal))
            return (false, "base.phase", null, null, default);
        if (HasActiveExpeditionMembershipUnlocked(db, actor.InstanceId))
            return (false, "base.on-expedition", null, null, default);
        if (!DemonRarityIds.TryParse(profile.Rarity, out var rarity))
            return (false, "base.rarity", null, null, default);
        return (true, "", actor, profile, rarity);
    }

    /// <summary>"" = valid; otherwise the refusal reason. Locked specimens are player-protected
    /// from consumption — the lock's teeth (owner lock 1/7).</summary>
    string ValidateSacrificesUnlocked(
        SqliteConnection db, long playerId, FusionRequest request, int requiredCount, DemonRarity? requireRarity)
    {
        var ids = request.SacrificeInstanceIds;
        if (ids.Count != requiredCount) return "sacrifices.count";
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count) return "sacrifice.duplicate";
        foreach (var id in ids)
        {
            if (string.Equals(id, request.BaseInstanceId, StringComparison.Ordinal))
                return "sacrifice.is-base";
            var actor = ReadUniqueActorUnlocked(db, id);
            var profile = actor == null ? null : ReadDemonProfileUnlocked(db, id);
            if (actor is null || actor.PlayerId != playerId || profile is null)
                return "sacrifice.invalid";
            if (!string.Equals(actor.Phase, UniqueActorPhases.Roster, StringComparison.Ordinal))
                return "sacrifice.phase";
            if (profile.Locked) return "sacrifice.locked";
            if (HasActiveExpeditionMembershipUnlocked(db, id)) return "sacrifice.on-expedition";
            // The designation has teeth like the lock: the active patron is unconsumable
            // (spec-patron-demon.md) — it may still LEAD a merge, only consumption refuses.
            if (IsPatronUnlocked(db, playerId, id)) return "sacrifice.is-patron";
            if (requireRarity is { } band)
            {
                if (!DemonRarityIds.TryParse(profile.Rarity, out var rarity) || rarity != band)
                    return "sacrifice.rarity";
            }
        }

        return "";
    }

    /// <summary>"" = paid; otherwise the refusal reason. Souls + shards + result-element essences.</summary>
    string SpendFusionCostsUnlocked(
        SqliteConnection db, long playerId, string corr, FusionCost cost, string resultElementId, string now)
    {
        var balance = ReadSoulBalanceUnlocked(db, playerId);
        if (balance.Balance < cost.Souls) return "souls.insufficient";

        var drops = new List<(string MaterialId, long Qty)>
        {
            ("shard." + cost.ShardRarity.ToId(), cost.ShardCount),
            ("essence." + resultElementId, cost.EssenceCount)
        };
        if (!TrySpendDemonMaterialsUnlocked(db, playerId, drops, now))
            return "materials.insufficient";

        if (!AppendSoulLedgerUnlocked(db, playerId, 0, -cost.Souls, SoulEarnPolicy.Reasons.Fusion,
                "spend", corr, corr, now))
            throw new InvalidOperationException("fusion spend dedupe collision — correlation reused outside the fusion log");
        return "";
    }

    bool TrySpendDemonMaterialsUnlocked(
        SqliteConnection db, long playerId, IReadOnlyList<(string MaterialId, long Qty)> needs, string now)
    {
        foreach (var (materialId, qty) in needs)
        {
            if (!DemonMaterialCatalog.IsKnown(materialId))
                throw new ArgumentException($"Unknown demon material id '{materialId}'.");
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                UPDATE rpg_demon_materials SET qty = qty - $q, updated_utc = $t
                WHERE player_id = $p AND material_id = $m AND qty >= $q;
                """;
            cmd.Parameters.AddWithValue("$q", qty);
            cmd.Parameters.AddWithValue("$t", now); // one timestamp per fusion, tx-consistent
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$m", materialId);
            if (cmd.ExecuteNonQuery() == 0)
                return false; // insufficient — the enclosing tx rolls earlier decrements back
        }

        return true;
    }

    void ConsumeSacrificesUnlocked(
        SqliteConnection db, IReadOnlyList<string> sacrificeIds, string consumerInstanceId, string now)
    {
        foreach (var id in sacrificeIds)
        {
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    UPDATE rpg_unique_actors SET phase = $phase, revision = revision + 1, updated_utc = $now
                    WHERE instance_id = $id;
                    """;
                cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.Retired);
                cmd.Parameters.AddWithValue("$now", now);
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
            }

            AppendLineageUnlocked(db, id, "consumed-by",
                JsonSerializer.Serialize(new { consumer = consumerInstanceId }), now);
            ReleaseContractOnRetireUnlocked(db, id, now);
        }
    }

    void AppendLineageUnlocked(SqliteConnection db, string instanceId, string @event, string detailJson, string now)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO rpg_demon_lineage(instance_id, event, detail_json, t)
            VALUES($i, $e, $d, $t);
            """;
        cmd.Parameters.AddWithValue("$i", instanceId);
        cmd.Parameters.AddWithValue("$e", @event);
        cmd.Parameters.AddWithValue("$d", detailJson);
        cmd.Parameters.AddWithValue("$t", now);
        cmd.ExecuteNonQuery();
    }

    void AppendFusionLogUnlocked(
        SqliteConnection db, long playerId, string corr, string mode, FusionRequest request,
        string outputJson, ulong seed, string now)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO rpg_fusion_log(player_id, correlation_id, mode, inputs_json, output_json, seed, t)
            VALUES($p, $c, $m, $in, $out, $seed, $t);
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$c", corr);
        cmd.Parameters.AddWithValue("$m", mode);
        cmd.Parameters.AddWithValue("$in", JsonSerializer.Serialize(request));
        cmd.Parameters.AddWithValue("$out", outputJson);
        cmd.Parameters.AddWithValue("$seed", seed.ToString());
        cmd.Parameters.AddWithValue("$t", now);
        cmd.ExecuteNonQuery();
    }

    // ---- reads ----

    public List<DemonLineageRow> ListDemonLineage(string instanceId, int limit = 100)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT id, instance_id, event, detail_json, t FROM rpg_demon_lineage
                WHERE instance_id = $i ORDER BY id ASC LIMIT $l;
                """;
            cmd.Parameters.AddWithValue("$i", instanceId ?? "");
            cmd.Parameters.AddWithValue("$l", limit);
            using var r = cmd.ExecuteReader();
            var rows = new List<DemonLineageRow>();
            while (r.Read())
                rows.Add(new DemonLineageRow(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4)));
            return rows;
        }
    }

    public (string Mode, string InputsJson, string OutputJson, ulong Seed)? TryGetFusionLog(
        long playerId, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return null;
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return ReadFusionLogUnlocked(db, playerId, correlationId.Trim());
        }
    }

    (string Mode, string InputsJson, string OutputJson, ulong Seed)? ReadFusionLogUnlocked(
        SqliteConnection db, long playerId, string corr)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT mode, inputs_json, output_json, seed FROM rpg_fusion_log
            WHERE player_id = $p AND correlation_id = $c;
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$c", corr);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return (r.GetString(0), r.GetString(1), r.GetString(2), ParseSeed(r.GetString(3)));
    }

    DemonSpecimenDto? ReadSpecimenUnlocked(SqliteConnection db, string instanceId)
    {
        var actor = ReadUniqueActorUnlocked(db, instanceId);
        var profile = actor == null ? null : ReadDemonProfileUnlocked(db, instanceId);
        return actor == null || profile == null ? null : new DemonSpecimenDto { Actor = actor, Profile = profile };
    }

    /// <summary>
    /// Replay semantics: the log's job is idempotency, not a snapshot. Specimens and the balance
    /// are re-read LIVE (a later merge, rename, or spend is reflected — returning the stale
    /// mid-flight state would contradict every other surface). Only the discovery result is
    /// event-like and comes back from the stored output, so the reveal survives a lost response.
    /// </summary>
    FusionOutcome RebuildOutcomeUnlocked(
        SqliteConnection db, long playerId, (string Mode, string InputsJson, string OutputJson, ulong Seed) log)
    {
        using var doc = JsonDocument.Parse(log.OutputJson);
        var root = doc.RootElement;
        DemonSpecimenDto? baseSpecimen = null;
        DemonSpecimenDto? minted = null;
        string? recipeId = null;
        if (root.TryGetProperty("baseInstanceId", out var b) && b.GetString() is { } baseId)
            baseSpecimen = ReadSpecimenUnlocked(db, baseId);
        if (root.TryGetProperty("mintedInstanceId", out var m) && m.GetString() is { } mintedId)
            minted = ReadSpecimenUnlocked(db, mintedId);
        if (root.TryGetProperty("recipeId", out var rec))
            recipeId = rec.GetString();
        var newlyDiscovered = root.TryGetProperty("newlyDiscovered", out var nd) && nd.GetBoolean();
        var discoverySouls = root.TryGetProperty("discoverySouls", out var ds) ? ds.GetInt64() : 0;
        return new FusionOutcome(true, log.Mode, baseSpecimen, minted, recipeId,
            newlyDiscovered, discoverySouls, ReadSoulBalanceUnlocked(db, playerId));
    }

    static bool SameRequest(FusionRequest stored, FusionRequest request) =>
        stored.Mode == request.Mode
        && stored.BaseInstanceId == request.BaseInstanceId
        && stored.PickedTraitId == request.PickedTraitId
        && stored.SacrificeInstanceIds.SequenceEqual(request.SacrificeInstanceIds, StringComparer.Ordinal);
}
