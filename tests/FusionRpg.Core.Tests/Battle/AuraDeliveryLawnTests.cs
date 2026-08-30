using FusionRpg.Contracts;
using FusionRpg.Core.Aura;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Stats.Derived.Subsystems;
using Xunit;

namespace FusionRpg.Core.Tests.Battle;

/// <summary>
/// aura-skill-todo.md Phase 5 / <b>TC2</b> — the <b>lawn</b> twin of <see cref="AuraDeliveryTests"/>,
/// which proves the same thing on the Sim host.
///
/// <para><b>What TC2 said, and what turned out to be true.</b> TC2 was recorded as ⛔ BLOCKED on the
/// grounds that <c>EffectBag.Grant</c> rejects an unregistered <c>EffectId</c>. Re-checking that
/// constraint against code rather than inheriting it (DESIGN-GATE: <i>"test the constraint before you
/// declare it"</i>) split it into two very different halves:</para>
/// <list type="bullet">
///   <item><b>Not a blocker — the catalog.</b> <c>EffectBag</c> takes an <see cref="IEffectCatalog"/>
///   by constructor injection and tests already register their own defs
///   (<c>EffectBagTests</c> uses <c>InMemoryEffectCatalog</c>). An unknown effect id is a *content*
///   gap on the live lawn, not a testability wall.</item>
///   <item><b>A real blocker — the action whitelist.</b> <c>EffectOverlayMerge.AllowedByAction</c>
///   (lines 130-154) enumerates ten actions — <c>ModifyStat</c>, <c>ApplyStatus</c>,
///   <c>ApplyResourceDelta</c>, … — and <b>none of them is a derived-stat action</b>. So a grant
///   carrying <c>derived.channel</c>/<c>derived.op</c>/<c>derived.amount</c> is refused with
///   <c>unknown overlay key 'derived.channel' for effect actions</c>. Adding that action (sink
///   executor + param schema + registry row + content validation) is squarely
///   <c>effect-atom</c> Wave 6 / E20-E25, and building it inside this task would be scope creep into
///   another program's module.</item>
/// </list>
///
/// <para><b>So what this file proves, exactly.</b> Everything downstream of the grant hop, using real
/// production code (<see cref="GrantedDerivedAtomReader"/> → <see cref="AtomDerivedSubsystem"/> →
/// <c>ActorHub</c>), with a real plant/zombie <see cref="StatContext"/>. The only substituted piece is
/// the <b>grant transport</b>: grants are placed in a real <see cref="InMemoryEffectGrantStore"/>
/// directly instead of arriving through <c>EffectBag.Grant</c>.</para>
///
/// <para><b>What it does NOT prove, stated plainly so nobody reads more into a green run:</b> that a
/// real authored aura compiles to a derived-stat effect and survives <c>EffectBag.Grant</c>'s overlay
/// validation. That single hop is the whole of the remaining TC2 gap, and it stays open against Wave 6
/// — along with A5's live on-the-lawn proof in
/// <c>docs/architecture/effect-atom/spec-derived-write-lawn.md</c>.</para>
/// </summary>
public class AuraDeliveryLawnTests
{
    // Same two tunings AuraDeliveryTests uses, so "the two hosts agree" is a comparison of like with
    // like rather than of two differently-configured ladders.
    static AuraTuning Rung7To10() => new(new Dictionary<int, long>
    {
        [7] = 5359, [8] = 7090, [9] = 9379, [10] = 12407,
    }, MaxActiveAuras: 1);

    static AptitudeTuning LinearGammaTuning() => AptitudeTuningLoader.Parse("""
        {
          "schemaVersion": 1, "version": 1,
          "grant": { "aptitudePointsPerTheta": 3, "skillPointsPerTheta": 1 },
          "pointEconomy": { "aptitudePointsPerThetaMilliByScope": { "commander": 1, "demonType": 4, "aspect": 4, "uniqueDemon": 6 }, "respecPrice": 10 }, "guardEconomy": { "flatCommitCost": 50, "absorbDrainSharePermille": 300, "riposteShareCapPermille": 400 }, "mitigation": { "scaleMilli": 1000, "families": ["combat.defense", "combat.dodge", "combat.parry", "combat.block", "combat.absorption", "combat.heal"] },
          "read": { "contest": { "spanPoints": 100.0, "shareExponentMilli": 1000 }, "magnitude": { "shareExponentMilli": 1000 } },
          "recovery": { "scaleMilli": 374, "targetRecoveryShareMilli": 670, "families": ["resource.regen"] },
          "familyRead": { "combat.power": "magnitude" },
          "edges": [ { "channel": "combat.power.omni", "source": "Might", "kMilli": 1000 } ]
        }
        """);

    /// <summary>The identical call <see cref="AuraDeliveryTests"/> makes for its Sim-host assertion.</summary>
    static long T10Value() =>
        AuraMagnitude.Compute(rung: 10, share: 1.0, pTheta: 1_000_000, Rung7To10(), LinearGammaTuning());

    const int PlantType = 20_001;

    static InMemoryEffectGrantStore StoreWith(params EffectGrant[] grants)
    {
        var store = new InMemoryEffectGrantStore();
        foreach (var g in grants) store.Upsert(g);
        return store;
    }

    static EffectGrant AuraGrant(string ownerKind, string ownerKey, string channel, double amount) => new()
    {
        GrantId = "g-aura",
        EffectId = "aura:test-ember",           // the same source tag the Sim-host twin uses
        OwnerKind = ownerKind,
        OwnerKey = ownerKey,
        Overlay = new Dictionary<string, object?>
        {
            [GrantedDerivedAtomReader.ChannelKey] = channel,
            [GrantedDerivedAtomReader.OpKey] = "flat",
            [GrantedDerivedAtomReader.AmountKey] = amount,
        },
    };

    static FusionRpg.Core.Stats.Derived.ActorHub HubOver(IEffectGrantStore store)
    {
        var hub = new FusionRpg.Core.Stats.Derived.ActorHub(StatSystemBootstrap.CreateDefault());
        hub.Register(new AtomDerivedSubsystem(ctx => GrantedDerivedAtomReader.Read(store, ctx)));
        return hub;
    }

    static StatContext Plant(string entityKey = "0xPLANT") =>
        new() { Side = StatSide.Plant, TypeId = PlantType, EntityKey = entityKey };

    static StatContext Zombie(string entityKey = "0xZOMBIE") =>
        new() { Side = StatSide.Zombie, TypeId = PlantType, EntityKey = entityKey };

    // ── criterion 1: the two hosts agree ─────────────────────────────────────────────────────────

    /// <summary>
    /// TC2's first acceptance criterion, and its stated point: <i>"the two hosts agreeing"</i>. The
    /// value <see cref="AuraDeliveryTests"/> feeds into <c>ActiveCommanderAura</c> on the Sim host
    /// reaches <c>combat.power.omni</c> on a lawn plant, unchanged, through the real lawn executor.
    /// </summary>
    [Fact]
    public void An_active_aura_raises_combat_power_omni_on_a_lawn_plant_by_the_same_T10_value_the_sim_host_uses()
    {
        var t10 = T10Value();
        Assert.True(t10 > 1000, "test needs a large, unambiguous buff, not a rounding-noise one");

        var hub = HubOver(StoreWith(AuraGrant("plant", PlantType.ToString(), DerivedStatChannels.CombatPowerOmni, t10)));

        var derived = hub.Resolve(Plant()).Derived;

        Assert.Equal(t10, derived.Get(DerivedStatChannels.CombatPowerOmni, 0), 6);
    }

    /// <summary>The aura reaches <b>every</b> plant of its type, not just the one whose entity key was
    /// named — the lawn twin of the Sim host's "every friendly squad actor". Type-scoped delivery is
    /// what makes an aura an aura rather than a single-target buff.</summary>
    [Fact]
    public void A_type_scoped_aura_reaches_every_plant_of_that_type()
    {
        var t10 = T10Value();
        var hub = HubOver(StoreWith(AuraGrant("plant", PlantType.ToString(), DerivedStatChannels.CombatPowerOmni, t10)));

        foreach (var key in new[] { "0xA", "0xB", "0xC" })
            Assert.Equal(t10, hub.Resolve(Plant(key)).Derived.Get(DerivedStatChannels.CombatPowerOmni, 0), 6);
    }

    /// <summary>A match-scoped aura reaches a plant too — the `match` owner key is the broadest of the
    /// three shipped scopes and is how a commander-wide aura is expressed.</summary>
    [Fact]
    public void A_match_scoped_aura_also_reaches_a_lawn_plant()
    {
        var t10 = T10Value();
        var hub = HubOver(StoreWith(AuraGrant("match", EffectOwnerKeys.Match, DerivedStatChannels.CombatPowerOmni, t10)));

        Assert.Equal(t10, hub.Resolve(Plant()).Derived.Get(DerivedStatChannels.CombatPowerOmni, 0), 6);
    }

    // ── criterion 2: never the enemy side ────────────────────────────────────────────────────────

    /// <summary>TC2's second acceptance criterion — the lawn twin of
    /// <c>AuraDeliveryTests.The_aura_never_touches_the_enemy_wave_side</c>. A plant-side aura must not
    /// reach a zombie, <b>even at the identical type id</b>, which is the case a naive owner-key match
    /// would get wrong.</summary>
    [Fact]
    public void A_plant_side_aura_never_touches_a_zombie_even_at_the_same_type_id()
    {
        var hub = HubOver(StoreWith(AuraGrant("plant", PlantType.ToString(), DerivedStatChannels.CombatPowerOmni, T10Value())));

        var zombie = hub.Resolve(Zombie());

        Assert.Equal(0, zombie.Derived.Get(DerivedStatChannels.CombatPowerOmni, 0), 6);
        // ...and the plant in the same resolve pair genuinely IS buffed, so this cannot pass because
        // the aura reached nobody at all.
        Assert.True(hub.Resolve(Plant()).Derived.Get(DerivedStatChannels.CombatPowerOmni, 0) > 0);
    }

    // ── criterion 3: absent, nothing moves ───────────────────────────────────────────────────────

    /// <summary>TC2's third acceptance criterion. With no aura granted, the lawn resolve is unchanged —
    /// asserted by reference identity on <c>AppliedCombat</c>, which <c>ActorHub.MergeAppliedCombat</c>
    /// returns as the primary <i>instance</i> when no bridge channel carries a value. No goldens move
    /// because nothing composes at all.</summary>
    [Fact]
    public void Absent_any_aura_the_lawn_resolve_is_unchanged()
    {
        var hub = HubOver(StoreWith());

        var result = hub.Resolve(Plant());

        Assert.Equal(0, result.Derived.Get(DerivedStatChannels.CombatPowerOmni, 0), 6);
        Assert.True(ReferenceEquals(result.AppliedCombat, result.RuntimePrimary),
            "an empty aura set must leave AppliedCombat as the primary instance — anything else is a silent write");
    }

    /// <summary>Withdrawing the aura returns the channel to zero — the "disable" half. An aura that
    /// cannot be turned off is a permanent stat, which is a different feature.</summary>
    [Fact]
    public void Withdrawing_the_aura_returns_the_channel_to_zero()
    {
        var store = StoreWith(AuraGrant("plant", PlantType.ToString(), DerivedStatChannels.CombatPowerOmni, T10Value()));
        var hub = HubOver(store);

        Assert.True(hub.Resolve(Plant()).Derived.Get(DerivedStatChannels.CombatPowerOmni, 0) > 0);

        Assert.True(store.Withdraw("g-aura"));

        Assert.Equal(0, hub.Resolve(Plant()).Derived.Get(DerivedStatChannels.CombatPowerOmni, 0), 6);
    }

    // ── the named remaining gap, asserted rather than described ──────────────────────────────────

    /// <summary>
    /// <b>The TC2 gap, pinned as a test so it cannot be quietly forgotten.</b> This asserts the blocker
    /// is exactly where this file's summary says it is: a derived-stat overlay is refused by
    /// <c>EffectOverlayMerge.AllowedByAction</c>, because no shipped effect action declares those keys.
    ///
    /// <para>When <c>effect-atom</c> Wave 6 (E20-E25) adds the derived-stat action, <b>this test will
    /// start failing</b> — and that is the intended signal. At that point delete it and write the real
    /// end-to-end grant test in its place, closing TC2's last hop.</para>
    /// </summary>
    [Fact]
    public void The_remaining_gap_a_derived_overlay_is_still_refused_by_every_shipped_effect_action()
    {
        var overlay = new Dictionary<string, object?>
        {
            [GrantedDerivedAtomReader.ChannelKey] = DerivedStatChannels.CombatPowerOmni,
            [GrantedDerivedAtomReader.OpKey] = "flat",
            [GrantedDerivedAtomReader.AmountKey] = 1234.0,
        };

        // Every action the shipped whitelist knows about, tried one at a time. Not one of them accepts
        // a derived-stat overlay -- which is precisely why TC2's last hop cannot be written yet.
        foreach (var action in new[]
                 {
                     EffectActions.ModifyStat, EffectActions.ApplyStatus, EffectActions.ClearStatus,
                     EffectActions.ApplyResourceDelta, EffectActions.Economy,
                 })
        {
            var actions = new[] { new EffectActionRow { Action = action } };

            var ok = EffectOverlayMerge.TryValidateOverlayForDef(actions, overlay, out var error);

            Assert.False(ok, $"action '{action}' now accepts a derived-stat overlay — Wave 6 has landed; " +
                             "delete this test and write the real end-to-end grant test that closes TC2.");
            Assert.Contains("derived.", error!, StringComparison.Ordinal);
        }
    }
}
