using System.Text.Json;
using FusionRpg.Core.Items.Materials;
using FusionRpg.Data;

namespace FusionRpg.Server;

/// <summary>
/// The item program's <b>write</b> surface — the workbench. Deliberately its own file rather than a
/// <c>MapPost</c> inside <c>ItemSurfaceEndpoints.cs</c>: module 20 is read-only by construction, and
/// a write path through the presentation layer is the "second surface" that module exists to prevent.
/// The verbs here belong to modules 14, 15 and 16, and each route is a thin shell over
/// <see cref="ItemWorkbench"/> — no policy, no pricing and no persistence decisions live in this file.
///
/// <para><b><c>correlationId</c> is required, not optional.</b> Every verb is a spend, and a spend
/// without an idempotency key is a double-spend waiting for a network retry. It is the same key
/// <c>rpg_material_spend_log</c> and <c>effect_instance_op</c> are both unique on, so one retried
/// request returns the recorded outcome from both.</para>
/// </summary>
public static class WorkbenchEndpoints
{
    public sealed record SalvageRequest(long? PlayerId, string? InstanceId);

    public sealed record UpcycleRequest(long? PlayerId, string? RecipeId, string? CorrelationId);

    public sealed record EnhanceRequest(
        long? PlayerId, string? InstanceId, string? RecipeId, string? CorrelationId, bool? WardLoaded);

    public sealed record SocketAddRequest(
        long? PlayerId, string? InstanceId, string? RecipeId, string? CorrelationId);

    public sealed record SocketInsertRequest(
        long? PlayerId, string? InstanceId, string? RecipeId, string? InsertContainerId, int? SocketIndex,
        string? CorrelationId);

    public sealed record SocketImbueRequest(
        long? PlayerId, string? InstanceId, string? RecipeId, int? SocketIndex, string? Element,
        string? CorrelationId);

    /// <summary>
    /// One recipe as a picker offers it — item-content <c>item-naming</c> T4.
    /// </summary>
    /// <param name="Name">The corpus's own authored English name (<c>"Forge: Cloth Armor"</c>), or
    /// <c>""</c> for an entry that authors none. The client shows the recipe id ONLY in the row's
    /// secondary line, never in the name slot.</param>
    public sealed record WorkbenchRecipeDto(
        string RecipeId, string Name, string Operation, string Frame, string OutputKind, string? OutputRef);

    /// <summary>One insert the player actually holds, named — item-content <c>item-naming</c> T4.</summary>
    /// <param name="Name">The gem corpus's authored name (<c>"Ember Shard"</c>), or <c>""</c> for a
    /// container the corpus does not carry. `Element` is <c>""</c> for a genuinely element-free
    /// insert, which is a legitimate value and not an absent read.</param>
    public sealed record WorkbenchInsertDto(string ContainerId, string Name, string Element, long Qty);

    public static void MapWorkbench(this WebApplication app, ItemWorkbench bench)
    {
        if (bench is null) throw new ArgumentNullException(nameof(bench));

        // The bench's OWN gem corpus — the same delegate it prices `socket-insert` against. Read off
        // the bench rather than passed in again: a picker named by a different catalog than the one
        // that prices filling the socket is how two surfaces come to disagree about what a gem is
        // called. `null` serves the held list with empty names, which the client renders as
        // "unnamed" rather than as the container id.
        var lookupInsert = bench.LookupInsert;

        // ⭐ The ONE read in this file, and it is why it is here rather than in the read-only surfaces
        // file: the list a picker offers must be the list the executor below prices against, and
        // `ItemWorkbench.Recipes` is that exact corpus. A recipe read from anywhere else could offer a
        // row the very next POST would refuse with `material.recipe-unknown`.
        //
        // ⏸ Until item-content T4 (2026-09-06) NO route served these 30 rows at all, so the craft and
        // socket benches asked the player to TYPE `recipe.014`. `GET /api/recipes` is the PvZ fusion
        // table and a different thing entirely.
        app.MapGet("/api/items/workbench/recipes", (string? operation) =>
        {
            var rows = bench.Recipes.Recipes.Values
                .Where(r => operation is not { Length: > 0 } wanted ||
                            string.Equals(CraftOperations.Id(r.Operation), wanted, StringComparison.Ordinal))
                .OrderBy(r => CraftOperations.Id(r.Operation), StringComparer.Ordinal)
                .ThenBy(r => r.RecipeId, StringComparer.Ordinal)
                .Select(r => new WorkbenchRecipeDto(
                    r.RecipeId, r.Name, CraftOperations.Id(r.Operation), r.Frame, r.OutputKind, r.OutputRef))
                .ToList();
            return Results.Ok(rows);
        });

        // The inserts the player actually holds, named — so "set an insert" is a pick, not a typed
        // container id. The `gem.` prefix is the same filter the combinations route already applies to
        // stock (ItemSurfaceEndpoints), not a new rule invented here.
        app.MapGet("/api/items/workbench/inserts/{playerId}", (string playerId, RpgStore store) =>
        {
            var rows = store.ListStock(playerId)
                .Where(s => s.ContainerId.StartsWith("gem.", StringComparison.Ordinal) && s.Qty > 0)
                .OrderBy(s => s.ContainerId, StringComparer.Ordinal)
                .Select(s =>
                {
                    var found = lookupInsert?.Invoke(s.ContainerId);
                    return new WorkbenchInsertDto(
                        s.ContainerId, found?.Name ?? "", found?.Def.Element ?? "", s.Qty);
                })
                .ToList();
            return Results.Ok(rows);
        });

        app.MapPost("/api/items/workbench/salvage", (SalvageRequest body, RpgStore store) =>
        {
            if (body.InstanceId is not { Length: > 0 } instanceId)
                return Results.BadRequest(new { error = "instanceId required" });
            return Render(bench.Salvage(body.PlayerId ?? store.GetCurrentPlayerId(), instanceId));
        });

        app.MapPost("/api/items/workbench/upcycle", (UpcycleRequest body, RpgStore store) =>
        {
            if (body.RecipeId is not { Length: > 0 } recipeId)
                return Results.BadRequest(new { error = "recipeId required" });
            if (body.CorrelationId is not { Length: > 0 } correlationId)
                return Results.BadRequest(new { error = "correlationId required — a spend without one is not retry-safe" });
            return Render(bench.Upcycle(body.PlayerId ?? store.GetCurrentPlayerId(), recipeId, correlationId));
        });

        app.MapPost("/api/items/workbench/enhance", (EnhanceRequest body, RpgStore store) =>
        {
            if (body.InstanceId is not { Length: > 0 } instanceId)
                return Results.BadRequest(new { error = "instanceId required" });
            if (body.RecipeId is not { Length: > 0 } recipeId)
                return Results.BadRequest(new { error = "recipeId required" });
            if (body.CorrelationId is not { Length: > 0 } correlationId)
                return Results.BadRequest(new { error = "correlationId required — a spend without one is not retry-safe" });
            return Render(bench.Enhance(
                body.PlayerId ?? store.GetCurrentPlayerId(), instanceId, recipeId, correlationId,
                body.WardLoaded ?? false));
        });

        app.MapPost("/api/items/workbench/socket-add", (SocketAddRequest body, RpgStore store) =>
        {
            if (body.InstanceId is not { Length: > 0 } instanceId)
                return Results.BadRequest(new { error = "instanceId required" });
            if (body.RecipeId is not { Length: > 0 } recipeId)
                return Results.BadRequest(new { error = "recipeId required" });
            if (body.CorrelationId is not { Length: > 0 } correlationId)
                return Results.BadRequest(new { error = "correlationId required — a spend without one is not retry-safe" });
            return Render(bench.SocketAdd(
                body.PlayerId ?? store.GetCurrentPlayerId(), instanceId, recipeId, correlationId));
        });

        app.MapPost("/api/items/workbench/socket-insert",
            (SocketInsertRequest body, RpgStore store) =>
        {
            if (body.InstanceId is not { Length: > 0 } instanceId)
                return Results.BadRequest(new { error = "instanceId required" });
            if (body.RecipeId is not { Length: > 0 } recipeId)
                return Results.BadRequest(new { error = "recipeId required" });
            if (body.InsertContainerId is not { Length: > 0 } insertContainerId)
                return Results.BadRequest(new { error = "insertContainerId required" });
            if (body.CorrelationId is not { Length: > 0 } correlationId)
                return Results.BadRequest(new { error = "correlationId required — a spend without one is not retry-safe" });
            return Render(bench.SocketInsert(
                body.PlayerId ?? store.GetCurrentPlayerId(), instanceId, recipeId, insertContainerId,
                body.SocketIndex, correlationId));
        });

        app.MapPost("/api/items/workbench/socket-imbue",
            (SocketImbueRequest body, RpgStore store) =>
        {
            if (body.InstanceId is not { Length: > 0 } instanceId)
                return Results.BadRequest(new { error = "instanceId required" });
            if (body.RecipeId is not { Length: > 0 } recipeId)
                return Results.BadRequest(new { error = "recipeId required" });
            if (body.SocketIndex is not { } socketIndex)
                return Results.BadRequest(new { error = "socketIndex required" });
            if (body.Element is not { Length: > 0 } element)
                return Results.BadRequest(new { error = "element required" });
            if (body.CorrelationId is not { Length: > 0 } correlationId)
                return Results.BadRequest(new { error = "correlationId required — a spend without one is not retry-safe" });
            return Render(bench.SocketImbue(
                body.PlayerId ?? store.GetCurrentPlayerId(), instanceId, recipeId, socketIndex, element,
                correlationId));
        });
    }

    /// <summary>
    /// A refused operation is <b>409 with the named rule</b>, never a 200 carrying a sad face and never
    /// a bare 400: the request was well formed and the answer is "the content rules say no", which is
    /// the same distinction <c>LoadoutEndpoints</c> already draws. The body is identical either way, so
    /// a caller renders one shape.
    /// </summary>
    static IResult Render(WorkbenchOutcomeDto outcome) =>
        outcome.Ok ? Results.Ok(outcome) : Results.Json(outcome, statusCode: StatusCodes.Status409Conflict);
}

/// <summary>
/// ⏸ <b>A stopgap over module 6's missing <c>item_base_type</c> table</b>, and it says so rather than
/// pretending to be the table. Module 6 shipped the 740-entry base-type corpus as seed JSON and the
/// Core readers, but no table and no loader, so <c>socketMax</c> — a base type's own declared value,
/// which <c>SocketGeometry</c> takes as a parameter and never looks up — has nowhere to come from at
/// runtime. This reads it straight off the shipped corpus at boot.
///
/// <para>The day module 6 lands the table, this class is deleted and the delegate reads the table.
/// It is deliberately a <c>Func</c> at the <see cref="ItemWorkbench"/> boundary so that swap costs one
/// line — the same seam <c>LootContentView.SocketMaxFor</c> already uses.</para>
/// </summary>
public static class BaseTypeSocketMaxCorpus
{
    /// <summary>
    /// Returns <c>null</c> for an id the corpus does not carry, which the workbench refuses by name.
    /// An absent directory yields a lookup that always returns <c>null</c> — the socket verbs then
    /// refuse rather than guessing, which is <c>LootPipeline.Sockets</c>'s own rule.
    /// </summary>
    public static Func<string, int?> Load(string baseTypesDir)
    {
        var byId = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!Directory.Exists(baseTypesDir)) return _ => null;

        foreach (var file in Directory.EnumerateFiles(baseTypesDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(File.ReadAllText(file)); }
            catch (JsonException) { continue; }

            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("entries", out var entries) ||
                    entries.ValueKind != JsonValueKind.Array) continue;

                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String) continue;
                    if (!entry.TryGetProperty("socketMax", out var max) || max.ValueKind != JsonValueKind.Number)
                        continue;
                    byId[id.GetString()!] = max.GetInt32();
                }
            }
        }

        return baseTypeId => byId.TryGetValue(baseTypeId, out var value) ? value : null;
    }

    /// <summary>An explicit lookup for tests and for a host with no corpus on disk.</summary>
    public static Func<string, int?> From(IReadOnlyDictionary<string, int> byId) =>
        baseTypeId => byId.TryGetValue(baseTypeId, out var value) ? value : null;
}
