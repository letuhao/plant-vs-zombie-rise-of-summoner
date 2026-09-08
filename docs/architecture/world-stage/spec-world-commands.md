# Spec: world-commands

**Status: Draft — Phase 1 (Specify), awaiting owner review.** Module id `world-commands` in the
[world-stage capability map](../world-stage-map.md). **Level 1, no dependencies** — it touches no FE
file, so it parallelises with `world-contract` and `world-wire`.

**Ideal:** [world-stage-ideal.md](../world-stage-ideal.md) §2.2, §2.3, §8c.2, §8c.4, §8c.6, §8d.2.
**Catalog:** [design/11-world-stage.html](../../design/11-world-stage.html) §H, §K.

---

## Objective

Open the world's write surface. Two orders the engine can already resolve cannot be filed at all; the
economy's central decision is made by the engine on the player's behalf; a warden binding is read,
hashed, persisted and cleared in production but set by nothing; and a stance that unlocks prospecting
is four changes away, not the one §2.2 claimed.

This is the module the map calls one of its **two named exceptions** to *"not a redesign of the turn
engine"* — the owner authorised the cede order (§8d.2) and the `dowse` stance, and nothing else here
changes how a turn resolves.

**The shape of the whole module, in one example.** `sustain` is blocked **twice over**:

- `WorldCommandRequest` has no `Amount` (`WorldDtos.cs:205-217`), so `WorldEndpoints.cs:72-82` cannot
  set one, and admission refuses `amount.invalid` at `WorldCommandAdmission.cs:63`;
- and even if it could, `CommandPayload` (`RpgStore.WorldTurns.cs:442-444`) does not persist it, so
  the order would come back amountless when `TurnEngine.cs:134` re-admits it from the log.

`WorldCommand` itself already has both fields — `Amount` at `WorldCommand.cs:76`, `StructureId` at
`:79`. `SustainResolver.Run` (`SustainResolver.cs:19`) and `BuildResolver.Run` (`BuildResolver.cs:21`)
are fully implemented and wired into the engine at `TurnEngine.cs:214` and `:280`. **The gap is the
DTO and the payload. Nothing else.**

**Success is that every kind in `WorldCommandKinds.All` can be filed, persisted, re-admitted from the
log and resolved — and that the player, not `LoamForecast.Weakest`, decides what ground is given up.**

## Design

### 1. `Amount` and `StructureId` — three files, six sites, two of them silent

`ReadCommandRow`'s neighbours already wrote the warning this module is paying off
(`RpgStore.WorldTurns.cs:437-441`):

> *"Every optional field a command can carry. Adding one to `WorldCommand` and forgetting it here
> loses it in the round trip and the order comes back malformed — which is exactly how `stance` was
> found missing."*

| # | Site | Change |
|---|---|---|
| 1 | `WorldDtos.cs:205-217` | `WorldCommandRequest` gains `long? Amount` and `string? StructureId` |
| 2 | `WorldEndpoints.cs:72-82` | the `WorldCommand` projection sets both |
| 3 | `RpgStore.WorldTurns.cs:442-444` | `CommandPayload` gains `long? Amount = null, string? StructureId = null` |
| 4 | `RpgStore.WorldTurns.cs:168-169` | the `JsonSerializer.Serialize` call passes both |
| 5 | `RpgStore.WorldTurns.cs:647-662` | `ReadCommandRow` hydrates both |
| 6 | `RpgStore.WorldTurns.cs:679-713` | `ListWorldCommandsUnlocked` hydrates both |

**§8c.4 counted five sites. There are six, and the sixth is the dangerous one.** `ReadCommandRow`'s
own doc says it is *"shared by both listers"* (`:643-646`) — it is, at `:400` and `:430` — but
`ListWorldCommandsUnlocked` (`:679`) **does not call it.** It inlines the same deserialization at
`:697-709`, and it is the site that feeds the engine: `:507` reads it inside the commit transaction,
and `TurnEngine.cs:134` re-admits what it returns. A field added to the other two and missed here
would survive every listing a client sees and vanish only at the moment the turn resolves.

> **Sub-decision.** `ListWorldCommandsUnlocked` is changed to call `ReadCommandRow`, making the
> comment true and reducing six sites to five for the next field. This is a two-line refactor inside
> `FusionRpg.Data` with an existing behavioural test around it, and it removes the exact class of
> defect the comment describes.

`Amount` is a **magnitude** and stays `long` end to end — `WorldCommand.Amount` is already `long?`
(`WorldCommand.cs:76`), and it is spent against `CarriedLoam`, also `long` (`WorldState.cs:262`). No
`int` appears on this path, and none is introduced.

### 2. The cede order — §8d.2, owner-decided

Today `LoamPhases.Pressure` picks the sector to release **itself**, every turn, via
`LoamForecast.Weakest` (`LoamPhases.cs:133-146`, the call at `:138`). There is no `abandon` / `cede` /
`release` kind (`WorldCommand.cs:36-37`). §8c.2 named that the economy's core tension existing as a
notification rather than a decision, and plate 11 §K.4's *"Give up Hollowmoor instead"* is a lie until
this lands.

**`WorldCommandKinds.Cede = "cede"`.** Names a sector; needs no entity. Admission requires a
`SectorId` the commander owns — `WorldCommandAdmission.Admit` already refuses `sector.unknown` at
`:45-46` and already checks entity ownership at `:38-39`, so the new arm follows `Claim`'s shape
(`:54-58`) with an ownership check instead of an entity one.

#### The constraint that shapes the implementation

§8c.6 lists as **load-bearing**: *"warning and act share `Weakest`, so the forecast and the event
cannot disagree."* Today they cannot, because `LoamPhases.cs:138` and `LoamForecast.cs:62` (inside
`WillRelease`, `:58`) call the same function. §8d.2 requires that survive:

> **The player's choice is an *input* to `Weakest`, never a second code path.**

```csharp
// LoamForecast.cs:19 — one new parameter, one new clause, same ordering underneath.
public static string? Weakest(
    WorldState world, IReadOnlyList<string> component, long available, long upkeep,
    string? ceded = null)
{
    if (available >= upkeep) return null;

    var candidates = component
        .Where(id => world.Sectors.First(s => s.SectorId == id).WardenBindingId is null)
        .ToList();
    if (candidates.Count == 0) return null;

    // The player's choice wins only where the engine could have chosen it anyway: in this
    // component, and not warded. A cede order naming warded or foreign ground is not a second
    // rule — it simply is not a candidate, and the default ordering answers.
    if (ceded is { } id && candidates.Contains(id, StringComparer.Ordinal)) return id;

    return candidates
        .OrderBy(x => LoamBalance.PerSector(world, world.Sectors.First(s => s.SectorId == x)))
        .ThenBy(x => x, StringComparer.Ordinal)
        .First();
}
```

Threading it, both halves:

- **The act.** `LoamPhases.Pressure` (`LoamPhases.cs:100`) takes a `faction id → ceded sector id` map.
  `TurnEngine.Pressure` (`TurnEngine.cs:210-218`) already holds the turn's `commands` and builds it,
  exactly the way it already derives `postures` from `stance` orders at `:285-288`.
- **The forecast.** `WorldEndpoints.ComputeLoamReading` (`:420-461`) calls
  `LoamForecast.WillRelease` at `:455` and must pass the same preference, or `WillReleaseNextTurn`
  (`WorldDtos.cs:125`) starts naming a different sector than the turn will release. The pending orders
  are already reachable from that endpoint — `store.ListLoggedWorldCommands(worldId, turn)` is called
  at `WorldEndpoints.cs:185` — **but on a different route.** Corrected 2026-09-03 by audit: that call
  sits in `MapTurns`' `GET /{worldId}/turn/{turn}` handler, while `ComputeLoamReading` runs on
  `/state` (`:420-461`). Threading the cede preference needs a **new store read on the state route**,
  not a nearby call reused.

**A test asserts the two agree**, over a world with a cede order filed: `WillRelease` and the sector
`Pressure` actually fades are the same id. That test is the whole reason this design has one function
instead of two.

#### Version and goldens — triage before re-blessing

`LoamPhases.Pressure`'s behaviour changes, so per §8d.2 this is a `RulesetVersion` decision:
**`TurnEngine.RulesetVersion` 5 → 6** (`TurnEngine.cs:42`).

What that costs is smaller than "a re-bless", and the difference is worth stating rather than
budgeting past:

- The world state hash is taken over `WorldCanonical.Write(world)` only (`StateHasher.cs:17`), and
  `RulesetVersion` is not in it — it is stored beside the log (`RpgStore.WorldTurns.cs:525`) and
  gates report re-derivation at `:592`.
- With **no cede order filed**, `Weakest(…, ceded: null)` returns exactly what it returns today. So
  `WorldWaveOneAcceptanceTests.GoldenFinalHash`
  (`tests/FusionRpg.Data.Tests/WorldWaveOneAcceptanceTests.cs:123`, asserted `:323`) is **expected not
  to move**.

> **Decision: triage, then re-bless — never re-bless to make a suite green.** A moved hash on a
> scenario that files no cede order is a **defect in the preference threading**, not a golden that
> needs blessing. `decisions.md:103` is the precedent and it is exact: buff-debuff-scope's first
> implementation moved this same golden at a neutral default, and the fix was to follow
> `WorldCanonical`'s own non-default-row shape rather than re-bless — **zero goldens moved in the
> shipped version.**

The three additions in this module (`cede`, `bind-warden`, `dowse`) land under **one** version bump,
per `decisions.md:98`: *"`RulesetVersion` advances **once** for the combined move."*

### 3. The `bind-warden` command

`WorldSector.WardenBindingId` (`WorldState.cs:173`) is read by `LoamForecast.cs:24` (a warded sector
is never the fade target) and `LoamPhases.cs:162` (a warded sector neither rises nor falls), hashed at
`WorldCanonical.cs:37`, persisted at `RpgStore.World.cs:441`, and cleared on capture at
`ClaimResolver.cs:85`. **It is set non-null nowhere in production** — the only writers are
`LoamTextureTests.cs:355, 378, 413, 430`.

**`WorldCommandKinds.BindWarden = "bind-warden"`.** Names a sector the commander owns and carries the
binding id. The kind is `bind-warden`, not `ward` — `ward` names a *lane* action
(`WorldLaneDto.WardLevel`) that stays unbuilt, and the collision was already repaired once; it must
not return through a spec's own text. A `WardenResolver` in `Movement/`, resolving in `Snapshot`
beside `Claim` and `Build` for the same reason those two do (`TurnEngine.cs:274-280`): ownership is
only settled once the turn has run, and a warden bound to ground you lost this turn must not stick.

#### The Core/Data boundary makes this two steps, and there is no rollback

`FusionRpg.Core.csproj` declares exactly one `ProjectReference` — `FusionRpg.Contracts`. Core
**cannot** call `RpgStore.BindAsWarden`, and a comment in that csproj records that a guard
substring-scans the file for the data project's name. The orchestration therefore lives in the Server
layer, which references both:

```
POST /api/world/{worldId}/bind-warden   { sectorId, instanceId }
  1. store.BindAsWarden(playerId, instanceId)            → RpgStore.Contracts.cs:283
  2. store.SubmitWorldCommands(worldId, [bind-warden…])   → the ordinary command path
```

> **Accepted risk, stated rather than engineered around: if step 2 fails, step 1 is not rolled back.**
> The player has paid the soul fee (`RpgStore.Contracts.cs:316-321`) and holds a non-releasable
> binding with no sector attached. There is no cross-store transaction to reach for — the contract
> lives in the player database and the order lives in the world's command log, and inventing a
> distributed rollback for a single-player local server is a worse trade than the failure it prevents.
>
> **What makes it tolerable is that step 1 is idempotent.** `BindAsWarden` returns `("replay",
> existing)` for an instance already bound as a warden (`:301-305`), so the client's correct response
> to any failure is to **retry the whole call**, which re-binds nothing and re-files the order. The
> endpoint says so in its own doc comment, and a test proves the replay path.

**Making warden state live means it starts participating in the hash.** `WorldCanonical.cs:37`
already emits `s.WardenBindingId` in the sector row — today always the null placeholder, in every
world, in every golden. The moment a warden can be bound, that cell carries a real id and every
subsequent hash in that world differs from the one it would have had. **No existing golden moves** (no
shipped scenario binds a warden), but this is the first production path that can change a hash without
changing a number, and it should be named before it surprises someone.

The economics are already built and are **not** re-litigated here: capacity, the soul fee and the
non-releasable flag are `BindAsWarden`'s (`:310-323`), and `/api/contracts/bind` calls the ordinary
`BindContract` at `ContractEndpoints.cs:31` — this is `BindAsWarden`'s **first production call site**.

### 4. The `dowse` stance — four changes, not one

§2.2 called prospecting *"blocked by one line."* §8c.4 corrected it. All four, verified:

1. **`MovementPolicy.Stances` (`Movement/LaneCost.cs:13`)** is `{ March, Scout, Hold }`, so admission
   refuses `stance.unknown` at `WorldCommandAdmission.cs:51`. Add `Dowse`.
2. **`BudgetFor` (`LaneCost.cs:38-42`)** has arms for `Hold` and `Scout` and a `_` default returning
   `PointsPerTurn` (`:23`) — **a dowser would silently receive the full march budget.** It needs its
   own arm. Half a march is what `Scout` already pays for double sight (`:26`); a dowser sees four
   lanes out (`Prospecting.DowserSightLanes = 4`, `IntelRecorder.cs:174`) against a scout's reach, so
   the number is a balance question, not a structural one.
   > **It goes in `data/tuning/`, not a `const`.** `movement.dowseBudgetMilli` in a published
   > `world.v2.json`, read through `WorldTuningHub` the way `LoamPolicy.CarryPerBearer`
   > (`LoamPolicy.cs:91`) and `TurnCalendar.DaysPerWeek` (`TurnCalendar.cs:22`) already are. The file
   > is never hand-edited — `python tools/tuning/publish.py world movement.dowseBudgetMilli=<n>`
   > writes the next version and leaves `world.v1.json` on disk as the revert target, per its own
   > `_meta.rebalance`. `MovementPolicy`'s existing `const` budgets are pre-existing debt this module
   > does not inherit and does not fix.
3. **`Prospecting.Reveal` (`IntelRecorder.cs:179`) has no production caller.** Its caller is the
   projection, and that is `world-wire`'s §4.
4. **No DTO carries the revealed set.** Also `world-wire`'s §4.

**One string, not two.** `Prospecting.DowserStance = "dowse"` already exists at `IntelRecorder.cs:176`
and `Reveal` matches on it at `:187`. `MovementPolicy.Dowse` must be the same literal or a dowser
passes admission and reveals nothing. A test asserts `MovementPolicy.Dowse == Prospecting.DowserStance`
— cheap, and it catches the only way this can silently half-work.

### 5. Determinism and replay are safe, and here is why

§8c.4's clean pass, re-verified against the code in this session:

- **Old `payload_json` rows still deserialize.** The new `CommandPayload` members are optional with
  defaults (`RpgStore.WorldTurns.cs:442-444`), and both hydration sites already null-coalesce a failed
  deserialize (`:649-650`, `:697-698`). A row written before this module reads back with `Amount` and
  `StructureId` null — which is the state those orders were actually in.
- **`WorldCanonical` never hashes commands.** It writes factions, sectors, slots, lanes, entities,
  members, intel and the faction-scope row (`WorldCanonical.cs:30-90`). No command reaches it, so no
  new command kind can move a hash by existing.
- **A stored order with no amount refuses exactly as today**, at `WorldCommandAdmission.cs:63`, in the
  `Reveal` phase, reported as a dropped command at `TurnEngine.cs:137`. Re-admission is not weakened.

**No `decisions.md` lock is contradicted.** The phase order is untouched — `cede` reads inside
`Pressure`, `bind-warden` resolves inside `Snapshot`, and neither adds nor moves a phase.

## What stays out

- **Every projection.** `Amount` and `StructureId` go *in*; nothing about the results comes back out
  on a DTO here. The prospected set, `CarriedLoam`, `WardenBindingId` and effective capacity are all
  `world-wire`'s.
- **The FE.** No file under `web/` changes. `world-targeting` and `world-confirms` build the surfaces
  that call these routes; this module makes the routes answer.
- **Recruitment and the `Growth` no-op** (`TurnEngine.cs:196-200`) — `sector-development`'s.
- **Server-side standing orders.** `MarchResolver.cs:29-30` re-issues a standing order whole each
  turn and the client resubmits; that is a real gap (§2.3) and not this module's.
- **Contract capacity, soul pricing or the non-releasable rule.** Shipped in `demon-contracts` and
  read as-is (`RpgStore.Contracts.cs:283-326`).
- **A cede *forecast* UI.** The number already exists (`WorldSectorDto.WillReleaseNextTurn`,
  `WorldDtos.cs:125`); drawing it is `world-inspector`'s.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.Server.Tests
dotnet test tests\FusionRpg.E2E.Tests
dotnet test tests\FusionRpg.Guard.Tests

.\scripts\guard-dal.ps1                              # SQL stays inside FusionRpg.Data
python scripts\audit-magic-numbers.py --summary      # the dowse budget must not land as a const
python scripts\audit-overflow.py                     # Amount is a long on every hop

# the tuning row, once — never hand-edit world.v1.json
python tools\tuning\publish.py world movement.dowseBudgetMilli=<n>
```

## Project structure

```
src/FusionRpg.Contracts/
  WorldDtos.cs                 → WorldCommandRequest gains Amount + StructureId
src/FusionRpg.Server/
  WorldEndpoints.cs            → :72-82 sets both; ComputeLoamReading takes the cede preference
  WorldWardenEndpoint.cs       → new: POST /api/world/{worldId}/bind-warden, the two-step orchestration
src/FusionRpg.Core/World/
  Turn/WorldCommand.cs         → Cede + BindWarden kinds, added to All (:36-37)
  Turn/WorldCommandAdmission.cs→ arms for cede and bind-warden
  Turn/TurnEngine.cs           → RulesetVersion 5 → 6; Pressure builds the cede map; Snapshot runs WardenResolver
  Loam/LoamForecast.cs         → Weakest gains `ceded`; WillRelease passes it through
  Loam/LoamPhases.cs           → Pressure takes the cede map and passes it to Weakest (:138)
  Movement/LaneCost.cs         → MovementPolicy.Dowse; BudgetFor gains its arm
  Movement/WardenResolver.cs   → new: sets WorldSector.WardenBindingId
src/FusionRpg.Data/
  Sqlite/RpgStore.WorldTurns.cs→ CommandPayload + the two hydration sites; ListWorldCommandsUnlocked
                                 calls ReadCommandRow
data/tuning/world.v2.json      → movement.dowseBudgetMilli (published, not hand-edited)
```

## Code style

Follow the file being edited. `WorldCommandKinds` documents each kind in one sentence saying what the
player is doing, not what the code does; admission arms are one `if` block per kind in kind order;
resolvers are `static WorldState Run(world, commands, report, phase)` and rebuild with `with`.

```csharp
/// <summary>
/// Give up a sector deliberately: name the ground this faction will let go if its component cannot
/// cover upkeep, instead of letting the engine pick the weakest contributor (spec-loam-fe.md's
/// abandonment surface). A preference, not a demolition order — a component that covers its upkeep
/// releases nothing, and a cede naming warded or foreign ground is simply not a candidate.
/// </summary>
public const string Cede = "cede";
```

The cede preference is threaded as a **plain map**, never as a service or an interface — it is one
dictionary built at the top of `Pressure` from the same `commands` list `postures` is built from
(`TurnEngine.cs:285-288`), and it is passed down, not looked up.

## Testing strategy

xUnit, in the project owning the boundary. Six levels, and level 2 is the one this module exists for:

1. **Round trip, per field (Data.Tests).** A `sustain` with an `Amount` and a `build` with a
   `StructureId` are submitted, read back through **all three** hydration paths — `:400`, `:430` and
   `:679` — and still carry both. The third path is the one `TurnEngine.cs:134` re-admits from and the
   one `stance` was lost on.
2. **Reveal round-trip, end to end (E2E).** Submit `sustain`, commit the turn, assert the sector's
   stock rose. This is the trip Gate A names, and until it passes nothing above level 1 is safe to
   build.
3. **Forecast and act agree, with a cede filed (Core.Tests).** Over a component in shortfall with a
   cede order naming a *non-weakest* member: `LoamForecast.WillRelease` and the sector
   `LoamPhases.Pressure` actually fades are the same id. Then the three refusal cases — ceding a
   warded sector, a sector in another component, and one this faction does not own — each fall back to
   the default ordering rather than doing nothing.
4. **Determinism (Core.Tests / Data.Tests).** `GoldenFinalHash` is **unchanged** with no cede order
   filed — asserted, not assumed, and a failure here is triaged as a defect before any re-bless. A
   `payload_json` row written without the new members deserializes and re-admits identically. A cede
   order changes no hash by *existing*, only by being *acted on*.
5. **Bind-warden (Server.Tests / Data.Tests).** The two-step endpoint binds then files; a retry after a
   simulated step-2 failure hits `BindAsWarden`'s `"replay"` path (`RpgStore.Contracts.cs:301-305`)
   and lands the order; a warded sector is excluded from `Weakest` (`LoamForecast.cs:24`) and neither
   fades nor recovers (`LoamPhases.cs:162`); capture clears the binding (`ClaimResolver.cs:85`).
6. **Dowse (Core.Tests).** `MovementPolicy.Dowse == Prospecting.DowserStance`; a `dowse` stance order
   is **admitted** where today it is refused `stance.unknown`; and `BudgetFor("dowse")` returns the
   tuned budget rather than falling through to `PointsPerTurn` — the silent half of the defect, which
   no test would catch by observing that the order was accepted.

The four boundary guards run too. `guard-dal.ps1` matters more than usual here: §3's orchestration is
the first place in the world stack where a Core concept and a store call meet, and the reason it sits
in the Server layer is a boundary a guard enforces rather than a preference.

## Boundaries

- **Always:** add a command field to `WorldCommand`, the DTO **and** `CommandPayload` in the same
  change, and prove the round trip at all three hydration sites; keep a magnitude `long` end to end;
  make a player choice an *input* to the shared function; put a balance number in `data/tuning/` and
  publish it with the tool.
- **Ask first:** any further `RulesetVersion` bump beyond the single 5 → 6 this module takes — the
  three additions are batched under it on purpose (`decisions.md:98`). Any change to the phase order
  (locked, `decisions.md:7`). Any second code path that could compute a fade target — §8c.6 calls the
  shared `Weakest` load-bearing and it stays one function.
- **Never:** re-bless a world golden to make a suite green — triage first; a hash that moves with no
  cede order filed is a bug (`decisions.md:103`'s precedent). Never call `RpgStore` from
  `FusionRpg.Core` (its csproj references `FusionRpg.Contracts` alone). Never write SQL outside
  `FusionRpg.Data`. Never weaken `WorldCommandAdmission` to let an incomplete order through — an order
  with no amount must keep refusing at `:63`.

## Success criteria

1. A `sustain` with an `Amount` and a `build` with a `StructureId` survive submit → persist → list →
   re-admit → resolve, proven at all three hydration paths.
2. `ListWorldCommandsUnlocked` calls `ReadCommandRow`; the payload shape has one hydration site, and
   the comment at `:643-646` is true.
3. `cede` is a command kind; `LoamForecast.Weakest` takes the player's choice as an input and there is
   still exactly one function that answers "which sector fades"; a test proves the forecast and the
   act name the same sector with a cede filed.
4. `TurnEngine.RulesetVersion` is 6, and `GoldenFinalHash` is **unchanged** — verified before any
   re-bless is even considered.
5. `bind-warden` is a command kind, `RpgStore.BindAsWarden` has its first production caller, and
   `WorldSector.WardenBindingId` is set by production code for the first time. The two-step failure
   mode is documented at the endpoint and covered by a retry test.
6. `dowse` is in `MovementPolicy.Stances`, **has its own `BudgetFor` arm**, that arm reads
   `data/tuning/world.v2.json`, and its literal matches `Prospecting.DowserStance`.
7. All five .NET suites, the four boundary guards, and both audits are green; the magic-number audit
   shows no new balance literal on this module's files.

## Open questions

**None.** §8d.2 decided the cede order and the constraint on how it is implemented; §8c.4 corrected
the prospecting count from one change to four; the bind-warden's Core/Data boundary has one legal shape and
its failure mode is an **accepted risk with a stated mitigation**, not a question. The two genuine
forks this module contained are decided in the text: the sixth round-trip site is closed by making
`ListWorldCommandsUnlocked` share `ReadCommandRow` (§1), and the version bump's golden exposure is
resolved by triage-before-re-bless with the `decisions.md:103` precedent behind it (§2). The `dowse`
budget's *value* is a balance number and therefore not a design question — it lands in `world.v2.json`
where a balance pass moves it without a rebuild.
