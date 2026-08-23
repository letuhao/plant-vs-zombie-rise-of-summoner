using FusionRpg.Contracts;
using FusionRpg.Core.World;
using FusionRpg.Core.World.Intel;
using FusionRpg.Core.World.Loam;
using FusionRpg.Core.World.Topology;
using FusionRpg.Core.World.Turn;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

/// <summary>
/// The world map's HTTP surface (spec-world-model.md §Server, spec-turn-engine.md §Server).
///
/// Reads are plain projections. The only two writes a client gets are filing orders and ending its
/// own turn — every actual mutation happens inside the turn engine, behind the barrier.
/// </summary>
public static class WorldEndpoints
{
    /// <summary>Bounded input at the boundary — a turn's orders are a handful, not a firehose.</summary>
    const int MaxCommandsPerSubmit = 200;

    public static void MapWorld(this WebApplication app)
    {
        var g = app.MapGroup("/api/world");

        g.MapGet("/{playerId:long}", (long playerId, RpgStore store) =>
        {
            var header = store.GetActiveWorld(playerId);
            return header is null ? Results.NotFound() : Results.Ok(Project(header));
        });

        // Takes a viewer and refuses an unknown one rather than quietly falling back to omniscience.
        // Not the *only* place fog reaches the wire — the turn report projects its entries and its
        // commands the same way — but this is the one a map view polls.
        g.MapGet("/{worldId}/state", (string worldId, string? asFaction, bool? lifelines, RpgStore store) =>
        {
            var world = store.LoadWorldState(worldId);
            if (world is null) return Results.NotFound();

            var viewer = string.IsNullOrWhiteSpace(asFaction)
                ? world.Factions.FirstOrDefault(f => f.Kind == WorldFactionKind.Player)?.FactionId
                : asFaction.Trim();

            if (viewer is null || world.Factions.All(f => !string.Equals(f.FactionId, viewer, StringComparison.Ordinal)))
                return Results.BadRequest(new { reason = "faction.unknown" });

            // Reconnection cost is an O(holdings⁴) sweep and the overlay it feeds is off by
            // default, so it is asked for rather than always paid for. Free at six sectors; not free
            // at the size world-generator will produce, on an endpoint a map view polls.
            var topology = lifelines == true ? Lifelines(world, viewer) : NoLifelines;

            return Results.Ok(Project(world, new BelievedWorldView(world, viewer), topology));
        });

        g.MapPost("/{worldId}/commands", (string worldId, SubmitWorldCommandsRequest body, RpgStore store) =>
        {
            var world = store.LoadWorldState(worldId);
            if (world is null) return Results.NotFound();
            if (body.Commands is not { Count: > 0 })
                return Results.BadRequest(new { reason = "commands.empty" });

            // An FE almost always means "my orders", so the player faction is the default.
            var commanderId = string.IsNullOrWhiteSpace(body.CommanderId)
                ? world.Factions.FirstOrDefault(f => f.Kind == WorldFactionKind.Player)?.FactionId
                : body.CommanderId!.Trim();
            if (commanderId is null) return Results.BadRequest(new { reason = "commander.unknown" });

            if (body.Commands.Count > MaxCommandsPerSubmit)
                return Results.BadRequest(new { reason = "commands.too-many", max = MaxCommandsPerSubmit });

            var commands = body.Commands.Select(c => new WorldCommand
            {
                CommanderId = commanderId,
                CommandId = (c.CommandId ?? "").Trim(),
                Kind = (c.Kind ?? "").Trim(),
                EntityId = c.EntityId,
                SectorId = c.SectorId,
                SlotIndex = c.SlotIndex,
                Stance = c.Stance,
                LanePath = c.LanePath ?? new List<string>()
            }).ToList();

            // One pass over the batch — the store reads the world once and owns which turn is open.
            // Partial acceptance is reported per command: one stale order must not throw away the
            // rest of a commander's turn.
            var results = store.SubmitWorldCommands(worldId, commands)
                .Select(o => new WorldCommandResultDto
                {
                    CommandId = o.CommandId,
                    Ok = o.Ok,
                    Reason = o.Reason,
                    Replayed = o.Replayed
                })
                .ToList();

            return Results.Ok(new { turn = world.CurrentTurn, commanderId, results });
        });

        MapTurns(g);
    }

    /// <summary>
    /// Turn writes. Kept in the same module as the reads but deliberately separate from them: the
    /// only two things a client may do to a world are file orders and end its own turn.
    /// </summary>
    static void MapTurns(RouteGroupBuilder g)
    {
        g.MapPost("/{worldId}/commit", (string worldId, CommitWorldTurnRequest? body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var world = store.LoadWorldState(worldId);
            if (world is null) return Results.NotFound();

            var commanderId = string.IsNullOrWhiteSpace(body?.CommanderId)
                ? world.Factions.FirstOrDefault(f => f.Kind == WorldFactionKind.Player)?.FactionId
                : body!.CommanderId!.Trim();
            if (commanderId is null) return Results.BadRequest(new { reason = "commander.unknown" });

            // Required, not optional. A commit that does not name its turn is a commit that can be
            // retried into the *next* one once the AI releases the barrier automatically, which
            // costs the player a turn they never played.
            if (body?.Turn is not { } expectedTurn)
                return Results.BadRequest(new { reason = "turn.missing" });

            var result = store.CommitWorldTurn(worldId, commanderId, expectedTurn);
            if (!result.Ok) return Results.BadRequest(new { reason = result.Reason });

            // Only the commit that actually stepped the world is worth waking every client for.
            if (result.Advanced) hub.Clients.All.SendAsync("WorldUpdated", new { worldId });

            return Results.Ok(new WorldTurnCommitDto
            {
                Ok = true,
                Reason = result.Reason,
                Advanced = result.Advanced,
                StateHash = result.StateHash,
                CurrentTurn = store.GetWorldHeader(worldId)?.CurrentTurn ?? world.CurrentTurn
            });
        });

        // Takes a viewer for the same reason /state does. Fog is not a secrecy boundary here —
        // `?asFaction=` already hands any caller any faction's view, and this is a single-player
        // game with a trusted client — it is a *rendering* rule: by default you are shown your own
        // war, and auditing somebody else's is a thing you ask for on purpose.
        g.MapGet("/{worldId}/turn/{turn:int}", (string worldId, int turn, string? asFaction, RpgStore store) =>
        {
            var log = store.GetWorldTurnLog(worldId, turn);
            if (log is null) return Results.NotFound();

            var world = store.LoadWorldState(worldId);
            var viewer = string.IsNullOrWhiteSpace(asFaction)
                ? world?.Factions.FirstOrDefault(f => f.Kind == WorldFactionKind.Player)?.FactionId
                : asFaction.Trim();

            if (world is not null && (viewer is null
                || world.Factions.All(f => !string.Equals(f.FactionId, viewer, StringComparison.Ordinal))))
                return Results.BadRequest(new { reason = "faction.unknown" });

            var believed = world is null || viewer is null ? null : new BelievedWorldView(world, viewer);

            // Reports outside the hot tail are re-derived by replay, and re-derivation refuses
            // rather than fabricating across an engine version change — so this can legitimately
            // come back empty for an old turn played by an older ruleset.
            var report = store.GetWorldTurnReport(worldId, turn);

            return Results.Ok(new WorldTurnReportDto
            {
                Turn = turn,
                StateHash = log.StateHash,
                Phases = report?.Phases.ToList() ?? new List<string>(),
                // Projected like the commands beside them (W39). Entries carry free text, so the
                // filtering is on the structured `SectorId` rather than on the prose — matching a
                // sector name out of a sentence works until somebody writes a different sentence.
                Entries = report?.Entries
                    .Where(e => VisibleTo(e.SectorId, believed))
                    .Select(e => new WorldTurnEntryDto
                    {
                        Phase = e.Phase, Kind = e.Kind, Subject = e.Subject,
                        Detail = e.Detail, SectorId = e.SectorId
                    })
                    .ToList() ?? new List<WorldTurnEntryDto>(),

                // Read from the command log rather than the report, so a trimmed turn still says
                // what everyone was trying to do — and why, where an AI was the one trying.
                Commands = store.ListLoggedWorldCommands(worldId, turn)
                    .Where(l => VisibleTo(l.Command, viewer, believed))
                    .Select(l => new WorldTurnCommandDto
                    {
                        CommanderId = l.Command.CommanderId,
                        CommandId = l.Command.CommandId,
                        Kind = l.Command.Kind,
                        EntityId = l.Command.EntityId,
                        SectorId = l.Command.SectorId,
                        Reason = l.Reason
                    })
                    .ToList()
            });
        });
    }

    /// <summary>
    /// Whether one order belongs in this viewer's account of the turn.
    ///
    /// Your own orders always. Somebody else's only where you have some belief about the ground it
    /// names — otherwise a turn report would quietly tell you the name of every sector on the map,
    /// which is the thing the state projection spends its whole existence preventing.
    ///
    /// </summary>
    /// <summary>
    /// Whether one report line belongs in this viewer's account of the turn.
    ///
    /// A line about nowhere in particular — a calendar tick, a command refused before it named any
    /// ground — is shown to everyone, because it reveals nothing about the map.
    /// </summary>
    static bool VisibleTo(string? sectorId, BelievedWorldView? believed)
    {
        if (believed is null || sectorId is null) return true;
        return believed.Believed(sectorId) is not null;
    }

    static bool VisibleTo(WorldCommand command, string? viewer, BelievedWorldView? believed)
    {
        if (viewer is null || believed is null) return true;
        if (string.Equals(command.CommanderId, viewer, StringComparison.Ordinal)) return true;

        // An order that names no ground says only "somebody, somewhere, did nothing".
        if (command.SectorId is not { } sectorId) return false;

        return believed.Believed(sectorId) is not null;
    }

    /// <summary>SIM-only world creation — mapped inside the /api/test group.</summary>
    public static void MapWorldTest(this RouteGroupBuilder test)
    {
        test.MapPost("/world/create", (CreateWorldRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var playerId = body.PlayerId ?? store.GetCurrentPlayerId();
            var worldId = string.IsNullOrWhiteSpace(body.WorldId) ? "world-1" : body.WorldId!.Trim();
            var templateId = string.IsNullOrWhiteSpace(body.TemplateId)
                ? WorldTemplateCatalog.FirstLightId
                : body.TemplateId!.Trim();

            if (!WorldTemplateCatalog.IsKnown(templateId))
                return Results.BadRequest(new { reason = "template.unknown", templateId });
            if (!ulong.TryParse(body.Seed, out var seed))
                seed = 1;

            var built = WorldTemplateCatalog.Build(templateId, seed, worldId);
            var (ok, reason, _) = store.CreateWorld(playerId, built);
            if (!ok)
                return reason == "world.exists"
                    ? Results.Conflict(new { reason })
                    : Results.BadRequest(new { reason });

            hub.Clients.All.SendAsync("WorldUpdated", new { worldId });
            return Results.Ok(new { ok = true, worldId, templateId });
        });
    }

    static WorldSectorDto ProjectSector(
        WorldSector sector, IWorldView view,
        (IReadOnlyDictionary<string, long> Cost, IReadOnlySet<string> Critical) lifelines,
        LoamReading loam)
    {
        var state = view.StateOf(sector.SectorId);
        var believed = view.Believed(sector.SectorId);

        // Never seen: where it is, and that is the whole payload.
        if (state == IntelState.Unknown || believed is null)
            return new WorldSectorDto
            {
                SectorId = sector.SectorId,
                Intel = IntelState.Unknown.ToString(),
                Phase = SectorPhase.Unknown.ToString(),
                LayoutX = sector.LayoutX,
                LayoutY = sector.LayoutY
            };

        // Owner-only: these dictionaries only ever contain the *viewer's own* holdings
        // (ComputeLoamReading is built over `TerritoryComponents.For(world, view.FactionId)`), so a
        // sector this faction does not own simply has no entry — gated structurally, not by a
        // per-field ownership check that could be forgotten on the next field added here.
        var production = loam.ProductionBySector.TryGetValue(sector.SectorId, out var p) ? p : 0;
        var upkeep = loam.UpkeepBySector.TryGetValue(sector.SectorId, out var u) ? u : 0;
        var stock = loam.StockBySector.TryGetValue(sector.SectorId, out var st) ? st : 0;
        var componentId = loam.ComponentIdBySector.TryGetValue(sector.SectorId, out var cid) ? cid : null;
        var componentTotals = componentId is not null && loam.ComponentTotals.TryGetValue(componentId, out var totals)
            ? totals
            : (Production: 0L, Upkeep: 0L, Stock: 0L);

        return new WorldSectorDto
        {
            SectorId = sector.SectorId,
            TypeId = sector.TypeId,
            Climate = believed.Climate?.ToString(),
            DangerBand = believed.DangerBand,
            DevelopmentLevel = believed.DevelopmentLevel,
            FractureIntensityMilli = believed.FractureIntensityMilli,
            Habitable = Habitability.For(believed.Slots.Select(sl => sl.SlotTypeId)),
            Phase = believed.Phase.ToString(),
            OwnerFactionId = believed.OwnerFactionId,

            // Pressure and depletion stay zero: nobody glances at a sector and reads its depletion
            // off, and until something models observing them there is nothing honest to send.
            // Stability is owner-only (spec-loam-fe.md) and read from truth directly, the same
            // pattern `LifelineCost` already uses for an owner-only number computed over the
            // viewer's own holdings.
            StabilityMilli = string.Equals(sector.OwnerFactionId, view.FactionId, StringComparison.Ordinal)
                ? sector.StabilityMilli
                : 0,
            Intel = state.ToString(),
            IntelAge = view.AgeOf(sector.SectorId),
            LastSeenTurn = believed.LastSeenTurn,
            LayoutX = sector.LayoutX,
            LayoutY = sector.LayoutY,

            Slots = believed.Slots.Select(sl => new WorldSlotDto
            {
                SlotIndex = sl.SlotIndex,
                SlotTypeId = sl.SlotTypeId,
                Element = sl.Element?.ToString(),
                State = sl.State.ToString(),
                GuardWaveId = sl.GuardWaveId,
                GuardState = sl.GuardState.ToString()
            }).ToList(),

            LifelineCost = lifelines.Cost.TryGetValue(sector.SectorId, out var cost) ? cost : 0,
            Lifeline = lifelines.Critical.Contains(sector.SectorId),

            LoamProduction = production,
            LoamUpkeep = upkeep,
            LoamNet = production - upkeep,
            ComponentId = componentId,
            ComponentProduction = componentTotals.Production,
            ComponentUpkeep = componentTotals.Upkeep,
            ComponentNet = componentTotals.Production - componentTotals.Upkeep,
            LoamStock = stock,
            ComponentStock = componentTotals.Stock,
            WillReleaseNextTurn = loam.ReleaseCandidates.Contains(sector.SectorId),

            Forces = believed.Forces.Select(f => new WorldForceDto
            {
                EntityId = f.EntityId,
                OwnerFactionId = f.OwnerFactionId,
                Kind = f.Kind.ToString(),
                Exact = f.Exact,
                Strength = f.Exact ? f.Strength : 0,
                BandName = StrengthBandCatalog.ByIndex(f.BandIndex).Name,
                BandCeiling = f.Defensive
            }).ToList()
        };
    }

    static WorldHeaderDto Project(WorldHeaderRow h) => new()
    {
        WorldId = h.WorldId,
        TemplateId = h.TemplateId,
        CurrentTurn = h.CurrentTurn,
        State = h.State,
        CreatedUtc = h.CreatedUtc,
        Revision = h.Revision
    };

    /// <summary>
    /// Projects a world through one faction's eyes. Nothing here reads `WorldState` for anything a
    /// viewer has not seen: sectors come from belief, forces come from belief, and the only entities
    /// returned are the viewer's own.
    ///
    /// The sector's layout position is always sent — the graph's shape is public knowledge, and a
    /// map you cannot draw is unusable — but an unseen sector carries nothing else at all.
    /// </summary>
    /// <summary>
    /// How load-bearing each of the viewer's own sectors is. Computed over their holdings alone, so
    /// it leaks nothing — it describes territory they can already see. Withholding it would be the
    /// computer having the fun: the AI reads exactly this to decide what to garrison.
    /// </summary>
    static readonly (IReadOnlyDictionary<string, long> Cost, IReadOnlySet<string> Critical) NoLifelines =
        (new Dictionary<string, long>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));

    static (IReadOnlyDictionary<string, long> Cost, IReadOnlySet<string> Critical) Lifelines(
        WorldState world, string factionId)
    {
        var holdings = world.Sectors
            .Where(s => string.Equals(s.OwnerFactionId, factionId, StringComparison.Ordinal))
            .Select(s => s.SectorId)
            .ToHashSet(StringComparer.Ordinal);

        if (holdings.Count == 0)
            return (new Dictionary<string, long>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));

        return (
            ReconnectionCost.For(world, holdings),
            ArticulationPoints.Find(LaneGraph.Build(world, holdings)));
    }

    /// <summary>
    /// Derived loam numbers (spec-loam-fe.md), computed from `loam-calc` at projection time and
    /// never stored. Built the same shape as <see cref="Lifelines"/>: computed once, over the
    /// viewer's own holdings only, so the gating is structural — a sector this faction does not own
    /// simply never appears in any of these maps, rather than being computed and then hidden.
    /// </summary>
    sealed record LoamReading(
        IReadOnlyDictionary<string, string> ComponentIdBySector,
        IReadOnlyDictionary<string, long> ProductionBySector,
        IReadOnlyDictionary<string, long> UpkeepBySector,
        IReadOnlyDictionary<string, long> StockBySector,
        IReadOnlyDictionary<string, (long Production, long Upkeep, long Stock)> ComponentTotals,
        IReadOnlySet<string> ReleaseCandidates);

    static readonly LoamReading NoLoamReading = new(
        new Dictionary<string, string>(StringComparer.Ordinal),
        new Dictionary<string, long>(StringComparer.Ordinal),
        new Dictionary<string, long>(StringComparer.Ordinal),
        new Dictionary<string, long>(StringComparer.Ordinal),
        new Dictionary<string, (long, long, long)>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal));

    static LoamReading ComputeLoamReading(WorldState world, string factionId)
    {
        var componentIdBySector = new Dictionary<string, string>(StringComparer.Ordinal);
        var productionBySector = new Dictionary<string, long>(StringComparer.Ordinal);
        var upkeepBySector = new Dictionary<string, long>(StringComparer.Ordinal);
        var stockBySector = new Dictionary<string, long>(StringComparer.Ordinal);
        var componentTotals = new Dictionary<string, (long, long, long)>(StringComparer.Ordinal);
        var releaseCandidates = new HashSet<string>(StringComparer.Ordinal);

        foreach (var component in TerritoryComponents.For(world, factionId))
        {
            var componentId = component[0];   // stable and meaningful on the wire — the spec's own choice
            long totalProduction = 0, totalUpkeep = 0, totalStock = 0;

            foreach (var sectorId in component)
            {
                var sector = world.Sectors.First(s => string.Equals(s.SectorId, sectorId, StringComparison.Ordinal));
                var production = LoamProduction.For(sector);
                var upkeep = LoamUpkeep.For(world, sector);

                componentIdBySector[sectorId] = componentId;
                productionBySector[sectorId] = production;
                upkeepBySector[sectorId] = upkeep;
                stockBySector[sectorId] = sector.LoamStock;
                totalProduction += production;
                totalUpkeep += upkeep;
                totalStock += sector.LoamStock;
            }

            componentTotals[componentId] = (totalProduction, totalUpkeep, totalStock);

            // A turn-early warning for spec-loam-fe.md's abandonment surface, computed by the same
            // static the engine's own Pressure phase uses to pick who takes the fade — reusing it
            // rather than re-deriving the selection is what keeps this from silently disagreeing
            // with what actually happens next turn.
            var willRelease = LoamForecast.WillRelease(world, component);
            if (willRelease is not null)
                releaseCandidates.Add(willRelease);
        }

        return new LoamReading(componentIdBySector, productionBySector, upkeepBySector, stockBySector, componentTotals, releaseCandidates);
    }

    static WorldStateDto Project(
        WorldState w, IWorldView view,
        (IReadOnlyDictionary<string, long> Cost, IReadOnlySet<string> Critical) lifelines)
    {
        var loam = ComputeLoamReading(w, view.FactionId);

        return new WorldStateDto
        {
            WorldId = w.WorldId,
            TemplateId = w.TemplateId,
            CurrentTurn = w.CurrentTurn,
            Factions = w.Factions.Select(f => new WorldFactionDto
            {
                FactionId = f.FactionId,
                Kind = f.Kind.ToString(),
                Name = f.Name
            }).ToList(),
            Sectors = w.Sectors.Select(s => ProjectSector(s, view, lifelines, loam)).ToList(),
        Lanes = view.Lanes.Select(l => new WorldLaneDto
        {
            LaneId = l.LaneId,
            FromSectorId = l.FromSectorId,
            ToSectorId = l.ToSectorId,
            TypeId = l.TypeId,
            Length = l.Length,
            Width = l.Width,
            HazardMilli = l.HazardMilli,
            WardLevel = l.WardLevel,
            State = l.State.ToString()
        }).ToList(),
        Entities = view.OwnForces.Select(e => new WorldEntityDto
        {
            EntityId = e.EntityId,
            Kind = e.Kind.ToString(),
            OwnerFactionId = e.OwnerFactionId,
            AtSectorId = e.AtSectorId,
            OnLaneId = e.OnLaneId,
            OnLaneTowardSectorId = e.OnLaneTowardSectorId,
            LaneProgressMilli = e.LaneProgressMilli,
            Stance = e.Stance,
            MovementRemaining = e.MovementRemaining,
            Routed = e.Routed,
            Members = e.Members.Select(m => new WorldEntityMemberDto
            {
                InstanceId = m.InstanceId,
                SpeciesId = m.SpeciesId,
                Level = m.Level,
                Hp = m.Hp,
                Wounds = m.Wounds
            }).ToList()
        }).ToList()
        };
    }
}
