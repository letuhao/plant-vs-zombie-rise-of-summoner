# Plan: backlog-clear — one sequence across the open programs

**Program id:** `backlog-clear` · Tasks: [backlog-clear-todo.md](backlog-clear-todo.md)

This is a **sequencing plan across programs**, not a new capability. Every task below belongs to an
existing program and stays recorded in that program's own todo; this pair is the running order and the
checkpoint set, so work stops being ad-hoc.

**Scope:** S ≈ under an hour · M ≈ a focused session · L ≈ multi-session.

---

## Where this came from

A full sweep of all 18 `tasks/*-todo.md` files on 2026-08-31 found **156 open items**. They sorted into
four buckets, and only one of them was engineering:

| Bucket | Count | Disposition |
|---|---|---|
| Owner decisions | 9 | **Closed 2026-08-31** — 4 decided by the implementer, 5 by the owner (below) |
| Owner review ticks | 22 | **19 ticked 2026-08-31**; 3 remain (world-map Phase 3/7 reviews, overlay-switch criteria) |
| Owner live runs | ~15 | Stay the owner's — deploys, stress scenarios, visual trials |
| Blocked on real play | 7 | class-system P9.3/P9.4 — needs games played, not code (see below) |

### Decisions taken 2026-08-31

| # | Decision | By |
|---|---|---|
| D1 | **`commanderOnly` is deleted** — four seed files + `spec-aura-content.md`. Zero consumers in `src/`; an unauthored second answer to a question aura already answers | Owner |
| D2 | **Aura coefficients derive from `P(Θ)` with an even channel split**, authored as tunables so a balance pass is a file save | Implementer |
| D3 | **Build the production binding producer** — see the correction below | Owner |
| D4 | **Routed forces are NOT changed** — they are still finished off where they stand | Owner |
| D5 | **Zomboss gets a momentum term** — a commitment bonus for continuing last turn's plan | Owner |
| D6 | **Kernel clock stays unscaled** — runs through pause, byte-identical to today's grids | Implementer |
| D7 | **One kernel instance per board**, torn down at `board.end` | Implementer |
| D8 | **U14 win-rate sweep signed off** | Owner |
| D9 | **`KernelPurityScan`'s `var x = 1.5f;` gap is left alone** — tightening could redden unrelated files for no current benefit | Implementer |

### ⛔ The correction D3 rests on — read before starting Phase 1

The aura todo says the production binding producer is *"`effect-atom` **E20-E25**, another program's
named deliverable"* ([aura-skill-todo.md:2019](aura-skill-todo.md#L2019)). **That is wrong**, and it
was believed for three sessions:

- [effect-atom-plan.md:11](effect-atom-plan.md#L11) — *"Wave 6 closed same-day, all six modules:
  **E20–E25 fully built and proven**."*
- E20–E25 are `content-boot`, `status-stat-applier`, `channel-policy-reader`, `content-codegen`,
  `validation-in-ci`, `compose-channel-cache` ([effect-atom-plan.md:113-118](effect-atom-plan.md#L113)).
  **None of them creates a binding.**

**The gap is nonetheless real, and larger than "somebody else's task":**
`RpgStore.Bind` ([RpgStore.AtomInstances.cs:205](../src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs#L205))
has **19 test callers and zero production callers** — verified by grepping `\.Bind(` across `src/`,
`tests/` and `tools/`; the five `src/` hits are BepInEx `config.Bind(...)` and unrelated.

So Phase 1 is a **new, unspecced module** and is therefore **spec-first**. It does not inherit an
approved spec from anywhere, and the design gate applies in full.

---

## ⛔ Unresolved: is the numbering a priority list or a dependency chain?

**The first draft did not say, and that ambiguity caused a real disagreement during execution. This
section records the question and the facts — it does not settle it, because the answer is the
owner's.**

**What was actually done, stated plainly:** Phase 1's spec (BP1) was written and left for review;
**no BP2/BP3/BP4 code was written.** Phases 3, 4, 5, 9 and 10 were then built. That followed the
reading "Phase 1's *own* code waits for Phase 1's review, and independent programs continue" —
taken together with the goal's *"surface them and continue with what isn't blocked"*.

**The other reading is that no phase may start until Phase 1's review clears.** Under it, the
correct action after writing BP1 was to stop entirely. That reading is available in the words
"in phase order", and if it is the intended one, the work in phases 3–10 ran ahead of its gate.

The facts that bear on it, neither of which decides it:

| Phase | Program | Depends on an earlier phase? |
|---|---|---|
| 1 binding producer | aura-skill / commander-surface | — |
| 2 aura finish | aura-skill | **Yes** — AU2 needs Phase 1 |
| 3 kernel drive | battle-timeline | **No** |
| 4 Zomboss momentum | world-map | **No** |
| 5 game-gui deletion | game-gui | **No** |
| 6 loam | loam | **No** |
| 7 combat waves | combat-unification | **No** |
| 8 interactive battles | battle-timeline | **No** |
| 9 actor-hud + housekeeping | actor-hud / action | **No** |
| 10 seedsmith | seedsmith | **No** |

Only AU2 has a technical cross-phase dependency; the rest share no code, no data and no contract
with an `effect_binding` row. **That is an argument for the priority reading, not a proof of it** —
"in phase order" could still be a process rule about sequencing rather than a claim about technical
coupling, and a process rule does not need a technical reason to be the intended one.

**⛔ Owner: which reading applies?** If it is strict sequencing, say so and the remaining phases wait
on each gate in turn. Either answer is workable; the cost of the ambiguity was a long disagreement
that neither reading could end.

**Where a phase genuinely is gated, it says so** — and four such gates were found while executing
(see the section above): Phase 6 on a spec review, Phase 7's E1/E2 on a respec, Phase 8 on unwritten
specs, and Phase 5 on Checkpoint H. Those are recorded per-phase, not inferred from the numbering.

## Ordering principle

Dependency first, then leverage, then noise reduction. Three things drove the order:

1. **Phase 1 unblocks two programs at once.** The binding producer is aura-skill's last real gap *and*
   `commander-surface`'s last unmet cross-program prerequisite — its snapshot changes cannot affect
   lawn stats until a production path creates bindings.
2. **Phase 3 is what the owner actually picked** on 2026-08-31 morning. `P1b`/`P1c` are delivery
   labels on B27/B25; the chain that delivers them is B25 → B26 → B27, and B24's review (now given)
   was the only thing gating it.
3. **Cheap, decided, noise-reducing work goes before large speculative work.** Phases 4–5 are small and
   already decided; Phases 8–9 are multi-session and spec-first.

**seedsmith is deliberately last** — [action-todo.md:1706](action-todo.md#L1706) records it as a
*development tool, built after this program*, and 49 items of tooling ahead of shipped-feature gaps
would be the wrong trade.

---

## ⛔ Gates this plan missed, found by executing it (2026-08-31)

The plan was written after reading the specs for phases 1–5 but **not** those for 6–10, and it said
so. Executing it surfaced four gates the phase table did not record. Corrected here rather than left
for the next session to rediscover:

| Phase | Gate the plan missed | State |
|---|---|---|
| **5 game-gui** | Checkpoint I is *"gated on Checkpoint H"* — the owner's own 2026-08-24 instruction — **not** on the deletion sign-off the plan named | Gate **did** clear (H's last box was the review ticked 2026-08-31). Right answer, wrong reason |
| **5 game-gui** | Its premise is false: six of the seven named components are **live dependencies of their own replacements** | Not executable as written. Only `RosterPage` was dead; deleted |
| **6 loam** | `spec-loam-fe-2.md` is *"Draft — Phase 1 (Specify), awaiting owner review"* with an unticked propagation box | **Blocked on owner review**, exactly like Phase 1 |
| **7 combat-unification** | `spec-battle-enrichment.md` is *"partly superseded and should be rebased after T5"* (`battle-timeline-map.md:112`), and T5 shipped 2026-08-28 | E1/E2 need a **respec first**. **E3 is explicitly independent — "can ship any time"** and is open |
| **8 battle-timeline** | B19–B23 are spec-first and **no spec files exist** | Each needs writing before any code |

**The pattern worth naming:** after phases 3 and 4 shipped, what remains is almost entirely blocked on
**review and respec, not on engineering**. The one exception is **Phase 7's E3 (hybrid payloads)**,
which its own map row clears to ship now.

## What this plan does NOT claim to have read

Honesty about the gate: this session read the design-gate rows for **battle/turns, injector↔game,
performance, effects, stats, and the aura/atom layer**, and verified the Phase 1–5 claims against code.
It did **not** read the specs behind **loam L44–L50, combat-unification waves E1/E2/E3, seedsmith, or
game-gui's deletion criteria**.

Those phases therefore carry **task titles and dependencies only** — no invented acceptance criteria.
Each begins with a "read the spec" task, and its real acceptance lines are written at phase start
against what the spec actually says. A plan that fabricated criteria for nine unread specs would read
more complete and be worth less.

---

## Phases

| # | Phase | Programs touched | Scope | Gate |
|---|---|---|---|---|
| 1 | **Binding producer** (spec → build → live) | aura-skill, effect-atom, commander-surface | L | ⛔ spec review; ⛔ owner live proof |
| 2 | **Aura finish** — delete `commanderOnly`, author containers | aura-skill | M | — |
| 3 | **Kernel drive** — B25 injector half, B26, B27 | battle-timeline | L | ⛔ owner live (B27) |
| 4 | **Zomboss momentum** | world-map | M | ⛔ owner playtest |
| 5 | **game-gui dead-code deletion** | game-gui | S | sign-off already given (D8 batch) |
| 6 | **loam L44–L50** | loam | L | — |
| 7 | **combat-unification E1/E2/E3** | combat-unification | L | version bump + re-bless |
| 8 | **battle-timeline Phases 4–5** — B19–B23 | battle-timeline | L | spec-first, each |
| 9 | **actor-hud backlog** (4 polish items) | actor-hud | S | — |
| 10 | **seedsmith** (49) | seedsmith | L | — |

Checkpoints sit after phases 1, 3, 5, 7 and 10 — see the todo.

---

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| **Phase 1's spec discovers the producer belongs somewhere unexpected** (Server-on-loadout-save vs injector-at-`board.start` vs Cold push through the Funnel) | High — it changes which program owns it | It is spec-first *because* of this. `overlay-control-loops.md` §3 already constrains it: grants reach the bag **Cold → Funnel**, never plugin→Bag, so the answer is bounded before the spec starts |
| **Phase 3's B26 moves a golden** despite the prediction that it won't | Medium | The prediction is written down and B26's acceptance *requires running the suites*, not assuming. `decisions.md`'s Battle time model row already records the bump trigger and the predicted-delta discipline |
| **Phase 7 re-blesses goldens three times** (v3, v4, v5) | Medium | Already the program's own design — each wave carries a `RulesetVersion` bump and a zero-content invariant test, so a wave with no content authored is byte-identical |
| Deleting shipped components in Phase 5 | Medium — irreversible in spirit | Sign-off given 2026-08-31. Deletion criteria demand the full suite green with the deleted files' **tests removed, not skipped** |
| A phase's unread spec turns out to contradict this ordering | Low | Each unread phase opens with a read task; the order is revisited then rather than defended |

---

## Not in this plan

- **Owner live runs** (~15): vfx-v3 + vfx-identity-batch6 trials, shield stress, demon PT7,
  buff-debuff T11, injector-stub W3, actor-hud live polish, world-map playtests, perf 300/600/1000z,
  commander-surface deploy smoke. These need the game on the owner's machine.
- **class-system P9.3/P9.4** — verified this session, not assumed: `ResidualFitLoop` has no
  win-rate-consuming code path, *and* the live corpus holds exactly **one** real matchup
  (`rift-skirmish`/`Precision`, 1 battle, correctly flagged insufficient). No decision unblocks it;
  it needs games played.
- **action A9/A10** — A10 is an owner deferral (built with the board map), A9 waits on A10.
- **world-map Phase 3 / Phase 7 reviews and overlay-switch criteria** — three review ticks not in the
  2026-08-31 approval batch.

## One stale line to fix in passing

[action-todo.md:1705](action-todo.md#L1705) — *"A8's reaction lane — waits on timeline **B6**"* — is
closed by its own evidence. B6 shipped 2026-08-28, and B6's entry records that A8 *"ended up **not**
needing this lane at all; it ships as a stance with riposte-on-release, not a reaction."* Cleared in
Phase 9's housekeeping task.
