# Spec: Socket allowance by item kind (`socket-allowance-by-kind`)

**Initiative:** `species-gear-chain` ([map](../species-gear-chain-map.md)) · **Module:** `socket-allowance-by-kind`
**Owning program:** `item` module 16 (`sockets`)
**Status:** spec, 2026-09-13. Awaiting owner approval. No build authorized.
**Source ideal:** [species-craft-ideal.md](../species-craft-ideal.md) § Socket caps by item kind

---

## Objective

**Make socket allowance a property of what kind of item something is, expressed as a tunable table.**

> **Owner, 2026-09-13:** *"add new cap for them, tunable — so we have multiple socket cap for each
> type of item. This reversed for unique item… (boss item)."*

| Item kind | Allowance | Rationale |
|---|---|---|
| **Ordinary** | the base's own maximum | unchanged — today's behaviour |
| **Set piece** | **reduced, tunable — not capped at 1** | the trade for a set bonus. D2 capped set/unique/rare at exactly one socket via Larzuk, which is genre-proven but would devalue 910 shipped sets; a tunable reduction keeps some socket play |
| **Unique / boss item** | ⭐ **reversed — *more* than the base maximum** | these are the trophies of world events and boss raids; extra sockets are what distinguishes them, rather than a bigger number |

**Why a table rather than a rule.** A single *"set pieces get fewer"* rule cannot express the
inversion, and **the inversion is the interesting half**: the same mechanism that makes a set piece a
*commitment* makes a boss item a *prize*. One tunable table says both, and adding a fourth item kind
later costs a row.

---

## What exists today — verified against code and the shipped tuning file

### Built — and there are already **four** layers, not one

⚠ An earlier draft said three. The base type's own `socketMax` is a real fourth layer
(`socketCeilingNote`: *"A base type may declare its OWN socketMax anywhere in [0, its role's
ceiling]… module 6 measured it 740 times across the shipped corpus"*). **So this module adds a
fifth**, not a fourth. `data/tuning/sockets.v1.json` (currently `version: 2`, owned by module 16)
already governs socket count at:

| Layer | Key | Shape |
|---|---|---|
| 1. Rarity roll | `rarityGrant` | Per-rung `{socketMin, socketMax}` — the inclusive window the count is rolled from at drop, **before** the base type's own `socketMax` clamps it |
| 2. Role ceiling | `socketCeiling` | 15 role rows (`armament-primary` 4, `core-guard` 4, `ward-array` 3, … `jewel-major` 1) |
| 3. Structural | `structuralCeiling: 4` | Absolute |

- **The structural ceiling is enforced, not advisory.** `SocketTuning.cs:148-152` throws a
  `SocketTuningRejection` if the file's `structuralCeiling` disagrees with
  `SocketLimits.SocketMaxCeiling = 4` (`:41`), with the reason stated in the message: *"the ceiling is
  structural and mirrored in code, so the two move together in one reviewed change or the file and
  the runtime disagree silently."*
- **`SocketLimits.SocketMaxCeiling` correctly documents itself as structural, not a progression
  ceiling** — *"a LEGIBILITY limit on one item's recipe shape… It bounds nothing that grows"* — and
  notes a tuning row above it **throws at load, never clamps**, *"because a clamp turns 'this role
  quietly lost a socket' into a bug with no symptom."*
- **The parser has no defaults.** *"No key has a default. A missing section throws at load rather than
  resolving to a silently-invented socket count, price or shape."* That is `tunables-ssot.md` T5, and
  the new table must match it.
- `SetExclusivityValidator.cs:33` is the only set-aware craft rule today; `:41` `MaySocket => true`.

### Real gap

There is **no item-kind dimension anywhere** in the socket layer. A set piece and an ordinary item of
the same role and rung get identical allowances.

### ⭐ The constraint, sharper than the ideal stated it

The ideal notes the inversion *"cannot exceed 4 until the eight-socket topology lands."* Reading the
shipped `rarityGrant` table makes that much more pointed:

| Rung | `socketMax` |
|---|---|
| `sunwoven` | **4** |
| `almanac` | **4** |
| `heirloom` / `firstseed` | 3 |
| `fused` / `chimeric` | 2 |

**`sunwoven` and `almanac` are already at 4 — the structural ceiling.** Unique and boss items are
precisely the items that sit at those rungs.

⛔ **So the inversion is a no-op today for exactly the items it is for.** `+1` on an `almanac` unique
resolves to 4, the same as without it. It only bites at `heirloom` and below.

**This is not a reason to drop the inversion** — it is a reason to spec it honestly. Two consequences:

1. The table ships and is **correct**, and becomes *effective* when the decided-but-unapplied
   eight-socket topology (`decisions.md:135`) lands. The spec must not claim a player-visible change
   at the top rungs.
2. **The clamp must throw or be visible, never silent.** `SocketLimits`' own comment gives the reason:
   a silent clamp is *"a bug with no symptom."* An inversion that quietly evaporates at `almanac` is
   exactly that bug.

---

## The composition order — the real design decision

Adding a fourth layer requires saying **where** it applies. Three positions are possible and they are
not equivalent:

| Position | Effect | Verdict |
|---|---|---|
| Before the rarity roll (shift the window) | Changes `socketMin` too — a reduced set piece could roll *below* its rung's floor | ✅ **Chosen.** It is the only position where "reduced" and "increased" are symmetric, and where the allowance is a property of the item rather than of the roll's outcome |
| After the roll, before the role clamp | Adjusts the rolled count | ❌ Rejected — a post-hoc adjustment makes the rolled value a lie, and re-rolling is not idempotent |
| After the role clamp | Adjusts the final number | ❌ Rejected — it can exceed the role ceiling, which is a stated invariant |

**Chosen order, stated as the contract:**

```
rarityGrant window  →  shifted by socketAllowanceByKind  →  roll
                    →  clamped by the base type's own socketMax
                    →  clamped by the role's socketCeiling
                    →  bounded by structuralCeiling (THROWS on a tuning row above it)
```

### ⛔ Four rejections, not one — the derived table must satisfy every invariant the authored one does

An earlier draft named only the inverted-window case. **The shift produces a *derived* window table
per kind, and nothing re-validates it against the invariants `SocketTuning` already enforces on the
authored table.** All four are load rejections naming the kind and rung:

| # | Rejection | Why |
|---|---|---|
| 1 | **Inverted** — `shiftedMax < shiftedMin` | A reduction may narrow a window, never invert it |
| 2 | ⭐ **Non-overlapping** | `SocketTuning.cs:282-300` refuses a non-overlapping authored table, and `rarityGrantNote` says why: *"Adjacent windows OVERLAP by design (OD4)… so socket count **never becomes a strict ladder**."* A per-kind shift can break OD4 even when every individual window is valid |
| 3 | ⭐ **Non-monotonic** | Same parser check, same reason |
| 4 | ⭐ **Negative** | `SocketTuning.cs:268` reads `NonNegative(row, "socketMin")` from the **file**; the derived shift is not covered. `chaff`/`sprout` are `{0,0}`, so a `set: -1` delta yields `[-1..-1]` — **not inverted, and therefore passing the only check the earlier draft specified** |

⛔ **OD4 is the load-bearing one.** Open question 2 (shift `socketMin`, `socketMax`, or both) is
precisely the knob that decides whether overlap survives, and it must be answered against OD4 rather
than on symmetry alone.

---

## Tech stack

C# .NET 8 (`FusionRpg.Core/Items/Sockets`), xUnit. No new dependency. No FE surface.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Socket"
dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~ItemSocket"
dotnet run --project tools/ItemSeedValidator
dotnet test tests/FusionRpg.Guard.Tests
```

## Project structure

| Path | Role |
|---|---|
| `data/tuning/sockets.v1.json` | Gains `socketAllowanceByKind`; `version` 2 → 3 |
| `src/FusionRpg.Core/Items/Sockets/SocketTuning.cs` | Parser + the new rejections |
| `src/FusionRpg.Core/Items/Sockets/` (the grant path) | Applies the shift before the roll |
| `tests/FusionRpg.Core.Tests/Items/Sockets/` | Composition-order and rejection tests |

## Code style

Fail closed, and name the kind in the message — matching the parser's existing voice:

```csharp
// socketAllowanceByKind shifts the rarity WINDOW before the roll (the only position where
// "reduced" and "increased" are symmetric). A shift that inverts the window is a LOAD
// REJECTION, never a silent clamp: SocketLimits' own note is that a clamp turns
// "this item quietly lost a socket" into a bug with no symptom.
if (shiftedMax < shiftedMin)
    throw new SocketTuningRejection(
        $"socket tuning: socketAllowanceByKind['{kind}'] shifts rung '{rung}' to " +
        $"[{shiftedMin}..{shiftedMax}], an inverted window — a reduction may narrow a rung's " +
        "window but may never drive its maximum below its minimum");
```

---

## Tunables

| Number | Meaning | Owner |
|---|---|---|
| `socketAllowanceByKind` — one signed delta per kind (`ordinary` 0, `set` negative, `unique`/`boss` positive) | The decided cap table | `data/tuning/sockets.v1.json`, `version` bump |

⛔ **`socketAllowanceByKind` is a tunable on an APPEND-ONLY surface, and the two words fight.**
`rarityGrantNote`: *"APPEND-ONLY in the same sense `ordinal` is — the count derives from the rung row
**at the instance's recorded `catalog_revision`**, so a silent edit re-sockets every item ever dropped
at that rung."* The new table feeds the same derivation, so **a post-ship `set: -1 → -2` silently
re-sockets every set piece ever dropped.**

**No build-time test catches this.** Therefore:

- `socketAllowanceByKind` rows are **append-only and pinned to `catalog_revision`**, exactly as
  `rarityGrant` rows are. A balance pass adds a revision; it never edits a shipped row.
- It is listed under Tunables because a balance pass *chooses* its values — **once**, per revision.
  That is a different contract from an ordinary tunable, and it is stated here because the distinction
  is invisible at the call site.

**Structural (stays `const`, with a comment saying why):** `structuralCeiling` / `SocketMaxCeiling = 4`
— already correctly marked and enforced; `strainSplice.ingredientCount = 4`
(D20, a reviewed content decision, and the parser already refuses a value above the ceiling).

⚠ **A revision is the `version` field inside `sockets.v1.json`, not a new file.** The file is already
at `version: 2`, having been re-owned by module 16 from module 6. The ideals' `sockets.v{n}.json`
phrasing would have created a second file. **`rarityGrant` is append-only in the same sense `ordinal`
is** — the count derives from the rung row at the instance's recorded `catalog_revision`, so editing a
shipped row **re-sockets every item ever dropped at that rung**. The new key is additive; no existing
row may be edited.

## Numeric types

Socket counts are small `int`s bounded by `structuralCeiling = 4`. **No magnitude is produced or
consumed** — the socket layer explicitly *"never reads `contentScale`"* (`SocketLimits` comment). The
deltas are signed `int`s in the range that keeps every rung's window valid; there is no overflow
surface here, and this is stated so no later reader assumes the magnitude rules were overlooked.

## ActorHub gate

**N/A and checked.** Socket *count* is a capacity, not a combat number. What a socketed insert
contributes composes through the existing atom path into `ActorHub`, unchanged by this module. **No
private fold is introduced.**

## Testing strategy

| Level | What it asserts |
|---|---|
| Unit | **Composition order** — the shift applies to the window before the roll, then the base `socketMax`, then the role ceiling, then the structural bound |
| Unit | A set-piece reduction narrows the window and never drives `socketMax` below `socketMin` — the inverted case **throws**, naming kind and rung |
| Unit | ⭐ **The inversion is visibly bounded at `almanac`/`sunwoven`** — the result is 4 and the bound is *reported*, not silently swallowed |
| Unit | A tuning row above `structuralCeiling` still **throws at load** — the existing guarantee is not weakened |
| Unit | An absent `socketAllowanceByKind` section **throws** — no key has a default (T5) |
| Unit | An unknown item kind in the table is a load rejection — the kind set is a closed vocabulary |
| Contract | Every rung in `rarityGrant` still has a valid window after every kind's shift, for all kinds × all ten rungs |
| Regression | An **ordinary** item's socket count is byte-identical to today for every rung and role |

⛔ **No test asserts how many sockets any shipped item has.** Item counts are readings. The
assertions are on the **composition contract** and the **closed kind vocabulary**.

## Boundaries

**Always**
- Fail closed with a message naming the kind and rung.
- Keep `structuralCeiling`'s existing throw-never-clamp behaviour.
- Treat `rarityGrant` rows as append-only — editing one re-sockets every item dropped at that rung.
- Commit via MCP `repo-git.commit` with explicit `paths`.

**Ask first**
- The kind vocabulary itself (`ordinary` / `set` / `unique` / `boss` / … ) — it is closed, and adding
  a member is a reviewed change.
- Raising `structuralCeiling`. That is the eight-socket topology, `decisions.md:135`, and
  `strain-splice-host` owns its migration order — **not this module's to apply.**
- Changing any shipped `rarityGrant` row.
- ⛔ **Changing any shipped `socketAllowanceByKind` row.** Same append-only / `catalog_revision`
  contract, same silent-re-socket consequence — and no test will catch it.

**Never**
- Silently clamp an inversion to the structural ceiling. That is the "bug with no symptom" the
  existing comment warns about.
- Cap set pieces at exactly one socket. Genre-proven in D2, but it would devalue 910 shipped sets, and
  the owner decided a **tunable reduction** instead.
- Spec the inversion against headroom that does not exist yet.
- Let this module design the unique / boss / void item kinds. They are **reserved** (below).
- Assert a population count.

## Reserved, not designed — three item kinds

> **Owner, 2026-09-13:** *"mention unique item and boss item, void item — we will defer them, not
> design or ship in this program, reserved for the future program."*

| Kind | Source | Status |
|---|---|---|
| **Unique item** | world event | **Reserved.** ⚠ Item **module 17 `uniques`** already exists (*"hand-authored items that break generator rules"*) — so this is plausibly a **new acquisition route onto an existing kind**, not a new kind. Reconcile before either is specced |
| **Boss item** | a 4-party boss raid | **Reserved.** No raid content type exists today |
| **Void item** | dropped by a **void beast** via a **void raid** — the void sieging a player-held sector | **Reserved for its own program.** The only reserved kind whose source is *defensive* — the content comes to you |

**None is designed, specced or shipped here.** They appear because the table must leave room for them,
and because naming a kind early is cheaper than discovering it inside a spec.

## Success criteria

1. `socketAllowanceByKind` exists in `sockets.v1.json` at a bumped `version`, with a row per kind.
2. The shift applies to the rarity window **before** the roll, in the specced order.
3. An inverted window, an unknown kind, and an absent section each **throw at load**, naming the cause.
4. The structural ceiling's throw-never-clamp behaviour is intact and tested.
5. An ordinary item's socket count is unchanged for every rung and role — proven by regression.
6. The inversion's boundedness at `sunwoven`/`almanac` is **reported**, not hidden.
7. No shipped `rarityGrant` row edited.
8. Core, Data and Guard suites green; `ItemSeedValidator` green.

## Open questions

1. **Is the delta signed-integer or a per-mille multiplier?** **Recommendation: a signed integer
   delta.** Socket counts are 0–4; a per-mille on a four-point scale rounds to the same handful of
   values while being far harder to read in a tuning file.
2. **Does the set reduction apply to `socketMin`, `socketMax`, or both?** **Recommendation: both** —
   shifting only `socketMax` narrows the window asymmetrically and makes low rolls relatively more
   common, which is a different design than "fewer sockets."
3. **Do `unique` and `boss` share one row or get two?** **Recommendation: two rows with identical
   values today.** They are different acquisition routes and will diverge; one row now means a
   migration later, and a duplicate row costs one line.
