using FusionRpg.Contracts;
using FusionRpg.Core.World;
using FusionRpg.Core.World.Intel;
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

        // The only place fog can leak, which is why it takes a viewer and refuses an unknown one
        // rather than quietly falling back to omniscience.
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

            var result = store.CommitWorldTurn(worldId, commanderId);
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

        g.MapGet("/{worldId}/turn/{turn:int}", (string worldId, int turn, RpgStore store) =>
        {
            var log = store.GetWorldTurnLog(worldId, turn);
            if (log is null) return Results.NotFound();

            // Reports outside the hot tail are re-derived by replay, and re-derivation refuses
            // rather than fabricating across an engine version change — so this can legitimately
            // come back empty for an old turn played by an older ruleset.
            var report = store.GetWorldTurnReport(worldId, turn);

            return Results.Ok(new WorldTurnReportDto
            {
                Turn = turn,
                StateHash = log.StateHash,
                Phases = report?.Phases.ToList() ?? new List<string>(),
                Entries = report?.Entries
                    .Select(e => new WorldTurnEntryDto
                    {
                        Phase = e.Phase, Kind = e.Kind, Subject = e.Subject, Detail = e.Detail
                    })
                    .ToList() ?? new List<WorldTurnEntryDto>()
            });
        });
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
        (IReadOnlyDictionary<string, long> Cost, IReadOnlySet<string> Critical) lifelines)
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

        return new WorldSectorDto
        {
            SectorId = sector.SectorId,
            TypeId = sector.TypeId,
            Climate = believed.Climate?.ToString(),
            DangerBand = believed.DangerBand,
            DevelopmentLevel = believed.DevelopmentLevel,
            Phase = believed.Phase.ToString(),
            OwnerFactionId = believed.OwnerFactionId,

            // Stability, pressure and depletion stay zero: nobody glances at a sector and reads its
            // depletion off, and until something models observing them there is nothing honest to
            // send. Development is different — you can see how built-up ground is by standing on it.
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

    static WorldStateDto Project(
        WorldState w, IWorldView view,
        (IReadOnlyDictionary<string, long> Cost, IReadOnlySet<string> Critical) lifelines) => new()
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
        Sectors = w.Sectors.Select(s => ProjectSector(s, view, lifelines)).ToList(),
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
