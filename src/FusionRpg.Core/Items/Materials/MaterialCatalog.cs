using FusionRpg.Core.Demons;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Items.Materials;

/// <summary>
/// The five spend classes (`salvage-craft` §1, item module 14). Closed: a sixth is an
/// <b>ask-first</b> boundary, and the question a lane must answer to earn one is "which of these five
/// questions is unanswerable for my spend?" — not "I want another currency".
/// </summary>
public enum MaterialClass
{
    /// <summary>"May I act at all?" — the flat fee. A ledger balance, not a material id, which is why
    /// <see cref="MaterialCatalog.All"/> is 27 and not 28.</summary>
    Souls = 0,

    /// <summary>"How good may it be?" — the rarity ceiling. `shard.{rung}`, ten ids, already shipped.</summary>
    Shard,

    /// <summary>"What is it made of?" — frame-locked, graded by item level. `substrate.{frame}.{grade}`.</summary>
    Substrate,

    /// <summary>"What flavour?" — element direction, no magnitude. `essence.{element}`, six ids, shipped.</summary>
    Essence,

    /// <summary>"What am I doing to it?" — make / improve / re-randomise. `catalyst.{verb}`.</summary>
    Catalyst,
}

public sealed class MaterialVocabularyRejection : Exception
{
    public MaterialVocabularyRejection(string message) : base(message) { }
}

/// <summary>
/// The 27-id closed cost vocabulary every other sink spends in (`salvage-craft` §1). Wraps
/// <see cref="DemonMaterialCatalog"/> for the sixteen ids that already ship rather than re-minting
/// them — the spec's own instruction, and the reason the four legacy shard ids keep resolving here
/// exactly as they do there.
///
/// <para>⛔ <b>Order matters and it is the SPEND order</b> (`salvage-craft` §"The spend transaction",
/// step 4): souls → shard → substrate → essence → catalyst. A partial failure then always fails at
/// the same point, so two logs of one refusal are byte-comparable. <see cref="MaterialClass"/>'s
/// member order IS that order, and <see cref="ClassRank"/> is what the store sorts on.</para>
/// </summary>
public static class MaterialCatalog
{
    /// <summary>The two frames a substrate can be made of. Not a third vocabulary —
    /// <see cref="ItemFrame"/> is the shipped enum and this is its lower-case id form.</summary>
    public static readonly IReadOnlyList<string> SubstrateFrames = new[] { "humanoid", "plant" };

    /// <summary>The four substrate grades, ordinal 1..4. Structural, not a balance list: a fifth
    /// grade is a new material id and a new authored row, never a tuning edit.</summary>
    public static readonly IReadOnlyList<string> SubstrateGrades = new[] { "crude", "sound", "fine", "prime" };

    /// <summary>The three catalyst verbs (§1 row 5). A fourth is an ask-first boundary.</summary>
    public static readonly IReadOnlyList<string> CatalystVerbs = new[] { "forge", "temper", "flux" };

    static IReadOnlyList<string>? _all;

    /// <summary>
    /// Every ISSUABLE material id, in class order: shard ×10, substrate ×8, essence ×6, catalyst ×3.
    /// Twenty-seven. Souls carry no id — they are a ledger balance (`rpg_soul_ledger`), which is the
    /// whole reason the count is 27 rather than 28.
    /// </summary>
    // Lazy for the same reason DemonMaterialCatalog is: it reads another catalog's statics, and a
    // static-initialiser cycle is a silent empty list rather than a throw.
    public static IReadOnlyList<string> All => _all ??= Build();

    static IReadOnlyList<string> Build()
    {
        var ids = new List<string>(27);

        // The sixteen shipped ids come from DemonMaterialCatalog, never re-derived here — the spec's
        // "reuse, not re-mint" line. Its own order is essence-then-shard; this list is class order.
        foreach (var rarity in DemonRarityLadder.All)
            ids.Add($"shard.{rarity.ToId()}");

        foreach (var frame in SubstrateFrames)
            for (var g = 0; g < SubstrateGrades.Count; g++)
                ids.Add($"substrate.{frame}.{SubstrateGrades[g]}");

        foreach (var element in ElementRoster.Concrete)
            ids.Add($"essence.{element.ToElementId()}");

        foreach (var verb in CatalystVerbs)
            ids.Add($"catalyst.{verb}");

        return ids;
    }

    static HashSet<string>? _issuable;
    static HashSet<string> Issuable => _issuable ??= new HashSet<string>(All, StringComparer.Ordinal);

    /// <summary>True for an id this build will mint. The four legacy shard ids are deliberately
    /// <b>false</b> here and <b>true</b> in <see cref="IsKnown"/> — a saved reference still resolves,
    /// but nothing new is ever created in the retired vocabulary.</summary>
    public static bool IsIssuable(string? materialId) => materialId != null && Issuable.Contains(materialId);

    /// <summary>
    /// True for an issuable id OR one of the four legacy shard ids (`shard.common` / `rare` / `epic` /
    /// `legendary`), which stay resolvable for one release so a stale client or a saved reference does
    /// not hard-fail (`spec-rarity-migration.md` §4 point 4). Delegates to
    /// <see cref="DemonMaterialCatalog.IsKnown"/> for those rather than re-listing them.
    /// </summary>
    public static bool IsKnown(string? materialId) =>
        IsIssuable(materialId) || (materialId != null && DemonMaterialCatalog.IsKnown(materialId));

    /// <summary>True only for the four retired band ids — resolvable, never minted, and never a legal
    /// cost line, because a recipe demanding one is a recipe nothing can ever pay.</summary>
    public static bool IsLegacyShardId(string? materialId) =>
        materialId != null &&
        materialId.StartsWith("shard.", StringComparison.Ordinal) &&
        LegacyDemonRarityIds.IsLegacyId(materialId.Substring("shard.".Length));

    /// <summary>
    /// Which class an id belongs to. Throws on anything outside the closed vocabulary — including a
    /// <b>source-tagged</b> id such as <c>essence.fire.pvz</c>, which the Boundaries list forbids
    /// outright: the injector enriches, it never gates (SC8), so a PvZ-exclusive material id would
    /// make the lawn a required source for a web operation.
    /// </summary>
    public static MaterialClass ClassOf(string materialId)
    {
        if (string.IsNullOrWhiteSpace(materialId))
            throw new MaterialVocabularyRejection("material id is empty");

        var dots = materialId.Count(c => c == '.');

        if (materialId.StartsWith("shard.", StringComparison.Ordinal) && dots == 1 && IsKnown(materialId))
            return MaterialClass.Shard;

        if (materialId.StartsWith("substrate.", StringComparison.Ordinal) && dots == 2 && IsIssuable(materialId))
            return MaterialClass.Substrate;

        if (materialId.StartsWith("essence.", StringComparison.Ordinal) && dots == 1 && IsIssuable(materialId))
            return MaterialClass.Essence;

        if (materialId.StartsWith("catalyst.", StringComparison.Ordinal) && dots == 1 && IsIssuable(materialId))
            return MaterialClass.Catalyst;

        throw new MaterialVocabularyRejection(
            $"material id '{materialId}' is not in the 27-id closed vocabulary " +
            "(a source-tagged id like 'essence.fire.pvz' is refused here by design — the injector enriches, it never gates)");
    }

    /// <summary>The fixed spend order, as a sort key. Souls first, catalyst last.</summary>
    public static int ClassRank(MaterialClass cls) => (int)cls;

    /// <summary>`substrate.{frame}.{grade}` for a frame id and an ordinal grade 1..4. Throws rather
    /// than clamping an out-of-range grade — a grade-5 request is a caller bug, and a silent clamp to
    /// prime would hand out the exact material the grade lock exists to protect.</summary>
    public static string SubstrateId(string frame, int grade)
    {
        if (!SubstrateFrames.Contains(frame, StringComparer.Ordinal))
            throw new MaterialVocabularyRejection($"substrate frame '{frame}' is not one of humanoid/plant");
        if (grade < 1 || grade > SubstrateGrades.Count)
            throw new MaterialVocabularyRejection($"substrate grade {grade} is outside 1..{SubstrateGrades.Count}");
        return $"substrate.{frame}.{SubstrateGrades[grade - 1]}";
    }

    /// <summary>The ordinal grade 1..4 carried by a substrate id, or 0 if it is not a substrate.</summary>
    public static int GradeOf(string materialId)
    {
        if (!materialId.StartsWith("substrate.", StringComparison.Ordinal)) return 0;
        var lastDot = materialId.LastIndexOf('.');
        if (lastDot < 0) return 0;
        var idx = SubstrateGrades.ToList().IndexOf(materialId.Substring(lastDot + 1));
        return idx < 0 ? 0 : idx + 1;
    }

    /// <summary>The frame id carried by a substrate id, or null.</summary>
    public static string? FrameOf(string materialId)
    {
        if (!materialId.StartsWith("substrate.", StringComparison.Ordinal)) return null;
        var parts = materialId.Split('.');
        return parts.Length == 3 && SubstrateFrames.Contains(parts[1], StringComparer.Ordinal) ? parts[1] : null;
    }

    public static string ShardId(DemonRarity rarity) => $"shard.{rarity.ToId()}";

    public static string EssenceId(string elementId) => $"essence.{elementId}";

    public static string CatalystId(string verb) => $"catalyst.{verb}";
}
