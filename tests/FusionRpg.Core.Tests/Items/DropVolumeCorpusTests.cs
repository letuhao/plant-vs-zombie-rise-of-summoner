using System.Text.Json;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Drops;
using Xunit;

namespace FusionRpg.Core.Tests.Items;

/// <summary>
/// `drop-volume` (item module 11) against the REAL shipped corpora — `data/seed/loot/**`,
/// `data/seed/rarity/ladder.v1.json`, `data/tuning/item-rarity.v1.json`,
/// `data/seed/items/base-types/**`, and the 40-table seedsmith corpus at
/// `data/seed/items/drop-tables/`. Nothing here is synthetic.
/// </summary>
public class DropVolumeCorpusTests
{
    static string RepoRoot() => DropVolumeTests.RepoRoot();

    static string LootDir() => Path.Combine(RepoRoot(), "data", "seed", "loot");

    internal static LootCorpus Corpus() => LootCorpusReader.Merge(
        Directory.EnumerateFiles(LootDir(), "*.json")
            .Where(p => Path.GetFileName(p).StartsWith("tables", StringComparison.Ordinal))
            .Select(p => LootCorpusReader.Parse(File.ReadAllText(p))));

    /// <summary>
    /// The ten rungs, joined from the two REAL files module 7 shipped: the seeded ladder rows
    /// (`data/seed/rarity/ladder.v1.json`) and the re-derived drop weights
    /// (`data/tuning/item-rarity.v1.json` → the `drop_weight_default` budget key).
    /// </summary>
    internal static IReadOnlyList<RarityRung> Ladder()
    {
        var tuning = ItemRarityTuning.Parse(
            File.ReadAllText(Path.Combine(RepoRoot(), "data", "tuning", "item-rarity.v1.json")));

        using var doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoRoot(), "data", "seed", "rarity", "ladder.v1.json")));

        var rungs = new List<RarityRung>();
        foreach (var e in doc.RootElement.GetProperty("entries").EnumerateArray())
        {
            var id = e.GetProperty("id").GetString()!;
            rungs.Add(new RarityRung(
                id, e.GetProperty("ordinal").GetInt32(),
                e.GetProperty("prefixRolls").GetInt32(), e.GetProperty("suffixRolls").GetInt32(),
                e.GetProperty("minTier").GetInt32(), e.GetProperty("maxTier").GetInt32(),
                tuning[id].DropWeightPer100k));
        }

        return rungs;
    }

    internal static IReadOnlyDictionary<(string Frame, string Role), List<string>> BaseTypes()
    {
        var dir = Path.Combine(RepoRoot(), "data", "seed", "items", "base-types");
        var map = new Dictionary<(string, string), List<string>>();
        foreach (var path in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("entries", out var entries)) continue;
            foreach (var e in entries.EnumerateArray())
            {
                if (e.TryGetProperty("enabled", out var en) && en.ValueKind == JsonValueKind.False) continue;
                var key = (e.GetProperty("frame").GetString()!, e.GetProperty("role").GetString()!);
                if (!map.TryGetValue(key, out var list)) map[key] = list = new List<string>();
                list.Add(e.GetProperty("id").GetString()!);
            }
        }

        foreach (var list in map.Values) list.Sort(StringComparer.Ordinal);
        return map;
    }

    // ---- Correction 1: the calibration, exactly, at Θ = 20 ---------------------------------------

    [Fact]
    public void At_theta_pin_the_shipped_per_event_yields_hold()
    {
        var t = DropVolumeTests.Tuning();
        var corpus = Corpus();
        var byId = corpus.Tables.ToDictionary(x => x.TableId, StringComparer.Ordinal);
        var scale = DropVolume.VolumeScaleMilli(t.ThetaPin, t);
        Assert.Equal(1000, scale);

        // Every row of spec-drop-volume.md §Correction 1, in per-mille, exactly.
        var expected = new (string TableId, long PerMille)[]
        {
            ("drop.web.wave-normal", 550),
            ("drop.web.wave-boss", 1400),
            ("drop.exp.scout-30m", 700),
            ("drop.exp.forage-4h", 1600),
            ("drop.exp.hunt-8h", 2600),
            ("drop.exp.warpath-20h", 4200),
            ("drop.world.sector-clear", 1500),
            ("drop.pvz.run", 500),
        };

        foreach (var (tableId, perMille) in expected)
        {
            var table = Assert.Contains(tableId, (IDictionary<string, DropTableRow>)byId);
            var actual = DropTableDraw.ExpectedEquipmentPerMille(
                table, itemLevel: 10, scale, id => byId.TryGetValue(id, out var x) ? x : null,
                maxDepth: t.MaxNestingDepth);
            Assert.Equal(perMille, actual);
        }
    }

    [Fact]
    public void Volume_scales_draw_counts_never_weights()
    {
        // Composition at Θ=20 and Θ=200 is statistically identical; only the COUNT moves. Scaling
        // weights would change composition, which is L0's job (X4), not volume's.
        var t = DropVolumeTests.Tuning();
        var corpus = Corpus();
        var byId = corpus.Tables.ToDictionary(x => x.TableId, StringComparer.Ordinal);
        var table = byId["drop.web.wave-normal"];

        var atPin = DropTableDraw.ExpectedEquipmentPerMille(table, 10,
            DropVolume.VolumeScaleMilli(20, t), id => byId.TryGetValue(id, out var x) ? x : null);
        var high = DropTableDraw.ExpectedEquipmentPerMille(table, 10,
            DropVolume.VolumeScaleMilli(200, t), id => byId.TryGetValue(id, out var x) ? x : null);

        Assert.True(high > atPin);

        // The yield ratio equals the SCALE ratio exactly — the per-draw composition is untouched.
        Assert.Equal(DropVolume.VolumeScaleMilli(200, t) * atPin / 1000, high);

        // And the base-type mix of the shared slate does not move with Θ: it is a plain uniform.
        var slate = byId["drop.shared.hybrid-core-any"].Groups.Single().Entries;
        Assert.Equal(24, slate.Count);
        Assert.Single(slate.Select(e => e.Weight).Distinct());
    }

    // ---- the corpus itself ------------------------------------------------------------------------

    [Fact]
    public void The_shipped_loot_corpus_validates()
    {
        var t = DropVolumeTests.Tuning();
        var corpus = Corpus();
        var rarityIds = RarityLadder.RungIds.ToHashSet(StringComparer.Ordinal);

        var result = DropTableValidator.Validate(corpus.Sources, corpus.Tables, t, new DropContentLookups(
            CurrencyExists: id => id == "souls",
            RarityIdExists: rarityIds.Contains,
            RarityOrdinalExists: o => Ladder().Any(r => r.Ordinal == o)));

        Assert.True(result.IsOk, result.ToString());
    }

    [Fact]
    public void Every_equipment_entry_names_a_real_frame_and_role_with_real_base_types()
    {
        var baseTypes = BaseTypes();
        foreach (var table in Corpus().Tables)
            foreach (var g in table.Groups)
                foreach (var e in g.Entries.Where(x => x.Kind == DropEntryKind.Equipment))
                {
                    Assert.NotNull(e.Frame);
                    Assert.NotNull(e.Role);
                    Assert.True(baseTypes.ContainsKey((e.Frame!, e.Role!)),
                        $"{table.TableId}/{g.GroupKey}: no base type exists for ({e.Frame}, {e.Role})");
                    Assert.NotEmpty(baseTypes[(e.Frame!, e.Role!)]);
                }
    }

    [Fact]
    public void Affix_channel_is_authored_on_every_equipment_entry_and_only_boss_tables_use_boss()
    {
        foreach (var table in Corpus().Tables)
            foreach (var g in table.Groups)
                foreach (var e in g.Entries.Where(x => x.Kind == DropEntryKind.Equipment))
                    Assert.True(AffixChannels.IsKnown(e.AffixChannel),
                        $"{table.TableId}/{g.GroupKey} seq {e.Seq} carries channel '{e.AffixChannel}'");

        var byId = Corpus().Tables.ToDictionary(x => x.TableId, StringComparer.Ordinal);
        Assert.All(byId["drop.shared.hybrid-core-boss"].Groups.Single().Entries,
            e => Assert.Equal(AffixChannels.Boss, e.AffixChannel));
        Assert.All(byId["drop.shared.hybrid-core-any"].Groups.Single().Entries,
            e => Assert.Equal(AffixChannels.Drop, e.AffixChannel));
    }

    [Fact]
    public void No_pvz_run_loot_source_is_authored_and_one_would_be_refused_by_name()
    {
        var corpus = Corpus();
        Assert.DoesNotContain(corpus.Sources, s => s.SourceKind == DropTableValidator.UndesignedSourceKind);

        var t = DropVolumeTests.Tuning();
        var withPvz = corpus.Sources.Append(
            new LootSourceRow("pvz-run", "run-1", "drop.pvz.run", 5)).ToList();

        var result = DropTableValidator.Validate(withPvz, corpus.Tables, t);
        Assert.Equal(AtomRejectionReason.ContentRuleViolated, result.Reason);
        Assert.Contains("drop.source-kind-undesigned", result.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("defaulted to 1", result.Detail.Replace("rather than defaulted", ""),
            StringComparison.Ordinal);
    }

    [Fact]
    public void At_least_one_first_clear_grant_is_rung_100()
    {
        // §3.8's deterministic source for `almanac`, as a CORPUS test rather than a runtime check.
        var grants = Corpus().Sources
            .Where(s => !string.IsNullOrEmpty(s.FirstClearGrant))
            .Select(s => s.FirstClearGrant!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(grants);

        // The grant container lives in data/seed/containers/ — an OWNED folder of SeedScanner — so
        // it imports through the standard AtomSeedFile path like every other container, rather than
        // through a second writer beside the drop tables.
        var containerDir = Path.Combine(RepoRoot(), "data", "seed", "containers");
        var rarities = new Dictionary<string, string>(StringComparer.Ordinal);
        var noRolls = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(containerDir, "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.GetProperty("kind").GetString() != "container") continue;
            foreach (var e in doc.RootElement.GetProperty("entries").EnumerateArray())
            {
                var id = e.GetProperty("id").GetString()!;
                if (e.TryGetProperty("rarity", out var r)) rarities[id] = r.GetString()!;

                // §3.5: "a fixed, authored item... no rolls at all, so it never disappoints."
                if (e.GetProperty("prefixRolls").GetInt32() == 0
                    && e.GetProperty("suffixRolls").GetInt32() == 0
                    && !e.TryGetProperty("pool", out _))
                    noRolls.Add(id);
            }
        }

        Assert.Contains(grants, g => rarities.TryGetValue(g, out var rung) && rung == "almanac");
        Assert.All(grants, g => Assert.Contains(g, noRolls));

        // This is also the FIRST shipped container to name a rarity at all — data/seed/rarity's own
        // README records that none did, which makes it the first live exercise of the
        // effect_container.rarity FK module 7 wired.
        Assert.Contains("almanac", rarities.Values);
    }

    [Fact]
    public void RecommendedLevel_appears_nowhere_in_items_drops()
    {
        var dropsDir = Path.Combine(RepoRoot(), "src", "FusionRpg.Core", "Items", "Drops");
        foreach (var file in Directory.EnumerateFiles(dropsDir, "*.cs"))
            Assert.DoesNotContain("RecommendedLevel", DropVolumeTests.CodeOnly(File.ReadAllText(file)), StringComparison.Ordinal);

        // The corpus names the live field instead — WaveDef.ContentIndex, which IS Θ_content.
        var raw = File.ReadAllText(Path.Combine(LootDir(), "tables.v1.json"));
        Assert.Contains("WaveDef.ContentIndex", raw, StringComparison.Ordinal);
    }

    [Fact]
    public void Content_levels_match_the_shipped_wave_and_expedition_catalogs()
    {
        // Cross-checked against shipped code, not against the spec's prose.
        var byKey = Corpus().Sources.ToDictionary(s => s.Key, StringComparer.Ordinal);

        Assert.Equal(1, byKey["web-wave:rift-skirmish"].ContentLevel);
        Assert.Equal(3, byKey["web-wave:rift-warband"].ContentLevel);
        Assert.Equal(6, byKey["web-wave:rift-onslaught"].ContentLevel);
        Assert.Equal(10, byKey["web-wave:rift-tyrant"].ContentLevel);

        foreach (var tier in new[] { "scout-30m", "forage-4h", "hunt-8h", "warpath-20h" })
            Assert.True(byKey.ContainsKey("expedition-tier:" + tier), $"no loot_source for tier '{tier}'");
    }

    // ---- the OTHER corpus: the seedsmith drop tables ---------------------------------------------

    [Fact]
    public void The_seedsmith_drop_table_corpus_uses_kinds_this_build_cannot_yet_resolve()
    {
        // ⛔ Not a synthetic probe: this is the REAL 40-table corpus at
        // data/seed/items/drop-tables/, and it holds 315 entries of four kinds whose payload
        // machinery does not exist. The refusal must be BY NAME, per kind, naming the owning module
        // — never a silent drop.
        var dir = Path.Combine(RepoRoot(), "data", "seed", "items", "drop-tables");
        var counts = new Dictionary<DropEntryKind, int>();
        var tables = 0;

        foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var e in doc.RootElement.GetProperty("entries").EnumerateArray())
            {
                tables++;
                foreach (var g in e.GetProperty("groups").EnumerateArray())
                    foreach (var row in g.GetProperty("entries").EnumerateArray())
                    {
                        var name = row.GetProperty("entryKind").GetString()!;
                        Assert.True(LootCorpusReader.TryKind(name, out var kind),
                            $"'{name}' is outside the nine-value enum entry-shapes.md §9 declares");
                        counts[kind] = counts.GetValueOrDefault(kind) + 1;
                    }
            }
        }

        Assert.Equal(40, tables);

        // The two kinds spec-drop-volume.md's own data-shape table omits are REAL and shipped —
        // wave R2 added them to entry-shapes.md §9 on 2026-08-23 and the corpus uses them heavily.
        Assert.Equal(144, counts[DropEntryKind.Unique]);
        Assert.Equal(60, counts[DropEntryKind.Consumable]);
        Assert.Equal(70, counts[DropEntryKind.Charm]);
        Assert.Equal(41, counts[DropEntryKind.Insert]);

        var unavailable = counts.Where(kv => !DropTableDraw.IsAvailable(kv.Key)).Sum(kv => kv.Value);
        Assert.Equal(315, unavailable);
    }

    [Fact]
    public void An_insert_or_charm_entry_is_refused_by_name_until_x7_lands()
    {
        // Verified against shipped code rather than asserted: ContainerKind has six values and none
        // of D27's four, so X7 has not landed.
        var kinds = Enum.GetNames(typeof(ContainerKind));
        Assert.Equal(6, kinds.Length);
        Assert.DoesNotContain("Gem", kinds);
        Assert.DoesNotContain("Charm", kinds);

        var t = DropVolumeTests.Tuning();
        foreach (var kind in new[]
                 {
                     DropEntryKind.Insert, DropEntryKind.Charm,
                     DropEntryKind.Consumable, DropEntryKind.Unique,
                 })
        {
            var table = new DropTableRow("drop.test.x7", new[] { "web" }, null, null, true, 1, new[]
            {
                new DropTableGroupRow("g", 0, 1, new[]
                {
                    new DropTableEntryRow(0, kind, "some.ref", 100),
                    new DropTableEntryRow(1, DropEntryKind.Nothing, "", 900),
                }),
            });

            var result = DropTableValidator.Validate(Array.Empty<LootSourceRow>(), new[] { table }, t);
            Assert.Equal(AtomRejectionReason.ContentRuleViolated, result.Reason);
            Assert.Contains("drop.entry-kind-unavailable", result.Detail, StringComparison.Ordinal);
            Assert.Contains(LootCorpusReader.KindName(kind), result.Detail, StringComparison.Ordinal);
            // The refusal names WHO lands it — a build order, not a defect.
            Assert.Contains("module", result.Detail, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void No_new_member_of_the_closed_thirty_three_code_list()
    {
        // spec-drop-volume.md's own success criterion, and it is checkable. I12 asked for eight new
        // codes; this module mints none.
        var names = Enum.GetNames(typeof(AtomRejectionReason));
        Assert.Equal(35, names.Length); // 33 + None + ContentRuleViolated
        Assert.Contains("None", names);
        Assert.Contains("ContentRuleViolated", names);

        foreach (var invented in new[]
                 {
                     "UnknownDropTable", "UnknownBaseTypeSet", "UnknownCurrency", "DropTableDepthExceeded",
                     "DropTableCycle", "StandaloneRuleViolation", "RarityUnsatisfiable", "LootReplayMismatch",
                 })
            Assert.DoesNotContain(invented, names);

        // And the namespace this module raises under is registered.
        DropTableValidator.Validate(Array.Empty<LootSourceRow>(), Array.Empty<DropTableRow>(),
            DropVolumeTests.Tuning());
        Assert.True(ContentRuleNamespaces.IsRegistered("drop.entry-kind-unavailable"));
    }
}
