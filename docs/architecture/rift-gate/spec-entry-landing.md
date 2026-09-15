# Spec: `entry-landing`

**Program:** `rift-gate` · **Map:** [../rift-gate-map.md](../rift-gate-map.md)
**Ideal:** [../rift-gate-ideal.md](../rift-gate-ideal.md) — decisions 8, 16
**Touches:** the web FE's landing decision (`web/fusion-rpg-web/src/app/**`), consuming the embed marker
**Depends on:** `first-open-signal` (soft — the "first open" fact), `story-scene` (sibling — the scene
that plays first), `commander-surface` (sibling — the commander name)

---

## Objective

**Land the player at the new-save entry point (`/sanctum`) on first open.** That is the whole module.
The map is explicit (owner, 2026-09-15): `/sanctum` is where `TitleScreen`'s **Continue** goes
(`TitleScreen.tsx:34`) and where the **eight rail layers** unlock by real play
(`railState.ts:65-74`); `/saves` was the rejected alternative because it has no unlockable pages, so it
cannot be what "other pages stay locked" describes.

**This module does NOT create a player.** Map Audit 2, fact 1: `SeedPlayerIfEmpty` inserts
`(1,'Player 1')` on any empty DB and sets `current_player_id=1` (`RpgStore.cs:3865-3878`), called from
schema unlock (`:149`). A player row **always** exists before the FE can read anything. The ideal's
"auto-create the player if needed" is **already built**; this module must not re-implement it.

**What actually changes.** Today the overlay loads the **server root** — `PlaySession.ActiveUrl =>
$"http://127.0.0.1:{p}"` (`PlaySession.cs:38`) has no path, and the SPA uses `HashRouter`
(`App.tsx:13`), so the landing route is `/` = **`TitleScreen`**. The injector navigates the same bare
URL (`OverlayViewHost.cs:47,281`), and the launcher the same (`MainWindow.xaml.cs:322`,
`OverlayWindow.xaml.cs:85-89`). So "land at `/sanctum` on first open" is a **landing decision the FE
makes** (it knows the first-open fact and the embed marker), not a URL the host hardcodes — the host
must keep navigating the bare origin so a later reload does not pin a stale route.

**Success:** the first open of the FE lands the player at `/sanctum`; a later open is not forced there;
a plain browser visit is unchanged; the player row is never created by this module.

**ASSUMPTIONS I'M MAKING:**

1. **The landing is a FE-side decision**, taken once, using the first-open fact (`first-open-signal`)
   and the embed marker (`overlay-hide`). The host's navigate target stays the bare server URL for
   every entry (`PlaySession.cs:38`; `OverlayViewHost.cs:281`; `OverlayWindow.xaml.cs:88`) — pinning
   `/sanctum` into the host URL would fight the `HashRouter` and the "remember last page" question
   `overlay-spec.md:281` already leaves open.
2. **"First open" is not the same as "first visit".** The fact is durable and once-per-player
   (`first-open-signal`), so the redirect fires **at most once per player**. A player who revisits later
   keeps their own route.
3. **The story scene runs first** (decision 4, `story-scene`). This module lands **after** it: the
   landing contract is "the entry point the story ends into", not "replace the story". `SanctumStage`
   already mounts the prologue when the story row is eligible
   (`SanctumStage.tsx:85-89,260-265`), so landing on `/sanctum` is exactly the surface where the scene
   plays — this module does not duplicate the trigger.
4. **The commander label is not this module's.** Decision 10: the first commander's display name = the
   player's name is owned by `commander-surface`. This module **depends on** that seam; it does not
   edit `spec-commander-list-api.md` or its tests (`MatchCommanderSnapshotTests` pins the literal at
   ~7 places, `rift-gate-map.md:325-330`).
5. **The embed marker may suppress browser-only chrome** (decision 16's own note), but only where a
   surface already exists; inventing new in-game chrome is out of scope.

→ Correct me now or implementation proceeds with these.

## Tech stack

- Web: React Router `HashRouter` (`App.tsx:13`), a one-shot landing component/module, existing bus
  queries (`useOnboarding`, `usePlayers`, both already used by `SanctumStage` and `TitleScreen`).
- No new route, no new stage, no new band (`game-gui-principles` GG-1: `/sanctum` is an existing stage).
- No server change — the player row and the first-open fact already exist (`RpgStore.cs:3865-3878`;
  `first-open-signal`).

## Commands

```powershell
cd web/fusion-rpg-web
npm test -- --run entryLanding
npm test -- --run TitleScreen
npm run build          # tsc --noEmit + vite build — type errors fail the build
npm run check:bundle   # entry-chunk budget; no new heavy import may land on the entry
```

Web verification in this repo has a **known mapping gap** (F1: the FE verification mapping stays
follow-up per `story-scene-map.md`'s ownership table), so the FE commands above are run directly and
the C# boundary selector is not applicable to a web-only spec.

## Project structure

| Path | Duty |
|---|---|
| `web/fusion-rpg-web/src/shell/entryLanding.ts` (new) | Pure decision: `shouldLandOnSanctum({ firstOpen, embedded, pathname })` → boolean. No React, no fetch — testable with fixtures. The one place the landing rule lives |
| `web/fusion-rpg-web/src/shell/useEntryLanding.ts` (new) | The one-shot effect: reads the first-open fact + embed marker, and navigates to `/sanctum` exactly once when the rule says so. Mounted beside the existing root effects (`App.tsx:12,21-24`) |
| `web/fusion-rpg-web/src/app/App.tsx` | Mount `useEntryLanding()` at the root, next to `ActorSurfaceCatalogBootstrap` |
| `web/fusion-rpg-web/src/app/TitleScreen.tsx` | **Unchanged behaviour**: its Continue already goes to `/sanctum` (`:34`). It stays the surface a **plain browser** visit meets, and the surface a returning player meets |
| `web/fusion-rpg-web/src/stages/sanctum/SanctumStage.tsx` | **Not edited** — it already mounts the story scene when eligible (`:85-89,260-265`). The landing target is the surface that already owns the scene trigger |

## Code Style

A pure decision function first, a thin effect second — the same split `SanctumStage`/`railState` and
`useGlobalKeys`/`keymap` already use. The decision is exhaustively testable with no DOM.

```ts
/**
 * The one landing rule. The FE decides, not the host: the host keeps navigating the bare server
 * origin (PlaySession.ActiveUrl has no path), and HashRouter's "/" is TitleScreen.
 *
 * First open only — the fact is durable and once-per-player, so a returning player keeps their route.
 * The story scene runs first (story-scene); landing on /sanctum is the surface that already mounts it.
 */
export function shouldLandOnSanctum(input: {
  firstOpen: boolean;
  embedded: boolean;
  pathname: string;
}): boolean {
  if (!input.firstOpen) return false;
  if (input.pathname === "/sanctum") return false; // already there
  return true;
}
```

## Testing Strategy

- **The decision is a truth table, tested without a DOM.** `shouldLandOnSanctum` is exercised for:
  first open ≠ on `/sanctum` → true; first open **on** `/sanctum` → false; not first open → false at any
  path. The embed marker is an input so a later policy change is a one-line test change — v1's rule does
  not branch on it (the landing is legitimate in a plain browser too, since the fact is keyed on the FE
  being opened, `first-open-signal`).
- **The one-shot is proven one-shot.** A test asserts a second render with `firstOpen` still true does
  **not** navigate again — the redirect is once per mount/fact, not a per-render loop. This is the
  module's one real hazard (a redirect loop), so it gets its own test.
- **No population count.** The landing asserts a route and a boolean — there is no roster/species/total
  anywhere. `/sanctum`'s unlock ladder (`railState.ts:65-74`) is rendered from derived state and is not
  asserted as a literal count (its own module already forbids that, `railState.ts:19-27`).
- **`TitleScreen` unchanged.** Its existing tests pass untouched; `title-continue` still navigates
  `/sanctum` (`:32-34`). A regression here would mean the landing moved the wrong surface.
- **Bundle budget.** `npm run check:bundle` must pass — the landing adds no heavy import
  (no Phaser/recharts on the entry).
- **Live (owner terminal):** first open (tombstone / F10) lands on `/sanctum`, and if the story is
  eligible the scene plays there; a second open does not re-force `/sanctum`; a plain browser visit to
  the root still shows `TitleScreen`. Read the result back through the normal path
  (`live-probe-standard.md` §3) — the actual route the page is on, not a navigation call's return.

## Boundaries

- **Always:** make the landing decision in the FE; keep it once-per-player via the durable fact; keep
  the host navigating the bare origin; leave `TitleScreen` as the browser/returning-player surface.
- **Ask first:** changing the landing target away from `/sanctum`; adding browser-only chrome
  suppression; pinning a route into the host's navigate URL.
- **Never:** create a player row (already built, `RpgStore.cs:3865-3878`); edit `spec-commander-list-api.md`
  or `MatchCommanderSnapshotTests` (owned by `commander-surface`, decision 10); duplicate the story
  scene trigger (owned by `story-scene`, decision 4); assert a population count or the rail's layer
  count; add a route or a stage.
- **ActorHub gate: N/A.** No actor combat/derived/AppliedCombat magnitude is produced or consumed; no
  fold is invented.

## Tunables

| Number | Home | Why |
|---|---|---|
| **None** | — | The landing is a route decision and a durable boolean. It has no pacing, size, magnitude, or rate. If a future "how long to wait before redirecting" is ever wanted, it would be a structural delay with a comment, never a tuning key — but v1 redirects immediately and introduces nothing |

The story scene's own pacing (`beatTransitionMs`, etc.) belongs to `story-scene`'s
`data/tuning/story-scene-ui.v1.json`, not here.

## Success Criteria

- [ ] On the first open the FE lands on `/sanctum`; the story scene, when eligible, plays there
      (`SanctumStage.tsx:85-89,260-265`) — the trigger is **not** duplicated.
- [ ] The redirect is **once per player**, driven by the durable first-open fact; a returning player
      keeps their route.
- [ ] A plain browser visit to the root still shows `TitleScreen`; `title-continue` still goes to
      `/sanctum` (`TitleScreen.tsx:34`).
- [ ] **No player row is created** by this module — a real `RpgStore` player row already exists
      (`RpgStore.cs:3865-3878`), asserted by not calling `CreatePlayer` from the landing path.
- [ ] The host still navigates the bare server origin in both host modes (`PlaySession.cs:38`;
      `OverlayViewHost.cs:281`; `OverlayWindow.xaml.cs:88`).
- [ ] No redirect loop: a second render does not re-navigate.
- [ ] `commander-surface`'s seam is **consumed, not edited**; no test there moves because of this module.
- [ ] No new route/stage/band; `npm run build` and `npm run check:bundle` pass.
- [ ] ActorHub gate N/A stated; no tuning number introduced.

## Open Questions

1. **Does the landing also apply when the story is *not* eligible?** Decision 8 says land on the default
   page after the story. If a player's first open happens with the story already settled (a
   pre-existing victory sets the row acknowledged/skipped, `RpgStore.Onboarding.cs:44-58`), the story
   does not play — this spec still lands them on `/sanctum` (the new-save entry point), which is
   consistent with the decision. Confirm if the owner wants the settled case to behave differently.
2. **The first-open fact's read timing.** The landing effect needs the durable fact before it can
   decide. It reads through the normal onboarding path (which `SanctumStage` already queries,
   `:80`); if the read is still in flight the landing waits rather than redirecting on a default. This
   is a sequencing detail for `/plan`, not a behaviour ambiguity: **never redirect on an unknown fact**.
