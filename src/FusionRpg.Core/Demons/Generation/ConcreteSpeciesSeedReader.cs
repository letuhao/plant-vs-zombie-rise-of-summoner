using System.Text.Json;

namespace FusionRpg.Core.Demons.Generation;

/// <summary>
/// `catalog-runtime`'s Injector-side flip (`seed-to-concrete`, 2026-09-06) — the read half of what
/// <see cref="ConcreteSpeciesSerializer.Canonical"/> writes. Exists because the Injector has no SQL
/// access (`guard-dal.ps1`) and no `almanac_seed` table to join against, so it cannot go through
/// `RpgStore.BuildDemonSpeciesSnapshot()` the way the Server does — it needs a pure, Core-only path
/// from a committed <c>data/generated/demons/&lt;SpeciesId&gt;.json</c> file straight to a
/// <see cref="ConcreteSpecies"/>, mirroring how <see cref="Fusion.FusionRecipeSeedReader"/> mirrors
/// its own write-side counterpart.
///
/// <para><b>Every field <see cref="ConcreteSpeciesSerializer.Canonical"/> writes, read back exactly —
/// except <c>name</c>, which was never written in the first place.</b> <see cref="ConcreteSpecies.Name"/>'s
/// own doc comment says why: it is "looked up from <c>almanac_seed</c> by the caller," a database read
/// this module deliberately does not perform, and <see cref="ConcreteSpeciesSerializer.Canonical"/>'s
/// own field list confirms it — <c>"name"</c> is not one of the keys it serialises. A reader here
/// leaves <see cref="ConcreteSpecies.Name"/> <c>null</c>, exactly like a caller that "has not resolved
/// it yet" — <see cref="ConcreteSpeciesMapper.ToDemonSpeciesDef"/>'s own <c>Name ?? SpeciesId</c>
/// fallback (already the Server's own rule, unconditionally exercised on the Injector because it never
/// has anything else to prefer) is not a divergence invented here, it is the SAME rule always applied.
/// </para>
/// </summary>
public static class ConcreteSpeciesSeedReader
{
    public static ConcreteSpecies Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new ConcreteSpecies
        {
            SpeciesId = Str(root, "speciesId"),
            Rarity = Enum.Parse<DemonRarity>(Str(root, "rarity")),
            Theta = root.GetProperty("theta").GetInt32(),
            PTheta = root.GetProperty("pTheta").GetInt64(),
            AttackIntervalMs = root.GetProperty("attackIntervalMs").GetInt64(),
            AttackIntervalSource = Str(root, "attackIntervalSource"),
            RangeCells = root.GetProperty("rangeCells").GetInt64(),
            VariantCount = root.GetProperty("variantCount").GetInt32(),
            Side = Str(root, "side"),
            GameTypeId = root.GetProperty("gameTypeId").GetInt32(),
            ElementPrimary = Enum.Parse<Stats.Derived.ElementTypeId>(Str(root, "elementPrimary")),
            ElementSecondary = root.TryGetProperty("elementSecondary", out var es) && es.ValueKind == JsonValueKind.String
                ? Enum.Parse<Stats.Derived.ElementTypeId>(es.GetString()!)
                : null,
            DeployMode = Enum.Parse<DemonDeployMode>(Str(root, "deployMode")),
            Acquisition = Enum.Parse<DemonAcquisition>(Str(root, "acquisition")),
            Variants = StringArray(root, "variants"),
            TraitPool = StringArray(root, "traitPool"),
            // Name: deliberately absent — see the class doc above.
            Magnitudes = root.TryGetProperty("magnitudes", out var mags) && mags.ValueKind == JsonValueKind.Object
                ? mags.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt64(), StringComparer.Ordinal)
                : new Dictionary<string, long>(StringComparer.Ordinal),
        };
    }

    public static ConcreteSpecies ParseFile(string path) => Parse(File.ReadAllText(path));

    static string Str(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()!
            : throw new FormatException($"missing or non-string '{prop}'");

    static string[] StringArray(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Select(e => e.GetString() ?? "").ToArray()
            : Array.Empty<string>();
}

/// <summary>
/// `ConcreteSpecies` → `DemonSpeciesDef`, extracted 2026-09-06 from
/// <c>RpgStore.BuildDemonSpeciesSnapshot()</c>'s own inline mapping so the Server (SQL-sourced
/// <c>ConcreteSpecies</c> rows) and the Injector (<see cref="ConcreteSpeciesSeedReader"/>-sourced ones)
/// compute the identical roster from the identical shape, rather than two copies of the same eight
/// field assignments that could silently drift the way <c>AttackIntervalMs</c> itself once did (found
/// missing from the compiled roster on 2026-09-05, exactly this class of bug).
/// </summary>
public static class ConcreteSpeciesMapper
{
    public static DemonSpeciesDef ToDemonSpeciesDef(ConcreteSpecies s) => new()
    {
        SpeciesId = s.SpeciesId.Trim().ToLowerInvariant(),
        Name = s.Name ?? s.SpeciesId,
        Side = s.Side,
        GameTypeId = s.GameTypeId,
        // Independently numbered per side in the source game (BigWallNut/plant and
        // BlackTrainZombie/zombie both carry GameTypeId 255) — the same wide split
        // DemonSpeciesGenerator.cs already established, reproduced here rather than invented anew.
        DemonTypeId = DemonSpeciesCatalog.DemonTypeIdFloor + (s.Side == "plant" ? 50_000 : 0) + s.GameTypeId,
        ElementPrimary = s.ElementPrimary,
        ElementSecondary = s.ElementSecondary,
        BaseRarity = s.Rarity,
        DeployMode = s.DeployMode,
        Acquisition = s.Acquisition,
        Variants = s.Variants,
        // s.TraitPool (ConcreteSpecies) stays the anchor's own open LLM flavor text, untouched —
        // DemonTraitPoolCuration bridges to DemonTraitCatalog's closed gameplay vocabulary by
        // species id/rarity/gameTypeId instead of re-interpreting that flavor text. A direct
        // s.TraitPool passthrough was tried once already and SpeciesCatalogDiffTests caught the
        // vocabulary mismatch — see trait-pool-hardcoded-empty.
        TraitPool = DemonTraitPoolCuration.PickFor(s.SpeciesId, s.Rarity, s.GameTypeId),
        AttackIntervalMs = s.AttackIntervalMs,
        // demon-lawn-deploy T1.5: unlike TraitPool, a straight pass-through is correct here — Magnitudes
        // is already the closed, channel-shaped gameplay vocabulary (DerivedStatChannels ids), not open
        // anchor flavor text needing curation.
        Magnitudes = s.Magnitudes,
    };
}
