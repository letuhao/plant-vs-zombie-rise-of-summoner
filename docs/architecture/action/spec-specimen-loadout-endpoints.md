# Spec: specimen-loadout-endpoints (A27)

Module **A27** in the [action map](../action-map.md) §17. Reads
[action-playability-ideal.md](../action-playability-ideal.md) gap #2. Depends on **A26** (a specimen
must be able to actually hold a second real action before its loadout means anything to look at or
change).

## Objective

`LoadoutEndpoints.cs` gives Dave (the commander, `OwnerKind.Player`) a real `GET/POST /api/loadout`
surface over already-generic, already-tested storage (`RpgStore.Loadouts.cs`,
`LoadoutSet.Validate`/`GetLoadoutOrAutoEquip`). **No REST surface exists for a specimen's own held/
equipped set** (`OwnerKind.Entity`/`UniqueActor` + `instanceId`) — searched, zero hits in
`src/FusionRpg.Server/**`. This is what a real battle actually reads
(`WebMatchService.cs:565,684-704`), so it is the thing a player needs to see and change, not Dave's own
commander loadout.

## Design

1. **Route, matching the equipment endpoint's own precedent** (`PUT /actors/{id}/equipment/{slot}`,
   action-map.md §14): `GET /api/actors/{instanceId}/loadout` and `POST /api/actors/{instanceId}/loadout`.

2. **⚠️ Corrected 2026-09-13 (audit pass) — two owner scopes are involved, not one, and they are not
   interchangeable.** `WebMatchService.EquippedActionIdsFor` (`WebMatchService.cs:717-735`) already
   establishes the real, load-bearing convention, verified in code, not guessed:

   | What | Owner scope | Why |
   |---|---|---|
   | **The loadout slot assignment itself** (`GetLoadout`/`SetLoadout`/`GetLoadoutOrAutoEquip`) | `OwnerKind.Entity`, keyed by `instanceId` | A loadout *preference* is not permanent progress — `WebMatchService.cs:702-705`'s own doc comment: *"losing one on a session boundary degrades gracefully to auto-equip, never to nothing."* This is also **session-scoped**: `ClearSessionScopedBindings()` (`Program.cs:654`, the boot sweep) deletes every `entity:` binding on a session boundary |
   | **The unlock-ladder grants** (what the specimen actually *holds*, durable) | `OwnerKind.UniqueActor`, same `instanceId` | Fixed 2026-09-07 (`action-grant-owner-kind-durability`) specifically because a hard-earned grant under `Entity` would be silently wiped by the same session sweep. `OwnerKind.UniqueActor` is the durable, never-reused specimen identity |
   | **Item-granted actions** (a second, separate grant source) | `OwnerKind.Entity`, same `instanceId` | Session-scoped by nature (tied to what's currently equipped) |

   So `isHeld` must merge **both** grant scopes, exactly as `EquippedActionIdsFor` already does
   (`WebMatchService.cs:729-735`: `store.ListGrants(unlockLadderGrants).Concat(store.ListGrants(entityScope))`),
   filtered to `Kind: ActionKind.Skill` (excludes basic/innate — matching T21's existing "a basic/innate
   entry rejects as a category error" rule; those are never loadout-eligible in the first place). **Do
   not read only one scope** — that would either silently drop real unlock-ladder progress (reading only
   `Entity`) or silently drop item-granted actions (reading only `UniqueActor`), the exact defect class
   `EquippedActionIdsFor`'s own doc comment (`WebMatchService.cs:719-725`) was written to prevent.

3. **`isMidRun`**: same honest-gap placeholder as Dave's (`() => false`), for the same reason
   (`LoadoutEndpoints.cs:26-35` — no production "is this player mid-run" oracle exists yet anywhere at
   the Server layer). Record it the same way, do not fabricate one.

4. **Existence check before touching loadout state.** Confirm `instanceId` resolves to a real specimen
   (mirroring `LoadoutEndpoints.cs:45`'s `store.PlayerExists(playerId)` check for Dave — the exact
   store method for a specimen profile lookup is an implementation-time confirmation, not re-derived
   here) — return `404` otherwise, never construct an `OwnerScope` against an id nobody owns.

5. `GET` should return the **same shape** `GetLoadoutOrAutoEquip` already computes for battle setup — a
   real persisted loadout if one exists, the auto-equip fallback otherwise — so the endpoint never lies
   about what a battle would actually use.

## Tunables

None. This module is a routing/validation surface over an already-tunable system (rung, cap — owned by
A26/the unlock ladder).

## Commands

```powershell
dotnet test tests\FusionRpg.Server.Tests --filter "FullyQualifiedName~SpecimenLoadout"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~LoadoutStore"
```

## Project Structure

```
src/FusionRpg.Server/SpecimenLoadoutEndpoints.cs      (new, mirrors LoadoutEndpoints.cs)
tests/FusionRpg.Server.Tests/Actions/SpecimenLoadoutEndpointsTests.cs   (new)
```

## Code Style

Mirror `LoadoutEndpoints.cs` line for line where the shape is identical (route group, request DTO,
`Results.Ok`/`Results.Conflict`/`Results.NotFound` pattern) — the only real deltas are the owner-scope
construction and the `isHeld` predicate.

```csharp
g.MapPost("/{instanceId}", (string instanceId, SetLoadoutRequest body, RpgStore store) =>
{
    if (!store.SpecimenExists(instanceId)) return Results.NotFound(); // confirm real method name at implementation time
    var loadoutScope = new OwnerScope(OwnerKind.Entity, instanceId);       // slot assignment: session-scoped
    var unlockLadderGrants = new OwnerScope(OwnerKind.UniqueActor, instanceId); // durable
    var entityGrants = new OwnerScope(OwnerKind.Entity, instanceId);       // item-granted, session-scoped
    var held = store.ListGrants(unlockLadderGrants).Concat(store.ListGrants(entityGrants))
        .Where(g => store.GetAction(g.ActionId) is { Kind: ActionKind.Skill })
        .Select(g => g.ActionId)
        .ToHashSet(StringComparer.Ordinal);
    var result = store.SetLoadout(loadoutScope, body.ActionIds,
        isHeld: id => held.Contains(id),
        isMidRun: () => false);
    // ...same Ok/Conflict shape as LoadoutEndpoints.cs
});
```

## Testing Strategy

| Case | Expect |
|---|---|
| `GET` for a specimen with a persisted loadout | returns exactly that loadout |
| `GET` for a specimen with none | returns the auto-equip result, matching `GetLoadoutOrAutoEquip`'s own output |
| `POST` naming an action the specimen does not hold | rejected, no state change |
| `POST` naming a real held action | accepted, persisted, round-trips on the next `GET` |
| `POST` with a 6th action id | rejected, matching `LoadoutSet`'s existing cap rule (already tested at store level) |
| A basic/innate action id in the loadout list | rejected as a category error, matching T21's existing rule |
| Unknown `instanceId` | `404`, checked before any `OwnerScope` is constructed |
| An action granted only under `OwnerKind.UniqueActor` (unlock-ladder) | accepted as held — proves the merge isn't silently reading only `Entity` |
| An action granted only under `OwnerKind.Entity` (item-granted) | accepted as held — proves the merge isn't silently reading only `UniqueActor` |
| A withdrawn/no-longer-live grant (same `action_id`, source withdrawn) | **not** treated as held — `ListGrants` must reflect only live grants, matching T3's withdraw-by-source rule |

## Boundaries

**Always:** reuse `RpgStore.Loadouts`/`LoadoutSet.Validate` — do not re-implement validation at the
endpoint. Confirm the exact `OwnerScope` construction against `WebMatchService.cs:693-694` before
writing the endpoint, don't guess the `OwnerKind` member.

**Ask first:** the exact URL path if it collides with an existing or planned route naming convention
(recommend `/api/actors/{instanceId}/loadout` to match the equipment endpoint; confirm before shipping).

**Never:** accept a catalog-exists-but-not-held action id — that would let a player equip an action
nobody ever unlocked, defeating the entire unlock-ladder mechanism this program built.

## Success Criteria

1. A real specimen's held/equipped set is readable and writable over real HTTP.
2. Only actually-held actions can be equipped.
3. `GET` always reflects what a real battle (`WebMatchService`) would actually equip.
