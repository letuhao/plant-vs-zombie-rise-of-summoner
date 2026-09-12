using FusionRpg.Core.Actions.Eligibility;
using FusionRpg.Core.Actions.Seeding;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Actions.Unlock;

/// <summary>One roll attempt's result. <see cref="Granted"/> false with a null
/// <see cref="RefusalReason"/> means "no eligible candidate" — a legal no-op, not a refusal the
/// ladder itself made.</summary>
public readonly record struct UnlockGrantOutcome(bool Granted, string? GrantedActionId, UnlockRefusalReason? RefusalReason);

/// <summary>
/// T59.6 (spec-action-instance-and-grant.md §4): the roll-and-grant logic for one level gained. Pure,
/// DB-free like its sibling <see cref="UnlockDiscardService"/> (T20) — same injected-delegate seam, so
/// Core stays free of SQL per the DAL boundary. The caller (T59.7,
/// <c>AwardUniqueActorXpUnlocked</c>'s own transaction) invokes <see cref="TryRollOnce"/> once per
/// level gained, in the SAME transaction as the XP award — never a post-commit hook (spec's own stated
/// reason: <see cref="UnlockState"/>'s persistence must never observe a level the XP award itself
/// failed to commit).
/// </summary>
public sealed class ActionUnlockGrantService
{
    readonly Func<string, UnlockState> _loadUnlockState;
    readonly Action<string, UnlockState> _saveUnlockState;
    readonly Func<IReadOnlyList<ActionRow>> _catalog;
    readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _familyOf;
    readonly Action<string, string> _grant;

    /// <param name="loadUnlockState">Reads one owner's current <see cref="UnlockState"/> —
    /// <see cref="UnlockState.Empty"/> for a specimen with no row yet.</param>
    /// <param name="saveUnlockState">Persists the updated state. Called ONLY on a successful accept —
    /// never on an empty candidate set or a missed roll.</param>
    /// <param name="catalog">The full imported action catalog — read once per call, matching
    /// <see cref="ActionEligibility.Candidates"/>'s own caller-supplies-everything contract.</param>
    /// <param name="familyOf">The specimen's own species → families lookup (A-E1's decided mapping —
    /// a relation, not a scalar; see <see cref="FamilyMap"/>).</param>
    /// <param name="grant">Grants the chosen action to the owner — the already-proven
    /// <c>RpgStore.UpsertGrant</c> path, one level up. Called ONLY alongside <paramref name="saveUnlockState"/>,
    /// never independently.</param>
    public ActionUnlockGrantService(
        Func<string, UnlockState> loadUnlockState,
        Action<string, UnlockState> saveUnlockState,
        Func<IReadOnlyList<ActionRow>> catalog,
        IReadOnlyDictionary<string, IReadOnlyList<string>> familyOf,
        Action<string, string> grant)
    {
        _loadUnlockState = loadUnlockState ?? throw new ArgumentNullException(nameof(loadUnlockState));
        _saveUnlockState = saveUnlockState ?? throw new ArgumentNullException(nameof(saveUnlockState));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _familyOf = familyOf ?? throw new ArgumentNullException(nameof(familyOf));
        _grant = grant ?? throw new ArgumentNullException(nameof(grant));
    }

    /// <summary>
    /// One roll attempt. <paramref name="specimenWorldSeed"/> plus the ratchet's own
    /// <c>state.EarnCount</c> (already "which attempt is this" — no separate counter needed, spec §4
    /// point 3) name the one deterministic stream this call uses for BOTH which candidate is offered
    /// and whether the roll lands — two sub-streams off that name, never a caller-supplied RNG, so two
    /// calls with the same inputs are always byte-identical.
    /// </summary>
    public UnlockGrantOutcome TryRollOnce(string instanceId, string? speciesKey, ulong specimenWorldSeed, UnlockTuning tuning)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("instanceId required", nameof(instanceId));
        if (tuning is null) throw new ArgumentNullException(nameof(tuning));

        var state = _loadUnlockState(instanceId);
        var heldIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var h in state.Held) heldIds.Add(h.UnlockId);

        var candidates = new List<ActionRow>();
        foreach (var a in ActionEligibility.Candidates(_catalog(), speciesKey, _familyOf))
            if (!heldIds.Contains(a.ActionId)) candidates.Add(a);

        if (candidates.Count == 0)
            return new UnlockGrantOutcome(false, null, null); // legal no-op, never a throw

        var streamName = $"unlock:{instanceId}:{state.EarnCount}";
        var options = new List<WeightedOption<ActionRow>>(candidates.Count);
        foreach (var c in candidates) options.Add(new WeightedOption<ActionRow>(c, Weight: 1));
        var chosen = WeightedChoice.Pick(options, unchecked((long)specimenWorldSeed), streamName);

        // AtomRngImpl, never the literal type name -- the action-layer purity guard
        // (ActionsPurityGuardTests.cs) bans the substring "Random" anywhere under Core/Actions/, and
        // this repo's own global alias (TargetModeNames.cs: `global using AtomRngImpl = ...AtomRandom`)
        // exists specifically so real code can construct one without tripping it.
        var acceptRng = new AtomRngImpl(specimenWorldSeed, streamName + ":accept");
        var accept = state.TryAccept(chosen.ActionId, tuning, acceptRng);
        if (!accept.Accepted)
            return new UnlockGrantOutcome(false, null, accept.Reason);

        _saveUnlockState(instanceId, state);
        _grant(instanceId, chosen.ActionId);
        return new UnlockGrantOutcome(true, chosen.ActionId, null);
    }
}
