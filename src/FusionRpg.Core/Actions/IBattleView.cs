using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Actions;

/// <summary>
/// T33 (spec-action-selection.md §4): the read seam fog of war will swap — "`IBattleView` from day
/// one, even while it returns everything." Every board/roster fact <see cref="StubIntentSource"/>
/// needs comes through here, never through a direct read of <c>BattleEngine</c>'s own actor list —
/// under fog, "nearest target" becomes "nearest KNOWN target", which is a change to every read the AI
/// makes, and this interface is what confines that change to one implementation later.
///
/// <para>Cost/cooldown/stance seams (<see cref="CooldownLedger"/>, <see cref="IStanceCheck"/>,
/// <see cref="IAffordabilityCheck"/>) are NOT part of this view — those are the shared services
/// <see cref="UsabilityEvaluator.Evaluate"/> already takes as their own parameters, and fog never
/// touches them; only board/roster visibility does.</para>
/// </summary>
public interface IBattleView
{
    /// <summary>Every currently-live actor's ptr, in the view's own listed order — the order
    /// <see cref="StubIntentSource"/> falls back to when no board exists (spec §6: "with coordinates
    /// absent, nearest is undefined... falls back to `SourceOrder`").</summary>
    IReadOnlyList<string> LiveActorKeys { get; }

    /// <summary>0 plant / 1 zombie / 2 bullet — the same <see cref="EntityFacts.Side"/> vocabulary,
    /// so "enemy" is <c>SideOf(other) != SideOf(self)</c> with no third comparison invented.</summary>
    int SideOf(string actorKey);

    /// <summary><c>null</c> when no board exists yet — the SAME sentinel
    /// <see cref="UsabilityEvaluator"/>'s own <c>casterPos</c>/<c>targetPos</c> already use, so range
    /// gates and "nearest" share one absence convention.</summary>
    GridPos? PositionOf(string actorKey);

    /// <summary>The narrow fact window gate 5's compiled predicate evaluates against.</summary>
    EntityFacts FactsOf(string actorKey);

    /// <summary>Every action this actor currently holds, already compiled (spec §5's own hot-loop
    /// discipline: "nothing parses JSON during battle" applies to the AI's reads too) — gate 1's
    /// "bound" set.</summary>
    IReadOnlyList<CompiledAction> HeldActionsOf(string actorKey);
}
