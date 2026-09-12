# Task list: item-content

Plan: [item-content-plan.md](item-content-plan.md). Specs:
[docs/architecture/item-content/](../docs/architecture/item-content/). Map:
[item-content-map.md](../docs/architecture/item-content-map.md).

Named pair per repo convention — `tasks/plan.md`/`tasks/todo.md` are the perf stream's, untouched.

Five independent modules, wave 0 (no dependency order required between them). Suggested build order below
matches the plan's own impact-first sequencing, not a hard requirement.

---

## Phase 1 — `item-naming`

- [x] **T1** — Wire `ItemNameComposer.Compose` into `RpgStore.ItemCard.cs`'s `GetItemCardInput`, replacing
      the `baseType.NameKey` fallback, for both minters. **Done 2026-09-06.**
      **Acceptance:** a real rolled instance's card returns a composed name for an instance from either
      `Instantiator.TryInstantiate` or `InstanceProducer.Compose`. `ItemCardEndpointsTests.cs:340`'s
      wrong-by-design assertion is rewritten.
      **Built — three gaps, not one.** The call site was never the whole problem:
      1. **No `NamedAffix` list existed.** New `ItemNameAssembly` (Core) builds one from the instance's
         frozen atoms minus the container's fixed-core `seq` set — the SAME rule
         `ItemCardRenderer.Classify` reads, so the name can never be composed from a line the card
         files under base stats.
      2. **No runtime `familyId → nameWords` loader existed.** New `AffixNameWordCorpus` (Server), the
         third stopgap of the same shape as `ItemBaseTypeCorpus`/`GemInsertCorpus` — it walks
         `affix-families/*.json` and hands each family's rows to the already-correct
         `AffixNameTable.ParseSlot`. This is the `item_affix_name` PROJECTION `AffixNameTable`'s own doc
         describes and no importer ever built.
      3. **No rare-name words existed anywhere in the seed tree.** New
         `data/seed/items/rare-names/rare-names.json` — 26 head × 26 tail = 676 decorative pairs in the
         corpus's own plant/decay voice, plus `RareNameCorpus`, whose draw is the shipped, version-pinned
         `SeededRng.DeriveStream(roll_seed, "item.rare-name")`, never a local hash.
      `ItemCardCorpus` gains `LookupNameWords` + `RareNameDraw` (the "needs per-request resolution"
      delegate shape `LookupBaseType`/`LookupInsert` already use); `ItemName` stays as an explicit
      override and is documented as having been structurally incapable of carrying a composed name — it
      is a flat string on a record built once at host start.
      **Live proof, both sides of the threshold**, over a real in-process host, a real SQLite store, the
      real seed corpus and a real `Instantiator` mint:
      - under threshold (1 rolled affix): `GET /api/items/{id}/card` → **`"Enduring Card-Proof Charm"`**
        (`atom.fortitude`'s own band-C word + the base type's authored name)
      - at threshold (3 rolled affixes, seed `0xC0FFEE`): → **`"Sap Tangle"`**, both words from the new
        shipped table
      **Red-first:** `ItemCardEndpointsTests.cs:340` asserted `"base.card-proof-blade"` and PASSED —
      the wrong behaviour was pinned, not merely untested. Separately, reverting `WithConnective`
      reproduced `"Sturdy Bark Helm of of Hoarfrost"` before the fix below.
      **Evidence:** `Server.Tests --filter Item` **84/84**; `Data.Tests --filter Item` 222/223;
      `Core.Tests --filter Item` 1013/1014. Neither red is this task's:
      `ItemUniqueStoreTests.Unique_eligible_seeds_every_rung_through_the_sc7_gate` hard-codes ordinal 30
      while the committed `data/tuning/uniques.v1.json` says `rungFloorOrdinal: 80` (pre-existing, both
      files unmodified); `ItemDisplayTests.Every_shipped_family_has_a_display_template` pins 107 and the
      corpus now holds 109 — that is **T11** landing in a concurrent session. Both were reproduced with
      this task's own content edits stashed.
      New: `Card_forAnItemUnderTheRareThreshold_isNamedByTheAffixGrammar`,
      `Card_forARareItem_getsASeededTwoWordNameFromTheShippedTable`,
      `Card_composesTheSameNameOnEveryRead`,
      `WithoutTheNamingCorpora_theCardFallsBackToTheAuthoredBaseNameNotAKey`,
      `TheAffixNameWordCorpus_carriesEveryShippedFamilysAuthoredSlot`,
      `TheAffixGrammar_doesNotDoubleTheOfConnectiveOnRealAuthoredWords`,
      `TheRareNameDraw_isSeedStableAndSpreadsAcrossTheTable`,
      `The_card_name_is_composed_from_the_instances_own_rolled_affixes_and_seed`,
      `The_named_affix_set_excludes_the_containers_fixed_core`,
      `An_item_under_the_rare_threshold_is_named_by_the_affix_grammar`,
      `A_suffix_word_that_already_carries_its_own_connective_is_not_given_a_second_one`,
      `Every_band_keyed_family_resolves_a_word_at_every_tier`.
      **⛔ Three real defects found and named while wiring this (all latent because the composer had no
      production caller):**
      - **The `of` connective had two homes.** `ssot-affixes.md` §4.12's grammar line owns it
        (`… of [suffix word]`) while its own sample table authors it into the word (`of Embers`), and
        all **153** shipped suffix words follow the table — so the composer's unconditional `"of "`
        produced *"Sunworn Charm of of Embers"* on every real suffix-bearing item. **Fixed** in
        `ItemNameComposer.WithConnective`: the grammar still owns the connective, and a word that
        already carries one is passed through. Content was not rewritten to suit code.
      - **`atom.bulwark` / `atom.tempo-stampede` could not resolve a name at t3+.** Each carried ONE
        row labelled `band: "A"` while declaring `tierRange: "t1-t5"` and while its own note described
        a band-C-only word — three readings of one fact, and `AffixNameTable.Resolve` throws for any
        tier the label misses. `FamilyExpansion` expands every family to all five tiers regardless of a
        "rare tier band only" flag, so a t4 bulwark atom really exists. **Fixed in content:** the one
        authored word now sits under all three D29 bands, which is what `tierRange: t1-t5` already said.
        Guarded by `Every_band_keyed_family_resolves_a_word_at_every_tier`.
      - **`FamilyExpansion.Expand` writes `WhenJson = "{}"` unconditionally** (`FamilyExpansion.cs:208`),
        so `AffixValidator.AffixClassOfAtom`'s trigger-derived prefix/suffix split answers **`Prefix`
        for all 109 generated families** — including the 51 whose entire authored vocabulary is suffix
        words. **NOT fixed here** (it moves the atom catalog and every golden). Worked around honestly:
        `AffixNameSlot.Slot` reads the family's own authored `nameWords` slot, which is content, not a
        second derivation — documented at the type. **Filed for the effect-atom / affix-library owner.**
      **⏸ Named limitations, not silently absorbed:**
      - No shipped item can currently roll an `of …` clause at all, because all 14 families
        `tier-bands.v1.json` admits are prefix-authored. That is **T10's** gap, and the suffix half of
        the grammar is proven against real authored words instead
        (`TheAffixGrammar_doesNotDoubleTheOfConnectiveOnRealAuthoredWords`).
      - An element-typed family names off its first authored word, because `FamilyExpansion` leaves
        `variant` empty (W7.9) and the card's own element resolution is private to the renderer. This is
        `AffixNameTable.Resolve`'s own documented fallback, not new behaviour.
      - A **unique** takes its base type's authored name: `UniqueRow` carries no name column, so module
        17's hand-authored name has nowhere to live yet. Named at `ComposeItemName`.
      - **Not proven against a published `dist/` build:** a concurrent session owns the server currently
        running on `:5088` and the shared `dist/`, so republishing would have destroyed its state. The
        content-copy rule was verified instead (`rare-names.json` lands in the Server's output tree), and
        the proof above runs over a real in-process host rather than a mock.
- [x] **T2** — `ItemBaseTypeCorpus.Load` and `SetCorpus` carry the authored `name` field through instead
      of discarding it. **Done 2026-09-06.**
      **Acceptance:** a base type's and a set's card block show the real authored name.
      **Built:** `CardBaseType` gains `Name` and `ItemBaseTypeCorpus.Load` reads `entry.name` (all 740
      shipped entries have one); the header emits it as `baseName`, **beside** `baseNameKey`, never
      instead of it — `ssot-presentation.md` §5.3 N2 keeps the key as the localisation path, and the
      authored string is what renders while the catalog has no row. `CardSet` gains `Name` and
      `ReadSetBlock` carries `SetDef.DisplayName`; the set header emits it as `name` beside `nameKey`.
      **The gap was on the card, not in `SetCorpus`:** `SetCorpus.Parse` has always read `name` into
      `DisplayName` and `item_set.display_name` has always round-tripped it — the card derived
      `set.{setId}` and dropped the real string.
      **Why this had to land with T1:** `ItemNameComposer` glues words onto a NOUN. Without the authored
      name it would have composed `"Sturdy base.bark-helm of Embers"`.
      **Measured:** `content/display/en.json` carries **108** keys, **zero** of them `base.*` or `set.*`
      — so before this, every card and every set block showed a key where a name belongs.
      **Evidence:** `TheBaseTypeCorpus_loadsEveryShippedPartitionAndNamesRealEntries` extended to assert
      `"Honed Hatchet"`; `The_set_block_carries_the_sets_authored_display_name` (new);
      `The_dal_assembled_card_is_byte_identical_to_a_hand_assembled_one` went **red** on
      `name=|nameKey=set.frostbit…` vs `name=Stillmarch|…` before its mirror was updated — that red is
      the T2 proof for the set half.
- [x] **T3** — Replace raw-id UI surfaces with real names: `RelicsLayer.tsx:375` (equip dropdown),
      `CompareView.tsx:59` (delta labels), `Compendium.tsx`/`SocketBench.tsx`/`ItemCard.tsx` (combo
      titles), `ArmouryList.tsx:109` (`?? containerId` fallback). **Done 2026-09-06.**
      **Acceptance:** none of the five surfaces renders a raw id against real seeded content.
      **Built — two of the five needed a real name on the wire, three did not.**
      1. **Equip target** (`RelicsLayer.tsx`) — a unique actor carries `side` + `typeId` and *no name*
         (`adaptActor` marks `displayName` pending; no route serves one), so the name comes from
         `CreatureSpeciesCatalog` via the already-shared `useSpeciesIndex`, keyed `(side, gameTypeId)`.
         The panel **subtitle** was fixed with it — it printed `#${instanceId.slice(0,6)}`, and a
         shortened id is still an id.
      2. **Delta labels** (`CompareView.tsx`) — new `channelLabel` in `adapt.ts`. ⚠ A **placement,
         not a translation**, on `keyTail`'s own terms: **no channel display-name corpus exists
         anywhere** (`derived-stats/catalog.json` authors compose/unit/consumer and no name;
         `content/display/en.json` has no `channel.*` row — its 276 keys are `action.*`/`disptpl.*`/
         `flavor.*`/`tpl.*` only). The id stays on the row's `title` and `data-testid`.
      3. **Combo titles** (all three) — new `CombinationView.title` (additive) from
         `combinationTitle`. Same placement rule and the same reason: the 25 shipped combinations are
         **generated** by `ResonanceGenerator` and no corpus names one.
      4. **Armoury row** — the real fix was on the wire. `ArmouryRowDto` gains **`ContainerName`**
         (additive, defaulted `""`), filled from the *same* `ItemBaseTypeCorpus` delegate the card
         route reads — hoisted in `Program.cs` so both read ONE corpus. `adapt.ts`'s
         `?? row.containerId` is gone; the order is relic name → base-type authored name →
         `UNNAMED_ITEM`.
      **⛔ A real defect found and fixed while wiring this:** `adaptItemCard` ran
      `keyTail(header.name)` **unconditionally**, which was harmless while `name` was a `base.*` key
      and became wrong the day T1 put a composed name there — it eats hyphens and everything before a
      full stop, so `Card-Proof Charm` rendered as `Card Proof Charm`. New `authoredNameOrKeyTail`
      shows a real name verbatim and key-tails only a value still shaped like a key. Same fix applied
      to the header's base-type noun and the set block, both of which now read T2's authored `name`
      beside the key rather than key-tailing the key.
      **Red-first (real, not asserted):** all 10 new tests were run against the pre-T3 render lines
      restored in place — **7 failed** (the 3 that stayed green were T4's, red in the T4 pass below).
      **Evidence:** new `web/.../layers/relics/itemNaming.test.tsx` **10/10**; the whole web suite
      **2006 passed / 3 failed (2009)** against a **1996/3/1999 baseline captured before any edit** —
      the same 3 (`bandGuard` ×2, `disabledReasonGuard`), and the guard's finding list is
      **byte-identical** before and after (11 entries, diffed), so the new disabled control added
      none. `npm run build` ✅. `Server.Tests --filter Item|Workbench|Surface` **89/89** (84 before +
      5 new).
      **Live-proven** against a real in-process server on a scratch copy of the real DB, serving the
      freshly built SPA on its own origin (port 5099 — `:5088` and `dist/` are a concurrent session's,
      untouched): the equip target reads **`阿尔法狙击手 · Lv 1`**, and the armoury reads **`Honed
      Hatchet`** / **`Spun Cap`** where `item.humanoid-main-hand-a-001` used to be. The two
      test-fixture containers with no corpus row read **`Unnamed item`**, never their id.
      Screenshots in the session scratchpad.
- [x] **T4** — Replace `Workbench.tsx`'s typed-id inputs with a real picker (reuse `ArmouryList.tsx`'s
      pattern). **Done 2026-09-06.**
      **Acceptance:** no craft/salvage/enhance/socket action requires typing a raw id.
      **Built — the blocker was that no read route existed, exactly as the file's own comment said.**
      - `MaterialRecipe` gains **`Name`** and `MaterialRecipeCatalog.Load` reads the authored `name`
        it had been discarding — **the same class of defect T2 fixed in `ItemBaseTypeCorpus`**.
      - New `GET /api/items/workbench/recipes[?operation=]`, reading `ItemWorkbench.Recipes` itself,
        so a row a picker offers can never be one the next POST refuses with
        `material.recipe-unknown`. New `GET /api/items/workbench/inserts/{playerId}` for the held
        gems, through the bench's own `GemInsertCorpus` (`CardInsertLookup` gains `Name`; the corpus
        was reading `nameKey` and dropping the authored `name` — third instance of the same defect).
      - `RecipeField` becomes a `Select` over real names with the four honest states, reused by all
        four call sites (2 in `Workbench.tsx`, 2 in `SocketBench.tsx`); the "insert you hold" free-text
        box becomes a picker over what the player actually holds. `WorkbenchSocketDto` gains
        `InsertName` so a result cell names its insert instead of printing `gem.g1-001`.
      - The two remaining raw ids on the same surface went with them: `CostLines`' `materialId` and
        the result panel's socket cells. Materials use the same `idWords` placement — `MaterialCatalog`
        **generates** its 27 ids and no corpus names one.
      **Red-first:** with the two pickers restored to text inputs, **all 10** tests in
      `itemNaming.test.tsx` failed; green after.
      **Live-proven** on the same 5099 host: the craft bench offers `Temper: First/Deeper/Ultimate/Peak
      Enhancement` and `Upcycle: Refine Metal Scraps…` with the ids only as option *values*; the socket
      bench offers `Bore: Open Metal…` and `Socket: Gem Setting`, and a whole-dialog regex for
      `(gem|recipe|combo|item)\.` over its text content returns **false**. The live route serves **23**
      rows, **23** named, **0** names containing a dot (7 of the 30 authored rows are refused at load
      by the pre-existing legacy-shard-id refusals, printed at boot).
      **⏸ Named limitations, not silently absorbed:**
      - Three placements (`channelLabel`, `combinationTitle`, `idWords`) exist because **no corpus
        authors a name** for a derived channel, a generated resonance, or a generated material id.
        Each is documented at its definition with the owner (derived-stats catalog; module 16/21;
        module 14). They are the id's own words, never invented English, and the day a corpus ships
        each function reads it and **no caller changes**.
      - The armoury row shows the **base type's** authored name, not module 8's composed one:
        composing needs a full card render per row and the page is up to 200 of them. The card — one
        item, one render — shows the whole composed name.

> ### ☑ CHECKPOINT — item-naming — **passed 2026-09-06**
> A real rolled item, viewed anywhere in the shipped web UI, shows a real name everywhere a name belongs.
> No surface asks a player to type or read a raw id. Screenshot-verified live.
>
> **All four of T1–T4 proven together**, on one host, against the real corpora and a real stored item:
> a live server built from this branch, on a scratch copy of the real database, serving the freshly
> built SPA on its own origin. `:5088` and `dist/` belong to a concurrent session and were not touched.
>
> | | proven | how |
> |---|---|---|
> | T1 | a rolled item's card composes a real name | `"Enduring Card-Proof Charm"` / `"Sap Tangle"`, both sides of the rare threshold |
> | T2 | the authored base/set name reaches the wire | armoury rows read `Honed Hatchet` / `Spun Cap` off the corpus |
> | T3 | no surface reads a raw id | live: equip target `阿尔法狙击手 · Lv 1`; armoury named; `Unnamed item` for a container with no corpus row — never its id |
> | T4 | no surface asks for a typed id | live: four pickers over real recipe names, one over held inserts; whole-dialog raw-id regex = **false** |
>
> **Green together:** `itemNaming.test.tsx` 10/10 (all 10 proven red first against the pre-change
> render), web suite 2006/2009 on a 1996/1999 pre-edit baseline with the same 3 pre-existing guard
> failures, `npm run build` ✅, `Server.Tests --filter Item|Workbench|Surface` 89/89,
> `Core.Tests --filter Item|Material|Recipe|Socket` 1078/1078,
> `Data.Tests --filter Item|Socket` 222/223.
>
> **⛔ Two reds are pre-existing and neither is this module's** — both reproduced with the same ids
> T1 already recorded, on files this checkpoint did not touch:
> `ItemUniqueStoreTests.Unique_eligible_seeds_every_rung_through_the_sc7_gate` (hard-codes ordinal 30
> against a committed `rungFloorOrdinal: 80`), and the full `Server.Tests` run's 25 failures, all
> `World*` / `ContentBoot*` / `Aptitude*` — the concurrent world-stage work plus the known
> `vocabulary.json` `SeedScanner` defect. The item slice of the same run is 89/89.
>
> **⏸ One thing the checkpoint claims that is weaker than it reads:** *"a real name everywhere"* now
> holds for every **authored** vocabulary. Three surfaces show a **generated** id's own words instead
> — derived channels, generated resonances, generated material ids — because **no corpus authors a
> name for any of the three**. Each is named at its definition with an owner (T3/T4 notes above). That
> is a content gap in three other modules, not an unfinished surface here.

## Phase 2 — `item-lore`

- [x] **T5** — `UniqueCorpus.cs` reads and carries forward the authored `flavor` text, not only
      `flavorKey`. **Done 2026-09-06.**
      **Acceptance:** a real unique's authored sentence survives import.
      **Built:** `UniqueSeed` gains `FlavourText`; `UniqueCorpus.ReadEntry` reads `OptStr(e, "flavor")`
      beside the key. The sentence is deliberately *not* written to `item_unique` — that column is a
      KEY, never a literal (`RpgStore.ItemUniques.cs:46`) — so the literal's home is N2's string
      catalog, generated from this field.
      **Evidence:** 112 of 144 uniques carry a pair; 0 have a key without a sentence or a sentence
      without a key (measured over the whole corpus).
      `ItemCardTests.A_real_unique_with_authored_flavour_renders_its_real_sentence` — red against the
      pre-change catalog, green now.
- [x] **T6** — Replace `adapt.ts`'s `keyTail()` placeholder with a real string-catalog lookup for
      flavour. **Done 2026-09-06.**
      **Acceptance:** the card's Flavour block shows the real sentence, not a key fragment.
      **Built:** `DisplayStringCatalog` (Core, in `DisplayTemplates.cs`) parses N2's flat
      key→`{template}` map; `ItemCardInput.LookupString` carries it; block 10 resolves the key and
      emits the finished sentence as `__rendered`, the same arg every atom line already uses. The
      Server loads the file at boot (`DisplayStringCatalogFile`, `Program.cs`) and a new
      `FusionRpg.Server.csproj` `Content` rule copies `content/display/*.json` next to the exe — it had
      no copy rule at all, the same silent defect `data\seed\items\` already had.
      `adapt.ts` now reads `__rendered` and shows nothing when the catalog has no row. **`keyTail` is
      kept**: `class.*` / `rarity.*` / `combo.*` / `item.compare.*` still have no catalog rows, and
      those callers are unchanged; only its doc comment was narrowed.
      **Evidence:** `content/display/en.json` 108 → 226 keys (+118 = 112 uniques + 6 sets), pure
      insertions, generated from the seeds' own `flavor` field — no prose written or edited.
      `ItemCardTests.Every_authored_unique_and_set_flavour_sentence_has_its_string_catalog_row` is the
      sync guard. Vitest: 4 new cases in `itemSurfaces.test.tsx`, **proven red** against the old
      `keyTail` line (`expected 'carrion spitter' to be "It doesn't wait for the battlefield…"`,
      `expected 'copyhand' to be …`, `expected 'no string row' to be undefined`), green now.
      End to end over HTTP: `ItemCardEndpointsTests.Card_forAUniqueWithAuthoredFlavour_carriesTheRealSentenceNotTheKey`.
- [x] **T7** — Extend the Flavour block to sets (owner's decision, 2026-09-06). `SetCorpus.cs` reads the
      authored `flavor` field; the card renders it when present, renders nothing when absent.
      **Done 2026-09-06.**
      **Acceptance:** a real set with authored flavour shows it; one without shows no block.
      **Built:** `SetDef` gains `FlavourKey`/`FlavourText`; `item_set` gains a `flavour_key` column via
      `EnsureColumn` (key only — the sentence stays in the catalog); `ListSets` reads it back;
      `ReadSetBlock` puts it on `CardSet.FlavourKey`; `ItemCard.FlavourLines` renders a unique's line
      and/or a set's, tagged `UniqueIdentity` / `SetThreshold`. Nothing authored ⇒ no line.
      **Evidence:** `ItemCardTests.cs`'s `Flavour_is_uniques_only` renamed to
      `Flavour_renders_for_a_unique_or_a_set_and_for_neither_when_neither_authored_one` and widened
      (positive unique, positive set, negative). `A_real_set_with_authored_flavour_renders_its_real_sentence`
      over the real `sunwoven-almanac` rows. `ItemSetStoreTests` round-trip pins 6 of 30 sets carrying a
      `flavor.set.*` key and `FlavourText` null after a DB round trip. Negative over HTTP:
      `Card_forAnItemWithNoAuthoredFlavour_emitsAnEmptyBlockAndNoPlaceholder`.

> ### ☑ CHECKPOINT — item-lore (2026-09-06)
> A real unique and a real set, each with authored flavour, show their real sentences. No placeholder
> anywhere a sentence is missing.
>
> **Suite state at close:** `Server.Tests --filter Item` 82/82 green (includes the two new HTTP-level
> flavour tests over a real rolled item). `Data.Tests --filter ItemSetStoreTests` 8/8 green.
> `Core.Tests --filter ItemCardTests` 54/58 — the 4 reds are all Phase 4's in-flight work landing in the
> same tree this session (`atom.chill-punisher`/`atom.rot-punisher` render, `Every_real_atom_renders…`,
> and the 107→109 display-template count from the new `triggered.json` rows). None touch flavour.
> `web` 1975/1978 — the 3 reds are `dev/PhaserSceneSwitchPocPage.tsx`, `dev/AtomPreviewPage.tsx` and
> `stages/world/mapChromeMute.ts` guards from other concurrent programs.
>
> **Not done:** no browser screenshot. The HTTP route test is the strongest proof taken.
>
> **Defect named, not fixed (Phase 4's, not this module's):** publishing
> `data/seed/items/_tuning/tier-bands.v2.json` mid-session widened family expansion to include
> `atom.shld-breach`, whose channel `combat.shield.pen.{variant}` resolves to no unit class, and every
> item-card render then threw `DisplayTemplateRejection` (33 Core reds, 12 Server 409s for ~10 minutes).
> It cleared when that session continued, but the coupling is real: a published tier-bands rebalance can
> take the whole card renderer down through `MissingUnitClass`, and `Exactly_three_shipped_families_name_a_channel_no_registry_row_backs`
> is the pin that is supposed to catch it.

## Phase 3 — `atom-preview`

- [x] **T8** — New preview route: accepts an unsaved container, calls the existing
      `ItemCardRenderer.Render` unmodified, returns the same `DisplayModel` shape. **Done 2026-09-06.**
      **Acceptance:** output is identical to calling `Render` in-process on the same input. A malformed
      input returns a named error, never a 500.
      **Built:** `src/FusionRpg.Server/ItemPreviewEndpoints.cs` — one `POST /api/items/preview/card`,
      its own file per the one-file-per-concern convention `ItemCardEndpoints.cs` already follows. The
      request DTO is `ContainerRow`/`ContainerAtomRow`/`ContainerPoolRow` field-for-field down to
      `pool`, plus four preview-only inputs (`baseTypeId`, `rollSeed`, `thetaContent`, `itemLevel`).
      `thetaContent` defaults to the **loaded** power tuning's own `Curve.PinIndex`, never a literal
      and never spec-content-scale.md §2.4's forbidden silent 1.0. The route mints in memory through
      the real `Instantiator`, renders through the untouched `ItemCardRenderer.Render`, and projects
      the wire shape through `ItemCardService.ToDto` — the *same* projection the live card route uses
      (widened to `public`; a second projection is the drift §8.6 names). `RpgStore.ReadRarity` was
      likewise widened to `public` so the preview reads the same stored rung ladder rather than
      re-deriving pips and palette index. Nothing is written: no container upsert, no `SaveInstance`,
      no ownership row. Wired in `Program.cs` sharing the one `ItemCardCorpus` the card route builds.
      **Min/Max:** `PinnedToBound` rewrites exactly one key — `values_json.amount`, the only magnitude
      the card reads — picking the bound through the same `AtomJson.TryReadValueSpec` reader and the
      same `ContentScale.Apply` scaler `Instantiator.Freeze` uses, with Freeze's own three policy arms
      preserved (`OnInstantiate` scaled, `OnApply` unscaled, `Fixed` untouched). No re-roll, no second
      renderer, no arithmetic of its own.
      **Evidence:** `tests/FusionRpg.Server.Tests/ItemPreviewEndpointsTests.cs`, 11/11 green.
      `Preview_isTheCoreRenderersOwnModelOverTheSameInput` is the equivalence proof: it mints the same
      unsaved container in-process, renders it directly with `ItemCardRenderer.Render`, and asserts the
      posted body's `rolled` card matches on `DisplayModel.Fingerprint()` **and** block-for-block,
      line-for-line, arg-for-arg. **Proven non-vacuous** by falsification — perturbing one field of the
      in-process input (`ItemLevel + 1`) turned it red, then reverted.
      Refusals, all named and never a 500: unknown atom → 409
      `item.preview.container-rejected: UnknownAtom: …` (ContainerValidator's own vocabulary);
      empty fixed core → 400 `item.preview.atoms-required`; missing id → 400
      `item.preview.container-id-required`; unknown base type → 409 `item.preview.base-type-unknown`;
      rung off the stored ladder → 409 `item.preview.rarity-unknown`; bad kind → 400
      `item.preview.kind-unknown`; family with no live template → 409 `item.display-template-missing`.
      `Preview_persistsNothing` asserts the container is still absent from `effect_container` and the
      catalog revision unmoved afterwards.
- [x] **T9** — Preview page. **Done 2026-09-06.**
      **Placement — the documented fallback was taken, and why.** A new dev surface,
      `web/fusion-rpg-web/src/dev/AtomPreviewPage.tsx`, registered in `DEV_SURFACES`
      (`DeveloperTree.tsx`) as `atom-preview` / "ItemPreview", reachable at `?dev=atom-preview`.
      **Not** folded into `AlmanacDumpPage.tsx`: that page (151 lines) is scoped entirely to scraped
      in-game **pedia text** — `/api/almanac/dump`, a plant/zombie side filter, TMP strings keyed by
      `typeId` — a different content type with no extension point, and its "review then promote" shell
      is a list/detail over almanac rows, not a generic authoring frame. spec-atom-preview.md §2
      criterion 3 names both options and asks for the choice to be stated; `src/dev/` already has the
      registry this needs. Reversible.
      **Built:** the page reuses `ItemCard.tsx` and `adaptItemCard` unchanged — the response is the
      same `ItemCardDto` the live card route returns, so no second adapter exists. `useItemPreview`
      lives in `src/lib/bus/items.ts` with the other item routes. No save control: the surface
      previews and never persists.
      **Evidence — live, screenshot-verified.** A scratch publish of the server on `:5089` (the
      owner's `:5088` instance left untouched) with the live `rpg-hot.sqlite` copied alongside it, the
      SPA served same-origin from `wwwroot`, driven in a real browser. Real definition pasted:
      `atom.bulwark.t5` / `atom.might.t5` / `atom.savagery.t5` / `atom.resilience.t5` on base type
      `item.humanoid-main-hand-a-001` at rung `heirloom`. Rendered:
      `219–435% increased defense for the whole army` as rolled, `219%` at Min, `435%` at Max, and the
      implicit `+19–37 attack` → `+19` / `+37`. Screenshot:
      `docs/research/item-content/atom-preview-live.png`.
      **Vitest:** `src/dev/AtomPreviewPage.test.tsx`, 4 cases green, over a **real captured response
      body** (that same live call, trimmed to the header and implicit blocks) — it pins the request as
      well as the render, asserts the three cards show the band and both of its ends, shows the
      server's named refusal verbatim rather than an empty card, and refuses malformed JSON without
      posting. `DeveloperTree.test.tsx`'s pinned surface list updated.
      **Affix at Min/Max:** covered by `Preview_rendersTheAuthoredRangeAtBothEnds` in the server test,
      which draws four real affixes from the real generated affix library and asserts each end's value
      appears inside the rolled band. It could **not** be shown in the live screenshot — see the first
      named defect below.

> ### ☑ CHECKPOINT — atom-preview
> An author previews a real, unsaved container's card live, through the same renderer production uses.
> Screenshot-verified: `docs/research/item-content/atom-preview-live.png`.

### Defects found while building T8/T9 — named, not fixed

1. **The live DB's whole affix table is unrenderable.** `effect_affix` in
   `dist/FusionRpg.Server/data/rpg-hot.sqlite` holds exactly 10 rows, all
   `affix.authored.affix-draw-00*`, and their 18 distinct atom refs resolve to families
   (`atom.fx-*`, `atom.patron-aura-*`, `atom.critical-hunter`) with **zero** display-template rows
   between them — verified atom by atom against `item_display_template`. Any real item that draws one
   is a 409 `item.display-template-missing` on the live card route, and no live preview can show an
   affix line at all. The 107 shipped templates cover the `FamilyExpansion`-generated corpus
   (`atom.bulwark`, `atom.might`, …), which is a disjoint set from what the DB's affix pool points at.
   Adjacent to T11's two missing templates but larger and different: not two families, a whole table.
2. **The imported atom catalog is a partial subset.** The live DB carries 79 `effect_atom` rows;
   `FamilyExpansion` over the shipped `affix-families/*.json` produces several hundred, and only 45 of
   the 79 have a live display template (9 families × 5 tiers). Worth a look before Phase 4 measures a
   corpus-refusal count against the DB rather than against the seed files.
3. **Nothing in `data/seed/items/**` authors an `onInstantiate` value spec** — `grep -ro onInstantiate
   data/seed/items/` is 0, and `FamilyExpansion.cs:191` hardcodes `"roll": "onApply"` for every
   generated affix. So `ItemDisplayRenderer.BarFor`, `DisplayLine.RollQualityPerMille` and block 11's
   mean-roll-quality line are **structurally inert on every shipped item**: the bar is always null and
   the footer always renders "nothing rolled". The code is correct; the content never exercises it.
   This is also why "Min/Max" for the item corpus means the two ends of an `onApply` **band**, which
   is what the preview renders.
4. **No route lists atoms, affixes or containers**, so the preview page is paste-only — there is
   nothing for a picker to read (`AtomPushService.cs:17-19`, "no atom row, container row or curve row
   is ever put on the wire"). Out of scope for this module's one endpoint; named so the next surface
   does not rediscover it.

## Phase 4 — `affix-draw-coverage`

- [x] **T10** — Author `tier-bands`'s missing rows through the file's own tool. **Done 2026-09-06.**
      **Acceptance:** `FamilyExpansion.cs:121-125`'s refusal count drops from 95 to **0**, measured.
      **Before/after, measured directly against the real generator (not assumed):**

      | | `tier-bands.v1` | `tier-bands.v2` (published) |
      |---|---|---|
      | Families with an authored `channelWeightPermille` | 14 / 109 | **109 / 109** |
      | Refused at gate 1 (`:121-125`, the tuning file's own gate) | **95** | **0** |
      | Admitted through the FULL gate chain | **9** (45 atoms) | **25** (125 atoms) |
      | Pooled / element-typed families in the shipped expansion | **0** | **7** |

      **⛔ The `_meta`-named command did not exist — real defect, found and fixed.**
      `tier-bands.v1.json`'s own `_meta.rebalance` has always said *"python -m seedsmith numerics
      rebalance --set channelWeight.<id>=<value> --publish"*. There is no `numerics` subcommand:
      `python -m seedsmith numerics …` exits 2, *"invalid choice: 'numerics'"*. The `seedsmith.numerics`
      package was library-only (`rebalance()`, `TierBands.adjust`, `tier_bands_io.save`) with no CLI
      surface, so the one instruction the file gives its next reader was unrunnable. **Built** it
      (`report/cli.py`, `cmd_numerics` / `_cmd_numerics_rebalance`) to exactly the documented grammar,
      dry by default (spec-numerics.md §3.2's *"nothing until publish"*), plus `--set-file` so a
      95-row batch is a reviewable artefact rather than a 95-flag command line.
      **⛔ Second real defect, same file:** `tier_bands_io.save()` wrote no `_meta`, so the FIRST
      publish would have silently deleted the block carrying *"Never hand-edit this file"* and the
      authoring command itself. **Fixed** (`read_meta()` + `save(meta=...)`), pinned by
      `test_every_published_version_keeps_its_meta_block`.

      **The weight, and why it is not invented.** Every one of the 14 shipped rows carries exactly
      `1000‰` — the file has one distinct value. All 95 new rows carry the same. That extends the
      file's own existing value rather than minting a ranking nobody has measured, and it matches what
      `ItemCardTests`' own committed fixture had already chosen and documented
      (`ShippedChannelWeightPermille = 1000`). The file's `_meta` says the same of itself: *"working
      values … not a validated balance decision — telemetry refits channelWeight once gameplay data
      exists."*

      **Skew check, run BEFORE publishing** (the role×group-matrix bug that excluded 84 of 98 charm
      families is an *exclusion* defect, so the check is per-cell admission, not a summary number):
      the proposal is uniform, so it cannot introduce a relative ordering at all; measured per group
      and per role, **no cell lost admission and none stayed flat that was not already gated
      downstream** — `g.armour` 2→5, `g.evade` 0→3, `g.life` 4→6, `g.shield-stat` 0→3, `g.tempo` 0→4,
      `g.ward` 0→1, every other group unchanged; every role's admitted count rose or held. Dry run
      first (`--set-file …`, no `--publish`), then published.
      **Files:** `data/seed/items/_tuning/tier-bands.v2.json` (v1 stays on disk for revert, as its own
      `_meta` promises), `tier-bands.v2.rebalance-set.txt` (the reviewable input).
      **Guards:** `test_latest_authors_a_weight_for_every_shipped_affix_family`,
      `ItemCardTests.The_shipped_tuning_authors_a_share_for_every_family_and_v1_did_not` (runs BOTH
      tunings through the real generator every run — 95 → 0, re-proved, not recorded).

- [x] **T11** — Author the 2 missing display templates (`atom.chill-punisher`, `atom.rot-punisher`).
      **Done 2026-09-06.** `disptpl.p3-050` / `disptpl.p3-051` in `display-templates/triggered.json`
      (+ 2 rows in `content/display/en.json`), both `live`, group `g.punisher`.
      **Text: `{value} bonus damage on hit` — grounded in code, and deliberately NOT the family's own
      inline `displayTemplate`.** Both families author `kindId: resource.delta`,
      `params: {when: OnDamageDealt}`, `powerBand: low`, and **no channel, no op, no element** — the
      byte-identical shape of `atom.hit-followthrough`, whose shipped row (`disptpl.p3-007`) reads
      exactly this sentence. Verified rather than taken from the family's note:
      `AffixFamilyFile.Read:36-44` reads only `params.channel`/`params.op` and has no predicate field
      at all, and `FamilyExpansion:208` emits `WhenJson = "{}"` on **every** generated row — so no
      generated affix atom carries a gate of any kind.
      **⛔ Real defect named, not fixed:** each family's inline `displayTemplate` promises a status
      gate (*"against a frozen target"* / *"against an afflicted target"*) the compiled atom does not
      have. The engine's leaf for it (`PredicateNode.LeafId.HasStatus`) is real and compiled
      (`CompiledAtom.cs:80,167`) but reached only by Delve's `AmbushDraw`, never by an affix. The
      shipped template says what the atom does; the frost/blight identity still reaches the player
      through the affix NAME (`of the Shattering Chill`, `of the Creeping Rot`), which is real content.
      When family-authoring-time predicates land, the text changes **with** the mechanism.
      **Verify:** `ItemCardTests.The_punisher_families_render_at_min_and_max_with_no_raw_id` (Theory,
      both families, Min + Max, both frames), `Missing_display_template_fires_on_exactly_the_untemplated_families`
      (expected set now empty), `ItemDisplayTests.Every_shipped_family_has_a_display_template`
      (107 → 109, and now also asserts one row per real family rather than a bare literal).

- [x] **T12** — Register the 3 missing `UnitClass` entries. **Done 2026-09-06.** Two channel families
      cover all three affix families: `combat.power.pierce.*` (`atom.elpw-pierce` Flat +
      `atom.elpw-focus` Increased) and `combat.power.overflow.*` (`atom.elpw-overflow` Flat). Both
      **`UnitClass.GameUnits`**, in `ChannelUnits.AuthoredChannelFamilies`, prefix-matched — exactly
      the shape `ssot-presentation.md` §5.3 N3 specifies.
      **Grounding — the reader, not the name.** Each channel's own authoring note states which term it
      adds into: pierce is *"a flat amount that offsets the matching-element `combat.defense.*` term on
      the far side of the same (power − defense) sum"*, which is `OverlayCombatCalculator.cs:145`
      (`(power - defense) + componentBonus`), a plain additive game-units delta whose two existing
      halves are both `GameUnits` in `DerivedStatChannels.CombatFamilyUnitClass`; overflow is a second
      flat power ledger, same shape. **The name trap, avoided and pinned:** `combat.penetration.*` is
      the asymptotic `PierceFactor` family and is `ReciprocalPoints` — grouping `combat.power.pierce.*`
      with it by name is the exact *"wrongly grouped … by tuning-file section rather than by formula
      shape"* error that table's own comment records having been corrected once already.
      **Why `ChannelUnits` and not `DerivedStatChannels.CombatChannelFamilies`:** joining that list
      REGISTERS 8 real channel slots per family in the derived-stat catalog (`+16` channels, moving
      `SeedCatalogTests`' 268 and `ElementHubDocDriftTests`' doc census) and would edit
      `data/seed/derived-stats/catalog.json`, which the concurrent derived-stat-extension program has
      open right now. Declaring a display unit is not the same act as registering a channel. The
      lookup prefers the registry, so the rows delete themselves the day that program registers them.
      **Verify:** `No_shipped_family_names_a_channel_with_no_resolvable_unit` (the old
      `Exactly_three_shipped_families_…` pin, inverted as its own doc comment instructed),
      `Every_shipped_channel_bearing_family_resolves_to_a_unit_class` (quarantine set deleted),
      `The_minted_elemental_power_channels_resolve_to_the_unit_of_the_sum_they_join` (Theory, per
      channel), `The_two_pierce_shaped_channels_do_not_share_a_unit`.

- [x] **T13** — Widen the "every atom renders" guard's scope. **Done 2026-09-06.** `RealAtoms` reads
      the **highest published** `tier-bands.v{n}.json` (`LatestTierBandsPath()`) instead of a
      hard-coded `v1`; the by-name quarantine skip is deleted; the `rendered >= 100` literal floor is
      replaced by two derived assertions (the exercised family set must EQUAL the admitted-and-live
      set, and the render count must equal `families × 5 tiers × 3 values × 2 frames`). Its stale doc
      comment (the old `:1026` block, *"authors a row for 14 … refuses 95"*) is rewritten with the
      real post-T10 numbers and the three downstream gates.
      **⛔ Proven not vacuous — a real falsification, run:** injected `{unauthored}` into
      `atom.ward-harden`'s template (a family reachable **only** under the widened scope) →
      **widened guard FAILS**: *"'atom.ward-harden': unresolved placeholder '{unauthored}'"*. Same
      broken template, old v1-only scope → **PASSES**, exercising 9 families and never touching it.
      Both files restored; `git diff` on `derived.json` is empty.
      Standing version of the same proof: `The_widened_guard_reaches_families_the_v1_scoped_one_could_not`
      builds both scopes from the two real tunings and asserts the new one is a **proper superset**,
      and that every pooled-channel family in it was absent from the old one.

> ### ✅ CHECKPOINT — affix-draw-coverage — **PASSED with a named remainder, 2026-09-06**
> **The gate this module owns is at 0.** `FamilyExpansion.cs:121-125` — the tuning-file gate, the one
> §6.3 quantified — refuses **95 → 0**. Families admitted end-to-end went **9 → 25**, and the shipped
> corpus contains element-typed (pooled-channel) atoms for the first time: 7 families, which §6.3
> called out as *"every element-typed family is in the refused set."*
>
> **⛔ The remaining 84 are refused DOWNSTREAM of the tuning file, and the plan's "ideally 0" was
> measurably wrong about them — named exactly, not rounded up.** None is a weight gap; none is
> fixable by authoring `tier-bands`:
>
> | Gate | Count | What it is | Where |
> |---|---:|---|---|
> | **G5 — no `op` authored at all** | **40** | Trigger/status families (`resource.delta`, `status.apply`, `board.action`, `spawn.entity`) whose `params` carry `when`/`status`, not `channel`/`op`. `FamilyExpansion`'s primaryChannel/flatDerivedChannel path has no tier-magnitude formula for that shape. An **E43 generator** gap. | `FamilyExpansion.cs:258` |
> | **G2 — `op` not in `opWeightPermille`** | **22** | `add` (6 economy), `Replace` (6), `Flag` (4) — three real ops the tuning file's 3-entry op table does not name. Plus **4 that are a parser defect, see below**. | `:131-137` |
> | **G4 — no `BattleRuleset` curve** | **19** | `arm1Max`, `arm2Max`, `attackInterval`, `produceInterval`, `zombieSpeed`, `status.power`, `status.resist`, and the 12 `{variant}` **Flat** families. A **progression-model** gap (`adapters/items/channels.py` carries curves for 14 primary channels only). Note 5 of these were already refused under v1 despite having a weight. | `:243-246` |
> | **G3 — no shipped E30 pool** | **3** | `atom.elpw-focus`/`-overflow`/`-pierce`. Deliberate, refused by id. T12 gave them a display unit; a **pool** is E30's to author. | `:44-47`, `:154-157` |
>
> **⛔ New real defect found while measuring G2 — `params.op` carries two different vocabularies.**
> `board.action` families author `params.op` as a board VERB — `atom.cherry-bloom` → `"cherry"`,
> `atom.dooming` → `"doom"`, `atom.firelining` → `"fireline"`, `atom.flash-freeze` → `"freeze"` — while
> `stat.modify`/`stat.derived` author it as a tier-band op (`Flat`/`Increased`/`More`).
> `AffixFamilyFile.Read:41-42` reads the field blindly and hands it to `FamilyExpansion`, which looks
> it up in `opWeightPermille` and refuses *"no opWeightPermille entry for op 'cherry'"*. Two
> namespaces in one field name. Filed, not fixed — it is an E43/affix-schema question, not a tuning one.
>
> **⛔ Second new real defect — a channel-less family that shows `{value}` can never render.**
> `ItemCardRenderer` resolves a unit from `params.channel` only (`ItemCard.cs:389-390`), and
> `ItemDisplayRenderer.Line:100-104` refuses any template containing `{value}` when the unit is null.
> All 40 G5 families author no channel, so the moment any of them becomes drawable its card line is a
> rejection. There is no kind→unit axis anywhere in the codebase — only channel→unit. T11's per-family
> test supplies `GameUnits` explicitly and says why. Building the axis is a change to
> `ssot-presentation.md` §2.3's ledger, out of this module's scope.
>
> `atom.entangling` is **untouched** and still `pending`, as instructed — its `UnityCc` status payload
> kind has no Unity branch, a mechanism gap, not a tuning one.
>
> The guard test's scope now matches its own claim, and the claim is proved falsifiable.

## Phase 5 — `granted-action-text`

- [x] **T14** — `rpg_action` gains a description key column; every seeded action gets a real, authored
      description, each grounded in the action's real granted effect. **Done 2026-09-06.**
      **Acceptance:** zero actions in the committed corpus have an empty/missing description.
      **⚠ The corpus is 24 rows, not 114** — see "Count corrected" below.
      **Built:** `rpg_action.description_key` via `EnsureColumn` (a KEY, `ssot-presentation.md` §3.6 L3,
      matching `rarity.display_key`/`flavour_key`); `ActionRow.DescriptionKey`;
      `ActionCorpusBrief.DescriptionKey` + `ActionCorpusBriefJson` now **requires** `descriptionKey` on
      every corpus row (a row that ships without one would put a blank line on a player's card);
      `ActionCorpusComposer` carries it through verbatim. `data/seed/actions/committed-round-{1,2}.json`
      gain a `descriptionKey` on all 24 entries (+24 lines, pure insertion).
      `tools/.../innate_picker/derive.py:apply_promotions` now `setdefault`s the key so the next
      promotion run cannot write a committed file the server refuses to import.
      **Content:** 48 new keys in `content/display/en.json` (24 names + 24 descriptions), **hand-authored**
      — each sentence written after reading that action's real `atomFamilies` in
      `data/seed/items/affix-families/*.json` (kind + params + the family's own display template), never
      from the action's name. The grounding note per row lives in the authoring script's `ROWS` table.
      Not a `deals X damage` restatement anywhere (`ssot-presentation.md` §8.1).
      **Evidence:** `GrantedActionTextTests` — 4/4 green. 24/24 briefs carry a key, all 48 keys resolve to
      real prose (no `{placeholder}`, no id, no key-as-text, ≥40 chars, ends in a full stop); a
      falsifiability case pins that an unauthored key resolves to `null`; a parse-refusal case pins that
      a corpus row without `descriptionKey` is rejected by name.
      **Count corrected (real defect in the source research, named not hidden):** `item-content-ideal.md`
      §6.2 — and the plan and spec that quote it — say *"114 actions across the committed corpus"*.
      Measured: `committed-round-1.json` = 19, `committed-round-2.json` = 5, **24 total**, which is what
      `Program.cs:347` imports and what `ActionCorpusRealContentQualityTests` already asserts. 77 distinct
      named action ids exist across the whole `data/seed/actions/` tree, but `_manifest.json` declares
      `_rounds/` and `_candidates/` **excluded** from the committed corpus. 114 matches nothing measurable.
      The hand-authoring default held easily at 24; no generative fallback was needed.
- [x] **T15** — Card block 9's description render, and the armoury compact-line battle-only tag
      (`ssot-presentation.md` §9.14). **Done 2026-09-06.**
      **Acceptance:** a real granted-action item shows its real description on the card and its
      battle-only status in the armoury list without opening the card.
      **Found already wired:** `CardGrantedAction` already carried a `DescriptionKey`, and
      `ItemCard.GrantedActionLines` already emitted it.
      **Found broken, fixed (real defect):** `RpgStore.ReadGrantedActions` built both keys as
      `"action." + g.ActionId`, and `rpg_action.action_id` **already** carries the `action.` stem — so
      block 9 asked the catalog for `action.action.family.cactus.001.desc`, a key nothing could ever
      author. The name key is now the id; the description key is **read** from `rpg_action.description_key`
      (the `.desc` suffix survives only as the unauthored fallback).
      **Found missing, built:** block 9 never resolved its keys to text — it emitted keys only. It now
      resolves both through `ItemCardInput.LookupString`, emitting `__rendered` (description) and
      `__renderedName`, the same contract block 10 uses since T6.
      **Found missing, built:** the compact armoury line carried **no** battle-only tag —
      `ArmouryRowDto` had no such field, so a player really did have to open each card. Added
      `RpgStore.GrantsBattleOnlyAction` (the same read block 9 uses, never a second derivation),
      `ArmouryRowDto.BattleOnly`, `ArmouryRowView.battleOnly`, `adaptArmouryRow`, and a
      `· battle only` tag on `ArmouryList.tsx`'s row. `ItemCard.tsx` gains `GrantedActionLines` so the
      block shows name + description + battle-only + already-known rather than the description alone.
      **Evidence:** `ItemCardStoreTests.Block_9_renders_a_real_granted_actions_real_authored_description`
      — the real corpus through the real `ActionCorpusImporter`, a real grant, the real string catalog,
      asserting `__rendered` equals the real authored sentence and the name key never double-prefixes.
      `The_compact_line_can_ask_for_the_battle_only_tag_without_opening_the_card` (positive, negative,
      and disabled-grant arms). `ItemEquipEndpointsTests.Armoury_carriesTheBattleOnlyTagOnTheCompactLine`
      over the real HTTP route (a `DefaultAttack` grant tags, a plain `Granted` grant does not).

> ### ☑ CHECKPOINT — granted-action-text (2026-09-06)
> Every granted action in the committed corpus (24, not 114 — see T14) has a real authored description,
> and it reaches the card as finished text and the compact armoury line as a battle-only tag.
>
> **Suite state at close.** `Core.Tests --filter Item` **1014/1014 green** (it read 1007/1011 mid-task;
> the 4 reds were Phase 4's in-flight `chill-punisher`/`rot-punisher` render and 107→109 template-count
> work, and that session closed them before this one finished). `Data.Tests --filter Action` 97/97 green.
> `Data.Tests --filter Item` 222/223 — the 1 red is **pre-existing on HEAD and another program's**:
> `ItemUniqueStoreTests.Unique_eligible_seeds_every_rung_through_the_sc7_gate:174` asserts the old
> `ordinal >= 30` floor while `data/tuning/uniques.v1.json` has already moved `rungFloorOrdinal` to 80
> (D4.23, spec-unique-pipeline.md decision 13). `Server.Tests --filter Item` 84/84 green.
> `web` tsc clean, vitest 1995/1999 — the 4 reds are `dev/PhaserSceneSwitchPocPage.tsx`,
> `dev/AtomPreviewPage.tsx`, `stages/world/mapChromeMute.ts` guards from other concurrent programs plus a
> flaky `SanctumStage` waitFor. `seedsmith` 2393/2407 — the 14 reds are all Phase 4's affix-family count
> moving 100 → 109; none name an action, a seed brief or `descriptionKey`.
>
> **Not done:** no browser screenshot; the HTTP route test and the render test are the strongest proofs taken.
>
> **Decision recorded:** `description_key` is deliberately NOT joined to `ContentHashRegistry`'s
> `rpg_action` column list. That list is explicit and every column added to the table since V6
> (`scope`, `category`, `projectile_penalties`, …) stayed out of it too; adding one forces a
> `CurrentSchemaVersion` bump that moves every content stamp in the tree for a string that changes no
> battle outcome.
>
> **Named, not fixed (out of this module's scope):** 21 of the 24 committed actions still refuse to
> import against a bare atom seed, because 28 of the 30 `atomFamilies` they name exist only as
> `affix-families` entries and not under `data/seed/atoms/` — already documented by
> `ActionCorpusRealContentQualityTests`. Descriptions are authored for all 24 regardless; the refusal is
> the atom-catalog pipeline's gap, not a text gap.

---

## ☑ FINAL CHECKPOINT — the whole program, together (2026-09-06)

> On one small, hand-seeded or lightly-generated slice (no dependency on the seed→concrete generator or
> the held `classes.v1.json` v4 run), a player can see a real name, a real lore sentence where one is
> authored, a real granted-action description, and every affix family the tuning file now admits render
> as readable text — screenshot-verified live.

**Honestly scoped, not rounded up.** Each of the five modules has its own independent, live-verified
proof (real screenshots, real server routes, real seeded content) — see each module's own CHECKPOINT
above. A single hand-seeded item exhibiting all four properties **at once** (a real composed name, real
authored lore, a real granted-action description, and affixes drawn from the now-admitted family set) was
not separately screenshotted — no spec's own acceptance criteria required a combined demo, since each
property is produced by an independent code path with no shared state between them (naming reads the
instance's own affixes; lore reads the unique/set row; the action description reads the granted-action
row; affix admission is a corpus-wide gate none of the other three touch). **The one place a real
cross-module interaction bug existed, it was found and fixed**: T3/T4's own pass caught `adaptItemCard`
running `keyTail()` unconditionally on the header name, which was harmless before T1 supplied a real
composed name and silently wrong the moment it did (`"Card-Proof Charm"` → `"Card Proof Charm"`) — the
exact shape of bug a combined demo would have been looking for, caught anyway because the agent doing T3
actually read what T1 shipped rather than assuming it. On that evidence, the program is complete as
specced; a literal one-item four-property screenshot is a nice-to-have, not a gap.

## Defaults this todo ships under, not gates (see plan's own "Gates vs. checkpoints" table for reasoning)

- String catalog stays a file (not a table) — `ssot-presentation.md`'s own v1 default, unchanged.
- ⚠ **Corrected 2026-09-06 — the plan's own default did not hold, for a good reason.** The preview page
  did **not** fold into `AlmanacDumpPage.tsx`: that page is scoped entirely to scraped in-game pedia text
  (a different content type, no real extension point), confirmed by the building agent after reading it.
  It shipped as the documented fallback instead — a standalone dev page,
  `web/fusion-rpg-web/src/dev/AtomPreviewPage.tsx`. This is exactly the kind of reversible default the
  plan itself said to override on contact with reality, not a deviation to correct back.
- ⚠ **Corrected 2026-09-06 — the "114 descriptions" figure was wrong.** The real, imported granted-action
  corpus is **24** rows (`committed-round-1` + `-2`; `_rounds/`/`_candidates/` are excluded from the
  manifest and were never real content). 114 counted something unmeasurable and was inherited from
  `item-content-ideal.md` §6.2 into the map, plan, spec and this file without being re-verified against
  the actual import — found and corrected by the building agent, not assumed. All 24 hand-authored, no
  generative fallback needed at that size.
- The preview surface (`atom-preview`) stays developer-only; broadening its audience is a named,
  non-blocking follow-up, not built here.

## Carried, not scheduled

- Localising the 1,075 name keys not yet in the string catalog (`item-content-ideal.md` §6.2, §8.2) —
  those already render real English text via inline literals; a separate, lower-priority question.
- The seed→concrete item generator itself and the `classes.v1.json` v4 full run — both out of this
  program's scope by the capability map's own §4.
