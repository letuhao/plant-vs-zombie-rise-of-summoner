using FusionRpg.Core.Demons;
using FusionRpg.Data;

namespace FusionRpg.Server;

/// <summary>Souls reads (spec-soul-economy.md). Spends are feature-owned (summoning etc.), never generic.</summary>
public static class SoulEndpoints
{
    public static void MapSouls(this WebApplication app)
    {
        var g = app.MapGroup("/api/souls");

        g.MapGet("/{playerId:long}", (long playerId, RpgStore store) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            return Results.Ok(store.GetSoulBalance(playerId));
        });

        g.MapGet("/{playerId:long}/ledger", (long playerId, RpgStore store, int? limit, long? afterId) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            return Results.Ok(store.ListSoulLedger(playerId, limit ?? 100, afterId ?? 0));
        });
    }

    /// <summary>SIM-only seed — mapped inside the /api/test group.</summary>
    public static void MapSoulTestSeed(this RouteGroupBuilder test)
    {
        test.MapPost("/seed-souls-demo", (RpgStore store, long? playerId, long? amount) =>
        {
            var pid = playerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            // Clamp: huge seeds overflow SQLite integer addition into REAL and corrupt the balance.
            var delta = Math.Clamp(amount ?? 1000, 1, 1_000_000);
            var (_, balance) = store.AwardSouls(
                pid, delta, SoulEarnPolicy.Reasons.Seed, "seed-" + Guid.NewGuid().ToString("N"));
            return Results.Ok(balance);
        });
    }
}
