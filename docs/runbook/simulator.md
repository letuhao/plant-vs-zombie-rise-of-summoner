# Simulator (developers)

The Simulator tab and `/api/sim/*` pretend to be the injector. Do **not** launch PVZRH against the same server.

## Enable

`dotnet run` uses `Properties/launchSettings.json`, which sets `FUSIONRPG_SIM=1`.

Or:

```powershell
$env:FUSIONRPG_SIM = "1"
$env:FUSIONRPG_NO_BROWSER = "1"   # optional, skip opening a browser
dotnet run --project src/FusionRpg.Server
dotnet test
```

Unset the flag (player zip or `.\scripts\deploy-play.ps1`): `/api/sim/*` and `/api/test/*` return 404. The Simulator tab stays hidden.

## Use the UI

1. Open `http://127.0.0.1:5088`
2. Status shows `source` (`none` / `sim` / `injector`) and `simEnabled`
3. Simulator tab: Hello, Start board, Spawn plant/zombie, Hit, Die, Mower place/start/die, Wave, Win/Lose/Result, Bullet, End board, Reset
4. Header: create/select player. Runs tab is filtered to the current save.

## Collision

If a real injector heartbeat is fresh (`source=injector`, last 5 seconds), sim POSTs return **409**. Stop the game or wait, then use the sim.

## Defaults

Plant: type 0, hp/max 300, atk 20, col 0, row 0.  
Zombie: type 0, hp/max 270, atk 50, armor 0.  
Hit damage: 50. Ptrs: `P1`, `P2`, … / `Z1`, `Z2`, … (reset restarts counters).
