namespace FusionRpg.Core.Effects.Atoms;

/// <summary>One hashed column. <paramref name="IsJson"/> selects canonical JSON over raw text.</summary>
public readonly record struct ContentHashColumn(string Name, bool IsJson)
{
    public static ContentHashColumn Text(string name) => new(name, false);
    public static ContentHashColumn Json(string name) => new(name, true);
}

/// <summary>
/// One covered table and the columns hashed from it, in declared order.
///
/// <para>The column list is <b>explicit</b> rather than read from <c>PRAGMA table_info</c>. Reading it
/// from the database would make adding a column move every stamp silently — the exact accident the
/// versioned registry exists to prevent. Adding a column is a registry edit and a version bump.</para>
/// </summary>
public sealed record ContentHashTable(string TableName, IReadOnlyList<ContentHashColumn> Columns);

/// <summary>
/// Which tables the content hash covers, versioned (spec-content-hash.md, definitions §8).
///
/// <para><b>A registry, not a fixed list.</b> E9 adds <c>power_coefficient</c> and
/// <c>power_trigger_frequency</c>; E18 adds the element roster and both matrices — all after this
/// module ships. A fixed list would silently invalidate every stamp E11 made, and the refuse-on-
/// mismatch rule would turn that into a hard failure of the whole Checkpoint D corpus. An added table
/// is an explicit, attributable version bump instead.</para>
///
/// <para><b>Order is the table name, ordinal.</b> Not module registration order: reordering code
/// initialisation would then move the hash with no content change at all.</para>
/// </summary>
public static class ContentHashRegistry
{
    /// <summary>Bump when a table joins or leaves. E18 → 2, E9 → 3 (map §4 build positions 14, 15).</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Version 1: the tables E2, E4 and E5 actually created. Instances, bindings and
    /// <c>content_meta</c> are absent on purpose — content is hashed, player state is not, and
    /// <c>content_meta</c> holds the revision rather than any content.
    /// </summary>
    static readonly ContentHashTable[] V1 = Sorted(new[]
    {
        new ContentHashTable("effect_atom", new[]
        {
            ContentHashColumn.Text("atom_id"),
            ContentHashColumn.Text("kind_id"),
            ContentHashColumn.Text("family_id"),
            ContentHashColumn.Text("variant"),
            ContentHashColumn.Text("tier"),
            ContentHashColumn.Text("name"),
            ContentHashColumn.Json("when_json"),
            ContentHashColumn.Json("params_json"),
            ContentHashColumn.Json("tags_json"),
            ContentHashColumn.Json("power_json"),
            ContentHashColumn.Json("power_override_json"),
            ContentHashColumn.Text("power_note"),
            ContentHashColumn.Text("icd_key"),
            ContentHashColumn.Text("trigger_id"),
            ContentHashColumn.Text("enabled"),
            ContentHashColumn.Text("revision"),
        }),
        new ContentHashTable("effect_container", new[]
        {
            ContentHashColumn.Text("container_id"),
            ContentHashColumn.Text("container_kind"),
            ContentHashColumn.Text("slot"),
            ContentHashColumn.Text("rarity"),
            ContentHashColumn.Text("min_tier"),
            ContentHashColumn.Text("max_tier"),
            ContentHashColumn.Text("level_req"),
            ContentHashColumn.Text("pool_rolls"),
            ContentHashColumn.Json("tags_json"),
            ContentHashColumn.Text("enabled"),
            ContentHashColumn.Text("revision"),
        }),
        new ContentHashTable("effect_container_atom", new[]
        {
            ContentHashColumn.Text("container_id"),
            ContentHashColumn.Text("seq"),
            ContentHashColumn.Text("atom_id"),
            ContentHashColumn.Json("overrides_json"),
        }),
        new ContentHashTable("effect_container_pool", new[]
        {
            ContentHashColumn.Text("container_id"),
            ContentHashColumn.Text("atom_id"),
            ContentHashColumn.Text("weight"),
            ContentHashColumn.Text("group_key"),
        }),
        new ContentHashTable("effect_curve", new[]
        {
            ContentHashColumn.Text("curve_id"),
            ContentHashColumn.Text("input"),
            ContentHashColumn.Json("points_json"),
            ContentHashColumn.Text("revision"),
        }),
        new ContentHashTable("rarity", new[]
        {
            ContentHashColumn.Text("rarity_id"),
            ContentHashColumn.Text("ordinal"),
            ContentHashColumn.Text("pool_rolls"),
            ContentHashColumn.Text("min_tier"),
            ContentHashColumn.Text("max_tier"),
        }),
    });

    /// <summary>The covered set for a version, already in hash order.</summary>
    public static IReadOnlyList<ContentHashTable> For(int schemaVersion) => schemaVersion switch
    {
        1 => V1,
        _ => throw new ArgumentOutOfRangeException(nameof(schemaVersion),
            $"contentHashSchemaVersion {schemaVersion} is not a known registry version " +
            $"(latest is {CurrentSchemaVersion})"),
    };

    public static bool IsKnownVersion(int schemaVersion) => schemaVersion == 1;

    public static IReadOnlyList<ContentHashTable> Current => For(CurrentSchemaVersion);

    static ContentHashTable[] Sorted(ContentHashTable[] tables) =>
        tables.OrderBy(t => t.TableName, StringComparer.Ordinal).ToArray();
}
