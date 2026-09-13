# item-content — ideal

**Status:** idea phase, captured 2026-09-06. Research only — no spec, no plan, no code in this document.
Triggered by the owner declining the `classes.v1.json` v4 full generative run and asking for a
multi-perspective audit of player-facing content-quality gaps first, with new specs to follow.

**Program relationship:** this is not a new subsystem. It is the item program's own presentation and
authoring-quality slice — everything downstream of "the atom/container/instance layer already works" and
upstream of "a player can read what they picked up." `item-ideal.md` (D1–D41) and
`docs/architecture/item/ssot-presentation.md` (Lane G3, drafted 2026-08-22) are both authoritative here
and are not re-litigated; this document verifies their design against **today's shipped code**, four
research passes deep, and reports what actually reached a player versus what only reached a spec.

---

## 0. Architecture principles, restated

**Every RPG feature lives in the RPG layer and is never built by changing what PvZ is** (`CLAUDE.md`).
Nothing in this document is about the PvZ game's own capabilities — naming, lore, description rendering
and content-authoring tooling are entirely inside the RPG's own content-authoring and content-display
pipeline (seed JSON → import → `DisplayModel` → the web SPA). The question this document keeps asking
is never "can the game show this" — it is **"does the RPG layer already have a machine for this, and is
it wired end-to-end, or is it inert?"** An inert path — a function nothing calls, a field nothing reads,
a column dropped at import — is a **wiring gap**, not a content or architecture shortfall, and every
finding below is sorted with that distinction on purpose, because four independent research passes found
that almost everything the owner is worried about is exactly this shape.

**The design-gate discipline applied here:** read the subsystem's own SSOT before proposing anything
(`docs/DESIGN-GATE.md` §1), verify every claim against `src/`, cite `file:line`, and never trust a
document's self-report of its own current state — `ssot-presentation.md` is from 2026-08-22 and this
program has shipped a great deal since, including an entire web UI **today**. Every finding below was
re-verified against current code by four separate research passes, not read off the spec.

---

## 1. Why this document exists — the owner's own words

> *"I want to confirm that we already cover diversity, I prefer a small item part that can [be] playable
> first, the pipeline [is] still not complete, item [names are just an] id, not user friendly, set or
> unique items have no lore or description, item atom [effects] have no GUI to show options, atom effect
> lack of user friendly description, and maybe we still [have] a lot [of] gap[s] — I think [it's] better
> to use multiple perspectives, audit gap[s] and make new specs to cover [them] first. [A] full run now
> can [mean we need to] rerun [it] when [we] play [the] game because [there's] still a lot [of] sub
> pipeline[s]."* — owner, declining the `classes.v1.json` v4 full generation run, 2026-09-06

Read plainly: don't spend the one expensive, hard-to-reverse generation pass while the pipeline that
would present its output to a player is still leaking content at multiple points. Fix the leaks first,
on a small slice, then decide about the big pass — because a second full generation after the pipeline
matures is exactly the "rerun" cost the owner is naming.

**On diversity, confirmed:** yes, already covered, and this document does not re-open it. The 2026-09-06
generative-content evaluation sample (`tasks/item-todo.md`, Checkpoint 0's own evidence table) measured
261/261 legal brief picks (vs. an earlier 60/7), 81 of 98 eligible families drawn across all five axes
(vs. 14 defensive-only before the fix), and charm-axis Gini at its floor — a real, executed, falsifiable
measurement, not an assumption. What this document adds is a **different** measurement — draw-gate
eligibility for the affix layer specifically (§4) — which turns out to have the same numeric shape
(a small admitted set against a much larger authored one) for a related but distinct reason.

---

## 2. The single unifying finding

Four independent research passes — item/set/unique naming, lore/flavour text, an authoring GUI, and
atom-effect description coverage — converged on the same structural shape independently, which is why it
is reported once here instead of four times:

> **Real, already-authored, often high-quality content exists. It does not reach a player.** Not because
> it needs to be written, but because a specific, narrow, named piece of wiring — a function nobody
> calls, a field the importer drops, a tuning file with 14 rows where ~109 are needed — sits between the
> content and the player.

The honest metric, counted rather than estimated: **898 authored flavour sentences exist in the item
corpus and zero reach a player. 107 of 109 affix families have real display text and only 14 (12.8%) can
ever be drawn into real content.** Every number below is measured, with file:line, not inferred from the
spec.

This reframes the owner's "maybe we still have a lot of gaps" concern precisely: the gaps are real and
there are several, but they are **shallow and specific**, not a signal that months of new authoring are
owed. The pattern is exactly the one this session's engineering audit found repeatedly in the plumbing
layer (a real production caller missing, a content-copy rule missing, a literal read the wrong field) —
it turns out the same pattern governs content presentation too.

---

## 3. Finding 1 — item, set and unique naming

**Owner's concern:** *"item [names are just an] id, not user friendly."*

### 3.1 What is built

- **The naming machine is correct and complete.** `ItemNameComposer.Compose` (`src/FusionRpg.Core/Items/ItemNameComposer.cs:25-48`) and the word table (`AffixNameTable.cs:24-84`) implement I8's grammar (`ssot-affixes.md` §4.12) against a real, authored `nameWords` corpus across all 98 base affix-family files (`data/seed/items/affix-families/g-life.json:51` is one instance).
- **The old failure mode is already gone.** `ssot-presentation.md` §7.5 documented, in 2026-08-22, that the shipped roster screen equipped items by typing a raw container id into a text box (`RosterPage.tsx`, placeholder `stub.atk_ring | stub.butter_bead | stub.hp_charm`). That file is **deleted** (commit `4195a2d`); its route now redirects to the new sanctum panel (`routes.tsx:122`, asserted by `e2e/creatures.spec.ts:87-89`). This specific, historically-documented failure is resolved, not merely hidden.
- **Set and base-type names are authored.** All 740 base-type entries carry a real `name` (e.g. "Spun Cap"); all 30 sets carry a real `name` (e.g. "Stillmarch").

### 3.2 Wiring gaps — every one of these is "the code to fix a specific call site," not "build a system"

| # | Gap | Evidence |
|---|---|---|
| 1 | `ItemNameComposer.Compose` has **zero production callers** — only its own test calls it (`ItemNameComposerTests.cs`) | `Program.cs:692-696` builds `ItemCardCorpus` without an `ItemName`; `RpgStore.ItemCard.cs:203` falls back to the generic `baseType.NameKey` |
| 2 | The fallback is self-documented as a known gap, and a test **locks in the wrong behaviour** | `ItemCardEndpoints.cs:241-244`'s own comment; `ItemCardEndpointsTests.cs:340` asserts the raw key `"base.card-proof-blade"` as the expected `name` |
| 3 | No runtime `familyId → nameWords` loader exists, and the rare-name draw has no production supplier | `tasks/item-todo.md:8850` |
| 4 | Authored set/base-type `name` fields are **read and then discarded** by the loader that feeds the card | `ItemBaseTypeCorpus.Load` (`ItemCardEndpoints.cs:45-59`) reads `nameKey` and drops the sibling `name` field entirely |
| 5 | Today's shipped web UI shows raw ids and GUIDs in several places, not because naming is impossible but because nothing upstream supplied a name | `RelicsLayer.tsx:375` (a full instance GUID inside a dropdown `<option>`); `CompareView.tsx:59` (a raw channel id as the delta table's entire left-column label); `Compendium.tsx:47` / `SocketBench.tsx:67` / `ItemCard.tsx:281` (raw `comboId` as a row title); `ArmouryList.tsx:109` (falls back to `container.xxx` via `adapt.ts:732`'s `?? containerId`); `Workbench.tsx:54,76,143` (the player **types** a raw recipe/container id — the exact `RosterPage` failure mode, recurring in a new file built this same week) |

### 3.3 Real gaps

- **Neither minter (`Instantiator.TryInstantiate` nor `InstanceProducer.Compose`) stores a name on the instance row, and this is deliberate, not a gap** (`ItemNameComposer.cs:10-11`) — naming is meant to be a render-time composition so a reroll cannot silently rename an item. Recorded here so a future spec does not "fix" this by adding a stored name column; the fix is wiring the composer into the render path, not the data model.

### 3.4 Prior art

Diablo 2's magic/rare naming is exactly I8's already-chosen shape: `[prefix] [base] of [suffix]` for magic items (roughly half suffix-only, a quarter prefix-only, a quarter both), up to three prefixes and three suffixes for rares, one affix per family. Rare *names* (distinct from their affixes) come from a purely decorative two-word draw (`RarePrefix.txt`/`RareSuffix.txt`, e.g. "Bitter Cleave") gated only by item type, with no bearing on the rolled mods — this validates I8 §4.12's own name-band split. Path of Exile and Grim Dawn follow the same prefix/base/suffix pattern; Grim Dawn derives displayed rarity from the highest-quality affix present rather than a separate roll, a cheap pattern worth knowing about if this repo's own rarity-vs-affix coupling is ever revisited. ([Diablo 2 Prefix](https://diablo2.diablowiki.net/Prefix), [D2 Affixes](https://diablo-archive.fandom.com/wiki/Affixes_(Diablo_II)), [PoE Modifiers](https://pathofexile.fandom.com/wiki/Modifiers), [Grim Dawn Items](https://grimdawn.fandom.com/wiki/Items))

---

## 4. Finding 2 — lore and description for sets and uniques

**Owner's concern:** *"set or unique items have no lore or description."*

### 4.1 What is built

- **The render slot exists and is tested.** Card block 10 ("Flavour") is real: `ItemCard.cs:31,40,46,52,238,582-597`, a real DB column (`RpgStore.ItemUniques.cs:46`), a real route (`ItemCardEndpoints.cs:59`), a real web component (`ItemCard.tsx:345-348`), and a real test (`ItemCardTests.cs:602-616`, `Flavour_is_uniques_only`).
- **The prose already exists, and it is good.** 112 of 144 unique entries carry both a `flavorKey` and a real authored sentence (example: *"It grows toward whatever screamed last."*). All 740 base types and 40/70 charms carry authored flavour text too. **898 sentences total.** An existing internal review (`docs/architecture/item/review/wave2-flavour-quality.md`) already rates this writing "the best writing in the project."

### 4.2 Wiring gaps

| # | Gap | Evidence |
|---|---|---|
| 1 | The import pipeline reads only the flavour **key** and never the flavour **text** for uniques | `UniqueCorpus.cs:210` reads `OptStr(e, "flavorKey")` only; the sibling `flavor` literal is never read anywhere in `src/` |
| 2 | The renderer fakes a display string instead of a real lookup | `adapt.ts:964` does `view.flavour = keyTail(flavour.args.flavourKey)` — takes the key's last path segment (`flavor.unique.carrion-spitter` → *"carrion spitter"*) — and `adapt.ts:812-818` documents this itself as *"a placement, never a translation"* |
| 3 | `content/display/en.json` carries **zero** flavour keys — all 108 entries are mechanical affix templates | confirmed by direct read |
| 4 | 6 of 30 sets carry authored flavour text (`sunwoven-almanac.json:83-84` etc.) that **no code path reads at all** — not even at the wrong level, unlike uniques | `SetCorpus.cs:15` reads `DisplayName` only; `ssot-sets.md`'s own DDL has no flavour column |
| 5 | 32 of 144 uniques (8 each across 4 files) have neither key nor text — this is a smaller, separate authoring gap, not a wiring one, and is already self-documented | `UniqueRow.cs:113-115` |

### 4.3 Real gap, and correctly so

**Sets have no design for lore at all**, and this is not an oversight to silently fix. `ssot-presentation.md` §4.1 scopes card block 10 to "uniques only," and no later ruling overturned it (checked `decisions.md`, `spec-uniques.md:257`, `item-ideal.md`) — even though `seed-contract.md:277-278` and `spec-set-charm-gen.md:42` both make `flavor` an available field on every generated entry kind, including sets. **This matches genre convention closely**: Diablo 2/4 uniques carry background flavour text; set items in the same games generally do not — flavour is treated as a property of hand-authoring, not of item power tier. Path of Exile is the outlier, giving flavour text to every Unique and Divination Card, written by named staff. **This is a real decision point for the owner** (§8), not a defect: do sets get a lore surface (a genuine new small scope, since the field already exists in the seed contract and 6 sets already used it), or does the existing "uniques only" ruling stand?

### 4.4 Prior art

D2/D4 uniques carry 1-2 sentences (~10-25 words) of background flavour; PoE extends the convention to every Unique and Divination Card via three named staff writers. This repo's 112 authored unique lines match the genre norm in scale and tone exactly. Precedent for narrative text elsewhere in this repo: `CreatureTraitCatalog.cs:7,13-29` ships 14 hardcoded one-line blurbs; the Delve/party-dungeon program ships a **live**, working `Flavor` field on the wire (`DomainRow.cs`, `QuestRow.cs`) — proof that this repo's own content pipeline can and does deliver flavour text to a player when it is wired, which is the strongest available evidence that this is a wiring gap and not a structural one. ([Wowhead: uniques in D4](https://www.wowhead.com/diablo-4/news/unique-items-in-diablo-4-season-1-affixes-effects-models-flavor-text-334167), [PoEDB FlavourText](https://poedb.tw/us/FlavourText), [Writing Flavour Text — E. McRae, PoE](https://www.edmcrae.com/article/writing-flavour-text-for-items-in-your-game))

---

## 5. Finding 3 — a GUI for atom/effect authoring

**Owner's concern:** *"item atom [effects] have no GUI to show options."*

### 5.1 The honest headline

**This one is a real gap, confirmed with the least ambiguity of the four.** No GUI, web page, admin
panel, or notebook exists anywhere in this repo for browsing, previewing, or configuring atom/effect
definitions. Confirmed by direct search: zero frontend references to atoms/kinds/affix-pools/containers
(`web/fusion-rpg-web/src` grep for `effectAtom|atomKind|affixPool|EffectContainer|atomFamily` = 0
matches); the existing dev panel lists ten surfaces, none atom-related
(`web/fusion-rpg-web/src/dev/DeveloperTree.tsx:26-37`); no server route exposes atom/effect/container
data at all (`AtomPushService.cs:17-19` states outright: *"No atom row, container row or curve row is
ever put on the wire"*); every one of the 37 `tools/` projects is a console executable, none a GUI
framework or notebook. A prior internal audit already reached the same conclusion in different words:
*"no effect authoring surface exists yet"* (`effect-adoption-audit-2026-08-22.md:124`).

The current, confirmed authoring workflow: hand-edit raw JSON under `data/seed/items/affix-families/`,
run `tools/ItemSeedValidator` for a text report, run `tools/AtomImporter --check --validate` for an
all-or-nothing console pass/fail. No preview of the rendered result exists anywhere in that loop.
`tools/seedsmith`'s own LLM-brief preview (`items generate --dry-run --sample-brief`) shows **bare atom
ids**, and one of its own modules **deliberately refuses** to use a family's shipped display template
when building a brief (`adapters/trees/nodegen/vocab.py:43-45`) — so even the generative pipeline's own
human-facing surface shows a developer id, not a sentence.

### 5.2 The gap is narrower than "no GUI" sounds

The description half of the concern is **not** a real gap — it is already built, separately from any
GUI. `ItemCardRenderer.Render` (`src/FusionRpg.Core/Items/Display/ItemCard.cs:216`) is a **pure,
DB-free function** — it takes in-memory rows and two lookup delegates, and tests already construct these
by hand without touching a database (`ItemCardTests.cs:249`). It turns an atom into a real sentence via
109 of 109 affix families carrying a real `displayTemplate` today. **The renderer that would power a
preview GUI already exists, is tested, and needs no redesign** — what is missing is a route that accepts
an *unsaved* container (the current `GET /api/items/{id}/card` route 404s on anything not already
persisted and owned, `RpgStore.ItemCard.cs:156-163`, "no write path, deliberately") and a page that calls
it.

### 5.3 A close, working precedent already exists — in a sibling pipeline

`web/fusion-rpg-web/scripts/render-tree-cards.mjs` (891 lines) already generates reviewable HTML cards
for **passive-tree** content — rendering each node's channel id, unit class, trigger and preview
magnitude through the *same shipped* `formatMagnitude` function the live app uses, so there is one
magnitude contract rather than two (`:1-14`). It runs offline (not reachable from the running app), but
it is a complete, working example of exactly the pattern item-content needs: **take real content, run it
through the real renderer, produce something a human can read and approve before it ships.** Separately,
`features/almanac-dump/AlmanacDumpPage.tsx:58` is a real, in-app "review then promote" page with its own
edit route (`Program.cs:1040`) — proof the web app already has the *shell* pattern for an author-facing
content page, not just an offline script.

### 5.4 Prior art

Public documentation of other studios' internal item/effect authoring tools is thin — GGG's is
undocumented outside community reverse-engineering (`RePoE`). The transferable pattern is **schema-driven
form generation over the exact JSON shape this repo already has** — `react-jsonschema-form` (a React
library already compatible with this repo's stack) auto-generates an editing form from a JSON Schema, and
`gitcms` is a public example of a git-backed CMS over JSON files with almost exactly this repo's
`data/seed/**` shape. Unity's `CustomEditor`/`EditorWindow` pattern (typed schema → generated inspector →
live preview) is the canonical in-engine analogue, even though this repo is not Unity-authored on this
side. Spreadsheet-to-game-data pipelines (Unreal DataTables, Sheets-to-ScriptableObject converters) are
the dominant industry pattern for *bulk numeric balance*, which is a different problem from "show me what
this atom does" and should not be reached for here. ([RePoE](https://github.com/brather1ng/RePoE),
[react-jsonschema-form](https://github.com/rjsf-team/react-jsonschema-form),
[gitcms](https://github.com/wpf500/gitcms), [Unity custom Inspector](https://docs.unity3d.com/Manual/UIE-HowTo-CreateCustomInspector.html))

---

## 6. Finding 4 — how complete is "atom effects read as sentences", really

**Owner's concern:** *"atom effect lack of user friendly description."*

### 6.1 The real numbers, counted today

| Corpus | Count |
|---|---|
| Affix families authored | **109** |
| `item_display_template` rows | **107** |
| Templates `status: "live"` | **106** (1 `pending`) |
| Template keys present in `content/display/en.json` | **107 / 107** |
| Families that pass the `tier-bands.v1.json` draw gate | **14 / 109 (12.8%)** |
| Families in the actually-generated atom corpus on disk | **9** (45 atoms, across `g-armour`/`g-attack`/`g-life` only) |

### 6.2 What is still genuinely missing (small, and named precisely)

- **Real gap, no template at all (2 families):** `atom.chill-punisher`, `atom.rot-punisher` — authored into the affix-family corpus today, the template file was never updated to match.
- **Real gap, mechanism unwritten (1 family):** `atom.entangling` — its template exists and is `pending` because its status payload kind (`UnityCc`) has no Unity branch implemented; this is a mechanism gap, not a text gap.
- **Real gap, no unit registered (3 families):** `atom.elpw-focus` / `-overflow` / `-pierce` — their channels have no `UnitClass` entry, pinned by an existing test (`ItemCardTests.cs:1457`).
- **Real gap, a genuinely separate and larger hole: granted actions.** 114 actions across the committed corpus have a `name` and **zero** `description` — the field does not exist in either the seed content or the `rpg_action` schema (`RpgStore.Actions.cs:22-60`), even though `ssot-presentation.md` §9.14 already commits to rendering an action's name **and description** as card block 9. This is not a rendering gap; there is nothing to render yet.
- **Real gap, a localisation-shape gap rather than a blank-text gap:** 1,075 other content-name keys (560 base-types, 145 uniques, 109 affix names, 70 charms, 40 gems, 40 drop-tables, 35 sets, 30 recipes, 25 socket-words, 21 materials) are **not** represented in `content/display/en.json` — but they do carry inline English `name` literals in their own seed files, so a player sees real text today; it just is not routed through this program's own key-based localisation contract (L3, `ssot-presentation.md` §3.6).

### 6.3 The dominant bottleneck, quantified precisely

> **95 of 109 families have a real, complete, already-authored display template and cannot appear in any
> real dropped item, because `data/seed/items/_tuning/tier-bands.v1.json` only authors a draw weight for
> 14 of them.** `FamilyExpansion.cs:124` refuses any family whose stem the tuning file does not name.
> **Every element-typed family is in the refused set**, which is why no pooled-channel atom exists in any
> shipped content today. The fix is entirely a content-authoring pass against one already-identified
> file (`python -m seedsmith numerics rebalance --publish`), not a code change — this is the same root
> cause the engineering audit found twice already this session (P1.5's geared-corner-run content gap, and
> module 10's Compose-render fix), now quantified as the dominant reason "user-friendly descriptions" feel
> incomplete: the descriptions are there, 88% of them simply never get a chance to be read.

### 6.4 The "every atom renders" guard test's real scope

The guard test that is supposed to prove universal coverage (`ItemCardTests.cs:949`,
`Every_real_atom_renders_at_min_mid_and_max_with_no_raw_id`) only walks the families the shipped
`tier-bands.v1.json` admits — i.e. the same 14, not all 109 (`:57`, its own doc comment says so at
`:1026`). This is not a defect in the test; a family the corpus cannot draw genuinely has nothing to test
against yet. It does mean the test's green status is not evidence that "atom descriptions are done" —
that claim needs the §6.3 fix first.

### 6.5 Prior art

Path of Exile's `stat_translations.json` is, structurally, exactly this repo's Lane G3 design generalised
further: one translation entry maps a *stat id* to text and can cover several stat ids at once, with
condition-gated format variants for different value ranges and unit-conversion "index handlers" (e.g.
divide-by-100) living **in the translation layer itself** — the same shape as this repo's still-open
`Increased`/`More` ÷1000 boundary question (`ssot-presentation.md` §3.2.5, claim C1, still unconfirmed).
Diablo 3's affix model confirms the same fixed-sentence/variable-roll split this repo already chose: the
sentence is authored once, the magnitude is drawn at drop time. No public source gives an exact template
count for a AAA game's stat-translation table, so this repo's "~110 templates cover ~775-1,075 atoms"
ratio cannot be benchmarked against a published number — only its *architecture* is confirmed to match
shipped genre practice. ([RePoE stat_translations](https://github.com/repoe-fork/repoe/blob/master/RePoE/docs/stat_translations.md),
[PoE Wiki: Stat](https://pathofexile.fandom.com/wiki/Stat), [Diablo Wiki: Affix](https://www.diablowiki.net/Affix))

---

## 7. The ideal — what "done" looks like

This is a vision, not a spec or a task list (that is the next phase). Stated as end states:

1. **A rolled item always has a real name.** `ItemNameComposer` is called from the one real production
   path that assembles an `ItemCardInput`, for every minter, and no card or list ever falls back to a
   base-type key when a rolled instance is behind it. Set and base-type names read from their own
   authored `name` field, not discarded at load.
2. **Authored flavour text reaches the player it was written for.** A unique's card shows its real
   sentence, not a key fragment. The sets-lore question (§4.3) is a decision the owner makes once,
   explicitly, rather than an accidental non-render.
3. **An author can see what an atom does before it ships**, by pointing a small preview surface — reusing
   the existing, tested, DB-free `ItemCardRenderer` — at an unsaved container, the same way
   `render-tree-cards.mjs` already does for passive-tree content. This does not require a general-purpose
   editor; a read-only preview closes the specific pain named.
3a. **Player-facing raw ids are gone from the shipped UI**, not just from the deleted `RosterPage` —
   `CompareView`'s delta labels, `Compendium`/`SocketBench`'s combo titles, and `ArmouryList`'s
   container-id fallback all resolve to real names, and no surface asks a player to type an id
   (`Workbench.tsx`'s current recipe/container text inputs get a real picker).
4. **Display-template coverage is measured against the tuning gate, not against the template count.**
   The honest metric moves from "14 of 109 (12.8%)" toward "109 of 109," by authoring
   `tier-bands.v1.json`'s missing 95 rows — a content pass with a named owner and no code change — after
   which the existing guard test's own scope should be widened to match, so a green run means what it
   claims to mean.
5. **Granted actions have a description, in schema and in content**, closing the one card block
   (`ssot-presentation.md` §9.14) that currently has nothing to render at all.

**Sequencing implied by the owner's own preference** ("a small item part that can be playable first"):
items 1-3a are pure wiring — no new content, no generation cost, fixable and testable in isolation, and
they are the ones that make even a *small* hand-seeded or lightly-generated slice look and read like a
finished feature rather than a debug build. Item 4 is a real, but narrow and already-scoped, content-tuning
pass, independent of the big `classes.v1.json` v4 run. Item 5 is a separate, smaller schema-plus-content
addition. None of the five requires or benefits from running the full ~1,800-piece generation first —
if anything, fixing 1-3a and 4 first is what would make a *later* full run's output actually presentable,
which is the owner's own stated reasoning for holding it.

---

## 8. Open questions for the owner

1. **Do sets get a lore surface, or does "uniques only" stand?** The field already exists in the seed
   contract and 6 sets already used it (§4.3). This is a real design decision, not a defect — genre
   precedent supports either answer.
2. **Is the localisation-key gap (§6.2's 1,075 keys) in scope for this pass, or tracked separately?** The
   text already reaches players today via inline literals; the gap is architectural (not routed through
   L3) rather than a blank-screen problem, so it may be lower priority than items 1-5 above.
3. **Should the atom/effect preview (§7 item 3) be a standalone dev page, or folded into the existing
   almanac-dump review pattern** (`AlmanacDumpPage.tsx`) that already has an edit-and-promote shell in the
   live app?
4. **What counts as "a small playable slice"** for the purposes of validating the wiring fixes before any
   larger generation decision — a specific number of hand-seeded or lightly-generated items, a specific
   base-type family, or something else? This document does not propose a number; it is the next
   conversation.

---

## Design-gate checklist

| Box | State |
|---|---|
| Read `DESIGN-GATE.md`'s topic index for this subsystem | ✅ "Anything a player sees (UI)" and "The atom / Secondary effect layer" rows, this session |
| Read every doc in the §1 row(s), this session | ✅ `ssot-presentation.md` (full, ~1284 lines), `ssot-affixes.md` §4.12, `ssot-uniques.md`, `ssot-sets.md`, `effect-atom/definitions.md`, `effect-pipeline-map.md`, across four parallel research passes |
| Checked `decisions.md` for a lock covering this | ✅ checked for a sets-lore ruling; none found — §4.3/§8.1 name it as open |
| Every factual claim cites file:line | ✅ throughout §3-§6 |
| Verified claims against CODE, not comments | ✅ all four passes verified against `src/`/`web/` directly; several findings explicitly contradict what a comment or an older spec implied |
| Sorted into built / wiring gap / real gap | ✅ every finding, §3-§6 |
| Web-searched genre prior art with sources | ✅ §3.4, §4.4, §5.4, §6.5 |
| Nothing contradicts a design-gate invariant | ✅ no proposal here changes where logic lives, the Foundation/Secondary boundary, or any numeric-type rule |
| No spec, plan or code written | ✅ this document stops at the ideal |
