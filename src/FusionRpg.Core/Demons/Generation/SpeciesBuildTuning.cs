using System.Text.Json;

namespace FusionRpg.Core.Demons.Generation;

/// <summary>`redistribution-plan`'s own balance surface (`data/tuning/species-build.v1.json`,
/// tunables-ssot.md T1) — the parity band, the per-species lean range and its crowding sensitivity,
/// and the shape limits (`min`/`maxAptitudesPerSpecies`). Server-only host wiring, mirroring every
/// other generation-tool tuning file (`DemonShapeTuning`) — the injector never plans a build.</summary>
/// <summary><see cref="RespecBasePrice"/>/<see cref="RespecEscalationPermille"/>/
/// <see cref="RespecDecayDays"/> — species-build-todo.md T4.1, spec-species-respec.md's own decision
/// 15: the price rises with the respec COUNT on that species and decays over time (churn, not
/// investment; never species level — that was decision 9, withdrawn by audit finding A2). Added
/// beside this file's existing redistribution-plan keys per the spec's own instruction ("shared with
/// m4 — add the three respec keys beside them; do not rewrite the file"), not a second tuning file, so
/// <see cref="RespecPolicy"/> reads the same hub every other species-build consumer already reads.</summary>
public sealed record SpeciesBuildTuning(
    int SchemaVersion, int Version,
    long ParityFloorPermille, long ParityCeilingPermille,
    long LeanMinPermille, long LeanMaxPermille,
    long CrowdingFactor, long SecondarySharePermille,
    int MaxAptitudesPerSpecies, int MinAptitudesPerSpecies,
    long RespecBasePrice, long RespecEscalationPermille, int RespecDecayDays);

public sealed class SpeciesBuildTuningRejection : Exception
{
    public SpeciesBuildTuningRejection(string message) : base(message) { }
}

/// <summary>Server-only host wiring (spec-redistribution-plan.md's own ⛔ callout): the generation
/// tool (`tools/DemonBuildPlanGen`) reads `species-build.v1.json` directly by file path, exactly like
/// `DemonSpeciesGen` reads its own tuning files — this hub exists for any FUTURE runtime consumer
/// (e.g. `demon-type-allocation`, module 5) that needs the band/lean values live rather than baked
/// into the committed plan, following this repo's own every-tuning-file-gets-a-hub convention. The
/// injector never configures this — m6's design is explicit that it receives points, never the plan,
/// the level, or the budget rule.</summary>
public static class SpeciesBuildTuningHub
{
    static SpeciesBuildTuning? _tuning;

    public static void Configure(SpeciesBuildTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    public static SpeciesBuildTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "SpeciesBuildTuningHub.Configure(...) has not run. The redistribution-plan band/lean values " +
        "read data/tuning/species-build.v{n}.json (tunables-ssot.md T5) — there is no built-in default to fall back to.");
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class SpeciesBuildTuningLoader
{
    public static SpeciesBuildTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new SpeciesBuildTuningRejection("species build tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new SpeciesBuildTuningRejection($"species build tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            return new SpeciesBuildTuning(
                SchemaVersion: Int(root, "schemaVersion"),
                Version: Int(root, "version"),
                ParityFloorPermille: Long(root, "parityFloorPermille"),
                ParityCeilingPermille: Long(root, "parityCeilingPermille"),
                LeanMinPermille: Long(root, "leanMinPermille"),
                LeanMaxPermille: Long(root, "leanMaxPermille"),
                CrowdingFactor: Long(root, "crowdingFactor"),
                SecondarySharePermille: Long(root, "secondarySharePermille"),
                MaxAptitudesPerSpecies: Int(root, "maxAptitudesPerSpecies"),
                MinAptitudesPerSpecies: Int(root, "minAptitudesPerSpecies"),
                RespecBasePrice: Long(root, "respecBasePrice"),
                RespecEscalationPermille: Long(root, "respecEscalationPermille"),
                RespecDecayDays: Int(root, "respecDecayDays"));
        }
    }

    static int Int(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new SpeciesBuildTuningRejection($"species build tuning: missing or non-integer '{key}'");
        return v;
    }

    static long Long(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt64(out var v))
            throw new SpeciesBuildTuningRejection($"species build tuning: missing or non-integer '{key}'");
        return v;
    }
}
