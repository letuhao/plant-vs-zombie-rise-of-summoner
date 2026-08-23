using FusionRpg.Contracts;
using FusionRpg.Core.Match;

namespace FusionRpg.Server;

/// <summary>
/// T14 — a small, real, seeded relic catalog. No acquisition system exists yet, so every
/// player holds the full catalog (see game-gui-todo.md's honest scoping note); equipping a
/// relic to an actor goes through the existing `/api/unique/actors/{id}/equipment` routes.
/// </summary>
public static class RelicEndpoints
{
    public static void MapRelics(this WebApplication app)
    {
        app.MapGet("/api/relics", () => Results.Ok(new RelicCatalogListDto
        {
            Items = RelicCatalog.Items.ToList()
        }));
    }
}
