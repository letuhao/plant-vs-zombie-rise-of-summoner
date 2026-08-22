# Item enrichment — reconciliation plan (waves R1–R4)

**Status:** Plan, 2026-08-22, approved by the owner. Follows the thirteen-lane enrichment round whose
outputs are the `ssot-*.md` files in this folder.

**[enrichment-contract.md](enrichment-contract.md) remains binding on every document produced here** —
the terminology lock, the nine shared rules, the reserved `container_kind` values, and the ten boundary
cuts all still apply. This plan adds waves; it does not amend the contract.

---

## Why there is a second round

The thirteen lanes each argued their own case and **nobody cross-examined any of them.** They also
produced, between them, thirteen claims of defects in shipped code — every one read from source, none
executed, because every lane honestly left the design gate's *"I tested the constraint"* box unticked.

And the round exposed gaps in its own scope: four mechanisms that no lane owns.

So: verify the claims, settle the contested questions, fill the gaps, then reconcile.

---

## R1 — Verify (1 agent)

**Deliverable:** `defect-register.md`

Runs the suites and checks every defect claim against code. Confirms, refutes, or marks needs-repro,
with `file:line`, a minimal repro where one exists, and a fix-size estimate. **No design, no fixes.**

This is the cheapest agent in the plan and the one the design gate's evidence rule 4 exists for.

## R2 — Debate (4 agents)

Each takes one contested question, reads both sides from the lane docs, and returns a decision **with
the losing argument stated**. A debate that does not say what it rejected has not been held.

| Id | Question | Deliverable |
|---|---|---|
| **D1** | Durable ownership: add an `actor:{instanceId}` scope to E6, or adopt the assign/bind projection split? | `decision-d1-durable-ownership.md` |
| **D2** | The mutation contract: what reproducibility can honestly be promised without catalog archiving? | `decision-d2-mutation-contract.md` |
| **D3** | Cost and rarity rebase: shard-per-band vs shard-per-rung; the two `pool_rolls` sources of truth; the `CurveInput.Rarity` conflict | `decision-d3-cost-rarity-rebase.md` |
| **D4** | Content budget and gear cadence: total the authoring load, recommend a v1 cut, and answer how long an item stays relevant | `decision-d4-content-budget.md` |

## R3 — Gap lanes (4 agents)

Mechanisms no lane owned. Same 10-section shape, same contract.

| Id | Lane | Deliverable |
|---|---|---|
| **G1** | **Uniques as a content class** — the one that breaks the generator's rules on purpose | `ssot-uniques.md` |
| **G2** | **Consumables** — and the action-layer seam they actually need | `ssot-consumables.md` |
| **G3** | **The presentation contract** — atoms to readable item text, in two frame vocabularies | `ssot-presentation.md` |
| **G4** | **Item-granted actions** — the shape of the `grants_action_id` seam | `ssot-granted-actions.md` |

## R4 — Reconcile (owner session, not an agent)

An index for this folder, the defect register handed to the effect-atom program, targeted edits to
[../item-ideal.md](../item-ideal.md) where lanes corrected it (chiefly §6.2 and §6.4), and one master
decision list.

---

## Not in this plan: the fixes

Nine of the thirteen defect claims are against **shipped, green, tested code**. Several touch E6 and E5,
which are ask-first. The orphan-sweep fix is a behaviour change that could collide with the
golden-ordering hazard in [../decisions.md](../decisions.md).

**That is a build, and it needs the owner's authorization separately from a design round.** R1 produces
the register that makes the decision cheap.

---

## Standing rules for every agent in R1–R3

- Read [enrichment-contract.md](enrichment-contract.md) first. It is binding.
- **No web search.** Own knowledge only; mark recalled numbers as unverified.
- Write **exactly one file**, the path named in the brief. Never edit `item-ideal.md`, the contract,
  this plan, or another agent's file.
- **No git write commands.** Read-only git is fine.
- Cite `file:line` for every claim about this repo's code.
- R2 debates must name what they rejected and why. R3 lanes must fill the
  *"what this lane needs from other lanes"* section properly.
