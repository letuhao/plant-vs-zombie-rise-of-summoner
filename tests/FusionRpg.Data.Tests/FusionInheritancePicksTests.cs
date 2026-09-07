using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Fusion;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// WAVE F2.4 (demon-standalone, 2026-09-07, `demon-mechanism-gaps-ideal.md` §3.4): `ExecuteFusion`
/// accepts, validates, and prices player-selected inheritance picks — the closing seam over F2.1
/// (`InstanceProducer.Compose` forced picks), F2.2 (`GetSpecimenMaterialisedRoll`), and F2.3
/// (`FusionCostTable.InheritPick`).
///
/// <para>Recipe fixture deliberately selects one whose OWN INPUTS are already at or above
/// <c>DemonRecipeCatalog.OutputEligibilityFloor</c> (Cultivated) — unlike the sibling
/// <c>FusionStoreTests.Recipe</c> (a Cultivated-output recipe, whose own inputs sit one rung BELOW
/// that floor per <c>InputPoolBelow</c>). <c>InheritCostByRarity</c> only covers Cultivated-and-above,
/// so a Cultivated-output recipe's inputs are never pick-eligible; this fixture picks a recipe one or
/// more rungs higher, whose inputs genuinely are.</para>
/// </summary>
public class FusionInheritancePicksTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public FusionInheritancePicksTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-inherit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    static readonly PowerTuning MaterialiseTuning = PowerTuning.Build(
        1, 1, 80_000, 0, 20, 680, 1000, 25000, 250, 1000, 5000, 5000, 25000);
    const int PinTheta = 20;

    static readonly DemonRecipeDef Recipe = DemonRecipeCatalog.All
        .First(r =>
            DemonRarityLadder.AtLeast(DemonSpeciesCatalog.Get(r.InputSpeciesIdA).BaseRarity, DemonRecipeCatalog.OutputEligibilityFloor)
            && DemonRarityLadder.AtLeast(DemonSpeciesCatalog.Get(r.InputSpeciesIdB).BaseRarity, DemonRecipeCatalog.OutputEligibilityFloor));
    static readonly DemonSpeciesDef Output = DemonSpeciesCatalog.Get(Recipe.OutputSpeciesId);

    string Mint(string speciesId)
    {
        var species = DemonSpeciesCatalog.Get(speciesId);
        var (specimen, _) = _store.MintDemon(1, new DemonMintSpec
        {
            SpeciesId = species.SpeciesId,
            Side = species.Side,
            GameTypeId = species.GameTypeId,
            Rarity = species.BaseRarity.ToId(),
            Variant = "normal",
            ElementPrimary = species.ElementPrimary.ToElementId(),
            ElementSecondary = species.ElementSecondary?.ToElementId(),
            TraitIds = new List<string> { species.TraitPool[0] },
            Origin = "summon"
        });
        return specimen.Actor.InstanceId;
    }

    /// <summary>A real POOLED species-passive container with roll budget — a forced pick needs a
    /// nonzero `SuffixRolls`/`PrefixRolls` budget to land in (F2.1's own "exceeds-roll-budget"
    /// refusal), unlike `SpecimenMaterialisedRollTests`'s fixed-atom-only fixture.</summary>
    void SeedPooledSpecies(string speciesId, out string atomId, int amount)
    {
        var familyId = "atom." + speciesId.Replace(".", "-") + "-passive";
        atomId = familyId + ".t1";
        var atomRes = _store.UpsertAtom(new AtomRow
        {
            AtomId = atomId, KindId = "stat.modify", FamilyId = familyId, Tier = 1,
            Name = atomId, ParamsJson = $$"""{"channel":"maxHp","op":"flat","amount":{{amount}}}""",
        });
        Assert.True(atomRes.IsOk, atomRes.ToString());
        var affixId = "affix." + speciesId + ".passive";
        var affixRes = _store.UpsertAffix(
            new AffixRow(affixId, AffixClass.Prefix, new[] { new AffixRefRow(1, atomId) }),
            _store.GetAtom);
        Assert.True(affixRes.IsOk, affixRes.ToString());
        var containerRes = _store.UpsertContainer(new ContainerRow
        {
            ContainerId = $"species-passive.{speciesId}",
            Kind = ContainerKind.SpeciesPassive,
            PrefixRolls = 1,
            Pool = new[] { new ContainerPoolRow(affixId, 100) },
        });
        Assert.True(containerRes.IsOk, containerRes.ToString());
    }

    void Bankroll(long souls)
    {
        var cost = FusionCostTable.Recipe(Output.BaseRarity);
        _store.AwardSouls(1, souls, "seed", "inherit-bank-" + Guid.NewGuid().ToString("N"));
        _store.AddDemonMaterials(1, new[]
        {
            ("shard." + cost.ShardRarity.ToId(), (long)cost.ShardCount * 5),
            ("essence." + Output.ElementPrimary.ToElementId(), (long)cost.EssenceCount * 5),
        });
    }

    /// <summary>Seeds species A's own pooled passive content, mints both sacrifices, materialises A's
    /// roll for player 1 (never B — B is only ever read through `GetSpecimenMaterialisedRoll`'s own
    /// null-safe path in tests that don't need it), and returns the sacrifice ids plus the atom id A
    /// actually rolled.</summary>
    (string a, string b, string atomId, string pick) SetUpValidSacrifices()
    {
        SeedPooledSpecies(Recipe.InputSpeciesIdA, out var atomId, 7);
        var a = Mint(Recipe.InputSpeciesIdA);
        var b = Mint(Recipe.InputSpeciesIdB);
        var materialise = _store.MaterialisePlayerSpecies(1, PinTheta, MaterialiseTuning);
        Assert.True(materialise.IsOk, materialise.Rejection.ToString());
        var pick = _store.GetDemonProfile(a)!.TraitIds[0];
        return (a, b, atomId, pick);
    }

    [Fact]
    public void A_valid_affordable_pick_set_succeeds_and_the_output_instance_carries_it_verbatim()
    {
        var (a, b, atomId, pick) = SetUpValidSacrifices();
        // Seeded AFTER MaterialisePlayerSpecies ran — this player has never owned the output
        // species, so a first-time forced pick is legal (the "already-materialised" gate is closed).
        SeedPooledSpecies(Output.SpeciesId, out _, 3);
        Bankroll(5000);

        var (ok, reason, outcome) = _store.ExecuteFusion(1, "inherit-1", new FusionRequest(
            FusionModes.Recipe, null, new[] { a, b }, pick,
            new[] { new FusionPick(a, atomId) }), seed: 7);

        Assert.True(ok, reason);
        var mintedInstanceId = outcome!.Minted!.Profile.InstanceId;
        Assert.Equal(Output.SpeciesId, outcome.Minted.Profile.SpeciesId);

        // The forced pick materialised into player_species for the output species (append-only,
        // first ever roll) and carries the picked atom verbatim.
        var speciesRow = _store.ListPlayerSpecies(1).Single(r => r.SpeciesId == Output.SpeciesId);
        var speciesInstance = _store.GetInstance(speciesRow.InstanceId)!;
        Assert.Contains(speciesInstance.Atoms, at => at.AtomId == atomId);
        // Plus the correctly-rolled remainder: PrefixRolls=1 total, 1 forced -> 0 remaining rolled,
        // so the instance carries EXACTLY the forced pick, nothing else.
        Assert.Single(speciesInstance.Atoms);
        _ = mintedInstanceId;
    }

    [Fact]
    public void A_pick_set_over_slotsByRaritys_cap_is_refused_before_spending_anything()
    {
        var (a, b, _, pick) = SetUpValidSacrifices();
        SeedPooledSpecies(Output.SpeciesId, out _, 3);
        Bankroll(5000);
        var balanceBefore = _store.GetSoulBalance(1).Balance;
        var slotCap = FusionRoller.SlotsFor(Output.BaseRarity);

        var overCap = Enumerable.Range(0, slotCap + 1)
            .Select(_ => new FusionPick(a, "atom.does-not-need-to-resolve"))
            .ToArray();
        var (ok, reason, _) = _store.ExecuteFusion(1, "inherit-overcap", new FusionRequest(
            FusionModes.Recipe, null, new[] { a, b }, pick, overCap), seed: 7);

        Assert.False(ok);
        Assert.Equal("picks.exceeds-slots", reason);
        Assert.Equal(balanceBefore, _store.GetSoulBalance(1).Balance);
        Assert.DoesNotContain(_store.ListPlayerSpecies(1), r => r.SpeciesId == Output.SpeciesId);
    }

    [Fact]
    public void An_unaffordable_pick_set_is_refused_before_spending_anything()
    {
        var (a, b, atomId, pick) = SetUpValidSacrifices();
        SeedPooledSpecies(Output.SpeciesId, out _, 3);
        // Enough for the base recipe cost alone, nowhere near enough once the pick's own inherit
        // cost is added on top.
        var baseCost = FusionCostTable.Recipe(Output.BaseRarity);
        _store.AwardSouls(1, baseCost.Souls, "seed", "inherit-tight");
        _store.AddDemonMaterials(1, new[]
        {
            ("shard." + baseCost.ShardRarity.ToId(), (long)baseCost.ShardCount * 5),
            ("essence." + Output.ElementPrimary.ToElementId(), (long)baseCost.EssenceCount * 5),
        });
        var balanceBefore = _store.GetSoulBalance(1).Balance;

        var (ok, reason, _) = _store.ExecuteFusion(1, "inherit-poor", new FusionRequest(
            FusionModes.Recipe, null, new[] { a, b }, pick,
            new[] { new FusionPick(a, atomId) }), seed: 7);

        Assert.False(ok);
        Assert.Equal("souls.insufficient", reason);
        Assert.Equal(balanceBefore, _store.GetSoulBalance(1).Balance);
        Assert.DoesNotContain(_store.ListPlayerSpecies(1), r => r.SpeciesId == Output.SpeciesId);
        // Sacrifices survive — a refused fusion consumes nothing (ExecuteFusion's own contract).
        Assert.Equal(FusionRpg.Contracts.UniqueActorPhases.Roster, _store.GetUniqueActor(a)!.Phase);
    }

    [Fact]
    public void A_pick_naming_an_atom_its_specimen_never_actually_rolled_is_rejected_by_name()
    {
        var (a, b, _, pick) = SetUpValidSacrifices();
        SeedPooledSpecies(Output.SpeciesId, out _, 3);
        Bankroll(5000);
        var balanceBefore = _store.GetSoulBalance(1).Balance;

        var (ok, reason, _) = _store.ExecuteFusion(1, "inherit-bad-atom", new FusionRequest(
            FusionModes.Recipe, null, new[] { a, b }, pick,
            new[] { new FusionPick(a, "atom.never-rolled-by-anything") }), seed: 7);

        Assert.False(ok);
        Assert.Equal("picks.atom-not-rolled", reason);
        Assert.Equal(balanceBefore, _store.GetSoulBalance(1).Balance);
        Assert.DoesNotContain(_store.ListPlayerSpecies(1), r => r.SpeciesId == Output.SpeciesId);
    }

    [Fact]
    public void A_pick_naming_an_instance_that_is_not_one_of_the_two_sacrifices_is_rejected_by_name()
    {
        var (a, b, atomId, pick) = SetUpValidSacrifices();
        SeedPooledSpecies(Output.SpeciesId, out _, 3);
        Bankroll(5000);

        var stranger = Mint(Recipe.InputSpeciesIdA);
        var (ok, reason, _) = _store.ExecuteFusion(1, "inherit-stranger", new FusionRequest(
            FusionModes.Recipe, null, new[] { a, b }, pick,
            new[] { new FusionPick(stranger, atomId) }), seed: 7);

        Assert.False(ok);
        Assert.Equal("picks.source-not-a-sacrifice", reason);
    }

    [Fact]
    public void A_pick_set_is_refused_when_the_player_already_owns_the_output_species()
    {
        var (a, b, atomId, pick) = SetUpValidSacrifices();
        SeedPooledSpecies(Output.SpeciesId, out _, 3);
        // Player already has a shared roll for the output species (e.g. from an earlier summon) —
        // picks can no longer be honored without silently re-rolling every other specimen's shared
        // instance out from under it.
        var alreadyMaterialise = _store.MaterialisePlayerSpecies(1, PinTheta, MaterialiseTuning);
        Assert.True(alreadyMaterialise.IsOk);
        Assert.Contains(_store.ListPlayerSpecies(1), r => r.SpeciesId == Output.SpeciesId);
        Bankroll(5000);

        var (ok, reason, _) = _store.ExecuteFusion(1, "inherit-already", new FusionRequest(
            FusionModes.Recipe, null, new[] { a, b }, pick,
            new[] { new FusionPick(a, atomId) }), seed: 7);

        Assert.False(ok);
        Assert.Equal("picks.already-materialised", reason);
    }

    [Fact]
    public void Zero_picks_reproduces_todays_exact_recipe_behavior()
    {
        var (a, b, _, pick) = SetUpValidSacrifices();
        Bankroll(5000);

        var (ok, reason, outcome) = _store.ExecuteFusion(1, "inherit-none", new FusionRequest(
            FusionModes.Recipe, null, new[] { a, b }, pick), seed: 7);

        Assert.True(ok, reason);
        Assert.Equal(Output.SpeciesId, outcome!.Minted!.Profile.SpeciesId);
        // No forced picks -> no player_species materialise attempt for the output species at all
        // (that job stays MaterialisePlayerSpecies's own, untouched by a picks-free fusion).
        Assert.DoesNotContain(_store.ListPlayerSpecies(1), r => r.SpeciesId == Output.SpeciesId);
    }
}
