# Spec: almanac-recipes-fix

Module in the [almanac map](../almanac-map.md). No dependencies.

## Objective

`GET /api/recipes` returns `{"items":[]}`. **Corrected after adversarial review 2026-08-23** — the
original version of this spec claimed the injector log showed zero "recipe" lines after triggering
the dump and inferred `EnqueueRecipes()` "ran to completion but produced nothing, silently." That
inference doesn't hold: `CheatActions.DumpRecipes()` unconditionally calls
`CheatState.Note("recipes enqueued")` after `EnqueueRecipes()` returns
([CheatActions.cs:719-727](../../../src/FusionRpg.Injector/CheatActions.cs)), and `CheatState.Note`
always writes `[cheat] recipes enqueued` at Info level
([CheatState.cs:631-634](../../../src/FusionRpg.Injector/CheatState.cs)). If the action dispatched
at all, that line should exist. Its absence means either the cheat command never reached
`DumpRecipes` in the first place, or the log file checked wasn't the live one — a different,
unexplained problem the original diagnosis never considered. **This module now starts by
re-establishing what actually happens, not by trusting the prior "confirmed live" claim.**

**A second, independently confirmed defect** makes the manual trigger moot regardless: `_recipesDumped`
is latched by an **automatic** path, not just the cheat action.
[RpgClient.cs:128](../../../src/FusionRpg.Injector/RpgClient.cs) calls
`GameHooks.RequestTypeCatalog()` unconditionally after every SignalR connect attempt (success or
HTTP-fallback failure) — i.e. at injector boot, before the player is necessarily in a level.
[GameHooks.cs:466](../../../src/FusionRpg.Injector/GameHooks.cs) does the same on a retry path.
`PumpMainThread()` ([GameHooks.cs:106-111](../../../src/FusionRpg.Injector/GameHooks.cs)) drains
that flag into `EnqueueTypeCatalog()`, which calls `EnqueueRecipes()`
([GameHooks.cs:126](../../../src/FusionRpg.Injector/GameHooks.cs)) — setting `_recipesDumped = true`
almost immediately every session, regardless of whether `PlantMixTreeManager` had anything to give
yet. By the time the owner is in a level and presses the cheat button, `EnqueueRecipes` returns
instantly at its own early-return guard, having logged nothing further. **This is a confirmed code
defect, independent of whatever explains the missing "recipes enqueued" log line above.**

```csharp
// GameHooks.cs — EnqueueRecipes, current shape
public static void EnqueueRecipes()
{
    if (_recipesDumped) return;               // <- already true by the time a level exists, see above
    try
    {
        PlantMixTreeManager.Init();
        var dict = PlantMixTreeManager.ChildToParents;
        if (dict == null) return;              // <- silent no-op, no log
        ...
        if (entries.Count > 0)
            RpgHost.Client?.Enqueue(...);       // <- null-conditional: a null Client silently drops every entry too
        _recipesDumped = true;                  // <- set even on a null dict / empty result
    }
    catch (Exception ex) { RpgHost.Log.Warning("catalog recipes: " + ex.Message); }
}
```

Candidate causes, none assumed — this module starts with diagnosis, not a fix:

1. **Confirmed:** the auto-latch above makes the manual trigger a guaranteed no-op after the first
   few seconds of any session.
2. `PlantMixTreeManager.Init()`/`ChildToParents` may only populate inside an active board/level —
   relevant to when the *automatic* first call (which fires before a level exists) should run, not
   just the manual one.
3. Every `ParentA`/`ParentB`/`Result` cast may be throwing inside the per-entry `try/catch continue`
   ([GameHooks.cs:241-242](../../../src/FusionRpg.Injector/GameHooks.cs)), silently dropping every
   entry even when `dict` is non-null.
4. `RpgHost.Client?.Enqueue(...)` is null-conditional — a not-yet-connected client silently discards
   a fully-built entry list.
5. **Not yet ruled out:** loss on the server side. `RpgStore.cs:2383` routes `"catalog.recipes"` to
   `ProjectRecipes`, which ends in a swallowing `catch { /* malformed */ }`
   (`RpgStore.cs:2493`) — the empty `GET /api/recipes` result is equally consistent with server-side
   loss as with anything upstream. The fix must not assume the defect is Injector-only.

Done means: recipes populate live inside a level, the auto-latch no longer forecloses that before it
can happen, and every remaining silent-drop point (dict null, cast failure, null client, server
parse failure) logs enough to distinguish "genuinely nothing to send" from "something ate it."

## Design (locked on approval)

1. **Fix the auto-latch first — this is not conditional on diagnosis.** `_recipesDumped` must only
   latch after a genuine attempt succeeded or definitively found nothing *while board-scoped data was
   available* — not on the very first boot-time call, which fires before a level exists. Candidate:
   don't call `EnqueueRecipes()` from `EnqueueTypeCatalog()`'s automatic path at all (recipes need a
   board; the rest of the type catalog doesn't), and instead trigger it once from a board-start hook
   or leave it purely cheat-triggered — needs the same live check as diagnosis, but this part of the
   fix is already justified by the code alone, independent of what task 2 finds.
2. **Diagnose the rest in a level**, with the auto-latch fixed: trigger `recipes`, confirm the
   `[cheat] recipes enqueued` line actually appears (re-establishing whether the original missing-log
   observation was a dispatch problem, a stale-log problem, or something else), then check `GET
   /api/recipes`.
3. **If still empty:** add logging on the `dict == null` return, the per-entry cast-failure `catch`
   (count + first exception message), the null-`Client` case, and — since server-side loss is not
   ruled out — verify `ProjectRecipes`'s catch isn't the actual sink (check server logs / add a log
   there too if needed).
4. **Un-latch on failure**, same rationale as before: `_recipesDumped` should not permanently freeze
   a legitimately-too-early or malformed-data outcome as "done."

Exact fix is intentionally left open pending the live diagnosis in tasks 2-3 — do not lock further
code changes before each cause is confirmed (design-gate evidence rule: test the constraint before
declaring it). Task 1 (the auto-latch) is the exception — it's a confirmed defect from static
reading, not a hypothesis.

## Commands

Same as [almanac-capture-fix](spec-almanac-capture-fix.md) — Injector-only, `FUSIONRPG_GAME_DIR`
build, no automated test run.

## Structure

```
src/FusionRpg.Injector/GameHooks.cs   (EnqueueRecipes: auto-latch fix + logging)
```

May also touch `src/FusionRpg.Data/Sqlite/RpgStore.cs` (`ProjectRecipes`) if server-side loss is
confirmed (candidate cause 5) — not assumed, checked live first.

## Testing strategy

Live-only, same class as almanac-capture-fix:

1. **Confirm the auto-latch fires before a level exists** (pre-fix baseline): fresh injector launch,
   check the log immediately for `EnqueueTypeCatalog`/`RequestTypeCatalog` activity and whether
   `_recipesDumped` is already `true` before entering any level — establishes the defect this spec
   opens with is real, not assumed.
2. Apply the auto-latch fix; start a level; trigger `POST /api/cheats/action {"action":"recipes"}`;
   confirm `[cheat] recipes enqueued` now actually appears in the log (re-establishing what the
   original "zero log lines" observation meant).
3. `GET /api/recipes` — expect non-empty `items`, each with `parentA`/`parentB`/`result` + resolved
   names.
4. If step 3 is still empty: check the new diagnostic log lines (dict-null, cast-failure, null-client)
   to identify which candidate cause is real; check server logs / `ProjectRecipes` behavior too —
   don't assume the defect is Injector-only.
5. Regression: confirm a second trigger within the same process is a no-op only when the first
   trigger actually succeeded (not when it silently found nothing).

## Boundaries

- **Always:** log why `EnqueueRecipes` produced zero entries — silence is the actual defect here,
  not just the empty result.
- **Ask first:** nothing structural expected; if the fix turns out to need calling
  `PlantMixTreeManager.Init()` from a different hook (e.g. board start) rather than on-demand from
  the cheat action, confirm that's acceptable before restructuring the trigger.
- **Never:** guess at the fix and ship it unverified — this module's whole point is that the
  previous state (silent empty result) already looked "done."

## Success criteria

1. `GET /api/recipes` returns real fusion-tree entries when triggered from inside a level.
2. The automatic boot-time call (via `RequestTypeCatalog`) no longer permanently latches
   `_recipesDumped = true` before a board exists — proven by test 1 above.
3. If `EnqueueRecipes` legitimately finds nothing (e.g. called too early), the log says so instead
   of looking identical to success — including the null-`Client` and per-entry cast-failure cases.
4. Server-side loss (`ProjectRecipes`'s swallowing catch) is either ruled out with evidence or fixed
   alongside — not assumed away.
