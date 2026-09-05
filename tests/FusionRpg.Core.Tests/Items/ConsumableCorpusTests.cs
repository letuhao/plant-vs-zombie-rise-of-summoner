using System.Text.Json;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items.Consumables;
using FusionRpg.Core.Items.Drops;
using Xunit;

namespace FusionRpg.Core.Tests.Items;

/// <summary>
/// Module 18 against the <b>real shipped corpus</b> — <c>data/seed/items/consumables/*.json</c>,
/// 60 rows across three partitions, authored 2026-08-22 and read by nothing until this module.
///
/// <para>Nothing here is fixtured: the grade map is checked against the real frozen
/// <c>bands.v1.json</c>, the families against the real 98 affix families, the kinds against the real
/// registry, and the drop references against the real 40-table drop-table corpus module 11 refused
/// them from.</para>
/// </summary>
public class ConsumableCorpusTests
{
    static string Seed(params string[] parts) =>
        Path.Combine(new[] { ConsumableTests.RepoRoot(), "data", "seed" }.Concat(parts).ToArray());

    static readonly IReadOnlyList<ConsumableSeed> Corpus = LoadCorpus();
    static readonly IReadOnlyDictionary<string, string> FamilyKinds = LoadFamilyKinds();
    static readonly ConsumableTuning Tuning = ConsumableTests.Tuning();

    static IReadOnlyList<ConsumableSeed> LoadCorpus()
    {
        var all = new List<ConsumableSeed>();
        foreach (var f in Directory.GetFiles(Seed("items", "consumables"), "*.json")
                     .OrderBy(x => x, StringComparer.Ordinal))
            all.AddRange(ConsumableCorpus.Parse(File.ReadAllText(f)));
        return all;
    }

    static IReadOnlyDictionary<string, string> LoadFamilyKinds()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var f in Directory.GetFiles(Seed("items", "affix-families"), "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            if (!doc.RootElement.TryGetProperty("entries", out var entries)) continue;
            foreach (var e in entries.EnumerateArray())
                if (e.TryGetProperty("id", out var id) && e.TryGetProperty("kindId", out var kind))
                    map[id.GetString()!] = kind.GetString()!;
        }

        return map;
    }

    static ConsumableCorpusReport Report() =>
        ConsumableCorpusValidator.Validate(
            Corpus, Tuning, family => FamilyKinds.TryGetValue(family, out var k) ? k : null);

    // ---- the corpus itself ----------------------------------------------------------------------------

    [Fact]
    public void The_corpus_is_sixty_rows_across_three_partitions_measured_not_assumed()
    {
        Assert.Equal(60, Corpus.Count);
        Assert.Equal(60, Corpus.Select(c => c.ContainerId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            new[] { "consumables/1", "consumables/2", "consumables/3" },
            Corpus.Select(c => c.Partition).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToArray());
        Assert.All(Corpus.GroupBy(c => c.Partition), g => Assert.Equal(20, g.Count()));
    }

    [Fact]
    public void Every_seed_id_is_ALREADY_a_legal_container_id_so_no_derivation_is_needed()
    {
        // ⭐ Unlike a unique's `unique.` tracking id — which is not a legal container id at all and
        // needed UniqueContainerIds to derive one — `naming.v1.json`'s consumable template and §4.6's
        // container prefix coincide. Nothing here invents a second id for one row.
        Assert.All(Corpus, c => Assert.True(ConsumableContainerIds.IsWellFormed(c.ContainerId)));
        Assert.All(Corpus, c => Assert.StartsWith("consumable.", c.ContainerId, StringComparison.Ordinal));
    }

    [Fact]
    public void The_corpus_authors_only_the_three_v1_classes_and_the_two_v1_contexts()
    {
        Assert.Equal(
            new[] { ConsumableClass.Restore, ConsumableClass.Draught, ConsumableClass.Ward },
            Corpus.Select(c => c.ClassId).Distinct().OrderBy(c => (int)c).ToArray());

        Assert.All(Corpus, c => Assert.All(c.UseContexts, u => Assert.True(Tuning.Authors(u))));
        Assert.Equal(
            new[] { UseContext.Menu, UseContext.Dispatch },
            Corpus.SelectMany(c => c.UseContexts).Distinct().OrderBy(u => (int)u).ToArray());

        // measured, so a corpus change cannot quietly move them
        Assert.Equal(16, Corpus.Count(c => c.ClassId == ConsumableClass.Restore));
        Assert.Equal(29, Corpus.Count(c => c.ClassId == ConsumableClass.Draught));
        Assert.Equal(15, Corpus.Count(c => c.ClassId == ConsumableClass.Ward));
        Assert.Equal(26, Corpus.Count(c => c.UseContexts.Contains(UseContext.Menu)));
        Assert.Equal(34, Corpus.Count(c => c.UseContexts.Contains(UseContext.Dispatch)));
    }

    [Fact]
    public void No_shipped_row_authors_grantsActionId_or_cooldownKey_so_the_seam_is_inert_as_v1_requires()
    {
        Assert.All(Corpus, c => Assert.Null(c.GrantsActionId));
        Assert.All(Corpus, c => Assert.Null(c.CooldownKey));
    }

    // ---- the grade map, against the frozen registry ---------------------------------------------------

    [Fact]
    public void The_grade_tier_map_mirrors_the_frozen_registry_value_for_value()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Seed("items", "_registry", "bands.v1.json")));
        var root = doc.RootElement;
        Assert.True(root.GetProperty("frozen").GetBoolean());

        var registry = root.GetProperty("powerBand").GetProperty("tierMap");
        var mirrored = Tuning.GradeTierMap;

        Assert.Equal(registry.EnumerateObject().Count(), mirrored.Count);
        foreach (var p in registry.EnumerateObject())
        {
            Assert.True(mirrored.TryGetValue(p.Name, out var grade),
                $"bands.v1.json powerBand '{p.Name}' is missing from consumables.v1.json's gradeTierMap");
            Assert.Equal(p.Value.GetInt32(), grade);
        }
    }

    [Fact]
    public void Every_row_resolves_to_a_grade_and_the_histogram_is_measured()
    {
        var report = Report();
        Assert.Equal(60, report.GradeHistogram.Values.Sum());
        Assert.All(report.GradeHistogram.Keys, g => Assert.InRange(g, 1, 5));

        // trivial 3 / low 17 / medium 31 / high 9 / extreme 0 — pinned so a re-author is visible
        Assert.Equal(3, report.GradeHistogram.GetValueOrDefault(1));
        Assert.Equal(17, report.GradeHistogram.GetValueOrDefault(2));
        Assert.Equal(31, report.GradeHistogram.GetValueOrDefault(3));
        Assert.Equal(9, report.GradeHistogram.GetValueOrDefault(4));
        Assert.Equal(0, report.GradeHistogram.GetValueOrDefault(5));
    }

    [Fact]
    public void The_grade_is_DERIVED_from_the_seeds_powerBand_and_never_authored_beside_it()
    {
        // The seed contract forbids an author typing a magnitude; a grade authored next to a band would
        // be a second source of truth for the same fact. Neither the parsed seed nor the shipped JSON
        // carries a grade key.
        Assert.DoesNotContain("grade", typeof(ConsumableSeed).GetProperties().Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var f in Directory.GetFiles(Seed("items", "consumables"), "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            foreach (var e in doc.RootElement.GetProperty("entries").EnumerateArray())
                Assert.False(e.TryGetProperty("grade", out _));
        }

        foreach (var seed in Corpus)
        {
            Assert.True(ConsumableCorpusValidator.TryToDefRow(seed, Tuning, out var def));
            Assert.Equal(Tuning.GradeTierMap[seed.PowerBand], def.Grade);
        }
    }

    // ---- exclusion groups ------------------------------------------------------------------------------

    [Fact]
    public void The_exclusion_group_is_derived_as_family_pipe_variant_exactly_as_the_worked_examples_spell_it()
    {
        // §7.1 spells it `atom.vitality|`; §7.2 spells it `atom.elemental-power|fire`.
        var vital = Corpus.First(c => c.ContainerId == "consumable.k1-001");
        Assert.Equal("atom.vitality|", vital.ExclusionGroup);

        var elemental = Corpus.FirstOrDefault(c => c.Element == "fire" && c.Family == "atom.elemental-power");
        Assert.NotNull(elemental);
        Assert.Equal("atom.elemental-power|fire", elemental!.ExclusionGroup);
    }

    [Fact]
    public void The_corpus_offers_breadth_within_a_group_which_is_what_the_one_per_group_rule_forces()
    {
        var report = Report();
        // 17 groups hold more than one row — several grades of one family, of which a run may take
        // exactly one. That is the rule working, not a collision.
        Assert.Equal(17, report.ExclusionGroups.Count(g => g.Value > 1));
        Assert.Equal(60, report.ExclusionGroups.Values.Sum());
        Assert.All(report.ExclusionGroups.Keys, k => Assert.Contains('|', k));
    }

    // ---- the corpus validates ---------------------------------------------------------------------------

    [Fact]
    public void The_shipped_corpus_produces_EXACTLY_ONE_refusal_and_it_is_a_real_defect_not_a_fixture()
    {
        // ⛔ `consumable.k2-015` ("Purifying Tonic") authors family `atom.cleansing` → kind
        // `status.clear`, whose Battle support is None, and names `useContext: dispatch`. That is
        // ssot-consumables.md's own FAILURE MODE 5 — "a consumable quietly does nothing because its
        // atom is dead in the runtime it was used in" — live in the shipped corpus, caught by the check
        // §6.3 exists for. 59 of the 60 rows are clean.
        var report = Report();
        var fail = Assert.Single(report.Rejections);
        Assert.StartsWith(ConsumableRules.RuntimeUnsupported, fail.Detail, StringComparison.Ordinal);
        Assert.Contains("consumable.k2-015", fail.Detail, StringComparison.Ordinal);
        Assert.Contains("status.clear", fail.Detail, StringComparison.Ordinal);
        Assert.Equal(AtomRejectionReason.ContentRuleViolated, fail.Reason);
    }

    [Fact]
    public void Every_resolvable_family_maps_to_a_kind_legal_in_its_contexts_runtimes_with_one_named_exception()
    {
        // The invisible-nerf guard over the REAL corpus rather than a planted row.
        var checkedPairs = 0;
        var dead = new List<string>();

        foreach (var seed in Corpus)
        {
            if (!FamilyKinds.TryGetValue(seed.Family, out var kindId)) continue;
            var kind = AtomKindRegistry.Get(kindId);
            Assert.NotNull(kind);
            foreach (var ctx in seed.UseContexts)
                foreach (var runtime in UseContexts.RuntimesFor(ctx))
                {
                    checkedPairs++;
                    if (kind!.SupportIn(runtime) == RuntimeState.None) dead.Add(seed.ContainerId);
                }
        }

        Assert.True(checkedPairs > 0, "the runtime check asserted nothing, which is not a pass");
        // pinned as a set, so the defect cannot grow silently and cannot quietly be "fixed" by
        // widening the check
        Assert.Equal(new[] { "consumable.k2-015" }, dead.Distinct().ToArray());
    }

    [Fact]
    public void Every_kind_the_corpus_reaches_can_express_a_lifetime_except_the_same_one_defective_row()
    {
        // §4.2's real requirement, stated precisely rather than as "must carry OnActivate": a
        // consumable's core atom needs EITHER a fire point (a kind carrying OnActivate — the instant
        // case, §7.1) OR no trigger at all (a permanent modifier bound for the run and withdrawn at
        // its end — the draught case, §7.2's `stat.derived`, definitions §14.2). Those are the two
        // shapes v1 has, and a kind that is neither can express no lifetime this module owns.
        //
        // ⛔ `status.clear` is neither: it stays on the narrow board-event set (H3). So the SAME row
        // the runtime check refused fails here too, from a second direction — which is what makes it a
        // defect rather than a threshold.
        var offenders = Corpus
            .Where(c => FamilyKinds.ContainsKey(c.Family))
            .Where(c =>
            {
                var k = AtomKindRegistry.Get(FamilyKinds[c.Family])!;
                return !k.AllowsTrigger(AtomTriggers.OnActivate) && k.Triggers.Count > 0;
            })
            .Select(c => c.ContainerId)
            .Distinct()
            .ToArray();

        Assert.Equal(new[] { "consumable.k2-015" }, offenders);

        // the three kinds the other 48 resolvable rows reach, and which shape each one is
        var goodKinds = Corpus
            .Where(c => FamilyKinds.ContainsKey(c.Family) && c.ContainerId != "consumable.k2-015")
            .Select(c => FamilyKinds[c.Family])
            .Distinct()
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "resource.delta", "stat.derived", "stat.modify" }, goodKinds);
        Assert.True(AtomKindRegistry.Get("resource.delta")!.AllowsTrigger(AtomTriggers.OnActivate));
        Assert.True(AtomKindRegistry.Get("stat.modify")!.AllowsTrigger(AtomTriggers.OnActivate));
        Assert.Empty(AtomKindRegistry.Get("stat.derived")!.Triggers);   // the permanent-modifier shape
    }

    [Fact]
    public void All_sixteen_restore_rows_reach_a_kind_that_carries_a_fire_point()
    {
        // The instant class is the one that genuinely needs OnActivate — §4.2's "hardest finding", and
        // the whole reason the eighth trigger was asked for. Every one of the 16 lands on `stat.modify`,
        // which carries it.
        var restores = Corpus.Where(c => c.ClassId == ConsumableClass.Restore).ToList();
        Assert.Equal(16, restores.Count);
        Assert.All(restores, c =>
        {
            Assert.True(FamilyKinds.TryGetValue(c.Family, out var kindId));
            Assert.True(AtomKindRegistry.Get(kindId!)!.AllowsTrigger(AtomTriggers.OnActivate));
        });
    }

    // ---- ⛔ the defect the corpus carries -----------------------------------------------------------------

    [Fact]
    public void Exactly_one_phantom_family_is_named_by_the_corpus_and_it_is_excluded_rather_than_guessed()
    {
        // ⛔ `atom.elemental-power` resolves to no affix-family row. It is real in
        // `_exemplars/affix-family.exemplar.json` (template content module 8 deliberately left out of
        // the 98) and in the lane's own §7.2 worked example, but the shipped corpus has no such family.
        // Module 10 filed eight phantom families; module 17 found five of them in the unique corpus.
        // This is a NINTH, from a third direction. Excluded from the runtime check rather than guessed
        // into it — a guess would make an unresolved reference look like a balance failure.
        var report = Report();
        Assert.Equal(new[] { "atom.elemental-power" }, report.PhantomFamilies);

        // 11 of the 60 rows sit on it, all of them draughts. (13 rows carry an `element`; two of those
        // name `atom.elemental-defense`, which IS one of the shipped 98.)
        Assert.Equal(11, Corpus.Count(c => c.Family == "atom.elemental-power"));
        Assert.Equal(2, Corpus.Count(c => c.Family == "atom.elemental-defense"));
        Assert.Contains("atom.elemental-defense", FamilyKinds.Keys, StringComparer.Ordinal);
        Assert.All(Corpus.Where(c => c.Family == "atom.elemental-power"),
            c => Assert.Equal(ConsumableClass.Draught, c.ClassId));

        // and it really is absent from the shipped 98, not merely missing a kindId
        Assert.DoesNotContain("atom.elemental-power", FamilyKinds.Keys, StringComparer.Ordinal);
        Assert.Equal(98, FamilyKinds.Count);
    }

    // ---- module 11's 60 refused drop entries -------------------------------------------------------------

    [Fact]
    public void All_sixty_consumable_drop_entries_resolve_against_this_corpus()
    {
        // Module 11 refused 60 `consumable` drop-table entries by name, naming this module. Every one
        // of their refs points at a row that exists here — the block really was "referentially perfect
        // and unobtainable", exactly as it was for the 144 uniques.
        var ids = new HashSet<string>(Corpus.Select(c => c.ContainerId), StringComparer.Ordinal);
        var refs = new List<string>();

        foreach (var f in Directory.GetFiles(Seed("items", "drop-tables"), "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            if (!doc.RootElement.TryGetProperty("entries", out var tables)) continue;
            foreach (var table in tables.EnumerateArray())
            {
                if (!table.TryGetProperty("groups", out var groups)) continue;
                foreach (var group in groups.EnumerateArray())
                {
                    if (!group.TryGetProperty("entries", out var rows)) continue;
                    foreach (var row in rows.EnumerateArray())
                        if (row.TryGetProperty("entryKind", out var k) && k.GetString() == "consumable")
                            refs.Add(row.GetProperty("ref").GetString()!);
                }
            }
        }

        Assert.Equal(60, refs.Count);
        Assert.All(refs, r => Assert.Contains(r, ids));
    }

    [Fact]
    public void The_consumable_drop_entry_kind_STAYS_refused_and_the_reason_names_the_real_blocker()
    {
        // ⏸ Module 11's reason read "module 18 (consumables); ssot-generation.md §5.4 keeps it
        // deliberately absent until the action layer exists". Module 18 exists, so that pointer would
        // now be stale in exactly the way this program keeps naming. Updated in place — and pinned, so
        // it cannot go stale a second time.
        Assert.False(DropTableDraw.IsAvailable(DropEntryKind.Consumable));
        var reason = DropTableDraw.UnavailableKinds[DropEntryKind.Consumable];
        Assert.Contains("seed-to-concrete", reason, StringComparison.Ordinal);
        Assert.Contains("X7", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("until the action layer exists", reason, StringComparison.Ordinal);
    }

    // ---- what does NOT exist yet, pinned so it cannot be claimed --------------------------------------

    [Fact]
    public void No_recipe_in_the_shipped_corpus_outputs_a_consumable_container()
    {
        // ⏸ §7.5 prices a batch of Lesser Restorative (`operation = forge`, `output_qty = 5`) and I9's
        // schema already allows it, but module 14's 30-recipe corpus authors none. Pinned as an absence
        // so "recipes output consumables" is not read as shipped.
        var outputs = new List<string>();
        foreach (var f in Directory.GetFiles(Seed("items", "recipes"), "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            if (!doc.RootElement.TryGetProperty("entries", out var entries)) continue;
            foreach (var e in entries.EnumerateArray())
                if (e.TryGetProperty("outputRef", out var r) && r.ValueKind == JsonValueKind.String)
                    outputs.Add(r.GetString()!);
        }

        Assert.DoesNotContain(outputs, o => o.StartsWith("consumable.", StringComparison.Ordinal));
    }

    [Fact]
    public void No_girdle_base_type_authors_consumableSlots_yet_so_the_belt_count_is_a_caller_parameter()
    {
        // ⏸ D37's consequence 1: "Module 6 authors `girdle` base types with a `consumableSlots` value
        // on the directional-profile pass." Not done — measured, not assumed. Until it is, the belt
        // count reaches GateManifest as a parameter and an unequipped player is refused at 0.
        var girdles = 0;
        foreach (var f in Directory.GetFiles(Seed("items", "base-types"), "*.json", SearchOption.AllDirectories))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            if (!doc.RootElement.TryGetProperty("entries", out var entries)) continue;
            foreach (var e in entries.EnumerateArray())
            {
                if (!e.TryGetProperty("role", out var role) || role.GetString() != "girdle") continue;
                girdles++;
                Assert.False(e.TryGetProperty("consumableSlots", out _));
            }
        }

        Assert.True(girdles > 0, "no girdle base type exists at all, which would be a different defect");
    }
}
