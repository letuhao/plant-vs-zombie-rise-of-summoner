# Spec: `overlay-hide`

**Program:** `rift-gate` · **Map:** [../rift-gate-map.md](../rift-gate-map.md) — **Audit correction 1**
**Ideal:** [../rift-gate-ideal.md](../rift-gate-ideal.md) — decisions 9, 14, 16
**Touches:** Contracts (the command-name constant), Server (one endpoint + the `SendInjectorCommand` helper),
Injector (the drain case + local hide + the pipe-client verb), Launcher (the pipe verb + `ParseCommand` row),
Web (the **Leave** control), and `docs/launcher/overlay-spec.md` (**required deliverable**)
**Depends on:** — (independent of every other module)

---

## Objective

Give the **web FE a way to close its own window**: the FE's **Leave** control → the **existing**
server→injector command path → the injector hides locally (injector-hosted) or relays a new pipe verb
(launcher-hosted). Also: **both hosts set an embed marker at open**, so the page knows it is embedded
and that Leave can work. And the hard contract: **hide must not acknowledge the story.**

**The corrected shape (map Audit 1) — the injector is the router, not a new transport.** Both hide
sinks already exist: launcher `HideOverlayToGame()` (`MainWindow.xaml.cs:354`), injector
`OverlayViewHost.Hide()` (`OverlayViewHost.cs:67`), plus Esc in both (`OverlayWindow.xaml.cs:25-32`;
`OverlayViewPolicy.IsCloseKey`, `:22-23`). What does **not** exist is any way for a web page to reach
either: verified zero `WebMessageReceived` / `postMessage` / host-object channel in `src/` or
`web/fusion-rpg-web/src`. A direct FE→launcher channel would be a **second** transport in a program
whose premise is that the transport ships; the relay reuses one path and adds one verb.

1. FE **Leave** → the **existing** server→injector command path (`Command` SignalR push at
   `Program.cs:1622`, with the `InjectorCommandInbox` HTTP fallback — `software-architecture.md:148`,
   `InjectorCommandInbox` cap 2000, injector polls `/api/cheats/commands/pending` at
   `RpgClient.cs:177-190`).
2. Injector receives it (drain at `RpgClient.cs:121-136` → `CheatCommandRunner.Enqueue`).
   **Injector-hosted:** call `OverlayViewHost.Hide()` in-process — done.
   **Launcher-hosted:** relay the new `hide` verb over the **existing** pipe
   (`OverlayPipeServer.cs:14-20`, today `toggle`/`ping`; client `OverlaySwitch.Send`, `:172`).
3. Launcher's pipe handler (`MainWindow.xaml.cs:286-299`) routes `hide` to the same
   `HideOverlayToGame()` the Esc key uses.

**The drain is already generic, despite its name.** `CheatCommandRunner` is documented as the
*"main-thread drain for SignalR/HTTP cheat commands"* (`:19`), but the vocabulary it dispatches is
**not** cheats: the same drain already runs `reload-stats`, `pvz.stats.reload`,
`aptitudes.allocation.reload`, `commander.snapshot.reload`, `passive-tree.bound-atoms.reload`,
`lawn-deploy.roster.reload`, and `power.index.reload` (`:57,63,69,82,88,94,100`), while the injector's
SignalR handler enqueues them on the same `Command` event (`RpgClient.cs:121`). So `overlay.hide` rides
a seam that already carries refresh commands, **not** a gameplay-cheat surface. The `[cheat-cmd]` log
label (`:41`) is the same misnomer. **Name the vocabulary properly so the misnomer does not spread;
do NOT rename `CheatCommandRunner` in this program** (that would touch every existing caller).

**Success:** with the view open, clicking **Leave** hides it and restores game focus, in **both** host
modes; with **no** embed marker (a plain browser visit) Leave is not offered; hide never writes the
story ledger.

**ASSUMPTIONS I'M MAKING:**

1. **The named vocabulary constant.** The new command name is `overlay.hide`, carried as a
   **`const string` on a new Core class** (e.g. `FusionRpg.Core.Overlay.OverlayCommandNames.Hide`),
   following the existing precedent `EffectGrantRehydrate.ApplyCommandName = "effects.grants.apply"`
   (`EffectGrantSession.cs:60`) — used at `UniqueActorService.cs:256`. Not a bare literal dropped into
   the drain.
2. **The `SendInjectorCommand` helper exists twice** — `Program.cs:1617` and
   `UniqueActorService.cs:281` (map Audit 1). **The spec picks `Program.cs`'s**: the new endpoint is a
   page→host close request, not a unique-actor domain operation, and `UniqueActorService`'s copy is
   scoped to that service. Adding a **third** is explicitly forbidden. (If implementation finds the
   `Program.cs` copy is not reachable from the endpoint's minimal-API lambda without a capture, the
   correct fix is to hoist/adapter the one helper, never to duplicate it a third time.)
3. **`overlay.hide` is idempotent and non-blocking.** Hiding an already-hidden view is a no-op
   (`OverlayViewHost.PumpTick`'s `case Command.Hide: if (_visible) HideWindow(...)`, `:239-241`; the
   launcher's `HideOverlayToGame` is likewise safe to call when hidden, `:354-359`). The FE never waits
   on the effect.
4. **The embed marker is the query flag `?embed=1`, appended by each host at navigate time** (decision
   16, owner-confirmed 2026-09-15). Not the hash: the SPA's `HashRouter` owns the hash. Verified
   same-origin-safe: `OverlayViewPolicy.IsSameOrigin` compares only scheme/host/port (`:53-55`), so a
   query marker does not trip the lock. The page must behave correctly with **no** marker.
5. **The pipe guard is extended, not paralleled.** `OverlayPipeContractGuardTests` already asserts
   *"every verb the client sends is one the server accepts"* (`:39-57`), so adding `Send("hide", ...)`
   plus a `"hide" => OverlayPipeCommand.Hide` row makes the new verb **guard-covered by construction**.
   The guard's fixture is extended; no second guard test is added.

→ Correct me now or implementation proceeds with these.

## Tech stack

- Contracts: one `CommandDto.Name` string constant (Core class, `EffectGrantRehydrate` precedent).
- Server: one endpoint that builds the command via the existing helper; Contracts unchanged otherwise.
- Injector: one drain case + `OverlayViewHost.Hide()` (injector host) + `OverlaySwitch.Send("hide")`
  (launcher host) — the pipe client already exists.
- Launcher: one enum value + one `ParseCommand` row + one `OnOverlayPipeCommand` case.
- Web: the **Leave** control + an embed-marker read. TS/React, existing `sendJson`/bus helpers
  (`rest.ts`), `<Button>` from `@/ui`.

## Commands

```powershell
# Contracts + Core constant
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Overlay"

# The pipe contract guard (must stay green with the new verb)
dotnet test tests/FusionRpg.Guard.Tests --filter "FullyQualifiedName~OverlayPipeContract"

# Launcher pipe parse tests
dotnet test tests/FusionRpg.Launcher.Tests --filter "FullyQualifiedName~OverlayPipeServer"

# Server endpoint
dotnet test tests/FusionRpg.Server.Tests

# Web: Leave control + marker behaviour + story-ledger non-write
cd web/fusion-rpg-web; npm test -- --run overlayLeave; npm run build
```

### Boundary selection (never the whole suite — AGENTS.md verification boundary)

```powershell
.\scripts\verify-change.ps1 -Paths `
  src/FusionRpg.Core/Overlay/OverlayCommandNames.cs,`
  src/FusionRpg.Contracts/Dtos.cs,`
  src/FusionRpg.Server/Program.cs,`
  src/FusionRpg.Launcher/Services/OverlayPipeServer.cs,`
  src/FusionRpg.Launcher/MainWindow.xaml.cs,`
  src/FusionRpg.Injector/CheatCommandRunner.cs,`
  src/FusionRpg.Injector/Hud/OverlaySwitch.cs,`
  src/FusionRpg.Injector/Hud/OverlayViewHost.cs `
  -Session rift-gate-spec-20260915-c41a
```

This change **crosses module boundaries** (Contracts + Server + Injector + Launcher + Web) — one of the
three cases where the fuller local suite is warranted at the end
(`AGENTS.md` verification boundary: "a change that crosses program or module boundaries"). Scoped
selection per-path still runs first; the cross-boundary full run is the finishing checkpoint, not the
reflex.

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Core/Overlay/OverlayCommandNames.cs` (new) | The named vocabulary: `public const string Hide = "overlay.hide";` with a doc comment explaining *why* it lives here (the drain's misnomer) and that `CheatCommandRunner` is **not** renamed by this program |
| `src/FusionRpg.Server/Program.cs` | One endpoint at **`POST /api/overlay/leave`** (owner-decided 2026-09-15; named for the player action, not the mechanism) that builds `new CommandDto { Name = OverlayCommandNames.Hide }` and sends via the **existing** `SendInjectorCommand` (`:1617`) |
| `src/FusionRpg.Injector/CheatCommandRunner.cs` | One drain case: `if (name is OverlayCommandNames.Hide)` → injector-hosted `OverlayViewHost.Hide()` else `OverlaySwitch.RequestHide()` (pipe). Sits beside the refresh cases (`:57-105`) |
| `src/FusionRpg.Injector/Hud/OverlaySwitch.cs` | `RequestHide()` enqueues the pipe `hide` verb (launcher host) or calls `OverlayViewHost.Hide()` (injector host) — reusing the same `Send`/`InjectorHosted` split already at `:58-59,172`. **Never touches the story ledger** |
| `src/FusionRpg.Launcher/Services/OverlayPipeServer.cs` | `OverlayPipeCommand.Hide` + the `"hide" => OverlayPipeCommand.Hide` row (`:81-86`) |
| `src/FusionRpg.Launcher/MainWindow.xaml.cs` | `case OverlayPipeCommand.Hide: HideOverlayToGame();` (`:286-299`) — the **same** hide Esc uses (`:354`) |
| `src/FusionRpg.Injector/Hud/OverlayViewHost.cs` · `src/FusionRpg.Launcher/OverlayWindow.xaml.cs` | **The embed marker at open** (both hosts): append the marker at `Navigate` (`OverlayViewHost.cs:275-287`) and at `EnsureWebAsync`'s navigate (`OverlayWindow.xaml.cs:85-89`) |
| `web/fusion-rpg-web/src/shell/OverlayLeave.tsx` (new) | The **Leave** control: renders only when the embed marker is present; posts the leave request; behaves correctly with no marker (not offered) |
| `web/fusion-rpg-web/src/shell/overlayEmbed.ts` (new) | Pure read of the embed marker from the **query string** (`?embed=1`, owner-decided). Testable with no host |
| `docs/launcher/overlay-spec.md` | **Required deliverable** — see below |

## Code Style

Core constant mirrors `EffectGrantRehydrate`'s shape; the drain case mirrors the existing refresh
cases (one `if (name is ...)` guard, an early `return`, no throw escaping).

```csharp
namespace FusionRpg.Core.Overlay;

/// <summary>
/// Names for the presentation commands the web FE may send to the host over the server→injector
/// command seam. They ride <c>CheatCommandRunner</c>'s drain, whose name is a misnomer: it already
/// carries refresh/presentation commands (reload-stats, pvz.stats.reload, …), not cheats. The class
/// name is NOT renamed by this program — that would touch every existing caller — so this constant
/// is where the vocabulary is named correctly instead.
///
/// <see cref="Hide"/> closes the overlay window WITHOUT acknowledging any story: the story ledger is
/// written only by the FE's own ack path. One Esc (or one Leave) must not burn the prologue.
/// </summary>
public static class OverlayCommandNames
{
    public const string Hide = "overlay.hide";
}
```

Web Leave control, mirroring the existing Button idiom and honest-absence rule:

```tsx
// Renders only when the page is actually embedded — a plain browser visit gets no dead button.
export function OverlayLeave() {
  const embedded = useOverlayEmbed();
  if (!embedded) return null;
  return (
    <Button variant="ghost" size="sm" data-testid="overlay-leave"
            onClick={() => postOverlayLeave()}>
      Leave
    </Button>
  );
}
```

## Testing Strategy

- **The story ledger is never written by hide — a hard contract, tested at both ends.**
  `RiftPrologueDialog.tsx:131-133` currently treats dismissal (`onOpenChange(false)` and
  `onEscapeKeyDown`) as a durable `completed | skipped` write via `finish(...)`. The hide path must
  **not** reach `acknowledge.mutateAsync` (`:106-110`) nor `AcknowledgeOnboardingStory`
  (`RpgStore.Onboarding.cs`). Two tests: (a) FE — a leave/hide does not call the ack mutation;
  (b) Server — the leave endpoint writes **no** `rpg_onboarding_story` row (read back through the
  normal onboarding read path, `OnboardingEndpoints.ProjectState`). This is the map's
  `overlay-hide → story-scene` boundary.
- **Idempotency + non-blocking.** Hide on an already-hidden view is a no-op; the endpoint returns
  without waiting on the effect. Both sinks already guard (`OverlayViewHost.cs:239-241`;
  `MainWindow.xaml.cs:354-359`).
- **The pipe contract guard, extended.** `OverlayPipeContractGuardTests` (`:39-57`) gains
  `hide` in the client's sent-verb set and the server's `ParseCommand` row — the guard proves the two
  sides agree, exactly as it does for `toggle`/`ping`. No second guard test.
- **Embed marker — same-origin safety is a real test, not a comment.**
  `OverlayViewPolicy.IsSameOrigin` (`:48-56`) is unit-tested today; a new case asserts the marker URL
  still passes same-origin (scheme/host/port unchanged). A second test asserts the page with **no**
  marker offers no Leave control (honest absence, gap 9 option (ii)).
- **The marker is set by both hosts.** `OverlayViewHost`'s navigate (`:275-287`) and the launcher's
  (`OverlayWindow.xaml.cs:85-89`) both append it — a test pins both call sites (contract guard shape,
  matching `OverlayPipeContractGuardTests`' source-read style).
- **No derived population count is asserted.** Endpoint and ledger tests assert contracts (a command
  name round-trips, a ledger row is unchanged/absent, a verb is accepted) — never a count that grows
  with content.
- **Live (owner terminal, both host modes):** `overlayHost=launcher` and `overlayHost=injector` — open
  the view, click **Leave**, the view hides and the game regains focus; a plain browser visit shows no
  Leave. Read the result back through the normal path (`live-probe-standard.md` §3): the window is
  actually hidden and the game has focus, not merely a 200 response.

## Boundaries

- **Always:** reuse the existing server→injector command path and the existing pipe; route launcher
  `hide` to the **same** `HideOverlayToGame()` Esc uses (one hide, one route more — `overlay-spec.md:80`);
  name the command via the Core constant; keep pipe I/O off the Unity main thread; log every failure.
- **Ask first:** adding a **third** `SendInjectorCommand` copy; renaming `CheatCommandRunner`; adding
  any second FE→host channel (WebView2 `postMessage`/`WebMessageReceived`); changing the default host.
- **Never:** acknowledge (or touch) the story ledger from hide; make `overlay.hide` a gameplay command;
  a bare command literal dropped into the drain; a new transport beside the shipped one; a dead **Leave**
  button in a plain browser (`overlay-spec.md`'s honest-control rule).
- **ActorHub gate: N/A.** No actor combat/derived/AppliedCombat magnitude is produced or consumed; no
  fold is invented. The command is presentation only.
- **Required document deliverable:** `docs/launcher/overlay-spec.md` — its own header says *"This
  document is the contract for the feature — update it before changing behavior"* (`:3`). Adding a
  third pipe verb, a page→host close path, and a host embed marker **is** a behaviour change to that
  feature, so the spec must be updated in the same commit: the transport table (`:108`) gains `hide`,
  the verb list (`:277`) gains the third verb, and the behavior contract (`:74-86`) records the
  page→host close and the embed marker. The existing rule *"All three entry points converge on **one**
  toggle method. No entry point gets its own behavior"* (`:80`) is what the new path must not violate
  — `hide` is the **same hide**, one more route.

## Tunables

| Number | Home | Why |
|---|---|---|
| Probe interval / debounce / send timeout if the bridge adds any | `data/tuning/overlay.v1.json` → `switchState` (already has `debounceMs`, `probeIntervalMs`, `sendTimeoutMs`, `:21-25`) | Existing group; no new number is introduced by hide (hide is a one-shot command, not a debounced toggle) |
| Nothing else | — | Hide has no placement, size, magnitude, or pacing number. A missing `switchState` key is a load rejection naming it (`OverlayTuning.cs:56-60`) |

## Success Criteria

- [ ] FE **Leave** hides the overlay in **both** host modes and restores game focus; identical result to
      Esc (`OverlayWindow.xaml.cs:25-32`; `OverlayViewPolicy.IsCloseKey`).
- [ ] **Hide never writes the story ledger** — no `completed`/`skipped` on hide, proven at the FE
      (no ack mutation) and the Server (no `rpg_onboarding_story` change), read back through the normal
      onboarding path.
- [ ] The command is `OverlayCommandNames.Hide` (`"overlay.hide"`), a named Core constant; **no** bare
      literal in the drain; `CheatCommandRunner` **not** renamed.
- [ ] One `SendInjectorCommand` helper is used (the `Program.cs:1617` one); **no third copy** exists
      afterward.
- [ ] The pipe guard covers the new verb by extension: every client-sent verb is one the server accepts.
- [ ] Both hosts set the embed marker at open; same-origin still passes; **no** marker → **no** Leave
      control (honest absence).
- [ ] `docs/launcher/overlay-spec.md` is updated **in this commit** (transport table, verb list,
      behavior contract), and its "one toggle method, no entry point gets its own behavior" rule holds.
- [ ] ActorHub gate N/A stated; no gameplay command; no new transport; no new tuning number.

## Open Questions

> **Decided (owner, 2026-09-15)** — the three choices below were proposed defaults and are now
> **confirmed**, so they are recorded as decisions, not questions:
>
> 1. **Endpoint route: `POST /api/overlay/leave`** — named for the player action, not the mechanism; the
>    internal command stays `overlay.hide` (`OverlayCommandNames.Hide`). Settled; no behaviour difference
>    from a route mirroring the verb.
> 2. **Embed marker: a query flag `?embed=1`** — not the hash. The SPA's `HashRouter` (`App.tsx:13`) owns
>    the hash, so a marker there would be parsed as a route; `TitleScreen` already uses query params
>    (`?system=1`/`?dev=`). Same-origin is unaffected (`OverlayViewPolicy.IsSameOrigin` compares only
>    scheme/host/port, `:53-55`).
>
> No open question remains in this module.

1. **Should `hide` also be reachable from Esc/F10 inside the FE?** No — the native host already owns
   those keys (`OverlayViewPolicy.IsCloseKey`), the FE must **not** bind F10 (`keymap.ts:18`), and the
   FE's Esc is the layer stack's (`useGlobalKeys.ts`). The **Leave** control is the page's only close
   affordance; the host keys remain the host's. This is a boundary statement, not an open question.
