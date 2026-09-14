# Capability map: `lawn-combat-wire`

**Ideal:** [lawn-combat-wire-ideal.md](lawn-combat-wire-ideal.md) (idea phase, audited, **D1–D9** settled)
**Status:** boundary approved 2026-09-13; all module specs written; **revised after a two-pass
adversarial audit of the specs themselves** — see "Audit corrections" at the bottom.

> **D8 and D9 are load-bearing and were added after the first map draft.** D8: one attack is one
> action trigger (drives the swing id). D9: an effect-bearing lawn hit is carried and coalesced, never
> dropped. A builder reading only this map previously never learned of them.

---

## Modules

| Module id | Responsibility | Depends on |
|---|---|---|
| `element-cache-invalidate` | `LawnElementResolver._cache` freezes `(side, elements)` per ptr for a whole match; hypno/charm changes side and never invalidates. Fix the invalidation. | — |
| `combat-numerics` | `OverlayCombatCalculator` is `double` throughout, divides by 1000.0 before multiplying, exits on an unchecked `(long)` cast, and `ClampToInt32` silently saturates a magnitude at the Unity boundary. Convert to `long`/per-mille discipline; bound → throw, never clamp. | — |
| `resource-subtick` | Build follow-up **S10.1**: regen accumulates in per-mille per tick with a carried `long` remainder, divided by 1000 exactly once. Unblocks regen in **both** lawn and battle. | — |
| `lawn-hit-attribution` | Record the firing actor (`Bullet.from` / `from_zombie`, or melee `attackerPtr`) instead of `bullet.Pointer`. Unblocks the Hub resolve **and** `entity:{ptr}` grant matching in one change. | — |
| `lawn-hit-entry` | The vanilla hit → drain → `DamagePacket` path, keyed on the **MatchRuntime board fold** (not the FSM). Owns the four gates, liveness/death ordering, ptr-reuse, drop policy, and multi-hit/piercing semantics. | `lawn-hit-attribution`, `combat-numerics` |
| `basic-attack-seed` | Load the shared fallback basic attack from **authored seed data** instead of the hardcoded C# row. Seed authored: `data/seed/actions/authored-basics.json` (`act.attack`, atom family `atom.fx-overlay-damage`). Needs: the `Program.cs` loader to include it, `ActionCorpusBriefJson` to read `kindHint`, `ActionCorpusComposer` to honour it instead of hardcoding `Skill`, and Kind-aware cost resolution (Basic → `stamina`, not the category template's `qi`). | — |
| **`lawn-action-bridge`** | **[audit ×2] Scope corrected — the first version was built on a misread grep.** The injector **already** references `FusionRpg.Core` (all host csprojs); the zero-grep measured *usage*, not reachability. And a compiled row **cannot** be transported (`CompiledAction.Condition` is an `ICompiledPredicate`, behavioural). Real scope, much smaller: promote `BasicAttackCompiled` from a private field in `BattleRunState.cs:64-81` into **one public factory** both callers share, and **configure `ActionTimingPolicy` injector-side** — without it the first touch **throws** (`ActionTimingPolicy.cs:15-17`; only `Configure` caller is `Server/Program.cs:223`). No transport, no cache, no trigger set. **Cost delivery is explicitly NOT solved here** — three shapes offered, decided in `basic-attack-cost`. | `basic-attack-seed` |
| `basic-attack-grant` | Bind `ActionKind.Basic` to every lawn actor at spawn so `HasOnDamageDealtGrant()` is true; carry a real `elementPayload` from the owner's species element, sourced at bind from `LawnElementResolverHost`. | `lawn-hit-attribution`, `element-cache-invalidate`, `lawn-action-bridge`, **`lawn-hit-entry`** |
| `basic-attack-cost` | Make `act.attack`'s cost **authored data** rather than a hardcoded empty array; seed `resource.max.*` on the lawn Hub; add regen as a third kernel kind; add the lawn's missing `CostLedger` call. | `resource-subtick`, `basic-attack-grant` |
| **`lawn-combat-calibration`** | **[audit] Added — one of its numbers is a hard blocker, not a balance nicety.** `action-shares.v1.json` is keyed by atom family and does **not** contain `atom.fx-overlay-damage`, while `ActionShareTable` *"rejects rather than defaults"* — so the feature **throws on first use**. Owns all four numbers (share, cost, regen, pool max), each **derived from a named shipped anchor** with the arithmetic recorded. Calibration, not balance. | `basic-attack-seed`, `resource-subtick` |
| `lawn-combat-live-proof` | Re-measure the perf baseline with the damage trigger-mask **on**, then run the falsifier probe outside a debug session. Produces no source. | all of the above |

**Build order**

```
element-cache-invalidate ─┐
combat-numerics ──────────┤
resource-subtick ─────────┤   (five independent, parallelisable)
lawn-hit-attribution ─────┤
basic-attack-seed ────────┘
        │
        ├─► lawn-hit-entry        (safety rules land FIRST — see hazard below)
        ├─► lawn-action-bridge
        │        │
        │        └─► basic-attack-grant   ── strictly AFTER lawn-hit-entry
        │                    │
        │                    └─► basic-attack-cost ──┐
        │                                            ├─► lawn-combat-live-proof
        lawn-combat-calibration ────────────────────┘
```

`lawn-combat-calibration` depends on `basic-attack-seed` + `resource-subtick` and gates the live
proof: proofs 2 (element differential) and 4 (exhaustion/recovery) are **unrunnable** until the
numbers exist.

### ⚠ Ordering is a safety constraint, not a preference — [audit]

**`basic-attack-grant` must not ship before `lawn-hit-entry`.** The first draft of this map showed
them as siblings. Binding the grant flips `HasOnDamageDealtGrant()` true for **every** actor, which
opens `EventDrainHost.cs:48/72` for every bullet and melee hit — with none of `lawn-hit-entry`'s
liveness guard, never-drop rule, swing dedupe or instakill guard in place. That means per-victim
triggering (violating D8), the `next <= 0` → `ForceKill*` double-`Die()` path live
(`EntityStatWriter.cs:263-287`), and a rider on the lawnmower's 1,000,000-damage event.

**A half-deployed program is worse than an undeployed one here.** Treat this edge as a gate.

---

## Why these boundaries

- **The four leaves are genuinely independent** and touch disjoint files, so they parallelise. Three
  of them are defects that exist regardless of this feature (`element-cache-invalidate`,
  `combat-numerics`, `resource-subtick`) — each is separately testable and separately shippable, and
  none needs the lawn wire to be worth landing.
- **`lawn-hit-attribution` is deliberately its own module even though it is one field.** It is the
  single change that dissolves two apparently-fatal blockers, every downstream module depends on it,
  and it is independently provable (assert the recorded attacker ptr is the shooter, not the bullet).
  Burying it inside `lawn-hit-entry` would hide the program's highest-leverage change.
- **`lawn-hit-entry` and `basic-attack-grant` are split along "path" vs "predicate".** Entry makes the
  packet correct when a grant exists; the grant makes one exist. Each is testable against the other's
  absence — entry with any on-damage-dealt grant, the grant with the existing battle path.
- **`basic-attack-cost` is last of the code modules** because it needs both a thing to cost
  (`basic-attack-grant`) and an expressible regen (`resource-subtick`). Its four wires must land
  **together**: cost, pool max, regen kind, ledger call. Any three without the fourth ships the whole
  feature silently inert — max 0 means never any stamina, which under D6 is indistinguishable from
  today's bug.
- **`lawn-combat-live-proof` is an operation, not a build.** Same shape as `live-probe`'s
  `actor-hub-live-proof`.

## Scoped out, with reasons

| Not here | Why | Owner |
|---|---|---|
| **Per-species** basics (`SpeciesBasicsRow`, 904 rows of generated attack/guard/move) | **Owner decision 2026-09-13: ship the shared fallback, loaded from seed.** Per-species basics need a generator that does not exist, a table nothing populates (`UpsertSpeciesBasics` zero callers), and the corpus composer able to emit `Basic` at scale. They buy *named, individually-timed* attacks — flavour — not the elemental mechanic, because **element rides the actor, not the action row** (`action-ideal.md:158-160`). All 904 species already deal their own element through the shared row. | action program |
| Loading the other 155 action briefs (`Program.cs:402`'s two-file array) | `basic-attack-seed` adds one authored file to that loader; widening it to the full generated corpus is the action program's own content gate | action program |
| The `guard` and `move` basics (D1's other two of three) | Guard is a STANCE (D3), move is movement — neither is needed for the elemental damage wire, and authoring them unused would be content nobody reads | action program |
| Proc coefficient / fire-rate value scaling | Prices *value* — the balance program's axis (D1: balance is a separate program) | balance program |
| Elemental reactions, status application, ICD | D4 — elements stay bonus/reduce only here | deferred, tracked in the ideal |
| Per-actor defense on incoming vanilla hits (`GameHooks.cs:700,703`) | Pre-existing defect, unrelated to the rider path | that program |
| Battle's `recovery.scaleMilli` sizing | `resource-subtick` makes regen *expressible*; choosing its value is a balance pass | `residual-fit` |

## Program-wide constraints — [audit] none of these were stated, all are required

### A kill switch, named

No spec proposed one, and the existing switches are **not** substitutes:

| Switch | What it actually does | Why it is not enough |
|---|---|---|
| `OVERLAY-COMBAT` (default **on**) | Gates `OverlayCombatMath` only | With it **off** but the grant bound, hits are still recorded, still drained, **still cost stamina**, and the delta is dropped at `Finalize` — **actors pay for nothing**. Strictly worse than undeployed |
| `FUSIONRPG_EVENT_V2=0` | Disables the whole v2 drain | Sledgehammer; takes out unrelated machinery |

**Required: a feature-specific switch** (e.g. `FUSIONRPG_LAWN_BASIC_ATTACK`) that disables **binding
the grant and charging the cost together**, so the off state is exactly today's behaviour. Owned by
`basic-attack-grant`, honoured by `basic-attack-cost`.

### A perf budget with a stop rule

"Record a fresh baseline" is satisfied by *recording a regression*. The program needs a number and an
action on breach, not just a measurement. The only figure in scope is the existing **4.44% frame share
at 300 zombies** — measured with the damage trigger-mask **off**, which this program pins permanently
**on**. The spec phase must set a ceiling (a starting proposal: **≤ 6% at 300z**, revisited once the
first real measurement exists) and name what happens if it breaches: the feature ships behind the kill
switch defaulted off, rather than shipping green.

### Three numbers nobody owns

Every spec correctly refuses to invent balance values, and the result is that **the feature has no
magnitude at all**:

| Number | Status |
|---|---|
| Basic-attack `sharePermille` — sets `anchor(Θ) = sharePermille × P(Θ)/1000`, i.e. **how much damage the feature does** | unowned |
| The `stamina` cost per swing | `basic-attack-cost` says "ask first" |
| The regen rate | `resource-subtick` says "leave `BaseResourceRegen` at 0"; `basic-attack-cost` says "ask first" |

**With cost non-zero and regen zero, every lawn actor swings once and is permanently inert — worse
than today's bug.** So regen is a **fifth** all-or-nothing wire, not an optional follow-up; the "four
wires" framing in `basic-attack-cost` is wrong.

**Resolution — superseded 2026-09-13: these now have their own module, `lawn-combat-calibration`.**
Leaving them as "placeholders somebody authors eventually" was wrong on two counts.

First — **corrected 2026-09-13, see finding #12 below**: this row originally claimed
`atom.fx-overlay-damage` was a **hard blocker** because it has no row in `action-shares.v1.json` and
`ActionShareTable` rejects rather than defaults, "so the feature throws on first use." That is false.
`ActionShareTable` has **zero production callers** — it belongs to the not-yet-landed seeding pipeline
(`ActionSeeder.Generate`, itself asserted `ActionCorpusProducerLanded = false`) — so a missing row there
throws on nothing real; the basic attack's base damage is `attacker.LiveAtk`, never a share read
through this table. What `lawn-combat-calibration` (T11, built 2026-09-14) actually found is narrower
and different: `atom.fx-overlay-damage`'s **compiled row carries no `amount` key at all**
(`EffectAtomCatalog.Generated.cs`, traced in `spec-lawn-combat-calibration.md`'s "RESOLVED" section),
so it resolves to a hardcoded `0` on every hit — a silent no-op, not a throw. T11 derived and shipped
`stamina` cost + regen (`battle-resources.v2.json`, `action-corpus-cost-templates.v2.json`); it
deliberately left this third number **unauthored** (see that spec's "DECIDED" section) because both
candidate homes for it sit outside a tuning-file change — either dead infrastructure
(`action-shares.v1.json`) or hand-editing generated atom seed data. A named follow-up
(`lawn-combat-rider-amount`) owns it.

Second, a number with no derivation is un-arguable — the module requires each value to trace to a
named shipped anchor (`atom.poison-rider = 300` as the rider precedent, the `atk(Θ=20) = 92` pin, the
measured vanilla pea at 20 damage / 1.5 s) with the arithmetic recorded in `_meta`. Still `UNMEASURED`,
still not a balance pass — but derived and traceable rather than invented.

---

## Audit corrections (2026-09-13, two-pass adversarial review of the specs)

| # | Finding | Fix |
|---|---|---|
| 1 | The injector has **zero** references to `FusionRpg.Core.Actions`; two specs assumed reachability | New module `lawn-action-bridge` |
| 2 | `basic-attack-grant` shown as a sibling of `lawn-hit-entry` — half-deploy is worse than no deploy | Hard ordering constraint above |
| 3 | No kill switch; `OVERLAY-COMBAT` off + grant bound = pay-for-nothing | Named above |
| 4 | Perf criterion satisfiable by recording a regression | Budget + stop rule above |
| 5 | `sharePermille`, cost and regen all unowned; regen is a fifth all-or-nothing wire | Placeholders, owned above |
| 6 | Map said "D1–D7"; D8/D9 exist and are load-bearing | Header fixed |
| 7 | Live-proof's Fire-vs-Ice proof is defeatable both ways | Rewritten in `spec-lawn-combat-live-proof.md` |
| 8 | "not the `{Hp=100…}` stub" is satisfiable *by* a stub | Rewritten in `spec-lawn-hit-attribution.md` |
| 9 | Escape-hatch criterion let the program be "done" with four creature types RPG-inert | Closed in `spec-lawn-hit-attribution.md` |
| 10 | Hypno re-bake seam: the resolver cache is fixed but a **baked** `elementPayload` stays stale | Added to `spec-basic-attack-grant.md` |
| 11 | `lawn-action-bridge`'s premise was factually wrong — injector already references Core; a compiled row cannot be transported; `ActionTimingPolicy` **throws** unconfigured; both named guards were vacuous | Spec rewritten; scope shrank from "build a transport" to "extract a factory + configure a policy" |
| 12 | ~~`sharePermille` missing ⇒ throws on first use~~ — **retracted, that claim was false.** `ActionShareTable` has zero callers; base damage is `attacker.LiveAtk`, not a share. New module `lawn-combat-calibration` still stands, but for **two** numbers (cost, regen) plus one open question — what amount `atom.fx-overlay-damage` resolves to | `lawn-combat-calibration`, corrected |

---

## Resolved during review

**Corpus scoping (owner, 2026-09-13): ship the fallback, but load it from seed data.** Not the
hardcoded C# row, and not per-species generation either. This became `basic-attack-seed`, and it
carries a real benefit beyond tidiness: the row's **cost** stops being a C# literal
(`BattleRunState.cs:81` `Costs: Array.Empty<...>()`) and becomes authored config, which is what makes
`basic-attack-cost` expressible at all.

The seed is authored and validated: `data/seed/actions/authored-basics.json`. Two findings made it
land cleanly —

- **`kindHint` is real after all.** The shipped briefs carry `"kindHint": "innate"`; the parser
  explicitly declines to consume it (`ActionCorpusBriefJson.cs:10-20` calls it "content-authoring
  metadata this module does not consume") and the composer hardcodes `Kind = ActionKind.Skill`
  (`ActionCorpusComposer.cs:144`). An earlier audit reported `kindHint` as absent from the repo; it is
  absent from `src/**` but present in the **data**. The defect is therefore confirmed and precisely
  located.
- **The atom family the basic attack needs already exists, purpose-built.**
  `atom.fx-overlay-damage` (`data/seed/atoms/fx-core.json:33`) is `kind: resource.delta`,
  `when.trigger: OnDamageDealt`, `params.channel: hp`. `resource.delta` satisfies the
  `ApplyResourceDelta` precondition for the `elementPayload` bake, and `OnDamageDealt` is exactly the
  trigger that makes `HasOnDamageDealtGrant()` true. One existing atom answers two of the ideal's
  three blockers.

**Still open for the spec, not for approval:** cost resolution must become Kind-aware. The category
template gives `attack → qi 20` (`action-corpus-cost-templates.v1.json`), but D5 requires a Basic to
cost `stamina`. Either the template gains a per-Kind rule or an authored brief may carry an explicit
cost — a `basic-attack-seed` spec decision.
