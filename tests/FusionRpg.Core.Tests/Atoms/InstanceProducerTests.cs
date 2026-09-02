using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Power;
using Xunit;

namespace FusionRpg.Core.Tests.Atoms;

/// <summary>
/// T3.6 (`instance-producer`, ⭐ the payoff): <see cref="InstanceProducer.Compose"/>'s Core-only half —
/// the fixed core frozen exactly as <see cref="Instantiator.TryInstantiate"/> already does, the pool
/// half now drawn through <see cref="Resolver.Resolve"/> (module 2) instead of
/// <see cref="Instantiator.Draw"/>. The Data-layer half (<c>RpgStore.ProduceAndBind</c> — real
/// persistence, <c>ResolveBindings</c> reachability, the transactional atomicity guarantee) has its
/// own tests in <c>FusionRpg.Data.Tests</c>, the same split <c>AffixValidatorTests</c>/
/// <c>AffixStoreTests</c> already established for T3.1.
/// </summary>
public class InstanceProducerTests
{
    const int PinTheta = 20;
    static readonly PowerTuning Tuning = PowerTuning.Build(
        1, 1, PowerTuning.FixedCMilli, 0, PowerTuning.FixedPinIndex, PowerTuning.FixedPinValue,
        1000, 25000, 250, 1000, 5000, 5000, 25000);

    static readonly Dictionary<string, AtomRow> Catalog = new(StringComparer.Ordinal);
    static readonly Dictionary<string, AffixRow> Affixes = new(StringComparer.Ordinal);

    static InstanceProducerTests()
    {
        void AddAtom(string family, string variant, int tier, string paramsJson)
        {
            var id = AtomRow.DeriveId(family, variant, tier);
            Catalog[id] = new AtomRow
            {
                AtomId = id, KindId = "stat.modify", FamilyId = family, Variant = variant, Tier = tier,
                ParamsJson = paramsJson,
            };
        }

        AddAtom("atom.vitality", "", 1, "{\"channel\":\"maxHp\",\"op\":\"flat\",\"amount\":45}");
        AddAtom("atom.roll", "", 1,
            "{\"channel\":\"atk\",\"op\":\"flat\",\"amount\":{\"min\":10,\"max\":20,\"roll\":\"onInstantiate\"}}");
        foreach (var v in new[] { "fire", "ice", "air" })
            AddAtom("atom.ember-power", v, 1, "{\"channel\":\"atk\",\"op\":\"flat\",\"amount\":5}");
    }

    static AtomRow? LookupAtom(string id) => Catalog.TryGetValue(id, out var a) ? a : null;
    static AffixRow? LookupAffix(string id) => Affixes.TryGetValue(id, out var a) ? a : null;
    static IReadOnlyList<string> DomainMembers(string domain) =>
        domain == "element" ? new[] { "fire", "ice", "air" } : Array.Empty<string>();

    static void Seed(params AffixRow[] affixes)
    {
        Affixes.Clear();
        foreach (var a in affixes) Affixes[a.AffixId] = a;
    }

    static InstanceRow Compose(ContainerRow c, long seed, int theta = PinTheta, VariantShift? variant = null)
    {
        var r = InstanceProducer.Compose(c, LookupAtom, LookupAffix, DomainMembers, seed, theta, Tuning,
            out var instance, variant);
        Assert.True(r.IsOk, r.ToString());
        return instance!;
    }

    [Fact]
    public void The_fixed_core_comes_first_and_the_drawn_pool_continues_the_numbering()
    {
        Seed(new AffixRow("affix.ember", AffixClass.Prefix, new[] { new AffixRefRow(1, "atom.ember-power.fire.t1") }));
        var c = new ContainerRow
        {
            ContainerId = "item.core-then-pool", Kind = ContainerKind.Item, PrefixRolls = 1,
            Atoms = new[] { new ContainerAtomRow(1, "atom.vitality.t1") },
            Pool = new[] { new ContainerPoolRow("affix.ember", 100) },
        };

        var instance = Compose(c, 1);

        Assert.Equal(new[] { 1, 2 }, instance.Atoms.Select(a => a.Seq));
        Assert.Equal("atom.vitality.t1", instance.Atoms[0].AtomId);
        Assert.Equal("atom.ember-power.fire.t1", instance.Atoms[1].AtomId);
    }

    [Fact]
    public void PowerJson_stays_null_on_every_produced_atom()
    {
        // A3's own guard — power is backfilled later (E9), never computed on this path.
        Seed(new AffixRow("affix.ember", AffixClass.Prefix, new[] { new AffixRefRow(1, "atom.ember-power.fire.t1") }));
        var c = new ContainerRow
        {
            ContainerId = "item.no-power", Kind = ContainerKind.Item, PrefixRolls = 1,
            Atoms = new[] { new ContainerAtomRow(1, "atom.vitality.t1") },
            Pool = new[] { new ContainerPoolRow("affix.ember", 100) },
        };

        var instance = Compose(c, 1);

        Assert.All(instance.Atoms, a => Assert.Null(a.PowerJson));
    }

    [Fact]
    public void A_slot_bearing_affix_the_old_Draw_could_never_expand_resolves_through_Compose()
    {
        // The whole point of T3.6: an affix Instantiator.Draw would THROW NotSupportedException for
        // (a slot ref) resolves cleanly here, because Compose calls Resolver.Resolve, not Draw.
        Seed(new AffixRow("affix.elemental", AffixClass.Prefix, new[]
        {
            new AffixRefRow(1, null, "E1", "element", 1, "atom.ember-power.$E1"),
        }));
        var c = new ContainerRow
        {
            ContainerId = "item.slot", Kind = ContainerKind.Item, PrefixRolls = 1,
            MinTier = 1, MaxTier = 1,
            Pool = new[] { new ContainerPoolRow("affix.elemental", 100, "g.elemental") },
        };

        var instance = Compose(c, 5);

        var atomId = Assert.Single(instance.Atoms).AtomId;
        Assert.StartsWith("atom.ember-power.", atomId, StringComparison.Ordinal);
    }

    [Fact]
    public void Content_scale_applies_to_both_the_core_and_the_drawn_pool()
    {
        var c = new ContainerRow
        {
            ContainerId = "item.scaled", Kind = ContainerKind.Item,
            Atoms = new[] { new ContainerAtomRow(1, "atom.vitality.t1") },
        };

        // PinTheta (20) is the pin: contentScale(20) == 1.000 exactly, so a non-pin theta must move
        // the frozen value away from the raw authored 45.
        var pinned = Compose(c, 1, theta: PinTheta);
        var scaled = Compose(c, 1, theta: PinTheta + 50);

        var pinnedAmount = System.Text.Json.JsonDocument.Parse(pinned.Atoms[0].ValuesJson)
            .RootElement.GetProperty("amount").GetInt32();
        var scaledAmount = System.Text.Json.JsonDocument.Parse(scaled.Atoms[0].ValuesJson)
            .RootElement.GetProperty("amount").GetInt32();

        Assert.Equal(45, pinnedAmount);
        Assert.NotEqual(pinnedAmount, scaledAmount);
    }

    [Fact]
    public void Same_container_revision_seed_and_variant_reproduces_identically()
    {
        Seed(new AffixRow("affix.roll", AffixClass.Prefix, new[] { new AffixRefRow(1, "atom.roll.t1") }));
        var c = new ContainerRow
        {
            ContainerId = "item.repro", Kind = ContainerKind.Item, PrefixRolls = 1,
            Pool = new[] { new ContainerPoolRow("affix.roll", 100) },
        };
        var variant = new VariantShift("blessed", 0, 1, 0, false);

        var a = InstanceProducer.Compose(c, LookupAtom, LookupAffix, DomainMembers, 77, PinTheta, Tuning,
            out var instA, variant, catalogRevision: 3);
        var b = InstanceProducer.Compose(c, LookupAtom, LookupAffix, DomainMembers, 77, PinTheta, Tuning,
            out var instB, variant, catalogRevision: 3);

        Assert.True(a.IsOk); Assert.True(b.IsOk);
        Assert.Equal(instA!.ContentFingerprint(), instB!.ContentFingerprint());
    }

    [Fact]
    public void A_bad_container_is_rejected_and_composes_no_instance()
    {
        var c = new ContainerRow { ContainerId = "item.bad", Kind = ContainerKind.Item,
            Atoms = new[] { new ContainerAtomRow(1, "atom.nope.t1") } };

        var r = InstanceProducer.Compose(c, LookupAtom, LookupAffix, DomainMembers, 1, PinTheta, Tuning,
            out var instance);

        Assert.False(r.IsOk);
        Assert.Null(instance);
    }
}
