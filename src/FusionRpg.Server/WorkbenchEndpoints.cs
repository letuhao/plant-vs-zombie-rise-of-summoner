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

    public static void MapWorkbench(this WebApplication app, ItemWorkbench bench)
    {
        if (bench is null) throw new ArgumentNullException(nameof(bench));

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
