# Spec: `item-card`

**Module id:** `item-card` · **Program:** [item](../item-map.md) · **Build order:** 10 of 21
**Depends on:** `affix-legality` (module 8), `power-reads` (module 9)
**Consumed by:** **module 20 `item-surfaces`** — the renderer. This module renders nothing.

## Objective

Turn atoms into text a player can read. The lane is
[G3 `ssot-presentation.md`](ssot-presentation.md); what this module delivers is its §2.1 projection,
as data:

```text
render(instance, container, catalogue, context) → DisplayModel
```

Plus the three things the projection cannot exist without: the **unit ledger** (`UnitClass` beside its
reader), the **template + string layer** (where a family's sentence lives), and the **validation** that
makes an unrenderable family a build failure rather than a raw id on a tooltip.

Plus one thing two lanes independently asked for and nobody owned: a **light-theme rarity palette**.

## Design

### ⚠ The scope boundary, stated first because it is the one that gets crossed

G3 §1 cedes *"UI layout, component code, CSS, routing"* to the web spec. The web spec, from its own
side, records the same seam as **unclaimed**:

> *"`item/ssot-presentation.md` is the **presentation contract for every number a player reads** … and
> it cedes 'UI layout, component code, CSS, routing' to **this file**. That seam is unclaimed from this
> side."* — `docs/web/spec.md:137-144`

**Two maps pointed at each other.** `item-map.md:154` resolves it by adding **module 20
`item-surfaces`**. So:

| This module (10) | Module 20 `item-surfaces` |
|---|---|
| `DisplayModel` — an ordered tree of blocks and `{key, args}` leaves | DOM, CSS, layout, routing, the loot filter |
| `UnitClass` and the channel→unit map | which column a unit group is drawn in |
| The template table and the string catalog | the `<span>` that shows a template's output |
| **Light-theme palette tokens** (hex values + the rules they satisfy) | applying the tokens to a surface |
| The four combination states and the distance rule | the compendium screen |

**A string is never glued in a component, and a component never computes a magnitude.** That is the
whole seam, and §6.3 case 1 is the test that keeps it.

### Why the projection is pure and lives in Core

Three reasons, all G3's and all still true (§2.1):

1. **Comparison diffs rendered lines**, so the server must be able to call the same function the tooltip
   calls.
2. **It must be testable without a browser** — the "every atom renders" guard iterates the whole catalog.
   That is a `dotnet test`, not a Playwright run.
3. **Standalone-first** (SC8): the card must render with the PvZ game closed. A projection in Core
   cannot reach for a Unity value.

⭐ **And one this module adds:** module 9's power read is a Core function. A card that computed its own
power number in TypeScript would be a second answer to a question `PowerScalar.Of` already answers
integer-exactly (`Effects/Atoms/Power/PowerReads.cs:65`).

### Three levels, each built from the one below

| Level | In | Out |
|---|---|---|
| **Line** | one atom + its frozen values + its unit class | `DisplayLine` |
| **Card** | one instance + container + sockets, set, enhancement, requirements | ordered `DisplayBlock[]` |
| **Compare** | two cards + I13's comparison payload | `CompareModel` |

```text
DisplayLine = { key, args, unitClass, contextRead?, rollQualityPerMille?, rollPolicy,
                sourceKind, groupOrder }
```

`sourceKind ∈ { base, implicit, affix-prefix, affix-suffix, enhancement, socket-insert, resonance,
word, set-threshold, granted-action, unique-identity, unique-variance }` (G3 §4.4). There is **no path
that produces a line except the line function** — that is what stops *"the tooltip says X and the list
says Y."*

### The unit ledger is code, not data — and the rule is E1's own

> *A thing may be **data** if adding a row changes behaviour **without new code**. If a new row needs a
> new consumer, it must be **code**.* — `spec-atom-kind-registry.md:19`

A channel's unit is inseparable from its reader. `UnitClass` sits beside `ParamSchema` in
`AtomKindRegistry`; a channel with a reader and no declared unit is a **load rejection**
(`MissingUnitClass`).

```csharp
public enum UnitClass {
    GameUnits, GameUnitsPerSecond, SigmoidPoints, SigmoidMultiplierPoints,
    StatusPotencyPoints, PerMilleRatio, Milliseconds, Count, Flag
}
```

`ChannelUnits.For(channelId)` matches by **prefix**, the way derived readers already match generated
element channels (`src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs:96-99` — the shield families
are declared as `…Prefix` constants and the element halves generated). A new element therefore needs no
new unit row.

⭐ **`status.power.*` is no longer blocked, and the lane does not know.** G3 §10 Q5 held `affliction`
and `stalwart` at `status = 'pending'` waiting on C2 — *"`ComputeNetFactor` uses a raw delta as a direct
multiplier"*. **C2 was real and is fixed by another program**
(`item-ideal.md` §2e): `src/FusionRpg.Core/Status/ResistanceEvaluator.cs:348` now reads
`Math.Clamp(1.0 + delta / StatusPolicy.NetFactorScale, …)`, and `Status/StatusPolicy.cs:24` cites
*"T3.2 (audit F4): netFactor = 1 + delta/NetFactorScale — linear, no cliff."* **Those two families ship
`live`, not `pending`**, and `StatusPotencyPoints` gets a real render arm.

### Numbers: four rules and Rule P

**Rule 1 — adopt the shipped conversion, do not write a second one.**

```ts
// web/fusion-rpg-web/src/features/demons/patronView.ts:23  (verified)
const pct = (milli: number) => `${(milli / 10).toFixed(1).replace(/\.0$/, "")}%`;
```

It moves into the shared display module and `patronView` calls it. One convention, not two.

**Rule 2 — never render a non-zero per-mille as `0%`.** Round **away from zero** at the display
boundary, the same direction the engine uses (`Effects/Atoms/CurveTable.cs:105` —
`DivRoundHalfAway`).

**Rule 3 — rounding happens once, at the display boundary, and never feeds back.** The renderer receives
the frozen integer from `effect_instance_atom.values_json` and formats it. It never re-applies a curve
and never re-rolls.

**Rule 4 — the invariant, and it is testable.** *If the card shows `+45 hp`, `values_json` holds `45`.*

**Rule P — precision never exceeds the source's claimed accuracy.** Frozen integer → exact; per-mille →
one decimal; ms → `250 ms` / `4.0 s`; sigmoid context read → one decimal in pp with `≈`; **power →
two significant figures with its band, `≈ 1,300 (±25%)`** (module 9 R3).

### Roll-quality bar — the split falls out of `RollPolicy`

Verified: `Effects/Atoms/ValueSpec.cs:10-20` declares exactly three policies —
`Fixed = 0`, `OnInstantiate`, `OnApply`.

| `RollPolicy` | Bar? | Renders |
|---|---|---|
| `Fixed` (`Min == Max`) | **none** | the value. A full bar here would be a lie about the item's luck |
| `OnInstantiate` | **bar** | the frozen value; `[Min, Max]` on expansion |
| `OnApply` | **none** | the **band** — `100–200 fire damage on hit`. The hit rolled it, not the item |

```text
segments = clamp(ceil(qualityPerMille * 5 / 1000), 1, 5)   // a non-zero roll never shows empty
```

Five segments, not ten (ten are not countable at a glance). The bar uses the theme's neutral→sun ramp,
**never the rarity palette** — I1 made lightness the rarity ladder, and a second lightness ladder inside
the same card would compete with it.

### The card — eleven blocks, and one disclosure rule

Order and contents are G3 §4.1, unchanged. The rule that makes the order safe:

> **Nothing that can differ between two items of the same base type may be hidden.**

Everything that varies is on the face: every magnitude, affix, socket, set line, requirement, the
enhancement level, the roll bars. What hides behind expansion is **invariant explanation** — an atom's
`[Min, Max]` band, the full resonance catalog beyond active and one-away, the set's member list, the
salvage breakdown.

**Never shown at all** (§2.4): atom / family / container / instance ids, **tier numbers**, name bands,
group ids, per-mille as per-mille.

**One collision, resolved and flagged:** I6 wants `+12` at the left edge
(`ssot-enhancement.md:759-762`); I1's pips want the same place. Render **pips → `+12` → name**, because
pips are the rarity ladder's accessibility channel. **Owner's call** (G3 §10 Q2) — reversible in one
ordering constant.

### D27's four container kinds change what a card must source

**D27 (owner, 2026-09-03):** `gem` · `set` · `charm` · `combo` join `ContainerKind`. That is
**effect-atom's** change (`definitions.md` §1 + `ContainerRow.cs`, `item-ideal.md` §2g row 3), not ours —
but it is what gives a socket insert, a set bonus, a charm resonance and a socket combination each a
legal container to render *from*. `sourceKind` already distinguishes them, so no `DisplayModel` change is
needed when they land; the template table gains rows.

### ⭐ The light-theme palette — owed twice, owned here

Two lanes asked independently and neither owned it:

- `ssot-rarity.md:785-786` (§10.7): *"A light-theme palette. §3.3's hexes are tuned for a dark surface."*
- `ssot-presentation.md` §10.6: the overlay *"renders over a running game whose frame brightness we do
  not control."*

**The shipped palette is dark-surface-only, and that is measured, not asserted.** Its ten hexes run
`#63645d → #f3eaa0` (`ssot-rarity.md:117-126`) with `L*` **strictly increasing** 42.1 → 91.9
(`:394-396`), and monotone through a Viénot deuteranope transform (`:401-403`).

**The invariant is not "L* increases". It is "contrast against the ground increases."** On a dark ground
those coincide; on a light one they invert — `almanac` at `L* 91.9` on a white surface is nearly
invisible, and the top of the ladder becomes the least legible rung. So the light palette keeps every
rule and flips one:

| Rule | Dark (shipped) | Light (this module) |
|---|---|---|
| Ordering channel | `L*` **increasing** with ordinal | `L*` **decreasing** with ordinal |
| What actually increases | contrast vs. ground | contrast vs. ground — **unchanged** |
| adjacent `ΔL*` | ≥ 2.5 | ≥ 2.5 |
| distance-2 `ΔL*` | ≥ 7 | ≥ 7 |
| monotone under deuteranope + protanope | required | required |
| hue carries the ordering | **forbidden** | **forbidden** |
| pips + rung name in text | always | always |
| min contrast vs. its own ground | (not stated) | **WCAG AA 4.5:1 for the rung name text** |

**Hue may be preserved per rung** (a `fused` item stays blue-ish in both themes) because hue is not the
ordering channel in either. **The ten concrete hexes are a design pass and taste is the owner's**
(`ssot-rarity.md` §10.8 makes the same point about rung names). What this module ships is the token
slot, the rule set above, and `Palette_lightness_is_monotone` extended to run over **both** palettes —
so a light palette cannot be added later without satisfying the same measured rules.

⚠ **The pip count and `display_key` do not fork.** Only `color_hex` gains a second value. That means
either a `rarity.color_hex_light` column or a theme-keyed lookup beside the row — **an ask on I1 /
module 7 `rarity-bands`**, recorded here rather than taken.

## Data shape

| # | Thing | Kind | Note |
|---|---|---|---|
| **N1** | `item_display_template(family_id PK, template_key, template_plant, group_id, status, enabled, revision)` | **data** | one row per family; a new family with no row is a **rejection**, never a blank |
| **N2** | `content/display/en.json` — flat `key → { template, plural? }` | **data**, a file | joins the content hash as a file digest. A table is defensible (G3 §10 Q3) and is the owner's |
| **N3** | `UnitClass` + `ChannelUnits.For(channelId)` | **code** | beside `ParamSchema`; §2.3's rule |
| **N4** | `item_base_type.display_json` must carry a **name key**, not name parts | ask on I3 | else base names cannot localise |
| **N5** | `rarity` light-theme colour slot | ask on **module 7** | see palette above |

**Redefined:** `effect_atom.name` becomes a **short label key** — two or three words, no digits, never a
card line — `NOT NULL`, non-empty, validated.
⚠ Verified: `Effects/Atoms/AtomRow.cs:31` declares `public string Name { get; init; } = "";` and
`AtomRowValidator` never reads it — **defect C3, confirmed** (`item-ideal.md` §2e). **C3 is module 1's**
(`item-map.md` §7 row 3); this module states the validation it depends on and does not double-own it.

### Validation — four new reason codes, each argued

| Code | Why it cannot reuse an existing one |
|---|---|
| **`MissingUnitClass`** | The failure this lane exists for. `+10 fire power` and `+10 crit rate` become indistinguishable. No existing code names *"this number has no unit"* |
| **`MissingDisplayTemplate`** | A present atom with no words. `UnknownAtom` is about an absent atom |
| **`MissingDisplayKey`** | The row exists, the string does not — a different fix |
| **`UnrenderedMagnitude`** | A number the engine applies and the player never sees — the `status.expose` defect in mirror image |

Fifteen input→code rows are G3 §6.1 and carry over unchanged. **Import is all-or-nothing; load is
per-row** — definitions §10's two-phase rule. Rows 8, 9 and 14 are load-phase as defence in depth against
a database edited outside the importer.

⚠ **Adding a reason code is a reviewed change to `definitions.md` §10's closed 33-code list**
(effect-atom's), and G1 already flagged code inflation. If the owner wants fewer, `MissingDisplayTemplate`
and `MissingDisplayKey` collapse into `MissingDisplayString`; **`MissingUnitClass` and
`UnrenderedMagnitude` must not collapse into anything** — they are the two that make the units problem a
build failure.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ItemDisplay"

# the whole-catalog guard — slow, and the one that matters
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~EveryAtomRenders"
```

## Project structure

```text
src/FusionRpg.Core/Effects/Atoms/UnitClass.cs           new — N3, beside ParamSchema
src/FusionRpg.Core/Items/Display/DisplayModel.cs        new — DisplayLine / DisplayBlock / CompareModel
src/FusionRpg.Core/Items/Display/ItemDisplayRenderer.cs new — the projection; the ONLY line producer
src/FusionRpg.Core/Items/Display/DisplayTemplates.cs    new — N1 loader + placeholder parse
src/FusionRpg.Core/Items/Display/RarityPalette.cs       new — dark + light token sets and their rules
src/FusionRpg.Data/Sqlite/RpgStore.ItemDisplay.cs       new — item_display_template
content/display/en.json                                 new — N2
web/fusion-rpg-web/src/features/demons/patronView.ts    edit — call the shared pct, stop owning it
tests/FusionRpg.Core.Tests/Items/ItemDisplayTests.cs    new
```

## Code style

```csharp
// The bar answers "where in the range did this roll land", so it may only appear where a range
// existed. ValueSpec.cs:10-20 already draws that line; re-deriving it here would be a second
// answer. Fixed => no bar (a full bar would be a lie about luck); OnApply => no bar, show the band.
static RollBar? BarFor(AtomRow atom, int qualityPerMille) => atom.Roll switch
{
    RollPolicy.Fixed        => null,
    RollPolicy.OnApply      => null,
    RollPolicy.OnInstantiate => new RollBar(
        // ceil, then clamp to 1: a real roll never renders as an empty bar (Rule 2's direction,
        // applied to segments rather than to a percentage).
        Math.Clamp((qualityPerMille * 5 + 999) / 1000, 1, 5)),
    _ => null,
};
```

## Testing strategy

| Test | Asserts |
|---|---|
| `every_atom_in_the_catalog_renders` | at `Min`, midpoint and `Max`: no raw id, no unresolved `{placeholder}`, no empty string. **The test that stops "half the items read as raw ids"** |
| `rendered_equals_applied` | every rendered magnitude equals the integer in `values_json`, under the engine's own `DivRoundHalfAway` |
| `no_comparison_column_mixes_two_unit_classes` | over a generated matrix of every channel pair — SC4 as a layout invariant |
| `display_model_is_byte_identical_for_one_seed` | same `(container_id, catalog_revision, roll_seed)` ⇒ identical model, generated name included |
| `frame_parity_every_template_resolves_for_both_frames` | no item is unrenderable on one frame |
| `plural_policy_holds` | exactly the three key families carry plurals; every other key declares `plural: none` |
| `all_four_combination_states_and_the_set_ladder_render` | the near-miss path is exercised, not declared |
| `near_miss_uses_the_same_evaluator_as_the_active_set` | one function with a `distance` parameter — never a parallel pass |
| `an_unreachable_combination_is_undiscovered_never_one_away` | a two-socket item cannot promise a four-insert resonance |
| `a_channel_with_a_reader_and_no_unit_class_is_rejected_at_load` | `MissingUnitClass`, the code this lane exists for |
| `a_declared_magnitude_the_template_never_shows_is_rejected` | `UnrenderedMagnitude` |
| `a_family_with_atoms_and_no_template_row_is_rejected` | `MissingDisplayTemplate`, never a silent blank |
| `status_power_families_render_live_not_pending` | C2 is fixed (`ResistanceEvaluator.cs:348`); `affliction` and `stalwart` are renderable |
| `power_row_renders_under_rule_p` | `≈ 1,300 (±25%)` from module 9's R3 — two sig figs, never four |
| `tier_numbers_never_appear_in_any_line` | §2.4, as a test rather than a convention |
| `dark_and_light_palettes_both_satisfy_the_monotone_rules` | `L*` monotone in **contrast against the ground** in both; ΔL* ≥ 2.5 adjacent, ≥ 7 distance-2; monotone under deuteranope and protanope |
| `rung_name_meets_wcag_aa_on_its_own_ground_in_both_themes` | 4.5:1, measured |
| `pip_count_and_display_key_do_not_fork_per_theme` | only `color_hex` gains a second value |
| `no_second_renderer_exists` | one line producer; the SPA is the only consumer — G3 §8.6 as a guard |
| `patron_view_calls_the_shared_percent_helper` | one conversion convention, not two |

## Boundaries

**Always:** produce structured data, never markup; every human-readable leaf is `{key, args}`; one line
function; `UnitClass` beside its reader; round once, at the display boundary; keep both palettes under
the same measured rules.

**Ask first:** adding a reason code (effect-atom's closed 33-code list); moving the string catalog from
a file to a table (G3 §10 Q3); the pips-vs-`+12` ordering (§10 Q2); **suppressing the power row
entirely** rather than banding it (§10 Q7); the ten light-theme hexes (taste — the owner's);
`rarity.color_hex_light` (module 7's table).

**Never:** render a tier number, an atom id, a family id, or per-mille as per-mille. Never let a
component compute a magnitude or glue a string. Never write a second renderer — G3 §8.6 forbids it and
`overlay-spec.md` already settled that the launcher and injector overlays load the same web app. Never
encode "better" or a rarity ordering in hue alone.

## Success criteria

- [ ] Every atom in the shipped catalog renders at `Min`, midpoint and `Max` with no raw id and no
      unresolved placeholder.
- [ ] Every rendered magnitude equals the frozen integer in `values_json`, proven over a seeded instance.
- [ ] A channel with a reader and no `UnitClass` is a **load rejection**, not a bare number.
- [ ] The `DisplayModel` is byte-identical for one `(container_id, catalog_revision, roll_seed)`.
- [ ] The projection is in Core, callable with no browser and with the PvZ game closed.
- [ ] **A light-theme palette ships** with its ten tokens, and both palettes pass the same monotone /
      ΔL* / colour-blind / WCAG rules under one test.
- [ ] `affliction` and `stalwart` render `live` — C2's block is lifted with its fix cited.
- [ ] Module 20 consumes the model and adds no display logic of its own; no second line producer exists.
