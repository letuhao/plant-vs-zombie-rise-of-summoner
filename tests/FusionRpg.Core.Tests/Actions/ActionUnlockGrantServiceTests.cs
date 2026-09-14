using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Unlock;
using Xunit;

namespace FusionRpg.Core.Tests.Actions;

/// <summary>
/// T59.6 (spec-action-instance-and-grant.md §4): pure roll-and-grant logic, mirroring
/// `UnlockDiscardTests.cs`'s own fake-delegate style. `PoisonAction`/`PoisonState` throw if ever
/// invoked — the mechanism the acceptance bar's "grant/persist are never called on a missed or
/// empty-pool roll" is proven with, not asserted from reading the code.
/// </summary>
public class ActionUnlockGrantServiceTests
{
    static readonly UnlockTuning AlwaysAccepts =
        new(P1Milli: 1000, DeltaMilli: 500, FloorMilli: 1, HeldCap: 10, RungCap: 10, DiscardTaxCoeffMilli: 100);

    static readonly UnlockTuning RarelyAccepts =
        new(P1Milli: 1, DeltaMilli: 500, FloorMilli: 1, HeldCap: 10, RungCap: 10, DiscardTaxCoeffMilli: 100);

    static ActionRow Row(string id, EligibilityScope scope = EligibilityScope.General, string? scopeKey = null) => new()
    {
        ActionId = id, Name = id, Kind = ActionKind.Skill, Scope = scope, ScopeKey = scopeKey, Rung = 1,
    };

    static void Poison(string a, string b) => throw new InvalidOperationException("grant must never be called here");
    static void PoisonSave(string a, UnlockState b) => throw new InvalidOperationException("saveUnlockState must never be called here");

    [Fact]
    public void AnEmptyCatalogIsALegalNoOpAndNeverCallsSaveOrGrant()
    {
        var service = new ActionUnlockGrantService(
            loadUnlockState: _ => UnlockState.Empty(),
            saveUnlockState: PoisonSave,
            catalog: () => Array.Empty<ActionRow>(),
            familyOf: new Dictionary<string, IReadOnlyList<string>>(),
            grant: Poison);

        var outcome = service.TryRollOnce("specimen-1", speciesKey: null, specimenWorldSeed: 42, AlwaysAccepts);

        Assert.False(outcome.Granted);
        Assert.Null(outcome.GrantedActionId);
        Assert.Null(outcome.RefusalReason);
    }

    [Fact]
    public void HoldingEveryCatalogActionIsALegalNoOpAndNeverCallsSaveOrGrant()
    {
        var catalog = new[] { Row("action.a"), Row("action.b") };
        var alreadyHeld = UnlockState.FromPersisted(2, new[] { new HeldUnlock("action.a", 1), new HeldUnlock("action.b", 2) });

        var service = new ActionUnlockGrantService(
            loadUnlockState: _ => alreadyHeld,
            saveUnlockState: PoisonSave,
            catalog: () => catalog,
            familyOf: new Dictionary<string, IReadOnlyList<string>>(),
            grant: Poison);

        var outcome = service.TryRollOnce("specimen-1", speciesKey: null, specimenWorldSeed: 42, AlwaysAccepts);

        Assert.False(outcome.Granted);
        Assert.Null(outcome.RefusalReason);
    }

    [Fact]
    public void ASuccessfulRollSavesTheUpdatedStateAndGrantsTheChosenAction()
    {
        var catalog = new[] { Row("action.only") };
        UnlockState? saved = null;
        string? grantedAction = null;
        string? grantedTo = null;

        var service = new ActionUnlockGrantService(
            loadUnlockState: _ => UnlockState.Empty(),
            saveUnlockState: (id, state) => saved = state,
            catalog: () => catalog,
            familyOf: new Dictionary<string, IReadOnlyList<string>>(),
            grant: (id, actionId) => { grantedTo = id; grantedAction = actionId; });

        var outcome = service.TryRollOnce("specimen-1", speciesKey: null, specimenWorldSeed: 42, AlwaysAccepts);

        Assert.True(outcome.Granted);
        Assert.Equal("action.only", outcome.GrantedActionId);
        Assert.NotNull(saved);
        Assert.Contains(saved!.Held, h => h.UnlockId == "action.only");
        Assert.Equal("specimen-1", grantedTo);
        Assert.Equal("action.only", grantedAction);
    }

    [Fact]
    public void TheSameInputsProduceTheSameOutcomeEveryTime()
    {
        var catalog = new[] { Row("action.a"), Row("action.b"), Row("action.c") };

        UnlockGrantOutcome Run() => new ActionUnlockGrantService(
            loadUnlockState: _ => UnlockState.Empty(),
            saveUnlockState: (_, _) => { },
            catalog: () => catalog,
            familyOf: new Dictionary<string, IReadOnlyList<string>>(),
            grant: (_, _) => { }).TryRollOnce("specimen-x", speciesKey: null, specimenWorldSeed: 777, AlwaysAccepts);

        var first = Run();
        var second = Run();

        Assert.Equal(first.Granted, second.Granted);
        Assert.Equal(first.GrantedActionId, second.GrantedActionId);
    }

    /// <summary>Acceptance bar: "a poison-delegate test proves grant/persist are never called on a
    /// missed... roll." `RarelyAccepts` (P1Milli=1, i.e. a 0.1% base chance) makes a miss the
    /// overwhelmingly likely outcome for almost any seed; scanning a bounded range of seeds finds a
    /// real one deterministically rather than asserting from the formula alone.</summary>
    [Fact]
    public void AMissedRollNeverCallsSaveOrGrant()
    {
        var catalog = new[] { Row("action.only") };
        var foundAMiss = false;

        for (ulong seed = 0; seed < 200 && !foundAMiss; seed++)
        {
            var service = new ActionUnlockGrantService(
                loadUnlockState: _ => UnlockState.Empty(),
                saveUnlockState: PoisonSave,
                catalog: () => catalog,
                familyOf: new Dictionary<string, IReadOnlyList<string>>(),
                grant: Poison);

            var outcome = service.TryRollOnce("specimen-1", speciesKey: null, specimenWorldSeed: seed, RarelyAccepts);
            if (!outcome.Granted && outcome.RefusalReason == UnlockRefusalReason.RollMissed)
                foundAMiss = true;
            // Any other outcome (a hit) would already have thrown via the poison delegates above if
            // save/grant fired incorrectly on a miss -- reaching here at all for a hit seed is fine,
            // it just does not yet prove the claim, so the loop keeps scanning.
        }

        Assert.True(foundAMiss, "no missed roll found in 200 seeds at a 0.1% base chance -- something is wrong with the chance formula or the seed derivation");
    }

    [Fact]
    public void CandidatesRespectEligibilityScopeNotJustTheHeldFilter()
    {
        var catalog = new[]
        {
            Row("action.general"),
            Row("action.other-family", EligibilityScope.Family, "other-family"),
        };
        string? granted = null;

        var service = new ActionUnlockGrantService(
            loadUnlockState: _ => UnlockState.Empty(),
            saveUnlockState: (_, _) => { },
            catalog: () => catalog,
            familyOf: new Dictionary<string, IReadOnlyList<string>> { ["species.mine"] = new[] { "my-family" } }, // does not map to "other-family"
            grant: (_, actionId) => granted = actionId);

        service.TryRollOnce("specimen-1", speciesKey: "species.mine", specimenWorldSeed: 1, AlwaysAccepts);

        Assert.Equal("action.general", granted); // the only actually-eligible candidate
    }
}
