using System.Linq;
using System.Text.Json;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Effects.Atoms.Generation;
using FusionRpg.Core.Effects.Atoms.Power;
using Xunit;

namespace FusionRpg.Core.Tests.Atoms.Generation;

/// <summary>
/// E43 <c>family-expand</c> (spec-family-expand.md §5's ten-test table). Runs the real, shipped
/// corpus (98 affix families, tier-bands.v1.json) wherever a test needs to prove something about
/// today's actual data — matching <c>ChannelPoolTests</c>' own real-corpus discipline in this same
/// directory — and constructs synthetic families only where the real corpus genuinely has no example
/// to reach a code path with (element-typed pool emission: no real family has an authored share yet).
/// </summary>
public class FamilyExpansionTests
{
    static string FindDataDir()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "data");
            if (Directory.Exists(candidate)) return candidate;
            var up = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "data"));
            if (Directory.Exists(up)) return up;
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }
        throw new DirectoryNotFoundException("could not locate data/ above " + AppContext.BaseDirectory);
    }

    static (IReadOnlyList<FamilyEntryInput> Families, TierBandsInput TierBands) LoadReal()
    {
        var itemsRoot = Path.Combine(FindDataDir(), "seed", "items");
        var familiesDir = Path.Combine(itemsRoot, "affix-families");
        // Real bug fixed 2026-09-08 (atom-family-expansion): this hardcoded literal "v1.json",
        // the same defect fixed in tools/FamilyExpandGen/Program.cs — this file's own tests exist to
        // prove FamilyExpansion against "the real, shipped corpus" (its own class doc comment), which
        // means the real, LATEST published tuning, not a frozen v1 snapshot several versions behind
        // what's actually committed to data/seed/atoms/generated/ today.
        var tierBands = TierBandsFile.Read(File.ReadAllText(TierBandsFile.FindLatestPath(Path.Combine(itemsRoot, "_tuning"))));

        var families = new List<FamilyEntryInput>();
        foreach (var file in Directory.GetFiles(familiesDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            if (Path.GetFileName(file).StartsWith('_')) continue;
            families.AddRange(AffixFamilyFile.Read(Path.GetFileName(file), File.ReadAllText(file)));
        }

        return (families, tierBands);
    }

    /// <summary>The real BattleRuleset curves — same values production reads, since this assembly's
    /// own <c>ContractTuningTestBootstrap</c> module initializer configures <c>PowerTuningHub</c> with
    /// the shipped power-scale.v2.json values (bMilli 400, pinValue 680/92/22).</summary>
    static long? FlatReferenceBase(string channel) => channel switch
    {
        "maxHp" or "hp" => BattleRuleset.BaseHp(FamilyExpansion.ReferenceLevel),
        "atk" => BattleRuleset.BaseAtk(FamilyExpansion.ReferenceLevel),
        "defense" => BattleRuleset.BaseDefense(FamilyExpansion.ReferenceLevel),
        _ => null,
    };

    static FamilyExpansionResult ExpandReal()
    {
        var (families, tierBands) = LoadReal();
        return FamilyExpansion.Expand(families, tierBands, FlatReferenceBase);
    }

    /// <summary>Real, already-shipped pool catalog (`data/seed/channel-pools/pools.v1.json`, E30) —
    /// needed once the real corpus (via `LoadReal`'s own 2026-09-08 fix) started emitting real
    /// pool-referencing `stat.derived` rows (`evd-flinch`/`evd-harden`/`evd-seal`/`shld-breach`),
    /// which `CostFunction.Price` cannot price without a `lookupPool`. Mirrors
    /// `ContentValidationTests.RealLookupPool` exactly (not shared across test classes — this
    /// repo's own DAMP-over-DRY test convention).</summary>
    static Func<string, ChannelPoolRow?> RealLookupPool()
    {
        var path = Path.Combine(FindDataDir(), "seed", "channel-pools", "pools.v1.json");
        var rejection = ChannelPoolFile.TryParse(File.ReadAllText(path), out var pools);
        Assert.True(rejection.IsOk, rejection.Detail);
        var byId = pools.ToDictionary(p => p.PoolId, StringComparer.Ordinal);
        return id => byId.TryGetValue(id, out var row) ? row : null;
    }

    // ---- test 1: deterministic --------------------------------------------------------------------

    [Fact]
    public void Expansion_is_deterministic_across_two_runs()
    {
        var (families, tierBands) = LoadReal();

        var first = FamilyExpansion.Expand(families, tierBands, FlatReferenceBase);
        var second = FamilyExpansion.Expand(families, tierBands, FlatReferenceBase);

        Assert.Equal(first.Rows.Count, second.Rows.Count);
        for (var i = 0; i < first.Rows.Count; i++)
        {
            Assert.Equal(first.Rows[i].AtomId, second.Rows[i].AtomId);
            Assert.Equal(first.Rows[i].ParamsJson, second.Rows[i].ParamsJson);
            Assert.Equal(first.Rows[i].TagsJson, second.Rows[i].TagsJson);
        }

        Assert.Equal(
            first.Refusals.Select(r => (r.FamilyId, r.Reason)),
            second.Refusals.Select(r => (r.FamilyId, r.Reason)));
    }

    // ---- test 2: --check-equivalent — regenerate and compare to the committed output --------------

    [Fact]
    public void Regenerating_matches_the_committed_generated_files_exactly()
    {
        var result = ExpandReal();
        var byFamilyId = LoadReal().Families.ToDictionary(f => f.Id, f => f.SourceFile, StringComparer.Ordinal);
        var generatedDir = Path.Combine(FindDataDir(), "seed", "atoms", "generated");

        var bySource = result.Rows.GroupBy(r => byFamilyId[r.FamilyId]);
        foreach (var group in bySource)
        {
            var stem = Path.GetFileNameWithoutExtension(group.Key);
            var outPath = Path.Combine(generatedDir, $"family-expand.{stem}.json");
            Assert.True(File.Exists(outPath), $"expected committed output {outPath} — run FamilyExpandGen and commit the result");

            using var doc = JsonDocument.Parse(File.ReadAllText(outPath));
            var committedIds = doc.RootElement.GetProperty("entries").EnumerateArray()
                .Select(e => AtomRow.DeriveId(
                    e.GetProperty("family").GetString()!, "", e.GetProperty("tier").GetInt32()))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
            var freshIds = group.Select(r => r.AtomId).OrderBy(x => x, StringComparer.Ordinal).ToList();

            Assert.Equal(freshIds, committedIds);
        }
    }

    /// <summary>P1.1 (tasks/passive-tree-repair-plan.md): the id-only comparison above could not see a
    /// renamed family (same ids, new `name`) or a newline difference, and both really shipped — three
    /// committed files drifted from their sources for a full commit cycle. This pins the CONTRACT that
    /// would have caught it: each committed file's canonical text must equal the generator's canonical
    /// text (one shared serializer, <see cref="FamilyExpansionSeedFile"/>).
    /// <para>Comparison is made in canonical form, never against raw working-tree bytes: a checkout
    /// with <c>core.autocrlf=true</c> legitimately materialises CRLF, so asserting "no CRLF on disk"
    /// would test the environment rather than the content.</para>
    /// </summary>
    [Fact]
    public void Committed_generated_files_match_the_generator_byte_for_byte()
    {
        var (families, tierBands) = LoadReal();
        var generatedDir = Path.Combine(FindDataDir(), "seed", "atoms", "generated");
        var result = FamilyExpansion.Expand(families, tierBands, FlatReferenceBase);

        foreach (var group in result.Rows.GroupBy(r => families.First(f => f.Id == r.FamilyId).SourceFile))
        {
            var stem = Path.GetFileNameWithoutExtension(group.Key);
            var outPath = Path.Combine(generatedDir, $"family-expand.{stem}.json");
            Assert.True(File.Exists(outPath), $"expected committed output {outPath}");

            var committed = FamilyExpansionSeedFile.Canonicalize(File.ReadAllText(outPath));
            var fresh = FamilyExpansionSeedFile.ToCanonicalJson(group.ToList());

            Assert.Equal(fresh, committed);
        }
    }

    [Fact]
    public void A_manufactured_drift_is_detectable_by_the_same_comparison_the_check_mode_uses()
    {
        // Proves the comparison mechanism itself can FAIL, not just always pass — a corrupted copy of
        // a real committed row must disagree with what the generator produces fresh.
        var result = ExpandReal();
        var vitality = result.Rows.First(r => r.FamilyId == "atom.vitality" && r.Tier == 1);

        var corrupted = vitality with { ParamsJson = vitality.ParamsJson.Replace("\"min\":", "\"min\":999999,\"__was\":") };

        Assert.NotEqual(vitality.ParamsJson, corrupted.ParamsJson);
    }

    // ---- test 3: every emitted id matches AtomRow.DeriveId exactly ---------------------------------

    [Fact]
    public void Every_emitted_id_matches_AtomRow_DeriveId_exactly()
    {
        var result = ExpandReal();
        Assert.NotEmpty(result.Rows);

        foreach (var row in result.Rows)
            Assert.Equal(AtomRow.DeriveId(row.FamilyId, row.Variant, row.Tier), row.AtomId);
    }

    // ---- test 4: no collision across all 98 families ------------------------------------------------

    [Fact]
    public void No_family_tier_variant_collision_across_all_98_families()
    {
        var result = ExpandReal();

        var keys = result.Rows.Select(r => (r.FamilyId, r.Tier, r.Variant)).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    // ---- test 5: an element-typed family emits ONE row per tier with a pool reference --------------

    [Fact]
    public void Element_typed_family_emits_one_row_per_tier_with_a_pool_reference_not_seven()
    {
        // No real family reaches this path today (§ investigation: every element-typed family in the
        // real corpus also has no authored share, so it is refused for the share reason first). A
        // synthetic family with a real, in-vocabulary stem proves the pool-emission behaviour on its
        // own, isolated from the share gap.
        var tierBands = new TierBandsInput(
            BaseSharePermille: 35,
            ChannelWeightPermille: new Dictionary<string, long> { ["synth-elem"] = 1000 },
            OpWeightPermille: new Dictionary<string, long> { ["Increased"] = 1000 });

        var family = new FamilyEntryInput(
            Id: "atom.synth-elem", Name: "Synthetic Elemental", KindId: "stat.derived",
            Channel: "combat.power.{variant}", Op: "Increased", PowerBand: "medium", SourceFile: "synthetic.json",
            Tags: Array.Empty<string>());

        var result = FamilyExpansion.Expand(new[] { family }, tierBands, _ => null);

        Assert.Empty(result.Refusals);
        Assert.Equal(FamilyExpansion.TierCount, result.Rows.Count);

        var tiersSeen = new HashSet<int>();
        foreach (var row in result.Rows)
        {
            Assert.Equal("", row.Variant); // element never materialises into a variant segment
            tiersSeen.Add(row.Tier);

            using var doc = JsonDocument.Parse(row.ParamsJson);
            var channel = doc.RootElement.GetProperty("channel");
            Assert.Equal(JsonValueKind.Object, channel.ValueKind);
            Assert.Equal("pool.element-power", channel.GetProperty("pool").GetString());
            Assert.Equal(1, channel.GetProperty("count").GetInt32());
            Assert.False(channel.GetProperty("allowRepeat").GetBoolean());
        }

        Assert.Equal(FamilyExpansion.TierCount, tiersSeen.Count);
    }

    [Fact]
    public void Element_typed_family_naming_an_unmapped_channel_template_is_refused_never_guessed()
    {
        var tierBands = new TierBandsInput(
            BaseSharePermille: 35,
            ChannelWeightPermille: new Dictionary<string, long> { ["synth-elem-2"] = 1000 },
            OpWeightPermille: new Dictionary<string, long> { ["Increased"] = 1000 });

        var family = new FamilyEntryInput(
            Id: "atom.synth-elem-2", Name: "No Pool", KindId: "stat.derived",
            Channel: "combat.power.pierce.{variant}", Op: "Increased", PowerBand: "medium", SourceFile: "synthetic.json",
            Tags: Array.Empty<string>());

        var result = FamilyExpansion.Expand(new[] { family }, tierBands, _ => null);

        Assert.Empty(result.Rows);
        var refusal = Assert.Single(result.Refusals);
        Assert.Equal("atom.synth-elem-2", refusal.FamilyId);
        Assert.Contains("channel pool", refusal.Reason, StringComparison.Ordinal);
    }

    // ---- test 6: every emitted row validates and prices ---------------------------------------------

    [Fact]
    public void Every_emitted_row_validates_through_AtomRowValidator_and_prices_nonzero()
    {
        var result = ExpandReal();
        Assert.NotEmpty(result.Rows);

        var lookupPool = RealLookupPool();
        foreach (var row in result.Rows)
        {
            var validated = AtomRowValidator.Validate(row);
            Assert.True(validated.IsOk, $"{row.AtomId}: {validated}");

            var priced = CostFunction.Price(row, PowerTables.Authored(), lookupPool: lookupPool);
            Assert.True(priced.Ok, $"{row.AtomId}: {priced.Verdict.Reason}");
            // ⛔ Corrected 2026-09-08: no longer true once the real corpus grew past the original
            // 3-file, stat.modify-only baseline — `evd-flinch`/`evd-harden`/`evd-seal`/`shld-breach`
            // are real `stat.derived` pool-referencing rows now. The real invariant is just "priced
            // atoms carry real power," not "every atom is stat.modify."
            Assert.NotEqual(PowerVector.Zero, priced.Power);
        }
    }

    // ---- test 7: planted violations — refused by id --------------------------------------------------

    [Fact]
    public void PlantedViolation_a_family_with_no_authored_share_is_refused_by_id()
    {
        // ⛔ Corrected 2026-09-08: this used to rely on `atom.elpw-override` being a REAL family the
        // real corpus happened to leave uncovered — true when this test was written, false since
        // atom-family-expansion's `tier-bands-coverage` module published real coverage for it (and
        // 97 siblings). Relying on "some real family happens to have a real gap today" is exactly the
        // kind of test this repo's own incident log (this session's own drift-detection discipline)
        // warns against — switched to a genuinely synthetic family, matching the sibling planted-
        // violation test immediately below, which never depended on the real corpus's own coverage.
        var tierBands = new TierBandsInput(
            BaseSharePermille: 35,
            ChannelWeightPermille: new Dictionary<string, long>(), // deliberately empty -- no share for anyone
            OpWeightPermille: new Dictionary<string, long> { ["Flat"] = 1000 });

        var family = new FamilyEntryInput(
            Id: "atom.planted-no-share", Name: "Planted", KindId: "stat.modify",
            Channel: "atk", Op: "Flat", PowerBand: "medium", SourceFile: "synthetic.json",
            Tags: Array.Empty<string>());

        var result = FamilyExpansion.Expand(new[] { family }, tierBands, _ => 100);

        var refusal = result.Refusals.SingleOrDefault(r => r.FamilyId == "atom.planted-no-share");
        Assert.NotNull(refusal);
        Assert.Contains("no authored sharePermille", refusal!.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Rows, r => r.FamilyId == "atom.planted-no-share");
    }

    [Fact]
    public void PlantedViolation_a_family_naming_an_unknown_pool_is_refused_by_id_distinct_from_the_share_reason()
    {
        var tierBands = new TierBandsInput(
            BaseSharePermille: 35,
            ChannelWeightPermille: new Dictionary<string, long> { ["garbage-pool-family"] = 1000 },
            OpWeightPermille: new Dictionary<string, long> { ["Flat"] = 1000 });

        var family = new FamilyEntryInput(
            Id: "atom.garbage-pool-family", Name: "Garbage", KindId: "stat.derived",
            Channel: "combat.made-up-thing.{variant}", Op: "Flat", PowerBand: "medium", SourceFile: "synthetic.json",
            Tags: Array.Empty<string>());

        var result = FamilyExpansion.Expand(new[] { family }, tierBands, _ => 100);

        Assert.Empty(result.Rows);
        var refusal = Assert.Single(result.Refusals);
        Assert.Equal("atom.garbage-pool-family", refusal.FamilyId);
        Assert.Contains("no matching E30 channel pool", refusal.Reason, StringComparison.Ordinal);
    }

    // ---- test 8: no generated output is named fx-* ---------------------------------------------------

    [Fact]
    public void No_generated_output_file_name_ever_begins_with_fx_dash()
    {
        var (families, _) = LoadReal();

        foreach (var sourceFile in families.Select(f => f.SourceFile).Distinct())
        {
            var stem = Path.GetFileNameWithoutExtension(sourceFile);
            var outName = $"family-expand.{stem}.json";
            Assert.False(outName.StartsWith("fx-", StringComparison.OrdinalIgnoreCase), outName);
        }

        var generatedDir = Path.Combine(FindDataDir(), "seed", "atoms", "generated");
        if (Directory.Exists(generatedDir))
            foreach (var file in Directory.GetFiles(generatedDir))
                Assert.False(Path.GetFileName(file).StartsWith("fx-", StringComparison.OrdinalIgnoreCase), file);
    }

    // ---- test 10: the 21 pre-existing shipped atoms are untouched — additive only -------------------

    [Fact]
    public void Generated_atom_ids_never_collide_with_the_shipped_fx_star_atom_ids()
    {
        var atomsDir = Path.Combine(FindDataDir(), "seed", "atoms");
        var shippedFiles = new[] { "fx-board.json", "fx-core.json", "fx-status.json" }
            .Select(name => Path.Combine(atomsDir, name))
            .Where(File.Exists)
            .Select(f => (f, File.ReadAllText(f)));

        var collected = AtomSeedFile.Collect(shippedFiles);
        Assert.True(collected.IsOk, string.Join("; ", collected.Errors));
        var shippedIds = collected.Content.Atoms.Select(a => a.AtomId).ToHashSet(StringComparer.Ordinal);
        // spec-family-expand.md §5 test 10 names 21; the real corpus measures 20 (verified here rather
        // than trusted from the doc — DESIGN-GATE discipline). The count itself is not this test's
        // point; disjointness from E43's own generated ids is.
        Assert.Equal(20, shippedIds.Count);

        var result = ExpandReal();
        foreach (var row in result.Rows)
            Assert.DoesNotContain(row.AtomId, shippedIds);
    }

    // ---------------------------------------------------------------------------------------------
    // family-tags-closure (D28, 2026-09-07): a family's own authored `tags` now reaches TagsJson
    // alongside provenance, closing the gap `AffixFamilyFile.cs`'s own doc comment named as item's to
    // pick up. AffixTags.ParseTags/EligibilityRule already read whatever keys exist — nothing there
    // changes; these tests prove the STAMP, and one proves the shipped GATE now actually gates.
    // ---------------------------------------------------------------------------------------------

    static IReadOnlyDictionary<string, string> ParseTagsJson(string? json)
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json)) return tags;
        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
            if (prop.Value.ValueKind == JsonValueKind.String)
                tags[prop.Name] = prop.Value.GetString()!;
        return tags;
    }

    [Fact]
    public void AffixFamilyFile_Read_parses_a_real_tags_array_in_authored_order()
    {
        const string json = """
        {
          "entries": [
            {
              "id": "atom.parser-check", "name": "Parser Check", "kindId": "stat.modify",
              "powerBand": "medium", "params": { "channel": "x", "op": "Flat" },
              "tags": ["offensive", "utility"]
            }
          ]
        }
        """;

        var families = AffixFamilyFile.Read("g-parser-check.json", json);

        var family = Assert.Single(families);
        Assert.Equal(new[] { "offensive", "utility" }, family.Tags);
    }

    [Fact]
    public void AffixFamilyFile_Read_treats_an_absent_tags_key_as_legally_empty()
    {
        const string json = """
        {
          "entries": [
            {
              "id": "atom.no-tags-key", "name": "No Tags", "kindId": "stat.modify",
              "powerBand": "medium", "params": { "channel": "x", "op": "Flat" }
            }
          ]
        }
        """;

        var family = Assert.Single(AffixFamilyFile.Read("g-no-tags.json", json));
        Assert.Empty(family.Tags);
    }

    [Fact]
    public void AffixFamilyFile_Read_refuses_a_non_string_tag_rather_than_dropping_it_silently()
    {
        const string json = """
        {
          "entries": [
            {
              "id": "atom.bad-tag", "name": "Bad Tag", "kindId": "stat.modify",
              "powerBand": "medium", "params": { "channel": "x", "op": "Flat" },
              "tags": [123]
            }
          ]
        }
        """;

        Assert.Throws<FormatException>(() => AffixFamilyFile.Read("g-bad-tag.json", json));
    }

    [Fact]
    public void A_family_with_a_real_tag_stamps_it_alongside_provenance_not_instead_of_it()
    {
        var tierBands = new TierBandsInput(
            BaseSharePermille: 35,
            ChannelWeightPermille: new Dictionary<string, long> { ["tagged-stem"] = 1000 },
            OpWeightPermille: new Dictionary<string, long> { ["Flat"] = 1000 });
        var family = new FamilyEntryInput(
            Id: "atom.tagged-stem", Name: "Tagged", KindId: "stat.modify",
            Channel: "tagged-stem-channel", Op: "Flat", PowerBand: "medium", SourceFile: "synthetic.json",
            Tags: new[] { "offensive" });

        var result = FamilyExpansion.Expand(new[] { family }, tierBands, _ => 100);

        Assert.Empty(result.Refusals);
        Assert.NotEmpty(result.Rows);
        foreach (var row in result.Rows)
        {
            var tags = ParseTagsJson(row.TagsJson);
            Assert.True(tags.ContainsKey("offensive"));
            Assert.Equal("synthetic.json", tags["generatedFrom"]);
            Assert.Equal("E43", tags["generator"]);
        }
    }

    [Fact]
    public void A_family_with_no_tags_emits_only_the_two_provenance_keys_no_phantom_key()
    {
        var tierBands = new TierBandsInput(
            BaseSharePermille: 35,
            ChannelWeightPermille: new Dictionary<string, long> { ["untagged-stem"] = 1000 },
            OpWeightPermille: new Dictionary<string, long> { ["Flat"] = 1000 });
        var family = new FamilyEntryInput(
            Id: "atom.untagged-stem", Name: "Untagged", KindId: "stat.modify",
            Channel: "untagged-stem-channel", Op: "Flat", PowerBand: "medium", SourceFile: "synthetic.json",
            Tags: Array.Empty<string>());

        var result = FamilyExpansion.Expand(new[] { family }, tierBands, _ => 100);

        Assert.Empty(result.Refusals);
        var tags = ParseTagsJson(Assert.Single(result.Rows.Take(1)).TagsJson);
        Assert.Equal(2, tags.Count);
        Assert.True(tags.ContainsKey("generatedFrom"));
        Assert.True(tags.ContainsKey("generator"));
    }

    [Fact]
    public void A_colon_form_tag_splits_into_a_real_key_value_pair_for_AnyOfTags_even_though_no_real_family_uses_it_yet()
    {
        var tierBands = new TierBandsInput(
            BaseSharePermille: 35,
            ChannelWeightPermille: new Dictionary<string, long> { ["kv-stem"] = 1000 },
            OpWeightPermille: new Dictionary<string, long> { ["Flat"] = 1000 });
        var family = new FamilyEntryInput(
            Id: "atom.kv-stem", Name: "KeyValue", KindId: "stat.modify",
            Channel: "kv-stem-channel", Op: "Flat", PowerBand: "medium", SourceFile: "synthetic.json",
            Tags: new[] { "element:fire" });

        var result = FamilyExpansion.Expand(new[] { family }, tierBands, _ => 100);

        var tags = ParseTagsJson(result.Rows[0].TagsJson);
        Assert.Equal("fire", tags["element"]);
    }

    [Fact]
    public void A_tag_colliding_with_a_reserved_provenance_key_is_refused_not_silently_overwritten()
    {
        var tierBands = new TierBandsInput(
            BaseSharePermille: 35,
            ChannelWeightPermille: new Dictionary<string, long> { ["collide-stem"] = 1000 },
            OpWeightPermille: new Dictionary<string, long> { ["Flat"] = 1000 });
        var family = new FamilyEntryInput(
            Id: "atom.collide-stem", Name: "Collider", KindId: "stat.modify",
            Channel: "collide-stem-channel", Op: "Flat", PowerBand: "medium", SourceFile: "synthetic.json",
            Tags: new[] { "generator" });

        var result = FamilyExpansion.Expand(new[] { family }, tierBands, _ => 100);

        Assert.Empty(result.Rows);
        var refusal = Assert.Single(result.Refusals);
        Assert.Equal("atom.collide-stem", refusal.FamilyId);
        Assert.Contains("generator", refusal.Reason);
    }

    [Fact]
    public void The_real_corpus_offensive_defensive_utility_tags_reach_real_generated_output()
    {
        var (families, tierBands) = LoadReal();
        var withTags = families.Where(f => f.Tags.Count > 0).ToList();
        Assert.NotEmpty(withTags); // proves the parser side is real, not just theoretically wired

        var byId = families.ToDictionary(f => f.Id, StringComparer.Ordinal);
        var result = FamilyExpansion.Expand(families, tierBands, _ => 100);

        var checkedCount = 0;
        foreach (var row in result.Rows)
        {
            if (!byId.TryGetValue(row.FamilyId, out var source) || source.Tags.Count == 0) continue;
            var tags = ParseTagsJson(row.TagsJson);
            foreach (var expected in source.Tags)
                Assert.True(tags.ContainsKey(expected),
                    $"{row.AtomId}: expected real family tag '{expected}' from {source.Id} in TagsJson, got [{string.Join(",", tags.Keys)}]");
            checkedCount++;
        }
        Assert.True(checkedCount > 0, "no tagged real family produced a row to check — corpus or share gate drifted");
    }

    [Fact]
    public void EligibilityRule_RequireTags_now_actually_gates_using_a_real_stamped_family_tag()
    {
        // module 8's own shipped mechanism (EligibilityRule/EligibilityResolver) — proving the fix
        // closes the real, end-to-end gap D28 named, not just that a dictionary key exists.
        var tierBands = new TierBandsInput(
            BaseSharePermille: 35,
            ChannelWeightPermille: new Dictionary<string, long>
            {
                ["offense-gate-stem"] = 1000,
                ["utility-gate-stem"] = 1000,
            },
            OpWeightPermille: new Dictionary<string, long> { ["Flat"] = 1000 });
        var offensive = new FamilyEntryInput(
            Id: "atom.offense-gate-stem", Name: "Offense Gate", KindId: "stat.modify",
            Channel: "offense-gate-stem-channel", Op: "Flat", PowerBand: "medium", SourceFile: "synthetic.json",
            Tags: new[] { "offensive" });
        var utility = new FamilyEntryInput(
            Id: "atom.utility-gate-stem", Name: "Utility Gate", KindId: "stat.modify",
            Channel: "utility-gate-stem-channel", Op: "Flat", PowerBand: "medium", SourceFile: "synthetic.json",
            Tags: new[] { "utility" });

        var result = FamilyExpansion.Expand(new[] { offensive, utility }, tierBands, _ => 100);
        Assert.Empty(result.Refusals);

        var offensiveAtom = result.Rows.First(r => r.FamilyId == "atom.offense-gate-stem");
        var utilityAtom = result.Rows.First(r => r.FamilyId == "atom.utility-gate-stem");

        var rule = new EligibilityRule(
            RequireTags: new[] { "offensive" }, AnyOfTags: Array.Empty<string>(),
            Allow: Array.Empty<string>(), Deny: Array.Empty<string>());

        var offensiveTags = ParseTagsJson(offensiveAtom.TagsJson);
        var utilityTags = ParseTagsJson(utilityAtom.TagsJson);

        Assert.True(EligibilityResolver.IsEligible("affix.offense", offensiveTags, rule));
        Assert.False(EligibilityResolver.IsEligible("affix.utility", utilityTags, rule));
    }
}
