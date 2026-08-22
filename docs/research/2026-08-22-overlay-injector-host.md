# Spike: hosting the overlay view inside the game process

**Date:** 2026-08-22 · **Task:** overlay-switch T6 · **Contract:** [../launcher/overlay-spec.md](../launcher/overlay-spec.md) §Wave 2
**Verdict: not yet a go — nothing found blocks it, but three of five questions still need a running game.**

Wave 1 hosts the web view in the Launcher, so the overlay only exists when the player started through the Launcher. Wave 2 would move the view into the game process to remove that constraint. This spike answers what can be answered off the lawn, and states plainly what cannot.

## Method

A throwaway harness built against **`net6.0` — the injector's exact TFM, deliberately not `net6.0-windows`** — creating a real borderless `WS_POPUP` window via `user32` and attaching a `CoreWebView2Controller` to it. No WPF, no WinForms, no visible window. Run on this machine (Windows 11, Evergreen runtime **151.0.4129.93**).

```
runtime      : .NET 6.0.36
process arch : X64
PASS step0   : Evergreen runtime found, version 151.0.4129.93
PASS step1   : borderless HWND created without WPF/WinForms
PASS step2   : CoreWebView2Environment created (browser 151.0.4129.93)
PASS step3   : CoreWebView2Controller attached to a raw HWND
INFO cost    : environment + controller took 314 ms cold / 266 ms warm
PASS step4   : controller.Close() returned cleanly
PASS step5   : missing runtime surfaces as WebView2RuntimeNotFoundException (catchable, no crash)
```

## Answers

| # | Question | Verdict |
|---|---|---|
| 1 | Does the WebView2 loader initialize in an IL2CPP process? | **Partial go** — proven for a plain `net6.0` process; the IL2CPP/BepInEx half is untested |
| 2 | Can a borderless window owned by the game HWND hold z-order and focus? | **Unproven** — needs the game |
| 3 | Does input routing survive alt-tab? | **Unproven** — needs the game |
| 4 | Does teardown run cleanly on exit and on crash? | **Partial go** — clean exit proven; crash path unproven |
| 5 | What happens with no Evergreen runtime? | **Go** — `WebView2RuntimeNotFoundException`, catchable, no crash |

### 1 — Loader init (partial go)

The non-WPF path works and is reachable from the injector's own target framework. What this does **not** prove is the part unique to our host: whether BepInEx resolves `Microsoft.Web.WebView2.Core.dll` from the plugins folder, and whether the native `WebView2Loader.dll` P/Invoke resolves there too. Both are ordinary assembly-loading questions, but they are exactly the kind that behave differently under a modded IL2CPP game, so they stay open until run in-game.

**Constraint discovered — this is the main design output of the spike.** The WebView2 objects are apartment-bound. The first harness created the environment on a pumped STA thread but read `.Result` from a `ContinueWith` on the thread pool, and every call failed with:

```
NotImplementedException: Unable to cast to Microsoft.Web.WebView2.Core.Raw.ICoreWebView2Environment.
```

That message names version skew as the likely cause and sends you to the versioning docs, which is a false trail — the versions were fine. The real cause was crossing apartments. Wave 2 must **create and consume the environment, the controller, and the CoreWebView2 on one thread that it owns and pumps**, and never hand those objects to another thread. Note this is a *second* pump alongside Unity's own: the injector must never borrow the Unity main thread for it.

For the record, since the failure first looked like version skew: the Launcher's pinned SDK **1.0.2903.40** was re-tested against runtime 151.0.4129.93 after the harness was fixed and passes all five steps. **Wave 1 is not affected.**

### 4 — Teardown (partial go)

`controller.Close()` returns cleanly in a console process. The case that actually matters — the game process dying while a WebView2 host thread is live — is unproven, and it is the risk that decides whether this is safe to ship at all. A leaked `msedgewebview2.exe` after every crash would be worse than the Launcher host.

### 5 — Missing runtime (go)

Absence is a catchable `WebView2RuntimeNotFoundException`, not a crash, so wave 2 can degrade exactly as wave 1 does: show install instructions, keep working without the view.

### Cost

~270–315 ms to stand up environment + controller. Off the Unity thread this is invisible, but it means the view cannot be created lazily on the button click without a visible stall — wave 2 should create it once, in the background, and then only show/hide.

## What ships into the game folder

`Microsoft.Web.WebView2.Core.dll` plus the native `WebView2Loader.dll` (x64, ~166 KB). **Chromium is not bundled** — the Evergreen runtime is a machine-wide install. So wave 2 does not violate the "never ship an embedded browser inside the game folder" boundary, which was written against the ~150 MB UnityWebBrowser payload. Adding the package to the injector is still a **new NuGet dependency in the game process**, which the spec lists as ask-first.

## The pattern, if wave 2 proceeds

```csharp
// One thread, owned and pumped by us. Never Unity's main thread, never the pool.
var t = new Thread(HostLoop); t.SetApartmentState(ApartmentState.STA); t.Start();

// Inside HostLoop, after CreateWindowEx:
var envTask = CoreWebView2Environment.CreateAsync(userDataFolder: dir);
PumpUntil(() => envTask.IsCompleted);          // PeekMessage/Translate/Dispatch
var env = envTask.Result;                       // read on THIS thread
var ctlTask = env.CreateCoreWebView2ControllerAsync(hwnd);
PumpUntil(() => ctlTask.IsCompleted);
var controller = ctlTask.Result;                // read on THIS thread
```

The harness lives outside the repo (session scratchpad) because promoting it would add the NuGet dependency this spike has not yet earned.

## To close questions 2, 3, 4 (owner, in-game)

Fold into the T4 live session; each is one observation:

1. **Z-order** — with the host window created as `WS_POPUP` + topmost over the game HWND, does it actually cover a borderless-fullscreen PVZ Fusion, or does the game repaint over it?
2. **Focus** — click into the view, type; does the game keep receiving input it shouldn't, and does the view keep the caret?
3. **Alt-tab** — leave and return; does the view still take clicks, and does it stay attached to the right monitor?
4. **Teardown, clean** — quit the game normally; does `msedgewebview2.exe` exit with it?
5. **Teardown, crash** — kill the game from Task Manager; check for an orphaned `msedgewebview2.exe`. **A leak here is a no-go.**

## Recommendation

Do not start T7. Wave 1 works, is cheaper, and is still unverified live — proving it is worth more right now than starting a second host. Revisit wave 2 only if the live run shows players actually launching the game outside the Launcher, since that is the sole problem wave 2 solves.
