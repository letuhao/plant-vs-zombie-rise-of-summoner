namespace FusionRpg.Core.Delve.Domains;

/// <summary>
/// D4.15 (spec-domain-catalog.md §1) — one domain anchor's C# projection (`dungeon_domain`'s own
/// columns). `DangerBand` and `Entry` stay plain strings here, exactly like `QuestRow.Scope`/
/// `.CountBand`/`.RewardBand` already do in this same session's sibling module — `DomainCatalog.Load`
/// validates them, it does not transform this row into a second, enum-typed shape. Every other field
/// this module's own D4.15 acceptance line does not name (`layoutTemplateId`, `bossSpeciesRef`,
/// `retinueFamily`, `entranceHint`, `permadeathFromRung`) is still carried verbatim — `Load` refuses
/// none of them beyond the two claims D4.15 itself makes (a known `dangerBand`, a legal `entry`);
/// their own cross-references are `DomainPreflight`'s later job (D4.17, §2 rows 2-3).
/// </summary>
public sealed record DomainRow(
    string DomainId, string Name, string Flavor, string Theme, string Climate,
    string DangerBand, string Entry, string LayoutTemplateId, string BossSpeciesRef,
    string? RetinueFamily, string EntranceHint, string? PermadeathFromRung);
