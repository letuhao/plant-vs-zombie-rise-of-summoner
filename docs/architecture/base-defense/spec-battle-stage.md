# Spec: `battle-stage`

**Module 22 of 29 · level 8b · depends on `board-render` (8) · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04. **Added by owner decision 44.**

---

## Objective

**Retire the dead `battle` stage id by using it — as playback of a resolved battle on the generic
board layer.**

`railState.ts:31` has declared `currentStageId: "sanctum" | "world" | "lawn" | "battle"` since the Game
GUI decision, with **nothing behind `battle`**. Decision 40 made `board-render` serve both stages;
decision 44 says the id is retired by building it, not by deleting it.

**Success looks like:** `#/battle/{battleId}` plays back a `BattleReport` on the same layer `#/siege`
uses, and the FE has **zero declared-but-unbuilt stage ids** for the first time.

---

## ⛔ "Thin" is a constraint, not an aspiration

Decision 44 is explicit, and this section is the fence around it:

> *"`#/battle` renders a **resolved `BattleReport` in playback** on the generic board layer. It invents
> no battle requirements — the battle already resolves today and produces a report; the stage shows
> one. **Anything beyond playback needs `battle`'s own spec.**"*

**This module has no design authority over what a battle is.** Every spec in this program forbids
inferring requirements for an unspecced system, and `battle` is unspecced. So:

| In scope | Out of scope — needs a `battle` spec |
|---|---|
| Play back a resolved `BattleReport` | Playing a battle live |
| Scrub, step, pause playback | Issuing orders |
| Show actors, positions, HP, the event log | Deployment, targeting UI, any input to the sim |
| Reuse `stages/siege`'s subdirectory shape | A battle-specific HUD vocabulary |

**If a requirement is not derivable from a `BattleReport` that already exists, it is out of scope.**
That is the whole test, and it is a cheap one to apply.

---

## Why this is worth doing at all

Three reasons, and the first is the one that matters to the rest of the program:

1. **It proves `board-render` is generic.** A layer with one consumer is generic by assertion.
   Two consumers with genuinely different data — a district siege and a squad battle — is proof.
   `board-render`'s success criteria depend on this.
2. **It retires a cost the `decisions.md` amendment named.** That row's third cost was *"approving this
   leaves **two** declared-but-unbuilt ids unless `#/battle` lands first."* After this, zero.
3. **Playback is already most of what `stages/world/playback/` does**, so the shape exists to copy
   rather than invent.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `railState.ts:31` — the `battle` stage id, declared, **nothing behind it**.
- `BattleReport` + `Timeline.BattleTrace` — a resolved battle, serialized and SHA-256 hashed as the
  determinism golden. **This is the entire data source.**
- `src/stages/world/playback/` — a shipped playback module; the shape to copy.
- `board-render`'s generic layer (this program, level 8).
- `BattleModeProfileCatalog` — `classic-round`, `galaxy-sync`, `hybrid-atb`, `siege`.

**Real gap.** No route, no stage, no report→board projection.

---

## The contract

### 1. Route and stage id

`#/battle/{battleId}`. **`railState.ts`'s union is unchanged** — `battle` is already in it. This module
is the only one in the program that adds a stage **without** adding an id.

### 2. The six shell rows — but one is already there

`siege-stage` lists six per-stage integration points. `battle` already occupies row 1 (the id), so this
module fills the other five: route table, default-layer map, Esc/back-target map, the **GG-7
reachability matrix**, and the i18n label catalog.

**Zero branches**, same as `siege-stage`. A `if (stage === "battle")` in shell code means the shell has
grown a special case.

### 3. Report → board projection

A `BattleReport` has no board. **That is the point** — it is what makes this a real second consumer:

```ts
/**
 * Projects a resolved BattleReport onto the generic board layer.
 *
 * A classic-round battle has NO GridSpec — PositionOf was null for every actor in it. So the
 * projection SYNTHESISES a presentational layout: two facing ranks, squad and wave, ordered by actor
 * key. This is a VIEW decision with no simulation meaning, and it must be labelled as such wherever
 * it appears, or a player will read spatial meaning into a fight that had none.
 */
export function projectReportToBoard(report: BattleReport): BoardView;
```

> ### ⛔ The synthetic layout must never leak back into the sim
>
> This is the module's single real hazard. A presentational rank order is not a position; if anything
> ever reads it as one — a range check, an AI, a replay — a boardless battle silently acquires geometry
> and its goldens move.
>
> **Guarded structurally:** the projection lives in `stages/battle/`, takes a `BattleReport` and returns
> a view type, and **imports nothing from `Core`'s board namespace.** A source-scan test enforces it.

**A siege battle projects differently**: it *has* a board, so its real cells are used. One function,
two paths, and the second is what makes the layer's genericity real rather than nominal.

### 4. Playback controls

Step, scrub, play/pause, speed. **Reading a report, never re-resolving it** — the report is the
determinism golden, and re-running the sim to render it would make the FE a second resolver.

### 5. `stages/` may not name a `*Dto`

`contract/contractGuard.ts:57` guards `stages`, `layers` and `ui`. Same obligation `siege-stage`
carries; the board view type goes in `contract/types.ts` or stays `features/`-local.

### 6. Lazy-loaded

Same entry-chunk budget (≤180 KB gz). `battle` and `siege` **share** the lazily-loaded board chunk,
which is the second consumer paying for itself.

---

## Tunables

None. Presentation constants are module-local.

## Numeric types

TypeScript. `BattleReport` carries `long` HP and damage values that can exceed
`Number.MAX_SAFE_INTEGER` under a scaled `contentScale` — **carry them as strings or `bigint` and format
for display**, never parse into a `number`. Same rule as `siege-stage`, and the same reason it is easy
to forget: JavaScript will not complain.

## Boundaries

**Always:** derive every requirement from an existing `BattleReport` · five shell rows, zero branches ·
label the synthetic layout as presentational · lazy-load · share the board chunk with `siege`.

**Ask first:** anything interactive · a battle-specific HUD concept.

**Never:** re-resolve a battle in the FE · let the synthetic layout reach `Core` · add a stage id (it
exists) · infer a `battle` requirement that no report carries · parse a `long` into a JS `number`.

---

## Testing

| Test | Asserts |
|---|---|
| `Battle_stage_adds_no_new_stage_id` | the id was already declared |
| `Zero_declared_but_unbuilt_stage_ids_remain` | **the amendment's third cost, discharged** |
| `Shell_has_no_battle_specific_branch` | source scan of `src/shell/` |
| `A_boardless_report_projects_to_two_facing_ranks` | the synthetic path |
| `A_siege_report_projects_to_its_real_cells` | the real path — **two genuinely different consumers** |
| `Synthetic_layout_never_reaches_core` | **the hazard.** Import scan over `stages/battle/` |
| `Playback_never_re_resolves` | no sim call from the FE |
| `All_battle_goldens_byte_identical` | this module is FE-only and must move nothing |
| `Board_render_serves_both_stages_from_one_chunk` | `board-render`'s genericity, proven |
| `No_Dto_named_type_under_stages` | `contractGuard.ts:57` |
| `Long_values_survive_as_bigint` | above `MAX_SAFE_INTEGER` |
| `Battle_stage_is_lazily_loaded` | entry chunk unchanged |

## Success criteria

1. `#/battle/{battleId}` plays back a resolved report on the generic layer.
2. **Zero declared-but-unbuilt stage ids remain in the FE.**
3. `board-render` has two real consumers, with genuinely different projections.
4. The synthetic layout is provably unable to reach `Core`.
5. No battle golden moves — this module is FE-only.
6. Entry chunk unchanged; both stages share one lazy chunk.

## Open questions

None. Decision 44 fixes the scope at playback, and anything past that is `battle`'s own spec to write.
