# Capability map: `lawn-combat-wire`

**Ideal:** [lawn-combat-wire-ideal.md](lawn-combat-wire-ideal.md) (idea phase, audited, D1–D7 settled)
**Status:** capability map — **awaiting owner approval before any module spec is written**

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
| `basic-attack-grant` | Bind `ActionKind.Basic` to every lawn actor at spawn so `HasOnDamageDealtGrant()` is true; carry a real `elementPayload` from the owner's species element. | `lawn-hit-attribution`, `element-cache-invalidate`, `basic-attack-seed` |
| `basic-attack-cost` | Make `act.attack`'s cost **authored data** rather than a hardcoded empty array; seed `resource.max.*` on the lawn Hub; add regen as a third kernel kind; add the lawn's missing `CostLedger` call. | `resource-subtick`, `basic-attack-grant` |
| `lawn-combat-live-proof` | Re-measure the perf baseline with the damage trigger-mask **on**, then run the falsifier probe outside a debug session. Produces no source. | all of the above |

**Build order**

```
element-cache-invalidate ─┐
combat-numerics ──────────┤
resource-subtick ─────────┤   (five independent, parallelisable)
lawn-hit-attribution ─────┤
basic-attack-seed ────────┘
        │
        ├─► lawn-hit-entry ──────┐
        └─► basic-attack-grant ──┤
                                 └─► basic-attack-cost ──► lawn-combat-live-proof
```

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
