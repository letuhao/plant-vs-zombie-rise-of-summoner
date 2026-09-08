using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using FusionRpg.Core.Demons.Generation;

namespace FusionRpg.Tools.DemonCorpusDump;

/// <summary>
/// Canonical serialisation + content hash for `demon-seed` module 1 (spec-corpus-dump.md §3/§4).
/// Every render function here is a pure function of its input — no timestamp, no machine state —
/// so the same database produces byte-identical output on every run.
/// </summary>
public static class DumpWriter
{
    static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        SkipValidation = false,
        // Names are Chinese. The default encoder escapes every non-ASCII codepoint as \uXXXX,
        // which is valid JSON but makes a committed, diffable corpus unreadable and its diffs
        // meaningless — same choice DemonCorpusEmit/Program.cs already makes.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    // --- key ordering -------------------------------------------------------------------------
    // "keys sorted ordinal" (spec §3) is enforced mechanically here, not by hand-ordering each
    // WriteString call — a field added later in the wrong place in code still lands in the right
    // place in the file, because the sort runs at render time, not at authoring time.
    static JsonObject SortedObj(params (string Key, JsonNode? Value)[] fields)
    {
        var obj = new JsonObject();
        foreach (var f in fields.OrderBy(f => f.Key, StringComparer.Ordinal))
            obj[f.Key] = f.Value;
        return obj;
    }

    static JsonNode? Str(string? s) => s is null ? null : JsonValue.Create(s);
    static JsonNode? IntOrNull(int? i) => i is null ? null : JsonValue.Create(i.Value);
    static JsonNode? DblOrNull(double? d) => d is null ? null : JsonValue.Create(d.Value);

    // --- node builders --------------------------------------------------------------------------

    static JsonObject EnrichmentNode(DumpEnrichment e) => SortedObj(
        ("damageVsText", Str(e.DamageVsText)),
        ("description", Str(e.Description)),
        ("qualities", e.Qualities is null
            ? null
            : new JsonArray(e.Qualities.Select(q => (JsonNode?)JsonValue.Create(q)).ToArray())),
        ("source", JsonValue.Create(e.Source)),
        ("typeClass", Str(e.TypeClass)),
        ("unlockCondition", Str(e.UnlockCondition)),
        ("weaknessesText", Str(e.WeaknessesText)));

    static JsonObject AlmanacRowNode(DumpAlmanacRow r) => SortedObj(
        ("armor", IntOrNull(r.Armor)),
        ("armorMax", IntOrNull(r.ArmorMax)),
        ("attack", IntOrNull(r.Attack)),
        ("contractVersion", JsonValue.Create(r.ContractVersion)),
        ("cooldownSec", DblOrNull(r.CooldownSec)),
        ("costStatus", JsonValue.Create(r.CostStatus)),
        ("displayName", Str(r.DisplayName)),
        ("enrichment", r.Enrichment is null ? null : EnrichmentNode(r.Enrichment)),
        ("flavorInfo", Str(r.FlavorInfo)),
        ("flavorIntroduce", Str(r.FlavorIntroduce)),
        ("hp", IntOrNull(r.Hp)),
        ("rebuiltUtc", JsonValue.Create(r.RebuiltUtc)),
        ("side", JsonValue.Create(r.Side)),
        ("statsObserved", JsonValue.Create(r.StatsObserved)),
        ("sunCost", IntOrNull(r.SunCost)),
        ("typeId", JsonValue.Create(r.TypeId)),
        ("typeName", Str(r.TypeName)));

    static JsonObject BaselineNode(DumpSpawnBaseline b) => SortedObj(
        ("capturedUtc", JsonValue.Create(b.CapturedUtc)),
        ("side", JsonValue.Create(b.Side)),
        ("statsJson", JsonValue.Create(b.StatsJson)),
        ("typeId", JsonValue.Create(b.TypeId)));

    static JsonObject RecipeNode(DumpRecipe r) => SortedObj(
        ("parentA", JsonValue.Create(r.ParentA)),
        ("parentAName", Str(r.ParentAName)),
        ("parentB", JsonValue.Create(r.ParentB)),
        ("parentBName", Str(r.ParentBName)),
        ("result", JsonValue.Create(r.Result)),
        ("resultName", Str(r.ResultName)));

    static JsonObject ManifestNode(DumpManifest m) => SortedObj(
        ("baselineCount", JsonValue.Create(m.BaselineCount)),
        ("capturedUtc", JsonValue.Create(m.CapturedUtc)),
        ("contentHash", JsonValue.Create(m.ContentHash)),
        ("dumpFormatVersion", JsonValue.Create(m.DumpFormatVersion)),
        ("plantCount", JsonValue.Create(m.PlantCount)),
        ("recipeCount", JsonValue.Create(m.RecipeCount)),
        ("zombieCount", JsonValue.Create(m.ZombieCount)));

    // --- rendering ------------------------------------------------------------------------------

    /// <summary>Serialises one JSON node with the canonical writer options plus a trailing newline.</summary>
    static byte[] Render(JsonNode node)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, WriterOptions))
            node.WriteTo(w);
        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    public static byte[] RenderAlmanac(IReadOnlyList<DumpAlmanacRow> rows)
    {
        var arr = new JsonArray(rows.OrderBy(r => r.TypeId).Select(r => (JsonNode?)AlmanacRowNode(r)).ToArray());
        return Render(arr);
    }

    public static byte[] RenderSpawnBaselines(IReadOnlyList<DumpSpawnBaseline> rows)
    {
        var arr = new JsonArray(rows
            .OrderBy(r => r.Side, StringComparer.Ordinal).ThenBy(r => r.TypeId)
            .Select(r => (JsonNode?)BaselineNode(r)).ToArray());
        return Render(arr);
    }

    public static byte[] RenderRecipes(IReadOnlyList<DumpRecipe> rows)
    {
        var arr = new JsonArray(rows
            .OrderBy(r => r.ParentA).ThenBy(r => r.ParentB).ThenBy(r => r.Result)
            .Select(r => (JsonNode?)RecipeNode(r)).ToArray());
        return Render(arr);
    }

    public static byte[] RenderManifest(DumpManifest manifest) => Render(ManifestNode(manifest));

    /// <summary>SHA-256 over the four payload files' canonical bytes, in a fixed order. Excludes the manifest itself.</summary>
    public static string ComputeContentHash(byte[] plantAlmanac, byte[] zombieAlmanac, byte[] baselines, byte[] recipes)
    {
        using var sha = SHA256.Create();
        using var combined = new MemoryStream();
        combined.Write(plantAlmanac);
        combined.Write(zombieAlmanac);
        combined.Write(baselines);
        combined.Write(recipes);
        var hash = sha.ComputeHash(combined.ToArray());
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Builds the full rendered tree (four payload files + manifest) from a payload and a capture stamp.</summary>
    public static DumpTree BuildTree(DumpPayload payload, string capturedUtc)
    {
        var plantBytes = RenderAlmanac(payload.PlantAlmanac);
        var zombieBytes = RenderAlmanac(payload.ZombieAlmanac);
        var baselineBytes = RenderSpawnBaselines(payload.SpawnBaselines);
        var recipeBytes = RenderRecipes(payload.Recipes);

        var hash = ComputeContentHash(plantBytes, zombieBytes, baselineBytes, recipeBytes);

        var manifest = new DumpManifest(
            DumpFormatVersion: DumpFormat.Version,
            CapturedUtc: capturedUtc,
            ContentHash: hash,
            PlantCount: payload.PlantAlmanac.Count,
            ZombieCount: payload.ZombieAlmanac.Count,
            BaselineCount: payload.SpawnBaselines.Count,
            RecipeCount: payload.Recipes.Count);

        return new DumpTree(
            ManifestBytes: RenderManifest(manifest),
            PlantAlmanacBytes: plantBytes,
            ZombieAlmanacBytes: zombieBytes,
            SpawnBaselineBytes: baselineBytes,
            RecipesBytes: recipeBytes,
            Manifest: manifest);
    }

    public static void WriteToDisk(string outputRoot, DumpTree tree)
    {
        var almanacDir = Path.Combine(outputRoot, "almanac");
        Directory.CreateDirectory(almanacDir);
        File.WriteAllBytes(Path.Combine(outputRoot, "_manifest.json"), tree.ManifestBytes);
        File.WriteAllBytes(Path.Combine(almanacDir, "plant.json"), tree.PlantAlmanacBytes);
        File.WriteAllBytes(Path.Combine(almanacDir, "zombie.json"), tree.ZombieAlmanacBytes);
        File.WriteAllBytes(Path.Combine(outputRoot, "spawn-baseline.json"), tree.SpawnBaselineBytes);
        File.WriteAllBytes(Path.Combine(outputRoot, "recipes.json"), tree.RecipesBytes);
    }

    /// <summary>True when every file on disk under <paramref name="outputRoot"/> byte-matches <paramref name="tree"/>.</summary>
    public static bool MatchesDisk(string outputRoot, DumpTree tree)
    {
        return FileMatches(Path.Combine(outputRoot, "_manifest.json"), tree.ManifestBytes)
            && FileMatches(Path.Combine(outputRoot, "almanac", "plant.json"), tree.PlantAlmanacBytes)
            && FileMatches(Path.Combine(outputRoot, "almanac", "zombie.json"), tree.ZombieAlmanacBytes)
            && FileMatches(Path.Combine(outputRoot, "spawn-baseline.json"), tree.SpawnBaselineBytes)
            && FileMatches(Path.Combine(outputRoot, "recipes.json"), tree.RecipesBytes);
    }

    static bool FileMatches(string path, byte[] expected)
        => File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(expected);

    /// <summary>
    /// A DB-free self-consistency check: re-hashes the four payload files already on disk and
    /// compares against what <c>_manifest.json</c> declares. This is what CI runs instead of
    /// <see cref="MatchesDisk"/>'s full <c>--check</c> — decisions.md rules out a real game/Harmony
    /// (and therefore a populated <c>hot.sqlite</c>) in CI, so there is no live database to
    /// regenerate against there. This does not prove the committed dump still matches the game —
    /// only that nobody hand-edited or partially merged it since the last real run. Proving it
    /// matches the game is a local, owner-run step (spec-corpus-dump.md's own `--check`).
    /// </summary>
    public static (bool Ok, string Reason) VerifyCommittedTree(string outputRoot)
    {
        var manifestPath = Path.Combine(outputRoot, "_manifest.json");
        if (!File.Exists(manifestPath)) return (false, $"no _manifest.json under {outputRoot}");

        JsonNode? manifestNode;
        try { manifestNode = JsonNode.Parse(File.ReadAllText(manifestPath)); }
        catch (JsonException ex) { return (false, $"_manifest.json did not parse: {ex.Message}"); }
        if (manifestNode is not JsonObject manifestObj)
            return (false, "_manifest.json is not a JSON object");

        var declaredHash = (string?)manifestObj["contentHash"];
        if (string.IsNullOrEmpty(declaredHash)) return (false, "_manifest.json has no contentHash");

        var plantPath = Path.Combine(outputRoot, "almanac", "plant.json");
        var zombiePath = Path.Combine(outputRoot, "almanac", "zombie.json");
        var baselinePath = Path.Combine(outputRoot, "spawn-baseline.json");
        var recipesPath = Path.Combine(outputRoot, "recipes.json");
        foreach (var p in new[] { plantPath, zombiePath, baselinePath, recipesPath })
            if (!File.Exists(p)) return (false, $"missing payload file: {p}");

        var recomputed = ComputeContentHash(
            File.ReadAllBytes(plantPath), File.ReadAllBytes(zombiePath),
            File.ReadAllBytes(baselinePath), File.ReadAllBytes(recipesPath));

        if (!string.Equals(recomputed, declaredHash, StringComparison.Ordinal))
            return (false, $"hash mismatch: manifest declares {declaredHash}, files on disk hash to {recomputed}");

        int CountArray(string path)
        {
            var node = JsonNode.Parse(File.ReadAllText(path));
            return node is JsonArray arr ? arr.Count : -1;
        }

        var declaredPlant = (int?)manifestObj["plantCount"] ?? -1;
        var declaredZombie = (int?)manifestObj["zombieCount"] ?? -1;
        var declaredBaseline = (int?)manifestObj["baselineCount"] ?? -1;
        var declaredRecipe = (int?)manifestObj["recipeCount"] ?? -1;

        if (CountArray(plantPath) != declaredPlant) return (false, "plantCount does not match almanac/plant.json's array length");
        if (CountArray(zombiePath) != declaredZombie) return (false, "zombieCount does not match almanac/zombie.json's array length");
        if (CountArray(baselinePath) != declaredBaseline) return (false, "baselineCount does not match spawn-baseline.json's array length");
        if (CountArray(recipesPath) != declaredRecipe) return (false, "recipeCount does not match recipes.json's array length");

        return (true, $"hash {declaredHash} — plant={declaredPlant} zombie={declaredZombie} baselines={declaredBaseline} recipes={declaredRecipe}");
    }
}

public sealed record DumpTree(
    byte[] ManifestBytes,
    byte[] PlantAlmanacBytes,
    byte[] ZombieAlmanacBytes,
    byte[] SpawnBaselineBytes,
    byte[] RecipesBytes,
    DumpManifest Manifest);
