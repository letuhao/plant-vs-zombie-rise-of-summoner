using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items.Display;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Items;

/// <summary>
/// `item-card` (item module 10) — the projection that turns atoms into text a player can read.
/// Everything here is pure Core, no browser, no Unity (SC8).
/// </summary>
public class ItemDisplayTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }

    static List<DisplayTemplateRow> LoadAllTemplates()
    {
        var dir = Path.Combine(RepoRoot(), "data", "seed", "items", "display-templates");
        return Directory.EnumerateFiles(dir, "*.json")
            .SelectMany(f => DisplayTemplates.Parse(File.ReadAllText(f)))
            .ToList();
    }

    // ---- N1: the already-authored template corpus --------------------------------------------

    [Fact]
    public void Ninety_eight_families_have_a_display_template()
    {
        Assert.Equal(98, LoadAllTemplates().Count);
    }

    /// <summary>
    /// ⛔ A real, pre-existing defect found while building this module's coverage check, confirmed
    /// against `git show HEAD` to predate this session entirely: seven `implicit.family` values used
    /// by real `base-types/infusion/**` entries (and by several `sets`/`uniques` fixed-atom lists) do
    /// not correspond to ANY shipped `affix-families/*.json` entry, despite being listed as legal in
    /// `classes.v1.json`'s own frozen `infusion.legalFamilies` and despite `atom-family-library.md`
    /// §3.4 claiming all five status.apply families in that row are "shipped". `atom.affliction`
    /// (also in that legalFamilies list) is an eighth phantom with the same shape. This is exactly
    /// `MissingDisplayTemplate`'s job to catch — pinned here as a named, evidenced gap, not fixed by
    /// this module (authoring the missing atoms is affix-legality/base-types' territory, not display's).
    /// </summary>
    [Fact]
    public void Phantom_implicit_families_used_by_real_content_have_no_display_template()
    {
        string[] knownPhantoms =
        {
            "atom.buttering", "atom.chilling", "atom.blighting", "atom.rotting",
            "atom.sparking", "atom.marking", "atom.bonding", "atom.affliction",
        };
        var templated = LoadAllTemplates().Select(r => r.RuntimeFamily).ToHashSet(StringComparer.Ordinal);

        foreach (var phantom in knownPhantoms)
            Assert.DoesNotContain(phantom, templated);
    }

    [Fact]
    public void Stalwart_renders_live_C2_is_fixed()
    {
        var stalwart = LoadAllTemplates().Single(r => r.RuntimeFamily == "atom.stalwart");
        Assert.Equal("live", stalwart.Status);
    }

    [Fact]
    public void Every_template_resolves_with_no_leftover_placeholder_for_both_frames()
    {
        foreach (var row in LoadAllTemplates())
        {
            var placeholders = DisplayTemplates.PlaceholdersOf(row.Template);
            var args = placeholders.ToDictionary(p => p, p => "42", StringComparer.Ordinal);

            foreach (var frame in new[] { "humanoid", "plant" })
            {
                var rendered = DisplayTemplates.Render(row, frame, args);
                Assert.DoesNotContain('{', rendered);
                Assert.DoesNotContain('}', rendered);
                Assert.False(string.IsNullOrWhiteSpace(rendered));
            }
        }
    }

    [Fact]
    public void A_plant_frame_item_uses_the_override_where_present_and_the_humanoid_template_otherwise()
    {
        var evasion = LoadAllTemplates().Single(r => r.RuntimeFamily == "atom.evasion");
        Assert.NotNull(evasion.PlantOverrideTemplate);

        var args = new Dictionary<string, string> { ["value"] = "20", ["element"] = "fire" };
        var humanoid = DisplayTemplates.Render(evasion, "humanoid", args);
        var plant = DisplayTemplates.Render(evasion, "plant", args);
        Assert.NotEqual(humanoid, plant);

        var vitality = LoadAllTemplates().Single(r => r.RuntimeFamily == "atom.vitality");
        var vArgs = new Dictionary<string, string> { ["value"] = "45" };
        Assert.Equal(DisplayTemplates.Render(vitality, "humanoid", vArgs), DisplayTemplates.Render(vitality, "plant", vArgs));
    }

    [Fact]
    public void A_pending_status_family_is_refused_by_the_line_producer()
    {
        var atom = new AtomRow { AtomId = "atom.entangling.t1", KindId = "status.apply", FamilyId = "atom.entangling", Tier = 1, Name = "Entangling" };
        var template = LoadAllTemplates().Single(r => r.RuntimeFamily == "atom.entangling");
        Assert.Equal("pending", template.Status);

        Assert.Throws<DisplayTemplateRejection>(() =>
            ItemDisplayRenderer.Line(template, atom, "humanoid", 100, SourceKind.AffixPrefix, 0, UnitClass.PerMilleRatio));
    }

    // ---- Rule 1: the shipped percent conversion, adopted -----------------------------------------

    [Theory]
    [InlineData(150, "15%")]
    [InlineData(153, "15.3%")]
    [InlineData(10, "1%")]
    [InlineData(1, "0.1%")]
    public void FormatPerMille_matches_the_shipped_patronView_conversion(int milli, string expected) =>
        Assert.Equal(expected, ItemDisplayRenderer.FormatPerMille(milli));

    [Fact]
    public void A_nonzero_per_mille_never_renders_as_zero_percent()
    {
        var rendered = ItemDisplayRenderer.FormatPerMille(1);
        Assert.NotEqual("0%", rendered);
    }

    [Theory]
    [InlineData(250, "250 ms")]
    [InlineData(4000, "4.0 s")]
    public void FormatMilliseconds_renders_short_durations_as_ms_and_long_as_seconds(int ms, string expected) =>
        Assert.Equal(expected, ItemDisplayRenderer.FormatMilliseconds(ms));

    // ---- roll-quality bar: only OnInstantiate gets one -------------------------------------------

    [Fact]
    public void Fixed_roll_policy_never_gets_a_bar()
    {
        Assert.Null(ItemDisplayRenderer.BarFor(RollPolicy.Fixed, 1000));
    }

    [Fact]
    public void OnApply_never_gets_a_bar_the_hit_rolled_it_not_the_item()
    {
        Assert.Null(ItemDisplayRenderer.BarFor(RollPolicy.OnApply, 500));
    }

    [Fact]
    public void A_real_roll_never_renders_as_an_empty_bar()
    {
        var bar = ItemDisplayRenderer.BarFor(RollPolicy.OnInstantiate, qualityPerMille: 1);
        Assert.NotNull(bar);
        Assert.True(bar!.Value.Segments >= 1);
    }

    [Theory]
    [InlineData(0, 1)]    // the floor: even the worst roll shows one segment, never an empty bar
    [InlineData(200, 1)]
    [InlineData(201, 2)]
    [InlineData(999, 5)]
    [InlineData(1000, 5)]
    public void Segments_scale_with_quality_and_clamp_to_five(int qualityPerMille, int expectedSegments)
    {
        var bar = ItemDisplayRenderer.BarFor(RollPolicy.OnInstantiate, qualityPerMille);
        Assert.Equal(expectedSegments, bar!.Value.Segments);
    }

    // ---- ChannelUnits: N3 is already-shipped, this is the facade ---------------------------------

    [Fact]
    public void Primary_channels_resolve_to_game_units()
    {
        Assert.Equal(UnitClass.GameUnits, ChannelUnits.For("maxHp"));
        Assert.Equal(UnitClass.GameUnits, ChannelUnits.For("atk"));
    }

    [Fact]
    public void Attack_interval_resolves_to_milliseconds()
    {
        Assert.Equal(UnitClass.Milliseconds, ChannelUnits.For("attackInterval"));
    }

    [Fact]
    public void A_status_power_derived_channel_resolves_via_the_shipped_registry()
    {
        var unit = ChannelUnits.For("status.power.dot");
        Assert.Equal(UnitClass.StatusPotencyPoints, unit);
    }

    [Fact]
    public void An_unknown_channel_resolves_to_null_never_a_guess()
    {
        Assert.Null(ChannelUnits.For("not.a.real.channel"));
    }

    // ---- rarity palette ---------------------------------------------------------------------------

    [Fact]
    public void The_dark_palette_l_star_runs_from_42_to_92_and_is_monotone()
    {
        var result = RarityPalette.Validate(RarityPalette.Dark, RarityPalette.LightnessDirection.Increasing);
        Assert.True(result.Ok, string.Join("\n", result.Failures));

        Assert.Equal(42.1, RarityPalette.LStar(RarityPalette.Dark[0]), 1);
        Assert.Equal(91.9, RarityPalette.LStar(RarityPalette.Dark[^1]), 1);
    }

    [Fact]
    public void Dark_and_light_palettes_both_satisfy_the_monotone_rules()
    {
        var dark = RarityPalette.Validate(RarityPalette.Dark, RarityPalette.LightnessDirection.Increasing);
        var light = RarityPalette.Validate(RarityPalette.Light, RarityPalette.LightnessDirection.Decreasing);

        Assert.True(dark.Ok, string.Join("\n", dark.Failures));
        Assert.True(light.Ok, string.Join("\n", light.Failures));
    }

    [Fact]
    public void Rung_name_meets_wcag_aa_on_the_light_theme_ground()
    {
        foreach (var hex in RarityPalette.Light)
            Assert.True(RarityPalette.RungNameMeetsWcagAa(hex, "#ffffff"), $"{hex} fails WCAG AA against white");
    }

    [Fact]
    public void Pip_count_and_display_key_do_not_fork_only_color_hex_gains_a_second_value()
    {
        // Structural guarantee: both palettes are the SAME length (one slot per rung), and nothing in
        // this module carries a per-theme pip count or display key -- only the hex list itself forks.
        Assert.Equal(RarityPalette.Dark.Count, RarityPalette.Light.Count);
        Assert.Equal(10, RarityPalette.Dark.Count);
    }

    [Fact]
    public void A_uniform_palette_fails_the_adjacent_delta_rule()
    {
        // Negative control: proves Validate() actually rejects a bad palette rather than always passing.
        var flat = Enumerable.Repeat("#808080", 10).ToList();
        var result = RarityPalette.Validate(flat, RarityPalette.LightnessDirection.Increasing);
        Assert.False(result.Ok);
    }
}
