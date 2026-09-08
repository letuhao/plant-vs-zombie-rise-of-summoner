using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Timeline;

namespace FusionRpg.Core.Delve.Battle;

/// <summary>
/// D2.13 — one <see cref="IIntentSource"/> for a raid: the steered party's actors read an
/// interactive source, every other actor (the raid's un-steered parties AND every wave enemy) reads
/// one shared automated source — "a `siege-ai`-class policy per un-steered party"
/// (spec-encounter-generator.md R9), one shared instance being the correct degenerate case of "per
/// party" when nothing distinguishes one un-steered party's policy from another's.
///
/// <para><b>Dispatches on a precomputed key set, never <c>IBattleView.SideOf</c>/party lookup.</b>
/// Mirrors <see cref="FusionRpg.Core.Battle.Siege.SiegeIntentSource"/>'s own corrected shape exactly, for the identical
/// reason its doc comment records: `BattleEngine.Resolve` builds its `IBattleView` internally, AFTER
/// it is called — no external caller of `Resolve` (this class's whole reason to exist) ever holds one
/// to construct this class with. Which actor keys belong to the steered party is known BEFORE the
/// battle starts, from the `BattleSetup.Squad` entries carrying that `PartyIndex` — so dispatch
/// reduces to the same plain, deterministic key-set lookup `SiegeIntentSource` already proved correct,
/// rather than re-attempting the live-view approach its own comment names as broken.</para>
///
/// <para><b>Both sources are caller-supplied, not built here</b> — the same deliberate scoping
/// `SiegeIntentSource`'s constructor already takes (its own doc comment: "requiring the caller to
/// supply the AI side keeps this wrapper itself small, correct and fully testable today"). A real
/// `siege-ai`-class policy `IIntentSource` is that module's own remaining, un-started work
/// (`AiScoring` is a scoring utility, not a complete `IIntentSource`, confirmed by reading
/// `SiegeAi.cs` — this file does not fabricate one to fill the gap).</para>
/// </summary>
public sealed class RaidIntentSource : IIntentSource
{
    readonly IIntentSource _steered;
    readonly IIntentSource _automated;
    readonly IReadOnlySet<string> _steeredKeys;

    public RaidIntentSource(IIntentSource steered, IIntentSource automated, IReadOnlySet<string> steeredKeys)
    {
        _steered = steered ?? throw new ArgumentNullException(nameof(steered));
        _automated = automated ?? throw new ArgumentNullException(nameof(automated));
        _steeredKeys = steeredKeys ?? throw new ArgumentNullException(nameof(steeredKeys));
    }

    public ActionIntent TryDeclare(string actorKey, long nowTick) =>
        _steeredKeys.Contains(actorKey) ? _steered.TryDeclare(actorKey, nowTick) : _automated.TryDeclare(actorKey, nowTick);

    /// <summary>The precomputed key set for one party — every `Squad` actor whose
    /// <see cref="BattleActorSetup.PartyIndex"/> equals <paramref name="partyIndex"/>. Built from the
    /// setup BEFORE `Resolve` runs, matching the class doc's own "known before the battle starts"
    /// claim; never re-derived from a live view mid-battle.</summary>
    public static IReadOnlySet<string> KeysForParty(BattleSetup setup, int partyIndex) =>
        setup.Squad.Where(a => a.PartyIndex == partyIndex).Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
}
