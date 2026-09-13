using FusionRpg.Contracts;
using FusionRpg.Core.ActorSurface;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Creatures;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Effects.Atoms.Power;
using FusionRpg.Core.Power;
using FusionRpg.Core.Progression;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Stats.Derived.Subsystems;
using FusionRpg.Data;

namespace FusionRpg.Server;

/// <summary>
/// Sole Server Hot compose entry for UniqueActor sheet/derived — ActorHub only, FULL durable fan-in
/// (progression + aptitude + equip atoms + passive tree). Status (<c>l2b.derived</c>) stays
/// Injector-only (Hot session). BattleStatComposer stays locked-separate (ADR 2026-09-07).
/// Injector tree hydrate is a named wiring gap — PassiveTreeTuningHub is Server-only today.
/// </summary>
public static class UniqueActorHubCompose
{
    public static (ActorHub Hub, StatContext Ctx) Build(RpgStore store, UniqueActorDto actor)
    {
        var level = (int)Math.Max(1, actor.Level);
        var baseline = new EntityBaseline
        {
            Hp = BattleRuleset.BaseHp(level),
            MaxHp = BattleRuleset.BaseHp(level),
            Atk = BattleRuleset.BaseAtk(level)
        };

        var factory = new StatContextFactory();
        var ctx = string.Equals(actor.Side, "zombie", StringComparison.OrdinalIgnoreCase)
            ? factory.ForZombie(actor.InstanceId, baseline, actor.TypeId, playerId: actor.PlayerId)
            : factory.ForPlant(actor.InstanceId, baseline, actor.TypeId, playerId: actor.PlayerId);

        var powerIndex = new Power.ServerPowerIndexProvider(store, PowerTuningHub.Tuning);

        var specimenId = actor.InstanceId;
        var commanderAllocation = store.LoadAllocation(
            AllocationScope.Commander, AptitudeEndpoints.ScopeKey(actor.PlayerId));
        // A unique specimen is a dedicated progression source. Its aptitude input is keyed by the
        // specimen instance and never falls back to the empire-wide CreatureType allocation; general
        // lawn spawns resolve that fallback at their own spawn seam instead.
        var uniqueAllocation = store.LoadAllocation(AllocationScope.UniqueCreature, specimenId);
        IReadOnlyList<BoundDerivedAtom> BoundAtoms(StatContext _)
        {
            var list = new List<BoundDerivedAtom>();
            list.AddRange(EquippedBoundAtoms.DerivedFromStore(store, specimenId));
            list.AddRange(TreeBoundAtoms.ForPlayer(store, powerIndex, actor.PlayerId));
            return list;
        }

        // channelmods-hub: star/loyalty reach the sheet through the SAME producer battle uses
        // (StarLoyaltyBonus), so the two cannot drift. Read once — a specimen's star and its
        // contract loyalty are durable per-actor values, and Build runs per sheet/derived read.
        var profile = store.GetCreatureProfile(specimenId);
        var loyalty = store.GetContract(specimenId)?.Loyalty ?? 0;
        StarLoyaltyContribution StarLoyalty(StatContext _) =>
            new(profile?.Star ?? 0, loyalty, level);

        var hub = ActorHubBootstrap.CreateDefault(
            powerIndex: powerIndex,
            aptitudeTuning: AptitudeTuningHub.Tuning,
            aptitudeAllocation: _ => commanderAllocation + uniqueAllocation,
            boundDerivedAtoms: BoundAtoms,
            seedResourceBaseline: true,
            starLoyalty: StarLoyalty);

        return (hub, ctx);
    }

    public static ActorSheetDto ProjectSheet(
        RpgStore store, UniqueActorDto actor, IActorLiveStateStore? liveState = null)
    {
        var (hub, ctx) = Build(store, actor);
        var primaryFinal = hub.Stats.Resolve(ctx);
        var (snapshot, contributions) = hub.ResolveDerivedWithContributions(ctx);
        var powerIndex = new Power.ServerPowerIndexProvider(store, PowerTuningHub.Tuning);
        var profile = store.GetCreatureProfile(actor.InstanceId);
        var speciesName = profile != null
            && CreatureSpeciesCatalog.IsConfigured
            && CreatureSpeciesCatalog.IsKnown(profile.SpeciesId)
            ? CreatureSpeciesCatalog.Get(profile.SpeciesId).Name
            : null;
        var displayName = string.IsNullOrWhiteSpace(profile?.Nickname) ? speciesName : profile!.Nickname;
        var roleLabel = string.Equals(actor.Side, "zombie", StringComparison.OrdinalIgnoreCase) ? "Zombie" : "Plant";
        var xpToNext = RpgXpCurve.XpToNext(RpgActorKinds.Specimen, actor.Level);
        var standing = ProjectStanding(store, actor, powerIndex);
        var resourcePools = ProjectResourcePools(store, actor, snapshot, ctx, powerIndex);
        var (liveStatuses, shieldLayers, shieldSummary) = ProjectHotLive(liveState, actor.InstanceId);

        IReadOnlyList<DerivedStatSurfaceEntry> surfaceEntries = Array.Empty<DerivedStatSurfaceEntry>();
        try { surfaceEntries = DerivedStatSurfaceCatalogHub.Catalog.Entries; }
        catch (InvalidOperationException) { /* catalog optional until host configures */ }

        var registry = hub.Composer.Registry;
        var derived = snapshot.Channels
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv =>
            {
                registry.TryResolveChannel(kv.Key, out var def);
                var compose = def?.Compose.ToString() ?? "";
                var display = kv.Key;
                var reading = "";
                string? surfaceUnit = null;
                var surface = FindSurface(surfaceEntries, kv.Key);
                if (surface is not null)
                {
                    display = surface.DisplayName.Resolve("en");
                    reading = surface.Reading.Resolve("en");
                    surfaceUnit = surface.UnitClass.ToString();
                    if (string.IsNullOrEmpty(compose))
                        compose = surface.Compose.ToString();
                }

                var contribRows = contributions.ContributionsFor(kv.Key).ToList();
                var contribPairs = contribRows
                    .Select(c => (c.SourceId, c.Value))
                    .ToList();

                return new ActorSheetChannelDto
                {
                    ChannelId = kv.Key,
                    DisplayName = display,
                    Reading = reading,
                    ComposeKind = compose,
                    // D6: Value stays double (exempt); shield long elsewhere.
                    Value = kv.Value,
                    UnitClass = DerivedSheetChannelMeta.ResolveUnitClass(def, surfaceUnit),
                    DefaultValue = def?.DefaultValue ?? 0,
                    Cap = def?.Cap,
                    RenderState = DerivedSheetChannelMeta.ResolveRenderState(
                        kv.Key, def, kv.Value, contribPairs),
                    Contributions = contribRows
                        .Select(c => new ActorContributionDto
                        {
                            SourceId = c.SourceId,
                            Label = ContributionSourceIds.FictionLabel(c.SourceId),
                            Op = c.Op.ToString(),
                            Value = c.Value
                        })
                        .ToList()
                };
            })
            .ToList();

        var primary = primaryFinal.Contributions
            .Select(m =>
            {
                var sid = ContributionSourceIds.Primary(m.SourceKind, m.SourceId);
                return new ActorContributionDto
                {
                    SourceId = sid,
                    Label = ContributionSourceIds.FictionLabel(sid),
                    Op = m.Op.ToString(),
                    Value = m.Value
                };
            })
            .ToList();

        return new ActorSheetDto
        {
            InstanceId = actor.InstanceId,
            PlayerId = actor.PlayerId,
            Side = actor.Side,
            TypeId = actor.TypeId,
            DisplayName = displayName,
            SpeciesId = profile?.SpeciesId,
            SpeciesName = speciesName,
            Phase = actor.Phase,
            RoleLabel = roleLabel,
            Level = actor.Level,
            Xp = actor.Xp,
            XpToNext = xpToNext,
            ElementTyping = profile == null ? null : new ActorElementTypingDto
            {
                Primary = profile.ElementPrimary,
                Secondary = profile.ElementSecondary
            },
            Standing = standing,
            Derived = derived,
            Primary = primary,
            LiveStatuses = liveStatuses,
            ResourcePools = resourcePools,
            ShieldSummary = shieldSummary,
            ShieldLayers = shieldLayers
        };
    }

    /// <summary>
    /// Hot bag → liveStatuses + shieldLayers + shieldSummary (S1/S2). No bag entry = cold honesty.
    /// Summary elementId = front drain-order layer; stacks = layer count; null when no layers.
    /// </summary>
    static (
        IReadOnlyList<ActorStatusGlyphDto> LiveStatuses,
        IReadOnlyList<ActorShieldLayerDto> ShieldLayers,
        ActorShieldSummaryDto? ShieldSummary)
        ProjectHotLive(IActorLiveStateStore? liveState, string instanceId)
    {
        var bag = liveState?.Get(instanceId);
        if (bag is null)
        {
            return (
                Array.Empty<ActorStatusGlyphDto>(),
                Array.Empty<ActorShieldLayerDto>(),
                null);
        }

        var statuses = bag.LiveStatuses ?? Array.Empty<ActorStatusGlyphDto>();
        var layers = bag.ShieldLayers ?? Array.Empty<ActorShieldLayerDto>();
        if (layers.Count == 0)
            return (statuses, Array.Empty<ActorShieldLayerDto>(), null);

        long sumCurrent = 0, sumMax = 0;
        for (var i = 0; i < layers.Count; i++)
        {
            sumCurrent += layers[i].Current;
            sumMax += layers[i].Max;
        }

        // Q3 / S2: omit glance when empty layers or totals ≤ 0.
        if (sumCurrent <= 0)
            return (statuses, layers, null);

        var front = layers[0];
        return (
            statuses,
            layers,
            new ActorShieldSummaryDto
            {
                ElementId = front.ElementId,
                Current = sumCurrent,
                Max = sumMax,
                Stacks = layers.Count
            });
    }

    /// <summary>
    /// Standing = <see cref="PowerVector"/> from durable equip + tree atoms (definitions.md §7).
    /// Empty grants → Zero vector (ready, not pending).
    /// </summary>
    static ActorStandingDto ProjectStanding(
        RpgStore store, UniqueActorDto actor, IPowerIndexProvider powerIndex)
    {
        var atoms = new List<AtomRow>();
        foreach (var input in EquippedBoundAtoms.InputsFromStore(store, actor.InstanceId))
            atoms.Add(input.Atom);
        foreach (var bound in TreeBoundAtoms.ForPlayer(store, powerIndex, actor.PlayerId))
            atoms.Add(SyntheticStatDerived(bound));

        var vector = ActorPowerCache.Compose(atoms);
        return new ActorStandingDto
        {
            Offense = vector.Offense,
            Survivability = vector.Survivability,
            Control = vector.Control,
            Utility = vector.Utility,
            Economy = vector.Economy
        };
    }

    /// <summary>
    /// Cold UniqueActor pools: Max from Hub (<see cref="ResourceBaselineSubsystem"/> + gear/tree).
    /// Current from persisted subset or at-rest full. LiveStatuses/shield stay Hot-only.
    /// BattleRuleset floor is a last-resort guard only if Hub max is still 0 (should not happen
    /// when seedResourceBaseline is registered).
    /// </summary>
    static IReadOnlyList<ActorResourcePoolDto> ProjectResourcePools(
        RpgStore store,
        UniqueActorDto actor,
        ActorDerivedSnapshot snapshot,
        StatContext ctx,
        IPowerIndexProvider powerIndex)
    {
        var level = (int)Math.Max(1, actor.Level);
        var theta = Math.Max(1, powerIndex.ActorIndex(ctx));
        var baseHp = BattleRuleset.BaseHp(level);
        var persisted = store.GetUniqueActorPersistedPools(actor.InstanceId);
        var pools = new List<ActorResourcePoolDto>(DerivedStatChannels.ResourceIds.Count);

        foreach (var id in DerivedStatChannels.ResourceIds)
        {
            var max = ResourceChannelReader.Max(snapshot, id);
            if (max <= 0)
                max = BattleRuleset.BaseResourceMax(theta, id, baseHp); // overflow guard — Hub seed is SSOT
            long current;
            if (persisted.TryGetValue(id, out var stored))
                current = Math.Clamp(stored, 0, Math.Max(0, max));
            else
                current = max; // at-rest refill for ids not in the cross-delve persist subset
            pools.Add(new ActorResourcePoolDto
            {
                ResourceId = id,
                Current = current,
                Max = max
            });
        }

        return pools;
    }

    static AtomRow SyntheticStatDerived(BoundDerivedAtom bound) =>
        new()
        {
            AtomId = $"sheet.standing.{bound.SourceId}",
            KindId = "stat.derived",
            FamilyId = "sheet.standing",
            Variant = bound.Channel.Replace('.', '-'),
            Tier = 1,
            Name = bound.SourceId,
            ParamsJson =
                $"{{\"channel\":\"{bound.Channel}\",\"op\":\"flat\",\"amount\":{bound.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}",
            Enabled = true
        };

    static DerivedStatSurfaceEntry? FindSurface(IReadOnlyList<DerivedStatSurfaceEntry> entries, string channelId)
    {
        DerivedStatSurfaceEntry? best = null;
        var bestLen = -1;
        foreach (var e in entries)
        {
            var matches = string.Equals(channelId, e.Family, StringComparison.Ordinal)
                || channelId.StartsWith(e.Family + ".", StringComparison.Ordinal);
            if (matches && e.Family.Length > bestLen)
            {
                best = e;
                bestLen = e.Family.Length;
            }
        }
        return best;
    }
}
