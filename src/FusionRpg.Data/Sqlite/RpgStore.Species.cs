using System.Text.Json;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Stats.Derived;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>One row this import refused, and why. Never dropped silently.</summary>
public readonly record struct SpeciesImportError(string SpeciesId, string Detail)
{
    public override string ToString() => $"{SpeciesId}: {Detail}";
}

/// <summary>What one species import did, or refused to do — same "all or nothing, refusal names the
/// first failure and the total count" shape <c>ImportOutcome</c> already established for atoms.</summary>
public sealed record SpeciesImportOutcome(
    bool Committed, IReadOnlyList<SpeciesImportError> Errors, int Written, int Unchanged, int Deleted)
{
    public bool IsOk => Errors.Count == 0;
}

/// <summary>
/// `species-import` (T4.6, `spec-species-generator.md`'s downstream consumer, demon-seed module 13) —
/// `data/generated/demons/**` -> the `demon_species`/`demon_species_magnitude` tables, one transaction.
/// </summary>
public sealed partial class RpgStore
{
    void EnsureSpeciesSchemaUnlocked(SqliteConnection db)
    {
        Exec(db, """
            CREATE TABLE IF NOT EXISTS demon_species (
              species_id TEXT NOT NULL PRIMARY KEY,
              rarity TEXT NOT NULL,
              theta INTEGER NOT NULL,
              p_theta INTEGER NOT NULL,
              attack_interval_ms INTEGER NOT NULL,
              attack_interval_source TEXT NOT NULL,
              range_cells INTEGER NOT NULL,
              variant_count INTEGER NOT NULL,
              revision INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS demon_species_magnitude (
              species_id TEXT NOT NULL,
              channel TEXT NOT NULL,
              value INTEGER NOT NULL,
              PRIMARY KEY (species_id, channel)
            );
            """);

        // catalog-runtime pass-through columns (T4.8's own real precondition, resolved 2026-09-02) —
        // a database created before this migration has demon_species without them, so the addition
        // is explicit, matching effect_instance's own theta_content/content_scale_milli precedent
        // (T3.4). Defaults only ever apply to pre-migration rows read back after this point; a fresh
        // ImportSpecies call always supplies real values.
        EnsureColumn(db, "demon_species", "side", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(db, "demon_species", "game_type_id", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "demon_species", "element_primary", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(db, "demon_species", "element_secondary", "TEXT");
        EnsureColumn(db, "demon_species", "deploy_mode", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(db, "demon_species", "acquisition", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "demon_species", "variants_json", "TEXT NOT NULL DEFAULT '[]'");
        EnsureColumn(db, "demon_species", "trait_pool_json", "TEXT NOT NULL DEFAULT '[]'");
        EnsureColumn(db, "demon_species", "name", "TEXT");
    }

    /// <summary>
    /// Import a whole roster in one transaction — all or nothing (E14a's own guarantee, applied here):
    /// every row is checked before the first write, so one bad row writes nothing and the refusal
    /// names it plus the total count. A stored species absent from <paramref name="species"/> is
    /// DELETED (the roster this import describes is the roster that exists — a species removed
    /// upstream, e.g. de-classified, must not linger as a stale row nothing ever prunes).
    /// </summary>
    public SpeciesImportOutcome ImportSpecies(IReadOnlyList<ConcreteSpecies> species)
    {
        if (species is null) throw new ArgumentNullException(nameof(species));

        var errors = new List<SpeciesImportError>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in species)
        {
            if (string.IsNullOrWhiteSpace(s.SpeciesId))
                errors.Add(new SpeciesImportError(s.SpeciesId ?? "", "speciesId is empty"));
            else if (!seenIds.Add(s.SpeciesId))
                errors.Add(new SpeciesImportError(s.SpeciesId, "speciesId appears twice in this import"));
        }

        if (errors.Count > 0)
            return new SpeciesImportOutcome(false, errors, 0, 0, 0);

        // Resolved BEFORE the write transaction opens (matches T5.5/T5.6's own "compute first, write
        // second" discipline) — GetAlmanacSeed opens its own connection under its own lock(_gate),
        // and _gate is per-thread reentrant, but a second connection reading while this one holds an
        // open write transaction is complexity this import has no reason to carry.
        var names = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var s in species)
        {
            var almanac = GetAlmanacSeed(s.Side, s.GameTypeId);
            names[s.SpeciesId] = !string.IsNullOrWhiteSpace(almanac?.DisplayName) ? almanac.DisplayName
                : !string.IsNullOrWhiteSpace(almanac?.TypeName) ? almanac.TypeName
                : $"Demon {s.GameTypeId}";
        }

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();

            var written = 0;
            var unchanged = 0;

            foreach (var s in species)
            {
                var withName = s with { Name = names[s.SpeciesId] };
                var stored = ReadStoredUnlocked(db, tx, s.SpeciesId);
                if (stored is not null && SameContent(stored, withName))
                {
                    unchanged++;
                    continue;
                }

                ExecIn(db, tx, """
                    INSERT INTO demon_species
                      (species_id, rarity, theta, p_theta, attack_interval_ms, attack_interval_source,
                       range_cells, variant_count, revision, side, game_type_id, element_primary,
                       element_secondary, deploy_mode, acquisition, variants_json, trait_pool_json, name)
                    VALUES ($id, $rarity, $theta, $pTheta, $intervalMs, $intervalSource, $range, $variants, 1,
                            $side, $gameTypeId, $elPrimary, $elSecondary, $deployMode, $acquisition,
                            $variantsJson, $traitPoolJson, $name)
                    ON CONFLICT(species_id) DO UPDATE SET
                      rarity = excluded.rarity, theta = excluded.theta, p_theta = excluded.p_theta,
                      attack_interval_ms = excluded.attack_interval_ms,
                      attack_interval_source = excluded.attack_interval_source,
                      range_cells = excluded.range_cells, variant_count = excluded.variant_count,
                      side = excluded.side, game_type_id = excluded.game_type_id,
                      element_primary = excluded.element_primary, element_secondary = excluded.element_secondary,
                      deploy_mode = excluded.deploy_mode, acquisition = excluded.acquisition,
                      variants_json = excluded.variants_json, trait_pool_json = excluded.trait_pool_json,
                      name = excluded.name,
                      revision = demon_species.revision + 1;
                    """,
                    ("$id", s.SpeciesId), ("$rarity", s.Rarity.ToString()), ("$theta", s.Theta),
                    ("$pTheta", s.PTheta), ("$intervalMs", s.AttackIntervalMs),
                    ("$intervalSource", s.AttackIntervalSource), ("$range", s.RangeCells),
                    ("$variants", s.VariantCount),
                    ("$side", s.Side), ("$gameTypeId", s.GameTypeId),
                    ("$elPrimary", s.ElementPrimary.ToString()),
                    ("$elSecondary", (object?)s.ElementSecondary?.ToString() ?? DBNull.Value),
                    ("$deployMode", s.DeployMode.ToString()), ("$acquisition", (int)s.Acquisition),
                    ("$variantsJson", JsonSerializer.Serialize(s.Variants)),
                    ("$traitPoolJson", JsonSerializer.Serialize(s.TraitPool)),
                    ("$name", (object?)names[s.SpeciesId] ?? DBNull.Value));

                ExecIn(db, tx, "DELETE FROM demon_species_magnitude WHERE species_id = $id;", ("$id", s.SpeciesId));
                foreach (var (channel, value) in s.Magnitudes)
                    ExecIn(db, tx,
                        "INSERT INTO demon_species_magnitude (species_id, channel, value) VALUES ($id, $ch, $v);",
                        ("$id", s.SpeciesId), ("$ch", channel), ("$v", value));

                written++;
            }

            var deleted = 0;
            using (var cmd = db.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT species_id FROM demon_species;";
                using var r = cmd.ExecuteReader();
                var storedIds = new List<string>();
                while (r.Read()) storedIds.Add(r.GetString(0));
                foreach (var staleId in storedIds.Where(id => !seenIds.Contains(id)))
                {
                    ExecIn(db, tx, "DELETE FROM demon_species WHERE species_id = $id;", ("$id", staleId));
                    ExecIn(db, tx, "DELETE FROM demon_species_magnitude WHERE species_id = $id;", ("$id", staleId));
                    deleted++;
                }
            }

            tx.Commit();
            return new SpeciesImportOutcome(true, Array.Empty<SpeciesImportError>(), written, unchanged, deleted);
        }
    }

    /// <summary>
    /// `catalog-runtime`'s own snapshot source (T4.8, `spec-catalog-runtime.md` §3) — every stored
    /// species, converted to the shape `DemonSpeciesCatalog.Configure` needs. The ONE place
    /// `ConcreteSpecies` -> `DemonSpeciesDef` happens, so a host never hand-rolls the conversion.
    ///
    /// <para><c>SpeciesId</c> is lower-cased here — the anchor pipeline's own casing (`"Peashooter"`)
    /// and `DemonSpeciesCatalog.Validate`'s established lower-kebab rule (matching the compiled
    /// catalog's own real ids, e.g. `"driverzombie"`) are two different, already-shipped
    /// conventions; this is the one seam where the anchor pipeline's casing meets the catalog's own
    /// rule, so every other layer keeps reading/writing the anchor's own real casing unchanged.</para>
    ///
    /// <para><c>DemonTypeId</c> is computed here, once — <c>GameTypeId + DemonSpeciesCatalog.DemonTypeIdFloor</c>
    /// — never stored a second time (`ConcreteSpecies` deliberately does not carry it, its own doc
    /// comment already says why).</para>
    ///
    /// <para>⛔ <c>TraitPool</c> is deliberately left empty here, not <c>s.TraitPool</c> — found for
    /// real 2026-09-02, not assumed: the anchor's own `traits` field is an OPEN, free-form array
    /// (`anchor/schema.py`'s own `_open_array_prop`, unvalidated LLM flavor text — `pea.json`'s own
    /// real values are `"Projectile-launching"`, `"Defensive"`, `"Rapid-fire"`), while
    /// `DemonSpeciesDef.TraitPool` is validated against `DemonTraitCatalog`'s CLOSED, curated
    /// gameplay vocabulary (`"regenerator"`, `"berserker"`, `"loyal"`, ...) — two different
    /// vocabularies that happen to share a field name. Wiring one into the other was tried and
    /// caught by `SpeciesCatalogDiffTests.The_store_backed_snapshot_itself_passes_DemonSpeciesCatalog_Validate`,
    /// which threw exactly the mismatch this comment describes. `ConcreteSpecies.TraitPool` keeps
    /// carrying the anchor's own raw flavor strings (a legitimate, separate use — `species_effects.py`
    /// already reads the anchor's own `traits` field as LLM brief context) — only the SNAPSHOT
    /// conversion into the gameplay-validated field stops here, honestly, rather than picking a
    /// silently-wrong mapping. Assigning real trait ids to anchor-derived species is a genuine open
    /// design question (which of ~20 curated gameplay traits fits a given species?) this task does
    /// not answer.</para>
    /// </summary>
    public IReadOnlyList<Core.Demons.DemonSpeciesDef> BuildDemonSpeciesSnapshot()
    {
        var ids = ListSpeciesIds();
        var snapshot = new List<Core.Demons.DemonSpeciesDef>(ids.Count);
        foreach (var id in ids)
        {
            var s = GetSpecies(id);
            if (s is null) continue; // deleted between the two reads — a fresh Configure call retries

            snapshot.Add(new Core.Demons.DemonSpeciesDef
            {
                SpeciesId = s.SpeciesId.Trim().ToLowerInvariant(),
                Name = s.Name ?? s.SpeciesId,
                Side = s.Side,
                GameTypeId = s.GameTypeId,
                DemonTypeId = s.GameTypeId + Core.Demons.DemonSpeciesCatalog.DemonTypeIdFloor,
                ElementPrimary = s.ElementPrimary,
                ElementSecondary = s.ElementSecondary,
                BaseRarity = s.Rarity,
                DeployMode = s.DeployMode,
                Acquisition = s.Acquisition,
                Variants = s.Variants,
                TraitPool = Array.Empty<string>(),
            });
        }
        return snapshot;
    }

    public ConcreteSpecies? GetSpecies(string speciesId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return ReadStoredUnlocked(db, null, speciesId);
        }
    }

    public IReadOnlyList<string> ListSpeciesIds()
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT species_id FROM demon_species ORDER BY species_id;";
            using var r = cmd.ExecuteReader();
            var ids = new List<string>();
            while (r.Read()) ids.Add(r.GetString(0));
            return ids;
        }
    }

    static ConcreteSpecies? ReadStoredUnlocked(SqliteConnection db, SqliteTransaction? tx, string speciesId)
    {
        ConcreteSpecies? head;
        using (var cmd = db.CreateCommand())
        {
            if (tx is not null) cmd.Transaction = tx;
            cmd.CommandText = """
                SELECT species_id, rarity, theta, p_theta, attack_interval_ms, attack_interval_source,
                       range_cells, variant_count, side, game_type_id, element_primary, element_secondary,
                       deploy_mode, acquisition, variants_json, trait_pool_json, name
                FROM demon_species WHERE species_id = $id;
                """;
            cmd.Parameters.AddWithValue("$id", speciesId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            if (!DemonRarityIds.TryParse(r.GetString(1), out var rarity))
                throw new InvalidOperationException($"stored species '{speciesId}' has an unparseable rarity '{r.GetString(1)}'");
            if (!Enum.TryParse<ElementTypeId>(r.GetString(10), out var elementPrimary))
                throw new InvalidOperationException($"stored species '{speciesId}' has an unparseable elementPrimary '{r.GetString(10)}'");
            ElementTypeId? elementSecondary = null;
            if (!r.IsDBNull(11))
            {
                if (!Enum.TryParse<ElementTypeId>(r.GetString(11), out var parsedSec))
                    throw new InvalidOperationException($"stored species '{speciesId}' has an unparseable elementSecondary '{r.GetString(11)}'");
                elementSecondary = parsedSec;
            }
            if (!Enum.TryParse<DemonDeployMode>(r.GetString(12), out var deployMode))
                throw new InvalidOperationException($"stored species '{speciesId}' has an unparseable deployMode '{r.GetString(12)}'");

            head = new ConcreteSpecies
            {
                SpeciesId = r.GetString(0), Rarity = rarity, Theta = r.GetInt32(2), PTheta = r.GetInt64(3),
                AttackIntervalMs = r.GetInt64(4), AttackIntervalSource = r.GetString(5),
                RangeCells = r.GetInt64(6), VariantCount = r.GetInt32(7),
                Side = r.GetString(8), GameTypeId = r.GetInt32(9),
                ElementPrimary = elementPrimary, ElementSecondary = elementSecondary,
                DeployMode = deployMode, Acquisition = (DemonAcquisition)r.GetInt32(13),
                Variants = JsonSerializer.Deserialize<string[]>(r.GetString(14)) ?? Array.Empty<string>(),
                TraitPool = JsonSerializer.Deserialize<string[]>(r.GetString(15)) ?? Array.Empty<string>(),
                Name = r.IsDBNull(16) ? null : r.GetString(16),
            };
        }

        var magnitudes = new Dictionary<string, long>(StringComparer.Ordinal);
        using (var cmd = db.CreateCommand())
        {
            if (tx is not null) cmd.Transaction = tx;
            cmd.CommandText = "SELECT channel, value FROM demon_species_magnitude WHERE species_id = $id;";
            cmd.Parameters.AddWithValue("$id", speciesId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) magnitudes[r.GetString(0)] = r.GetInt64(1);
        }

        return head with { Magnitudes = magnitudes };
    }

    static bool SameContent(ConcreteSpecies stored, ConcreteSpecies incoming) =>
        stored.Rarity == incoming.Rarity && stored.Theta == incoming.Theta && stored.PTheta == incoming.PTheta
        && stored.AttackIntervalMs == incoming.AttackIntervalMs
        && stored.AttackIntervalSource == incoming.AttackIntervalSource
        && stored.RangeCells == incoming.RangeCells && stored.VariantCount == incoming.VariantCount
        && stored.Side == incoming.Side && stored.GameTypeId == incoming.GameTypeId
        && stored.ElementPrimary == incoming.ElementPrimary && stored.ElementSecondary == incoming.ElementSecondary
        && stored.DeployMode == incoming.DeployMode && stored.Acquisition == incoming.Acquisition
        && stored.Variants.SequenceEqual(incoming.Variants) && stored.TraitPool.SequenceEqual(incoming.TraitPool)
        && stored.Name == incoming.Name
        && stored.Magnitudes.Count == incoming.Magnitudes.Count
        && stored.Magnitudes.All(kv => incoming.Magnitudes.TryGetValue(kv.Key, out var v) && v == kv.Value);
}
