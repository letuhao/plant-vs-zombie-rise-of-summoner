namespace FusionRpg.Core.Delve.Attrition;

/// <summary>Which roster row a member's `CloseDelve(Extracted)` settlement writes (spec §7).
/// `RecoverDelves` is meaningful only for <see cref="Recover"/> — zero for the other two, never a
/// separate nullable field, since a tunable recovery count is never legitimately zero itself
/// (`risk.downedRecoveryDelves`'s own starting shape is 2).</summary>
public enum SettlementOutcome { Roster, Recover, Retire }

/// <summary>One member's full extraction settlement: the roster outcome and the loyalty `won` bit,
/// decided together because a caller settling one member at `CloseDelve` needs both (spec §7, §9).</summary>
public sealed record MemberSettlement(SettlementOutcome Outcome, int RecoverDelves, bool Won);

/// <summary>
/// `delve-attrition` D2.21/D2.22 (spec-delve-attrition.md §7-§9) — the pure extraction-time decisions.
/// D2.21 builds the wipe check (§8); D2.22 builds <c>Decide</c>'s per-member Retire/Recover/Roster
/// call (§7, §9) in this same file.
/// </summary>
public static class ExtractionSettlement
{
    /// <summary>
    /// §7 + §9, verbatim. <paramref name="downedOnce"/> is the CALLER's own effective value — on a
    /// wipe (§8), the caller passes <c>true</c> for every member regardless of that member's own
    /// history, since "on a wipe, all of them" are treated as `downedOnce`; this function itself knows
    /// nothing about wipes, only the one flag. <paramref name="permadeathApplies"/> is
    /// <c>PermadeathGate.Applies(domain, rung, oath)</c>, already resolved by the caller — this
    /// function never touches `difficulty-ladder`'s own types, matching this module's established
    /// "read model owned elsewhere" shape (<see cref="HungerCharge"/>, <see cref="RestResolver"/>).
    /// <paramref name="afflicted"/> is whether the member's nerve stage is the TOP one right now
    /// (`NerveLadder.StageFor(...) == thresholds.Count - 1`), resolved by the caller for the same
    /// reason. <paramref name="extracted"/>/<paramref name="bossKilled"/>/
    /// <paramref name="routeAtLeastHalfCleared"/> are raid-level facts, identical for every member of
    /// the call.
    /// </summary>
    public static MemberSettlement Decide(
        bool downedOnce,
        bool permadeathApplies,
        int downedRecoveryDelves,
        bool afflicted,
        bool extracted,
        bool bossKilled,
        bool routeAtLeastHalfCleared)
    {
        var outcome = !downedOnce ? SettlementOutcome.Roster
            : permadeathApplies ? SettlementOutcome.Retire
            : SettlementOutcome.Recover;

        int recoverDelves;
        if (outcome == SettlementOutcome.Recover)
        {
            if (downedRecoveryDelves <= 0)
                throw new ArgumentOutOfRangeException(nameof(downedRecoveryDelves), downedRecoveryDelves, "a recovery count is never zero or negative");
            recoverDelves = downedRecoveryDelves;
        }
        else
        {
            recoverDelves = 0;
        }

        // §9, verbatim: "won when the raid extracted AND (the boss was killed OR the party cleared at
        // least half the rooms on its route) AND the member is not afflicted at extraction."
        var won = extracted && (bossKilled || routeAtLeastHalfCleared) && !afflicted;

        return new MemberSettlement(outcome, recoverDelves, won);
    }

    /// <summary>A party stands while one member has <c>hp &gt; 0</c> and is not <see cref="DelveMemberState.Downed"/>
    /// (spec §8, verbatim). A member missing the <c>"hp"</c> key is a caller defect, not a "not
    /// standing" case — <see cref="DelveMemberState.Pools"/> is documented as carrying all six ids.</summary>
    public static bool PartyStands(IReadOnlyList<DelveMemberState> party)
    {
        if (party is null) throw new ArgumentNullException(nameof(party));

        foreach (var member in party)
        {
            if (!member.Pools.TryGetValue("hp", out var hp))
                throw new ArgumentException($"member '{member.InstanceId}' has no 'hp' pool.", nameof(party));
            if (hp > 0 && !member.Downed)
                return true;
        }

        return false;
    }

    /// <summary>The raid is wiped when every one of its parties has no standing member (spec §8:
    /// "When every party of the raid has no standing member: CloseDelve(Wiped)"). A raid with zero
    /// parties is a caller defect, not vacuously wiped or vacuously not — reject it rather than guess.</summary>
    public static bool IsWiped(IReadOnlyList<IReadOnlyList<DelveMemberState>> parties)
    {
        if (parties is null) throw new ArgumentNullException(nameof(parties));
        if (parties.Count == 0) throw new ArgumentException("a raid has at least one party.", nameof(parties));

        foreach (var party in parties)
            if (PartyStands(party))
                return false;

        return true;
    }
}
