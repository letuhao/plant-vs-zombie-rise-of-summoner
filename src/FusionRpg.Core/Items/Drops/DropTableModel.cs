using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Items.Drops;

/// <summary>
/// The closed entry-kind vocabulary.
///
/// <para>⚠ <b>Nine values, not seven.</b> spec-drop-volume.md's Data-shape table carries I12's
/// original seven (<c>equipment|material|currency|insert|charm|table|nothing</c>). The authoritative
/// seed-side contract — <c>entry-shapes.md</c> §9, and the 40-table corpus already shipped at
/// <c>data/seed/items/drop-tables/</c> — is a <b>nine</b>-value enum: wave R2 (2026-08-23) added
/// <c>unique</c> and <c>consumable</c> because the corpus held 144 uniques and 60 consumables that no
/// table could yield. <c>tools/ItemSeedValidator/Checks/DropTableCheck.cs</c> already enforces the
/// nine. The shipped contract wins over the spec's stale list.</para>
/// </summary>
public enum DropEntryKind
{
    Equipment,
    Material,
    Currency,
    Insert,
    Charm,
    Consumable,
    Unique,
    Table,
    Nothing,
}

/// <summary>
/// X4's channel, declared on the DROP-TABLE ENTRY and threaded through step 9 —
/// <b>never stored on the affix</b> (spec-affix-channel-weights.md: "the channel is a call-site
/// fact"; storing it would make an affix single-source and rebuild the problem one level down).
///
/// <para>⛔ Until X4 lands, this column is authored and <b>inert</b> — that is a WIRING GAP, not a
/// wall. A trash drop and a boss drop roll the same affixes today; the column is what makes closing
/// that a one-call change.</para>
/// </summary>
public static class AffixChannels
{
    public const string Drop = "drop";
    public const string Boss = "boss";

    /// <summary>A `boss` channel is a content-authoring fact, not a detected one — an "elite" is
    /// whatever the author marks, and there is no runtime heuristic to disagree with.</summary>
    public static readonly IReadOnlyList<string> All = new[] { Drop, Boss };

    public static bool IsKnown(string? channel) =>
        channel is not null && All.Contains(channel, StringComparer.Ordinal);
}

/// <summary>Who points at which table, and what level the content is (ssot-generation.md §5.1).</summary>
public sealed record LootSourceRow(
    string SourceKind,
    string SourceId,
    string TableId,
    int ContentLevel,
    string? FirstClearGrant = null)
{
    public string Key => SourceKind + ":" + SourceId;
}

/// <summary>One drop table. <c>SourceAllow</c> MUST contain `web` — standalone-first, enforced at import.</summary>
public sealed record DropTableRow(
    string TableId,
    IReadOnlyList<string> SourceAllow,
    int? MinIlvl,
    int? MaxIlvl,
    bool Enabled,
    long Revision,
    IReadOnlyList<DropTableGroupRow> Groups);

/// <summary>
/// An <b>independent</b> draw unit — the exact opposite of <c>effect_container_pool.group</c>, which
/// is an EXCLUSION unit. <see cref="Rolls"/> is the PRE-SCALE count step 5a multiplies by the Θ
/// volume scale.
/// </summary>
public sealed record DropTableGroupRow(
    string GroupKey,
    int Seq,
    int Rolls,
    IReadOnlyList<DropTableEntryRow> Entries);

/// <summary>
/// One typed entry. <c>RefId</c> means a different thing per <see cref="Kind"/> — a discriminated
/// union in a relational table, which only a validator can prove (that validator is
/// <see cref="DropTableValidator"/>, and it is the price the single-table design pays).
/// </summary>
public sealed record DropTableEntryRow(
    int Seq,
    DropEntryKind Kind,
    string RefId,
    int Weight,
    int MinCount = 1,
    int MaxCount = 1,
    int? MinIlvl = null,
    int? MaxIlvl = null,
    string? RarityFloor = null,
    IReadOnlyDictionary<int, int>? RarityWeightShift = null,
    bool Enabled = true,
    string AffixChannel = AffixChannels.Drop,
    string? Frame = null,
    string? Role = null);

/// <summary>One weighted draw's outcome — the entry that won, and the total it was drawn against.</summary>
public readonly record struct DropDrawResult(DropTableEntryRow? Entry, long TotalWeight);

/// <summary>
/// The weighted draw and the availability gate. Kept separate from
/// <see cref="LootPipeline"/> so the draw can be reasoned about — and re-run — on its own.
/// </summary>
public static class DropTableDraw
{
    static DropTableDraw() => ContentRuleNamespaces.Register("drop");

    /// <summary>
    /// Which entry kinds this build can actually RESOLVE to a payload. The rest are refused BY NAME
    /// at import — <c>ContentRuleViolated{drop.entry-kind-unavailable}</c> — never silently dropped
    /// and never quietly resolved to nothing.
    ///
    /// <para>Each unavailable kind names the module that lands it, so the refusal reads as a build
    /// order rather than a defect:</para>
    /// <list type="bullet">
    /// <item><c>insert</c> — X7's <c>gem</c> container kind, then module 16 `sockets`.</item>
    /// <item><c>charm</c> — X7's <c>charm</c> container kind, then module 13 `set-charm-gen`.</item>
    /// <item><c>consumable</c> — module 18; ssot-generation.md §5.4 makes its absence deliberate
    /// ("adding it now would ship a degenerate action mechanism the action program then has to absorb").</item>
    /// <item><c>unique</c> — module 17 `uniques`.</item>
    /// </list>
    ///
    /// <para>Verified 2026-09-04, not assumed: <c>ContainerKind</c> (`Effects/Atoms/ContainerRow.cs:7`)
    /// ships six values — Item, Trait, Skill, SpeciesPassive, Patron, WorldBuff — and none of D27's
    /// four (<c>gem</c>/<c>set</c>/<c>charm</c>/<c>combo</c>). X7 has not landed.</para>
    /// </summary>
    public static readonly IReadOnlyDictionary<DropEntryKind, string> UnavailableKinds =
        new Dictionary<DropEntryKind, string>
        {
            [DropEntryKind.Insert] = "X7 must land the 'gem' container_kind, then module 16 (sockets)",
            [DropEntryKind.Charm] = "X7 must land the 'charm' container_kind, then module 13 (set-charm-gen)",
            [DropEntryKind.Consumable] = "module 18 (consumables); ssot-generation.md §5.4 keeps it deliberately absent until the action layer exists",
            [DropEntryKind.Unique] = "module 17 (uniques)",
        };

    public static bool IsAvailable(DropEntryKind kind) => !UnavailableKinds.ContainsKey(kind);

    /// <summary>Kinds that name one specific thing and therefore require a <c>ref</c> (entry-shapes.md §9).</summary>
    public static bool NeedsRef(DropEntryKind kind) => kind switch
    {
        DropEntryKind.Equipment => false,
        DropEntryKind.Nothing => false,
        _ => true,
    };

    /// <summary>
    /// A row's effective weight at draw time. A disabled entry, or one whose ilvl band excludes the
    /// event, is treated as <c>weight = 0</c> — the shipped <c>effect_container_pool</c> precedent
    /// ("weight = 0 — row kept, never drawn"), never a delete and never a silent substitution.
    /// </summary>
    public static long EffectiveWeight(DropTableEntryRow e, int itemLevel)
    {
        if (!e.Enabled) return 0;
        if (e.MinIlvl is { } lo && itemLevel < lo) return 0;
        if (e.MaxIlvl is { } hi && itemLevel > hi) return 0;
        return e.Weight;
    }

    /// <summary>
    /// One weighted draw over a group's entries at a given item level. Integer-only and unbiased.
    ///
    /// <para>Returns <c>Entry = null</c> only when the whole group is unsatisfiable (every row at
    /// weight 0). The caller decides whether that is a fall-through to <c>nothing</c> or an
    /// <c>UnsatisfiablePool</c> — a group that WAS guaranteed and lost everything is a rejection, not
    /// a silent nothing, because "silently under-filling is the failure this program exists to
    /// remove".</para>
    /// </summary>
    public static DropDrawResult Draw(IReadOnlyList<DropTableEntryRow> entries, int itemLevel, IAtomRandom rng)
    {
        if (entries is null) throw new ArgumentNullException(nameof(entries));
        if (rng is null) throw new ArgumentNullException(nameof(rng));

        long total = 0;
        foreach (var e in entries)
            total = checked(total + EffectiveWeight(e, itemLevel));

        if (total <= 0) return new DropDrawResult(null, 0);
        if (total > int.MaxValue)
            throw new OverflowException(
                $"drop group weight total {total} exceeds the draw's integer range — overflow throws, it never wraps");

        // NextInclusive is [min, max]; the ladder walk below is the standard cumulative form.
        var roll = rng.NextInclusive(0, (int)(total - 1));
        long cursor = 0;
        foreach (var e in entries.OrderBy(e => e.Seq))
        {
            cursor = checked(cursor + EffectiveWeight(e, itemLevel));
            if (roll < cursor) return new DropDrawResult(e, total);
        }

        // Unreachable given the total above; a throw beats a silent last-entry fallback.
        throw new InvalidOperationException("drop group draw walked past its own cumulative weight");
    }

    /// <summary>
    /// The EXPECTED equipment yield of one table, in per-mille, at a given volume scale — no RNG.
    ///
    /// <para>This is what spec-drop-volume.md Correction 1's table asserts: at Θ = 20 (scale 1000‰)
    /// a normal web wave yields exactly 550‰ = 0.55 equipment items. Exact integer arithmetic
    /// throughout — a float here would make a calibration table that a balance pass reads as a
    /// contract into something that drifts in the last digit.</para>
    /// </summary>
    public static long ExpectedEquipmentPerMille(
        DropTableRow table, int itemLevel, long volumeScaleMilli,
        Func<string, DropTableRow?>? lookupTable = null, int depth = 0, int maxDepth = 3)
    {
        if (table is null) throw new ArgumentNullException(nameof(table));
        if (depth > maxDepth)
            throw new InvalidOperationException($"drop table '{table.TableId}' nests deeper than {maxDepth}");
        if (!table.Enabled) return 0;

        long total = 0;
        foreach (var g in table.Groups)
        {
            long groupTotal = 0;
            foreach (var e in g.Entries)
                groupTotal = checked(groupTotal + EffectiveWeight(e, itemLevel));
            if (groupTotal <= 0) continue;

            // Accumulate a NUMERATOR and divide at the end. Dividing per entry truncates once per
            // row, which turns a 24-way uniform slate authored at weight 1 into 984‰ instead of
            // 1000‰ — a calibration table a balance pass reads as a contract must not lose 1.6% to
            // integer rounding. Widen before multiplying; divide LAST.
            long numerator = 0;
            foreach (var e in g.Entries)
            {
                var w = EffectiveWeight(e, itemLevel);
                if (w == 0) continue;

                if (e.Kind == DropEntryKind.Equipment)
                {
                    numerator = checked(numerator + w * 1000);
                }
                else if (e.Kind == DropEntryKind.Table && lookupTable is not null)
                {
                    var nested = lookupTable(e.RefId);
                    if (nested is null) continue;

                    // A nested table draws its OWN authored rolls, unscaled. Compounding Θ once per
                    // nesting level would reintroduce exactly the quadratic shape D18 exists to
                    // refuse, and §5.3 property 5 is explicit that "nesting is for reuse, not for
                    // depth" — so the volume term applies at the top level only.
                    var nestedMilli = ExpectedEquipmentPerMille(nested, itemLevel, 1000, lookupTable, depth + 1, maxDepth);
                    numerator = checked(numerator + w * nestedMilli);
                }
            }

            // rolls‰ × P(equipment) — the two divisions are the group's own weight total and the
            // one per-mille normalisation, both taken once, at the end.
            var drawsMilli = DropVolume.ExpectedRollsMilli(g.Rolls, volumeScaleMilli);
            total = checked(total + checked(drawsMilli * numerator) / groupTotal / 1000);
        }

        return total;
    }
}
