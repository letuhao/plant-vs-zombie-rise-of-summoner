using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>F4–F6: fusion schema, Retired filtering, and the ExecuteFusion transaction.</summary>
public class FusionStoreTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public FusionStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-fusion-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    static readonly FusionRpg.Core.Demons.DemonSpeciesDef CatalogSpecies =
        FusionRpg.Core.Demons.DemonSpeciesCatalog.All
            .First(s => s.Side == "zombie" && s.Acquisition != FusionRpg.Core.Demons.DemonAcquisition.CaptureOnly);

    string Mint(string? speciesId = null)
    {
        var species = speciesId == null
            ? CatalogSpecies
            : FusionRpg.Core.Demons.DemonSpeciesCatalog.Get(speciesId);
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

    [Fact]
    public void Profiles_carry_star_and_promoted_defaults()
    {
        Mint();
        var profile = _store.ListDemonRoster(1).Items.Single().Profile;
        Assert.Equal(0, profile.Star);
        Assert.False(profile.Promoted);
    }

    [Fact]
    public void Retired_specimens_leave_the_roster_but_keep_their_profile()
    {
        var keep = Mint();
        var gone = Mint();
        Assert.True(_store.TryRetireUniqueActor(gone).Ok);

        var roster = _store.ListDemonRoster(1);
        Assert.Equal(keep, roster.Items.Single().Profile.InstanceId);
        Assert.NotNull(_store.GetDemonProfile(gone)); // history survives (lineage rule)
    }

    // ---- F5: star-merge transaction ----

    void Bankroll()
    {
        _store.AwardSouls(1, 5000, "seed", "fusion-bank-" + Guid.NewGuid().ToString("N"));
        _store.AddDemonMaterials(1, new[]
        {
            ("shard." + CatalogSpecies.BaseRarity.ToId(), 10L),
            ("essence." + CatalogSpecies.ElementPrimary.ToElementId(), 10L)
        });
    }

    [Fact]
    public void Star_merge_consumes_sacrifices_and_raises_the_base()
    {
        Bankroll();
        var baseId = Mint();
        var sacrifices = new[] { Mint(), Mint() }; // star 1 costs 2 same-rarity sacrifices
        var balanceBefore = _store.GetSoulBalance(1).Balance;

        var (ok, reason, outcome) = _store.ExecuteFusion(1, "merge-1", new FusionRequest(
            FusionModes.StarMerge, baseId, sacrifices, null), seed: 42);

        Assert.True(ok, reason);
        Assert.False(outcome!.Replayed);
        Assert.Equal(1, outcome.Base!.Profile.Star);
        Assert.Equal(balanceBefore - 50, outcome.Balance.Balance);

        var roster = _store.ListDemonRoster(1);
        Assert.Single(roster.Items); // sacrifices retired
        Assert.Equal(1, roster.Items[0].Profile.Star);
        Assert.Equal(9, _store.ListDemonMaterials(1)
            .Single(m => m.MaterialId.StartsWith("shard.")).Qty);
        Assert.Contains(_store.ListDemonLineage(baseId), l => l.Event == "star-merge");
        Assert.Contains(_store.ListDemonLineage(sacrifices[0]), l => l.Event == "consumed-by");
    }

    /// <summary>G4: a Retired demon still holding a contract would be a slot nobody could reclaim.</summary>
    [Fact]
    public void Consumed_sacrifices_release_their_contract_slots()
    {
        Bankroll();
        var baseId = Mint();
        var sacrifices = new[] { Mint(), Mint() };
        Assert.Equal(3, _store.CountBoundContracts(1));

        var (ok, reason, _) = _store.ExecuteFusion(1, "merge-slots", new FusionRequest(
            FusionModes.StarMerge, baseId, sacrifices, null), seed: 7);
        Assert.True(ok, reason);

        Assert.Equal(1, _store.CountBoundContracts(1));   // only the surviving base holds a slot
        Assert.All(sacrifices, id => Assert.False(_store.GetContract(id)!.Bound));
        Assert.True(_store.GetContract(baseId)!.Bound);
    }

    [Fact]
    public void Merge_replay_returns_the_stored_outcome_without_respending()
    {
        Bankroll();
        var baseId = Mint();
        var sacrifices = new[] { Mint(), Mint() };
        Assert.True(_store.ExecuteFusion(1, "merge-replay", new FusionRequest(
            FusionModes.StarMerge, baseId, sacrifices, null), 42).Ok);
        var balanceAfter = _store.GetSoulBalance(1).Balance;

        var (ok, reason, outcome) = _store.ExecuteFusion(1, "merge-replay", new FusionRequest(
            FusionModes.StarMerge, baseId, sacrifices, null), 999);
        Assert.True(ok);
        Assert.Equal("replay", reason);
        Assert.True(outcome!.Replayed);
        Assert.Equal(1, outcome.Base!.Profile.Star); // not 2 — nothing re-ran
        Assert.Equal(balanceAfter, _store.GetSoulBalance(1).Balance);

        // Same correlation, different request: mismatch, nothing written.
        var mismatch = _store.ExecuteFusion(1, "merge-replay", new FusionRequest(
            FusionModes.StarMerge, sacrifices[0], new[] { baseId, baseId }, null), 1);
        Assert.False(mismatch.Ok);
        Assert.Equal("correlation.mismatch", mismatch.Reason);
    }

    [Fact]
    public void Merge_refusals_write_nothing()
    {
        Bankroll();
        var baseId = Mint();
        var locked = Mint();
        _store.SetDemonLocked(locked, true);
        var free = Mint();
        var balance = _store.GetSoulBalance(1).Balance;

        // Locked sacrifice refuses (the lock finally has teeth).
        var lockedTry = _store.ExecuteFusion(1, "m-locked", new FusionRequest(
            FusionModes.StarMerge, baseId, new[] { locked, free }, null), 1);
        Assert.False(lockedTry.Ok);
        Assert.Equal("sacrifice.locked", lockedTry.Reason);

        // Wrong sacrifice count refuses.
        Assert.Equal("sacrifices.count", _store.ExecuteFusion(1, "m-count", new FusionRequest(
            FusionModes.StarMerge, baseId, new[] { free }, null), 1).Reason);

        // Base cannot sacrifice itself.
        Assert.Equal("sacrifice.is-base", _store.ExecuteFusion(1, "m-self", new FusionRequest(
            FusionModes.StarMerge, baseId, new[] { baseId, free }, null), 1).Reason);

        Assert.Equal(balance, _store.GetSoulBalance(1).Balance);
        Assert.Equal(3, _store.ListDemonRoster(1).Items.Count); // nobody consumed
        Assert.All(_store.ListDemonRoster(1).Items, s => Assert.Equal(0, s.Profile.Star));
    }

    [Fact]
    public void Merge_without_materials_refuses_and_spends_no_souls()
    {
        _store.AwardSouls(1, 5000, "seed", "no-mats-bank");
        var baseId = Mint();
        var sacrifices = new[] { Mint(), Mint() };
        var balance = _store.GetSoulBalance(1).Balance;

        var result = _store.ExecuteFusion(1, "m-mats", new FusionRequest(
            FusionModes.StarMerge, baseId, sacrifices, null), 1);
        Assert.False(result.Ok);
        Assert.Equal("materials.insufficient", result.Reason);
        Assert.Equal(balance, _store.GetSoulBalance(1).Balance);
        Assert.Equal(3, _store.ListDemonRoster(1).Items.Count);
    }

    // ---- F6: recipe + promotion modes ----

    static readonly FusionRpg.Core.Demons.Fusion.DemonRecipeDef Recipe =
        FusionRpg.Core.Demons.Fusion.DemonRecipeCatalog.All
            .First(r => DemonSpeciesCatalog.Get(r.OutputSpeciesId).BaseRarity == DemonRarity.Rare);

    void BankrollFor(FusionRpg.Core.Demons.Fusion.FusionCost cost, string elementId)
    {
        _store.AwardSouls(1, 5000, "seed", "recipe-bank-" + Guid.NewGuid().ToString("N"));
        _store.AddDemonMaterials(1, new[]
        {
            ("shard." + cost.ShardRarity.ToId(), (long)cost.ShardCount * 5),
            ("essence." + elementId, (long)cost.EssenceCount * 5)
        });
    }

    [Fact]
    public void Recipe_fusion_mints_the_output_and_discovers_once()
    {
        var output = DemonSpeciesCatalog.Get(Recipe.OutputSpeciesId);
        BankrollFor(FusionRpg.Core.Demons.Fusion.FusionCostTable.Recipe(output.BaseRarity),
            output.ElementPrimary.ToElementId());
        var a = Mint(Recipe.InputSpeciesIdA);
        var b = Mint(Recipe.InputSpeciesIdB);
        var pick = _store.GetDemonProfile(a)!.TraitIds[0];

        var (ok, reason, outcome) = _store.ExecuteFusion(1, "recipe-1", new FusionRequest(
            FusionModes.Recipe, null, new[] { a, b }, pick), seed: 42);

        Assert.True(ok, reason);
        Assert.Equal(Recipe.OutputSpeciesId, outcome!.Minted!.Profile.SpeciesId);
        Assert.Equal("fusion", outcome.Minted.Profile.Origin);
        Assert.Equal(pick, outcome.Minted.Profile.TraitIds[0]);
        Assert.Equal(Recipe.RecipeId, outcome.RecipeId);
        Assert.True(outcome.NewlyDiscovered);
        // First-ever: the recipe bonus AND the species bonus (never-seen species) both pay.
        Assert.Equal(2L * SoulEarnPolicy.DiscoveryDelta(output.BaseRarity), outcome.DiscoverySouls);
        Assert.Contains(Recipe.RecipeId, _store.ListFusionDiscoveries(1));

        // Inputs consumed; only the newborn remains on the roster.
        var roster = _store.ListDemonRoster(1);
        Assert.Equal(outcome.Minted.Profile.InstanceId, roster.Items.Single().Profile.InstanceId);
        Assert.Contains(_store.ListDemonLineage(outcome.Minted.Profile.InstanceId),
            l => l.Event == "recipe-birth");

        // Second craft of the same recipe: no second discovery payout.
        var a2 = Mint(Recipe.InputSpeciesIdA);
        var b2 = Mint(Recipe.InputSpeciesIdB);
        var again = _store.ExecuteFusion(1, "recipe-2", new FusionRequest(
            FusionModes.Recipe, null, new[] { a2, b2 }, pick), 43);
        Assert.True(again.Ok);
        Assert.False(again.Outcome!.NewlyDiscovered);
        Assert.Equal(0, again.Outcome.DiscoverySouls);
    }

    [Fact]
    public void Unmatched_input_pair_refuses()
    {
        _store.AwardSouls(1, 5000, "seed", "pair-bank");
        var a = Mint(Recipe.InputSpeciesIdA);
        var b = Mint(Recipe.InputSpeciesIdA == CatalogSpecies.SpeciesId ? Recipe.InputSpeciesIdB : CatalogSpecies.SpeciesId);
        var result = _store.ExecuteFusion(1, "pair-1", new FusionRequest(
            FusionModes.Recipe, null, new[] { a, b }, _store.GetDemonProfile(a)!.TraitIds[0]), 1);
        Assert.False(result.Ok);
        Assert.Equal("recipe.unknown", result.Reason);
    }

    [Fact]
    public void Promotion_gates_on_max_stars_and_runs_once()
    {
        var common = DemonSpeciesCatalog.All.First(s =>
            s.BaseRarity == DemonRarity.Common && s.Acquisition != DemonAcquisition.CaptureOnly);
        _store.AwardSouls(1, 5000, "seed", "promo-bank");
        _store.AddDemonMaterials(1, new[]
        {
            ("shard." + DemonRarity.Common.ToId(), 20L),
            ("shard." + DemonRarity.Rare.ToId(), 10L), // promotion charges the NEW rarity's shards
            ("essence." + common.ElementPrimary.ToElementId(), 20L)
        });
        var baseId = Mint(common.SpeciesId);
        string MintCommon() => Mint(common.SpeciesId);

        // Not maxed yet.
        Assert.Equal("promotion.not-ready", _store.ExecuteFusion(1, "promo-early", new FusionRequest(
            FusionModes.Promotion, baseId, Array.Empty<string>(), null), 1).Reason);

        // Climb to the common cap (3★): 2+3+4 sacrifices.
        var corr = 0;
        for (var star = 1; star <= 3; star++)
        {
            var fuel = Enumerable.Range(0, star + 1).Select(_ => MintCommon()).ToArray();
            var merge = _store.ExecuteFusion(1, "promo-merge-" + corr++, new FusionRequest(
                FusionModes.StarMerge, baseId, fuel, null), 1);
            Assert.True(merge.Ok, merge.Reason);
        }

        var (ok, reason, outcome) = _store.ExecuteFusion(1, "promo-1", new FusionRequest(
            FusionModes.Promotion, baseId, Array.Empty<string>(), null), seed: 9);
        Assert.True(ok, reason);
        var profile = outcome!.Base!.Profile;
        Assert.Equal(DemonRarity.Rare.ToId(), profile.Rarity);
        Assert.True(profile.Promoted);
        Assert.Equal(0, profile.Star); // stars reset with the new, higher cap
        Assert.Equal(2, profile.TraitIds.Count); // rare = 2 slots, first trait kept
        Assert.Contains(_store.ListDemonLineage(baseId), l => l.Event == "promotion");

        // Once only.
        Assert.Equal("promotion.not-ready", _store.ExecuteFusion(1, "promo-again", new FusionRequest(
            FusionModes.Promotion, baseId, Array.Empty<string>(), null), 1).Reason);
    }

    // ---- regression locks (post-build /test pass) ----

    [Fact]
    public void Post_promotion_merges_demand_the_new_rarity_fuel()
    {
        var common = DemonSpeciesCatalog.All.First(s =>
            s.BaseRarity == DemonRarity.Common && s.Acquisition != DemonAcquisition.CaptureOnly);
        _store.AwardSouls(1, 9000, "seed", "postpromo-bank");
        _store.AddDemonMaterials(1, new[]
        {
            ("shard." + DemonRarity.Common.ToId(), 30L),
            ("shard." + DemonRarity.Rare.ToId(), 30L),
            ("essence." + common.ElementPrimary.ToElementId(), 30L)
        });
        var baseId = Mint(common.SpeciesId);
        var corr = 0;
        for (var star = 1; star <= 3; star++)
        {
            var fuel = Enumerable.Range(0, star + 1).Select(_ => Mint(common.SpeciesId)).ToArray();
            Assert.True(_store.ExecuteFusion(1, "pp-m-" + corr++, new FusionRequest(
                FusionModes.StarMerge, baseId, fuel, null), 1).Ok);
        }
        Assert.True(_store.ExecuteFusion(1, "pp-promo", new FusionRequest(
            FusionModes.Promotion, baseId, Array.Empty<string>(), null), 2).Ok);

        // The base is RARE now — common fuel must refuse; the demon outgrew its old band.
        var commonFuel = new[] { Mint(common.SpeciesId), Mint(common.SpeciesId) };
        Assert.Equal("sacrifice.rarity", _store.ExecuteFusion(1, "pp-wrongfuel", new FusionRequest(
            FusionModes.StarMerge, baseId, commonFuel, null), 3).Reason);
    }

    [Fact]
    public void Locked_base_may_merge_only_sacrifices_are_protected()
    {
        Bankroll();
        var baseId = Mint();
        _store.SetDemonLocked(baseId, true); // the player's favorite — locked AND evolvable
        var fuel = new[] { Mint(), Mint() };

        var (ok, reason, outcome) = _store.ExecuteFusion(1, "locked-base", new FusionRequest(
            FusionModes.StarMerge, baseId, fuel, null), 1);
        Assert.True(ok, reason);
        Assert.Equal(1, outcome!.Base!.Profile.Star);
        Assert.True(outcome.Base.Profile.Locked); // lock survives the merge
    }

    [Fact]
    public void Retired_specimens_refuse_as_base_and_as_fuel()
    {
        Bankroll();
        var baseId = Mint();
        var fuel = new[] { Mint(), Mint() };
        Assert.True(_store.ExecuteFusion(1, "ret-m1", new FusionRequest(
            FusionModes.StarMerge, baseId, fuel, null), 1).Ok);

        // The consumed fuel is Retired — dead demons fuel nothing and lead nothing.
        // (Base is at star 1 now, so the next merge wants 3 sacrifices — count must match
        // or the count check fires first, which is the designed validation order.)
        var fresh = Mint();
        Assert.Equal("sacrifice.phase", _store.ExecuteFusion(1, "ret-m2", new FusionRequest(
            FusionModes.StarMerge, baseId, new[] { fuel[0], fresh, Mint() }, null), 1).Reason);
        Assert.Equal("base.phase", _store.ExecuteFusion(1, "ret-m3", new FusionRequest(
            FusionModes.StarMerge, fuel[0], new[] { fresh, Mint() }, null), 1).Reason);
    }

    [Fact]
    public void Expedition_members_refuse_fusion_in_both_roles()
    {
        Bankroll();
        var away = Mint();
        var home = Mint();
        var fuel = new[] { Mint(), Mint() };
        Assert.True(_store.DispatchExpedition(1, "fus-exp", "scout-30m", new[] { away }, 1).Ok);

        Assert.Equal("base.on-expedition", _store.ExecuteFusion(1, "exp-b", new FusionRequest(
            FusionModes.StarMerge, away, fuel, null), 1).Reason);
        Assert.Equal("sacrifice.on-expedition", _store.ExecuteFusion(1, "exp-s", new FusionRequest(
            FusionModes.StarMerge, home, new[] { away, fuel[0] }, null), 1).Reason);
    }

    [Fact]
    public void Partial_material_decrement_rolls_back_on_refusal()
    {
        // Shards present, essences absent: the shard decrement happens first inside the tx,
        // then the essence spend fails — the refusal must roll the shards back too.
        _store.AwardSouls(1, 5000, "seed", "partial-bank");
        _store.AddDemonMaterials(1, new[] { ("shard." + CatalogSpecies.BaseRarity.ToId(), 5L) });
        var baseId = Mint();
        var fuel = new[] { Mint(), Mint() };

        var result = _store.ExecuteFusion(1, "partial-1", new FusionRequest(
            FusionModes.StarMerge, baseId, fuel, null), 1);
        Assert.False(result.Ok);
        Assert.Equal("materials.insufficient", result.Reason);
        Assert.Equal(5, _store.ListDemonMaterials(1)
            .Single(m => m.MaterialId.StartsWith("shard.")).Qty); // untouched
        Assert.Equal(3, _store.ListDemonRoster(1).Items.Count);
    }

    [Fact]
    public void Untrimmed_base_id_cannot_sacrifice_itself()
    {
        // 2026-08-21 review Important 1: " abc " as base + "abc" as sacrifice used to slip past
        // the is-base guard and consume the base into itself.
        Bankroll();
        var baseId = Mint();
        var free = Mint();
        var result = _store.ExecuteFusion(1, "trim-1", new FusionRequest(
            FusionModes.StarMerge, " " + baseId + " ", new[] { baseId, free }, null), 1);
        Assert.False(result.Ok);
        Assert.Equal("sacrifice.is-base", result.Reason);
        Assert.Equal(2, _store.ListDemonRoster(1).Items.Count); // nobody consumed
    }

    [Fact]
    public void Recipe_replay_reproduces_the_discovery_reveal()
    {
        // 2026-08-21 review Important 2: a client that lost the original response must still see
        // its discovery banner on retry.
        var output = DemonSpeciesCatalog.Get(Recipe.OutputSpeciesId);
        BankrollFor(FusionRpg.Core.Demons.Fusion.FusionCostTable.Recipe(output.BaseRarity),
            output.ElementPrimary.ToElementId());
        var a = Mint(Recipe.InputSpeciesIdA);
        var b = Mint(Recipe.InputSpeciesIdB);
        var pick = _store.GetDemonProfile(a)!.TraitIds[0];
        var request = new FusionRequest(FusionModes.Recipe, null, new[] { a, b }, pick);

        var first = _store.ExecuteFusion(1, "replay-disc", request, 42);
        Assert.True(first.Ok);
        Assert.True(first.Outcome!.NewlyDiscovered);
        var balance = _store.GetSoulBalance(1).Balance;

        var replay = _store.ExecuteFusion(1, "replay-disc", request, 99);
        Assert.True(replay.Ok);
        Assert.Equal("replay", replay.Reason);
        Assert.True(replay.Outcome!.NewlyDiscovered, "the stored discovery flag must replay");
        Assert.Equal(first.Outcome.DiscoverySouls, replay.Outcome.DiscoverySouls);
        Assert.Equal(balance, _store.GetSoulBalance(1).Balance); // and pay nothing again
    }

    [Fact]
    public void First_fusion_of_a_species_pays_the_species_discovery_bonus_once_ever()
    {
        // 2026-08-21 review S5: one discovery policy across acquisition paths. The recipe output
        // species has never been seen — fusion pays BOTH the recipe bonus and the species bonus,
        // and the shared species:{id} dedupe blocks any later path from paying it again.
        var output = DemonSpeciesCatalog.Get(Recipe.OutputSpeciesId);
        BankrollFor(FusionRpg.Core.Demons.Fusion.FusionCostTable.Recipe(output.BaseRarity),
            output.ElementPrimary.ToElementId());
        var a = Mint(Recipe.InputSpeciesIdA);
        var b = Mint(Recipe.InputSpeciesIdB);
        var pick = _store.GetDemonProfile(a)!.TraitIds[0];

        var result = _store.ExecuteFusion(1, "species-disc", new FusionRequest(
            FusionModes.Recipe, null, new[] { a, b }, pick), 42);
        Assert.True(result.Ok);
        Assert.Equal(2L * SoulEarnPolicy.DiscoveryDelta(output.BaseRarity),
            result.Outcome!.DiscoverySouls); // recipe:{id} + species:{id}

        // The ledger row exists in the same shape summons write (species ref, shared dedupe).
        var ledger = _store.ListSoulLedger(1, 100).Items;
        Assert.Single(ledger, l => l.RefKind == "species" && l.RefId == output.SpeciesId);
    }

    [Fact]
    public void Recipe_mode_refuses_a_stray_base_id()
    {
        Bankroll();
        var a = Mint(Recipe.InputSpeciesIdA);
        var b = Mint(Recipe.InputSpeciesIdB);
        Assert.Equal("base.unexpected", _store.ExecuteFusion(1, "stray-base", new FusionRequest(
            FusionModes.Recipe, Mint(), new[] { a, b }, "swift"), 1).Reason);
    }

    [Fact]
    public void Forced_mid_merge_failure_leaves_zero_rows()
    {
        Bankroll();
        var baseId = Mint();
        var sacrifices = new[] { Mint(), Mint() };
        var balance = _store.GetSoulBalance(1).Balance;
        var shards = _store.ListDemonMaterials(1).Single(m => m.MaterialId.StartsWith("shard.")).Qty;

        _store.FusionMidTestHook = () => throw new InvalidOperationException("forced");
        try
        {
            Assert.Throws<InvalidOperationException>(() => _store.ExecuteFusion(1, "m-crash",
                new FusionRequest(FusionModes.StarMerge, baseId, sacrifices, null), 1));
        }
        finally
        {
            _store.FusionMidTestHook = null;
        }

        Assert.Equal(balance, _store.GetSoulBalance(1).Balance);
        Assert.Equal(shards, _store.ListDemonMaterials(1).Single(m => m.MaterialId.StartsWith("shard.")).Qty);
        Assert.Equal(3, _store.ListDemonRoster(1).Items.Count);
        Assert.All(_store.ListDemonRoster(1).Items, s => Assert.Equal(0, s.Profile.Star));
        Assert.Empty(_store.ListDemonLineage(baseId));
        Assert.Null(_store.TryGetFusionLog(1, "m-crash"));
    }
}
