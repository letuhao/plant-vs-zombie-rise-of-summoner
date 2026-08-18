# Events and lifecycle

What “something happened in the match” looks like on the 3.8.1 dump + live capture.

## Match / board

```text
Board.Awake        -> match objects exist, cache Board, board.start
  (plants/zombies Start / InitHealth as they spawn)
BoardStatistics.GameOver()  -> match.result (win/lose)
Board.Die          -> close run if still open
Board.OnDestroy    -> drop Board pointer if it matches
```

Treat “in a fight” as `Board != null` (and optionally a non-null `matchKey`). Do not walk plant/zombie lists when the board is gone.

## Plant life

```text
CreatePlant.SetPlant(...)     -> returns Plant (plant.place)
CreatePlant.SetPlantAttributes
Plant.Awake
Plant.Start                   -> plant.spawn / dump  *** usual scale hook
  ... Update / shoot / produce ...
Plant.TakeDamage / RealTakeDamage / DecreaseHealth
Plant.Die(DieReason)          -> plant.die
Plant.DieEvent / DieEventMustExecute
```

`DieReason` is a nested enum on `Plant` (`Plant.DieReason`). Do not Harmony `Board.OnPlantDie` on this pack (load-time AV with other plant mods).

## Zombie life

```text
CreateZombie.SetZombie(row, type, x, mindControl) -> Zombie
Zombie.InitHealth()           -> early dump (may be pre-buff)
Zombie.Start                  -> zombie.spawn / dump (skip type Nothing)
  ...
Zombie.TakeDamage / BodyTakeDamage / ApplyDamage
Zombie.Die(int reason)        -> zombie.die
Zombie.DestoryZombie()        -> also death cleanup
```

Two death paths — hook **both**. External HP tools often rewrite after `InitHealth` (e.g. reinforce / set-health); recapture those.

## Projectiles

```text
Bullet.InitData() -> bullet.init (noisy; SQLite only)
CreateBullet.SetBullet -> bullet.place (noisy)
```

Damage to zombies is observed on `Zombie.TakeDamage` and/or `board.boardStatistics.totalZombieDamage`.

## Level identity

Polled or hooked:

- `InGameUI` level name TMP / `SetLevelName`
- `GameAPP.theBoardLevel` / `theBoardType`

## Run statistics

`Board.boardStatistics` on 3.8.1 includes:

- plants planted / death / shoveled
- zombies killed / mind-controlled / killed-by-type
- total zombie damage
- sun produced / consumed
- money earned / consumed
- waves, duration, `GameOver()`

We also poll live board fields (`theWave`, `theSun`, `theMoney`, wave health, …) into `board.snapshot` / `board.economy`.

## Crowded hooks on this 3.8.1 pack

From `BepInEx/LogOutput.log` on this install:

- `GameAPP.Start` — other plugins already fight over it (duplicate dictionary keys). Do not patch.
- `PlantCardPackageBuilder.Start` — other mods NRE here.

Any new patch on `Start` of shared types needs null checks and per-class safe patching (our `SafePatchAll`).

## IL2CPP rules

- Compare boards/zombies with `Pointer` when identity matters.
- `TryCast<T>()` for IL2CPP type tests when needed.
- Never call game APIs (`Lawnf.GetName`, mix tree, dumps) from SignalR/thread-pool continuations — marshal to the Unity main thread.
