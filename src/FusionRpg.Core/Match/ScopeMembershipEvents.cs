namespace FusionRpg.Core.Match;

/// <summary>
/// The three transitions a buff/debuff scope's own-side WHO-value needs to react to
/// (buff-debuff-scope-ideal.md §4.1). Nothing else — this is not a general event bus.
/// </summary>
public enum ScopeMembershipTransition
{
    /// <summary>`UniqueBindingPhase.PendingSpawn -> Bound` (`MatchUniqueBindingsFacet.TryBindOnSpawn`).</summary>
    Bound,

    /// <summary>`UniqueBindingPhase.* -> Cleared` (`MatchUniqueBindingsFacet`'s `ClearInstance`).</summary>
    Cleared,

    /// <summary>
    /// `zombie.hypno`, either direction — T6's own new dispatch case in `MatchRuntime.cs`. Not raised
    /// from this file; `MindControlledNow` is the only field meaningful for this transition.
    /// </summary>
    MindControlToggled,
}

/// <summary>
/// Deliberately a struct, no allocation on the hot path — matches `UniqueBinding.Clone()`'s own
/// allocation discipline and this program's zero-allocation bar for anything in a per-tick path.
/// </summary>
public readonly record struct ScopeMembershipEvent(
    string Ptr,
    ScopeMembershipTransition Transition,
    bool? MindControlledNow = null);
