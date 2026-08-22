using System.Text.Json.Nodes;
using FusionRpg.Tools.ItemSeedValidator;
using FusionRpg.Tools.ItemSeedValidator.Model;
using FusionRpg.Tools.ItemSeedValidator.Registries;

namespace FusionRpg.ItemSeedValidator.Tests;

/// <summary>
/// Small inline registries and seed files. Deliberately tiny: these tests check the validator's
/// logic, not the real corpus, and a fixture nobody can read in one screen is a fixture nobody
/// checks. The real registries are exercised by running the tool.
/// </summary>
public static class SeedFixture
{
    public const string Core = """
    {
      "schemaVersion": 1, "registryVersion": 1, "frozen": true,
      "roles": {
        "frames": [{ "id": "humanoid" }, { "id": "plant" }],
        "list": [{ "roleId": "head-guard", "humanoidName": "head", "plantName": "crown" }],
        "commanderOnly": []
      },
      "rarity": { "ladder": [{ "id": "heirloom", "ordinal": 70 }] },
      "categories": { "list": [{ "id": "equipment" }] }
    }
    """;

    public const string Bands = """
    {
      "schemaVersion": 1, "registryVersion": 1, "frozen": true,
      "powerBand": {
        "enum": ["trivial", "low", "medium", "high", "extreme"],
        "tierMap": { "trivial": 1, "low": 2, "medium": 3, "high": 4, "extreme": 5 }
      },
      "costBand": { "enum": ["cheap", "steep"] },
      "dropBand": { "enum": ["staple", "uncommon"] },
      "variance": { "enum": ["narrow", "wide"] }
    }
    """;

    public const string Tags = """
    {
      "schemaVersion": 1, "registryVersion": 1, "frozen": true,
      "axes": [
        { "id": "material-nature", "exclusive": true, "appliesTo": ["base-type"], "values": ["organic", "metal"] },
        { "id": "mass-class", "exclusive": true, "appliesTo": ["base-type"], "values": ["light", "heavy"] }
      ],
      "tags": [
        { "id": "organic", "axis": "material-nature" }, { "id": "metal", "axis": "material-nature" },
        { "id": "light", "axis": "mass-class" }, { "id": "heavy", "axis": "mass-class" }
      ]
    }
    """;

    public const string Themes = """
    {
      "schemaVersion": 1, "registryVersion": 1, "frozen": true,
      "themes": [{ "id": "ember-harvest", "elementAffinity": ["fire"] }]
    }
    """;

    public const string Classes = """
    {
      "schemaVersion": 1, "registryVersion": 1, "frozen": true,
      "classLadders": {
        "armour": {
          "humanoid": [{ "id": "cloth", "rung": 1 }],
          "plant": [{ "id": "heartwood", "rung": 3 }]
        }
      },
      "implicitSlates": { "head-guard": { "legalFamilies": ["atom.vitality"], "excludedForRole": [] } },
      "excludedFamilies": { "global": [] }
    }
    """;

    public const string Naming = """
    {
      "schemaVersion": 1, "registryVersion": 1, "frozen": true,
      "idPolicy": { "sequenceDigits": 3, "sequenceRange": { "wave1Batch": "001-899" } },
      "idNamespaces": {
        "baseTypes": {
          "idTemplate": "item.{frame}-{frameRoleName}-{band}-{seq:03}",
          "frames": ["humanoid", "plant"], "bands": ["a", "b"]
        },
        "affixFamilies": {
          "groups": [{ "groupId": "g.life", "stem": "life", "existingFamilies": ["vitality"] }]
        },
        "uniques": { "bandAssignment": [{ "rungBandLowOrdinal": 70, "themeIds": ["ember-harvest"] }] },
        "sets": { "themeIds": ["ember-harvest"] },
        "charms": {
          "axisGroups": [{ "axisGroupId": "econ", "axes": ["economy"] }],
          "resonanceNote": "reserve charm.res-economy-2 and charm.res-economy-3"
        },
        "gems": { "idTemplate": "gem.g{slot}-{seq:03}", "slots": [1] },
        "materials": { "idTemplate": "material.{seq:03}" },
        "curves": { "idTemplate": "curve.{seq:03}" },
        "attributes": { "idTemplate": "attr.{seq:03}" },
        "socketWords": { "idTemplate": "sockword.{seq:03}" },
        "recipes": { "idTemplate": "recipe.{seq:03}" },
        "enhancementMilestones": { "idTemplate": "enh.{seq:03}" },
        "consumables": { "idTemplate": "consumable.k{slot}-{seq:03}", "slotAssignment": [{ "slot": 1 }] },
        "dropTables": { "idTemplate": "droptable.d{slot}-{seq:03}", "slotAssignment": [{ "slot": 1 }] },
        "displayTemplates": { "idTemplate": "disptpl.p{slot}-{seq:03}", "slotAssignment": [{ "slot": 1 }] }
      },
      "namingGrammar": {
        "nameKey": { "kindPrefixes": ["base", "unique", "set", "affix"] },
        "collisionNormalization": {
          "algorithm": [
            "4. Drop any resolved token that is one of the four closed connectives: `of`, `the`, `a`, `and`."
          ]
        }
      }
    }
    """;

    /// <summary>ash/Ashen and fang, plus Thistledown as an atomic seed word (rule 2a).</summary>
    public const string Words = """
    {
      "schemaVersion": 1, "registryVersion": 1, "frozen": true,
      "words": [
        { "canonicalId": "ash", "surfaceForms": { "noun": ["Ash"], "adjective": ["Ashen"] } },
        { "canonicalId": "fang", "surfaceForms": { "noun": ["Fang"] } },
        { "canonicalId": "thistle", "surfaceForms": { "noun": ["Thistle"] } },
        { "canonicalId": "down", "surfaceForms": { "noun": ["Down"] } },
        { "canonicalId": "thistledown", "surfaceForms": { "ofConcept": ["Thistledown"] } }
      ]
    }
    """;

    static JsonObject Obj(string json) => (JsonObject)JsonNode.Parse(json)!;

    public static RegistrySet Registries(bool withWords = false) =>
        RegistrySet.FromNodes(
            Obj(Core), Obj(Bands), Obj(Tags), Obj(Themes), Obj(Classes), Obj(Naming),
            withWords ? Obj(Words) : null);

    /// <summary>A complete, conforming base-type file with the given entries spliced in.</summary>
    public static string BaseTypeFile(params string[] entries) => $$"""
    {
      "schemaVersion": 1,
      "kind": "base-type",
      "_meta": {
        "batch": "test", "partition": "plant/crown/a", "contractVersion": 1,
        "registryVersions": { "tags": 1, "naming": 1 },
        "exemplarVersion": 1, "promptVersion": 1,
        "model": "test", "authoredUtc": "2026-08-22T00:00:00Z", "sourceRef": "test"
      },
      "entries": [ {{string.Join(",\n", entries)}} ]
    }
    """;

    /// <summary>A conforming base-type entry; <paramref name="extra"/> splices in the offence.</summary>
    public static string BaseTypeEntry(string id = "item.plant-crown-a-001",
        string name = "Heartbloom Crown", string nameKey = "base.heartbloom-crown", string extra = "") => $$"""
        {
          "id": "{{id}}",
          "nameKey": "{{nameKey}}",
          "name": "{{name}}",
          "frame": "plant",
          "role": "head-guard",
          "class": "heartwood",
          "band": "a",
          "iconKey": "icon.base.test",
          "tags": ["organic"]{{(extra.Length == 0 ? "" : ",\n          " + extra)}}
        }
        """;

    public static ValidationResult Validate(string json, string kindDirectory = "base-types",
        string path = "base-types/test.json", bool withWords = false) =>
        Validator.Run(Registries(withWords),
            new[] { SeedFile.Parse(json, path, kindDirectory) });

    public static IEnumerable<Finding> Errors(ValidationResult r) =>
        r.Findings.Where(f => f.Severity == Severity.Error);

    public static IEnumerable<string> ErrorCodes(ValidationResult r) =>
        Errors(r).Select(f => f.Code);
}
