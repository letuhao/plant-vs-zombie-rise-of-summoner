# Phaser architecture prior art — multi-mode reuse (2026-09-06)

**Status:** Research reference. **Not a spec and not a plan.** Evidence for a later `/idea`
refactor that builds on [fe-phaser-architecture-audit.md](../architecture/fe-phaser-architecture-audit.md).  
**Stack target:** Phaser **4.2** (`web/fusion-rpg-web` pin). Phaser 3.60+ docs still apply for
loader / cache / sound / anims unless noted.  
**Method:** Official Phaser docs + Photon Storm materials + official React templates; two parallel
web-research passes (assets/VFX/SFX/anims · scenes/plugins/camera/input/dataflow). Reconciled with
this repo’s DPLP locks and measured scatter.

**Companion audit:** [../architecture/fe-phaser-architecture-audit.md](../architecture/fe-phaser-architecture-audit.md)
— as-built lawn/world clone + unused `board-render` kernel.

---

## 1. One-sentence rule (FACT — official)

**Definitions and bytes are global per `Phaser.Game`; things on the display list (and tweens) are
scene-local.**

| Global (game-owned) | Scene-local |
|---|---|
| `textures`, `cache`, `sound`, `anims`, `registry` | Display list, `tweens`, Groups/pools, ParticleEmitters, cameras, input plugin, loader queue |

Source: [Scenes concepts — injection map](https://docs.phaser.io/phaser/concepts/scenes)
(`'anims'/'cache'/'sound'/'textures'/'registry' = Global`).

**Critical implication for this repo (INFERENCE, reconciled with audit):** we create **one Game per
React stage and destroy it on leave** (GG-11 / D2). Global caches, SoundManager, and AnimationManager
**die with that Game**. Code shared across lawn ↔ world ↔ siege ↔ delve must live in **plain TS
modules / factories** that take a `Scene` (or managers) as arguments — not in Game-scoped Scene
Plugins that assume one long-lived Game.

---

## 2. What “correct Phaser architecture” usually means

### 2.1 Scenes as mode shells, not mega-classes

**FACT.** Official design: Scene Manager owns modes; each Scene has its own systems/plugins; the
Game mostly forwards RAF and DOM input.
([Scenes](https://docs.phaser.io/phaser/concepts/scenes))

**Community consensus (not a hard engine rule):** keep `update` thin; push work into systems /
plugins / factories; avoid one Scene that owns gameplay + HUD + audio policy + save.
([Discourse best practices](https://phaser.discourse.group/t/what-are-phaser-3-bad-best-practices/5088);
[scenes-state-architecture](https://github.com/onmax/nuxt-skills/blob/main/skills/phaser-best-practices/references/scenes-state-architecture.md))

**Rejected for this product (lock):** a Phaser HUD Scene for Almanac chrome — React owns HUD
([fe-game-foundation.md](../architecture/fe-game-foundation.md) §9). Parallel `launch('HUD')` is
valid Phaser practice; it is the wrong plane here.

### 2.2 Lifecycle verbs (FACT)

| Verb | Effect | Typical use |
|---|---|---|
| `start` | Stop caller; start target | Menu → play |
| `launch` | Run target **in parallel** | Overlay HUD (classic Phaser) |
| `switch` | Sleep caller; start/wake target | Tab-like swap |
| `sleep` / `wake` | Stop update+render; keep state | Fast return |
| `pause` / `resume` | Freeze update; still render | Modal over frozen world |
| `stop` | Shutdown scene | Leave mode inside same Game |

Ops are **queued** to the next Scene Manager step — not always same-call-stack.
([ScenePlugin](https://docs.phaser.io/api-documentation/class/scenes-sceneplugin);
[phaserjs scenes skill](https://github.com/phaserjs/phaser/blob/master/skills/scenes/SKILL.md))

### 2.3 One Game vs many Games

| Pattern | Official / practical | Fit here |
|---|---|---|
| One Game, many Scenes | Default | Fast lawn↔siege *inside one stage* |
| Sequential Games (create → destroy → create) | Supported; keep `destroy` `noReturn=false` if another Game follows | **Our React stages** |
| Concurrent Games on one page | Historically fragile for WebGL | Avoid |

**INFERENCE:** Lawn Game and World Game as separate islands is **valid**. Sharing then means
**modules**, not living registry/texture/sound instances across islands.

---

## 3. How to build, share, and reuse components

### 3.1 Reuse mechanisms (ranked for *this* repo)

| Mechanism | Lifecycle | Use when | Tradeoff |
|---|---|---|---|
| **Plain TS modules + factories** (composition) | Outlive Game destroy | Camera math, VFX spawn API, SFX facade, board-render, EventBus contracts | You own wiring |
| **`GameObjectFactory.register`** | Process / Phaser build | Shared GO types (`this.add.sectorPin`) | Key collisions; register once |
| **Scene Plugin** | Per Scene, dies with Scene | Helpers that need `this.tweens` / cameras *inside one Game* | Useless across our stage destroys unless reinstalled every create |
| **Global Plugin** | One per Game | Audio bus facade, analytics | Dies with Game; don’t store Scene refs |
| **Base Scene inheritance** | — | Tiny bootstrap only | Scales poorly — mode logic creeps in |

Sources: [PluginManager](https://docs.phaser.io/api-documentation/class/plugins-pluginmanager);
[ScenePlugin](https://docs.phaser.io/api-documentation/class/plugins-sceneplugin);
[Cadmus Phaser 4 plugins](https://cadmus.page/en/phaser/03-guides/plugins/) (global / scene /
game-object shapes);
[Factories](https://docs.phaser.io/phaser/concepts/gameobjects/factories).

**Aligns with audit seams:** island runtime + grid board layer as **TS modules**; do not invent a
second folder beside `src/game/board/`.

### 3.2 Component catalog (what games usually centralize)

| Component | Where bytes/defs live | Where runtime lives | Share across lawn/siege/delve? |
|---|---|---|---|
| **Sprites / atlases** | Global Texture Manager | Scene display list | Yes — load once per Game (Boot/Preload) |
| **Animation defs** | Global Animation Manager | Per-sprite `AnimationState` | Yes — `anims.create` once; `sprite.play(key)` everywhere |
| **SFX / music buffers** | Global audio cache + Sound Manager | Playback instances on manager | Yes — load once; **explicit stop** on exit (no auto cleanup) |
| **VFX pools / emitters** | Textures global | Scene Groups / emitters / tweens | Share **presets + API**; instantiate per Scene |
| **Camera** | — | Per Scene | Share **math + adapters** (contain-fit vs pan/zoom); not one camera class |
| **Input / controls** | DOM Input Manager global | Scene Input Plugin | Share focus-gate helpers; **mode grammars stay local** |
| **Interactive objects** | — | Scene GOs + pick systems | Share factories; pick/hit rules per mode |
| **Scenes** | — | Scene Manager | One thin Scene per mode (or per island) |

---

## 4. Dataflow and data architecture

### 4.1 Inside one Phaser Game (FACT)

```text
Loader (per Scene) ──loads──► Texture Manager / Cache / Sound (global)
Scene.registry     ──cross-scene flags inside ONE Game
Scene.data         ──per-scene ephemeral
Scene.events       ──local pub/sub (must off on shutdown)
```

Registry listeners are **not** auto-cleared on scene shutdown — remove on `shutdown` or leak.
([Cross-scene communication](https://docs.phaser.io/phaser/concepts/scenes/cross-scene-communication);
[Data Manager](https://docs.phaser.io/phaser/concepts/data-manager))

### 4.2 React ↔ Phaser (FACT — official template)

Official [phaserjs/template-react-ts](https://github.com/phaserjs/template-react-ts): shared
`EventBus` (`EventEmitter`); React never owns GameObjects; Phaser never owns React setState.

**Our lock (already shipped):** unidirectional model React→Phaser; intents Phaser→React; generation
scoped; no FE prediction ([fe-game-foundation.md](../architecture/fe-game-foundation.md) RT-04/15).

```text
Server / fold (SSOT) → React host → EventBus lawn:model|world:model → Scene systems → GOs
                              ↑
                         lawn:select / world:select (intent only)
```

**Across islands:** React/Zustand/Query is the only durable SSOT. Each Game’s `registry` dies on
destroy.

### 4.3 Lite ECS / systems (community + our DPLP)

Full bitECS is optional and often overkill at CapPolicy lawn scale (DPLP rejected full ECS library).
**Lite registry + ordered systems** (Sync → Layout → FX → Pick) matches both community practice
(“keep update empty”) and our allow-lists. World’s four systems are the same idea with different
domain objects.

---

## 5. Topic briefs

### 5.1 Assets / sprites

**Pattern:** Boot (tiny payload) → Preload (shared packs / atlases) → mode scenes load only
mode-specific packs.

**FACT:** Loader is per-Scene; loaded assets go to **global** caches — “A texture loaded in one Scene
is instantly available to all other Scenes.”
([LoaderPlugin](https://docs.phaser.io/api-documentation/class/loader-loaderplugin);
[Loader concepts](https://docs.phaser.io/phaser/concepts/loader))

Prefer atlases / multiatlas over hundreds of individual images. Unload scene-only textures on
shutdown when memory matters — after destroying sprites that reference them.

**Repo note:** Lawn Boot only generates a placeholder texture today; world has no Boot. Cross-stage
asset sharing needs an **app-level** pack strategy (or accept reload per stage Game).

### 5.2 VFX

**Pattern:** Global VFX atlas + **scene-local** FxService (emitters, Groups, tweens).

Particles (3.60+): `this.add.particles` returns a ParticleEmitter Game Object (Manager removed);
use `reserve` / `maxAliveParticles`. Groups = object pools (`get` / `killAndHide` +
`tweens.killTweensOf`).
([Group](https://docs.phaser.io/api-documentation/class/gameobjects-group);
[ParticleEmitter](https://docs.phaser.io/api-documentation/class/gameobjects-particles-particleemitter))

**Phaser 4:** screen FX via **Filters** on cameras/GOs — don’t copy Phaser 3 pipeline internals
([phaser-core skill](../../.claude/skills/phaser-core/SKILL.md) pitfall).

**Repo:** `FxPool` is lawn-local — correct scope; extract a **module API** for siege/delve to call
with their Scene.

### 5.3 SFX

**FACT:** One SoundManager per Game; sounds **do not** auto-stop on scene shutdown; looping music
continues across `scene.start` unless stopped.
([audio skill](https://github.com/phaserjs/phaser/blob/master/skills/audio-and-sound/SKILL.md);
[Scenes concepts](https://docs.phaser.io/phaser/concepts/scenes))

**Pattern:** Audio Director facade (music bus / SFX bus) over `this.sound`; load once per Game;
explicit stop on stage leave (our destroy checklist should own this when audio lands).

Category volumes are **app-level** — Phaser has one master volume.

### 5.4 Animation

**FACT:** Animation Manager is a Game singleton; defs are global; playback state is per Sprite.
([AnimationManager](https://docs.phaser.io/api-documentation/class/animations-animationmanager))

Create once after atlases load (`anims.create` / `load.animation` / `createFromAseprite`); never
recreate per Scene. Guard with `anims.exists(key)` if hot-restarting.

### 5.5 Camera

Cameras are **per-Scene**. Share **adapters**, not one class:

| Adapter | Modes |
|---|---|
| Contain-fit (zoom + centerOn) | Lawn, siege board |
| Pan / zoom / edge-scroll | World map |
| Follow + deadzone | Delve / battle crawl (if needed) |

Scale Manager FIT/RESIZE ≠ board contain-fit — different layers
([Scale Manager](https://docs.phaser.io/phaser/concepts/scale-manager)).

**Repo:** `bindCamera` (lawn) vs `worldCameraSystem` (world) — correct split; audit already forbids
forcing world onto `GridSpec`.

### 5.6 Input / interactive control

Scene Input Plugin processes keys/pointers; captures are Game-global. When React owns Esc / panels:
disable Phaser keyboard (`keyboard.enabled = false`) or never bind Esc in Phaser; gate canvas focus
via EventBus.
([KeyboardPlugin](https://docs.phaser.io/api-documentation/class/input-keyboard-keyboardplugin);
Discourse capture discussions)

**Repo:** GG-18 top layer owns input; world already uses `ignoreRects` for chrome occlusion — keep
that pattern as a shared helper.

---

## 6. Share matrix for Rise of Summoner stages

**INFERENCE** — maps research onto product stages (IA: Sanctum / World / Lawn / Battle / Siege /
Delve).

| Capability | Lawn | Siege | Battle playback | World map | Delve | How to share |
|---|---|---|---|---|---|---|
| Island host / destroy / generation bus | ● | ● | ● | ● | ● | Island **runtime** module (audit §10) |
| Grid board (`GridSpec`, layers, contain-fit, cell pick) | ● | ● | ● (synthetic/real) | — | maybe | `board-render` retrofit lawn first |
| Graph map (pins, lanes, pan/zoom) | — | — | — | ● | — | World island only |
| Entity registry primitive | ● | ● | ● | ● | ● | `EntityRegistry` compose |
| Sync-from-model systems | ● | ● | ● | ● | ● | Interface + mode adapters |
| VFX pool / emitters | ● | ● | ● | light | ● | Module + scene FxService |
| SFX / music | ● | ● | ● | ● | ● | Facade over `this.sound` per Game |
| Animation defs | ● | ● | ● | light | ● | Create once per Game Preload |
| Pointer pick | cell/occupant | cell/unit | replay scrub | sector/lane/force | tile/actor | Shared hit-test utils; different grammars |
| Keyboard | optional nav | board nav | transport | camera arrows | move/confirm | `keyboardNav` for grids; world keeps camera keys |
| React HUD / Esc | ● | ● | ● | ● | ● | Always React (GG) |

● = expected consumer. Lawn↔siege is the densest share (grid kernel). Delve shares VFX/SFX/controls
facades more than grid layout. World shares island runtime + presentation utilities, **not**
`GridSpec`.

---

## 7. Recommended target shape for the later refactor (INFERENCE)

Not authorized to build — seam list for `/idea`:

```text
shared TS (survives Game destroy)
  island/     createGame, destroyGame, host buffer, generation bus factory
  board/      GridSpec, BoardLayers, terrainCache, bindCamera, keyboardNav, pickCell  ← already exists
  camera/     containFit + panZoom adapters
  vfx/        FxService factory(scene) + pool helpers
  sfx/        AudioDirector(scene.sound)
  input/      focusGate, ignoreRects helpers
  factories/  GO register once at app boot if needed

per React stage → new Phaser.Game
  Boot/Preload (shared pack keys for that stage)
  ModeScene(s) thin: wire systems + mode rules only
```

**Wrong paths (research + audit agree):**

1. One app-lifetime Game + `scene.start` between lawn and world (fights GG-11 / stage lifetime).
2. One mega Scene / deep BaseScene hierarchy.
3. Stuffing world map into `GridSpec`.
4. Storing ParticleEmitters on a module singleton across Game destroy.
5. Inventing a second generic layer beside unused `board-render`.

**Right path:** finish wiring **existing** board-render into lawn; extract island runtime; share
VFX/SFX/input as modules; then unpause siege/battle against named APIs
([tasks/base-defense-todo.md](../../tasks/base-defense-todo.md) pause).

---

## 8. Source index

### Official

- https://docs.phaser.io/phaser/concepts/scenes  
- https://docs.phaser.io/phaser/concepts/loader  
- https://docs.phaser.io/phaser/concepts/cameras  
- https://docs.phaser.io/phaser/concepts/input  
- https://docs.phaser.io/phaser/concepts/data-manager  
- https://docs.phaser.io/phaser/concepts/scenes/cross-scene-communication  
- https://docs.phaser.io/phaser/concepts/gameobjects/factories  
- https://docs.phaser.io/api-documentation/class/animations-animationmanager  
- https://docs.phaser.io/api-documentation/class/plugins-sceneplugin  
- https://docs.phaser.io/api-documentation/class/gameobjects-group  
- https://github.com/phaserjs/phaser/blob/master/skills/scenes/SKILL.md  
- https://github.com/phaserjs/phaser/blob/master/skills/audio-and-sound/SKILL.md  
- https://github.com/phaserjs/template-react-ts  

### Secondary

- https://cadmus.page/en/phaser/03-guides/plugins/  
- https://phaser.discourse.group/t/what-are-phaser-3-bad-best-practices/5088  
- https://github.com/onmax/nuxt-skills/blob/main/skills/phaser-best-practices/references/scenes-state-architecture.md  

### Repo

- [fe-phaser-architecture-audit.md](../architecture/fe-phaser-architecture-audit.md)  
- [fe-game-foundation.md](../architecture/fe-game-foundation.md)  
- [spec-board-render.md](../architecture/base-defense/spec-board-render.md)  

---

## 9. What I could not find

Mandatory gap section (research README rule).

1. **No first-party Phaser guide titled “architecture for React stage islands with Game destroy.”**
   Dual-plane React+Phaser is documented via templates (EventBus); sequential Game destroy is
   documented via `Game.destroy`; combining them with GG-1 stage lifetime is **our** synthesis.
2. **No official API named “contain-fit camera.”** Pattern is community (zoom + `centerOn` /
   RESIZE + math). Our `bindCamera` is a local implementation of that pattern.
3. **No authoritative published “share matrix” for multi-genre modes in one Phaser title** (lawn TD
   + empire map + siege + delve). Genre games usually ship one primary mode; multi-mode reuse
   guidance is pieced from Scene/Plugin/Loader docs.
4. **Phaser 4 concurrent multi-Game WebGL on one page** — old Phaser 2/SO warnings; **unverified**
   for 4.2; we avoid the pattern anyway.
5. **Quantified memory cost of rapid Game create/destroy** (our stage travel) — not published; treat
   as a probe item for a later plan, not as settled.
6. **Whether AnimationManager / Texture Manager should be lifted outside Phaser** for cross-stage
   zero-reload — no official support; would be custom asset pipeline (out of scope for this note).
