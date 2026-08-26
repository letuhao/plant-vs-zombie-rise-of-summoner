using System.Text.Json;
using System.Text.Json.Serialization;

namespace FusionRpg.Tools.CombatSim;

/// <summary>
/// The class system's whole balance surface, loaded from `tuning/aptitudes.v{n}.json`.
///
/// <para>Four blocks, and the split between them is the design:</para>
/// <list type="bullet">
///   <item><b>grant</b> — the point ECONOMY (how many points exist). <b>Free build: every point
///     costs one point</b>, so there is no price block — the player has no class to be outside of
///     (class-system-ideal.md §7a.3, withdrawn 2026-08-25).</item>
///   <item><b>read</b> — the two SCALE functions (PS-3). Tunable shape, not a hard-coded read.</item>
///   <item><b>familyRead</b> — the `unitClass` DECISION per family. A property of the CHANNEL
///     (what the formula compares it against), never of the edge that feeds it — which is why it
///     is one reviewable block rather than a flag repeated on 48 edges.</item>
///   <item><b>edges</b> — the DISTRIBUTION: channel ← aptitude, with a per-mille coefficient.</item>
/// </list>
///
/// <para><b>Every key is required.</b> A missing one is a load rejection naming it, never a built-in
/// default (tunables-ssot.md T5): a default is a number nobody chose that behaves like one somebody
/// did.</para>
/// </summary>
public sealed class AptitudeTuning
{
    public int SchemaVersion { get; set; }
    public int Version { get; set; }
    public GrantBlock? Grant { get; set; }
    public ReadBlock? Read { get; set; }
    public Dictionary<string, string>? FamilyRead { get; set; }
    public List<TunedEdge>? Edges { get; set; }

    public sealed class GrantBlock
    {
        public double? AptitudePointsPerTheta { get; set; }
        public double? SkillPointsPerTheta { get; set; }
    }

    public sealed class ReadBlock
    {
        public ContestRead? Contest { get; set; }
        public MagnitudeRead? Magnitude { get; set; }
    }

    public sealed class ContestRead
    {
        public double? SpanPoints { get; set; }
        public long? ShareExponentMilli { get; set; }
    }

    public sealed class MagnitudeRead
    {
        public long? ShareExponentMilli { get; set; }
    }

    /// <summary>An edge, or a <c>_group</c> divider. Dividers carry no channel and are dropped at
    /// load: the edge list is long enough that reading it without section headings is genuinely
    /// harder, and JSON has no comment the publisher tool would preserve.</summary>
    public sealed record TunedEdge(string? Channel, string? Source, long KMilli)
    {
        public bool IsDivider => string.IsNullOrWhiteSpace(Channel);
    }

    /// <summary>Real edges only — dividers and any future annotation row filtered out.</summary>
    public IEnumerable<TunedEdge> RealEdges => Edges!.Where(e => !e.IsDivider);

    /// <summary>Keys starting with <c>_</c> are prose for the reader, never data.</summary>
    static bool IsNote(string key) => key.StartsWith('_');

    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static AptitudeTuning Load(string nameOrPath)
    {
        var path = AptitudeModel.Resolve(nameOrPath, "tuning");
        var t = JsonSerializer.Deserialize<AptitudeTuning>(File.ReadAllText(path), Options)
                ?? throw new AptitudeTuningRejection($"{path}: empty");
        t.Validate(path);
        return t;
    }

    void Validate(string path)
    {
        void Need(bool ok, string key)
        {
            if (!ok) throw new AptitudeTuningRejection($"{path}: missing required key '{key}'");
        }

        Need(Grant?.AptitudePointsPerTheta is > 0, "grant.aptitudePointsPerTheta");
        Need(Grant?.SkillPointsPerTheta is >= 0, "grant.skillPointsPerTheta");
        Need(Read?.Contest?.SpanPoints is > 0, "read.contest.spanPoints");
        Need(Read?.Contest?.ShareExponentMilli is > 0, "read.contest.shareExponentMilli");
        Need(Read?.Magnitude?.ShareExponentMilli is > 0, "read.magnitude.shareExponentMilli");
        Need(FamilyRead is { Count: > 0 }, "familyRead");
        Need(Edges is { Count: > 0 }, "edges");

        // Every edge must have a read mode, and the only place it can come from is familyRead.
        // An unclassified channel is exactly the `unitClass: null` blocker this file exists to close,
        // so it rejects rather than picking one — a guessed scale is invisible and moves every number.
        var unclassified = RealEdges
            .Where(e => FamilyOf(e.Channel!) is null)
            .Select(e => e.Channel!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (unclassified.Count > 0)
            throw new AptitudeTuningRejection(
                $"{path}: familyRead has no entry for {string.Join(", ", unclassified)} — " +
                "the read mode is a property of the channel and cannot be inferred.");

        var sourceless = RealEdges.Where(e => string.IsNullOrWhiteSpace(e.Source)).Select(e => e.Channel!).ToList();
        if (sourceless.Count > 0)
            throw new AptitudeTuningRejection($"{path}: edge(s) with no source: {string.Join(", ", sourceless)}");
    }

    /// <summary>
    /// Which <c>familyRead</c> row governs a channel, or null if none does.
    ///
    /// <para><b>Exact match first, then strip one axis suffix.</b> `combat.parry.strength.omni` →
    /// `combat.parry.strength` (the element is the arena, never part of the scale class), and
    /// `resource.max.hp` → `resource.max`. But `move.range` and `progression.xpRate` carry NO axis,
    /// so blind stripping would look up `move` and `progression` and reject a channel that is
    /// perfectly well classified. Try the whole id before assuming it has a suffix.</para>
    /// </summary>
    public string? FamilyOf(string channel)
    {
        if (FamilyRead!.ContainsKey(channel)) return channel;
        var dot = channel.LastIndexOf('.');
        if (dot <= 0) return null;
        var stripped = channel[..dot];
        return FamilyRead.ContainsKey(stripped) ? stripped : null;
    }

    /// <summary>
    /// Materialize into the resolver the rest of the tool already drives. One resolver, so the
    /// analytic predictor and the simulator can never disagree about what a build's channels ARE —
    /// only about what happens to them.
    /// </summary>
    public AptitudeModel ToModel(string name)
    {
        var edges = RealEdges.Select(e => new AptitudeEdge(
            e.Channel!, e.Source!, e.KMilli / 1000.0,
            FamilyRead![FamilyOf(e.Channel!)!].Equals("magnitude", StringComparison.OrdinalIgnoreCase)
                ? ReadMode.Magnitude
                : ReadMode.Contest)).ToList();

        return new AptitudeModel
        {
            Name = name,
            Description = $"aptitudes.v{Version} — {edges.Count} edges, "
                          + $"contest span {Read!.Contest!.SpanPoints}, "
                          + $"gamma contest {Read.Contest.ShareExponentMilli / 1000.0:0.###} / "
                          + $"magnitude {Read.Magnitude!.ShareExponentMilli / 1000.0:0.###}",
            Edges = edges,
            ContestSpan = Read.Contest.SpanPoints!.Value,
            ContestShareExponent = Read.Contest.ShareExponentMilli!.Value / 1000.0,
            MagnitudeShareExponent = Read.Magnitude.ShareExponentMilli!.Value / 1000.0
        };
    }
}

public sealed class AptitudeTuningRejection : Exception
{
    public AptitudeTuningRejection(string message) : base(message) { }
}
