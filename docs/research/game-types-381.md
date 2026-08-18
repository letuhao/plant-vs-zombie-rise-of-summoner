# Game types on 3.8.1 (verified)

Dumped from local `BepInEx/interop/Assembly-CSharp.dll` with Mono.Cecil. This is the API BepInEx plugins see, not the native IL2CPP names inside `GameAssembly.dll`.

## Capture dump field lists

These are the **fixed JSON keys** written by `DumpPlant` / `DumpZombie` / `DumpBoardStats` / `DumpBoardConfig`. Missing a field later = add it here and to the dump helper. Do **not** add a SQL column per field.

`hpBase` / `attackBase` = values **before our slider**. Mode x10/x100 is already inside the live fields.

### Plant (`source`: `start` | `setPlantAttributes`)

`type`, `typeName`, `displayName`, `ptr`, `col`, `row`, `thePlantHealth`, `thePlantMaxHealth`, `theShieldHealth`, `attackDamage`, `theLevel`, `shootingLevel`, `limDamage`, `thePlantAttackInterval`, `attackSpeedAdder`, `hpBase`, `maxHpBase`, `hp`, `maxHp`, `attack`, `source`

### Zombie (`source`: `start` | `initHealth` | `setHealthInTravel` | `setZombieHealth` | `reinforce`)

`type`, `typeName`, `displayName`, `ptr`, `theHealth`, `theMaxHealth`, `theFirstArmorHealth`, `theFirstArmorMaxHealth`, `theSecondArmorHealth`, `theSecondArmorMaxHealth`, `theAttackDamage`, `level`, `theArmor`, `takeDmgMultiplier`, `damageMultiplier`, `theSpeed`, `theOriginSpeed`, `uniqueSpeed`, `currentAllHealth`, `totalAllHealth`, `isMindControlled`, `hpBase`, `maxHpBase`, `hp`, `maxHp`, `attackBase`, `attack`, `armorBase`, `armor`, `armorMaxBase`, `armorMax`, `source`

### BoardStatistics + live Board (snapshot)

`sun`, `finalSun`, `sunProduced`, `sunConsumed`, `finalMoney`, `moneyEarned`, `moneyConsumed`, `wave`, `maxWave`, `currentWave`, `theWave`, `theMaxWave`, `timeUntilNextWave`, `mowerUsedCount`, `plantsPlanted`, `plantsDied`, `plantsShoveled`, `zombiesKilled`, `zombiesMindControlled`, `totalZombieDamage`, `duration`, `gameDuration`, `gameResult`, `theSun`, `theMoney`, `thePoints`, `plantedCount`, `theCurrentPlantCount`, `theTotalNumOfZombie`, `zombieCurrentWaveHealth`, `zombieSpawnHealth`, `zombieTotalHealth`, `isHugeWave`, `maxSun`, `maxMoney`, `levelType`, `boardLevel`

### Board.config (`GameLevel.BoardConfig`) → `board.modifiers` / `runs.modifiers_json`

`zombieHealthMultiplier`, `zombieDamageMultiplier`, `zombieSpeedMultiplier`, `zombieCountMultiplier`, `zombieStartAmmor`, `plantModifyMin`, `plantModifyMax`, `zombieModifyMin`, `zombieModifyMax`, `waveInterval`, `conveyInterval`

## Capture A/B/C/D

**A — live today:** `Board.Awake`/`Die`/`OnDestroy`, `BoardStatistics.GameOver`, `HandleGameLose`, `BoardVictory.Win`, `Plant.Start`/`Die`/`TakeDamage`, `Zombie.Start`/`InitHealth`/`Die`/`DestoryZombie`/`TakeDamage`, `CreateMower.SetMower`, `Mower.StartMove`/`Die`, `Bullet.InitData`. Catalog `Enum.GetValues` is empty on this IL2CPP host.

**B — factory / poll / recipes:** `CreatePlant.SetPlant` / `MixEvent` / `SetPlantAttributes` / `UniqueEvent`, `CreateZombie.SetZombie` / `SetZombieWithMindControl`, poll `Board.theWave`/`theSun`/`theMoney`/`thePoints`/`plantedCount`/`mowerArray.started`, `Board.UseSun`/`GetSun`/`UseMoney`/`GetMoney`/`GetPoint`, `InGameUI.SetLevelName`, `BoardSpawner.SummonZombies`, `Board.HugeWaveEvent`, Il2Cpp enum dump + `Lawnf.GetName`, `PlantMixTreeManager.ChildToParents`, `InitZombieList.InitZombie`.

**C — player / travel / loot:** `Mouse.ClickOnCard`/`TryToSetPlantByCard`/`TryToSetZombieByCard`/`DisassemblePlant`/`TryToSetPlantByGlove`, `CardUI.UseOnce`, `InitBoard.CreateCard`, Almanac `SelectCard`, `Shovel.Use`/`PayBackSun`, `Glove.Use`, `Fertilize.Use`, `Hammer.Use`, `Wheel.Use`, `MiniPet.SetPet`/`GetExperience`, `GridItem.SetGridItem`/`Die`, `CreateBullet.SetBullet`, `CreateItem.SetCoin`, `ItemManager.SetBucket`, `Present.RandomPlant`, `Lawnf.SetDroppedCard`/`SetAward`, `PrizeMgr.Click`, `PauseMenu_Btn.Restart`, `GameLose.ProcessZombieEnter`, `Board.OnPlantCreate`/`OnPlantDie`, `Plant.RealTakeDamage`/`Crashed`, `Zombie.BodyTakeDamage`/`ApplyDamage`/`SetMindControl`, `TravelMgr.OnBoardStart`/`GetNormalBuff`/`GetUltiBuff`/`GetDebuff`/`GetInvestBuff`/`UnlockPlant`/`ReinforcePlant`/`ReinforceZombie`, `MultipleChoiceMenu.OnSelect`, `Board.SetHealthInTravel`, `Lawnf.SetZombieHealth`.

**D — out:** GodMode / `CheckBox` force / NoLose / `Time.timeScale` / writing sun-money, Harmony on `Update` / `OnTriggerStay2D` / `GameAPP.Start` / EventNodes / particles, foreign translation JSON tables, XP/loot design tables, `Resources.Load` prefabs, `CreatePlant.CheckMix` / `MixData.TryGetMix` (aiming spam).

## Signatures (3.8.1)

```
Plant SetPlant(int newColumn, int newRow, PlantType theSeedType, Plant targetPlant, Vector2 puffV, bool isFreeSet, bool withEffect, Plant hidplant)
void MixEvent(PlantType theSeedType, Plant plant, int theRow)
void SetPlantAttributes(Plant plant)
Zombie SetZombie(int theRow, ZombieType theZombieType, float theX, bool isMindControlled)
Zombie SetZombieWithMindControl(int theRow, ZombieType theZombieType, float theX, bool withEffect)
void UseSun(float count)
void GetSun(float count, bool save)
void UseMoney(int value)
void GetMoney(float count)
void GetPoint(float count, bool killZombie)
void SetLevelName(string name)
void SummonZombies(int wave)
void HugeWaveEvent(int currentWave)
void InitZombie(LevelType, int theLevelNumber, SceneType, int theSurvivalRound)
MiniPet SetPet(Board board, Vector2 position, PetType petType)
GridItem SetGridItem(int theColumn, int theRow, GridItemType theType, GraveType graveType)
Bullet SetBullet(float x, float y, int theRow, BulletType theBulletType, BulletMoveWay theMovingWay, bool fromEnermy)
GameObject SetCoin(int theColumn, int theRow, int theItemType, int theMoveType, Vector3 pos, bool freeSet)
void OnPlantCreate(Plant plant)
void OnPlantDie(Plant plant, DieReason plantDieReason)
String GetName(PlantType) / GetName(ZombieType)   // Lawnf
```

`PlantMixTreeManager.GetRecipes` takes `PlantType` — dump via `ChildToParents` (`MixParentInfo`: ParentA, ParentB, Result).

`Plant.DieReason`: Default, ByWheat, ByMix, ByDisMix, ByLevelUp, BySteal, ByBejeweled, ByShovel, BySelf, ByFreeze, Hid, CrashInWater, Crash, Wheel.

`GameResult`: None=0, Victory=1, Defeat=2, Surrender=3, Timeout=4.

`GameAPP.Start` is crowded — do not patch.

## How to re-dump later

Use Mono.Cecil from `BepInEx/core/Mono.Cecil.dll` on `BepInEx/interop/Assembly-CSharp.dll`. Do not `Assembly.Load` the interop DLL in PowerShell without `Il2CppInterop.Runtime`.
