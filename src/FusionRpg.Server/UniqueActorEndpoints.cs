using FusionRpg.Contracts;
using FusionRpg.Data;

namespace FusionRpg.Server;

public static class UniqueActorEndpoints
{
    public static void MapUniqueActors(this WebApplication app)
    {
        var g = app.MapGroup("/api/unique");

        g.MapGet("/actors", (RpgStore store, long? playerId) =>
        {
            var pid = playerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            return Results.Ok(store.ListUniqueActors(pid));
        });

        g.MapGet("/actors/{instanceId}", (string instanceId, UniqueActorService ua) =>
        {
            var row = ua.Get(instanceId);
            return row is null ? Results.NotFound() : Results.Ok(row);
        });

        g.MapPost("/actors", (CreateUniqueActorRequest body, RpgStore store, UniqueActorService ua) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            try
            {
                return Results.Ok(ua.Create(pid, body.Side, body.TypeId));
            }
            catch (ArgumentOutOfRangeException)
            {
                return Results.BadRequest(new { error = "typeId" });
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound();
            }
        });

        g.MapPost("/actors/{instanceId}/deploy", async (
            string instanceId,
            DeployUniqueActorRequest? body,
            UniqueActorService ua) =>
        {
            body ??= new DeployUniqueActorRequest();
            var result = await ua.DeployAsync(
                instanceId,
                body.CorrelationId,
                body.Col,
                body.Row,
                body.MatchKey,
                body.LoadoutJson);
            if (!result.Ok && result.Reason == "not_found")
                return Results.NotFound(result);
            if (!result.Ok)
                return Results.Conflict(result);
            return Results.Ok(result);
        });

        g.MapPost("/actors/{instanceId}/fail-deploy", (string instanceId, UniqueActorService ua) =>
        {
            var (ok, reason, actor) = ua.FailDeploy(instanceId);
            if (reason == "not_found") return Results.NotFound();
            if (!ok) return Results.Conflict(new { ok, reason, actor });
            return Results.Ok(actor);
        });

        g.MapPost("/actors/{instanceId}/retire", (string instanceId, UniqueActorService ua) =>
        {
            var (ok, reason, actor) = ua.Retire(instanceId);
            if (reason == "not_found") return Results.NotFound();
            if (!ok) return Results.Conflict(new { ok, reason, actor });
            return Results.Ok(actor);
        });

        g.MapGet("/actors/{instanceId}/equipment", (string instanceId, UniqueActorService ua) =>
        {
            var eq = ua.GetEquipment(instanceId);
            return eq is null ? Results.NotFound() : Results.Ok(eq);
        });

        g.MapPut("/actors/{instanceId}/equipment/{slot}", (
            string instanceId,
            string slot,
            PutUniqueEquipmentRequest? body,
            UniqueActorService ua) =>
        {
            body ??= new PutUniqueEquipmentRequest();
            var (ok, reason, eq) = ua.PutEquipment(instanceId, slot, body.ItemId);
            if (reason == "not_found") return Results.NotFound(new { ok, reason });
            if (!ok && IsValidationReason(reason))
                return Results.BadRequest(new { ok, reason, equipment = eq });
            if (!ok) return Results.Conflict(new { ok, reason, equipment = eq });
            return Results.Ok(eq);
        });

        g.MapDelete("/actors/{instanceId}/equipment/{slot}", (
            string instanceId,
            string slot,
            UniqueActorService ua) =>
        {
            var (ok, reason, eq) = ua.ClearEquipment(instanceId, slot);
            if (reason == "not_found") return Results.NotFound(new { ok, reason });
            if (!ok && IsValidationReason(reason))
                return Results.BadRequest(new { ok, reason, equipment = eq });
            if (!ok) return Results.Conflict(new { ok, reason, equipment = eq });
            return Results.Ok(eq);
        });

        g.MapPost("/actors/{instanceId}/xp", (
            string instanceId,
            AwardUniqueActorXpRequest? body,
            UniqueActorService ua) =>
        {
            body ??= new AwardUniqueActorXpRequest();
            var (ok, reason, actor) = ua.AwardXp(instanceId, body.Delta, body.Reason);
            if (reason == "not_found") return Results.NotFound(new { ok, reason });
            if (!ok && IsValidationReason(reason))
                return Results.BadRequest(new { ok, reason, actor });
            if (!ok) return Results.Conflict(new { ok, reason, actor });
            return Results.Ok(actor);
        });
    }

    static bool IsValidationReason(string reason) =>
        reason is "bad_delta" or "bad_args" or "bad_slot" or "unknown_item" or "slot_mismatch";
}
