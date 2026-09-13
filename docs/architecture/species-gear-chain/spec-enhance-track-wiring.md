# Spec: Enhance-track wiring (`enhance-track-wiring`)

**Initiative:** `species-gear-chain` ([map](../species-gear-chain-map.md)) · **Module:** `enhance-track-wiring`
**Owning program:** `item` module 15 (`enhance-reroll`)
**Depends on:** nothing. It is a Layer-0 module — no other module in this initiative gates it, and it
gates none.
**Status:** spec, 2026-09-13. Awaiting owner approval. No build authorized.
**Source ideal:** [tier-system-ideal.md](../tier-system-ideal.md):566-569

---

## Objective

**Make the per-item enhancement milestone that every base type already authors actually arrive on the
item.**

⭐ **This is a wiring gap, not a design problem.** The predicate that decides *whether* this level
grants a milestone is written and tested. The corpus that says *which* family a given base type grants
is authored on **every** entry. The record type that carries an appended atom exists, is canonically
serialised, and round-trips. The only thing missing is that nothing connects them.

**The exact inert line:**

```csharp
// src/FusionRpg.Server/ItemWorkbench.cs:272  (inside Enhance, :240)
new MutationResult(OutcomeId(attempt.Outcome), levelAfter - target.Head.EnhanceLevel,
    Array.Empty<AtomValueSet>(), Array.Empty<int>(), Array.Empty<AtomAppend>()),
```

Every enhancement the game has ever performed has recorded *"appended nothing"* — at +4, at +12, at
+20, and at every level after. **`Array.Empty<AtomAppend>()` is the whole defect**, and it has a twin
one layer down (§ Wiring gap, item 2).

---

## What exists today — verified against code and the shipped corpus this session

### Built

| Fact | Evidence |
|---|---|
| **The stride predicate** — *"True when this level draws a milestone atom — every stride-th level, forever."* | `src/FusionRpg.Core/Items/Mutation/EnhancePolicy.cs:129-131` |
| **The stride is tuned, not a `const`** — `milestoneStride: 4`, parsed as `Positive(root, "milestoneStride")` | `data/tuning/enhancement.v1.json`; `Items/Mutation/EnhancementTuning.cs:65`, `:100` |
| **The carrier type** — `AtomAppend(int Seq, string AtomId, IReadOnlyDictionary<string, long> Values)`, with *"`Seq` is allocated, never reused, never renumbered"* | `Items/Mutation/MutationOp.cs:97` |
| **`MutationResult.Appended` is a first-class field**, not an afterthought | `Items/Mutation/MutationOp.cs:110` |
| **Appends already survive persistence and replay byte-for-byte** — canonical writer sorts by `Seq` and emits `atomId` + sorted `values`; the reader reconstructs them | `MutationOp.cs:217-230` (write), `:249-255` (read) |
| **The intent is written into the enum itself** — *"+n → +n+1. Adds a scalar and, **on a stride level, a milestone atom**. Redraws nothing."* | `MutationOp.cs:15` |
| **The milestone corpus ships** — 19 entries under `kind: "enhancement-milestone"`, each with `runtimeFamily`, `kindId`, `params` and `powerBand` | `data/seed/items/enhancement-milestones/milestones.json` — counted |
| **Every base type authors a track.** Measured over `data/seed/items/base-types/**/*.json`: **62 files, 1,178 entries, 1,178 with `enhanceTrack`** — 1,082 three-rung, 96 two-rung; `atLevel` histogram is exactly `{4: 1178, 12: 1178, 20: 1082}` | counted this session with `python`/`json` over the shipped corpus |
| **Every track family resolves.** All 10 distinct families named by any track exist as a milestone `runtimeFamily`; **zero dangling references** | same count |
| **The validator already guards the join** — a milestone mints `atom.enhance-vigor`, a base type's `enhanceTrack` points at it, and the reserved stem makes an affix-family collision an error | `tools/ItemSeedValidator/Checks/ReferenceCheck.cs:274`, `:285-288`, `:297-308` |
| **The magnitude machinery exists** — `FamilyExpansion` turns an authored `(family, kindId, channel, op, powerBand)` into one atom row per tier, `long` throughout, widened before multiplying, divided by 1000 last, overflow throws | `Core/Effects/Atoms/Generation/FamilyExpansion.cs:6-21`, `:27`, `:31` |

### Wiring gap — three inert seams, each one line to name

1. ⛔ **`ItemWorkbench.cs:272` passes `Array.Empty<AtomAppend>()`.** The `Enhance` verb never asks
   whether this level is a milestone level and never resolves a family. Quoted in full above.
2. ⛔ **The store never applies an append either.** `RpgStore.AppendMutationOpUnlocked` applies
   `result.Suppressed` — `UPDATE effect_instance_atom SET suppressed = 1 …`
   (`src/FusionRpg.Data/Sqlite/RpgStore.InstanceOps.cs:206-207`) — and **inserts nothing for
   `result.Appended`**. So even a correctly-populated `MutationResult` would be recorded in the op
   ledger and never reach `effect_instance_atom`. **Fixing only the workbench produces a receipt for
   an atom the item does not have.** (The sibling divergence for `Result.Values` is already recorded
   as a finding in `ItemWorkbench.cs:229-238`; this is the same seam, a different field.)
3. ⛔ **`EnhancePolicy.IsMilestoneLevel` has zero production callers.** The only references in the
   repo are `tests/FusionRpg.Core.Tests/Items/EnhancePolicyTests.cs:288-292`. A tested predicate
   nothing calls is the signature of this defect.

### Real gap — exactly one, and it is a generator gap

⛔ **A milestone family has no magnitude, because nothing expands it into atom rows.**
`tools/FamilyExpandGen/Program.cs:50` sets `familiesDir = Path.Combine(itemsRoot, "affix-families")`
and enumerates that directory top-level only (`:114`). The milestone corpus lives in a **different**
partition (`data/seed/items/enhancement-milestones/`) and is therefore never expanded — measured:
**`data/seed/atoms/generated/family-expand.*.json` contains no `atom.enhance-*` row, and neither does
any file under `data/seed/atoms/` or `data/generated/`.**

So a milestone entry today carries a `kindId` (`stat.modify`), `params` (`channel`, `op`) and a
`powerBand` — everything `FamilyExpansion` needs — and **no value the runtime can read.**

⛔ **This is fixed by changing the generator's scope and regenerating, never by hand-writing atom
rows.** `milestones.json` and every base-type partition carry generator provenance (all 62 base-type
files carry `_meta.model`; the milestone corpus has its own ledger at
`data/seed/items/_runs/enhancement-milestones-gen.ledger.json` and its generator at
`tools/seedsmith/seedsmith/adapters/items/milestonegen/run.py:35`). Editing emitted JSON forks the
corpus from its generator and the next run reverts it.

### ⚠ One contradiction this module must resolve, not inherit

**The stride and the authored track disagree about which levels grant.**

- `milestoneStride: 4` means +4, +8, +12, +16, +20, +24 … **forever**, and its own note says why that
  shape was chosen: *"I6's milestone atoms at +4/+8/+12/+16/+20 restated as a STRIDE rather than a
  five-entry list — a list would be a hard stop at +20 dressed as content, and AGENTS.md forbids that
  shape."*
- Every authored `enhanceTrack` is `[4, 12, 20]` (or `[4, 12]`). Measured: no entry authors 8 or 16.

Both are shipped, both are sincere, and a naive wiring would silently pick one. §Design 2 picks one
and says why; Open question 1 is where the owner may overrule it.

### ⚠ Two stale facts found while measuring — recorded so they are not re-derived

| Where | The claim | Measured |
|---|---|---|
| `tier-system-ideal.md:566-569` | *"`grep -rn \"enhanceTrack\" src/` returns **zero hits**"* | **17,913 hits** in `src/` — all build output (`bin/**` copies of seed data) plus two injector artifact `base-types-gen.v1.json` copies. **Zero hits in any `.cs` file**, which is the true and much stronger claim. The stated grep does not reproduce |
| `data/tuning/base-types-gen.v1.json` `enhanceTrack.levelsNote` | *"the shape every one of the **740** shipped base-type entries already uses"* | **1,178** entries across 62 files. The note's *shape* claim (a 3-rung `[4,12,20]` ladder, 2-rung for legacy partitions) is exactly right; only the scale is stale — which is itself the argument for never asserting a population count |

---

## Design

### 1. One resolver in Core, one lookup from the host, one append at the seam

```
Enhance (Server)
  └─ EnhancePolicy.Resolve                       (unchanged)
  └─ IF EnhancePolicy.IsMilestoneLevel(levelAfter, tuning)          ← the dead predicate, called
       └─ MilestoneTrack.FamilyFor(track, levelAfter, tuning)       ← NEW, pure, Core
       └─ lookupMilestoneAtom(family, tier)                         ← host-supplied delegate
       └─ MutationResult.Appended = [ AtomAppend(seq, atomId, values) ]
  └─ RpgStore.TrySpendAndApply → AppendMutationOpUnlocked
       └─ INSERT INTO effect_instance_atom …                        ← NEW, the store half
```

⛔ **`MilestoneTrack` is pure and takes the loaded track, never a path.** `Core` never reads a file
(`tunables-ssot.md` §7.2); the Server injects the milestone corpus and the tuning at its composition
root exactly as it already injects `EnhancementTuning` into `ItemWorkbench`. A missing milestone
family, an unknown tier or an absent tuning section is a **load rejection naming the key** (T5) —
never a default, never a silently-skipped append.

### 2. ⭐ The authored track is the authority for *which* level and *which* family; the stride keeps the ladder open past it

**Recommended reconciliation of the contradiction above:**

- For levels the track authors (`atLevel` ∈ {4, 12, 20} today), the track's own `family` grants.
- **Past the last authored `atLevel`, the stride continues forever**, cycling the track's own
  families by ordinal. +24 grants the track's first family again, +28 the second, and so on without
  end.
- Levels below the last authored `atLevel` that the stride would hit but the track does not author
  (+8, +16) grant **nothing**.

**Why this reading and not the other.** The per-base-type track is *content* — it is what makes a
plant stem's milestones differ from a humanoid torso's, and discarding it in favour of a global stride
would make 1,178 authored rows decorative. The stride's own note says its entire purpose is negative:
to stop the ladder being *"a hard stop at +20 dressed as content."* Keeping the track for the authored
range and the stride for everything above it honours both — **the content decides the shape, the
stride guarantees there is no ceiling.**

⛔ **No hard progression ceiling.** A reading where milestones stop at the last authored `atLevel` is
exactly the shape `AGENTS.md` and `enhancement.v1.json`'s own `milestoneNote` forbid. If the owner
overrules §2, the replacement must still be unbounded above.

**Consequence, stated rather than hidden:** under this rule `milestoneStride` no longer reads as
*"every 4th level"* below the last authored rung. Its `milestoneNote` must be amended in the same
change — a `version` bump **inside** `data/tuning/enhancement.v1.json` (verified: the file carries
both `schemaVersion: 1` and `version: 1`), never a new filename.

### 3. Magnitude comes from the existing family expansion, not a new curve

⛔ **No private `f(level)`.** A milestone family is shaped exactly like an affix family —
`kindId` + `channel` + `op` + `powerBand` — which is the input `FamilyExpansion` already consumes. The
fix is to widen **FamilyExpandGen's input set** to include the milestone partition and regenerate, so
`atom.enhance-*` gains the same five tiered rows every affix family has.

The one genuinely new number is **which tier a milestone draws at a given ordinal** — a balance
number, therefore a tunable (§Tunables), never a `const`.

### 4. Append identity, idempotency and replay

- `AtomAppend.Seq` is *"allocated, never reused, never renumbered"* (`MutationOp.cs:96`). The
  allocation must happen **inside the same transaction as the op row**, on the caller-owned connection
  `AppendMutationOpUnlocked` already runs on (`RpgStore.InstanceOps.cs:119-129`) — not in the Server,
  which cannot see the current max seq without a second read.
- **Replay applies the recorded append verbatim and never re-resolves the family** (D2 clause 4,
  `MutationOp.cs:99-104`). A rebalance of the milestone tier ladder must be structurally unable to
  reach backwards into an item a player already owns.
- The ledger's `UNIQUE(instance_id, correlation_id)` idempotency is unchanged; a replayed enhance
  returns the recorded append rather than inserting a second atom row.

### 5. What this module does not do

- It does not touch `EnhancePolicy.Resolve`, the bands, the pity counter, or the gain curve.
- It does not author content. The 9 milestone families no track currently names are a **content
  reading**, not a gap this module closes — if the owner wants them reachable, that is a
  `basetypegen` regeneration, not a code change.
- It does not resolve the `Result.Values` divergence recorded at `ItemWorkbench.cs:229-238`. Same
  seam, different field, separate finding — named here so it is not conflated.

---

## Tech stack

C# .NET 8 (`FusionRpg.Core/Items/Mutation`, `FusionRpg.Data/Sqlite`, `FusionRpg.Server`), xUnit.
Python only for the generator scope change (`tools/FamilyExpandGen` is C#; the milestone corpus's own
generator is `tools/seedsmith`). No new dependency. No FE surface. **SQL only inside `FusionRpg.Data`.**

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Enhance"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Mutation"
dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"
dotnet test tests/FusionRpg.Server.Tests
dotnet run --project tools/FamilyExpandGen -- --check
dotnet run --project tools/ItemSeedValidator
.\scripts\guard-dal.ps1
.\scripts\guard-actor-hub.ps1
python scripts/audit-overflow.py
python scripts/audit-magic-numbers.py --targets M1
```

## Project structure

| Path | Role |
|---|---|
| `src/FusionRpg.Core/Items/Mutation/MilestoneTrack.cs` | **New.** Pure: `(track, level, tuning) → family?`. No I/O, no path, no default |
| `src/FusionRpg.Core/Items/Mutation/EnhancePolicy.cs` | Unchanged — `IsMilestoneLevel:129-131` finally gets a caller |
| `src/FusionRpg.Core/Items/Mutation/EnhancementTuning.cs` | Parses the new milestone tier ladder; rejects a missing section by name |
| `src/FusionRpg.Server/ItemWorkbench.cs` | `:272` stops being `Array.Empty<AtomAppend>()` |
| `src/FusionRpg.Data/Sqlite/RpgStore.InstanceOps.cs` | `:206` gains the append insert beside the suppress update, same transaction |
| `tools/FamilyExpandGen/Program.cs` | `:50` widens from one directory to the milestone partition as well |
| `data/seed/atoms/generated/` | **Regenerated output.** Gains `atom.enhance-*` rows — never hand-written |
| `data/tuning/enhancement.v1.json` | Gains the tier ladder; `milestoneNote` amended; **`version` bump inside the file** |
| `data/seed/items/enhancement-milestones/milestones.json` | **Read only.** Generated (`_runs/enhancement-milestones-gen.ledger.json`) — never hand-edited |

## Code style

```csharp
// The authored track decides WHICH family at WHICH level; the stride only guarantees the ladder
// never ends. Both are shipped and they disagree below the last authored rung (stride says +8 and
// +16, no track authors either) — this ordering is the resolution, not an accident, so it is
// commented where a reader would otherwise "simplify" it back to a bare modulo.
var family = MilestoneTrack.FamilyFor(target.BaseType.EnhanceTrack, levelAfter, _enhancement);
if (family is null)                       // a stride level the track does not author: grants nothing
    return MutationResult.Nothing(...);   // NOT a refusal — the attempt succeeded, it just had no milestone
```

⚠ `MilestoneTrack.FamilyFor` returns `null` for *"this level grants no milestone"* and **throws** for
*"this family does not exist in the injected corpus."* The two are different failures and collapsing
them is how a content gap becomes a silent no-op.

---

## Tunables

| Number | Meaning | Owner |
|---|---|---|
| `milestoneStride` | How often the ladder grants **past the last authored rung**. **Already shipped at `4`** — confirm, and amend its note to match §Design 2 | `data/tuning/enhancement.v1.json`, `version` bump |
| `milestoneTierLadder` — which of `FamilyExpansion`'s five tiers a milestone draws, by ordinal | ⭐ The one genuinely new balance number. A +20 milestone should not be a +4 milestone | Same file |

**Structural (stays `const`, with a comment saying why):** `FamilyExpansion.TierCount = 5`
(`FamilyExpansion.cs:27`) and `ReferenceLevel = 20` (`:31`) — both already `const` with a comment, both
frozen with `bands.v1.json`, and a sixth tier would need a `.t6` row on **every** family. This module
must not turn either into a dial. Likewise `MutationLimits.MutationSeqCap` (`MutationOp.cs:77`) — it
bounds a log's length, not how strong an item may become, and it **throws rather than clamping**.

⚠ **A revision is the `version` field inside `enhancement.v1.json`, not a new filename** — verified:
that file carries `version: 1`. ⚠ **`data/tuning/base-types-gen.v1.json` carries `schemaVersion` but
NO `version` field** (verified), so the same instruction does not transfer to it — and it is a
generator-side file this module has no reason to touch.

⛔ **No hard progression ceiling.** The milestone ladder is unbounded above by construction (§Design 2).
An absolute bound anywhere on this path is **derived and throws**, never clamps.

## Numeric types

- `AtomAppend.Values` is already `IReadOnlyDictionary<string, long>` (`MutationOp.cs:97`) — **`long`
  is the shipped shape and must stay it.** A milestone magnitude is a magnitude on an endless axis.
- Magnitudes are produced by `FamilyExpansion`, whose own contract is *"Every intermediate is `long`,
  widened before multiplying, divided by 1000 last, and overflow throws"* (`FamilyExpansion.cs:18-21`).
  **This module consumes that and introduces no second arithmetic.**
- **Never `float`** for a magnitude: integer-exactness fails at `Θ` = 232, inside normal play, and
  `float` is non-deterministic across runtimes — disqualifying on a hashed, persisted path, which
  `MutationCanonical.StateHash` (`MutationOp.cs:273`) is.
- **Widen before multiplying** (`(long)a * b`, never `(long)(a * b)`); **divide by 1000 last, exactly
  once**; **overflow throws, never wraps** — `checked`, no silent `unchecked`.
- `AtomAppend.Seq` and `atLevel` are small identity/ordinal `int`s — never magnitudes, never
  multipliers.

## ActorHub gate

**Contributes nothing new; consumes nothing.** An appended milestone atom lands on
`effect_instance_atom`, which the shipped equipment path already reads and which already reaches the
Hub as a **registered atom reader** (`AtomDerivedSubsystem`). A milestone atom is an ordinary atom on
an ordinary item — it composes exactly where every other item atom composes.

⛔ **No `*Composer*`, no private ChannelMods combat writer, no mode-local fold.**
`guard-actor-hub.ps1` must stay green. ⛔ **No second place computes the enhanced value** — the card
already composes the scalar gain from the persisted `enhance_level` over the rung's `enhance_cap`
(`RpgStore.ItemCard.cs:256-261`), and a milestone atom is **additive beside** that, never folded into
it. Folding would double-count on every surface that reads the card, which is the exact defect
`ItemWorkbench.cs:229-238` already records for `Result.Values`.

## Testing strategy

**Store tests run in memory**; disk only when the disk is the thing under test; a failed temp-delete
is a **failure**, never `catch { }`.

| Level | What it asserts |
|---|---|
| Unit | ⭐ **A milestone level produces a non-empty `MutationResult.Appended`** — the regression that proves `Array.Empty<AtomAppend>()` is gone |
| Unit | A non-milestone level appends nothing, and that is a **success**, not a refusal |
| Unit | ⭐ **Past the last authored `atLevel` the ladder keeps granting, forever** — asserted at a level far above any authored rung, proving no hard ceiling |
| Unit | A stride level the track does not author (+8, +16 under today's corpus) grants nothing |
| Unit | A track family absent from the injected corpus **throws**, and is not silently skipped |
| Unit | An absent milestone tier ladder section is a **load rejection naming the key** — no default (T5) |
| Unit | `checked` throws rather than wrapping at the magnitude boundary |
| Integration | ⭐ **The appended atom reaches `effect_instance_atom`**, not just the op ledger — the second wiring gap's own test |
| Integration | Replaying the same `(instanceId, correlationId)` appends **once**, returns the recorded atom, and never re-resolves the family |
| Integration | Appended `Seq` is allocated, never reused, never renumbered across several enhancements |
| Contract | Every `enhanceTrack[].family` in the shipped corpus resolves to a milestone `runtimeFamily` — a **join-closure** assertion, computed from the corpus on both sides |
| Contract | Every `atLevel` is positive and strictly ascending within its own track |
| Contract | `MutationOpKind` membership is pinned — **closed vocabulary, pinning is correct**, and this module changes it by **zero** |
| Contract | `FamilyExpandGen --check` and `ItemSeedValidator` green after the regeneration |
| Guard | `guard-dal.ps1`, `guard-actor-hub.ps1` green |

⛔ **No test asserts a population count or generated text.** Not `1,178` base-type entries, not `19`
milestone families, not `10` families currently referenced, not `62` files, and not any authored
`name`, `notes` or `nameKey` string. Every one of those grows or changes when content ships, and the
"fix" would be to bump the number. **Assert the join closes and the envelope holds; print the scale,
never assert it.** (The 2026-09-11 `84 → 904` incident is the precedent, and
`base-types-gen.v1.json`'s own stale *"740"* is this corpus's live example of the same failure.)

## Boundaries

**Always**
- Call the predicate that already exists (`EnhancePolicy.IsMilestoneLevel`) rather than writing a
  second modulo.
- Fix **both** halves — the workbench and the store — in the same change. Either alone ships a lie.
- Keep `MilestoneTrack` pure: no path, no file read, no default. Core never reads a file (T7.2).
- Distinguish *"no milestone at this level"* (`null`, success) from *"this family does not exist"*
  (throw).
- Allocate the append `Seq` inside the op's own transaction.
- Run store tests in memory.
- Run `audit-magic-numbers.py --targets M1` — this module touches the balance surface.
- Commit via MCP `repo-git.commit` with explicit `paths`.

**Ask first**
- ⛔ Any **`MutationOpKind`** member. The enum is closed and its own comment says *"Adding a member is
  ask-first"* (`MutationOp.cs:11`). **This module needs none** — `Enhance` already exists at `:16` —
  but if a reviewer proposes a distinct milestone op kind, that is an ask, and it **queues behind
  `Repair`** (filed first, `deployment-hierarchy-map.md:89`), **then `Elevate`**
  (`spec-rarity-promotion.md`), **then `item-upgrade-tree`'s.**
- Any **`CraftOperation`** member — closed, *"adding a verb here is code"*
  (`CostClassMatrix.cs:11-44`). This module needs none.
- Widening `FamilyExpandGen`'s input set (it changes what `data/seed/atoms/generated/**` contains, and
  CI fails on drift).
- The milestone tier ladder's starting values — they are the feel of the feature.
- ⚠ **An `ssot-enhancement.md` §5.3 amendment if a new `op_kind` is ever proposed.** Noted while
  reading: §5.3's table lists nine `op_kind` values and **does not list `socket-imbue`**, which the
  enum mints at `MutationOp.cs:42-47`. Pre-existing drift, not caused here, named so the next
  amendment reconciles it instead of inheriting it.

**Never**
- ⛔ Hand-edit `milestones.json`, any `base-types/**` partition, or any `data/seed/atoms/generated/**`
  file. All carry generator provenance (`_meta.model` on all 62 base-type files; a run ledger for the
  milestone corpus). **Fix the generator and regenerate.**
- ⛔ Write a private `f(level)` for a milestone magnitude. One power ladder;
  `ssot-power-scale.md` §10 is a closed inventory.
- ⛔ Let the milestone ladder stop at the last authored rung. That is the hard stop
  `enhancement.v1.json`'s own note and `AGENTS.md` both forbid.
- ⛔ Fold the milestone into the card's composed scalar gain. It is additive beside it.
- Re-resolve the family on replay. The recorded append is applied verbatim (D2 clause 4).
- Clamp anything silently. An absolute bound is derived and **throws**.
- Assert a population count or any generated string.

## Success criteria

1. ⭐ An enhancement to an authored milestone level **appends a real atom**, recorded in the op ledger
   **and** present on `effect_instance_atom` — provable on a live item, not only in a unit test.
2. `ItemWorkbench.cs:272` no longer passes `Array.Empty<AtomAppend>()`, and
   `EnhancePolicy.IsMilestoneLevel` has a production caller.
3. `RpgStore.AppendMutationOpUnlocked` applies `result.Appended` in the same transaction as the op row
   and the debit.
4. ⭐ **The ladder is unbounded above** — a milestone still grants at a level far past the last
   authored `atLevel`, asserted by test.
5. Every shipped `enhanceTrack[].family` resolves, asserted as a join over the corpus, not a count.
6. `atom.enhance-*` atom rows exist in `data/seed/atoms/generated/**` because **FamilyExpandGen
   produced them**; `--check` is green and the diff is committed.
7. Missing tuning section, unknown family and unknown tier all **reject by name**; no default anywhere.
8. Replay appends exactly once and never re-resolves.
9. `MutationOpKind` and `CraftOperation` are unchanged — zero members added.
10. Core, Data and Server suites green; `ItemSeedValidator` and `FamilyExpandGen --check` green;
    `guard-dal.ps1` and `guard-actor-hub.ps1` green; `audit-overflow.py` reports no new critical.

## Open questions

1. ⭐ **Stride or track — which decides the granting levels below +20?** §Design 2 recommends
   **track for the authored range, stride forever above it**, because the per-base-type track is the
   content and the stride's only stated job is forbidding a hard stop. **This is answerable now and is
   recommended as decided**; it is listed because a later session will otherwise re-derive it from two
   shipped sources that disagree. If overruled, the replacement must still be unbounded above.
2. **Which tier does a milestone draw at a given ordinal?** **Recommendation: a per-ordinal ladder in
   `enhancement.v1.json`, starting shallow** — a +4 milestone and a +20 milestone should not be the
   same row, and a single flat tier makes the last rung feel like the first. Balance data; one
   `version` bump to move it.
3. **Do the 9 unreferenced milestone families matter?** **Recommendation: no, and not here.** It is a
   content reading (10 of 19 referenced today), and closing it is a `basetypegen` regeneration, not a
   code change. Named so it is not mistaken for a wiring defect.
4. **Does an appended milestone atom count against the item's affix budget?** **Recommendation: no** —
   `ReferenceCheck.cs:285-288` reserves the `atom.enhance-*` stem precisely so a milestone cannot
   collide with an affix family, and the budget is `RerollPolicy`'s prefix/suffix accounting over
   *drawn* affixes. A milestone is granted, not drawn. Confirm against `RerollPolicy.ValidatePostOp`
   (`RerollPolicy.cs:176`) during the build rather than assuming.

---

## Gate status

**DESIGN-GATE §5, honestly.** Read this session: `DESIGN-GATE.md` §2/§3/§5, `tunables-ssot.md`,
`validation-ssot.md`, `ssot-power-scale.md` §10/§11/PS-8, `ssot-enhancement.md` §5.3, `CLAUDE.md`
"Numeric overflow", `AGENTS.md` Hard boundaries, plus both sibling specs in this directory and the
initiative map. Every factual claim above cites `file:line` or names the `python` count that produced
it, and each was verified against **code and shipped data, never a comment** — three claims carried by
comments (the ideal's grep, the `740` note, and *"forge cannot run"*) were checked and **two of them
were wrong**. **The §5 boundary box remains untickable**: this session wrote no `tasks/sessions/*.json`
record, because `/session-start` does not exist in this harness — unchanged from the map's own gate
status, and stated rather than hidden.
