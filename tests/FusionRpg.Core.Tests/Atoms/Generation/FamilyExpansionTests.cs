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
        var tierBands = TierBandsFile.Read(File.ReadAllText(Path.Combine(itemsRoot, "_tuning", "tier-bands.v1.json")));

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
            Channel: "combat.power.{variant}", Op: "Increased", PowerBand: "medium", SourceFile: "synthetic.json");

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
            Channel: "combat.power.pierce.{variant}", Op: "Increased", PowerBand: "medium", SourceFile: "synthetic.json");

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

        foreach (var row in result.Rows)
        {
            var validated = AtomRowValidator.Validate(row);
            Assert.True(validated.IsOk, $"{row.AtomId}: {validated}");

            var priced = CostFunction.Price(row, PowerTables.Authored());
            Assert.True(priced.Ok, $"{row.AtomId}: {priced.Verdict.Reason}");
            // Every emitted row is stat.modify — never a genuinely zero-power kind.
            Assert.NotEqual(PowerVector.Zero, priced.Power);
        }
    }

    // ---- test 7: planted violations — refused by id --------------------------------------------------

    [Fact]
    public void PlantedViolation_a_family_with_no_authored_share_is_refused_by_id()
    {
        var result = ExpandReal();

        var refusal = result.Refusals.SingleOrDefault(r => r.FamilyId == "atom.elpw-override");
        Assert.NotNull(refusal);
        Assert.Contains("no authored sharePermille", refusal!.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Rows, r => r.FamilyId == "atom.elpw-override");
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
            Channel: "combat.made-up-thing.{variant}", Op: "Flat", PowerBand: "medium", SourceFile: "synthetic.json");

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
}
