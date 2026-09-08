# Spec: player-content-boot (E46)

**Status: DRAFTED 2026-09-03** — from the Wave 6 retrospective backfill, which found this while writing
E20's record. Module **E46**, effect-atom. **No dependencies.**

**What it owns: getting authored content into a player's install.** Today `AtomImporter` is invoked from
exactly one place in the repo, and it is a developer script. **A player install therefore boots on the
shipped code fallback with the entire content layer inert** — every atom, container, curve, rarity,
element row and channel policy.

---

## 1. The defect

Verified 2026-09-03: a sweep of `scripts/`, `src/` and `.github/` for `AtomImporter` returns
**`scripts/deploy-play.ps1:218` and nothing else** (CI runs its *tests* at `ci.yml:97`, never the tool).

`src/FusionRpg.Launcher/` — the WPF player entry that installs the loader, picks a port and starts the
game and server — has **no import step**.

**Sorted: real gap**, and it is the widest-blast-radius finding of the whole backfill. Everything Wave 7
and the action corpus generate would reach the owner's dev deploy and **no player at all**.

### 1.1 Why it went unnoticed

E20 `content-boot` genuinely closed what it set out to close: *"editing a roster row moved the content
hash and changed nothing"* — `LoadContentIntoRuntime` calls `ElementTable.Use` / `PowerTables.Use` at
host startup, and `deploy-play.ps1` runs the importer first. **On the owner's machine the chain is
complete.** The gap is one link further out, in a path no test and no dev workflow exercises.

**This is the same shape as D6 and as `TryInstantiate`'s zero callers** — a path that is correct
everywhere it is looked at, and never invoked where it matters.

---

## 2. What exists today

| Thing | State | Evidence |
|---|---|---|
| `AtomImporter` — reads the seed tree, validates, imports in one transaction | **built** | `tools/AtomImporter/` |
| `RpgStore.ImportContent` — single transaction, one `catalog_revision` bump | **built** | — |
| `LoadContentIntoRuntime` at host startup | **built** (E20) | — |
| The importer invoked from a dev script | **built** | `scripts/deploy-play.ps1:218` |
| The importer invoked from the **player install path** | ⛔ **does not exist** | `src/FusionRpg.Launcher/` has no import step |
| A code fallback when tables are empty | **built** — and it is what every player runs on | E20's own record |

---

## 3. The contract

### 3.1 The question this module has to answer first

**When does a player's content get imported?** Three shapes, and the choice is the spec's real content:

| Shape | Cost | Risk |
|---|---|---|
| **At install** — the launcher runs the import once after unpacking | One-time, visible, fails loudly while the user is watching | Install grows by the import time (~3 s at today's volume) |
| **At first run** — the server imports on startup if `catalog_revision` is 0 | No install change | A failure happens where the user cannot see it, and the fallback silently takes over — **exactly the current defect, one layer in** |
| **Bundled** — ship a pre-imported `rpg-hot.sqlite` | Fastest boot, no import ever | The database becomes a build artifact; content and schema versions must match, and a migration path is now mandatory |

**Recommendation: at install, with an explicit first-run repair.** The install is the one moment a
failure can be *shown* to the person who can act on it, and the first-run check turns a corrupted or
skipped install into a recoverable state rather than a silent fallback.

### 3.2 A failed import must be loud, and must not be a silent fallback

**The fallback is the defect's accomplice.** Today an absent import is indistinguishable from a
successful one, because the code fallback produces a working game with no content. So:

- A failed or skipped import is **surfaced to the player**, not logged.
- The server **reports** which mode it is in — imported content or code fallback — on a surface the
  player and the owner can both read.
- **The fallback stays.** It is what makes a broken install playable, and removing it would turn a
  content bug into an unbootable product. **It must simply stop being invisible.**

### 3.3 What must be imported

Everything `SeedContent` carries — atoms, containers, curves, rarities, elements, the element matrix,
channel policies — plus whatever E32 adds (affixes) and E43 emits (~490 generated rows). **The module
imports the tree; it does not enumerate its contents**, so a new seed kind needs no change here.

---

## 4. What this module must NOT do

- **Remove the code fallback.** §3.2.
- **Import silently.** The whole defect is that absence looks like success.
- **Bundle a prebuilt database** without a migration path — §3.1 names the cost.
- **Change the importer.** `AtomImporter` works; it is not called.
- **Import on every launch.** `catalog_revision` and the content hash already answer *"is this current"*;
  a re-import per launch would pay ~3 s and bump the revision, making every rolled `effect_instance`
  unbindable (`StaleInstance`).

---

## 5. Testing strategy

| # | Test | Proves |
|---|---|---|
| 1 | A clean install ends with a **non-zero `catalog_revision`** and the shipped atoms queryable | The headline defect is closed |
| 2 | **Planted violation:** an install whose import is skipped **reports the fallback**, and a test asserts the report | §3.2 — absence stops looking like success |
| 3 | A failed import (corrupt seed file) surfaces an error the player can see; the game still starts on the fallback | Loud, and still playable |
| 4 | A second launch **does not re-import** and does not bump `catalog_revision` | §4 — no stale instances |
| 5 | First-run repair: a zero-revision database imports on startup, once | §3.1's recovery path |
| 6 | The import covers **every** `SeedContent` list, asserted by reflection over the type rather than a hand-written list | §3.3 — a new seed kind is covered by construction |

**Test 6 is the durable one.** A hand-written list is what let `SeedContent` lack `Affixes` unnoticed
until E32 went looking.

---

## 6. Acceptance criteria

1. A player install imports the seed tree; `catalog_revision` is non-zero and content is queryable.
2. Import mode — real content or code fallback — is **reported** on a surface both the player and the
   owner can read.
3. A failed import is visible and non-fatal; the fallback still boots the game.
4. Re-launch does not re-import or bump the revision.
5. A zero-revision database repairs itself once on first run.
6. Coverage of `SeedContent` is by reflection, not a maintained list.
7. `deploy-play.ps1`'s existing import is unchanged — the dev path keeps working exactly as it does.

---

## 7. Dependencies and cross-program hazards

| | |
|---|---|
| **Depends on** | Nothing. **Should land before any generated content ships**, or that content reaches no player |
| **E20** `content-boot` | Closed the host-startup half. This closes the install half. **Neither is wrong; together they are the chain** |
| **E32**, **E43** | Their output must be inside what this imports — covered by §3.3's whole-tree rule |
| **launcher** | Owns the install flow. **A cross-program change**, and the launcher's owner should see §3.1's three shapes before one is picked |
| **Stale instances** | A revision bump invalidates rolled instances. On a *first* install there are none, which is why install-time import is the cheap moment to do it |
