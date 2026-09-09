using FusionRpg.Contracts;
using FusionRpg.Core.ActorSurface;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Demons;
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
        // specimen instance and never falls back to the empire-wide DemonType allocation; general
        // lawn spawns resolve that fallback at their own spawn seam instead.
        var uniqueAllocation = store.LoadAllocation(AllocationScope.UniqueDemon, specimenId);
        IReadOnlyList<BoundDerivedAtom> BoundAtoms(StatContext _)
        {
            var list = new List<BoundDerivedAtom>();
            list.AddRange(EquippedBoundAtoms.DerivedFromStore(store, specimenId));
            list.AddRange(TreeBoundAtoms.ForPlayer(store, powerIndex, actor.PlayerId));
            return list;
        }

        var hub = ActorHubBootstrap.CreateDefault(
            powerIndex: powerIndex,
            aptitudeTuning: AptitudeTuningHub.Tuning,
            aptitudeAllocation: _ => commanderAllocation + uniqueAllocation,
            boundDerivedAtoms: BoundAtoms);

        return (hub, ctx);
    }

    public static ActorSheetDto ProjectSheet(RpgStore store, UniqueActorDto actor)
    {
        var (hub, ctx) = Build(store, actor);
        var primaryFinal = hub.Stats.Resolve(ctx);
        var (snapshot, contributions) = hub.ResolveDerivedWithContributions(ctx);
        var profile = store.GetDemonProfile(actor.InstanceId);
        var speciesName = profile != null && DemonSpeciesCatalog.IsKnown(profile.SpeciesId)
            ? DemonSpeciesCatalog.Get(profile.SpeciesId).Name
            : null;
        var displayName = string.IsNullOrWhiteSpace(profile?.Nickname) ? speciesName : profile!.Nickname;
        var roleLabel = string.Equals(actor.Side, "zombie", StringComparison.OrdinalIgnoreCase) ? "Zombie" : "Plant";
        var xpToNext = RpgXpCurve.XpToNext(RpgActorKinds.Specimen, actor.Level);

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
                var surface = FindSurface(surfaceEntries, kv.Key);
                if (surface is not null)
                {
                    display = surface.DisplayName.Resolve("en");
                    reading = surface.Reading.Resolve("en");
                    if (string.IsNullOrEmpty(compose))
                        compose = surface.Compose.ToString();
                }

                return new ActorSheetChannelDto
                {
                    ChannelId = kv.Key,
                    DisplayName = display,
                    Reading = reading,
                    ComposeKind = compose,
                    Value = kv.Value,
                    Contributions = contributions.ContributionsFor(kv.Key)
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
            Derived = derived,
            Primary = primary
        };
    }

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
