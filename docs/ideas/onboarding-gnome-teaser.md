# Gnome-quarantine onboarding teaser

**Status:** accepted narrative direction — a future UI implementation still needs its own scoped task.  
**Hangs on:** lawn first core · farm/hunt/defend · quests and events. It adds no loop, resource, reward, or checkpoint. The game has no player-facing save-slot feature; durable state belongs to the current server-selected profile in SQLite.  
**Lore boundary:** the Gnomes and the Gnomiverse are PvZ: Garden Warfare 2 inspiration; the Void, Rift/Fracture, and any quarantined-sector crisis are Rise of Summoner fiction. See [PvZ canon boundary](../research/pvz-lore/canon-boundary-and-onboarding.md).

## The promise

Before the first lawn, show a **four-beat visual prologue**. It should feel like a Diablo-style scene transition—not a lore lecture—and leave the player with one clear job:

> Keep this lawn anchored. Do not let the corruption spread.

The story establishes a three-way pressure without explaining a cosmology:

- **Dave** wants to save the familiar lawn and its people.
- **The Void** is the project-original corruption making places and creatures wrong.
- **The Gnomes** are strange time custodians. Their answer is quarantine: if a sector cannot be stabilized, they seal it away.

The Gnomes are not the Void and are not the final boss. They are an unnerving force whose solution may be worse than the disease for the people living inside a failing sector.

## The four beats

Each beat is one full-screen illustration or composed scene, one short line, and a single tap/click to advance. No narrator paragraphs; the image carries the context. Beats 1–3 use the `Next` action; beat 4 uses `Anchor the lawn`. Every beat has a **Skip intro** control, and beat 4 has the only action that starts play.

| Beat | What the player sees | On-screen line | What it teaches |
|---|---|---|---|
| 1 — Familiar | Moonlit lawn. A Peashooter and Sunflower face a tiny zombie wave; the picture is warm and recognizably PvZ. A hairline crack flickers far behind the fence. | **Dave:** “Uh-oh. That lawn is doing the wrong kind of wobbly.” | Start from something known and worth protecting. |
| 2 — Wrong | The crack opens. One plant-zombie silhouette stutters between shapes; color drains at its edges. No proper name is shown yet. | **Penny:** “Temporal signal unstable.” | This is a new corruption, seen before it is explained. |
| 3 — Judgment | A clean, geometric Gnome time-mark descends over the broken edge of the lawn. It pushes the corruption back, but also turns the grass beyond it into a silent, sealed horizon. | **Gnome signal:** “UNSTABLE SECTOR. QUARANTINE PENDING.” | The Gnomes can contain a threat, but their containment threatens the world itself. |
| 4 — Choice | Dave steps forward; Penny projects a small route from the lawn into a distant fractured sector. The Gnome mark remains in the sky. | **Dave:** “Then we fix it before they close the gate. Plants first. Questions later.” | The player starts on the lawn, with a future wider war promised rather than explained. |

**Final action:** `Anchor the lawn` → existing lawn start / first playable objective.  
**Secondary action:** `Skip intro` → the same destination. Skipping never loses Souls, grants, content, or future story access.

## Placement, eligibility, and lifecycle contract

The teaser is a **band-3 `DialogShell` owned by the Sanctum stage**. It opens over the Sanctum; it does
not add a route, rail entry, panel, checkpoint, or fourth first-session reveal. The Sanctum remains
mounted underneath, and the dialog is the only surface that receives input while it is open.

This feature does not create a save system. The current profile is the server-selected player
(`settings.current_player_id`, bootstrapped as Player 1 when the local database is empty). Story state
belongs in `rpg-hot.sqlite` keyed by `player_id`; it survives a server restart but is not a player-facing
save slot or cloud profile.

PvZ image capture has a different home: the existing `rpg-media.sqlite` image-dump pipeline. Raw captured
layers remain in `type_icon_layers`, composed portraits remain in `type_icons`, and `TypeIconStore`
remains the authority for dump provenance and retrieval. The Rift story ledger must not copy those BLOBs
into the hot database; the semantic Rift manifest may reference a dump layer by source key when an image
is reused. If the implementation needs a durable semantic-to-dump mapping, it adds metadata only in the
media database (never in profile/story SQL):

```sql
CREATE TABLE IF NOT EXISTS rift_asset_sources (
  asset_id TEXT NOT NULL,
  role TEXT NOT NULL,
  source_kind TEXT NOT NULL CHECK (source_kind IN ('pvz_dump', 'generated', 'licensed')),
  side TEXT,
  type_id INTEGER,
  layer TEXT,
  source_uri TEXT,
  sha256 TEXT,
  captured_utc TEXT NOT NULL,
  revision INTEGER NOT NULL DEFAULT 1,
  PRIMARY KEY (asset_id, role, revision)
);
```

`pvz_dump` rows require a matching `(side, type_id, layer)` in `type_icon_layers`; missing or stale
references fall back to the Rift placeholder. This table is provenance, not a player save, story ledger,
or progression authority, and it does not duplicate image BLOBs.

The server is authoritative for whether it should appear. The eligibility rule is:

```text
story = "rift-prologue-v1" is unseen
AND player has no settled PvZ lawn victory
AND no lawn run is currently active
AND the player is entering or returning to Sanctum
```

An existing profile that has never settled a PvZ lawn victory may see the prologue on its next Sanctum
entry. A profile with a settled PvZ lawn victory never receives the blocking first-entry dialog. A direct
deep link into a lawn, an active run, or a server response that is unavailable must not trap the player;
the teaser can wait for the next eligible Sanctum entry.

The story acknowledgement is a separate, non-reward record. It must not be encoded as a row in the
checkpoint ledger or inferred from browser storage:

```text
rpg_onboarding_story(
  player_id, story_id, version, state, outcome, acknowledged_utc, revision,
  PRIMARY KEY (player_id, story_id, version)
)
```

For v1, `story_id = "rift-prologue"`, `version = 1`, and `state` is `unseen` or `acknowledged`;
`outcome` is `completed` or `skipped`. Extend the existing onboarding API with a separate `stories`
field and one mutation; do not change the meaning of `checkpoints`:

```text
GET  /api/onboarding/{playerId}
  stories: [{ storyId, version, state, outcome, eligible, acknowledgedUtc, revision }]

POST /api/onboarding/{playerId}/stories/{storyId}/ack
  body: { version: 1, outcome: "completed" | "skipped" }
```

The mutation is idempotent: repeating the same outcome returns success, while an unknown story,
version, or outcome returns a named 4xx reason. It never creates a reward or changes a checkpoint.
The first successful acknowledgement increments the story `revision`; concurrent requests resolve to
one stored outcome, and a conflicting second outcome returns a named conflict without changing it.

New-player bootstrap creates the `unseen` row before the first Sanctum render. A migration/backfill
creates the row for existing players and applies the settled-victory rule below, so eligibility never
depends on a missing-row race.

For profiles created before this story exists, data initialization marks the story `skipped` when a settled
PvZ lawn victory already exists. If a new player bypasses the prologue during a state/API failure and
then settles the first PvZ lawn victory, the settlement may mark this separate story record `skipped`
as housekeeping. That write does not award anything, decide victory, or mutate the checkpoint ledger.

Beat position is session UI state, not durable progression. If the tab closes before acknowledgement,
the next eligible presentation starts at beat 1. Once the acknowledgement succeeds, reloads and retries
must not reopen the blocking dialog.

### State and failure behavior

| State | Player-visible behavior | Durable effect |
|---|---|---|
| `eligible` | Open the prologue at beat 1 | None |
| `playing` | One beat at a time; `Enter`/`Space` advances | None |
| `submitting` | Disable duplicate actions; show a short “Saving…” label | None until the server replies |
| `acknowledged` | Close and continue to the existing lawn start/focus prompt | Record `completed` or `skipped` |
| story GET fails | Show a compact retry notice plus `Continue to lawn`; never block play | None; retry on the next Sanctum entry |
| story acknowledgement fails | Let the chosen lawn action continue, show “We’ll try again next time,” and keep the story eligible until a first victory settles | None; no localStorage fallback |
| asset missing | Use the designed Rift placeholder from the asset spec and keep controls usable | None |

`Skip intro` is an explicit non-destructive choice. `Esc` follows the dialog convention and invokes
the same skip outcome; it must not silently discard a reward or run. The final `Anchor the lawn` action
and `Skip intro` share one destination and must be guarded against duplicate navigation.

## Accessibility and responsive behavior

- Use the existing dialog focus trap (GG-18/GG-19). Initial focus is the dialog title or the first
  actionable control; `Tab` cycles `Advance`/`Anchor the lawn` → `Skip intro` and returns to the
  opener after close. Arrow keys may advance only when focus is not in a button or link.
- The image is decorative (`aria-hidden="true"`) when the line is present. The current beat line is
  exposed as a labelled live region, and a text-only equivalent contains the beat number and speaker.
- Every control is a real button with a visible focus ring and at least 44×44 CSS px on touch layouts.
  The dialog title, progress (`1 of 4`), and current action are understandable without color or motion.
- `prefers-reduced-motion` removes fades, pans, distortion, and timed auto-advance; the same static
  scenes, copy, and controls remain available. There is no audio dependency.
- The dialog is bounded to the viewport (`max-width: 960px`, `max-height: calc(100vh - 32px)` on
  small screens and `-48px` on larger screens). Its body scrolls internally if text or localization
  expands; the Sanctum behind it never scrolls.

## Serious Rift VFX contract

The Rift cannot ship as a static PNG swap alone. Beats 2 and 3 require a high-impact, layered VFX pass
implemented through the existing VFX SSOT (`VfxCatalog` → `VfxDirector` → shared primitives), while
remaining presentation-only and safe to drop under load.

Required semantic cues for the prologue are:

```text
rift.portal.open       tear opens; edge energy and depth reveal
rift.portal.surge      corruption pulse; chromatic/lime energy pushes outward
rift.quarantine.seal   Gnome geometry locks the sector boundary
rift.quarantine.fade   sealed horizon settles into the final still
```

The visual grammar should combine a warped tear silhouette, layered rim glow, depth/parallax shift,
short particle streaks, a readable impact pulse, and the clean geometric quarantine mark. No cue may
write gameplay state, depend on HP polling, or bypass `VfxDirector`. Each cue needs a Core recipe,
rate/cap policy, an anchor decision, a reduced-motion/static fallback, and a `prove-vfx.ps1` assertion.

The full effect may degrade to the story sprite plus a static seal when the injector is absent, a shader
probe fails, the VFX cap is reached, or reduced motion is enabled. The story still advances.

## Verification and acceptance

The UI slice is complete only when these cases pass:

1. **Fresh player:** with no settled PvZ lawn victory, Sanctum opens the four beats once; `Anchor the
   lawn` records `completed` and reaches the existing lawn start without creating a checkpoint.
2. **Skip/reload:** `Skip intro` records `skipped`; a reload does not reopen it, and the three reward
   checkpoints remain unchanged.
3. **Interrupted beat:** closing the tab before acknowledgement leaves the story unseen; the next
   eligible Sanctum entry restarts at beat 1.
4. **Existing profile:** a player with a settled PvZ lawn victory never sees the blocking dialog.
5. **API failure:** GET or acknowledgement failure leaves a usable path to the lawn and offers retry;
   no browser-local value is treated as proof of acknowledgement. If the player wins before the story
   can be acknowledged, the separate story record becomes `skipped` without changing checkpoints.
6. **Accessibility:** keyboard-only, screen-reader text, focus restoration, reduced motion, touch
   target, and contrast checks pass against the existing dialog-shell test harness.
7. **Narrative safety:** no line claims that Gnomes can erase the PvZ multiverse, that the Void is
   franchise canon, or that the teaser itself transports armies or grants power.
8. **VFX:** beats 2/3 emit the declared Rift cues through the shared VFX director; the effect is visible
   in the live proof, drops safely when unavailable, and never changes gameplay state.

Recommended v1 events are observational only: `story_impression`, `story_advance`, `story_skip`,
`story_ack_success`, and `story_ack_failure`, each tagged with `story_id` and `version`. Telemetry is
not a gameplay authority and is not a release gate for the first implementation.

## First-user guide copy

Show this only after the final action, next to the existing first playable prompt:

> **Your first job**  
> Defend the lawn. A settled victory earns Souls and opens Dave’s commander sheet. The wider Rift story will meet you when you are ready.

This teaches the current server-owned first-session contract without moving or renaming its three reveals: first victory → Dave, level 3 → species progression, level 4 → equipment. See [first-session progression](../architecture/standalone/spec-first-session-progression.md).

## Implementation boundaries

- The teaser is **presentation only**. It cannot award a reward, decide a victory, calculate a progression gate, or replace the current `first-win-dave`, `level-3-general-species`, or `level-4-dave-equipment` queue.
- It belongs before the first playable lawn entry, not as a fourth milestone card inside the existing reward queue.
- Its acknowledgement must be durable and per player when implemented; browser-local storage is not an onboarding authority. A later server/Data slice may record that the teaser was seen, but must leave the reward-checkpoint ledger unchanged.
- It must be playable and testable with the lawn game closed as a standalone presentation path. The eventual lawn-start action is an enrichment destination, not its only rendering mode.
- Reduced-motion mode presents the same four static scenes without timed fades, pans, distortion, or particle emission.
- No new Gnome power rank or multiverse-destruction claim appears in UI copy. The story uses quarantine as Rise of Summoner fiction, inspired by the Gnomes’ canonical time/cosmic imagery.
- The four spoken lines are **original project copy**, not verbatim PvZ dialogue. “Gnome signal” is a
  project translation of the visual warning, not a claim that a canonical Gnome voice or protocol exists.

## Why this direction is stronger than a Void monologue

It preserves the franchise’s comic readability: Dave reacts like Dave, the lawn is immediately legible, and the odd Gnome warning is memorable. At the same time, it gives the Rift stakes a concrete shape: losing ground is not abstract failure; it may be sealed away forever.

## Deferred deliberately

- The Void’s origin and any relationship to Zomboss.
- Why the Gnomes cannot simply solve the crisis themselves.
- A Gnome King boss, Gnome faction mechanics, or a Gnome reward track.
- World-map sectors, legions, and empire UI before the player has completed the lawn-first learning sequence.

Those are later story/event material, not first-minute obligations.
