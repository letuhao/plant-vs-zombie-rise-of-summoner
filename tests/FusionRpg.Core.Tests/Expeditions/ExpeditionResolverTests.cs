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
    //
    // Re-blessed 2026-08-24 at RulesetVersion 3 (T4.2, power-dial): Squad()'s actors sit at
    // level 5, away from the Theta=20 pin, so BaseHp/BaseAtk/BaseDefense legitimately moved with
    // bMilli 0->400 — expected magnitude movement, the same triage as BattleGoldenTests.cs.
    // Same_inputs_resolve_identically and the recall pro-rating tests stayed green unchanged,
    // confirming the resolver's OWN per-tick RNG logic did not move, only the embedded magnitudes.
    //
    // Re-blessed 2026-08-30 (aura-skill T12, Gate B): named serialization-shape churn again, the
    // SAME class of change as the 2026-08-21 InnateShield re-bless — BattleSetup gained
    // ActiveAuras (default empty, no behavior change for every existing caller including this one),
    // so every embedded plan's serialized JSON gained an "activeAuras":[] key and every hash moved.
    // Verified NOT a determinism break before re-blessing, not assumed: every other test in this
    // file (Same_inputs_resolve_identically, recall pro-rating) stayed green unchanged, confirming
    // the resolver's own math and RNG streams did not move — only the embedded BattleSetup's shape.
    //
    // Re-blessed 2026-08-31 in ONE step covering two roster changes made together: the species cap
    // removal (24 -> 84 species) and RarityForRank's legendary tier becoming proportional
    // (7 legendary instead of 2 at 84 species). Both feed WildBand, so they are one re-bless.
    //
    // Why the roster touches this golden at all — a DIFFERENT class from every re-bless
    // above — those were serialization shape or magnitude churn with the roster fixed. This one is
    // a genuine content change. WildBand (ExpeditionResolver.cs:231) picks wild enemies from
    // DemonSpeciesCatalog.All filtered by rarity and ordered by SpeciesId, then indexes with
    // rng.NextInt(band.Count). Regenerating the catalog uncapped took it from 24 to 84 species, so
    // both the band's contents and its size changed and a different enemy is legitimately rolled.
    // Verified it is selection, not a determinism break: Same_inputs_resolve_identically and the
    // recall pro-rating tests stayed green unchanged, and Squad() uses a fixed "test-species" that
    // never touches the catalog — so the squad side of the resolution did not move at all.
    //
    // NOTE for the next capture: this golden is coupled to roster SIZE, so it moves every time
    // species are added. That is now expected rather than alarming, but it makes the test a poor
    // regression signal for the resolver itself — decoupling the wild-enemy pick from the live
    // catalog (a fixture band) would be the fix if the churn becomes annoying.
    const string ScoutHash = "D205A51C0D5376E49B93D0A807E45C5C701E06E14D92967944A1525E7243049B";
    const string ForageHash = "C8E6C8FE5A75EE2DB6391BDEB8D811EC3CE18A5F3BA9502D55A69AA69E1D296D";
    const string HuntHash = "40662D95ED23C07710258C149016A3360DAD31AA66078A9C0908AC53A3C1D204";
    const string WarpathHash = "96EC24504681E16A792ED0E624810E668C94B6EBD276C7419BF69315706847B6";

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
