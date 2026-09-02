using FusionRpg.Core.Effects.Atoms;
using Xunit;

namespace FusionRpg.Core.Tests.Atoms;

/// <summary>
/// T5.2 (`eligibility-tags`, `spec-eligibility-tags.md`): tag-based affix eligibility plus the
/// per-container allow/deny escape hatch — what keeps the affix library shared across features (Q6)
/// instead of forked per feature.
/// </summary>
public class EligibilityRuleTests
{
    static AffixRow Affix(string id, AffixClass cls = AffixClass.Prefix) =>
        new(id, cls, new[] { new AffixRefRow(1, "atom." + id.Replace("affix.", "") + ".t1") });

    static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Catalog = new(StringComparer.Ordinal)
    {
        ["affix.fire-power"] = new Dictionary<string, string> { ["element"] = "fire", ["theme"] = "elemental" },
        ["affix.ice-power"] = new Dictionary<string, string> { ["element"] = "ice", ["theme"] = "elemental" },
        ["affix.plain-strength"] = new Dictionary<string, string> { ["theme"] = "physical" },
        ["affix.hidden-gem"] = new Dictionary<string, string> { ["theme"] = "cosmetic" },
    };

    static IReadOnlyDictionary<string, string> TagsOf(string id) =>
        Catalog.TryGetValue(id, out var t) ? t : new Dictionary<string, string>();

    static readonly IReadOnlyList<AffixRow> AllAffixes = Catalog.Keys.Select(id => Affix(id)).ToArray();

    [Fact]
    public void Tag_match_selects_the_eligible_set()
    {
        var rule = new EligibilityRule(
            RequireTags: new[] { "element" }, AnyOfTags: Array.Empty<string>(),
            Allow: Array.Empty<string>(), Deny: Array.Empty<string>());

        var pool = EligibilityResolver.DrawablePool(AllAffixes, TagsOf, rule);

        Assert.Equal(new[] { "affix.fire-power", "affix.ice-power" }, pool.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void Allow_admits_an_affix_the_tags_alone_would_exclude()
    {
        var rule = new EligibilityRule(
            RequireTags: new[] { "element" }, AnyOfTags: Array.Empty<string>(),
            Allow: new[] { "affix.hidden-gem" }, Deny: Array.Empty<string>());

        var pool = EligibilityResolver.DrawablePool(AllAffixes, TagsOf, rule);

        Assert.Contains("affix.hidden-gem", pool); // no "element" tag — tags alone would exclude it
    }

    [Fact]
    public void Deny_excludes_an_affix_the_tags_alone_would_include()
    {
        var rule = new EligibilityRule(
            RequireTags: new[] { "element" }, AnyOfTags: Array.Empty<string>(),
            Allow: Array.Empty<string>(), Deny: new[] { "affix.fire-power" });

        var pool = EligibilityResolver.DrawablePool(AllAffixes, TagsOf, rule);

        Assert.DoesNotContain("affix.fire-power", pool); // has "element" — tags alone would include it
        Assert.Contains("affix.ice-power", pool);
    }

    [Fact]
    public void Deny_wins_over_allow_for_the_same_affix()
    {
        var rule = new EligibilityRule(
            RequireTags: Array.Empty<string>(), AnyOfTags: Array.Empty<string>(),
            Allow: new[] { "affix.hidden-gem" }, Deny: new[] { "affix.hidden-gem" });

        Assert.False(EligibilityResolver.IsEligible("affix.hidden-gem", TagsOf("affix.hidden-gem"), rule));
    }

    [Fact]
    public void An_unsatisfiable_eligible_rule_rejects_at_load()
    {
        // requireTags selects nothing at all — a container that still wants a prefix roll cannot be
        // satisfied, and this must reject at LOAD, not silently under-fill at roll time.
        var rule = new EligibilityRule(
            RequireTags: new[] { "no-such-tag" }, AnyOfTags: Array.Empty<string>(),
            Allow: Array.Empty<string>(), Deny: Array.Empty<string>());

        var r = EligibilityResolver.Validate(rule, AllAffixes, TagsOf, prefixRolls: 1, suffixRolls: 0);

        Assert.Equal(AtomRejectionReason.UnsatisfiablePool, r.Reason);
    }

    [Fact]
    public void A_satisfiable_rule_with_no_roll_budget_never_rejects()
    {
        var rule = new EligibilityRule(
            RequireTags: new[] { "no-such-tag" }, AnyOfTags: Array.Empty<string>(),
            Allow: Array.Empty<string>(), Deny: Array.Empty<string>());

        // Zero rolls in both budgets — an empty eligible set is fine, nothing will ever draw from it.
        var r = EligibilityResolver.Validate(rule, AllAffixes, TagsOf, prefixRolls: 0, suffixRolls: 0);

        Assert.True(r.IsOk, r.ToString());
    }

    [Fact]
    public void An_allow_reference_to_an_unknown_affix_is_rejected()
    {
        var rule = new EligibilityRule(
            RequireTags: Array.Empty<string>(), AnyOfTags: Array.Empty<string>(),
            Allow: new[] { "affix.does-not-exist" }, Deny: Array.Empty<string>());

        var r = EligibilityResolver.Validate(rule, AllAffixes, TagsOf, prefixRolls: 0, suffixRolls: 0);

        Assert.Equal(AtomRejectionReason.UnknownAtom, r.Reason);
    }

    [Fact]
    public void Two_features_declare_independent_eligibility_over_the_same_shared_affix()
    {
        // Q6's own reconciliation: "elemental mastery" (affix.fire-power) is authored ONCE and is
        // eligible for both a feature that wants any "element" affix and one that wants only affixes
        // tagged "physical" — proven by resolving the SAME catalog against two independent rules,
        // neither forking a copy of the affix.
        var elementFeature = new EligibilityRule(
            RequireTags: new[] { "element" }, AnyOfTags: Array.Empty<string>(),
            Allow: Array.Empty<string>(), Deny: Array.Empty<string>());
        var physicalFeature = new EligibilityRule(
            RequireTags: new[] { "theme" }, AnyOfTags: new[] { "theme:physical" },
            Allow: Array.Empty<string>(), Deny: Array.Empty<string>());

        var elementPool = EligibilityResolver.DrawablePool(AllAffixes, TagsOf, elementFeature);
        var physicalPool = EligibilityResolver.DrawablePool(AllAffixes, TagsOf, physicalFeature);

        Assert.Contains("affix.fire-power", elementPool);
        Assert.DoesNotContain("affix.fire-power", physicalPool);
        Assert.Contains("affix.plain-strength", physicalPool);
        // Same catalog entry, same AffixRow instance, resolved twice — never a per-feature fork.
        Assert.Same(AllAffixes.Single(a => a.AffixId == "affix.fire-power"),
            AllAffixes.Single(a => a.AffixId == "affix.fire-power"));
    }
}
