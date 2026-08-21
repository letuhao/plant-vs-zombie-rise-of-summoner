using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Expeditions;
using Xunit;

namespace FusionRpg.Core.Tests.Expeditions;

/// <summary>
/// D3: ExpeditionResolver — pure chain+events. Per-tick derived RNG streams make recall
/// pro-rating trivially exact: elapsed ticks resolve identically whether or not the tail runs.
/// </summary>
public class ExpeditionResolverTests
{
    static List<BattleActorSetup> Squad(int n = 2, int level = 5) =>
        Enumerable.Range(0, n).Select(i => new BattleActorSetup
        {
            Key = $"squad:{i}",
            Side = "squad",
            SpeciesId = "test-species",
            TypeId = 10_001,
            Level = level,
            MaxHp = BattleRuleset.BaseHp(level),
            Atk = BattleRuleset.BaseAtk(level),
            Defense = BattleRuleset.BaseDefense(level)
        }).ToList();

    static string Hash(ExpeditionResolution resolution) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resolution))));

    [Fact]
    public void Same_inputs_resolve_identically()
    {
        var a = ExpeditionResolver.Resolve("hunt-8h", Squad(), 77, elapsedTicks: 8);
        var b = ExpeditionResolver.Resolve("hunt-8h", Squad(), 77, elapsedTicks: 8);
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
    }

    [Fact]
    public void Recall_pro_rates_exactly_at_tick_boundaries()
    {
        var full = ExpeditionResolver.Resolve("forage-4h", Squad(3), 123, elapsedTicks: 8);
        var partial = ExpeditionResolver.Resolve("forage-4h", Squad(3), 123, elapsedTicks: 3);

        Assert.Equal(3, partial.Ticks.Count);
        Assert.Equal(
            JsonSerializer.Serialize(full.Ticks.Take(3)),
            JsonSerializer.Serialize(partial.Ticks));
        Assert.True(partial.Battles.Count <= full.Battles.Count);
        Assert.All(partial.Battles, b => Assert.True(b.TickIndex <= 3));

        var zero = ExpeditionResolver.Resolve("forage-4h", Squad(3), 123, elapsedTicks: 0);
        Assert.Empty(zero.Ticks);
        Assert.Empty(zero.Battles);
        Assert.Equal(0, zero.Rewards.EventSouls);
    }

    [Fact]
    public void Battle_chain_matches_the_tier_and_boss_lands_last()
    {
        foreach (var tier in ExpeditionTierCatalog.All)
        {
            var r = ExpeditionResolver.Resolve(tier.TierId, Squad(tier.SquadSlots), 9, tier.TickCount);
            var expected = tier.BattleCount + (tier.HasBossWave ? 1 : 0);
            Assert.Equal(expected, r.Battles.Count);
            Assert.All(r.Battles, b => Assert.True(WaveCatalog.IsKnown(b.Setup.WaveId)));
            if (tier.HasBossWave)
            {
                var boss = r.Battles.Last();
                Assert.True(boss.Boss);
                Assert.Equal(tier.TickCount, boss.TickIndex);
            }

            // Distinct per-battle seeds — one battle's rolls never shift another's.
            Assert.Equal(r.Battles.Count, r.Battles.Select(b => b.BattleSeed).Distinct().Count());
        }
    }

    [Fact]
    public void Wild_joins_respect_the_pool_rules()
    {
        // Sweep many seeds: every wild candidate must be summop-poolable — never capture-only,
        // never legendary (spec-expeditions.md §Never).
        for (ulong seed = 0; seed < 40; seed++)
        {
            var r = ExpeditionResolver.Resolve("warpath-20h", Squad(5), seed, 10);
            foreach (var tick in r.Ticks.Where(t => t.Kind == ExpeditionTickKinds.WildDemonMet))
            {
                var species = DemonSpeciesCatalog.Get(tick.WildSpeciesId!);
                Assert.NotEqual(DemonAcquisition.CaptureOnly, species.Acquisition);
                Assert.NotEqual(DemonRarity.Legendary, species.BaseRarity);
            }

            foreach (var join in r.Rewards.WildJoins)
                Assert.True(DemonSpeciesCatalog.IsKnown(join.SpeciesId));
        }
    }

    [Fact]
    public void Souls_and_materials_are_bounded_and_validated()
    {
        var r = ExpeditionResolver.Resolve("warpath-20h", Squad(5), 4242, 10);
        Assert.True(r.Rewards.EventSouls >= 0);
        Assert.All(r.Rewards.Materials, m =>
        {
            Assert.True(DemonMaterialCatalog.IsKnown(m.MaterialId), m.MaterialId);
            Assert.True(m.Qty > 0);
        });
        Assert.True(r.Rewards.SpecimenXpPerBattleWon > 0);
    }

    [Fact]
    public void Injury_debuffs_the_squad_for_later_battles()
    {
        // Find a seed whose timeline has an injury tick before a later battle, then assert the
        // injured member carries a power debuff in that battle's setup.
        for (ulong seed = 0; seed < 200; seed++)
        {
            var r = ExpeditionResolver.Resolve("warpath-20h", Squad(5), seed, 10);
            var injury = r.Ticks.FirstOrDefault(t => t.Kind == ExpeditionTickKinds.Injury);
            if (injury == null) continue;
            var laterBattle = r.Battles.FirstOrDefault(b => b.TickIndex > injury.TickIndex);
            if (laterBattle == null) continue;

            var member = laterBattle.Setup.Squad.Single(s => s.Key == injury.InjuredKey);
            Assert.Contains(member.ChannelMods, m => m.Amount < 0);
            return; // proven
        }

        Assert.Fail("no seed in 0..199 produced an injury before a later battle — timeline math is off");
    }

    [Fact]
    public void Unknown_tier_and_empty_squad_reject()
    {
        Assert.Throws<ArgumentException>(() => ExpeditionResolver.Resolve("no-tier", Squad(), 1, 1));
        Assert.Throws<ArgumentException>(() =>
            ExpeditionResolver.Resolve("scout-30m", new List<BattleActorSetup>(), 1, 1));
    }

    // Re-blessed 2026-08-21 at battle RulesetVersion 2 (combat-unification): named
    // serialization-shape churn — BattleActorSetup gained InnateShield, so every embedded
    // plan changed bytes even though expedition MATH did not (per-tick streams unchanged;
    // Same_inputs/Recall tests prove the resolver itself byte-stable across the re-bless).
    const string ScoutHash = "8F8623FAA38826F082BC39DB8BC6B26F18A1B76EACD05E202E4A72861AE6624D";
    const string ForageHash = "50888892407AE206A2505FB8EB96DE428BD30F3C9083B69DC92593AA04D298E2";
    const string HuntHash = "A4445896ADE17DD6EDE328C82F073D5599D7CC1D89C0B839CD38A6DF699626AA";
    const string WarpathHash = "1261227BA73392697F5C66BD286BA81BC4EDC31B3C985CA5585DB55ACFB816FA";

    [Fact]
    public void Tier_goldens_are_locked()
    {
        var actual =
            $"scout:{Hash(ExpeditionResolver.Resolve("scout-30m", Squad(2), 1001, 6))}\n" +
            $"forage:{Hash(ExpeditionResolver.Resolve("forage-4h", Squad(3), 2002, 8))}\n" +
            $"hunt:{Hash(ExpeditionResolver.Resolve("hunt-8h", Squad(4), 3003, 8))}\n" +
            $"warpath:{Hash(ExpeditionResolver.Resolve("warpath-20h", Squad(5), 4004, 10))}";
        var expected = $"scout:{ScoutHash}\nforage:{ForageHash}\nhunt:{HuntHash}\nwarpath:{WarpathHash}";
        Assert.Equal(expected, actual);
    }
}
