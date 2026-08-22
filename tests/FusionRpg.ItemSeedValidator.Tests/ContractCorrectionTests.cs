using FusionRpg.Tools.ItemSeedValidator;
using FusionRpg.Tools.ItemSeedValidator.Model;
using Xunit;

namespace FusionRpg.ItemSeedValidator.Tests;

/// <summary>
/// Five rules the validator got wrong, each found by a stage-1c partition failing while it was
/// correctly authored. They are grouped here rather than spread across the per-kind files because
/// they share one shape: the check was written for base types and then applied to a kind it does
/// not describe. Every test below fails against the pre-correction validator.
/// </summary>
public class ContractCorrectionTests
{
    static ValidationResult Validate(string kind, string directory, string entryJson)
    {
        var json = $$"""
        {
          "schemaVersion": 1,
          "kind": "{{kind}}",
          "_meta": {
            "batch": "test", "partition": "test/x", "contractVersion": 1,
            "registryVersions": { "naming": 1 },
            "exemplarVersion": 1, "promptVersion": 1,
            "model": "test", "authoredUtc": "2026-08-22T00:00:00Z", "sourceRef": "test"
          },
          "entries": [ {{entryJson}} ]
        }
        """;
        return Validator.Run(SeedFixture.Registries(),
            new[] { SeedFile.Parse(json, $"{directory}/test.json", directory) });
    }

    static IEnumerable<string> Codes(ValidationResult r) => r.Findings.Select(f => f.Code);
    static IEnumerable<string> Errors(ValidationResult r) =>
        r.Findings.Where(f => f.Severity == Severity.Error).Select(f => f.Code);

    /// <summary>
    /// A display template's localized string IS the template — "+{value} max health" is the whole
    /// point of the row. The placeholder rule was written to keep braces out of item names.
    /// </summary>
    [Fact]
    public void Display_template_name_may_carry_a_placeholder()
    {
        var result = Validate("display-template", "display-templates", """
            {
              "id": "disptpl.p1-001", "nameKey": "disptpl.affix.vitality",
              "name": "+{value} max health", "runtimeFamily": "atom.vitality",
              "plantOverrideKey": null, "groupId": "g.life", "status": "live", "tags": []
            }
            """);
        Assert.DoesNotContain("MarkupInString", Errors(result));
    }

    /// <summary>Real markup is still markup, template or not.</summary>
    [Fact]
    public void Display_template_name_may_not_carry_actual_markup()
    {
        var result = Validate("display-template", "display-templates", """
            {
              "id": "disptpl.p1-001", "nameKey": "disptpl.affix.vitality",
              "name": "<b>+{value} max health</b>", "runtimeFamily": "atom.vitality",
              "plantOverrideKey": null, "groupId": "g.life", "status": "live", "tags": []
            }
            """);
        Assert.Contains("MarkupInString", Errors(result));
    }

    /// <summary>
    /// On a recipe, `frame` is the scope the recipe applies to, and entry-shapes.md §4 gives it
    /// three values: humanoid | plant | any. core.v1.json's roster is the body list and has no
    /// `any` in it, which is correct for a body and wrong for a scope.
    /// </summary>
    [Fact]
    public void Recipe_frame_may_be_any()
    {
        var result = Validate("recipe", "recipes", """
            {
              "id": "recipe.001", "nameKey": "recipe.forge-test", "name": "Forge: Test",
              "operation": "forge", "outputKind": "mutation", "frame": "any",
              "costLines": [ { "material": "essence.fire", "costBand": "modest" } ]
            }
            """);
        Assert.DoesNotContain("RegistryValueUnknown", Errors(result));
    }

    /// <summary>A base type's frame is a body, and `any` is not one of them.</summary>
    [Fact]
    public void Base_type_frame_may_not_be_any()
    {
        var result = Validate("base-type", "base-types", """
            {
              "id": "item.humanoid-torso-a-001", "nameKey": "base.test", "name": "Ashen Fang",
              "frame": "any", "role": "core-guard", "class": "plate", "band": "a",
              "iconKey": "icon.test", "tags": []
            }
            """);
        Assert.Contains("RegistryValueUnknown", Errors(result));
    }

    /// <summary>
    /// A milestone's `runtimeFamily` is MINTED in a reserved stem, not borrowed — entry-shapes.md
    /// §6 requires it NOT to match an existing family. Resolving it as a reference inverted the
    /// rule and failed all ten correctly-authored rows.
    /// </summary>
    [Fact]
    public void Milestone_runtime_family_is_minted_not_resolved()
    {
        var result = Validate("enhancement-milestone", "enhancement-milestones", """
            {
              "id": "enh.001", "nameKey": "enh.enhance-vigor", "name": "Enhancement Vigor",
              "runtimeFamily": "atom.enhance-vigor", "kindId": "stat.modify",
              "params": { "channel": "maxHp", "op": "Flat" },
              "powerBand": "medium", "tags": ["defensive"]
            }
            """);
        Assert.DoesNotContain("ReferenceUnresolved", Errors(result));
    }

    /// <summary>The reserved stem is the whole mechanism, so leaving it must still reject.</summary>
    [Fact]
    public void Milestone_runtime_family_outside_the_reserved_stem_rejects()
    {
        var result = Validate("enhancement-milestone", "enhancement-milestones", """
            {
              "id": "enh.001", "nameKey": "enh.vigor", "name": "Enhancement Vigor",
              "runtimeFamily": "atom.vigor", "kindId": "stat.modify",
              "params": { "channel": "maxHp", "op": "Flat" },
              "powerBand": "medium", "tags": ["defensive"]
            }
            """);
        Assert.Contains("RuntimeFamilyStem", Errors(result));
    }

    /// <summary>
    /// A lowercase run inside a hyphenated compound is not a connective. The name pattern regex
    /// accepts `Wind-borne Inlay`, so flagging `borne` contradicted the grammar being enforced.
    /// </summary>
    [Fact]
    public void Hyphenated_compound_is_not_an_invented_connective()
    {
        var result = Validate("base-type", "base-types", """
            {
              "id": "item.plant-graft-1-b-002", "nameKey": "base.windborne-inlay",
              "name": "Wind-borne Inlay", "frame": "plant", "role": "jewel-minor",
              "class": "graft", "band": "b", "iconKey": "icon.test", "tags": []
            }
            """);
        Assert.DoesNotContain("InventedConnective", Errors(result));
    }

    /// <summary>A free-standing lowercase word still is one.</summary>
    [Fact]
    public void Free_standing_lowercase_word_is_still_an_invented_connective()
    {
        var result = Validate("base-type", "base-types", """
            {
              "id": "item.plant-graft-1-b-002", "nameKey": "base.inlay-and-fang",
              "name": "Inlay and Fang", "frame": "plant", "role": "jewel-minor",
              "class": "graft", "band": "b", "iconKey": "icon.test", "tags": []
            }
            """);
        Assert.Contains("InventedConnective", Errors(result));
    }

    /// <summary>
    /// An affix family is a mechanic label, and every family that ships is one word: Lifesteal,
    /// Retribution, Volley. The two-pool-word fusion rule demanded that new mechanics look unlike
    /// every mechanic already in the game.
    /// </summary>
    [Fact]
    public void Affix_family_may_carry_a_single_word_label()
    {
        var result = Validate("affix-family", "affix-families", """
            {
              "id": "atom.death-harvest", "nameKey": "affix.death-harvest", "name": "Harvest",
              "kindId": "stat.modify", "params": { "channel": "maxHp", "op": "Flat" },
              "powerBand": "medium", "tags": []
            }
            """);
        Assert.DoesNotContain("FusionNotDecomposable", Errors(result));
    }

    /// <summary>
    /// tags.v1.json's own `appliesToNote` calls the field "authoring guidance, not an enforced
    /// constraint". Enforcing it as an error contradicted the registry being enforced, so it is
    /// reported as a warning instead — still visible, no longer a gate.
    /// </summary>
    [Fact]
    public void Tag_axis_applicability_is_a_warning_not_an_error()
    {
        var result = Validate("consumable", "consumables", """
            {
              "id": "consumable.k1-001", "nameKey": "consumable.test", "name": "Ashen Draught",
              "classId": "draught", "useContext": ["menu"], "family": "atom.vitality",
              "powerBand": "low", "manifestCost": 1, "tags": ["heavy"]
            }
            """);
        Assert.DoesNotContain("TagAxisNotApplicable", Errors(result));
    }

    /// <summary>An unknown tag is still closed-vocabulary violation, and still an error.</summary>
    [Fact]
    public void Unknown_tag_is_still_an_error()
    {
        var result = Validate("consumable", "consumables", """
            {
              "id": "consumable.k1-001", "nameKey": "consumable.test", "name": "Ashen Draught",
              "classId": "draught", "useContext": ["menu"], "family": "atom.vitality",
              "powerBand": "low", "manifestCost": 1, "tags": ["instance-mutation"]
            }
            """);
        Assert.Contains("TagUnknown", Errors(result));
    }
}
