using System.Text.Json;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Uniques;
using Xunit;

namespace FusionRpg.Core.Tests.Items;

/// <summary>
/// The cross-row half of module 17, run against the <b>real shipped corpus</b> —
/// <c>data/seed/items/uniques/*.json</c>, 144 rows across 18 partitions, the block module 11 found
/// *"referentially perfect and unobtainable"* and refused by name.
///
/// <para>Everything here is measured, not fixtured: the base types are the real 740, the rarity
/// ordinals and tier windows are the real seeded ladder, the power axes and the counter-pressure
/// vocabulary are the real `core.v1.json`, and the family → kind map is the real 98 affix families.</para>
/// </summary>
public class UniqueCorpusTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root");
    }

    static string Seed(params string[] parts) => Path.Combine(new[] { RepoRoot(), "data", "seed" }.Concat(parts).ToArray());

    static readonly IReadOnlyList<UniqueSeed> Corpus = LoadCorpus();
    static readonly IReadOnlyDictionary<string, (string RoleId, ItemFrame Frame)> BaseTypes = LoadBaseTypes();
    static readonly IReadOnlyDictionary<string, RarityRungWindow> Windows = LoadWindows();
    static readonly IReadOnlyDictionary<string, int> Ordinals = LoadOrdinals();
    static readonly IReadOnlyDictionary<string, string> FamilyKinds = LoadFamilyKinds();

    static IReadOnlyList<UniqueSeed> LoadCorpus()
    {
        var all = new List<UniqueSeed>();
        foreach (var f in Directory.GetFiles(Seed("items", "uniques"), "*.json").OrderBy(x => x, StringComparer.Ordinal))
            all.AddRange(UniqueCorpus.Parse(File.ReadAllText(f)));
        return all;
    }

    static IReadOnlyDictionary<string, (string, ItemFrame)> LoadBaseTypes()
    {
        var map = new Dictionary<string, (string, ItemFrame)>(StringComparer.Ordinal);
        foreach (var f in Directory.GetFiles(Seed("items", "base-types"), "*.json", SearchOption.AllDirectories))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            if (!doc.RootElement.TryGetProperty("entries", out var entries)) continue;
            foreach (var e in entries.EnumerateArray())
            {
                var frame = e.GetProperty("frame").GetString() == "plant" ? ItemFrame.Plant : ItemFrame.Humanoid;
                map[e.GetProperty("id").GetString()!] = (e.GetProperty("role").GetString()!, frame);
            }
        }

        return map;
    }

    static JsonElement Ladder()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Seed("rarity", "ladder.v1.json")));
        return doc.RootElement.Clone();
    }

    static IReadOnlyDictionary<string, RarityRungWindow> LoadWindows() =>
        Ladder().GetProperty("entries").EnumerateArray().ToDictionary(
            e => e.GetProperty("id").GetString()!,
            e => new RarityRungWindow(
                e.GetProperty("id").GetString()!,
                e.GetProperty("minTier").GetInt32(),
                e.GetProperty("maxTier").GetInt32(),
                e.GetProperty("prefixRolls").GetInt32() + e.GetProperty("suffixRolls").GetInt32()),
            StringComparer.Ordinal);

    static IReadOnlyDictionary<string, int> LoadOrdinals() =>
        Ladder().GetProperty("entries").EnumerateArray().ToDictionary(
            e => e.GetProperty("id").GetString()!, e => e.GetProperty("ordinal").GetInt32(), StringComparer.Ordinal);

    static IReadOnlyDictionary<string, string> LoadFamilyKinds()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var f in Directory.GetFiles(Seed("items", "affix-families"), "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            if (!doc.RootElement.TryGetProperty("entries", out var entries)) continue;
            foreach (var e in entries.EnumerateArray())
                if (e.TryGetProperty("kindId", out var k) && k.ValueKind == JsonValueKind.String)
                    map[e.GetProperty("id").GetString()!] = k.GetString()!;
        }

        return map;
    }

    static JsonElement CoreRegistry()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Seed("items", "_registry", "core.v1.json")));
        return doc.RootElement.Clone();
    }

    static UniqueCorpusView View()
    {
        var core = CoreRegistry();
        var axes = core.GetProperty("powerCategories").GetProperty("list").EnumerateArray()
            .Select(e => e.GetProperty("id").GetString()!).ToList();
        var cp = core.GetProperty("counterPressure");
        var conditions = cp.GetProperty("conditions").GetProperty("list").EnumerateArray()
            .Select(e => e.GetProperty("id").GetString()!).ToList();
        var severities = cp.GetProperty("severityBands").EnumerateArray()
            .Select(e => e.GetProperty("id").GetString()!).ToList();

        return new UniqueCorpusView(
            id => BaseTypes.TryGetValue(id, out var v) ? v : null,
            id => Ordinals.TryGetValue(id, out var o) ? o : null,
            id => Windows.TryGetValue(id, out var w) ? w : null,
            axes, conditions, severities);
    }

    static IReadOnlyDictionary<string, IReadOnlyCollection<string>> LoadFamilyFrames()
    {
        var map = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        foreach (var f in Directory.GetFiles(Seed("items", "affix-families"), "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            if (!doc.RootElement.TryGetProperty("entries", out var entries)) continue;
            foreach (var e in entries.EnumerateArray())
            {
                if (!e.TryGetProperty("frames", out var fr) || fr.ValueKind != JsonValueKind.Array) continue;
                var list = fr.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString()!).ToList();
                if (list.Count > 0) map[e.GetProperty("id").GetString()!] = list;
            }
        }

        return map;
    }

    static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> FamilyFrames = LoadFamilyFrames();

    /// <summary>The same view plus §3.5's physics arm, which the general pass deliberately leaves off —
    /// see the test that uses it for why.</summary>
    static UniqueCorpusView PhysicsView() => View() with
    {
        FamilyFrames = id => FamilyFrames.TryGetValue(id, out var v) ? v : null,
    };

    static UniqueTuning Tuning() => UniqueTests.Tuning();

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// ⭐ The corpus was AUTHORED AND NEVER WIRED. 144 rows shipped 2026-08-22; nothing in Core read
    /// one until this module. `naming.v1.json` allocates 8 per partition across 18 partitions.
    /// </summary>
    [Fact]
    public void The_shipped_corpus_is_one_hundred_and_forty_four_rows_across_eighteen_partitions()
    {
        Assert.Equal(144, Corpus.Count);
        Assert.Equal(18, Corpus.Select(s => s.Partition).Distinct().Count());
        Assert.All(Corpus.GroupBy(s => s.Partition), g => Assert.Equal(8, g.Count()));
    }

    /// <summary>
    /// D4.23 (spec-unique-pipeline.md §5 point 5, decision 13) — the todo's own literal count test:
    /// 144 anchors, 95 disabled, 49 live at rung ≥ 80, no anchor's OWN rung/rarity changed. Split by
    /// rarity name, matching the spec's own "from disk" tally exactly (grafted 20 · cultivated 20 ·
    /// fused 20 · chimeric 20 · heirloom 15 = 95 below; firstseed 9 · sunwoven 23 · almanac 17 = 49 at
    /// or above) rather than by the seed files' own rung-BAND grouping, which spans two rarities per
    /// band (`-70` files mix `heirloom`, disabled, with `firstseed`, not) — the exact split this test
    /// would silently get wrong if it filtered by filename/partition instead of by rarity ordinal.
    /// </summary>
    [Fact]
    public void The_144_anchors_split_95_disabled_49_live_by_rung_floor_no_rarity_moved()
    {
        Assert.Equal(144, Corpus.Count);

        var disabled = Corpus.Where(s => !s.Enabled).ToList();
        var live = Corpus.Where(s => s.Enabled).ToList();
        Assert.Equal(95, disabled.Count);
        Assert.Equal(49, live.Count);

        // Every disabled anchor is below the new floor; every live one meets it -- "enabled" tracks
        // the floor exactly, it is not an independent flag that happens to agree today.
        Assert.All(disabled, s => Assert.True(Ordinals[s.RarityId] < Tuning().RungFloorOrdinal));
        Assert.All(live, s => Assert.True(Ordinals[s.RarityId] >= Tuning().RungFloorOrdinal));

        // The exact rarity tally the spec cites "from disk" -- pinned by name so a future edit that
        // silently re-runs an anchor (rather than disabling it) changes one of these counts.
        var byRarity = Corpus.GroupBy(s => s.RarityId).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        Assert.Equal(20, byRarity["grafted"]);
        Assert.Equal(20, byRarity["cultivated"]);
        Assert.Equal(20, byRarity["fused"]);
        Assert.Equal(20, byRarity["chimeric"]);
        Assert.Equal(15, byRarity["heirloom"]);
        Assert.Equal(9, byRarity["firstseed"]);
        Assert.Equal(23, byRarity["sunwoven"]);
        Assert.Equal(17, byRarity["almanac"]);

        // "no anchor's rung changed": every disabled seed keeps the SAME rarity id it always had --
        // this is a structural guarantee here (nothing in the disable pass touches "rarity"), not a
        // snapshot comparison, but the rarity-tally assertions above are the same claim from the data
        // side: the 95/49 counts by name match the pre-D4.23 rarity distribution exactly.
        Assert.All(Corpus, s => Assert.False(string.IsNullOrEmpty(s.RarityId)));
    }

    /// <summary>
    /// "The 64 `unique` table rows are re-pointed" (todo's own acceptance line) — the map's own drift
    /// note (spec-unique-pipeline.md §9) corrects this: 64 is the source-locked COUNT, and "the lock
    /// lives in which table references the id" (ideal :1521-1524) — a source-locked unique's binding
    /// is not a field on the anchor at all, it is which DOMAIN table names its id. No domain table
    /// exists yet (D4.16's own import arm is unbuilt, `domain-catalog`), so there is nothing to
    /// re-point today: this pins the COUNT the eventual re-pointing acts on, honestly, rather than
    /// asserting a re-pointing this task cannot perform.
    /// </summary>
    [Fact]
    public void Sixty_four_anchors_are_source_locked_the_count_a_future_domain_binding_repoints()
    {
        Assert.Equal(64, Corpus.Count(s => s.Acquisition == UniqueAcquisition.SourceLocked));
        Assert.Equal(40, Corpus.Count(s => s.Acquisition == UniqueAcquisition.Deterministic));
        Assert.Equal(40, Corpus.Count(s => s.Acquisition == UniqueAcquisition.Drop));
    }

    /// <summary>
    /// `naming.v1.json`'s own `idVsContainerIdNote` left this derivation open "for wave-1b". Every
    /// derived id is a legal container id under the shipped grammar, and they are all distinct.
    /// </summary>
    [Fact]
    public void Every_seed_id_derives_a_legal_and_distinct_item_container_id()
    {
        Assert.All(Corpus, s => Assert.StartsWith("item.", s.ContainerId, StringComparison.Ordinal));
        Assert.Equal(144, Corpus.Select(s => s.ContainerId).Distinct(StringComparer.Ordinal).Count());

        // The shipped container-id grammar, borrowed from the validator rather than restated.
        var atom = new AtomRow { AtomId = "atom.vitality.t1", KindId = "stat.modify", FamilyId = "atom.vitality", Tier = 1 };
        foreach (var s in Corpus)
        {
            var check = ContainerValidator.Validate(
                new ContainerRow { ContainerId = s.ContainerId, Kind = ContainerKind.Item },
                _ => atom, _ => null);
            Assert.True(check.IsOk, $"{s.ContainerId}: {check.Detail}");
        }
    }

    /// <summary>
    /// ⭐ The four cross-row checks over the WHOLE corpus, plus every per-row rule readable from a
    /// seed. Zero findings: the Latin-square allocation holds at 144, no role is doubled past the
    /// quota, no <c>jewel-minor</c> carries one, nothing sits below the rung floor and nothing at
    /// ordinal ≥ 90 is a plain drop.
    /// </summary>
    [Fact]
    public void The_whole_corpus_passes_every_cross_row_check()
    {
        var findings = UniqueCorpusValidator.Validate(Corpus, View(), Tuning());

        Assert.True(findings.Count == 0,
            "unexpected findings:\n" + string.Join("\n", findings.Take(20).Select(f => f.Rejection.Detail)));
    }

    /// <summary>
    /// §4.6's banner: at 144 the axis grid is <b>exactly saturated</b> — 8 roles × 18 partitions — which
    /// is why the allocation had to become a Latin square rather than a guideline. Measured: every
    /// <c>(rung band, role, power axis)</c> is used at most once, and the two saturated bands use every
    /// one of their 40 slots.
    /// </summary>
    [Fact]
    public void The_144_corpus_saturates_the_axis_grid_without_a_collision()
    {
        var keys = Corpus
            .Select(s => (Band: s.RungBand, Role: BaseTypes[s.BaseTypeId].RoleId, s.PowerAxis))
            .ToList();

        Assert.Equal(144, keys.Distinct().Count());

        // 8 roles x 5 axes = 40 slots per band; bands 30, 50 and 90 carry 5 partitions each = 40 rows.
        foreach (var band in new[] { "30", "50", "90" })
            Assert.Equal(40, keys.Count(k => k.Band == band));

        Assert.Equal(8, keys.Select(k => k.Role).Distinct().Count());
    }

    /// <summary>
    /// §3.7 device 4's other two arms, as counts rather than as a validator pass — so the numbers are
    /// visible in the report rather than only "no findings".
    /// </summary>
    [Fact]
    public void At_most_eight_of_fifteen_roles_per_frame_carry_a_unique_and_none_is_jewel_minor()
    {
        var byFrame = Corpus
            .GroupBy(s => BaseTypes[s.BaseTypeId].Frame)
            .ToDictionary(g => g.Key, g => g.Select(s => BaseTypes[s.BaseTypeId].RoleId).Distinct().ToList());

        Assert.Equal(2, byFrame.Count);
        foreach (var (_, roles) in byFrame)
        {
            Assert.Equal(8, roles.Count);
            Assert.DoesNotContain(roles, r => r.StartsWith("jewel-minor", StringComparison.Ordinal));
        }

        // ItemRole is closed at fifteen plus the commander-only sixteenth, so "8 of 15" is a real
        // fraction and not a number floating free of a registry.
        Assert.Equal(16, Enum.GetNames<ItemRole>().Length);
    }

    /// <summary>
    /// §4.5 rule 1, over the real corpus: no unique at ordinal ≥ 90 is a plain drop. Measured rather
    /// than asserted from the partition name, because a band-90 partition may still author a
    /// <c>sunwoven</c> or an <c>almanac</c> row and the rule keys on the ROW's rung.
    /// </summary>
    [Fact]
    public void No_shipped_unique_at_ordinal_ninety_or_above_is_a_plain_drop()
    {
        var high = Corpus.Where(s => Ordinals[s.RarityId] >= 90).ToList();
        Assert.NotEmpty(high);
        Assert.DoesNotContain(high, s => s.Acquisition == UniqueAcquisition.Drop);
        // D4.23: the floor now holds for every CURRENTLY OFFERED anchor, not every anchor in the
        // corpus outright -- 95 of the 144 sit below the raised floor on purpose (enabled: false,
        // "never re-runged, never deleted", spec-unique-pipeline.md §5 point 5) and are exempted from
        // this exact check by UniqueCorpusValidator's own matching skip, not just by this assertion.
        Assert.All(Corpus.Where(s => s.Enabled), s => Assert.True(Ordinals[s.RarityId] >= Tuning().RungFloorOrdinal));
    }

    /// <summary>
    /// Every declared counter-pressure resolves against `core.v1.json`'s own closed vocabulary — the
    /// registry that exists *because* this lane needed all three kinds authorable without a number.
    /// </summary>
    [Fact]
    public void Every_counter_pressure_declaration_resolves_against_the_core_registry()
    {
        var view = View();
        foreach (var s in Corpus)
        {
            switch (s.CounterPressure.Kind)
            {
                case UniqueCounterPressure.Drawback:
                    Assert.Contains(s.CounterPressure.SeverityBand, view.SeverityBands);
                    Assert.True(s.CounterPressure.Family is not null || s.CounterPressure.Channel is not null,
                        s.SeedId);
                    break;
                case UniqueCounterPressure.Conditional:
                    Assert.Contains(s.CounterPressure.Condition, view.CounterPressureConditions);
                    break;
            }
        }

        // All three kinds are actually exercised by the corpus -- a vocabulary with an unused arm is
        // an arm nobody has proved works.
        Assert.Equal(3, Corpus.Select(s => s.CounterPressure.Kind).Distinct().Count());
    }

    /// <summary>
    /// §3.6: at most one variance slot, and 1–3 identity atoms. Measured over the real corpus rather
    /// than only refused in the validator, so the shape's actual distribution is on the record.
    /// </summary>
    [Fact]
    public void Every_shipped_unique_holds_at_most_one_roll_and_at_most_three_identity_atoms()
    {
        Assert.All(Corpus, s => Assert.True(s.TotalRolls <= UniqueLimits.MaxTotalRolls));
        Assert.All(Corpus, s => Assert.InRange(s.FixedAtoms.Count, 1, Tuning().MaxIdentityAtoms));

        Assert.Equal(136, Corpus.Count(s => s.VarianceSlot is not null));
        Assert.Equal(8, Corpus.Count(s => s.VarianceSlot is null));
    }

    // ---------------------------------------------------------------------------------------------
    // Device 3, measured for the first time
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// ⭐ ssot-uniques.md §10.3 put *"the parity invariant is unmeasured"* to the owner as an open
    /// question, and spec-uniques.md shipped it as *"stated, never measured"* pending module 7's
    /// harness. The harness exists, so this is <b>the first measurement</b> — and it does not come
    /// out green.
    ///
    /// <para>The reading is deterministic (module 7's seed, module 7's roll count, module 7's paired
    /// comparison) and it is pinned here as a corpus regression, not as a pass/fail gate: parity is
    /// device 3 and device 3 was never one of the three HARD validators.</para>
    /// </summary>
    [Fact]
    public void Parity_is_measured_over_the_real_corpus_and_reports_a_real_distribution()
    {
        var t = Tuning();
        var report = UniqueParityMetric.Measure(Corpus, id => Windows.TryGetValue(id, out var w) ? w : null, t);

        Assert.True(report.HasThreshold);
        Assert.Equal(287, report.Readings.Count);   // one per (unique, identity atom)
        Assert.Equal(report.Readings.Count, report.InBand + report.StrictlyBetter + report.Trophy);

        // ⛔ The measured shape of the shipped corpus: most identity lines are BELOW the rolled
        // distribution at their own rung, which is §8.4's trophy failure, and a minority are above it,
        // which is §8.1's. Pinned so a re-authoring pass can see it move.
        Assert.Equal(90, report.InBand);
        Assert.Equal(47, report.StrictlyBetter);
        Assert.Equal(150, report.Trophy);
    }

    /// <summary>The measurement reproduces exactly — same seed, same rolls, same numbers.</summary>
    [Fact]
    public void The_parity_measurement_is_reproducible()
    {
        Func<string, RarityRungWindow?> w = id => Windows.TryGetValue(id, out var x) ? x : null;
        var a = UniqueParityMetric.Measure(Corpus, w, Tuning());
        var b = UniqueParityMetric.Measure(Corpus, w, Tuning());

        Assert.Equal(a.Readings.Select(r => r.WPerMille), b.Readings.Select(r => r.WPerMille));
    }

    // ---------------------------------------------------------------------------------------------
    // Device 2, measured and reported (never refused on a seed)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// ⛔ The budget device, priced over the real corpus. Reported, not refused — the seed authors no
    /// <c>budget_ae</c> at all, so refusing here would refuse content against a price the authors were
    /// never given a way to see. The over-allowance count is an UPPER bound: the baseline reads the
    /// seeded count-band floor, which is smaller than the published half-range.
    /// </summary>
    [Fact]
    public void The_budget_device_is_measured_over_the_real_corpus_and_reported()
    {
        var report = UniqueCorpusReporter.Measure(
            Corpus,
            id => Windows.TryGetValue(id, out var w) ? w : null,
            id => FamilyKinds.TryGetValue(id, out var k) ? k : null,
            Tuning());

        Assert.Equal(144, report.Readings.Count);
        Assert.Contains("upper bound", report.Basis, StringComparison.Ordinal);

        // Measured 2026-09-05 against the shipped corpus. Pinned so a re-authoring pass sees it move.
        Assert.Equal(36, report.OverAllowance);
        Assert.Equal(12, report.NarrowDeclaredAndUnsatisfied);
    }

    /// <summary>
    /// §9.1 asked module 7 to publish *"the rolled baseline in AE per rung, which §3.7's budget check
    /// divides by and which does not exist in any document yet."* It exists now, derived from the
    /// seeded ladder rather than authored: it is monotone up the ladder and <c>chaff</c>, the one rung
    /// with no pool, is zero.
    /// </summary>
    [Fact]
    public void The_rung_baseline_in_ae_is_published_and_derived_from_the_seeded_ladder()
    {
        var ordered = RarityLadder.RungIds.Select(id => Windows[id]).ToList();
        var baselines = ordered.Select(UniqueBudget.RungBaselineAeHundredths).ToList();

        Assert.Equal(0, baselines[0]);                       // chaff: no pool, no baseline
        Assert.Equal(500, baselines[^1]);                    // almanac: 3 + 2 affixes
        for (var i = 1; i < baselines.Count; i++)
            Assert.True(baselines[i] >= baselines[i - 1], $"{ordered[i].RarityId} regressed");
    }

    /// <summary>
    /// ⛔ <b>Two real, shipped corpus defects, found by this module's new physics check.</b>
    /// ssot-uniques.md §3.5 draws a line inside the frame filter that no registry encoded: a unique may
    /// bypass it where the filter is <i>taste</i>, and may not where the filter is <i>physics</i> — a
    /// Unity channel that only exists on the other side. The lane's own example is
    /// <c>plating</c>/<c>carapace</c> on a plant; <b>no shipped unique carries either</b>, and two carry
    /// different members of the same class instead:
    ///
    /// <list type="bullet">
    /// <item><c>unique.sunwoven-almanac-90-006</c> ("Hypocotyl of the Precept") is a <b>plant</b> unique
    /// carrying <c>atom.swiftness</c> — <c>stat.modify</c> on <c>zombieSpeed</c>, whose family row reads
    /// <c>frames: ["humanoid"], side: "zombie"</c>.</item>
    /// <item><c>unique.umbral-swarm-50-004</c> ("Encroaching Leash") is a <b>humanoid</b> unique carrying
    /// <c>atom.quickening</c> — <c>stat.modify</c> on <c>attackInterval</c>, whose family row reads
    /// <c>frames: ["plant"], side: "plant"</c> — and <c>atom.flourishing</c> in its variance slot, also
    /// plant-only.</item>
    /// <item><c>unique.umbral-swarm-50-005</c> is a <b>humanoid</b> unique whose variance slot draws
    /// <c>atom.quickening</c>.</item>
    /// </list>
    ///
    /// <para>Four findings across three uniques: the rule covers the variance slot as well as the fixed
    /// core, because a pool that can only ever draw a dead line is the same defect one step later.</para>
    ///
    /// <para>Not hand-fixed — the corpus is generated and the validator's own footer says to re-run the
    /// partition — but reported by name (`ItemSeedValidator`'s new <c>UniqueFrameCheck</c>) and pinned
    /// here so the set cannot grow silently. Kept out of the general cross-row pass above so that
    /// "the corpus passes every check" stays a true sentence about the checks that predate this one.</para>
    /// </summary>
    [Fact]
    public void Three_shipped_uniques_carry_a_family_their_own_frame_cannot_execute()
    {
        var findings = UniqueCorpusValidator.Validate(Corpus, PhysicsView(), Tuning());

        Assert.Equal(4, findings.Count);
        Assert.Equal(
            new[] { "unique.sunwoven-almanac-90-006", "unique.umbral-swarm-50-004", "unique.umbral-swarm-50-005" },
            findings.Select(f => f.SeedId).Distinct().OrderBy(x => x, StringComparer.Ordinal));
        Assert.All(findings, f => Assert.Contains(UniqueRules.Shape, f.Rejection.Detail, StringComparison.Ordinal));

        // The lane's own named example is clean — which is why the check reads the family corpus's
        // `frames` list rather than a hardcoded pair of family ids.
        Assert.DoesNotContain(Corpus.SelectMany(s => s.FixedAtoms),
            a => a.Family is "atom.plating" or "atom.carapace");
    }

    // ---------------------------------------------------------------------------------------------
    // The band -> channel rule module 11 recorded as this module's
    // ---------------------------------------------------------------------------------------------

    static IReadOnlyList<UniqueDropReference> DropReferences()
    {
        var refs = new List<UniqueDropReference>();
        foreach (var f in Directory.GetFiles(Seed("items", "drop-tables"), "*.json", SearchOption.AllDirectories))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            if (!doc.RootElement.TryGetProperty("entries", out var tables)) continue;
            foreach (var table in tables.EnumerateArray())
            {
                var tableId = table.GetProperty("id").GetString()!;
                if (!table.TryGetProperty("groups", out var groups)) continue;
                foreach (var g in groups.EnumerateArray())
                {
                    if (!g.TryGetProperty("entries", out var rows)) continue;
                    foreach (var row in rows.EnumerateArray())
                    {
                        if (row.TryGetProperty("entryKind", out var k) && k.GetString() == "unique")
                            refs.Add(new UniqueDropReference(
                                tableId, row.GetProperty("ref").GetString()!,
                                // d1 is the general table. ⚠ Read from the id because the shipped
                                // drop-table schema carries no channel marker -- the gap the validator's
                                // own parameter names.
                                tableId.StartsWith("droptable.d1-", StringComparison.Ordinal)));
                    }
                }
            }
        }

        return refs;
    }

    /// <summary>
    /// ⭐ The rule module 11's own addendum handed to this module: <i>"acquisition = 'drop' at ordinal
    /// ≥ 90 is UniqueUnreachable, so band 90 never appears in d1 — module 11 does not enforce that
    /// rule, and this module owns it."</i> Enforced, and measured green against the real drop corpus.
    /// </summary>
    [Fact]
    public void Every_unique_drop_reference_sits_in_the_channel_its_acquisition_declares()
    {
        var refs = DropReferences();
        Assert.Equal(144, refs.Count);
        Assert.Equal(144, refs.Select(r => r.UniqueSeedId).Distinct(StringComparer.Ordinal).Count());

        var findings = UniqueCorpusValidator.ValidateDropReferences(
            refs, Corpus, id => Ordinals.TryGetValue(id, out var o) ? o : null);

        Assert.True(findings.Count == 0,
            "unexpected findings:\n" + string.Join("\n", findings.Take(10).Select(f => f.Rejection.Detail)));

        // The shipped corpus partitions the three channels EXACTLY, which is what makes the rule
        // checkable rather than aspirational.
        var byTable = refs.GroupBy(r => r.TableId)
            .ToDictionary(g => g.Key, g => g.Select(r => Corpus.Single(s => s.SeedId == r.UniqueSeedId).Acquisition)
                                            .Distinct().ToList());
        Assert.Equal(new[] { UniqueAcquisition.Drop }, byTable["droptable.d1-001"]);
        Assert.Equal(new[] { UniqueAcquisition.SourceLocked }, byTable["droptable.d2-001"]);
        Assert.Equal(new[] { UniqueAcquisition.Deterministic }, byTable["droptable.d4-001"]);
    }

    /// <summary>And the rule has teeth: a band-90 deterministic unique planted in d1 is refused.</summary>
    [Fact]
    public void A_source_locked_or_band_ninety_unique_in_the_general_table_is_refused()
    {
        var high = Corpus.First(s => Ordinals[s.RarityId] >= 90);
        var findings = UniqueCorpusValidator.ValidateDropReferences(
            new[] { new UniqueDropReference("droptable.d1-001", high.SeedId, IsGeneralChannel: true) },
            Corpus, id => Ordinals.TryGetValue(id, out var o) ? o : null);

        // Both arms fire: the acquisition mismatch AND entry-shapes.md §9's band rule.
        Assert.Equal(2, findings.Count);
        Assert.All(findings, f => Assert.Contains(UniqueRules.Unreachable, f.Rejection.Detail, StringComparison.Ordinal));

        // The same reference in a non-general channel is fine -- that is where it belongs.
        Assert.Empty(UniqueCorpusValidator.ValidateDropReferences(
            new[] { new UniqueDropReference("droptable.d4-001", high.SeedId, IsGeneralChannel: false) },
            Corpus, id => Ordinals.TryGetValue(id, out var o) ? o : null));
    }

    /// <summary>
    /// ⏸ The <c>unique</c> drop ENTRY KIND is still refused, and for a reason that moved: module 17
    /// exists now, so the blocker is one step further on — no concrete unique container exists to hand
    /// a player. Pinned so the pointer cannot go stale a second time.
    /// </summary>
    [Fact]
    public void The_unique_entry_kind_is_still_unavailable_and_names_the_real_remaining_blocker()
    {
        Assert.False(FusionRpg.Core.Items.Drops.DropTableDraw.IsAvailable(
            FusionRpg.Core.Items.Drops.DropEntryKind.Unique));

        var reason = FusionRpg.Core.Items.Drops.DropTableDraw.UnavailableKinds[
            FusionRpg.Core.Items.Drops.DropEntryKind.Unique];
        Assert.Contains("seed-to-concrete", reason, StringComparison.Ordinal);
        Assert.Contains("CONCRETE unique container", reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// ✅ <b>Closed 2026-09-06.</b> This used to assert that five affix families named by the shipped
    /// unique corpus resolved to no affix-family row at all, so their kind was unknown and they were
    /// dropped from `narrow`'s raw-stat subtotal rather than guessed into it — module 10's
    /// phantom-family defect reaching this corpus. All five (and the four others in that set) are now
    /// authored, so the assertion is inverted: EVERY family this corpus names resolves.
    ///
    /// <para>The old test walked <c>fixedAtoms</c> only, which is how it missed
    /// <c>atom.affliction</c> — the unique corpus names it from a <c>varianceSlot</c>, a different
    /// field. Both surfaces are walked now, plus <c>counterPressure.family</c>, so nothing a unique
    /// can point a family at is left unchecked.</para>
    /// </summary>
    [Fact]
    public void Every_affix_family_the_unique_corpus_names_resolves_to_a_real_family()
    {
        var named = Corpus
            .SelectMany(FamiliesNamedBy)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        var unresolved = named.Where(f => !FamilyKinds.ContainsKey(f)).ToList();
        Assert.Empty(unresolved);

        // The five this test used to pin as unresolvable, named so a regression fails with their
        // names in front of the reader rather than as an empty-set diff.
        foreach (var wasPhantom in new[]
                 { "atom.bonding", "atom.buttering", "atom.chilling", "atom.marking", "atom.rotting" })
            Assert.Contains(wasPhantom, named, StringComparer.Ordinal);

        // atom.affliction reaches this corpus through `varianceSlot`, never `fixedAtoms` — the exact
        // shape the old walk could not see.
        Assert.Contains("atom.affliction", named, StringComparer.Ordinal);
        Assert.Equal("stat.derived", FamilyKinds["atom.affliction"]);
    }

    /// <summary>Every field on a unique seed that can name an affix family — `fixedAtoms`, the
    /// `varianceSlot`, and a drawback-shaped `counterPressure`.</summary>
    static IEnumerable<string> FamiliesNamedBy(UniqueSeed seed)
    {
        foreach (var atom in seed.FixedAtoms) yield return atom.Family;
        if (seed.VarianceSlot is { Family.Length: > 0 } variance) yield return variance.Family;
        if (seed.CounterPressure.Family is { Length: > 0 } drawback) yield return drawback;
    }
}
