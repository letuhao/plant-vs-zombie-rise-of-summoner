using FusionRpg.Core.Demons;
using FusionRpg.Data;

namespace FusionRpg.Server;

/// <summary>zomboss-deploy-ai T3.4 request body — <see cref="MatchSeed"/> is the SAME
/// `SeededRng.DeriveStream(0, matchKey).NextULong()` value the injector already computed to call
/// `ZombossDeployPolicy.Decide`, threaded through here so the trait roll this endpoint performs
/// (`RpgStore.MintForZomboss`) derives from that same per-match seed rather than a fresh, non-
/// reproducible one — matching this program's own "everything derives from matchKey" discipline.</summary>
public sealed class ZombossDeployRequest
{
    public string SpeciesId { get; set; } = "";
    public ulong MatchSeed { get; set; }
    public string? MatchKey { get; set; }
    public string? CorrelationId { get; set; }
    public int? Col { get; set; }
    public int? Row { get; set; }
}

public static class ZombossDeployEndpoints
{
    public static void MapZombossDeploy(this WebApplication app)
    {
        var g = app.MapGroup("/api/zomboss");

        // zomboss-deploy-ai T3.4 (Correction 3): composes two ALREADY-EXISTING, already-proven
        // primitives (MintDemon, UniqueActorService.DeployAsync) — no new write logic, no shortcut.
        // The decision (whether/which) was already made injector-side by ZombossDeployPolicy.Decide,
        // which needs live board state (ILawnBoardView) this server process never sees; this endpoint
        // performs only the privileged DB half that decision cannot reach on its own.
        g.MapPost("/deploy", async (ZombossDeployRequest? body, RpgStore store, UniqueActorService ua) =>
        {
            body ??= new ZombossDeployRequest();
            if (string.IsNullOrWhiteSpace(body.SpeciesId))
                return Results.BadRequest(new { error = "speciesId required" });
            if (!DemonSpeciesCatalog.IsKnown(body.SpeciesId))
                return Results.BadRequest(new { error = "unknown speciesId '" + body.SpeciesId + "'" });

            var specimen = store.MintForZomboss(body.SpeciesId, body.MatchSeed);

            var correlationId = string.IsNullOrWhiteSpace(body.CorrelationId)
                ? Guid.NewGuid().ToString("N")
                : body.CorrelationId;
            var result = await ua.DeployAsync(
                specimen.Actor.InstanceId, correlationId, body.Col, body.Row, body.MatchKey);

            if (!result.Ok && result.Reason == "not_found")
                return Results.NotFound(result);
            if (!result.Ok)
                return Results.Conflict(result);
            return Results.Ok(new { result.Ok, result.Reason, result.Queued, result.CorrelationId, result.Actor, specimen });
        });
    }
}
