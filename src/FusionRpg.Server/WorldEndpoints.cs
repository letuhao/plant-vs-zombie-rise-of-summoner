using FusionRpg.Contracts;
using FusionRpg.Core.World;
using FusionRpg.Core.World.Turn;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

/// <summary>
/// World map reads (spec-world-model.md §Server). This module is read-only by design — the turn
/// engine owns every mutation, so there is no endpoint here that can change a world.
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

        g.MapGet("/{worldId}/state", (string worldId, RpgStore store) =>
        {
            var world = store.LoadWorldState(worldId);
            return world is null ? Results.NotFound() : Results.Ok(Project(world));
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

    static WorldHeaderDto Project(WorldHeaderRow h) => new()
    {
        WorldId = h.WorldId,
        TemplateId = h.TemplateId,
        CurrentTurn = h.CurrentTurn,
        State = h.State,
        CreatedUtc = h.CreatedUtc,
        Revision = h.Revision
    };

    static WorldStateDto Project(WorldState w) => new()
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
        Sectors = w.Sectors.Select(s => new WorldSectorDto
        {
            SectorId = s.SectorId,
            TypeId = s.TypeId,
            Climate = s.Climate?.ToString(),
            DangerBand = s.DangerBand,
            Phase = s.Phase.ToString(),
            OwnerFactionId = s.OwnerFactionId,
            StabilityMilli = s.StabilityMilli,
            PressureMilli = s.PressureMilli,
            DepletionMilli = s.DepletionMilli,
            DevelopmentLevel = s.DevelopmentLevel,
            Intel = s.Intel.ToString(),
            LastSeenTurn = s.LastSeenTurn,
            LayoutX = s.LayoutX,
            LayoutY = s.LayoutY,
            Slots = s.Slots.Select(sl => new WorldSlotDto
            {
                SlotIndex = sl.SlotIndex,
                SlotTypeId = sl.SlotTypeId,
                Element = sl.Element?.ToString(),
                State = sl.State.ToString(),
                OwnerFactionId = sl.OwnerFactionId,
                GuardWaveId = sl.GuardWaveId,
                GuardState = sl.GuardState.ToString()
            }).ToList()
        }).ToList(),
        Lanes = w.Lanes.Select(l => new WorldLaneDto
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
        Entities = w.Entities.Select(e => new WorldEntityDto
        {
            EntityId = e.EntityId,
            Kind = e.Kind.ToString(),
            OwnerFactionId = e.OwnerFactionId,
            AtSectorId = e.AtSectorId,
            OnLaneId = e.OnLaneId,
            LaneProgressMilli = e.LaneProgressMilli,
            Stance = e.Stance,
            MovementRemaining = e.MovementRemaining,
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
