using FusionRpg.Core.Combat.Element;
using System.Text.Json;

namespace FusionRpg.Core.Effects.Atoms;

/// <summary>Which table a seed file's entries land in. The file says so; the directory is only a habit.</summary>
public enum SeedEntryKind
{
    Atom = 0,
    Container,
    Curve,
    Rarity,
    Element,
    ElementMatrix,
}

/// <summary>
/// One reason a seed file was refused, with enough location to fix it.
///
/// <para>The source path is carried because the expensive seed mistakes are cross-file — the same
/// <c>atom_id</c> authored twice, in two files, by two people. A reason without both paths sends the
/// author looking in one of them.</para>
/// </summary>
public sealed record SeedError(string SourcePath, string EntryId, AtomRejectionReason Reason, string Detail)
{
    public override string ToString() =>
        string.IsNullOrEmpty(EntryId)
            ? $"{SourcePath}: {Reason} — {Detail}"
            : $"{SourcePath} [{EntryId}]: {Reason} — {Detail}";
}

/// <summary>One authored curve, before it reaches the table.</summary>
public sealed record CurveSeed(string CurveId, CurveInput Input, IReadOnlyList<CurvePoint> Points);

/// <summary>
/// Everything one import wants to write, collected from every file and already checked for the
/// duplicates that only exist across files.
/// </summary>
public sealed class SeedContent
{
    public List<AtomRow> Atoms { get; } = new();
    public List<ContainerRow> Containers { get; } = new();
    public List<CurveSeed> Curves { get; } = new();
    public List<RarityRow> Rarities { get; } = new();

    /// <summary>The element roster (E18). Ordinals are explicit and append-only.</summary>
    public List<ElementRow> Elements { get; } = new();

    /// <summary>Matchup cells, tagged by which matrix they belong to — the two never share rows.</summary>
    public List<(string Matrix, ElementMatrixRow Row)> ElementMatrix { get; } = new();

    /// <summary>Entry id to the file that authored it — the other half of a duplicate report.</summary>
    public Dictionary<string, string> SourceOf { get; } = new(StringComparer.Ordinal);

    public int Count =>
        Atoms.Count + Containers.Count + Curves.Count + Rarities.Count
        + Elements.Count + ElementMatrix.Count;
}

/// <summary>What a collection pass produced, and everything wrong with it.</summary>
public sealed record SeedCollectResult(SeedContent Content, IReadOnlyList<SeedError> Errors)
{
    public bool IsOk => Errors.Count == 0;
}

/// <summary>
/// The authored seed format (E14a) — JSON files that diff, review and version like code, holding
/// content that lives in rows at runtime.
///
/// <para><b>JSON columns are stored canonically.</b> An authored <c>params</c> object is written to
/// <c>params_json</c> through <see cref="ContentHash.CanonicalJson"/>, not as the author typed it.
/// Storing the raw text would make re-indenting a seed file differ from the stored column, which
/// bumps <c>revision</c> — a hashed column — and so moves the content hash for an edit that changed
/// no content at all.</para>
///
/// <para><b>The kind comes from the file, not the folder.</b> <c>data/seed/atoms/</c> is a convention
/// for humans; a file that says <c>"kind": "atom"</c> is an atom file wherever it sits. Encoding the
/// layout twice means two places to be wrong.</para>
/// </summary>
public static class AtomSeedFile
{
    /// <summary>The only format version this importer reads. A newer file is refused, never guessed at.</summary>
    public const int SchemaVersion = 1;

    static readonly JsonDocumentOptions DocOptions = new() { CommentHandling = JsonCommentHandling.Skip };

    /// <summary>
    /// Read every file into one content set. Files are read in the order given; the caller sorts, so
    /// a duplicate names the same two paths on every machine.
    /// </summary>
    public static SeedCollectResult Collect(IEnumerable<(string Path, string Json)> files)
    {
        var content = new SeedContent();
        var errors = new List<SeedError>();

        foreach (var (path, json) in files)
            ReadInto(path, json, content, errors);

        return new SeedCollectResult(content, errors);
    }

    static void ReadInto(string path, string json, SeedContent into, List<SeedError> errors)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, DocOptions);
        }
        catch (JsonException ex)
        {
            errors.Add(new SeedError(path, "", AtomRejectionReason.BadParamValue, "not valid JSON: " + ex.Message));
            return;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new SeedError(path, "", AtomRejectionReason.BadParamValue,
                    $"a seed file is an object with 'kind' and 'entries', got {root.ValueKind}"));
                return;
            }

            // An unknown version is refused rather than read optimistically: a format that grew a
            // field would otherwise be imported with that field silently dropped.
            var version = Int(root, "schemaVersion", 0);
            if (version != SchemaVersion)
            {
                errors.Add(new SeedError(path, "", AtomRejectionReason.BadParamValue,
                    $"schemaVersion {version} — this importer reads {SchemaVersion}"));
                return;
            }

            if (!TryKind(Str(root, "kind"), out var kind))
            {
                errors.Add(new SeedError(path, "", AtomRejectionReason.UnknownKind,
                    $"kind '{Str(root, "kind")}' — one of "
                + "atom | container | curve | rarity | element | element-matrix"));
                return;
            }

            if (!root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                errors.Add(new SeedError(path, "", AtomRejectionReason.BadParamValue, "no 'entries' array"));
                return;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(new SeedError(path, "", AtomRejectionReason.BadParamValue,
                        $"an entry is an object, got {entry.ValueKind}"));
                    continue;
                }

                switch (kind)
                {
                    case SeedEntryKind.Atom: ReadAtom(path, entry, into, errors); break;
                    case SeedEntryKind.Container: ReadContainer(path, entry, into, errors); break;
                    case SeedEntryKind.Curve: ReadCurve(path, entry, into, errors); break;
                    case SeedEntryKind.Element: ReadElement(path, entry, into, errors); break;
                    case SeedEntryKind.ElementMatrix: ReadMatrixCell(path, entry, into, errors); break;
                    default: ReadRarity(path, entry, into, errors); break;
                }
            }
        }
    }

    // ---- entries -------------------------------------------------------------------------------

    static void ReadAtom(string path, JsonElement e, SeedContent into, List<SeedError> errors)
    {
        var family = Str(e, "family");
        var variant = Str(e, "variant");
        var tier = Int(e, "tier", 1);

        // The id is optional and derived when absent. When present it is kept as authored, not
        // rewritten: an id that disagrees with its own columns is E4's IdMismatch, and silently
        // preferring one side would import content nobody wrote.
        var id = Str(e, "id");
        if (id.Length == 0) id = AtomRow.DeriveId(family, variant, tier);

        if (!Claim(path, id, into, errors)) return;

        into.Atoms.Add(new AtomRow
        {
            AtomId = id,
            KindId = Str(e, "kind"),
            FamilyId = family,
            Variant = variant,
            Tier = tier,
            Name = Str(e, "name"),
            WhenJson = Json(e, "when", "{}"),
            ParamsJson = Json(e, "params", "{}"),
            TagsJson = Json(e, "tags", "{}"),
            PowerJson = JsonOrNull(e, "power"),
            PowerOverrideJson = JsonOrNull(e, "powerOverride"),
            PowerNote = StrOrNull(e, "powerNote"),
            IcdKey = StrOrNull(e, "icdKey"),
            Enabled = Bool(e, "enabled", true),
        });
    }

    static void ReadContainer(string path, JsonElement e, SeedContent into, List<SeedError> errors)
    {
        var id = Str(e, "id");
        if (!Claim(path, id, into, errors)) return;

        if (!TryContainerKind(Str(e, "kind"), out var kind))
        {
            errors.Add(new SeedError(path, id, AtomRejectionReason.UnknownKind,
                $"container kind '{Str(e, "kind")}' is not one of the six"));
            return;
        }

        var atoms = new List<ContainerAtomRow>();
        if (e.TryGetProperty("atoms", out var atomEls) && atomEls.ValueKind == JsonValueKind.Array)
        {
            var seq = 0;
            foreach (var a in atomEls.EnumerateArray())
            {
                if (a.ValueKind != JsonValueKind.Object) continue;
                // seq defaults to the authored order, which is what writing a list already means.
                atoms.Add(new ContainerAtomRow(Int(a, "seq", seq), Str(a, "atom"), JsonOrNull(a, "overrides")));
                seq++;
            }
        }

        var pool = new List<ContainerPoolRow>();
        if (e.TryGetProperty("pool", out var poolEls) && poolEls.ValueKind == JsonValueKind.Array)
            foreach (var p in poolEls.EnumerateArray())
            {
                if (p.ValueKind != JsonValueKind.Object) continue;
                pool.Add(new ContainerPoolRow(Str(p, "atom"), Int(p, "weight", 0), StrOrNull(p, "group")));
            }

        into.Containers.Add(new ContainerRow
        {
            ContainerId = id,
            Kind = kind,
            Slot = StrOrNull(e, "slot"),
            Rarity = StrOrNull(e, "rarity"),
            MinTier = IntOrNull(e, "minTier"),
            MaxTier = IntOrNull(e, "maxTier"),
            LevelReq = IntOrNull(e, "levelReq"),
            PoolRolls = Int(e, "poolRolls", 0),
            TagsJson = Json(e, "tags", "{}"),
            Enabled = Bool(e, "enabled", true),
            Atoms = atoms,
            Pool = pool,
        });
    }

    static void ReadCurve(string path, JsonElement e, SeedContent into, List<SeedError> errors)
    {
        var id = Str(e, "id");
        if (!Claim(path, id, into, errors)) return;

        var inputName = Str(e, "input");
        if (!Enum.TryParse<CurveInput>(inputName, ignoreCase: true, out var input)
            || !Enum.IsDefined(typeof(CurveInput), input))
        {
            errors.Add(new SeedError(path, id, AtomRejectionReason.BadCurve,
                $"input '{inputName}' — one of level | rarity | tier"));
            return;
        }

        var points = new List<CurvePoint>();
        if (e.TryGetProperty("points", out var pts) && pts.ValueKind == JsonValueKind.Array)
            foreach (var p in pts.EnumerateArray())
            {
                if (p.ValueKind != JsonValueKind.Object) continue;
                points.Add(new CurvePoint(Int(p, "x", 0), Int(p, "mult", 1000)));
            }

        into.Curves.Add(new CurveSeed(id, input, points));
    }

    static void ReadRarity(string path, JsonElement e, SeedContent into, List<SeedError> errors)
    {
        var id = Str(e, "id");
        if (!Claim(path, id, into, errors)) return;

        into.Rarities.Add(new RarityRow(
            id, Int(e, "ordinal", 0), Int(e, "poolRolls", 0), Int(e, "minTier", 1), Int(e, "maxTier", 1)));
    }

    static void ReadElement(string path, JsonElement e, SeedContent into, List<SeedError> errors)
    {
        var id = Str(e, "id");
        if (!Claim(path, id, into, errors)) return;

        // Absent is refused, never defaulted to 0: an ordinal is the element's identity in every
        // generated channel, and a silent 0 would collide with fire.
        if (IntOrNull(e, "ordinal") is not { } ordinal)
        {
            errors.Add(new SeedError(path, id, AtomRejectionReason.MissingParam,
                "an element needs an explicit ordinal — it names every channel generated from it"));
            return;
        }

        into.Elements.Add(new ElementRow(id, Str(e, "displayName"), ordinal, Bool(e, "enabled", true)));
    }

    /// <summary>
    /// One matchup cell. <c>matrix</c> chooses <c>combat</c> or <c>shield</c> — required, because a
    /// cell that guessed its matrix would silently rebalance the other one.
    /// </summary>
    static void ReadMatrixCell(string path, JsonElement e, SeedContent into, List<SeedError> errors)
    {
        var matrix = Str(e, "matrix").ToLowerInvariant();
        var attacker = Str(e, "attacker");
        var defender = Str(e, "defender");
        var cellId = $"{matrix}:{attacker}>{defender}";

        if (matrix is not ("combat" or "shield"))
        {
            errors.Add(new SeedError(path, cellId, AtomRejectionReason.BadParamValue,
                $"matrix '{matrix}' — one of combat | shield"));
            return;
        }

        if (attacker.Length == 0 || defender.Length == 0)
        {
            errors.Add(new SeedError(path, cellId, AtomRejectionReason.MissingParam,
                "a matchup cell needs both an attacker and a defender"));
            return;
        }

        if (!Claim(path, cellId, into, errors)) return;

        var unit = Int(e, "unit", 0);
        if (unit is < -1 or > 1)
        {
            errors.Add(new SeedError(path, cellId, AtomRejectionReason.BadParamValue,
                $"unit {unit} — one of -1 | 0 | 1; K is applied by the caller, once"));
            return;
        }

        into.ElementMatrix.Add((matrix, new ElementMatrixRow(attacker, defender, unit)));
    }

    /// <summary>
    /// Take the id, or report who already has it.
    ///
    /// <para>Last write wins would make the imported content depend on directory iteration order —
    /// the same files would produce a different content hash on a different filesystem.</para>
    /// </summary>
    static bool Claim(string path, string id, SeedContent into, List<SeedError> errors)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            errors.Add(new SeedError(path, "", AtomRejectionReason.BadParamValue, "entry has no id"));
            return false;
        }

        if (into.SourceOf.TryGetValue(id, out var first))
        {
            errors.Add(new SeedError(path, id, AtomRejectionReason.DuplicateKey,
                $"already authored in {first}"));
            return false;
        }

        into.SourceOf[id] = path;
        return true;
    }

    // ---- json helpers --------------------------------------------------------------------------

    static bool TryKind(string name, out SeedEntryKind kind)
    {
        switch (name.ToLowerInvariant())
        {
            case "atom": kind = SeedEntryKind.Atom; return true;
            case "container": kind = SeedEntryKind.Container; return true;
            case "curve": kind = SeedEntryKind.Curve; return true;
            case "rarity": kind = SeedEntryKind.Rarity; return true;
            case "element": kind = SeedEntryKind.Element; return true;
            case "element-matrix": kind = SeedEntryKind.ElementMatrix; return true;
            default: kind = SeedEntryKind.Atom; return false;
        }
    }

    static bool TryContainerKind(string name, out ContainerKind kind)
    {
        // `species-passive` and `world-buff` are the authored spellings (definitions §1); the enum
        // spells them in Pascal case. Separators are dropped rather than mapped one by one.
        var normalised = name.Replace("-", "").Replace("_", "");
        return Enum.TryParse(normalised, ignoreCase: true, out kind)
               && Enum.IsDefined(typeof(ContainerKind), kind);
    }

    static string Str(JsonElement o, string name) =>
        o.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() ?? "" : "";

    static string? StrOrNull(JsonElement o, string name)
    {
        var s = Str(o, name);
        return s.Length == 0 ? null : s;
    }

    static int Int(JsonElement o, string name, int fallback) =>
        o.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v)
            ? v : fallback;

    static int? IntOrNull(JsonElement o, string name) =>
        o.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v)
            ? v : null;

    static bool Bool(JsonElement o, string name, bool fallback)
    {
        if (!o.TryGetProperty(name, out var el)) return fallback;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback,
        };
    }

    /// <summary>A nested object, canonicalised — see the class remark on why the raw text is not stored.</summary>
    static string Json(JsonElement o, string name, string fallback) =>
        o.TryGetProperty(name, out var el) && el.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? ContentHash.CanonicalJson(el.GetRawText())
            : fallback;

    static string? JsonOrNull(JsonElement o, string name) =>
        o.TryGetProperty(name, out var el) && el.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? ContentHash.CanonicalJson(el.GetRawText())
            : null;
}
