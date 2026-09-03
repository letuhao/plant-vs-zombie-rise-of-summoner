using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Effects.Atoms.Generation;

/// <summary>
/// One affix-family entry (E43 input), reduced to the columns the primaryChannel/flatDerivedChannel
/// formula (bands.v1.json) actually reads. Everything else on the authored entry — roles, frames,
/// nameWords, tags, notes — is the item program's own authored surface, not this generator's business
/// (spec-family-expand.md §4: "reconcile and expand only").
/// </summary>
/// <param name="Channel">Raw `params.channel` as authored. May be empty (a kind with no primary
/// channel), a concrete channel (`"maxHp"`), or an element-typed template (`"combat.power.{variant}"`)
/// — the <c>{variant}</c> marker is never expanded into concrete channels here (W7.9).</param>
/// <param name="Op">Raw `params.op` as authored (`"Flat"`/`"Increased"`/`"More"`/...), or null when the
/// kind carries no op.</param>
/// <param name="SourceFile">The affix-family file's own base name (e.g. <c>"g-life.json"</c>) — carried
/// into every emitted row's <c>tags.generatedFrom</c> (spec §3.2).</param>
public sealed record FamilyEntryInput(
    string Id, string Name, string KindId, string Channel, string? Op, string PowerBand, string SourceFile);

/// <summary>
/// The generator-input balance surface (<c>data/seed/items/_tuning/tier-bands.v1.json</c>) — the ONLY
/// place a per-channel <c>sharePermille</c> is authored (bands.v1.json's own
/// <c>sharePermilleOwnership</c> note). A channel stem absent from <see cref="ChannelWeightPermille"/>
/// has no authored share and E43 refuses that family rather than guess one.
/// </summary>
public sealed record TierBandsInput(
    long BaseSharePermille,
    IReadOnlyDictionary<string, long> ChannelWeightPermille,
    IReadOnlyDictionary<string, long> OpWeightPermille);

/// <summary>One family E43 could not expand, with a reason an author can act on. Expected, reported
/// content — never a crash, and never silently dropped (spec §3.2 step 3).</summary>
public sealed record FamilyRefusal(string FamilyId, string Reason);

/// <summary>What one expansion pass produced: the rows it could emit, and every family it refused.</summary>
public sealed record FamilyExpansionResult(IReadOnlyList<AtomRow> Rows, IReadOnlyList<FamilyRefusal> Refusals);
