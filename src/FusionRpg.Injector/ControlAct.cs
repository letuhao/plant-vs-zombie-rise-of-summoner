using System.Text.Json;
using FusionRpg.Injector.Lawn;

namespace FusionRpg.Injector;

/// <summary>
/// Lawn verb chains (game-control module <c>control-act</c>). Composes the invoke
/// primitives <c>control-click</c> already owns — no raw engine writes here. Each verb
/// resolves, invokes, and emits a receipt naming the expected telemetry kind; the
/// read-back itself runs through the normal event path (a follow-up poll, performed by
/// the caller or the MCP tool), never from the response alone.
/// </summary>
public static class ControlAct
{
    // Per-call tag echoed in the receipt for poll correlation. Written once per Drain
    // dispatch (single-threaded) and read by the verb below — never shared state.
    static string _tag = "";

    public static void Run(JsonElement p)
    {
        _tag = "";
        string verb = "";
        try
        {
            if (p.TryGetProperty("tag", out var tagEl) && tagEl.ValueKind == JsonValueKind.String)
                _tag = tagEl.GetString() ?? "";
            verb = (Str(p, "verb") ?? "").Trim().ToLowerInvariant();
            switch (verb)
            {
                case "place": DoPlace(p); break;
                case "shovel": DoShovel(p); break;
                case "hammer": DoHammer(); break;
                default:
                    CheatState.Error("debug.act: unknown verb '" + verb + "' (place|shovel|hammer)");
                    break;
            }
        }
        catch (Exception ex)
        {
            CheatState.Error("debug.act " + verb + ": " + ex.Message);
        }
    }

    static void DoPlace(JsonElement p)
    {
        var typeId = Int(p, "typeId", -1);
        var col = LawnCoords.ClampCol(Int(p, "col", CheatState.SpawnCol));
        var row = LawnCoords.ClampRow(Int(p, "row", CheatState.SpawnRow));
        if (typeId < 0)
        {
            CheatState.Error("debug.act place: typeId is required (resolve link)");
            return;
        }
        CardUI? card = null;
        try
        {
            foreach (var c in UnityEngine.Object.FindObjectsOfType<CardUI>())
            {
                if (c == null) continue;
                try
                {
                    if ((int)c.thePlantType == typeId) { card = c; break; }
                }
                catch { /* keep scanning */ }
            }
        }
        catch (Exception ex)
        {
            CheatState.Error("debug.act place: card scan failed (resolve link): " + ex.Message);
            return;
        }
        if (card == null)
        {
            CheatState.Error($"debug.act place: no CardUI for typeId {typeId} (resolve link)");
            return;
        }
        var mouse = Mouse.Instance;
        if (mouse == null)
        {
            CheatState.Error("debug.act place: no Mouse.Instance (resolve link)");
            return;
        }
        try
        {
            mouse.theMouseColumn = col;
            mouse.theMouseRow = row;
            mouse.ClickOnCard(card);
            mouse.TryToSetPlantByCard();
        }
        catch (Exception ex)
        {
            CheatState.Error("debug.act place: invoke link failed: " + ex.Message);
            return;
        }
        CheatState.SpawnCol = col;
        CheatState.SpawnRow = row;
        DebugRuntime.Emit("debug.act.done", new Dictionary<string, object>
        {
            ["tag"] = _tag,
            ["verb"] = "place",
            ["typeId"] = typeId,
            ["col"] = col,
            ["row"] = row,
            ["expectKind"] = "card.place",
            ["snapshot"] = ControlRefs.CurrentSnapshot()
        });
        CheatState.Note($"debug act place {typeId} @{col},{row} (expect card.place telemetry)");
    }

    static void DoShovel(JsonElement p)
    {
        var col = LawnCoords.ClampCol(Int(p, "col", CheatState.SpawnCol));
        var row = LawnCoords.ClampRow(Int(p, "row", CheatState.SpawnRow));
        var mouse = Mouse.Instance;
        if (mouse == null)
        {
            CheatState.Error("debug.act shovel: no Mouse.Instance (resolve link)");
            return;
        }
        Shovel? shovel = null;
        try
        {
            foreach (var s in UnityEngine.Object.FindObjectsOfType<Shovel>())
            {
                if (s != null) { shovel = s; break; }
            }
        }
        catch (Exception ex)
        {
            CheatState.Error("debug.act shovel: shovel scan failed (resolve link): " + ex.Message);
            return;
        }
        if (shovel == null)
        {
            CheatState.Error("debug.act shovel: no live Shovel tool (resolve link)");
            return;
        }
        try
        {
            mouse.theMouseColumn = col;
            mouse.theMouseRow = row;
            shovel.Use(mouse);
        }
        catch (Exception ex)
        {
            CheatState.Error("debug.act shovel: invoke link failed: " + ex.Message);
            return;
        }
        DebugRuntime.Emit("debug.act.done", new Dictionary<string, object>
        {
            ["tag"] = _tag,
            ["verb"] = "shovel",
            ["col"] = col,
            ["row"] = row,
            ["expectKind"] = "plant.shovel",
            ["snapshot"] = ControlRefs.CurrentSnapshot()
        });
        CheatState.Note($"debug act shovel @{col},{row} (expect plant.shovel telemetry)");
    }

    static void DoHammer()
    {
        var mouse = Mouse.Instance;
        if (mouse == null)
        {
            CheatState.Error("debug.act hammer: no Mouse.Instance (resolve link)");
            return;
        }
        Hammer? hammer = null;
        try
        {
            foreach (var h in UnityEngine.Object.FindObjectsOfType<Hammer>())
            {
                if (h != null) { hammer = h; break; }
            }
        }
        catch (Exception ex)
        {
            CheatState.Error("debug.act hammer: hammer scan failed (resolve link): " + ex.Message);
            return;
        }
        if (hammer == null)
        {
            CheatState.Error("debug.act hammer: no live Hammer tool (resolve link)");
            return;
        }
        try
        {
            hammer.Use(mouse);
        }
        catch (Exception ex)
        {
            CheatState.Error("debug.act hammer: invoke link failed: " + ex.Message);
            return;
        }
        DebugRuntime.Emit("debug.act.done", new Dictionary<string, object>
        {
            ["tag"] = _tag,
            ["verb"] = "hammer",
            ["expectKind"] = "item.hammer",
            ["snapshot"] = ControlRefs.CurrentSnapshot()
        });
        CheatState.Note("debug act hammer (expect item.hammer telemetry)");
    }

    static string? Str(JsonElement p, string name) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String
            ? e.GetString()
            : null;

    static int Int(JsonElement p, string name, int dflt) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var e) && e.TryGetInt32(out var v) ? v : dflt;
}
