# Module spec: `coverage-assignment` (A-S7)

**Program:** `action-corpus` ([capability map](../action-corpus-map.md) §4). **Depends on:** A-S1
(`distribution-planner`), roster-balance's `usage-stats` (FC1, cross-program). **Consumed by:** A-P1/
A-P2/A-P3 (`general-propose`/`family-propose`/`signature-propose`), at `finalize_candidate` — a small,
named change to each, not a rebuild.

**Status:** proposed 2026-09-06, real finding, not approved for build yet.

---

## 1. Why this module exists — the real finding, not a hypothesis

Roster-balance's own FC3 (`usage-direction`, `spec-usage-direction.md`) built a rendering cue —
`(underused, consider this)` — appended to a family's line in a propose pipeline's brief, so an
underused family is at least *visible* to the model as worth picking. It was proven correct against
FC1's real report and shipped 2026-09-06.

**Then it was run for real, three times, and it did nothing.** `docs/research/action-corpus/
_usage-2026-09-06.json`: all-time used-family count stayed at **exactly 59/100** across three real
batches (rounds 905/906/907, 60 signature drafts, 24 newly accepted) — with the cue rendering
correctly and visibly on every brief (confirmed by direct inspection: ~60 of ~98 options carried the
cue on one real brief, since most of the population genuinely is underused). **A cue the model is
free to ignore is not a guarantee, and this is now measured, not assumed.**

The same underlying defect explains a second real gap: `atom.chill-punisher`/`atom.rot-punisher`
(the two payoff families `pairings.json` names, spec-distribution-planner.md's own closed
deliverable) still show **0 usage** after real runs. `assign_pairing_roles` (`distribution_planner/
derive.py:370-390`) folds a payoff/enabler pairing into a brief's *prompt text* ("Pairing role:
payoff for atom.chill-punisher") and folds the specific enabler family into the brief's *pool*
(`allowedAtomFamilies`) — but nothing ever REQUIRES the model to actually pick either family. It is
the identical soft-cue shape as FC3's own, just older and un-measured until now.

**This module makes coverage a guarantee, not a request.** It is the deterministic-planner
discipline this whole program already applies to category, pairing role, target mode and rung
window — extended to the one axis that was left soft on purpose (constraint 4: never narrow
`allowedAtomFamilies`) and is now shown to need a different kind of enforcement, not a bigger cue.

---

## 2. What this module owns

**Exactly one thing per brief: `requiredFamilies` — a list of 0 or 1 family ids that MUST appear in
the accepted candidate's final `atomFamilies`, regardless of what the model drafted.** Never a
magnitude, never a second roll, never a change to `allowedAtomFamilies` (constraint 4 stays
untouched — this module changes what happens to *already-eligible* families after the model answers,
exactly like `atomFamilyUsageWeights` only ever changed how an eligible family was *described*).

**Two sources for `requiredFamilies`, mutually exclusive per brief:**

1. **A pairing-role brief's own family.** `role: "payoff"` → `requiredFamilies = [pairedPayoffFamily]`.
   `role: "enabler"` → `requiredFamilies = [forcedEnabler]`. Either way, the family this pairing
   exists to prove out becomes the guarantee, not a hope.
2. **Every other brief (`role: "none"`) gets the next family in a round-robin rotation**, ordered by
   CURRENT real usage ascending (least-used first, ties broken by family id) — recomputed fresh from
   the latest FC1 report every round, never a persisted cursor (§5).

A brief never gets both. A pairing brief's own family already IS the coverage target for that slot —
stacking a second, unrelated requirement onto the same small action risks exactly the mechanical
incoherence §6 already accepts as a cost for the round-robin case; there is no reason to pay it twice
on one brief.

---

## 3. ⛔ A real, small schema gap this module's own dependency needs closed first

`assign_pairing_roles` already computes `forced_enabler` (`PairingAssignment.forced_enabler`) but
**never serializes it onto the brief** — `plan_subject` only writes `pairing: {role, pairedPayoffFamily}`
(`distribution_planner/derive.py:602`). This module needs to know WHICH enabler family was forced,
not re-derive it (re-deriving would duplicate `assign_pairing_roles`'s own tie-break logic and drift
silently if that logic ever changes which enabler index it picks).

**Fix, owned by A-S1, additive and backward-compatible:** `plan_subject` also writes
`pairing.forcedEnabler` (the exact `PairingAssignment.forced_enabler` value, `None` for every
`payoff`/`none` role). Every existing consumer that reads `pairing.role`/`pairing.pairedPayoffFamily`
and ignores unknown keys is unaffected. A-S1's own tests gain one row: an `enabler`-role brief's
`pairing.forcedEnabler` equals the family already proven present in its `allowedAtomFamilies`.

---

## 4. The round-robin, precisely

For a "none"-role brief, in one round:

1. Load the full family namespace (`load_family_ids()` — 100 today).
2. Load the latest FC1 report (`usage_direction.weights.latest_usage_report_path` +
   `weights_from_usage_report`'s own sibling reader — this module reads **counts**, not weights;
   a new, small reader over the same report, not a new report shape).
3. Sort the population by `(usageCount, familyId)` ascending — a never-used family sorts before a
   once-used one; ties (all the zeros) sort by id, deterministically.
4. Walk the round's "none"-role briefs **in the plan's own emitted order** (already deterministic —
   `brief.{scope}.{scopeKey}.{ordinal:03d}`, sorted). Assign `sorted_population[i % len(population)]`
   to the i-th "none"-role brief, `i` counting only "none"-role briefs — a global cursor over the
   WHOLE round, never reset per subject. This is what keeps round-robin from repeating
   `distribution_planner`'s own already-found monoculture bug (§7 of `roster-balance-todo.md`'s
   sibling finding): a global cursor spreads assignments across subjects by construction, the same
   property that made restricting pairing to family/general scope the fix there.
5. **Skip forward past a conflict.** Before finalizing assignment `i`, check it against
   `multiplicativePairs` (`data/tuning/action-corpus-run.v1.json`, the SAME table
   `validate_no_multiplicative_conflict` already reads) — if assigning it would recreate a knowingly-
   additive pair as multiplicative against a family the SAME subject's `allowedAtomFamilies` already
   forces or a sibling brief in this subject's own group already required, advance the cursor to the
   next population member instead (reusing `validate_no_multiplicative_conflict`'s own check, never a
   parallel implementation). In the pathological case where every remaining candidate conflicts
   (not reachable with today's 2-pair table and 100-family population, but never assumed away),
   `requiredFamilies` is empty for that one brief, and this is a REPORTED skip, not a silent one.

**No usage report yet** (a fresh checkout, or FC1 has never run): every family is equally due,
population order falls back to `sorted(family_ids)` alone — the honest "everyone starts at zero"
case, never an error and never a guess at synthetic counts.

---

## 5. ⛔ Recomputed every round, never a persisted cursor — restated, because it is the one design choice a downstream session will be tempted to "optimize"

A rotation implemented as a **stored** cursor (round 7 remembers it left off at family index 42) is
the more obvious data structure, and it is the wrong one here, for the same reason `set-charm-gen.v1.json`'s
own tuning note gives for capability/stat pick counts: *"never derive a design proportion from a
snapshot of a generated corpus."* A persisted cursor:

- Drifts the moment a NEW family lands (exactly what happened this session —
  `atom.chill-punisher`/`atom.rot-punisher` arriving mid-program) — a stored cursor has no way to
  notice a family was inserted before its old position and simply never visits it.
- Cannot recover if a round's real acceptance rate makes some assignments never actually land (an
  "unresolved" outcome never gets `requiredFamilies` spliced in at all, §6) — a persisted cursor
  would still advance past that family as though it were covered.

Recomputing fresh from FC1's real, measured, already-committed usage counts means every round's
assignment is **provably correct against real data at the moment it runs**, the identical property
FC3's own `weights_from_usage_report` was built on (`usage-direction-decision` §C3) — reused here,
not reinvented.

---

## 6. The splice — where coverage actually becomes real

At `finalize_candidate` (`general_propose`/`family_propose`/`signature_propose`'s own `derive.py`,
identical shape in all three — the same three call sites the SMOKE BATCH vote fix and FC3's
`usage_weights` threading already touched), **after** the normal vote resolves `atomFamilies`:

```python
if outcome == "accepted":
    required = brief.get("requiredFamilies") or []
    atom_families = sorted(set(voted_atom_families) | set(required))
```

- **Never touches an `unresolved` or `blocked` candidate.** `entry=None` there; there is nothing to
  splice into. Coverage assignment cannot rescue a candidate the vote itself rejected — it only
  augments a candidate that already succeeded on its own.
- **Only ever adds.** `required` is validated against `forbiddenAtomFamilies`/`multiplicativePairs`
  at ASSIGNMENT time (§4 step 5), never at splice time — by the time `finalize_candidate` runs, the
  required family is already known-safe for this brief, so the splice itself is unconditional,
  cheap, and needs no re-validation.
- **Zero model calls.** Exactly one deterministic set union per accepted candidate.

**The honest cost, named rather than hidden:** a spliced family can be mechanically incoherent with
what the model actually drafted — a `movement`-category action gaining an unrelated `utility:
+money per kill` family because that was next in rotation. This is the accepted price of a real
guarantee over a soft cue that provably does not work; `atom.entangling`'s and `atom.bloodletting`'s
own notes already accept an equivalent cost (an authored-but-partial mechanic) for the same reason —
shipping the honest, working half beats withholding the whole thing.

---

## 7. Where this sits in the pipeline

```
A-S1 (plan, now also carries pairing.forcedEnabler) ─► A-S7 (adds requiredFamilies per brief)
         ─► A-S2 (brief-assembly, unaffected -- copies unknown fields through byte-identical)
         ─► A-P1 / A-P2 / A-P3 (finalize_candidate reads requiredFamilies, splices on accept)
```

A-S7 reads A-S1's own plan file (`_briefs/round-<n>.json`) and the latest FC1 report, and **writes
back to the same plan file** (or `--plan-out`, defaulting to the input path) — the identical
file-handoff shape A-S1→A-S2 already uses, so A-S2 needs **zero changes**: its own docstring already
states every non-`familyActions` field passes through byte-identical, and `requiredFamilies` is
exactly such a field.

**Zero model calls, permanently** — same class of module as A-S1/A-S2/A-S5, pure arithmetic over
already-committed files.

---

## 8. Acceptance criteria

1. A "none"-role brief's `requiredFamilies` is deterministic and reproducible: the same plan +
   the same FC1 report produce byte-identical assignments across two runs.
2. Every family in the population is assigned at least once within `len(population)` "none"-role
   briefs, proven by construction (the round-robin walk), not sampled.
3. A pairing-role brief's `requiredFamilies` always equals its own pairing family
   (`pairedPayoffFamily` for payoff, `forcedEnabler` for enabler) — never independently
   round-robin-assigned.
4. A required family that would recreate a `multiplicativePairs` conflict is skipped for that slot,
   never spliced anyway — proven by a planted-violation test using the real `atom.keen-edge`/
   `atom.cruelty` pair.
5. `finalize_candidate`'s splice never fires on an `unresolved` or `blocked` outcome.
6. Omitting `requiredFamilies` from a brief (an older plan, or A-S7 never ran) produces byte-identical
   output to today — the same additive-discipline proof `family_glossary`/`usage_weights` already
   established, extended to a third optional field.
7. No usage report present degrades to `sorted(family_ids)` ordering, never an error, never a
   fabricated count.
8. A real, measured check after one real round: the never-used list (FC1's own report) strictly
   shrinks — not asserted as a fixed target number (population and acceptance rate both move), but
   never flat across a round that ran real "none"-role briefs, which is the exact failure this module
   exists to fix.

---

## 9. Testing strategy

- **Model-free throughout.** No transport import anywhere in this module — tests drive
  `assign_required_families(plan_entries, usage_counts, family_ids, pairing_table,
  multiplicative_pairs)` (pure) directly, never a live CLI call.
- **Byte-identical rerun**, proven by hash, same discipline as every other model-free module in this
  program (A-S1/A-S2/A-S5's own determinism tests).
- **Planted violations, named explicitly:**
  - a required family colliding with `multiplicativePairs` is skipped, and the report says so, never
    silently dropped;
  - a synthetic all-population-conflicts case (a fixture, not the real 2-pair table) still returns
    `requiredFamilies: []` rather than raising;
  - an enabler-role brief with a MISSING `pairing.forcedEnabler` (an older, pre-§3 plan file) is
    refused with a message naming the field, never silently treated as `role: "none"`.
- **Real-data integration test**, mirroring FC3's own C3 checkpoint: run the round-robin against
  FC1's real, live, committed report and assert every one of the 41 real never-used families
  (`atom.bloodletting`, `atom.chill-punisher`, ... — the exact list already named in
  `docs/research/action-corpus/_usage-2026-09-06.json`) appears in `requiredFamilies` at least once
  within the first 41 "none"-role briefs, in the report's own ascending-usage order. Zero model
  calls, real data, the same "prove the mechanism against committed evidence" shape roster-balance's
  own C3 already used.

---

## 10. Boundaries

- **Never a new roll.** This module assigns and splices; it never calls `Instantiator` and never
  invents a second resolution path. Law 1.
- **Never a magnitude.** `requiredFamilies` is a list of ids. Law 2.
- **Never widens `allowedAtomFamilies`.** The required family is ALWAYS already a member of the
  brief's own pool (the pool is the full namespace, constraint 4) — this module changes which
  already-legal option gets included, never which options exist.
- **Never rescues an unresolved vote.** Coverage assignment augments a success; it does not create
  one. An "unresolved" brief stays unresolved, honestly, exactly as today.
- **Never a persisted rotation state.** §5 is binding: recompute from FC1's real report every round.
- **Ask first:** whether `requiredFamilies` should ALSO retrofit onto `general-propose`/
  `family-propose`'s own already-shipped rounds (901-904) retroactively, versus applying only
  forward from the round it lands in. Not decided here — a real content decision, not an
  architecture one.
