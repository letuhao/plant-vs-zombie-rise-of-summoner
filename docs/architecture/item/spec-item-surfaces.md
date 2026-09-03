# Spec: `item-surfaces`

**Module id:** `item-surfaces` · **Program:** [item](../item-map.md) · **Build order:** 20 of 21
**Depends on:** `armoury` (2), `item-card` (10), `sockets` (16)
**Added:** 2026-09-03, by audit — **of nineteen modules, none produced anything a player sees**
**Rulings:** D5, D18/§2f.2 (the `40/day` line), **D20** (127 combinations), D21, D22, D26 ·
lanes [G3 `ssot-presentation.md`](ssot-presentation.md), [I13 `ssot-inventory.md`](ssot-inventory.md),
[I4 `ssot-sockets.md`](ssot-sockets.md) · governed by
[game-gui-principles.md](../game-gui-principles.md) and
[information-architecture.md](../../design/information-architecture.md)

## Objective

Everything a player sees. Six surfaces:

| Surface | What it is |
|---|---|
| **Armoury list + filter** | the held items, sortable, filterable, with the inbox and the loot filter |
| **The equip screen** | the paperdoll: 15 roles (12 hybrid) against one specimen |
| **Item-card render** | module 10 produces the `DisplayModel`; **this renders it** |
| **Comparison** | equipped-versus-candidate, the default presentation wherever a choice is made |
| **Socket preview** | what the current fill produces, and what is one insert **or one swap** away |
| **Combination compendium** | the 127 combinations, revealed as earned |

**This module renders. It computes nothing.** Every magnitude comes from module 10; every distance comes
from module 16's evaluator; every comparison verdict comes from I13's payload.

## Design

### ⛔ GG-1 first: there is no new route, and the home already exists and is already built

**GG-1 — a game is a stage with layers, not a document with pages.** *"Replacing the stage is reserved
for genuinely leaving the session"*; *"Routing to a sibling screen in order to look at something"* is
forbidden (`game-gui-principles.md` §GG-1).

The item surfaces' home is already declared and already exists:

| | |
|---|---|
| **Layer** | **Relics** — band 2, key `R`, `#/<stage>?panel=relics` (`information-architecture.md` §3, §4, §8) |
| **Unlocks on** | *first container acquired* (§7) — so it renders from **state**, never a constant list (GG-44) |
| **Built today** | `web/fusion-rpg-web/src/layers/relics/RelicsLayer.tsx` — a `PanelShell` with three tabs (`held` / `equipped` / `storage`), fed by `useRelics()` → `/api/relics` (`lib/bus/queries.ts:357-360`, `RelicEndpoints.cs:15`) over the four-item `RelicCatalog`, equipping through the `rpg_unique_equipment` pipeline |
| **Honest today** | the `storage` tab renders an `EmptyState` saying storage is not tracked yet — a designed state, not a fake |

> **This module replaces that layer's *body*. It adds no route, no stage, no fourth top-level surface,
> and no second entry point.** Comparison, the compendium and the socket bench are sub-views **inside**
> the Relics panel and inside band-3 dialogs — never siblings of it.

**GG-10 caps depth at three pushes.** The budget: Relics (1) → item detail (2) → socket bench or
compendium (3). Nothing goes deeper; if it wants to, the information architecture is wrong.

### ⭐ The boundary with the web program — both maps pointed at each other; this ends it

Three documents, three positions, one of them a direct claim collision:

| Document | Says |
|---|---|
| `ssot-presentation.md` §1 | cedes *"UI layout, component code, CSS, routing"* to the web spec |
| `docs/web/spec.md:137-144` | *"`item/ssot-presentation.md` is the presentation contract for every number a player reads … **That seam is unclaimed from this side.**"* |
| ⛔ `docs/web/spec.md:399` | success criterion 7 **claims** *"the item card's eleven blocks"* as web-program work |

**The cut, and it is a cut both sides can hold:**

| The web program owns | This module owns |
|---|---|
| The **shell**: `PanelShell` / `DialogShell` / `Toast`, `layerStack.ts`, `Rail.tsx`, `bandGuard`, `keymap` | the **contents** of the Relics panel and of the item dialogs |
| The **kit**: `Button` `Select` `Meter` `Delta` `Tag` `Frame` `Tooltip` `EmptyState` and the four required states | the item card, the paperdoll, the comparison view, the socket bench, the compendium — composed **from** the kit |
| Tokens, motion M1–M9, i18n plumbing (Lingui), `formatMagnitude` and its guard (`i18n/magnitude.ts`, `i18n/magnitudeGuard.ts`) | which token, which transition, which key — never a raw hex, never a bare number |
| Routing and the URL grammar | the `?panel=relics&tab=…&sel=…&cmp=…` parameters this layer contributes |

⭐ **And the contract type already exists, with nine of eleven blocks stubbed out waiting for exactly
this module.** `ContainerView` (`web/fusion-rpg-web/src/contract/types.ts:135-149`) has the eleven
blocks and `Pending<T>` states; `sockets` and `set` are typed `unknown` with a
`// spec-sockets-and-sets.md` comment; and `adaptRelic` (`contract/adapt.ts:124-144`) returns `absent()`
for requirements, affixes, enhancement, sockets, set, granted action and footer, with
`pendingWithReason(PLAYER_PENDING.relicImplicit)` for the implicit. **Filling those is this module's
build**, and the `Pending` shape means every unfilled block already renders an honest state rather than
a blank.

⛔ **`docs/web/spec.md`'s success criterion 7 must be amended** to say the item card's eleven blocks are
delivered by item module 20 against the web kit — otherwise two programs both believe they own it and
one of them will discover it late. That is a doc change, and it is the whole point of this module
existing.

### ⛔ The compendium and the socket preview are requirements, not niceties

D20 replaced *≤ 20 hand-authored words* with **102 generated Strains and Splices**. Against
`ssot-sockets.md` §4.4's 25 generated resonances that is **127 combinations**.

§4.4's own learnability argument was: *"Twenty-five generated containers plus ≤ 20 words is a
combination catalog of ~45. That is a size a player can learn. Four hundred would not be."* **127 is past
that bar**, and §8.2's wiki-dependency failure — *"D2's runeword list was, in practice, an out-of-game
resource"* — is live again. D20 says so explicitly: *"That mitigation moves from a nicety to a
requirement."*

The mitigation is §4.4's and §8.2's own, and it is two things:

1. **Reveal a recipe in the compendium once the player has held every ingredient at least once.**
   *"The list is content the game gives you, not knowledge you import."*
2. **The socket bench previews what the current fill produces and what is one insert away** —
   *"a design requirement of this lane, not a nicety: without it the resonance layer is invisible and
   reverts to being a stat tax."*

And §8.2's residual risk names the hard part: ordering. *"The preview must state the required order
explicitly, and the 'one insert away' hint must include 'one **swap** away'."*

### ⭐ How the swap hint stays tractable — decide by multiset, then count cycles

**The naive shape is the trap.** A Strain or Splice is an *ordered* recipe of **4 ingredients** (D20,
amended §2f.2 — the count was unstated before and is now fixed at 4, matching `socket_max`). Asking
*"which permutation of my fill satisfies this recipe"* is 4! = **24** arrangements per candidate recipe,
× 127 recipes = **3,048 evaluations per card**, recomputed on every insert. That is the version to
refuse.

**Never enumerate a permutation. Split the question in two, and the second half only ever runs on an
exact multiset match:**

```text
For a fill F (the ordered contents of the item's sockets, empties included)
and a recipe R (an ordered sequence of ingredients, |R| <= 4):

  missing = multiset(R) - multiset(F)          -- what R needs that F does not hold AT ALL

  if |missing| > 0:
      distance = |missing|,  kind = INSERT      -- ordering is IRRELEVANT here, and that is the
                                                   whole saving: an insert-away recipe is decided
                                                   without ever looking at order
  else:                                         -- F holds exactly R's ingredients
      if F == R as a sequence:  ACTIVE
      else:  distance = n - cycles(sigma),  kind = SWAP
             where sigma is the permutation mapping F's positions onto R's, and
             (n - cycles) is the MINIMUM number of transpositions that sorts it.
```

**Why this is exact and not an approximation.** The minimum number of transpositions needed to realise a
permutation of *n* elements is exactly `n − c`, where `c` is its cycle count — a standard result, not a
heuristic. So the swap hint is the true minimum, computed in one pass.

**Cost per card:**

| Step | Work |
|---|---|
| Prune the candidate set | a recipe is a candidate only if `item.sockets ≥ |R|` (G3 §4.3's `∞` rule — *"an unreachable combination is `undiscovered`, never `one-away`"*) and, for a Strain/Splice, the base is low-rarity and non-set (**D21**) |
| Multiset difference | O(k), k ≤ 4 — a 6-element element histogram subtraction |
| Cycle count | O(k), **and only in the `|missing| == 0` branch**, which is rare |
| **Total** | ≤ 127 × O(4) ≈ **500 integer ops**, no allocation, no permutation |

Two more properties that keep it honest:

- ⛔ **It is the same evaluator.** G3 §4.3: *"near-miss is computed by the same pure evaluator that
  computes the active set, called once with a distance parameter. Never a second function."* I4 already
  specifies evaluation as *"a pure, ordered function of `(socket contents, socket affinities,
  catalog_revision)`"* — this extends its return with `(distance, kind)`. **Two functions is precisely
  how *"the tooltip said one more and it did not fire"* happens.**
- **`distance` for a Pure-k resonance is `max(0, k − count(e))`, and `∞` if the item lacks k sockets.**
  Resonances are unordered, so they never reach the swap branch at all.

**And the display cap does the rest.** Never render 127. Render **active**, then **one-away** (insert or
swap, each naming its exact remedy), then **known-inactive by name only**, then nothing —
`ssot-presentation.md` §4.3's four closed states, which is GG-26 progressive disclosure applied to a
catalog that cannot fit.

> ⚠ **Affinity is a bonus, not a gate.** D22 was reverted (§2f.2): matching affinity grants an
> **enhanced tier**, reusing §4.2's `+1` pattern. So affinity never changes a `distance` — it changes the
> *result*. The preview must show both, or a player will read a matched fill as a different recipe.

### The loot filter — I12's `40/day` is an interface requirement and it lands here

⛔ **It is not a drop cap.** D26 forbids metering the player, and §2f.2 is explicit: *"The `40/day` line
is I12 asking for **a loot filter** — an interface requirement, not a cap."*

The numbers, so the filter is built against a target rather than a feeling:

| Source | Number |
|---|---|
| `ssot-generation.md:855-857` | ~10 equipment items per half-hour session; **20–30 per day**; 6–9 keepers |
| `ssot-generation.md:859-862` | *"no loot filter on day one"* + a *"salvage everything below rarity X"* button. **Tripwire: if measured steady-state inflow exceeds 40 items/player/day, a filter is required before the next content wave"* — measured in `item_drop_log`, *"a number to instrument, not a hope"* |
| `ssot-inventory.md:534-541` | ~2,000 rolled rows before the FE needs virtualisation · **~60 reviewed per session** before players stop reading · ≤ ~30/hour before a drop-time filter stops being optional |

⚠ **And I12's own axis is wrong and must be restated.** §2f.2: *"I12's `20–30 items/day` imports a
wall-clock axis the game does not have. Restate per **content event**, not per day."* So the filter's
threshold is authored per-run / per-content-event, and `item_drop_log` is instrumented on that axis.

**What ships:** the filter is a **client-side view rule over the armoury** plus I13's auto-salvage rules
at the drop boundary (`rpg_item_rule`, §4.6) — never a server-side throttle on generation. The four
pressures I13 names instead of a bag cap all live here: the **inbox** (`seen = 0`, an unreviewed count in
the header — *"an inbox can be emptied; a stash cannot"*), the **gap board**, auto-salvage, and the
two-grade split that keeps stock as counters.

### GG-50 — every collection declares its behaviour at 10, 100 and 1000

| Surface | 10 | 100 | 1,000 | Ceiling |
|---|---|---|---|---|
| **Armoury list** | render all | render all | **virtualize** | search-first above 2,000; `InventoryCeiling` at 20,000 rows is module 2's **structural abuse guard**, and its comment says so |
| **Gap board** | render all | render all | 48 × 15 = **720 cells**, server-computed and memoised per `(player_id, armoury revision, catalog_revision)`; defaults to showing only cells with an available strict improvement | — |
| **Compendium** | render all | render all | **127 is the whole population** — render all, four-state filtered | — |
| **Paperdoll** | 15 cells | — | — | bounded by construction |

### GG-61 and the 640px wall — measured, not assumed

`PanelShell` is `w-[min(640px,92vw)]`, `max-h-[min(720px,82vh)]`, body `overflow-y-auto`
(`shell/PanelShell.tsx:79-82`, `:92-93`). And the fully-populated item card was already measured at
**945px of content against a 720px cap** (`game-gui-principles.md` §GG-61).

Three consequences, all layout decisions this module must make rather than discover:

1. **The item card scrolls inside the shell**, and identity blocks 1–6 must be above the fold; sockets,
   set, granted action, flavour and footer may fall below it.
2. **Comparison is stack-first, not side-by-side.** `RelicsLayer.tsx` already carries this finding in a
   comment: *"plate 02 §B assumes a 1000px-wide panel; `PanelShell` caps every layer at 640px — two
   columns inside that cap left the comparison too narrow to read comfortably at any width."* Do not
   re-litigate it; the delta table is one column with a unit-class group header (G3 §4.2).
3. **The socket bench and the compendium are band-3 dialogs**, not more content inside the panel.

### The rules this module is graded against, and the two it cannot satisfy alone

| Rule | What it requires here |
|---|---|
| **GG-47** — anything choosable is comparable | equipped-versus-candidate is the **default** presentation when a choice is being made, not a tooltip afterthought. A card built only to *display* cannot later be asked to display a *difference* — so the card component takes an optional incumbent from day one |
| **GG-46** — a number states its meaning | every rendered number goes through module 10's `DisplayLine` and the web `formatMagnitude`; `magnitudeGuard.ts` already walks the tree and fails on a bare magnitude |
| **GG-25** — show the thing, not a row about it | the armoury is cards and pips, not a `DataTable`. Tables are for logs |
| **GG-27** — the squint test | the dominance verdict is a **word and a shape**, never a colour alone: `Strictly better ▲` · `Strictly worse ▼` · `Sidegrade ◆` · `Not comparable ◇` |
| **GG-17** — designed states | loading / empty / error / **locked**, on every one of the six surfaces. The `storage` tab's honest `EmptyState` today is the pattern, not the exception |
| **GG-44** — complexity unlocks | Relics on first container; the compendium on the first socketed item; a locked entry **says what unlocks it** and is never invisible |
| **GG-9** — one canonical home per concept | the item card is one component. **G3 §8.6 forbids a second renderer**, and `overlay-spec.md` already settled that the launcher and injector overlays load the same web app |

⚠ **Two it cannot satisfy alone, and both are named rather than assumed:** the **comparison payload** is
I13's algorithm (module 2's surface) — this module renders `strictly-better / strictly-worse / sidegrade
/ incomparable` and, for a sidegrade, *"the trade, spelled out"*; and there is **no synthesized scalar**
until module 9's power read lands, at which point power joins as **one row above the delta table** and
the table stays.

### What the armoury surface must never invent

- ⛔ **A bag capacity.** §2.5 — unlimited rows, no expansion currency, no tabs. *"A bag cap here would be
  pure friction in a browser tab."* The five pressures replace it.
- ⛔ **A score.** *"There is no single score. 9 hit points and 5 accuracy points are not the same
  currency"* — and it is a **persistent footnote**, not a dismissible hint. *"A player who dismisses it
  once will read its absence as a missing feature forever."*
- ⛔ **A tier number, an atom id, a family id, a group id, or per-mille as per-mille** (G3 §2.4).

## Commands

```powershell
# unit + component
cd web\fusion-rpg-web; npm run test -- relics socket compendium armoury

# the two guards this module must not break
cd web\fusion-rpg-web; npm run test -- magnitudeGuard bandGuard

# the renderer's own contract lives in Core (module 10) and is asserted there
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ItemDisplay"
```

## Project structure

```text
web/fusion-rpg-web/src/layers/relics/RelicsLayer.tsx          edit — replace the body; keep the layer
web/fusion-rpg-web/src/layers/relics/ArmouryList.tsx          new — GG-50 virtualized above 100
web/fusion-rpg-web/src/layers/relics/ArmouryFilter.tsx        new — the loot filter + the inbox count
web/fusion-rpg-web/src/layers/relics/Paperdoll.tsx            new — 15 roles / 12 hybrid, the equip screen
web/fusion-rpg-web/src/layers/relics/ItemCard.tsx             new — renders DisplayModel's 11 blocks;
                                                                takes an optional incumbent (GG-47)
web/fusion-rpg-web/src/layers/relics/CompareView.tsx          new — stack-first at 640px; unit-class
                                                                group headers; the persistent footnote
web/fusion-rpg-web/src/layers/relics/SocketBench.tsx          new — band-3; the fill + the preview
web/fusion-rpg-web/src/layers/relics/Compendium.tsx           new — band-3; 127 rows, four states
web/fusion-rpg-web/src/contract/types.ts                      edit — SocketsView / SetView replace
                                                                `Pending<unknown>` (types.ts:144-145)
web/fusion-rpg-web/src/contract/adapt.ts                      edit — adaptRelic stops returning
                                                                absent() for nine of eleven blocks
src/FusionRpg.Server/ItemSurfaceEndpoints.cs                  new — armoury page, gap board, compare,
                                                                combination distances. Read-only
docs/web/spec.md                                              edit — amend success criterion 7 (§399)
```

**No new route, no new stage, no second renderer, no `z-index` outside the band tokens.**

## Code style

```ts
// Never enumerate a permutation. |R| <= 4, so a naive "which arrangement satisfies this" is 24 per
// recipe x 127 recipes per card, recomputed on every insert. Split it instead: the multiset decides
// INSERT-away (where order is irrelevant), and only an exact multiset match can be SWAP-away -- where
// the minimum transposition count is exactly n - cycles(sigma), which is one O(k) pass.
//
// This calls module 16's evaluator with a distance parameter. It is NOT a second near-miss pass:
// ssot-presentation.md §4.3 and ssot-sockets.md:277-279 both forbid one, and a parallel pass is how
// "the tooltip said one more and it did not fire" happens.
export function nearMiss(fill: Insert[], recipe: Recipe): NearMiss {
  if (fill.length < recipe.length) return { state: "undiscovered" };   // unreachable, never one-away

  const missing = multisetDifference(recipe.ingredients, fill);        // O(k), k <= 4
  if (missing.length > 0) return { state: "one-away", kind: "insert", distance: missing.length, missing };

  if (sameSequence(fill, recipe.ingredients)) return { state: "active" };

  // exact multiset, wrong order -- n - cycles is the exact minimum, not a heuristic
  const swaps = recipe.length - cycleCount(permutationOf(fill, recipe.ingredients));
  return { state: "one-away", kind: "swap", distance: swaps, requiredOrder: recipe.ingredients };
}
```

## Testing strategy

| Test | Asserts |
|---|---|
| `opening_relics_over_the_lawn_does_not_remount_the_stage` | **GG-1/GG-11 by reference identity**, not by the screen looking right — the keystone |
| `relics_adds_no_top_level_route` | the route table gains nothing; the panel is a `?panel=` parameter (GG-8) |
| `esc_pops_one_layer_and_the_socket_bench_pops_before_the_panel` | GG-6, over a 3-deep push |
| `every_player_action_is_within_three_pushes` | GG-10, as a reachability walk |
| `the_item_card_renders_all_eleven_blocks_from_the_display_model` | and computes **no** magnitude of its own |
| `no_bare_numeric_magnitude_reaches_the_dom` | the shipped `magnitudeGuard` over this module's files |
| `identity_blocks_are_above_the_fold_at_720px` | GG-61 against the measured 945px card |
| `comparison_is_the_default_when_a_candidate_is_selected` | GG-47 — not a tooltip, not an extra click |
| `comparison_never_mixes_two_unit_classes_in_one_column` | SC4 as a layout invariant, over a generated channel-pair matrix |
| `the_no_single_score_footnote_is_persistent_and_not_dismissible` | I13's ask, as a component property |
| `a_sidegrade_renders_the_trade_and_an_incomparable_renders_the_reason` | *"an incomparable verdict with no explanation reads as a bug"* |
| `dominance_is_a_word_and_a_shape_never_colour_alone` | GG-27, plus colour-blind simulation |
| `a_recipe_one_insert_away_is_decided_without_enumerating_an_order` | the tractability claim, asserted by instrumenting the evaluator's call count |
| `a_swap_away_recipe_reports_the_exact_minimum_transposition_count` | `n − cycles`, over hand-worked 2-, 3- and 4-cycle fills |
| `a_two_socket_item_never_shows_a_four_insert_recipe_as_one_away` | `undiscovered`, never `one-away` — G3 §4.3's `∞` rule |
| `near_miss_uses_the_same_evaluator_as_the_active_set` | one function with a distance parameter |
| `the_full_127_catalog_is_never_rendered_at_once` | active + one-away + known-inactive-by-name; the rest absent |
| `a_recipe_is_revealed_only_after_every_ingredient_has_been_held` | the compendium reveal rule, over a held-ledger fixture |
| `a_matched_affinity_changes_the_result_not_the_distance` | **D22 reverted** — a bonus, never a gate |
| `a_set_piece_offers_no_strain_or_splice` | D21's exclusivity, at the bench |
| `the_armoury_declares_its_strategy_at_10_100_and_1000` | GG-50, with rendered node counts at each magnitude |
| `the_loot_filter_hides_rows_and_changes_no_drop` | **D26** — the filter is a view rule; no server call changes generation |
| `the_inbox_count_falls_to_zero_when_the_unseen_are_reviewed` | *"an inbox can be emptied; a stash cannot"* |
| `every_surface_has_loading_empty_error_and_locked` | GG-17, all six surfaces × four states |
| `a_locked_compendium_says_what_unlocks_it` | GG-44 — never invisible, never present-but-dead |
| `there_is_exactly_one_item_card_component` | G3 §8.6 — no second renderer, asserted as a guard |
| `no_z_index_outside_the_band_tokens` | the shipped `bandGuard` |

## Boundaries

**Always:** open as a **layer over the current stage**, never as a route. Render module 10's
`DisplayModel` and nothing computed here. Call module 16's evaluator with a distance parameter. Use the
web kit's shells and tokens. Declare a collection strategy at 10 / 100 / 1000. Give every surface all
four states. Show comparison by default wherever a choice is being made.

**Ask first:** amending **`docs/web/spec.md` success criterion 7** (§399) so the item card's eleven
blocks are item-program work against the web kit — a doc change, and the reason this module exists.
Whether the socket bench and compendium are band-3 dialogs or in-panel views (this spec says band-3;
it is a layout call the owner may reverse). The **loot-filter default** — which rarity the *"salvage
everything below X"* button starts at. The pips-vs-`+12` left-edge ordering (G3 §10.2 — pips first here,
because pips are the rarity ladder's accessibility channel). Any **new key binding** — the verb table is
declared once (`information-architecture.md` §5) and no surface reassigns a global verb.

**Never:** ⛔ add a top-level route, a stage, or a second entry point for an item surface. Never build a
second item card, or a second near-miss pass. Never render a tier number, an atom id, a family id, or
per-mille as per-mille. Never encode "better" or a rarity ordering in hue alone. Never render all 127
combinations. ⛔ **Never turn the `40/day` tripwire into a drop cap, an inventory ceiling, or a cost
curve that rises with player power** — D26 puts every one of those out of this program's scope, and the
filter is the interface answer, not the metering one. Never build a modal outside the shell — *"feature
code never builds its own modal"*.

## Success criteria

- [ ] Opening Relics over a live Lawn does not remount the stage, proven **by reference identity**.
- [ ] The route table is unchanged; every item surface is a `?panel=` / band-3 push, and Esc pops one.
- [ ] The item card renders all eleven blocks from module 10's `DisplayModel`, and `magnitudeGuard`
      passes over every file this module adds.
- [ ] `ContainerView`'s `sockets` and `set` are real types, and `adaptRelic` returns `absent()` for no
      block that has data behind it.
- [ ] Comparison is the default presentation on candidate selection, stacks at 640px, groups by unit
      class, and carries the persistent no-single-score footnote.
- [ ] ⭐ **The socket preview reports insert-away and swap-away distances without enumerating a single
      permutation**, with the swap count proven exactly `n − cycles` over 2-, 3- and 4-cycle fixtures,
      and the whole 127-recipe pass measured under one frame budget.
- [ ] The compendium reveals a recipe only after every ingredient has been held, and never renders more
      than active + one-away + known-inactive-by-name.
- [ ] An unreachable combination is `undiscovered`, never `one-away`.
- [ ] The armoury declares and demonstrates its behaviour at 10, 100 and 1,000 rows.
- [ ] The loot filter hides rows and changes no drop — no code path in this module reaches generation.
- [ ] All six surfaces have loading, empty, error and locked states, and a locked one says what unlocks
      it.
- [ ] Exactly one item-card component exists in the tree.
- [ ] `docs/web/spec.md` §399's criterion 7 is amended, and both maps now name the same owner.
