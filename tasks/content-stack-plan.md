# Plan: content-stack

**One combined plan across three programs** — `effect-atom`, `effect-pipeline`, `action-corpus` — by
owner decision 2026-09-03: *"one combined plan for all the work."*

**Path note.** The bare `tasks/plan.md` / `tasks/todo.md` pair belongs to the perf stream, and
`AGENTS.md` is explicit that they are *"not defaults and not fallbacks."* So this is the prefixed pair
`content-stack-*`, which gives one ordered plan without colliding with another stream.

**Specs:** `docs/architecture/effect-atom/spec-*.md` · `docs/architecture/effect-pipeline/spec-*.md` ·
`docs/architecture/action-corpus/spec-*.md`. **Maps:** the three `*-map.md` files.
**Task list:** [`content-stack-todo.md`](content-stack-todo.md).

---

## 1. What this builds, in one paragraph

An **atom effect pool** that a model can draw from, an **action corpus** generated from it, and the
seam that makes either reach a running game. **48 modules.** ~~Today the machine is built and the
content is 21 demo atoms; and **a player install never imports anything at all**.~~

> **⛔ Both halves of that sentence are now out of date — corrected 2026-09-05, measured.** The atom
> corpus is **66** (20 shipped `fx-*`/trait atoms + E43's 45 generated family rows), confirmed by a
> real `AtomImporter --check --validate` run. And a player install **does** import now: `E46`
> `player-content-boot` shipped, so a clean install self-heals on first launch when
> `catalog_revision` is 0. Real generated ACTION content exists too (45 accepted candidates across
> the general and family scopes as of the 2026-09-05 runs). Recount rather than quote.

> **⛔ One claim in this paragraph was false and is corrected 2026-09-03:** it read *"`effect_binding` has
> zero rows"*. It does not — `ProduceAndBind` is called in production at `RpgStore.UniqueActors.cs:756`.
> **That error is the plan's most serious**, because Gate G2 and the whole of Phase 1 were sequenced
> around it. See §2 G2 and §3 Phase 1. Found by the plan-coverage audit, not by me.

---

## 1a. Where this actually stands — measured 2026-09-05

**44 of 48 modules are closed and independently re-verified.** Four items remain open, and the
distinction between them matters more than the count: three are blocked by something real in the
world, one is blocked by a machine.

| Open item | State | What would actually close it |
|---|---|---|
| **SMOKE BATCH** (G5 crit. 2) | `unresolved` **13.2%**, was 62.3% | A model that agrees with itself more often. The aggregation bug that caused the other 49 points is fixed; what is left is 7 briefs out of 53 where three samples picked six different families with zero overlap |
| **E44** `power-sweep` | 5 of 20 coefficient rows fitted | Content for `arm1/arm2`, or an owner **balance decision** for the rows whose kinds have no magnitude param at all — for those, no amount of data can ever produce a fit (see the todo's own per-row table) |
| **ep-9** `affix-authoring` | slot registry built, correctly inert | An atom family whose variants are real element ids. The registry derives itself and its inertness test goes red automatically the day one ships |
| **Live check → Fix-bug** | not started | A lawn session on the owner's machine (§5). Fix-bug is sequenced behind it by the owner's own instruction |

**The lesson worth keeping** (it cost five attempts on one gate): each of the first three had
previously been written off with a *scoping conclusion* — "no corpus exists", "that would be a
fork", "no vocabulary exists". All three read as airtight prose and all three dissolved on one grep
of the real code. A conclusion that closes off work is exactly the kind that has to be tested
against the tree, not reasoned about.

---

## 2. ⛔ The five gates that set the order

Everything below is ordinary dependency work. **These five are the ones that, if got wrong, waste the
rest.**

| # | Gate | Why it orders things |
|---|---|---|
| **G1** | **`A-E1 eligibility-axis` before any action-corpus stage** | `ActionRow` has no field naming who may hold an action. Every stage can run, commit a corpus, and the game still cannot decide what a species may unlock. **A corpus with no eligibility surface is content nothing can read** |
| **G2** | ⛔ **WITHDRAWN 2026-09-03 — the premise was false.** `ProduceAndBind` **is** called in production (`RpgStore.UniqueActors.cs:756`, inside the live equipment-binding sync), `prefix_rolls`/`suffix_rolls` and `effect_affix` are in the shipped schema (`RpgStore.Containers.cs:28,66`), and `Resolver.cs` exists. **ep-1, ep-2 and ep-4 are built.** `seed-to-concrete-todo.md` T3.1/T3.3/T3.6/T3.7 are all `[x]`, T3.7 labelled *"⭐ THE PROOF — fixture container → instance → binding → AtomRunner executes."* **Only `ActionSeeder.Generate` still has zero callers**, which is the action corpus's own wiring, not a seam gap. See §3 Phase 1 |
| **G3** | **`E42 units-correction` before `E30` or `E38` author magnitudes** | `definitions.md` §2 still calls three channel families *"resolver points"*; they are flat game units. `DESIGN-GATE.md` makes that file win over every spec, and **a units error does not fail a test** |
| **G4** | **`E46 player-content-boot` before any generated content ships** | `AtomImporter` runs from one dev script. A player install boots on the code fallback with the whole content layer inert |
| **G5** | **The smoke batch before any full run** — now **evidence-gated, not owner-gated** | The bar stays; **the owner is no longer the thing standing at it.** Per 2026-09-03: *"i don't want to join the gate — if the gate needs me, remove them."* A full run proceeds when the smoke batch meets the stated criteria in §2a — no one has to say yes. **§17's call budget remains a ceiling, not a plan** |

---

## 2a. ⛔ No gate requires the owner — changed 2026-09-03

**Owner instruction:** *"i don't want to join the gate — if the gate needs me, remove them."*

**So every gate is now one of three shapes, and none of them is a person:**

| Shape | Meaning | Example |
|---|---|---|
| **Dependency gate** | Module B cannot start until A ships. Mechanical | G1, G3, G4 |
| **Evidence gate** | Proceeds when a stated, checkable criterion is met. **Nobody approves it** | G5 |
| **Access task** | Needs the game assemblies or a live lawn — so it needs a *machine*, not a *decision*. **Scoped so it never blocks another module** | E27's sequencing, E37/E39's sweeps, E38's `Z-TAKEMULT`, E40's coin arm |

**The distinction that matters:** an access task cannot be removed — nobody can read the game's
`Assembly-CSharp` from this repo. What *can* be removed is its power to stop everything else. Each one
now states **what to check, what a pass looks like, and what proceeds meanwhile**, so the work queues
instead of halting.

**And every former "owner must decide" is now a stated default with its reasoning and what would
overturn it.** A default recorded with its argument is reviewable and reversible; a question left open
is neither. **If a default turns out wrong, changing it is a tuning row — that is the property each
was chosen for.**

### G5's criteria, stated so no one has to judge them

A full run proceeds when the smoke batch shows **all four**:

1. **Zero schema-audit defects** across the batch — no magnitude, weight, probability or duration in
   any model output.
2. **`unresolved` rate under 10%**, and every `unresolved` carries its named reason.
3. **A byte-identical replay** over unchanged inputs, proven by hash.
4. **The coverage report names its thin cells** — a pass that evaluated nothing is not a pass
   (`NOT_MEASURED` stays distinct from a pass, per A-S5).

**Any one failing means fix and re-run the smoke batch, not escalate.** The numbers above are the
neutral starting bar; they live in `data/tuning/action-corpus-run.v1.json` beside the run size, so
moving them is a config change with a diff.

---

## 3. Phases

Ordered by dependency, not by program. **Model-free work comes first throughout** — by the time the
first token is spent, the pool, the plan, the metrics and the dedup are inspectable against real data.

### Phase 0 — corrections that unblock everything · *all model-free, no dependencies*

`E42` units-correction · `E26` runner-def-emit · `E27` lawn-element-bind · `E28` param-parity ·
`E29` kind-value-guard · `E33` activation-edge · `E47` validate-gate-ci · **`A-E1` eligibility-axis** ·
`A-U1` rung-semantics

**Why together:** none depends on another, each turns a currently-silent failure loud, and three are
prerequisites of Phase 2. **`A-E1` is here because of G1** — it is the founding gap and it gates a whole
program.

⚠️ **`E27` carries a sequencing hazard.** It turns the element axis on for the first time on the lawn,
and both the open **VFX blind-identity trials** and the open **shield live absorb proof** read that exact
path. **Run those before E27 or after — never straddling.**

### Phase 1 — the seam · ⛔ **LARGELY ALREADY BUILT — corrected 2026-09-03**

> **This phase was written against a false premise and is the plan's most serious error.** I wrote that
> `effect_binding` has zero rows and three entry points have zero production callers. **Verified
> otherwise:** `ProduceAndBind` is called at `RpgStore.UniqueActors.cs:756`; the affix schema ships at
> `RpgStore.Containers.cs:28,66`; `Resolver.cs` exists. `seed-to-concrete-todo.md` records T3.1/T3.3/
> T3.6/T3.7 as done, with T3.7 as **the proof**.
>
> **The specs say so too, and they are also stale**: `spec-affix-schema.md` §1 has a table headed
> *"Exists in code today"* reading **no** in all four rows, each with a `file:line` that now says the
> opposite. `effect-pipeline-map.md` repeats it. **Correcting those specs is the first task here.**
>
> **What actually remains** is a re-audit, not a build: confirm each of ep-1/ep-2/ep-4 against its own
> acceptance criteria and record which are met. `ep-1`'s A1 in particular is **not** met —
> `Resolver.cs:60-66` runs two independent draws and its own test asserts only `Assert.NotEmpty`,
> commented *"today's two-independent-draws interim model"*. **A shipped module failing its own
> acceptance is a real finding and it belongs here.**
>
> ⚠️ **`seed-to-concrete-todo.md` and this file both claim these ten modules, in different orders with
> different dependencies, and neither references the other.** Which is authoritative is an owner call.

### Phase 2 — the pool

`E30` channel-pool → `E32` affix-import-path · effect-pipeline module 3 `affix-library` ·
`E43` family-expand · **`E46` player-content-boot**

**`E43` emits ~490 rows** — one per (family, tier), with element as a pool reference. That is the first
real content, and **G4 says `E46` lands before it ships**.

⚠️ **Three CI gates fail on the first generated row**, by construction: the exactly-16-id assertion, the
`fx-*` glob, and `Assert.Empty(compiled.Runtime)`. **Each needs a named change, not a rename to dodge
it.**

### Phase 3 — action corpus, model-free · *nine modules, zero tokens*

`A-C1` corpus-loader · `A-S0` characteristic-pool · `A-T1` type-weights · `A-S1` distribution-planner ·
`A-G1` tier-access-gate · `A-R1` resource-ownership · `A-S5` coverage-report · `A-S3` dedup-select ·
`A-S6` innate-picker

**By the end of this phase the only unknown left is the judgement itself.**

### Phase 4 — the model stages · ⛔ *G5 gate at the end*

`A-S4` validate-heal · `A-P1` general-propose ∥ `A-P2` family-propose → `A-P3` signature-propose

**`A-P3` waits on `A-P2`** — it must differ from its family's output, which is a dependency, not a flag.

**Ends at the smoke batch.** Metrics, defects found, defects fixed — the evidence a full run
proceeds on. **⛔ CORRECTED 2026-09-05: this used to read "the evidence for the OWNER'S decision",
which contradicted §2a on the same page.** Per the owner's own 2026-09-03 instruction (*"i don't
want to join the gate — if the gate needs me, remove them"*) G5 is **evidence-gated**: it proceeds
when the four §2a criteria hold, and nobody has to say yes. The plan still does not schedule past
this point.

⚠️ ~~**The roster is 84 species** (53 with family assignments, 28 in the classified tree, **8 with a
complete four-way anchor**). Eight fully-anchored species is the right size for a first batch.~~
**⛔ SUPERSEDED by measurement 2026-09-04.** The "8 fully anchored" figure was already stale when the
first real batch ran: the anchor tree grew throughout (28→68→87→764 rows, measured minutes apart).
Recomputed at run time from the real four-way join: **24 species across 12 families**, which is what
the real smoke batches actually used. Recompute it, never quote it.

### Phase 5 — movement and capability

`A-M1` movement-payload → `A-M2` lawn-reposition *(needs `E33`)* · Wave 8: `E34` `E35` `E36` `E37` `E38`
`E39` `E40` `E41`

⚠️ **`E35`, `E37` and `E41` all change `AtomKindRegistry`'s counts.** End state is
**`AttachPointCount = 7`, `KindCount = 16`** — stated once, in `spec-match-modify.md` §2.1. Each module
asserts only its own delta.

### Phase 6 — pricing

`E44` power-sweep

**Closes D2 and unblocks C1.** Per the owner: the gate stays but **may be passed deliberately** — *"we
cannot avoid tuning in this game, so that is normal."* **This phase does not block Phase 5.**

### Deferred, with a named reason

~~`E45` derived-write-lawn — *"has a spec and no `decisions.md` row. The ADR is the gate."*~~
⛔ **WITHDRAWN 2026-09-03 — wrong on both counts.** `decisions.md:104` **is** that row —
*"Derived-write lawn executor (2026-08-30)"*, carrying *"Owner decisions, approved 2026-08-30"* — and
the spec's own status line reads **"BUILT and PROVEN LIVE end to end, 2026-08-30"** with a
before/after live table. **Nothing was deferred; the module shipped.** `effect-atom-map.md` repeats the
false claim and needs the same correction. This is the failure class `DESIGN-GATE.md` §4 logs: a
constraint asserted without being tested.

---

## 4. Checkpoints

| | Passes when |
|---|---|
| **C0 — nothing fails silently** | Phase 0. A runner atom no longer throws at grant; a declared param either works or is refused at load; an unknown value in any kind is a load-time refusal. **A planted violation of each fails a test** |
| **C1 — an action can be held** | `A-E1`. `candidates(actor)` returns general ∪ family ∪ species, ordinally sorted, and **a null `scopeKey` matches only general** |
| **C2 — a binding reaches a runtime** | Phase 1. `effect_binding` is non-empty from a production path |
| **C3 — one row, many outcomes** | `E30`. A pooled atom resolves to different channels across two seeds and **identically on replay** |
| **C4 — content reaches a player** | `E46`. A clean install has a non-zero `catalog_revision`, and fallback mode is **reported** |
| **C5 — the plan is reviewable with no model** | Phase 3, against the real 84-species roster |
| **C6 — quality is proven** | The smoke batch meets all four §2a criteria. **Evidence-gated, not owner-gated** (corrected 2026-09-05 — this row used to say "Owner decides", contradicting §2a). **Status 2026-09-05: 3 of 4 criteria met.** Criterion 2 (`unresolved` under 10%) measures **13.2%** after the vote-aggregation fix took it from 62.3%; the residual is 7 genuinely-disjoint three-way model disagreements out of 53 real briefs, verified individually from the per-sample records |
| **C7 — live** | Owner-run lawn check, after the build. **A fix-bug phase follows it** |

---

## 5. What is owner-run, and what CI cannot tell you

**CI never compiles `FusionRpg.Injector`** — it runs ten managed test projects. So every injector-side
module (`E27`, `E28`, `E33`, `E34`–`E41`, `A-M2`, `E46`) needs a **local build and an owner-run lawn
check**, and a green suite is not evidence for any of them.

**Server lifetime:** start the server as a direct `Start-Process`, never from a tool call — a server
started inside an assistant call dies when that call's process tree is cleaned up.

---

## 6. Known residuals carried in, not fixed here

From the Wave 6 retrospective — defects in **shipped** code, recorded in each spec's *Known residuals*:

- **`E21` is lawn-only.** Battle subscribes nothing to `StatusRuntime.OnApplied`, so a `rally`/`expose`
  status still changes no stat there — the same gap E21 fixed for the lawn, one runtime over.
- **`E23` has no `--effect-check`.** A value-only edit to an `fx-*.json` atom leaves the generated
  catalog stale and fails nothing.
- **`E22`** accepts any channel string in code; the refusal lives only in the import transaction.
- **`E25`'s cache** was silently corrected to `AsyncLocal` after a real race, documented only in code.

---

## 7. Rules that apply to every task here

- **`long` for every magnitude, never `float`.** Widen before multiplying, divide by 1000 last, overflow
  throws.
- **A number a balance pass would change lives in `data/tuning/`**, published via `publish.py`.
- **No hard progression ceilings.** Structural limits are exempt **and must say so in a comment**.
- **The LLM writes identity; deterministic code writes magnitude.** Enforced by schema audit, never review.
- **Never a second roll beside `Instantiator`.**
- **Tests never call a model** — stub the transport so it **raises**.
- **Git is hands-off.** The owner commits; this plan never runs a git write command.
