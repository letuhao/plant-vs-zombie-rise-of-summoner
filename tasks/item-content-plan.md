# Implementation Plan: item-content

Specs: [item-content-map.md](../docs/architecture/item-content-map.md) → five modules under
[docs/architecture/item-content/](../docs/architecture/item-content/). Source research:
[item-content-ideal.md](../docs/architecture/item-content-ideal.md). Tasks:
[item-content-todo.md](item-content-todo.md).

Named pair per repo convention — `tasks/plan.md`/`tasks/todo.md` are the perf stream's, untouched.

## Overview

Every module here wires an already-built function, carries forward an already-authored field, or fills a
tuning file that already has 94 empty rows next to 14 full ones. No module authors a new mechanic, a new
naming grammar, or new game balance. The plan sequences five independent modules into six phases with one
rule the map already states: **item-naming ships first**, because every other module's own acceptance
criteria are easier to prove once a card shows a real name instead of a fallback key.

## Gates vs. checkpoints — the discipline this plan follows

Per this repo's own planning standard, no phase below is blocked on an external approval, a cross-team
ask, or a coordination check. Every choice this plan's own specs flagged as "ask first" is either (a) a
reversible implementation default, resolved here with its reasoning stated, or (b) a genuine, narrow
product question that ships as a named, non-blocking follow-up rather than a phase gate:

| Spec's own "ask first" | Resolved as | Why this is not a gate |
|---|---|---|
| item-naming: a genuinely new DTO field's shape | **Default: read `ssot-presentation.md` §5 before adding one; if none is needed (the fields listed in §3 below already cover every named surface), skip this entirely** | A research step inside the task, not an approval round-trip |
| item-lore: string catalog as a file vs. a table | **Default: stay a file** — `ssot-presentation.md` §5.3 N2 already made this call for v1 and named it reversible; this program's own volume (a few hundred more keys) does not change that calculus | The SSOT already picked a default; re-litigating it would be re-deciding a decision that was already made |
| atom-preview: standalone dev page vs. folding into `AlmanacDumpPage.tsx` | **Default: fold into `AlmanacDumpPage.tsx`** — one less page in the app shell, and the review-then-promote pattern already fits a preview-then-ship workflow better than a bare dev route | A UI-placement choice with no migration cost either way |
| atom-preview: should a non-developer ever reach this | **Named as a non-blocking follow-up, not built here** — the spec already scopes this module dev-only | Broadening the audience is a real product decision, but the module ships its full value before that question needs an answer |
| granted-action-text: hand-author 114 descriptions vs. a generative pass | **Default: hand-author**, following the same real-mechanism-first discipline this session's phantom-atom-family pass already used (read the action's real granted effect, write one real sentence) — a generative pass is a documented fallback only if the hand-authoring cost proves too high mid-task | 114 short, mechanically-grounded sentences is closer to this session's already-proven phantom-family authoring scale than to a 900-piece generation decision; no irreversible cost either way |
| affix-draw-coverage: are the rebalanced weights sane | **Not a gate — a verification STEP inside the task** (§Checkpoint E) | Measuring a real sample before publishing is exactly what T10 already does; it does not need to pause and ask anyone first, it needs to run and report |

**The one place this plan does treat something as load-bearing rather than default-and-move-on:**
whether sets get a lore surface. That question is not open here — the owner already decided it directly
(`item-content-ideal.md` §4.3, `item-content-map.md`'s own module description) — so `item-lore`'s tasks
below build it as settled, not as a question.

## Dependency graph

```text
wave 0   item-naming (T1-T4)                                    ┐
         item-lore (T5-T7)                                      ├─ independent, parallel
         atom-preview (T8-T9)                                   │
         granted-action-text (T14-T15)                          │
         affix-draw-coverage (T10-T13)                          ┘
```

All five modules are independent — no module's tasks require another module's tasks to land first. The
build order below groups tasks within a module (vertical slices, not horizontal layers), and orders the
five modules by impact, matching the map's own suggested sequencing, not by necessity.

## Phases and checkpoints

### Phase 1 — `item-naming` (highest impact: makes every other module's proof read as "a real item", not a fixture)

- **T1 — Wire `ItemNameComposer` into the real card-assembly path.** `RpgStore.ItemCard.cs`'s
  `GetItemCardInput` calls `Compose` for the instance's real container/affixes and carries the result as
  `ItemName`, replacing the `baseType.NameKey` fallback. Covers both minters (`Instantiator.TryInstantiate`
  and `InstanceProducer.Compose`) via one shared call, per the spec's own requirement that naming stay
  render-time and minter-agnostic.
  **Acceptance:** a real rolled instance's card returns a composed name, not a base-type key, for an
  instance from either minter. `ItemCardEndpointsTests.cs:340`'s existing wrong-by-design assertion is
  rewritten to assert the real composed name.
  **Verify:** a red-first test proven red against current code, green after; `dotnet test
  tests/FusionRpg.Core.Tests --filter ItemCard`, `tests/FusionRpg.Server.Tests --filter ItemCard`.

- **T2 — Stop discarding authored set/base-type names.** `ItemBaseTypeCorpus.Load` and `SetCorpus`
  carry the authored `name` field through, not only `nameKey`.
  **Acceptance:** a base type's card and a set's card block both show the real authored name.
  **Verify:** a red-first test per corpus loader.

- **T3 — Replace raw-id UI surfaces with real names**, now that T1/T2 supply one:
  `RelicsLayer.tsx:375`'s equip-target dropdown, `CompareView.tsx:59`'s delta-table row labels,
  `Compendium.tsx`/`SocketBench.tsx`/`ItemCard.tsx`'s combination row titles, `ArmouryList.tsx:109`'s
  `?? containerId` fallback.
  **Acceptance:** none of the five surfaces renders a raw container/instance/channel/combo id where a
  name belongs, proven against real seeded content, not a fixture with a pre-set name.
  **Verify:** a Vitest test per surface asserting no rendered node's text matches a raw-id shape;
  `npm run build`, `npm run test`.

- **T4 — Replace `Workbench.tsx`'s typed-id inputs with a real picker.** Reuses whichever list/search
  pattern `ArmouryList.tsx` already established.
  **Acceptance:** a player never types a raw recipe/container id to perform a craft/salvage/enhance/
  socket action.
  **Verify:** a Vitest test driving the picker end to end against a real recipe list.

> **✅ CHECKPOINT — item-naming.** A real rolled item, viewed anywhere in the shipped web UI (armoury
> list, card, compare, socket bench, compendium, workbench), shows a real name everywhere a name belongs,
> and no surface asks a player to type or read a raw id. Proven live against the running dev server with
> a real seeded item, not only by unit test.

### Phase 2 — `item-lore`

- **T5 — Fix unique flavour import.** `UniqueCorpus.cs` reads and carries forward the authored `flavor`
  text, not only `flavorKey`.
  **Acceptance:** a real unique with authored flavour carries its real sentence through import.
  **Verify:** a red-first test against `charnel-bloom-70` or an equivalent real, authored unique.

- **T6 — Replace the fake render with a real lookup.** `adapt.ts`'s `keyTail()` placeholder for
  `flavour.args.flavourKey` is replaced by a real string-catalog resolution.
  **Acceptance:** the card's Flavour block shows the real authored sentence, not a fragment of the key.
  **Verify:** a red-first Vitest test; `content/display/en.json` (or its chosen equivalent, per the
  file-stays-a-file default above) carries a real entry for every unique's populated `flavorKey`.

- **T7 — Extend the Flavour block to sets**, per the owner's own decision. `SetCorpus.cs` reads the
  set's authored `flavor` field (already present in the seed contract); the card's Flavour block renders
  for a set that has one, and renders nothing for one that does not.
  **Acceptance:** a real set with authored flavour (e.g. `sunwoven-almanac`) shows it; a set with none
  shows no block at all — no placeholder, no empty section.
  **Verify:** a positive and a negative red-first test; `ItemCardTests.cs:602-616`'s
  `Flavour_is_uniques_only` renamed and widened to match the new, correct invariant.

> **✅ CHECKPOINT — item-lore.** A real unique and a real set, each with authored flavour text, show
> their real sentences on their real cards, live. An item/set with no authored flavour shows no
> placeholder. The 32 uniques and 24 sets with no authored text are unaffected — this checkpoint proves
> wiring, not new authoring.

### Phase 3 — `atom-preview`

- **T8 — Build the preview route.** A new endpoint (file per this program's own one-file-per-concern
  convention) accepts an unsaved container definition and calls the existing, unmodified
  `ItemCardRenderer.Render`, returning the same `DisplayModel` shape the real card route returns.
  **Acceptance:** posting a real, valid container definition returns the identical output
  `ItemCardRenderer.Render` produces when called in-process on the same input — proving the route is a
  thin wrapper, not a second render path. A malformed input returns a real, named error, never a 500.
  **Verify:** a red-first equivalence test; a refusal-case test.

- **T9 — Build the preview page.** Folded into `AlmanacDumpPage.tsx` per the default above, reusing the
  existing `ItemCard.tsx` component to render the response.
  **Acceptance:** an author can paste or select a container definition and see it render as a real card,
  including at least one affix at `Min` and at `Max`, without saving anything.
  **Verify:** a Playwright/Vitest test driving the page end to end against a real container definition.

> **✅ CHECKPOINT — atom-preview.** An author previews a real, unsaved container's rendered card, live,
> through the same renderer production uses — screenshot-verified against the real running dev server,
> matching this session's own established verification standard for UI work.

### Phase 4 — `affix-draw-coverage`

- **T10 — Author `tier-bands.v1.json`'s missing rows.** Run `python -m seedsmith numerics rebalance
  --publish` (or the file's own current `_meta`-named command, re-read fresh before running) to fill a
  draw weight for all 109 families, not 14. Measure a real generation sample before and after — the same
  measured-not-assumed discipline this session's earlier diversity re-checks used — and confirm the
  proposed weights are not skewed in the way the earlier role×group-matrix bug was, before publishing.
  **Acceptance:** `FamilyExpansion.cs`'s refusal count drops from 95 to (ideally) 0, measured directly.
  **Verify:** a before/after corpus-refusal count; a real sample generation run.

- **T11 — Author the 2 missing display templates** (`atom.chill-punisher`, `atom.rot-punisher`),
  grounded in their real mechanical definition, never guessed from the name — matching this session's
  own phantom-family authoring discipline exactly.
  **Acceptance:** both render at Min/Max with no raw id.
  **Verify:** a per-family red-first test.

- **T12 — Register the 3 missing `UnitClass` entries** (`atom.elpw-focus`/`-overflow`/`-pierce`),
  grounded in each channel's real consumer.
  **Acceptance:** `ItemCardTests.cs:1457`'s existing pin passes for a real reason, not a stub.
  **Verify:** the existing pinned test, plus a per-channel render test.

- **T13 — Widen the "every atom renders" guard's scope**, once T10-T12 land. `ItemCardTests.cs:949`'s
  `RealAtoms` (`:57`) walks the real, now-larger admitted set; its doc comment (`:1026`) is corrected to
  match.
  **Acceptance:** the guard test passes at its new, larger scope, and is proven to have been capable of
  catching a real defect at that scope (a deliberately-broken fixture reproduces red before the fix, or
  an equivalent falsifying check).
  **Verify:** the widened test, run and green; a falsifying check that it is not vacuously passing.

> **✅ CHECKPOINT — affix-draw-coverage.** The corpus-refusal count is measured at 0 (or the plan names
> precisely which families remain refused and why, if the rebalance tool's own real output does not reach
> 100%). The guard test's scope matches its own claim.

### Phase 5 — `granted-action-text`

- **T14 — Schema and content.** `rpg_action` gains a description key column; all 114 seeded actions get
  a real, authored description, each grounded in the action's real granted effect (read the effect
  definition before writing the sentence).
  **Acceptance:** zero of the 114 actions have an empty or missing description after this task.
  **Verify:** a corpus-completeness guard test, matching `ItemCardTests.cs:949`'s own shape.

- **T15 — Wire card block 9 and the armoury compact-line tag.** Confirm (or build, if missing) that the
  description renders alongside the action's name, battle-only tag, and already-known state, and that the
  battle-only tag also appears on the compact armoury list line per `ssot-presentation.md` §9.14's own ask.
  **Acceptance:** a real item granting a real action shows its real description on its card and its
  battle-only status in the armoury list, without opening the card.
  **Verify:** a red-first render test against a real granted-action item.

> **✅ CHECKPOINT — granted-action-text.** Every granted action in the corpus has a real description, and
> it reaches both the card and the compact list.

### Final checkpoint — the whole program, together

> **✅ FINAL CHECKPOINT.** On one small, hand-seeded or lightly-generated slice (matching the owner's own
> "small playable slice" preference — no dependency on the seed→concrete generator or the held
> `classes.v1.json` v4 run), a player can see a real name, a real lore sentence where one is authored, a
> real granted-action description, and every affix family the tuning file now admits render as readable
> text — screenshot-verified live against the running dev server, the same standard this session already
> proved out for the item-system engineering audit itself.

## Verification, every phase

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter Items
dotnet test tests\FusionRpg.Data.Tests --filter Items
dotnet test tests\FusionRpg.Server.Tests --filter Items
npm run build   # web/fusion-rpg-web
npm run test    # web/fusion-rpg-web
python -m pytest tools/seedsmith   # for affix-draw-coverage's content pass only
```

**The bar is this repo's own standing baseline, not zero** — re-measure at each checkpoint against
whatever the current baseline is at build time (other streams are actively building in this tree), and
compare against that number, never against an assumed green.

## Risks — named, with the response

| Risk | Response |
|---|---|
| T10's rebalance tool proposes weights that reintroduce a role/element-exclusion bias, the same shape as the earlier role×group-matrix bug | T10 itself measures a real sample before publishing — this is a verification step already inside the task, not a separate gate |
| T14's 114 hand-authored descriptions turn out to be a larger authoring cost than expected mid-task | Documented fallback: a small, scoped seedsmith generative pass, following this repo's own seed-to-concrete conventions — named in the plan, not decided in advance, since the hand-authoring default may simply work |
| Any module's web-side work collides with a concurrent stream's own edits (this repo runs several programs at once) | Check `git status` before attributing any red test to this program's own change, matching the discipline the item-system engineering audit already established and proved out repeatedly |
