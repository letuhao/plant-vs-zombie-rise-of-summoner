# Spec: units-correction (E42)

**Status: DRAFTED 2026-09-03** — added by the spec-coverage audit, which found this **cited as a
prerequisite by two specs and owned by none.** Module **E42**, Wave 7. **No dependencies.**

**What it owns: correcting the units row in the document that outranks every spec.** The item program
proved on 2026-08-22 that `combat.power.*`, `combat.defense.*` and `combat.shield.*` are **flat game
units**, not resolver points. `definitions.md` still carries the wrong row, and `DESIGN-GATE.md` makes
that file **win over any spec**. E30 and E38 both author magnitudes from it.

---

## 1. Why a documentation fix is a module

It would normally be a one-line correction. Three things make it a spec:

1. **`DESIGN-GATE.md` gives `definitions.md` precedence over every spec.** A spec that contradicts it
   loses, so no downstream spec can fix this by being right.
2. **It is a prerequisite of two modules that author magnitudes at scale.** `spec-channel-pool.md` names
   it in its own hazards table (*"this module prices those channels — correct it before authoring
   magnitudes"*), and E38 extends the primary channel set from the same reference.
3. **A units error does not fail a test.** A magnitude authored in the wrong unit is a plausible-looking
   number that passes every schema audit, every guard and every build — the exact defect class Law 2
   describes: *"A wrong enum is visible. A wrong number is not."*

**It has been owed since 2026-08-22 and survived four adversarial passes** because every pass recorded it
as a hazard and none owned it. That is the pattern this module exists to break.

---

## 2. What exists today

| Thing | State | Evidence |
|---|---|---|
| The handoff, with the proof | **delivered 2026-08-22** | `docs/architecture/item/atom-layer-handoff.md` §1 — `CombatProbabilityPolicy` declares **no** `PowerScale`/`DefenseScale`, so those channels cannot be resolver points |
| The wrong row | **still shipped** | `docs/architecture/effect-atom/definitions.md` §2 |
| The same error, repeated in a worked example | **still shipped** | `docs/architecture/effect-atom/atom-family-library.md` §2a |
| `definitions.md` outranks every spec | **binding** | `docs/DESIGN-GATE.md` |
| Two modules author magnitudes from these documents | **specced** | `spec-channel-pool.md` (E30), `spec-entity-fields-12plus.md` (E38) |

**Sorted: real gap.** Not inert wiring — a correction that was handed over, accepted, and never applied.

---

## 3. The contract

1. **Correct `definitions.md` §2's units row** to state that `combat.power.*`, `combat.defense.*` and
   `combat.shield.*` carry **flat game units**, citing `atom-layer-handoff.md` §1 and the
   `CombatProbabilityPolicy` evidence — **the reason, not just the verdict**, so the next reader can
   check it rather than trust it.
2. **Correct `atom-family-library.md` §2a's worked example**, which repeats the error verbatim. A
   corrected rule beside an uncorrected example is worse than neither: the example is what gets copied.
3. **Keep the wrong row visible as a correction, not a silent overwrite.** This repo's convention
   throughout — a struck claim with its date and reason, so a session that remembers the old value learns
   it moved rather than doubting its memory.
4. **Sweep for the same claim elsewhere.** Both files are cited widely; any other document asserting
   resolver points for these families is corrected in the same pass.
5. **Add a doc-drift test** pinning the units statement to the shipped policy type, so it cannot silently
   revert. `AtomCatalogSsotDriftTests` is the local precedent — a doc that drifted for **168 channels**
   because nothing watched it.

---

## 4. What this module must NOT do

- **Change a coefficient, a magnitude, or any shipped number.** This corrects a *description*. If a
  shipped magnitude turns out to be authored in the wrong unit, that is a **finding for the owning
  program**, not a fix this module makes.
- **Change `CombatProbabilityPolicy` or any resolver.** The code is correct; the document is wrong.
- **Silently overwrite.** Rule 3.
- **Widen scope to other rows in §2.** Only the families the handoff proved.

---

## 5. Testing strategy

| # | Test | Proves |
|---|---|---|
| 1 | A doc-drift test asserts `definitions.md` §2 states **flat game units** for the three families | The correction cannot revert |
| 2 | **Planted violation:** restoring the phrase *"resolver points"* for those families **fails** | A drift test that cannot fail is not a guard |
| 3 | `atom-family-library.md` §2a's example agrees with §2, asserted by the same test | Rule 2 — the example is what gets copied |
| 4 | No shipped magnitude, coefficient or content hash moves | Rule 1 — this is a description fix |

**Test 2 is the one that matters.** The original error survived four months and four adversarial passes
precisely because nothing asserted it.

---

## 6. Acceptance criteria

1. `definitions.md` §2 states flat game units for `combat.power.*`, `combat.defense.*`,
   `combat.shield.*`, citing the handoff and the `CombatProbabilityPolicy` evidence.
2. `atom-family-library.md` §2a's worked example agrees.
3. The prior claim is struck with its date and reason, not deleted.
4. A drift test pins both, with a planted-violation companion.
5. No number, hash or golden moves.
6. E30's and E38's hazard rows are updated to cite this module as **closed** rather than as an open
   prerequisite.

---

## 7. Dependencies and cross-program hazards

| | |
|---|---|
| **Depends on** | Nothing. Should land **before** E30 and E38 author magnitudes |
| **Blocks** | **E30** `channel-pool` and **E38** `entity-fields-12plus` — both author magnitudes from these documents |
| **item program** | Authored the handoff. If a shipped item magnitude was authored under the wrong reading, that is **their finding to act on**; this module reports rather than repairs |
| **`DESIGN-GATE.md`** | Its precedence rule is why this cannot be fixed downstream — worth restating in the corrected row itself |
