# Notification SSOT — the ideal

**Status:** idea phase, 2026-09-13. Not a spec. No build authorized.
**Program id:** `notification-ssot`

## Which loop this extends

This is **cross-cutting infrastructure, not one loop** — said honestly rather than forced onto a
single row. It exists to carry events *from* whichever loop produced them *to* the player, wherever
they are standing. Real, cited demand exists across several rows of
[the-loops.md](../guide/the-loops.md):

| Loop | Real demand found this session |
|---|---|
| **Place 3 — Farming, hunting, defending the empire** | `loam.shortfall:` / `loam.release` — the case that started this investigation. Categories and channel defaults already exist (`categories.ts:8-15,23`); nothing feeds them (see Wiring gap) |
| **Place 5 — World stage empire** | `legion.runway:`, `supply.cut:`/`supply.change`, `intel.new:`, `command.dropped` — same already-built category list, same gap |
| **Place 6 — Dungeon crawler, the Delve** + **Place 1 — Lawn** + ~~**Place 3 — Siege**~~ **Place 3 — Farming, hunting, and defending the empire** (`the-loops.md` names the loop this way; Siege is one verb inside it, under "Defend" — **corrected, strengthen pass 2026-09-13, finding Q2**) | `deployment-hierarchy`'s injury-tier worsening (`docs/architecture/deployment-hierarchy/spec-injury-tiers.md:224-257`): an untreated wound "advances one step" at a tunable `wound.worsenAtSettles.{tier}` threshold, and a worsened-to-death case Retires the specimen. Real, specced, **unbuilt** — a durable per-specimen counter with no player-facing surface named anywhere yet |
| **Place 2 — Idle expeditions** + **Place 7 — Quests and events** | `deployment-hierarchy`'s corpse-cache decay (`spec-cache-decay-void.md:38,248-251`): a dead unique's gear decays on a ≈112-world-turn clock toward permanent loss, tunable and tickable, with **no notification mechanism named in that spec at all** — it is exactly the shape of `loam.shortfall` (ground versus gear, same "you will lose this if you do nothing" story) one program over |

Beyond `deployment-hierarchy` and `world-notify`'s own category list, no other program in the repo
currently specs "the player should be told about this" language — said plainly rather than padded
with invented consumers. Two real, currently-unbuilt candidates is the honest count.

## What this is

A single place the backend can push a player-facing fact — *your gear at the old campsite decays in
12 turns*, *Rex's wound worsened to Grave* — and a single place the frontend renders it, no matter
which stage the player is standing on. Today two systems each solve half of this and neither solves
the whole: one is transport-agnostic but only fires on the player's own FE mutations; the other has
the right event vocabulary and UI shape but is GET-polled and wired to exactly one stage's turn
clock. This is the plan to have one system that is both: backend-authored, backend-pushed over the
connection that already exists, and mountable from any stage.

## What already exists

### Built

- **`shell/toastStack.ts`** — a real, generic zustand toast store. `ToastEntry` already carries an
  optional `action: { label, run }` and `category?: string` (`toastStack.ts:5-17`), auto-expires at
  `DEFAULT_DURATION_MS = 5000` (`toastStack.ts:29`), and is rendered app-wide by `Toasts.tsx` behind a
  `pointer-events-none` container so it never blocks input. Fed today by exactly one producer: the
  global `MutationCache` listener (`lib/bus/mutationFeedback.ts:17-34`), wired once in
  `app/providers.tsx`. It is genuinely repo-wide **as a rendering surface**; it has never received a
  backend-pushed, unprompted event.
- **`stages/world/notify/`** — a complete, mostly-built world-stage notification subsystem per
  `docs/architecture/world-stage/spec-world-notify.md`:
  - `categories.ts:7-40` — closed `NotifyCategory` union (8 members), a `TOAST_TIER` allow-list, and a
    `CATEGORY_DEFAULT_CHANNEL` map. Comment on the file: "arriving on Toast by default is a spec
    change, not a code change" — this repo already treats the category vocabulary as the kind of
    closed enum a human extends, not a config row.
  - `notifyRailStore.ts:1-46` — five item states (`unread/opened/dismissed/minimized/blocking`) as a
    pure reducer over an array: `flush`, `onCommit`, `open`, `dismiss`, `minimizeCategory`. No class,
    no side effect, no persistence — every function is `(items) => items`.
  - `NotifyRail.tsx` / `RailItem.tsx` mounted inside `WorldStage.tsx:445-452`.
  - The spec's own text already anticipates exactly this generalization: *"Notifications on other
    stages. The toast stack is shared; the rail and the category model are the world stage's **until
    a second stage asks for them, at which point they move to `shell/` — a move, not a rewrite**,
    which is why the rail state is a pure store from the start"* (`spec-world-notify.md:189-191` —
    **corrected, strengthen pass 2026-09-13, finding Q5**: previously cited `:188-191` here while the
    same quote is cited `:189-191` below; verified this session the quoted text runs lines 189-191,
    both citations now match).
- **`RpgHub : Hub`** (`src/FusionRpg.Server/RpgHub.cs:9`) — real, production SignalR infrastructure.
  Two groups, `InjectorGroup`/`WebGroup` (`RpgHub.cs:31`). A repo-wide grep of `src/FusionRpg.Server`
  for `SendAsync(` returns **~50 call sites** across `CreatureEndpoints`, `DelveEndpoints`,
  `PassiveTreeEndpoints`, `ContractEndpoints`, `AptitudeEndpoints`, `CommanderEndpoints`,
  `WorldEndpoints`, `Program.cs`, and more — every one of them a bare *"X changed, go refetch"* signal
  (`CreaturesUpdated`, `SoulsUpdated`, `DelveUpdated`, `WorldUpdated`, `PassiveTreeUpdated`,
  `RpgProgressionUpdated`, …), never a self-contained title/body payload the client can render without
  a matching fetch.
  - **`IDelveLivePush`** (`src/FusionRpg.Server/DelveLivePush.cs:54-80`) is the closest existing
    precedent to a generic push abstraction: a plain `Push(eventName, payload)` interface (kept
    non-`IHubContext` deliberately, so tests can fake it with no live SignalR server —
    `DelveLivePush.cs:48-52`), one production implementation broadcasting to `WebGroup`, fire-and-forget
    ("a live delve push is best-effort telemetry about an already-durable state change… a dropped frame
    costs a client a live update, not correctness" — `DelveLivePush.cs:66-70`), and the *"broadcast, let
    the client filter by id"* discipline (every payload carries its own key).
  - `RpgHub.Hello` already does a **push-then-rehydrate** pair on reconnect
    (`PushGrantSnapshotAsync`/`PushPatronAsync`, `RpgHub.cs:47-48`) — the closest existing pattern for
    "catch a client up on what it missed while disconnected."
- **`TurnReportEntry`** (`src/FusionRpg.Core/World/Turn/TurnReport.cs:30-31`) already carries an
  `Audience` field for per-faction/per-player scoping ("a faction-scoped line… still needs to reach
  only the faction it is about, not 'nowhere in particular' read as 'everyone'" — doc comment,
  `TurnReport.cs:24-28`), and ~~`LoamPhases.cs:160,166` populates real `"loam.shortfall:"` entries with
  it today~~. **Correction (strengthen pass 2026-09-13, finding Q3) — citation didn't show what it
  claimed.** Verified this session against `TurnReport.Add`'s actual parameter order
  (`TurnReport.cs:92-93`: `Add(phase, kind, subject, detail, sectorId = null, audience = null)`):
  `LoamPhases.cs:166` reads `report.Add(phase, TurnReportKinds.Event, faction.FactionId,
  "loam.shortfall:" + shortfall, weakest)` — its 5th positional argument (`weakest`) binds to
  `SectorId`, not `Audience`, so that line carries no real `Audience` value at all. The line that
  actually does is `LoamPhases.cs:160`, which uses the named argument `audience: faction.FactionId`
  on a `"loam.shortfall.unresolved:"` entry (a different string, the shortfall-with-no-fix-available
  case). The correct citation for "a real `TurnReportEntry` carrying a populated `Audience` field" is
  `LoamPhases.cs:160`, not `:160,166`. This is real per-player scoping precedent — but it is
  entry-per-world-turn, read via GET (`WorldEndpoints.cs:210-224`), never pushed.

### Wiring gap

- **`WorldStage.tsx:135`** initializes `notifyItems` to `[]`; nothing ever feeds the polled
  `TurnReport` into it. The rail (`:446`), the categories, and the translation layer
  (`playbackTable.ts:62-64`) all exist and are disconnected — confirmed fresh this session, not
  carried over from an earlier claim.
- The spec's own §"What stays out" already names the seam for lifting the rail out of the world stage
  ("a move, not a rewrite") — that move has simply never been asked for, because nothing outside the
  world stage has needed it yet.

### Real gap

- **No notification-shaped table anywhere in `FusionRpg.Data`.** A repo-wide grep for `notification`
  (case-insensitive) across `src/FusionRpg.Data` returns exactly one hit, and it is unrelated:
  `RpgStore.Souls.cs:142`, a comment reading *"Earn notifications ride `PvzActivityUpdated`"* — itself
  another example of the invalidate-and-refetch convention, not a notification record.
- **No durable per-player notification log anywhere.** `notifyRailStore.ts` is pure client memory —
  it does not survive a reload, and nothing server-side remembers what a disconnected player missed.
  `TurnReport`'s `Audience` field is the nearest thing to a durable, per-player-scoped event record,
  and it is scoped to the world-turn clock only — an idle-expedition completion, a delve settle, or a
  lawn-match event has no equivalent record anywhere.
- **No server endpoint or hub method that pushes a title/body/category/severity-shaped payload.**
  Every existing push is the invalidate-and-refetch shape described above.
- **No cross-stage mount point.** `toastStack` is global; the rail and category model are, by the
  spec's own admission, world-stage-only until asked to move.
- **Added (strengthen pass 2026-09-13, finding C1) — this migration drops `world-notify`'s existing
  GG-50 volume-boundedness guarantee.** `spec-world-notify.md`'s own §GG-50 states the rail is safe
  under that gate *specifically because it is structurally bounded* — it flushes on End Turn except
  blockers, and *"a feed that empties every turn has no unbounded path"* (verified this session;
  `ui/volumeMatrix.test.ts`'s exhaustive `COLLECTION_SURFACES` registry, cited at
  `spec-world-notify.md:194-199`, is the actual gate — a new collection surface that skips
  registration turns that shipped test red). §The shape above proposes exactly the opposite default
  for every *new* mount (lawn/delve/expedition): *"dismiss-only (no auto-flush) until each domain
  names its own session boundary."* That default removes the structural bound on precisely the mount
  types most exposed to it — an idle-expedition-scale decay clock, a multi-room delve — so any new
  mount needs **its own** structural flush/bound named before it ships, or it inherits an
  unbounded-accumulation risk GG-50 exists to prevent. Not designed here; flagged so `/spec` does not
  silently inherit "dismiss-only" as if it were volume-neutral.
- **Added (strengthen pass 2026-09-13, finding C3) — neither feeder spec's acceptance gate requires
  telling the player anything.** `deployment-hierarchy-map.md`'s own module gates — **G2** ("a wound
  is real": *"a specimen graded into a wound tier fights measurably weaker... an untreated serious
  wound advances and can kill on its own settlement clock..."*, `deployment-hierarchy-map.md:144`) and
  **G4** ("decay is deterministic": replay/idempotency/void criteria, `deployment-hierarchy-map.md:146`)
  — verified this session to contain no player-facing acceptance criterion at all; neither
  `spec-injury-tiers.md` nor `spec-cache-decay-void.md` requires the player be told anything either (a
  repo-wide grep for "notif" across both returns zero hits). Both modules could ship "done" by their
  own gates with the exact gap that motivated this whole doc still open. This is named as a
  cross-program finding, not fixed here: whoever builds `injury-tiers`/`cache-decay-void` should be
  told — via this doc, or a note added to `deployment-hierarchy-map.md`'s own gates — that "player is
  notified" is not yet a gate criterion there and probably should be once notification-ssot exists.
- `decisions.md` has no lock on this topic (grepped, no matches) — nothing here overturns a settled
  decision.

## Prior art

**Dual-surface toast + notification-center pattern.** Industry UX guidance converges on exactly the
split `world-notify` already designed independently: *"A toast is transient and contextual: it
confirms that something just happened… and vanishes on its own — it should never carry information
the user needs to act on later"*, while a persistent surface *"allows the user to view and respond to
notifications after they have been displayed"* — and *"toasts can be dismissible only if there is
another surface… where the customer can find this content again later"* (SaaS UI Design,
[saasui.design](https://www.saasui.design/blog/saas-notification-toast-ux-patterns)). This confirms
the toast/rail split is an industry-standard shape, not a bespoke 4X borrowing — `world-notify`'s own
prior-art section (Endless Space 2, Civ VII, Humankind, Endless Legend) is real and already cited
there; it is not re-derived here. One caveat worth carrying forward: that research is turn-clock
research. None of those games have a live-match clock (the lawn) or a wall-clock idle-dispatch clock
(expeditions) — the flush rule ("empties on End Turn except blockers") has no analog on either of
those two clocks, and generalizing the shape must not silently assume one exists.

**Single hub, many named events (SignalR).** Community discussion on hub topology consistently favors
fewer hubs over more: *"Multiple `connectionId`s means multiple WebSocket connections… something
developers want to avoid,"* and *"many developers prefer a single hub design to reduce the number of
WebSocket connections"* even though SignalR's routing model makes one hub per route easy to reach for
([dotnet/aspnetcore #11372](https://github.com/dotnet/aspnetcore/issues/11372) and related threads).
This validates what `RpgHub` already does — one hub, ~50 distinctly-named `SendAsync` events — and
argues against adding a second hub for notifications. `IHubContext<RpgHub>` is already the pattern
every feature uses to push from outside the hub class itself (`DelveLivePush.cs:74-79`,
`AptitudeEndpoints.cs:117`, `CommanderEndpoints.cs:106`, …); a notification push should be one more
named event on the same hub, not a new one.

Sources: [SaaS Notification UX: Real Examples & Patterns](https://www.saasui.design/blog/saas-notification-toast-ux-patterns) ·
[Fluent 2 Toast usage](https://fluent2.microsoft.design/components/web/react/core/toast/usage) ·
[dotnet/aspnetcore #11372 — multiple hubs on 1 server](https://github.com/dotnet/aspnetcore/issues/11372) ·
[Use hubs in ASP.NET Core SignalR (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/signalr/hubs).

**Added (strengthen pass 2026-09-13, finding Q4) — this section is confirmation-quotes, not
sourced numbers, and that is named rather than left to look like an oversight.** The idea-phase
skill's own bar for Prior Art (Step 2) asks for "concrete numbers, formulas... not vibes." This
section brings back pattern confirmation (toast-vs-persistent-surface split, single-hub-vs-multi-hub
topology) with no tunable numbers or formulas, because this program is infrastructure/UX-pattern
work, not a numbers-tunable feature — there is no "cost curve" or "drop rate" for a notification
transport to source. Stated explicitly rather than presenting the quotes as satisfying a numbers bar
they do not.

**Added (strengthen pass 2026-09-13, finding Q6) — two of the four Sources links above are
background reading, not load-bearing citations.** [Fluent 2 Toast usage](https://fluent2.microsoft.design/components/web/react/core/toast/usage)
and [Use hubs in ASP.NET Core SignalR (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/signalr/hubs)
are listed but never quoted or directly referenced in the prose above (only the SaaS-UI-design and
`dotnet/aspnetcore` sources are quoted). Named plainly rather than implied as equally load-bearing:
the Fluent 2 page is a component-library reference for whichever FE consumes a toast/notification
surface, and the Microsoft Learn page is general SignalR-hub background consistent with, but not the
source of, the "single hub, many named events" argument above (that argument's actual citation is the
`dotnet/aspnetcore` issue thread).

## The shape

**Recommendation: generalize `world-notify`, do not build fresh.** Evidence, not a hedge:

1. The spec text itself names this exact move as the intended endpoint ("a move, not a rewrite" —
   `spec-world-notify.md:189-191`).
2. `notifyRailStore.ts` is already pure functions over an array — trivially portable to a hook any
   stage can mount; it was written this way specifically so the move would be cheap.
3. `categories.ts`'s model (closed category list + default channel + a "top tier" promotion rule) has
   nothing world-turn-specific in its *shape* — only its *contents* (8 world-stage categories) and its
   *flush trigger* are stage-specific.
4. `toastStack.ts` already proves the FE half of "shared across the whole app" today, serving
   mutation feedback from every page without per-stage wiring.

**But it is not a pure lift — three real reworks, named so `/spec` does not rediscover them:**

1. **The flush trigger must stop being `WorldTurnCommitDto.Advanced`.** That signal only exists on the
   world-turn clock. A generalized rail needs a per-mount flush policy — the world stage keeps
   flush-on-End-Turn; a lawn/delve/expedition mount has no equivalent boundary and should default to
   dismiss-only (no auto-flush) until each domain names its own session boundary. This is a design
   decision `/spec` must make explicitly, not an incidental rename.
2. **The category vocabulary needs a single closed registry, not eight world-stage strings.** Follow
   the same discipline this repo already uses for `AtomKindRegistry`/`NotifyCategory` itself: one
   `NotificationCategory` enum every domain registers into, extended only by a reviewed change — never
   a per-domain namespace that lets categories drift apart.
3. **The data source flips from GET-poll-and-translate to backend-push-and-store.** Today the client
   polls `TurnReport`, translates a keyframe, and derives rail items locally. The SSOT reverses this:
   the server decides category, severity and text, ~~pushes it over `RpgHub`, and a durable per-player
   table backs a GET-on-reconnect catch-up path~~. **Corrected (strengthen pass 2026-09-13, finding
   M3) — this read as two parallel actions; it is a sequence.** The server durably writes the
   notification row first, then pushes it over `RpgHub` — never the reverse, never both at once —
   matching `IDelveLivePush`'s own explicit precedent: *"a live push is best-effort telemetry about an
   already-durable state change... a dropped frame costs a client a live update, not correctness"*
   (`DelveLivePush.cs:66-70`). The durable per-player table is what a GET-on-reconnect catch-up path
   reads; the push is a notice about a write that has already happened, not the write itself. This is
   genuinely new backend work — nothing to lift.
   - **Added (strengthen pass 2026-09-13, finding H1).** Per this repo's binding "SQL only inside
     `FusionRpg.Data`" rule, that durable table's schema and every read/write to it live behind
     `RpgStore*`, guarded by `guard-dal.ps1` — never a Server-side ad-hoc store. Stated here because
     this doc otherwise discusses the table's shape without ever naming which layer owns it.

**Does `world-notify` get migrated or coexist?** Migrate, sequenced as its own spec's boundaries
already promise: the world-stage `ChannelControl`, End-Turn flush and `loam.*`/`legion.*` categories
become the **first real consumer** of the generalized store once `notifyRail`/`categories` relocate to
`shell/`, exactly as `spec-world-notify.md`'s own "What stays out" section already commits to. This is
a relocation of the store plus a swap of its data source — not a rewrite of the world-stage UI, and
not two systems running side by side once the move lands. **Correction (strengthen pass 2026-09-13,
finding C2) — the spec's own wording is not a pre-granted permission; see the Ask below.** The "a move,
not a rewrite" quote describes what the move looks like mechanically if the world-stage program agrees
to it — it is not, by itself, that program's sign-off to relocate/generalize its own module.

## Ask filed on owning program

**Added (strengthen pass 2026-09-13, finding C2).** `world-notify` is a specced, **owned** module of
`world-stage-map.md` (row 78: *"Two notification classes; the passive right rail; flush on End Turn
except blockers; per-category channel settings"*, wave 5 per that map's own build sequence,
`world-stage-map.md:96`). This doc's §The shape recommends relocating and generalizing its store
(`notifyRailStore.ts`, `categories.ts`) into `shell/` — before this correction, with zero formal ask
anywhere in this doc (verified this session: zero occurrences of "ask"/"sign-off"/"owner review"
prior to this section). Citing `spec-world-notify.md:189-191`'s own "a move, not a rewrite" line is
evidence the move is *cheap*, not evidence it is *authorized*. Filed here in the format
`deployment-hierarchy-map.md`'s own "External dependencies" table already uses for the same kind of
cross-program ask:

| Dependency | Owner map / module | What this program needs | Gate |
|---|---|---|---|
| `notifyRailStore.ts` / `categories.ts` relocation + generalization | `world-stage-map.md`, module `world-notify` (wave 5) | The world-stage program's sign-off to move its rail store and category registry into `shell/`, and to accept the generalized `NotificationCategory` registry as the new home for its own `loam.*`/`legion.*` categories | before `/spec` scopes the relocation |

This program needs that sign-off before `world-notify`'s store/categories can be relocated or
generalized — not merely cite the sibling spec's own anticipatory wording as if it were pre-granted.

## Transport

**Recommendation: backend push over the existing `RpgHub` connection, not GET-poll.** Reasoning:

- `RpgHub` is already proven at this exact shape and scale (~50 named push events, production, years
  of use) — reusing it costs one new event name, not new infrastructure.
- `IDelveLivePush` is a directly reusable *pattern*: a thin interface (testable without a live SignalR
  server), one Hub-backed implementation, fire-and-forget, broadcast-and-filter-by-id. A
  `INotificationPush` should copy this shape, not invent a new one.
- Community precedent argues against a second hub (Prior art, above) — add a `Notification`/
  `NotificationBatch` `SendAsync` name to `RpgHub`'s existing `WebGroup` broadcast, matching every
  other feature's convention.
- Keep a GET `/api/notifications?since=` endpoint for the cold-load/reconnect catch-up case — push
  serves an already-connected client; GET serves "I just opened the app" or "I was offline when this
  fired," mirroring `RpgHub.Hello`'s existing push-then-rehydrate pair.

**Added (strengthen pass 2026-09-13, finding M1) — real gap: no per-player scoping exists anywhere in
this transport plan.** `RpgHub` has exactly two SignalR groups, `InjectorGroup`/`WebGroup`
(`RpgHub.cs:31-32`) — no per-player group exists anywhere in the codebase. Every one of the ~50
existing pushes sends only an id/telemetry signal for the client to refetch its **own** authenticated
data, never player-private content — `IDelveLivePush`'s own "broadcast, let the client filter by id"
discipline is exactly why it never had to solve this: it only ever pushed ids/telemetry, not content.
`TurnReport.Audience` is the one existing per-player-scoped payload in the codebase, and it is
filtered server-side at GET time (`WorldEndpoints.cs:348`: `if (e.Audience is { } audience) return
string.Equals(audience, viewer, ...)`), never broadcast. This doc's transport plan pushes a
self-contained title/body payload over the shared `WebGroup`, which has no per-player filter at all —
named as an open question, not designed here: **does this server ever serve more than one player's
browser session at once?** If genuinely single-player-single-session, a broadcast may be moot in
practice, but this doc should say so explicitly rather than silently assume it. If there is any chance
of multiple concurrent sessions, a per-player SignalR group or a client-side player-id filter must be
added before this feature ever pushes content (not just ids) over `WebGroup`.

**Added (strengthen pass 2026-09-13, finding M4) — open question: push/reconnect dedup.** Because the
planned push carries a full title/body payload rather than an id+"go refetch" signal, a client that
reconnects mid-flight could receive the same notification twice — once live over `RpgHub`, once again
via the reconnect catch-up GET — with no id-tracking or idempotency rule named anywhere in this doc.
Named as a design requirement, not designed here: the payload needs a stable id the client can use to
dedupe against what it has already rendered, the same way any other idempotent-delivery surface in
this repo needs one.

## Tunables

Two real ones for the server-owned half of this feature, in `data/tuning/notification.v1.json`
(following the `<domain>.v{n}.json` convention — this is new balance/UX surface, not a structural
constant):

- **Retention window** for the durable per-player notification log — how long a delivered-and-read
  notification stays queryable before pruning. A balance/ops pass would plausibly want to change this.
  **Added (strengthen pass 2026-09-13, finding H2) — retention-vs-unread-loss risk.** Pruning by age
  alone could silently drop an *unread*, important notification (not just an already-dismissed one)
  while the player was away — distinct from Open Question 2's "does the log retain dismissed items"
  question, which is about intentional history, not accidental loss of something never seen. Named
  as a related but separate concern worth deciding alongside retention, not fully designed here.
- **Per-category rate limit / dedupe window** — a per-turn ticking source (cache decay ticks every
  world turn toward its ≈112-turn horizon) must not push a toast on every tick; a "don't repeat this
  category for this subject within N turns" throttle is exactly the kind of number a balance pass
  tunes by feel. **Corrected (strengthen pass 2026-09-13, finding M6) — unit fixed, was "turns/minutes"
  as if interchangeable.** `cache-decay-void` (one of the two feeder events) is built under an
  explicit "no wall clock, ever, for game state" rule — verified this session,
  `spec-cache-decay-void.md:43-48` cites `spec-delve-attrition.md:387-389`'s guard-tested ban on
  `DateTime`/`DateTimeOffset`/`Environment.TickCount` in decay/settlement code. So for any category
  driven by a world turn (both named feeder events today), this window is **turn-counted only** — a
  wall-clock unit would be meaningless against a clock the feeding module is forbidden from reading. A
  wall-clock-timed category, if one is ever added (e.g. an expedition-timer-driven notification), would
  need its **own separate** wall-clock tunable — the two clock domains must never be conflated in one
  number, the same discipline `cache-decay-void` already enforces for its own decay math.
- **Added (strengthen pass 2026-09-13, finding M5) — open question: same-commit multi-subject
  bursts.** The rate-limit tunable above only throttles repeats of the *same* category+subject; it
  does not address one `CommitWorldTurn` legitimately producing several *distinct-subject*
  notifications at once — e.g. `cache-decay-void`'s tick can destroy items across many different
  caches in one turn, each a distinct subject, none of them a repeat of another. Open, not decided
  here: does a same-turn burst of distinct-subject notifications get batched (this doc already names a
  `NotificationBatch` event type in §Transport but never decides when it fires versus sending
  individually), or sent one at a time — and how does that interact with `world-notify`'s existing
  three-toast-at-once cap once this becomes cross-stage?
- **Added (strengthen pass 2026-09-13, finding C4) — open question: cross-category severity/priority.**
  "Severity" is currently named once in passing (§Transport, "the server decides category, severity and
  text") with no taxonomy or cross-category ordering rule for when multiple important categories
  compete for the existing three-toast-at-once cap. Open: does severity get a real ordering (e.g. a
  `Critical` notification always preempts a slot even at the cap), or is this deliberately deferred to
  whoever builds it — same as this doc's other deferred design questions?

**Explicitly not tunable / not this feature's tunable:** the closed `NotificationCategory` vocabulary
(structural, human-extended like every other closed enum in this repo); the two-class toast/rail model
(structural); which SignalR group receives a push (structural). The FE toast's 5-second auto-expiry
(`toastStack.ts:29`) is a UI-polish constant, not a balance number in the `tunables-ssot.md` sense (no
Policy/Catalog/Rules/Ruleset/Math file owns it) — flagged here only so it is not mistaken for a gap
this feature must close. **Added (strengthen pass 2026-09-13, finding H3).** Correctly out of scope for
this doc's own new numbers as stated — but flagged now, not left as a surprise later: a future
balance/UX pass may still want per-category or per-severity auto-expiry (a `Critical` alert staying up
longer than a routine one), which would turn that constant into a real tunable at that point. Naming it
here only so it is not treated as a novel discovery when that pass arrives.

## What this deliberately does not decide

- The exact SQL schema of the durable notification table — a `/spec` question. **Added (strengthen
  pass 2026-09-13, finding H4).** One piece of type guidance stated now rather than left for `/spec`
  to re-derive: a notification id / any ordering-sequence column should default to `long` per this
  repo's numeric-overflow rule (`CLAUDE.md` "Numeric overflow," rule 1) — not `int` — even though
  notification counts are unlikely to reach combat-scale magnitudes.
- Whether the toast's 3-at-once cap and the rail's five item states are reused verbatim on every
  stage, or restyled per stage — a design question once a second real consumer is chosen.
- The precise `NotificationCategory` membership beyond what already exists (`world-notify`'s 8) plus
  the two named `deployment-hierarchy` candidates — that enumeration is `/spec` work, not idea-phase
  work.

## Open questions

Genuinely unresolved — no existing pattern already answers these, and each is answerable in one
sitting:

1. **Where does event→text translation live once this generalizes?** `world-playback` owns it for the
   world stage today ("this module never parses an engine token" — `categories.ts:5-6`). Does each
   consuming domain grow its own translator (a `delve-playback`, an `expedition-playback`, mirroring
   the existing pattern), or does the new server-side notification service centralize text generation
   for every domain in one place? Both are viable; the two-domain-only demand found this session is
   too thin to infer an answer from precedent.
2. **Does the durable log retain dismissed items** (a real notification center a player can reopen),
   or does dismissal mean gone — the way `world-notify`'s rail already works, where the *domain's own*
   record (the turn report) is the only history? This decides whether `rpg_notifications` needs a
   soft-delete column or a hard delete on dismiss.
3. **Which of the two named `deployment-hierarchy` candidates (injury-tier worsening, corpse-cache
   decay) is the first non-world-stage consumer to actually build against this?** Both are real and
   specced but unbuilt; picking one sequences the `/spec` scope and gives the generalized store its
   first proof beyond world stage. **Corrected (strengthen pass 2026-09-13, finding M2) — this
   understates a real prerequisite.** Neither `spec-injury-tiers.md` nor `spec-cache-decay-void.md` has
   **any** event/callback hook at the moment their respective notification-worthy events happen today —
   both are bare SQL reads/writes inside a settlement transaction (confirmed this session: a
   repo-wide grep for "notif", case-insensitive, across both spec files returns zero hits). Wiring
   either consumer requires a **new** hook to be added to those specs first — this is a real
   prerequisite dependency, not just a scope-sequencing choice. Named explicitly: **both**
   deployment-hierarchy specs need an amendment (a new callback/event emission point) before
   notification-ssot can consume them, and that amendment is an ask on the `deployment-hierarchy`
   program — not something notification-ssot can build around unilaterally, the same shape as the
   Ask filed above for `world-notify`.
4. **Added (strengthen pass 2026-09-13, finding M1) — does this server ever serve more than one
   player's browser session at once?** See §Transport's new gap note: `RpgHub` has no per-player
   group, and every existing push is id/telemetry-only for exactly this reason. Answerable in one
   sitting by the owner; the answer decides whether a per-player group/filter is a blocking
   prerequisite before this feature ever pushes payload content (not just ids) over `WebGroup`.
5. **Added (strengthen pass 2026-09-13, finding M4) — push/reconnect dedup id.** Does the payload
   carry a stable id from day one, or is dedup deferred until it causes a visible double-notification?
   See §Transport.
6. **Added (strengthen pass 2026-09-13, finding M5) — same-turn multi-subject batching.** Does a
   same-turn burst of distinct-subject notifications get batched via the already-named
   `NotificationBatch` event, or sent individually? See §Tunables.
7. **Added (strengthen pass 2026-09-13, finding C4) — cross-category severity ordering.** Does
   `Critical` (or any high-severity category) preempt a slot at the existing three-toast cap, or is
   this deferred to whoever builds it? See §Tunables.

## Handoff

**Added (strengthen pass 2026-09-13, finding Q1).** Written:
`docs/architecture/notification-ssot-ideal.md` (this file). Next step: `/spec` for a capability map +
module spec, once the open questions above are answered by the owner — in particular the two real
prerequisite asks this pass named (§Ask filed on owning program for `world-notify`'s relocation;
Open question 3's correction for the `deployment-hierarchy` hook amendments) should clear or at least
be acknowledged by their owning programs first. Do not write specs, plans, or code from this doc alone.

## Corrections log — strengthen pass 2026-09-13

Four independent lenses (mechanism soundness · cross-program/economy · hard-rule compliance ·
spec-quality-vs-bar) ran this session. See the four lens reports this session for the full
checked-and-held list; findings not listed below were checked and held.

| # | Lens | Finding | Disposition |
|---|---|---|---|
| M1 | mechanism soundness | No per-player SignalR scoping exists (`RpgHub` has only `InjectorGroup`/`WebGroup`); the planned push carries full payload content over the shared, unfiltered `WebGroup` | Added as a real gap (§Real gap) + Open question 4: does this server ever serve more than one player's session at once |
| M2 | cross-program/economy | Neither feeder spec (`spec-injury-tiers.md`, `spec-cache-decay-void.md`) has any event/callback hook at its notification-worthy moment | Corrected Open question 3 to name this as a real prerequisite ask on the `deployment-hierarchy` program, not just a scope-sequencing choice |
| M3 | spec-quality-vs-bar | Transport wording read as two parallel actions ("pushes... AND a durable table backs...") instead of a sequence | Corrected to state durable-write-then-push explicitly, citing `IDelveLivePush`'s own precedent |
| M4 | mechanism soundness | Full-payload push + reconnect catch-up GET can double-deliver with no dedup/idempotency rule named | Added as Open question 5 |
| M5 | mechanism soundness | The named rate-limit tunable only throttles same-subject repeats, not a same-turn burst of distinct-subject notifications | Added as a new Tunables bullet + Open question 6 |
| M6 | hard-rule compliance | Dedupe-window tunable worded "N turns/minutes" as if interchangeable, conflicting with `cache-decay-void`'s no-wall-clock rule | Fixed to turn-counted-only for turn-driven categories; noted a future wall-clock category would need its own separate tunable |
| C1 | cross-program/economy | This doc's "dismiss-only, no auto-flush" default for new mounts drops `world-notify`'s existing GG-50 volume-boundedness guarantee | Added as a named real gap in §Real gap, citing GG-50 and `volumeMatrix.test.ts` |
| C2 | cross-program/economy | `world-notify` is an owned module of `world-stage-map.md` (wave 5); this doc recommended relocating/generalizing it with zero formal ask | Added a new "Ask filed on owning program" section mirroring `deployment-hierarchy-map.md`'s External-dependencies format |
| C3 | cross-program/economy | Neither feeder spec's acceptance gate (nor `deployment-hierarchy-map.md`'s own G2/G4) requires telling the player anything | Added as a named cross-program finding in §Real gap |
| C4 | mechanism soundness | No cross-category severity/priority ordering rule for the existing three-toast cap | Added as a new Tunables bullet + Open question 7 |
| H1 | hard-rule compliance | The durable notification table's module boundary (SQL only inside `FusionRpg.Data`) was never stated | Added a sentence in §Transport naming `RpgStore*`/`guard-dal.ps1` |
| H2 | hard-rule compliance | Retention-by-age could silently drop an unread, important notification, distinct from the dismissed-items question already asked | Added a note beside the Retention window tunable |
| H3 | hard-rule compliance | The FE toast's 5000ms auto-expiry is correctly out of scope now, but a future per-category/per-severity auto-expiry pass was never flagged | Added a note beside the "explicitly not tunable" paragraph |
| H4 | hard-rule compliance | No type guidance given for the deferred notification-id/ordering column | Added a line in "What this deliberately does not decide": default to `long`, per `CLAUDE.md`'s numeric-overflow rule 1 |
| Q1 | spec-quality-vs-bar | No `## Handoff` section per the idea-phase skill's Step 5 | Added, naming the written path and the `/spec` next step |
| Q2 | spec-quality-vs-bar | The loop table's "Place 3 — Siege" row mislabels `the-loops.md`'s actual loop name | Corrected to "Place 3 — Farming, hunting, and defending the empire" (Siege is one verb inside it) |
| Q3 | spec-quality-vs-bar | Citation claimed `LoamPhases.cs:160,166` both populate a real `TurnReportEntry.Audience`; verified `:166`'s 5th positional argument binds to `SectorId`, not `Audience` | Corrected to cite `:160` only (named-argument `audience:` on the `.unresolved:` entry), with the parameter-order proof shown inline |
| Q4 | spec-quality-vs-bar | Prior Art section is confirmation-quotes only, with no concrete numbers/formulas, against the idea-phase skill's numbers bar | Added an explicit acknowledgment that this is infrastructure/UX-pattern work, not a tunable-numbers feature |
| Q5 | spec-quality-vs-bar | The same `spec-world-notify.md` quote cited as both `:188-191` and `:189-191` | Verified the quote runs lines 189-191; both citations now read `:189-191` |
| Q6 | spec-quality-vs-bar | Two of four Sources links (Fluent 2 Toast usage; MS Learn SignalR hubs) are never quoted/referenced in the Prior Art prose | Added a note naming both as background reading, not load-bearing citations |

No contradiction was found between any two corrections while applying them.
