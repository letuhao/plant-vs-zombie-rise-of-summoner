# Phaser kernel — the ideal

**Status:** idea phase, 2026-09-06. Not a spec. No build authorized.

**Program id:** `phaser-kernel`  
When it graduates: capability map `docs/architecture/phaser-kernel-map.md`, module specs under
`docs/architecture/phaser-kernel/`, tasks `tasks/phaser-kernel-plan.md` / `tasks/phaser-kernel-todo.md`.

**Inputs already locked (do not reopen):**

- Owner, this session: **build and test the new kernel before retiring the old paths.**
- Dual-plane (DPLP): React chrome / pure fold / Phaser island; Phaser never owns HTTP/SignalR.
- One React stage at a time; `Phaser.Game` created on enter, destroyed on leave (GG-11 / D2). Lawn
  Game and world Game **never coexist**.
- React owns HUD — no Phaser `UIScene` for Almanac chrome.
- Phaser **4.2** (`"phaser": "^4.2.1"`).
- No FE prediction (RT-15).
- Phaser under `src/game/`.
- `board-render` Decision 40: grid layer serves **siege + battle**, not the world map.

**Companions (evidence, not this program’s spec):**
[fe-phaser-architecture-audit.md](fe-phaser-architecture-audit.md),
[research/phaser-architecture-prior-art-2026-09-06.md](../research/phaser-architecture-prior-art-2026-09-06.md),
[fe-game-foundation.md](fe-game-foundation.md),
[base-defense/spec-board-render.md](base-defense/spec-board-render.md).

---

## Which loop this extends

This is **not a new loop**. It is how several **place** loops are *seen* on the web canvas:

| Loop ([the-loops.md](../guide/the-loops.md)) | Stage | What the kernel supplies |
|---|---|---|
| **1. Lawn — first core** | `#/lawn/{matchKey}` | Island runtime + **grid** board layer |
| **4. World map — adventure** and **5. World stage — empire** | `#/world` | Island runtime + **graph** camera/overlays (not `GridSpec`) |
| **3. Farm / hunt / defend** (sieges, Vision) | `#/siege/{id}` | Island + grid — **paused** until this program names APIs |
| Combat depth / expedition fights (WIP) | `#/battle/{id}` | Island + grid playback — **paused** (Decision 40 / 44) |
| **6. Dungeon crawler — the Delve** | `#/delve/{id}` | Island + VFX/SFX/input facades; spatial model is **not** Decision 40 |

Spine loops (power, summon, items) stay in Core/Data. Sanctum (`#/sanctum`) is a stage with **no
canvas today** and is out of this program.

Rise of Summoner is an RPG plus empire-building game. The lawn is the first core loop, not the whole
war. This kernel is the shared **presentation runtime** for every canvas stage. It does not invent a
parallel pitch.

---

## Load-bearing principles (restated, not linked)

A downstream session reads this file, not its neighbours.

1. **Every RPG feature lives in the RPG layer. It is never built by changing what PvZ is.** Phaser is
   a web view. Fusion’s `Plant`/`Zombie` fields and `EntityStatWriter` do not decide whether a siege
   board can share a camera helper with the lawn. “Does the lawn engine support X” is the wrong
   question for this program.
2. **The PvZ write surface constrains only persistent vanilla stat changes.** This program writes
   none. Overlay combat still resolves `DamagePacket` → dispatcher → Funnel → FA10 in Core. The
   canvas shows a lagged projection.
3. **Two async systems, deltas, record-then-drain.** The FE observes past snapshots and enqueues
   Intent. Delay is the designed worst case. Phaser must not predict the living set (RT-15).
4. **Gameless-first is capability, not the pitch.** Canvas stages stay playable with Fusion closed.
   The injector may enrich (live lawn mirror); it must not be the only forever path for a stage that
   has a web mode.
5. **One power ladder.** This program introduces no `f(level)`. Magnitudes stay `P(Θ)`; contests stay
   `Θ`. Phaser may display numbers; it does not own curves.
6. **The balance surface is data.** A number a *combat/economy* balance pass would change lives in
   `data/tuning/`. Cell pixels, contain-fit margins, and pool sizes are **visual-design / structural**
   (module-local constants with a comment) — `spec-board-render.md` already drew that line. Do not
   hide a yield curve in a shader, and do not dump camera lerp into `data/tuning/` unless a balance
   pass would actually touch it.
7. **No hard progression ceilings.** Not this program’s job. Do not add canvas-side caps on HP, XP, or
   roster size.
8. **A game interface is a stage with layers.** Opening a panel must not destroy the Game, camera, or
   subscriptions (GG-11). Esc and HUD stay React (GG-18). Phaser keyboard is gated when a layer owns
   input.
9. **Unity VFX is not web VFX.** Injector `VfxCatalog` / `VfxDirector` (decisions.md VFX row,
   complete 2026-09-04) is the lawn-game presentation plane. Phaser `FxPool` is a **browser** cosmetic
   pool. Share *semantic cue names* later if useful; never merge the two runtimes.

---

## What this is

The player travels between places: lawn, rift map, later siege, battle playback, delve. Each place
is one stage. Each canvas stage is one `Phaser.Game` that dies when they leave.

Today those islands were grown **in the order the stages shipped**, so lawn and world each cloned a
host, a destroy checklist, a camera, a registry, and a pick system. A generic grid layer was then
built for siege/battle (`src/game/board/`) and left almost unused. Siege canvas work is paused so a
third clone does not land.

**The feature:** one **Phaser kernel** — shared TypeScript modules that outlive a Game destroy —
that every canvas stage configures. Two kernels, not one mega-engine:

| Kernel | Who uses it | What it is |
|---|---|---|
| **Island runtime** | Every canvas stage | `createGame`, destroy checklist, generation-scoped bus, React host (buffer-until-ready, resize), `EntityRegistry` |
| **Grid board layer** | Lawn, siege, battle | Existing `board-render`: `GridSpec`, layers, terrain cache, contain-fit camera, cell pick, keyboard nav |

World keeps a **graph** camera and pin/lane/force factories. Delve shares island + VFX/SFX/input, not
`GridSpec`, unless a later delve spec proves a cell board.

**The player sentence:** the lawn and a siege feel like the same kind of board; the rift feels like a
map; hits and sounds can recur across places without each stage reinventing them.

**The engineering sentence:** definitions and bytes are global **per Game**; display-list and tweens
are scene-local; because we destroy the Game on stage leave, **share plain modules**, not a
process-lifetime Scene Plugin.

---

## What already exists

Sorted with the words this phase requires. A default-off path, a never-called function, or a test-only
importer is a **wiring gap**, not a wall.

### Built

| What | Proof (file:line, this session) |
|---|---|
| Phaser 4.2 pin | `web/fusion-rpg-web/package.json:33` `"phaser": "^4.2.1"` |
| Generic `createGame` / `buildGameConfig` (injected scenes, `Scale.RESIZE`, generation in registry) | `createGame.ts:26–51` |
| Lawn facade over that factory | `createLawnGame.ts:22–24` `scenes: [BootScene, LawnWorldScene]` |
| World facade over that factory | `createWorldGame.ts:19–27` `scenes: [WorldMapScene]` + theme |
| Dual EventBus maps + shared generation allocator | `EventBus.ts:5–7`, `allocGameGeneration` `EventBus.ts:176–178` |
| Lawn destroy checklist (tweens → scene `shutdown` → `lawn:destroyed` → `game.destroy(true)`) | `createLawnGame.ts:26–47` |
| World destroy checklist (same shape) | `createWorldGame.ts:30–46` |
| Lawn host: alloc generation, create on mount, ResizeObserver, buffer until `lawn:ready` | `LawnGameHost.tsx:24–63` |
| World host: same lifetime pattern | `WorldGameHost.tsx:53–56` (comment) + same `allocGameGeneration` / `createWorldGame` |
| DPLP system allow-list on lawn | `LawnWorldScene.ts:75–77` Sync → Layout → StatusFx → Pick; `init` sets generation `LawnWorldScene.ts:103–105` |
| World four-system allow-list | `WorldMapScene.ts:19–22` Sync → Camera/LOD → Pick → Overlay |
| Generic `EntityRegistry` | `EntityRegistry.ts:21–61` |
| Lawn wrapper composing it | `PtrEntityRegistry.ts:24–28` |
| Contain-fit camera, model→Phaser write-only | `bindCamera` live at `LawnWorldScene.ts:167–178` |
| Cell pick via `GridSpec`/`pickCell` adapter | `gridMath.ts` (audit + `spec-board-render` extraction 5) |
| Grid layer primitives + unit tests | `board/BoardLayers.ts:13–34`, `terrainCache.ts`, `keyboardNav.ts`, `visualMapping.ts`, `noClientPrediction.test.ts` |
| Lawn cosmetic FX pool (scene-local, `killTweensOf` on release) | `FxPool.ts:4–21` |
| World import guard (no React / `lib/bus` / `*Dto`) | `game/world/importGuard.test.ts` |
| Siege **shell** only (route, `?layer=`, placeholder — **zero Phaser**) | `SiegeStage.tsx:12–18`, `:20–40` |
| Lawn projector observe≠control + RT-15 reject | `fe-game-foundation.md` §3 RT-15, §6 plane locks |
| World lifetime: create on enter, destroy on leave, never on inspector; lawn/world Games never coexist | `spec-world-map-runtime.md` §Lifetime |
| Entry-chunk budget ≤180 KB gz; Phaser must stay out of the entry chunk | `decisions.md` Game GUI row; `spec-board-render.md` §6 |
| CapPolicy-scale lawn sprite budget (guidance) | `fe-game-foundation.md` §14 ≤50 plants / ≤80 zombies |
| Pause of siege 21.3+ / battle 22.x pending named kernel | `tasks/base-defense-todo.md` ~1045–1058 |

`game.destroy(true)` passes only `removeCanvas`. Phaser’s second argument `noReturn` **defaults
false**, which is the correct value when another Game will be created on the same page
([Game.destroy](https://docs.phaser.io/api-documentation/class/game)). That is **built**, not a gap.

### Wiring gap

Machinery exists and is inert, cloned, or only test-consumed.

| What | The inert / cloned line |
|---|---|
| `BoardLayers` / `terrainCache` / `keyboardNav` / `visualFor` | Grep of `web/fusion-rpg-web/src` production `.ts/.tsx`: **zero** importers outside `board/*.test.ts`. Lawn still paints in `LawnWorldScene.ensureGrid` (`LawnWorldScene.ts:203–219`) |
| `board-render` “closed” while Decision 40’s two live board consumers are missing | Module 20 closed in `base-defense-todo.md`; siege paused before canvas; lawn never adopted layers |
| `WorldRegistry` ignores `EntityRegistry` | `WorldRegistry.ts:5–8` three hand-rolled `Map`s; no import of `EntityRegistry` |
| Destroy checklist cloned | `createLawnGame.ts:26–47` vs `createWorldGame.ts:30–46` — same order, different scene key / bus emit |
| React hosts cloned | `LawnGameHost.tsx` vs `WorldGameHost.tsx` — buffer-until-ready + ResizeObserver duplicated |
| Host folder split | `features/lawn/LawnGameHost.tsx` vs `stages/world/host/WorldGameHost.tsx` — island-runtime debt, not a plane-lock break |
| BootScene lawn-hardcoded | `BootScene.ts:17–19` `this.scene.start("LawnWorldScene", …)` |
| World generation set in `create` not `init` | `WorldMapScene.ts:39–40` — restart-hygiene risk if a scene is reused without Game destroy |
| Lawn Scene constructor-held registry | `LawnWorldScene.ts:82` `private ptrRegistry = new PtrEntityRegistry()` — Phaser restart-state bug **if** a scene restarts without destroy; today Game destroy masks it |
| Lawn Phaser imports `@/lib/bus` | `LawnWorldScene.ts:19` `subscribeIconEpoch` — world forbids this; lawn has **no** `importGuard` analogue |
| `FxPool` lawn-only | `FxPool.ts` — correct scene scope; no module API for siege/delve to construct |
| `keyboardNav` built, lawn pointer-only | `keyboardNav.ts:75` `wireKeyboardNav`; never called from `LawnWorldScene` |
| Reduced-motion / no-prediction on board | Vacuous tripwire `noClientPrediction.test.ts` — **wiring** of the *constraint*, not a missing engine |
| `decisions.md` Lawn projector row still “Implementation deferred” | Stale vs `fe-game-foundation.md` W6–W7 shipped / `software-architecture.md` §4 Lawn Projector **Shipped** |
| `spec-board-render.md:15–17` “exactly one Phaser integration … lawn-shaped” | Stale after world island |

None of these mean “Phaser cannot share code across stages.” They mean the share layer was extracted
and then not adopted, while two live islands kept cloning.

### Real gap

No mechanism exists yet (or exists only as a research sketch).

| What | What would have to be built |
|---|---|
| Shared `destroyGame({ sceneKey, shutdown, emitDestroyed })` | One implementation; lawn/world wrappers become one-liners |
| Shared `PhaserGameHost` (or hook) | Generation, buffer-until-ready, ResizeObserver, StrictMode idempotency — parameterized by bus events |
| Camera **adapter pair** as named types | Contain-fit (exists) vs pan/zoom/edge-scroll (exists as `worldCameraSystem`) — shared *math helpers* only; not one class |
| Generic Boot / pack loader | Boot that takes `nextScene` + pack keys; world still has no Boot by D10 |
| App-level asset packs that survive Game destroy | Phaser Texture/Sound/Anim managers **die with the Game**. Cross-stage zero-reload would be a custom pipeline **outside** Phaser (research: no official API) |
| Audio Director (music bus / SFX bus) | No web Phaser sound path today |
| VFX module API (`FxService(scene)`) | Beyond lawn rings: emitters, Groups, Phaser 4 camera Filters |
| Input focus-gate helper | Formalize “disable Phaser keyboard when React layer owns input” (GG-18); world `ignoreRects` stays the occlusion pattern |
| Lawn `importGuard` | Mirror world’s ban (or a documented, scanned allow-list for icon epoch) |
| Siege / battle / delve Phaser scenes | Consumers; **paused** until APIs are named |
| Quantified create/destroy memory cost | Probe item — not published for Phaser 4.2 sequential Games |

---

## Prior art

Genre and engine research, with numbers where a source actually published them. Unverified claims
are marked.

### Official Phaser (facts)

- **Global per Game:** textures, cache, sound, anims, registry. **Scene-local:** display list, tweens,
  Groups/pools, ParticleEmitters, cameras, input plugin, loader queue.
  ([Scenes injection map](https://docs.phaser.io/phaser/concepts/scenes))
- **`Game.destroy(removeCanvas, noReturn)`** is **asynchronous** (runs after the current frame).
  `noReturn: true` destroys core plugins so **you cannot create another Game on the same page**.
  Sequential stage islands **must keep `noReturn` false** (default).
  ([Game.destroy](https://docs.phaser.io/api-documentation/class/game))
- Loader is per-Scene; loaded assets go to **game-global** caches — a texture loaded in one Scene is
  available to all Scenes **of that Game**.
  ([LoaderPlugin](https://docs.phaser.io/api-documentation/class/loader-loaderplugin))
- Sounds **do not** stop on scene shutdown; looping music continues across `scene.start` unless
  stopped. One SoundManager per Game.
  ([audio skill](https://github.com/phaserjs/phaser/blob/master/skills/audio-and-sound/SKILL.md))
- Animation defs are a Game singleton; playback state is per Sprite.
  ([AnimationManager](https://docs.phaser.io/api-documentation/class/animations-animationmanager))
- Particles since 3.60: `this.add.particles` returns a ParticleEmitter Game Object (Manager removed);
  pool with `reserve` / `maxAliveParticles`.
- Groups as pools: `maxSize` (`-1` = no limit); recycle via `active`/`visible`;
  `killAndHide` + `tweens.killTweensOf`.
  ([Group](https://docs.phaser.io/api-documentation/class/gameobjects-group))
- Official React template: one EventBus; React never owns GameObjects.
  ([template-react-ts](https://github.com/phaserjs/template-react-ts))

### Community / secondary (use as failure modes, not locks)

- **Keep `update` thin; avoid mega-Scenes and deep BaseScene trees.** Consensus, not an engine rule.
  ([Discourse best practices](https://phaser.discourse.group/t/what-are-phaser-3-bad-best-practices/5088))
- **Object pools vs GC:** creating/destroying sprites every shot causes GC; at 60 FPS a hitch of a
  few milliseconds is a visible stutter. One 2025 tutorial cites **~5 ms** as enough to miss a frame
  — **unverified** (not a Photon Storm measurement). The official mitigation is Group/`maxSize` and
  particle `reserve`, not a magic pool size. Ourcade: `get` → reset → tween → `killAndHide` +
  `killTweensOf`. Pre-create (example **50** bullets in that tutorial) is a *worked example*, not a
  budget for this game.
- **React StrictMode** double-mounts: create → destroy → create. A `gameRef` guard without a matching
  destroy-on-cleanup **leaks canvases**. Our hosts already treat create/destroy as the unit; the new
  host must keep generation-scoped buses (RT-02/11).
- **Concurrent WebGL `Phaser.Game`s on one page:** historically fragile (Phaser 2-era reports).
  **Unverified for Phaser 4.2.** We avoid the pattern anyway (GG-1).

### Dual-track rewrites (the owner’s constraint)

Martin Fowler’s **strangler fig**: grow the new path beside the old; route one slice at a time;
delete the host when the new path is 100%. Microsoft’s write-up of the same pattern: a façade that
never dies is a failure mode; dual-write of *data* is fragile.

**What that means here (inference, reconciled with GG-1):**

| Strangler idea | This repo |
|---|---|
| Façade | Thin `createLawnGame` / `createWorldGame` / hosts |
| New system | `island/` + wired `board/` + camera adapters |
| Traffic | **One live `Phaser.Game` per current stage** — never two Games as A/B |
| Shadow / parallel run | **Tests and a fixture scene**, then one host flips |
| Kill switch | Keep the old paint function until the new path’s tests pass; then delete |
| Failure mode | Dual *implementations* drift (two grids, two destroy lists) if both stay live in production |

GOV.UK’s published scale (312 agencies, 1.8 million redirects) is a *web-platform* datapoint, not a
game-engine one — cited only to show the pattern is “slice by surface, not by calendar.”

**Wrong dual-track:** mount lawn’s old Game and a hidden “kernel” Game at once to compare pixels.
That fights WebGL instance limits and GG-1.

**Right dual-track:** new modules have **no production importer** until Vitest (and, for the lawn
grid flip, the existing “byte-identical lawn” bar) passes; hosts switch one island at a time; then
the cloned bodies are deleted.

---

## The shape

### Two kernels, configured per stage

```text
shared TS (survives Game destroy — the only reuse that works with GG-11)
  island/     createGame, destroyGame, PhaserGameHost (or hook), generation
  board/      GridSpec, BoardLayers, terrainCache, pickCell, keyboardNav, visualMapping
              ← already on disk; do not invent a second folder
  camera/     containFit (bindCamera) + panZoom adapters (wrap worldCameraMath)
  entities/   EntityRegistry; typed wrappers (ptr, sector, actor, …)
  vfx/        FxService factory(scene) — later wave; FxPool is the first adapter
  sfx/        AudioDirector(scene.sound) — later wave, when audio exists
  input/      focusGate + ignoreRects helpers
  boot/       BootScene({ nextScene, packs }) — optional per island

per React stage → new Phaser.Game (noReturn=false)
  thin ModeScene: wire systems + mode rules only
  React HUD / Esc always (no UIScene)
```

### Dataflow (unchanged contract, centralized plumbing)

```text
Server / fold (SSOT) → React host → EventBus <stage>:model → Scene systems → GameObjects
                                 ↑
                    <stage>:select / pick (intent only)
React → lib/bus → Intent / commands     Phaser never fetch/SignalR
```

Each Game’s `registry` dies on destroy. Durable SSOT is React Query / fold / server. Bus maps stay
**per stage** (`lawnListeners` vs `worldListeners`) so teardown cannot wipe the other island’s
subscribers during StrictMode overlap. A later `siege:*` map is a third map, not a shared dump.

Interpolation between two **server-confirmed** positions is rendering. Extrapolating past the last
confirmed state is prediction and stays rejected.

### Dual-track cutover (locked by owner this session)

1. **Add** island modules and tests. Old `destroyLawnGame` / `destroyWorldGame` / hosts stay the live
   path until the new helper is covered.
2. **Switch** lawn host to `destroyGame` + shared host (behaviour-identical). World second. Production
   still one Game per stage.
3. **Shadow the grid:** `BoardLayers` + `terrainCache` proven by existing unit tests **plus** a
   fixture or lawn adapter behind a test; `ensureGrid` remains live until the lawn acceptance bar
   (byte-identical after extraction — `spec-board-render.md`) is met.
4. **Flip** `LawnWorldScene` to `BoardLayers`; delete `ensureGrid`.
5. **Compose** `WorldRegistry` on `EntityRegistry` (or three typed registries) with no visual change.
6. **Name** siege/battle imports in the capability map; **then** unpause `siege-stage` 21.3+ /
   `battle-stage` 22.x (`base-defense-todo.md` resume condition).
7. **Retire** cloned bodies in the same program, on a deadline — a permanent façade is the
   strangler’s known failure mode.

Lawn remains the **first live grid consumer**. Decision 40’s “proven by two callers” is siege/battle
*after* lawn is on the layer — not a third paint implementation.

### Alternatives rejected

| Rejected | Why |
|---|---|
| One app-lifetime `Phaser.Game` + `scene.start` lawn↔world | Fights GG-11 / D2 / world-map lifetime; caches would survive but stages would not |
| Mega-Scene or deep `BaseScene` hierarchy | Mode rules creep into the base; community and DPLP both reject God objects |
| Stuffing world into `GridSpec` / `BoardLayers` | World is a pan/zoom graph; lawn `ensureGrid` and world placeholder grid are not the same problem |
| Inventing a second generic folder beside `src/game/board/` | The unused kernel *is* the scatter |
| Phaser `UIScene` for HUD | React owns chrome (DPLP §9) |
| Game-scoped Scene Plugins as the share mechanism | They die with the Game we destroy on leave |
| ParticleEmitter / tween handles on module singletons | Scene-local; leaks across destroy |
| Full bitECS / second sim in the browser | DPLP rejected; CapPolicy-scale lite registry + systems |
| Client prediction for feel | RT-15; `spec-board-render` §2 rule 3 |
| Merging injector VFX into Phaser `FxPool` | Different plane, different thread, different SSOT |
| Building siege canvas by copying lawn or world “by feel” | The pause exists to prevent a third stack |
| Phaser 3 migration | Pin is 4.2; custom pipelines do not copy |

### What “centralized components” means

| Component | Share how | Do not share |
|---|---|---|
| Sprites / atlases | Pack keys + Boot/Preload **per Game** | Live Texture Manager across stage travel |
| Animation defs | `anims.create` once after load in that Game | Recreate per Scene; assume defs survive destroy |
| SFX / music | Facade over `this.sound`; explicit stop on destroy | `add`+`play` BGM in every Scene `create` |
| VFX | Presets + `FxService(scene)`; pools scene-local | Global emitter refs |
| Camera | Two adapters | One “universal camera” class |
| Input | Focus-gate + ignoreRects; hit-test utils | One pick grammar for cell vs sector vs room |
| Objects | Factories / `GameObjectFactory.register` if keys stay unique | Mode rules inside the factory |
| Scenes | Thin shells | One Play scene with `if (mode)` |

---

## Tunables

This program does **not** add a `data/tuning/phaser-kernel.v1.json` in the idea phase.

| Number | Class | Where it lives |
|---|---|---|
| Entry chunk **≤180 KB gz** | Structural (GUI budget already locked) | `decisions.md` Game GUI; `check:bundle` |
| `Scale.RESIZE`, contain-fit margins, cell pixel size | Visual-design | Module-local constants (`gridMath`, camera adapters) — not combat balance |
| `EntityRegistry` map size | Structural (grows with living set) | No cap; lawn already diffs by ptr |
| FX `maxSize` / particle `reserve` | Visual/perf structural | Named const + comment when a feel pass would change it; **then** consider a small FE presentation JSON — not Core `vfx.v{n}.json` (injector) |
| Generation counter | Literal / protocol | `allocGameGeneration` |
| `noReturn` | Structural | Always default/`false` while another stage Game can follow |
| Sprite budgets (50/80) | CapPolicy-scale **guidance**, not a progression ceiling | `fe-game-foundation.md` §14; do not turn into a hard stop on content |

A pool `maxSize` that silently drops a hit spark is a **presentation** limit (exempt, must say so in
a comment), not a PS-8 progression ceiling.

---

## What this deliberately does not decide

- Siege HUD, battle playback UX, delve room-graph visuals — those programs’ specs.
- Whether delve is a cell board or a follow-camera crawl — `party-dungeon` / `delve-stage`.
- Art, atlases, and a real audio corpus — D10 framed placeholders remain valid.
- Moving lawn React from `features/lawn/` to `stages/lawn/` — shell consistency, not this kernel.
- Lifting textures outside Phaser for zero-reload stage travel — custom pipeline; out of v1.
- Injector VFX recipes, Unity write paths, Funnel, turn engine.
- Unpausing siege **before** `/spec` names the import list — the pause record already sets the gate.

---

## Open questions

Owner decisions only. Recommendations that nobody needs to dispute are written as the shape above,
not as questions.

**None that block `/spec`.** The dual-track rule, the two-kernel split, and “do not invent a second
`board/`” are decided. Program id `phaser-kernel` is the recommendation matching the audit’s “cross-
program Phaser kernel”; change the prefix only if it collides with a name you prefer.

If a later `/spec` wants a kill-switch **in the running app** (old `ensureGrid` vs `BoardLayers` at
runtime), that is a product toggle we do **not** recommend: two live paint paths will drift. Tests
plus one flip is the dual-track.

---

## DESIGN-GATE §5 (this session)

```
[x] Subsystems: FE game foundation (DPLP), Game GUI (GG-1/11/18/38), IA stages,
    world-map Phaser lifetime, board-render / paused siege-battle, standalone capability,
    product vision / loops.
[x] Read this session: the-game.md, the-loops.md, software-architecture.md §§1–5,
    decisions.md (Product vision, Standalone-first, Game GUI, Lawn projector, VFX),
    game-gui-principles.md (GG-1, GG-11, GG-18, GG-38, D2), information-architecture.md §1–2,
    design/README.md, fe-game-foundation.md, lawn-projector.md (purpose/planes),
    tunables-ssot.md §§0–3, standalone-rpg-map.md (charter header),
    world-map-program.md (map + what it does not touch),
    world-map-runtime-ideal.md (principles + inventory shape),
    spec-world-map-runtime.md (lifetime, systems, camera),
    spec-board-render.md, fe-phaser-architecture-audit.md,
    phaser-architecture-prior-art-2026-09-06.md, party-dungeon-map.md (delve-stage row),
    base-defense-todo.md pause, CLAUDE.md RPG-layer rule (via Step 0).
[x] Locks checked: DPLP, GG-11/D2, Decision 40, RT-15, Phaser 4.2, folder law.
[x] Claims cite file:line or a named official URL; code verified this session (grep + reads).
[ ] Did not re-run vitest / check:bundle / Playwright this session — honest gap.
    Bundle figures (133.4–133.5 KB gz entry, Phaser absent) are from board-render task evidence
    2026-09-06, not re-measured here.
[x] No §2 invariant contradicted. Dual-track is two modules, not two concurrent Games.
[x] Stale-doc findings named; not silently “fixed” in those files this phase.
```

**Unread on purpose:** full `world-map-program` turn-engine internals; full `spec-world-map-gaps.md`;
siege/battle stage spec interiors (pause headers suffice); `overlay-control-loops.md` cover-to-cover
(DPLP already states observe≠control for this canvas).
