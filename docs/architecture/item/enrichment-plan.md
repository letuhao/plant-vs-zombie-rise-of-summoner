# Enrichment plan — closing the reachability gaps

**Status:** ✅ **complete 2026-08-23.** All four waves built and verified. `check_reachability.py` reports **0 gaps**; the CI gate is armed.

The corpus is referentially perfect and partly unplayable. `tools/ItemSeedValidator` reports
**1,438 entries, 0 errors**; `tools/seed_graph/check_reachability.py` reports **35 gaps and 34
notes** over the same files. Both are correct, because they answer different questions — one asks
whether references resolve, the other whether a player can reach the thing.

This plan closes the gaps. It is deliberately ordered so that each wave can be validated before the
next depends on it, and so the two waves that are pure judgement land last.

---

## 1. The gaps — all closed

| # | Gap | Scale | Wave | Closed by |
|---|---|---|---|---|
| G1 | Every set uncompletable | 30 sets | R1 | `bind_set_members.py` — 180 member rows |
| G2 | Uniques unobtainable | 144 | R2 | `acquisition` per §4.5 band policy + drop entries |
| G3 | Charms unobtainable | 70 | R2 | `charm` entry kind was already legal, never used |
| G4 | Consumables unobtainable | 60 | R2 | new `consumable` entry kind |
| G5 | Gems unobtainable | 30 of 40 | R2 | insert entries |
| G6 | Role/frame slots with no drop path | 7 | R2 | equipment entries |
| G7 | `+X` enhancement line unreachable | 10 families | R3 | `enhanceTrack` on all 740 base types |
| G8 | Material unobtainable / unspent | 1 + 4 | R3 | drop entry for `essence.ice`; 4 sinks still open, a NOTE |

**Result: 0 gaps, 1 note.** The remaining note is four materials no recipe consumes — a drop with
no sink, which is a legitimate mid-build state and not a reachability failure.

Three defects in the *validators* surfaced only once the new data existed, which is the argument
for building data and gate together rather than in sequence:

- **`SameStageReference`, 274 of them.** Drop tables referencing uniques are same-stage, and the
  rule correctly forbids that — same-stage partitions are authored in parallel and cannot see each
  other. But drop tables were *always* dispatched last precisely because they reference everything;
  only the stage label had not caught up. `drop-table` is now **stage 1d**, alone. Encoding the real
  dependency order beat punching a hole in the rule.
- **`MagnitudeAuthored` on `atLevel`.** A milestone rung (+4/+12/+20) is one of five fixed steps,
  the same species of value as a set threshold's `pieces` — a structural count, not a magnitude.
- **`ReferenceUnresolved` on `enhanceTrack[].family`.** A base type points at `atom.enhance-vigor`,
  which a milestone *mints* rather than an allocation handing out, so `ById` could never hold it.
  The resolver now consults minted runtime ids too — the fourth appearance of the tracking-id vs
  runtime-id split, and the first time it was anticipated rather than discovered.

`DropTableCheck` was added at the same time: entry-shapes.md §9 called `entryKind` a closed enum and
nothing enforced it, which is a poor state for a vocabulary that just grew by two values.

### Why the reference validator could not have caught any of these

Each is an **absent row**, not a wrong one. A reference check reads what is written and asks where
it points; it has nothing to read when the answer is "nobody wrote the row". That is not a defect
in the C# validator — it is the boundary of what referential integrity means, and the reason the
Python checker is a separate tool rather than more checks bolted into the first one.

---

## 2. Root cause worth fixing before the content

**The set exemplar is wrong, and it is why all five set agents produced the same defect.**
`data/seed/items/_exemplars/set.exemplar.json` shows `members: [{ "role": "core-guard" }]`.
`ssot-sets.md` §4 defines `item_set_member` as `(set_id, container_id, role, frame)` with
`PRIMARY KEY (set_id, container_id)`. Five agents followed the exemplar; the exemplar was the
defect, exactly as it was for `powerAxis` and the display templates.

**Fix the exemplar first, in R1, before dispatching anything.** This is the third time an exemplar
has propagated a shape defect to every agent that read it, and the standing lesson now has a third
data point: *an exemplar is the most-read file in the corpus during authoring, and a wrong one is
indistinguishable from a wrong contract.*

Add to the validator at the same time, so the shape cannot regress:
`SetMemberUnpinned` — a set member with no `baseType`, as an **error**, not a lint.

---

## 3. The waves

### R1 — set membership (the big one)

**5 agents, one per theme, Sonnet.** Each owns its existing `sets/<theme>.json` and adds, for every
member of every set in it: a `baseType` naming a real base type in that member's role, and the
`frame` that base type carries.

Constraints the brief must carry, because none is derivable from the file being edited:

- **A set is frame-NEUTRAL, not single-frame.** ssot-sets.md §3.7 line 222: *"A set is
  frame-neutral. Its members are frame-specific base types, at most one per (role, frame)."* So a
  theme whose `frameAffinity` is `both` gets **two member rows per role** — one humanoid, one plant
  — and a hybrid may mix them. Thresholds are unaffected: the evaluator counts distinct member
  *roles* equipped, not rows. A theme locked to one frame authors one row per role, which §3.7
  line 228 explicitly allows as the flavour-locked shape.
- **Member roles must all be in the hybrid role core** (§3.7 line 233) — the 13 roles with
  `hybridEligible: true` in `core.v1.json`. `ward-array` and `jewel-minor-b` are excluded. All 128
  existing members already comply; nothing was enforcing it, so the reachability tool now does.
- **A unique may not be a set member** (§3.8, hard no — both cost 1.5 AE and the piece would be
  paid for twice). 144 unique base types are therefore off-limits, and the brief must list them or
  the agent must grep `uniques/` and exclude what it finds.
- **A base type MAY belong to several sets** (owner decision, §5.1). `PRIMARY KEY
  (set_id, container_id)` permits it and nothing forbids it, so reuse pieces where the theme fits.
  Within one set, `UNIQUE (set_id, role, frame)` still means one member per role per frame.
- Six sets are grand (6 members); the rest are 4. Piece counts are already authored and correct.

**Cost:** roughly 180 member rows across 5 files (128 roles, doubled for the three
`both`-frame themes). Cheap, because the sets, their roles and their thresholds
already exist — this wave writes one field per member.

**Gate:** `SetUncompletable` and `SetShortOfThreshold` both reach zero.

### R2 — acquisition (the widest)

**5 agents.** Four own one drop-table partition each; the fifth adds `acquisition` to the uniques.

The drop-table shape currently supports `equipment | material | currency | insert | nothing`.
Uniques, charms and consumables have no entry kind at all, so this wave **extends the shape** —
which makes it the one wave that touches a contract rather than only data:

- `entry-shapes.md` §9 gains `unique`, `charm` and `consumable` entry kinds.
- Uniques are granted **by id**, never categorically: a unique is a specific thing, and a
  categorical grant would make every unique in a rung band interchangeable, which is precisely the
  convergence `ssot-uniques.md` §3.7 spends three rules preventing.
- Charms and consumables may be granted categorically (by `charmClass` / `classId`), because they
  are not identity content.
- The four channels already have scopes: d1 general, d2 source-locked, d3 quest, d4 crafted. A
  band-90 unique must **not** land in d1 — `ssot-uniques.md` §4.5 makes `acquisition = 'drop'` at
  ordinal ≥ 90 the refusal `UniqueUnreachable`. That rule needs a validator check too.
- G6's seven uncovered role/frame slots get equipment entries.
- G5's thirty unreferenced gems get insert entries.

**Gate:** `Unobtainable` and `SlotUncovered` reach zero.

### R3 — the enhancement track and the crafting economy

**2 agents.**

One authors `item_enhance_track` onto base types: which base type grants which milestone family at
which of +4/+8/+12/+16/+20. `entry-shapes.md` §6 already scoped this to the base-type kind and it
was simply never written. Not every base type needs a track — but if none has one, the feature does
not exist.

One reconciles materials against recipes: give `material.002` a drop, and either give the four
unspent materials a sink or mark them deliberately terminal.

**Gate:** `FeatureUnbound` and `RecipeInputUnobtainable` reach zero; `MaterialNeverSpent` is either
zero or explained in the file.

### R4 — arm the gate

Flip the CI step in `.github/workflows/ci.yml` from report-only to `throw`, and add
`SetMemberUnpinned` and `UniqueUnreachable` to the C# validator. After this, a future authoring
wave cannot reintroduce any of these classes.

---

## 4. What this plan does **not** do

The wave-2 semantic review found 19 MAJOR findings that are content *quality*, not reachability:
60 consumables with no flavour, three silent themes, a rarity ladder that does not always climb
([review/README.md](review/README.md) §2). Those are real and they are a different job — this plan
would close every gap and leave all of them untouched. They are not folded in here because mixing a
"make it work" pass with a "make it good" pass produces a wave that can be judged as neither.

---

## 5. Owner decisions — resolved 2026-08-23

1. **May a base type belong to more than one set?** → **Yes, freely.** The schema already permits it
   (`PRIMARY KEY (set_id, container_id)`), and no validator rule will constrain it. R1 briefs
   therefore let a piece serve several sets, which makes the 740 base types go much further across
   30 sets and lets themes overlap on shared gear. The risk this accepts is that one strong piece
   becomes near-mandatory; that is a balance question the win-rate sweep can answer later, not a
   structural one.
2. **Do sets use existing base types or bespoke pieces?** → **Answered by the document, not the
   owner.** ssot-sets.md line 40: "A set is a named group of item base types", and §4's table maps
   members onto `effect_container` rows of kind `item`. Existing base types. No sixth wave.
3. **Unique acquisition channels.** → **Answered by the document.** §4.5 already specifies all four
   channels and their weights: random drop at low weight, source-locked as *the primary channel*,
   quest/first-clear, and crafted-from-recipe as the deterministic top-rung answer. `acquisition` is
   a three-value enum (`drop | source-locked | deterministic`) and `drop` at ordinal ≥ 90 is the
   refusal `UniqueUnreachable`. R2 implements that table; nothing here needed deciding.

Two further decisions taken the same day, recorded in [build-log.md](build-log.md):

4. **Unique count: 144, not the 20 in §5.33.** The lane document predates the owner's Diablo-2 scale
   decision. §5.33 now carries a superseded banner; §3.7's *shape* rules remain binding.
5. **Which 8 roles carry uniques: the four heaviest and the four lightest.** Replaces an earlier
   default of "the eight heaviest", on §3.5's own argument that the unique wins the build rather
   than the stat sheet. Costs an 18-partition re-run, which is in flight.
