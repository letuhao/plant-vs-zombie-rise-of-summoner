# Spec: `control-act`

**Program:** `game-control` · **Map:** [../game-control-map.md](../game-control-map.md)
**Ideal:** [../game-control-ideal.md](../game-control-ideal.md) · **Standard:**
[../../contributing/live-probe-standard.md](../../contributing/live-probe-standard.md)
**Depends on:** `control-click` (invoke primitives)

---

## Objective

Chained lawn verbs with read-back — what an agent actually wants to *do*: pick a card
and place it, shovel a cell, use an item. Each verb composes `control-click` invokes
(never raw engine writes), then reads the result back through the normal telemetry path
(`card.pick`/`card.place` events, board-stats, selection state) — never the response
alone. One verb, one receipt, one read-back.

Who uses this: live-probe operators playing the lawn (setup boards, drive scenarios),
and any future autonomous lawn loop.

Success: `act.place {card,type,col,row}` ends with the plant in the cell per telemetry;
`act.shovel {col,row}` ends with the cell empty per telemetry; failures name which link
broke (resolve, invoke, or read-back) instead of returning a bare `ok:false`.

**ASSUMPTIONS I'M MAKING:**

1. Verb set v1: `place` (card→cell), `shovel`, `item` (glove/fertilize/hammer/wheel
   where a debug path already exists). No new game mechanics.
2. Read-back uses existing emits only (`card.pick`, `card.place`, `plant.shovel`,
   `debug.board-stats`) — no new telemetry.
3. Verbs run on the drain via the debug command path like everything else in this program.

→ Correct me now or implementation proceeds with these.

## Tech Stack

- Injector C# net6: composition over `ControlClick` + `ControlRefs` (this module's only
  engine calls are the ones `control-click` already owns).
- Server net8 relay (`DebugEndpoints.cs`, scope banners).
- No new packages, no new Unity references.

## Commands

```powershell
.\scripts\guard-debug-scope.ps1
dotnet test tests/FusionRpg.Guard.Tests --filter "FullyQualifiedName~DebugScope"
curl -X POST http://127.0.0.1:5088/api/debug/act -H "Content-Type: application/json" -d '{"verb":"place","typeId":3,"col":2,"row":1}'
```

## Project Structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Injector/ControlAct.cs` (new) | Verb implementations: resolve card ref (or find by typeId) → click → set cell → place → poll telemetry for the expected event within a bounded wait → receipt `{verb, ok, brokenLink?, evidence}` |
| `src/FusionRpg.Injector/CheatCommandRunner.cs` | One `debug.act` case (`{verb, ...}` payload) |
| `src/FusionRpg.Server/DebugEndpoints.cs` | `POST /api/debug/act` (Game Injector Debug banner + relay) |

## Code Style

Compose, then prove — the receipt always says which link was checked:

```csharp
public static void Place(int typeId, int col, int row)
{
    // Link 1: resolve — find the card or fail naming the card.
    if (!ControlRefs.TryFindCard(typeId, out var cardRef))
    {
        CheatState.Error($"debug.act place: no card for typeId {typeId} in current snapshot");
        return;
    }
    // Link 2: invoke — the click module's own verbs, never raw writes.
    if (!ControlClick.ClickByRef(cardRef)) return; // click module already said why
    if (!ControlClick.PlaceAt(col, row)) return;
    // Link 3: read-back — the normal telemetry path, bounded wait.
    if (!ActReadBack.WaitForCardPlace(typeId, col, row, ActTimeoutMs))
    {
        CheatState.Error("debug.act place: invoke ok, no card.place telemetry within window");
        return;
    }
    DebugRuntime.Emit("debug.act.done", new Dictionary<string, object>
    {
        ["verb"] = "place",
        ["typeId"] = typeId,
        ["col"] = col,
        ["row"] = row,
        // Richer-default contract, owner decision: the FULL acted scope snapshot rides
        // along (truncation-marked past budget) — not a fragment. Cost is bounded by the
        // inspect page budget, not by a second limit here.
        ["snapshot"] = ControlRefs.ScopeSnapshot("lawn")
    });
}
```

## Testing Strategy

- **Guard (CI):** `guard-debug-scope.ps1` green.
- **Unit (no game):** receipt shaping + link-failure naming on stubbed links only.
- **Live (real game+server):** each v1 verb ends in the telemetry-proven state; a
  deliberately bad input (unknown typeId, out-of-board cell) fails at the resolve link
  with the card/cell named — never a mid-chain silent drop.
- **Perf:** verbs are seconds-scale by nature (telemetry windows); the structural timeout
  const bounds every wait; nothing ambient.

## Boundaries

- Always: compose over `control-click` (no raw engine writes in this module); read back
  through normal telemetry with a bounded wait; name the broken link on failure; scope
  banners; guard green.
- Ask first: adding a verb beyond v1's three; lengthening any telemetry window.
- Never: fabricating the result (an `ok:true` without the telemetry event is the exact
  defect `live-probe-standard.md` §2 bans); new Unity write paths; combat/derived
  magnitudes (ActorHub gate N/A — stated).

## Success Criteria

- [ ] All three v1 verbs end telemetry-proven; receipts name their evidence.
- [x] Bad inputs fail at resolve with names, never mid-chain silence (proven live
  2026-09-14: unknown typeId, missing tool, unknown verb — each names its link).
- [ ] Responses bundle the snapshot (richer-default contract).
- [ ] Guard + scope tests green.
- [ ] Live telemetry-success proof is deferred: seed-bank cards are not `CardUI` objects
  and `Shovel`/`Hammer` tools materialize only on pickup (measured live) — the verbs run
  the day cards/tools exist; the unblock path is cursor-clicking the IMGUI tool icon
  first (cursor tier already proves real clicks land).

## Open Questions

None.
