using FusionRpg.Contracts;
using FusionRpg.Core.Onboarding;
using FusionRpg.Core.Progression;
using FusionRpg.Data;

namespace FusionRpg.Server;

/// <summary>Server adapter for the durable first-session checkpoint ledger.</summary>
public static class OnboardingEndpoints
{
    public static void MapOnboarding(this WebApplication app)
    {
        var g = app.MapGroup("/api/onboarding");

        g.MapGet("/{playerId:long}", (long playerId, RpgStore store) =>
        {
            var state = ProjectState(store, playerId);
            return state is null ? Results.NotFound() : Results.Ok(state);
        });

        g.MapPost("/{playerId:long}/checkpoints/{checkpointId}/claim",
            (long playerId, string checkpointId, RpgStore store) =>
            {
                if (!store.PlayerExists(playerId)) return Results.NotFound();
                var result = store.ClaimOnboardingCheckpoint(playerId, checkpointId);
                var body = new OnboardingClaimDto
                {
                    Ok = result.Ok,
                    Reason = result.Reason,
                    Checkpoint = result.Row is null ? null : ProjectCheckpoint(result.Row)
                };
                return result.Ok ? Results.Ok(body) : Results.Json(body, statusCode: StatusCodes.Status409Conflict);
            });
    }

    internal static OnboardingStateDto? ProjectState(RpgStore store, long playerId)
    {
        if (!store.PlayerExists(playerId)) return null;
        var rows = store.ListOnboardingCheckpoints(playerId) ?? Array.Empty<RpgStore.OnboardingCheckpointRow>();
        var player = store.GetRpgActor(playerId, RpgActorKinds.Player, 0)
                     ?? store.GetRpgProgressionSummary(playerId)?.Player;
        return new OnboardingStateDto
        {
            PlayerId = playerId,
            PlayerLevel = player?.Level ?? 1,
            Revision = rows.Count == 0 ? 0 : rows.Max(x => x.Revision),
            Checkpoints = rows.Select(ProjectCheckpoint).ToList()
        };
    }

    static OnboardingCheckpointDto ProjectCheckpoint(RpgStore.OnboardingCheckpointRow row) => new()
    {
        CheckpointId = row.CheckpointId,
        State = row.State,
        EarnedRunId = row.EarnedRunId,
        RewardRef = row.RewardRef,
        PayloadJson = row.PayloadJson,
        EarnedUtc = row.EarnedUtc,
        ClaimedUtc = row.ClaimedUtc,
        Revision = row.Revision
    };
}
