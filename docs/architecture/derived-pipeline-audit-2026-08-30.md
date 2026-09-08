# Derived-pipeline and action-layer defects — audit 2026-08-30

**Found while specifying the `aura-skill` program.** None of these are caused by that program; all of
them exist in the tree today and would bite any feature touching derived channels or the action layer.
Logged here so they are not buried inside one program's specs.

**Status: reported, not fixed.** No code was changed. Each entry names the evidence and what it costs.

---

## D1 — `ActorDerivedSnapshot.Overlay` is replace, not add

**Severity: high — silent, and the workaround is already load-bearing.**

`Stats/Derived/ActorDerivedSnapshot.cs:47-53`:

```csharp
public ActorDerivedSnapshot Overlay(IEnumerable<KeyValuePair<string, double>> extra)
{
    var next = FromValues(_channels);
    foreach (var (k, v) in extra)
        next._channels[k] = v;      // assignment, not +=
    return next;
}
```

The only production caller compensates by hand — `Injector/Effects/PatronAuraOverlay.cs:37`:
`pairs.Add(new(channel, derived.Get(channel) + milli / 10.0));`

**Cost:** a second overlay on the same channel **silently erases the first**. There is no arbitration,
no compose order, and **no guard test**. Today only one producer exists, so the defect is latent — it
becomes live the moment a second one lands. The patron aura's correctness currently depends on every
future author noticing the manual `Get +` and copying it.

**Suggested fix:** add `OverlayAdd` alongside `Overlay`, document which to use when, migrate
`PatronAuraOverlay` (dropping its manual compensation in the same change, or the aura doubles), and add
a regression test asserting two overlays accumulate.

---

## D2 — no idempotence guarantee for derived-channel re-assertion

**Severity: high — this is the real overflow path, and nothing states it.**

`shield-system-spec.md:135` records *"aura re-assert is genuinely idempotent"* for **shields**. (⚠️ An earlier revision of this entry attributed that quote to `decisions.md:41`. It is not there — corrected 2026-08-30.) There is no equivalent
statement anywhere for a **derived channel**.

A percentage re-asserted per tick against an already-buffed total is **geometric in tick count** — that,
not any share-based term, is the shape that reaches the overflow thresholds `CLAUDE.md` documents.
(`share` is a bounded `[0,1]` ratio, so `share² ≤ share`; a share-squared cross-term is *smaller* than
the linear term beside it and is not an overflow risk. This audit corrects an earlier claim in
`aura-skill-ideal.md` that said otherwise.)

**What currently prevents it:** nothing deliberate. D1's replace-not-add semantics make the one shipped
overlay *accidentally* idempotent. **Fixing D1 removes that accident without replacing the guarantee** —
so D1 and D2 must be addressed together, or fixing D1 opens D2.

**Suggested fix:** state the rule ("a derived-channel contribution is a function of its inputs, never of
the channel's current value") and enforce it with a test: hold inputs fixed, add an unrelated
contribution to the same channel from a different `SourceId`, assert the first producer's emitted value
is bit-identical.

---

## D3 — the first authored action row will throw every web battle

**Severity: high — a same-day landmine for whoever authors action content first.**

`Battle/BattleRunState.cs:243-246` throws `ArgumentException` for any non-empty `EquippedActionIds`
when no `ActionCatalog` was supplied. `ActionCatalog.Build` / `ActionCatalogHost` have **zero
production callers** — grep across `src/FusionRpg.Server/` and `src/FusionRpg.Injector/` returns
nothing. Meanwhile `WebMatchService.cs:390-392` builds `EquippedActionIds` from
`store.ListGrants(scope)` filtered to `ActionKind.Skill`.

**Cost:** the moment one `rpg_action_grant` Skill row lands in the database, **every web battle
throws**. The tables ship, the writers exist, and nothing warns the author.

**Suggested fix:** either wire an `ActionCatalog` in the server composition root, or make
`BattleRunState` degrade to "no equipped actions" with a logged warning rather than throwing. The
choice is a real decision; the current state is a trap either way.

---

## D4 — production battle runs with costs and stances permanently disabled

**Severity: medium — the seams exist and are inert, which reads as "built" from the outside.**

- `CostLedger` has **zero `new CostLedger(` sites** in `src/`. `TryPay` is called only from
  `tests/…/CostLedgerTests.cs`. `ActionCostTiming.PerTick` is read solely as a classifier at
  `StructureBudgetGuard.cs:67` and **never charged**.
- `Actions/Defence/StanceRuntime.cs` is dead outside tests; the only production `IStanceCheck` is the
  null object `NoStanceHeld.Instance` (`BasicAttack.cs:74`), alongside `AlwaysAffordable.Instance`.

**Cost:** any spec that says *"this calls the existing cost mechanism"* is true and useless — the
existing mechanism has no driver. The termination invariant (`spec-action-costs.md` §4.1), which
`decisions.md` makes **blocking**, cannot currently be satisfied by anything that relies on per-tick
payment.

---

## D5 — `stat.derived` is quarantined on Lawn and Sim (R4)

**Severity: high for any feature planning to deliver a derived channel outside battle.**

`Effects/Atoms/AtomKindRegistry.cs` — `stat.derived` carries
`RuntimeSupportMatrix(Lawn: None, Battle: Full, Sim: None)`, with the comment *"no opcode, no EffectBag
branch, no sink arm… A bind would have been accepted and then done nothing forever."* Four independent
refusals follow:

1. `BindGate` rejects `RuntimeState.None` with `RuntimeUnsupported` — a lawn bind **fails**.
2. `stat.derived` carries `AtomTriggers.None` — no trigger vocabulary, so nothing can toggle it.
3. `ScopeCompatibility` (4 rows) has **no `stat.derived` row**; `BattlefieldOwnSideReactor`'s
   constructor throws `ScopeUnsupportedException`. The only Relation delivery mechanism in the repo is
   unreachable for this atom kind.
4. `BattleEffects.cs:135` — *"battle mode consumes ApplyResourceDelta (FA10) / ApplyStatus (FA2) /
   ModifyStat (FA1) only."*

**Cost:** the quarantine is correct and deliberate — it is *documented* in the registry. But it is not
reflected in any planning document, so a program can be specced end-to-end against a delivery path that
does not exist. That is exactly what happened to `aura-skill`.

---

## D6 — `combat.*` families are not channels

**Severity: medium — an easy authoring error with a validation failure at the far end.**

`combat.power` is a **family** (`DerivedStatChannels.cs:188`); the registered channels are
`combat.power.omni | .fire | .ice | .air | .earth | .light | .dark`. `DerivedComposer.Compose` calls
`_registry.ValidateChannel` on every modifier, so emitting on the bare family fails.

**Cost:** any spec or content authored against a bare family name is rejected at compose.

⚠️ **Corrected 2026-08-30 — omni versus element slots is *not* a magnitude decision.** An earlier
revision of this entry called it *"a 7× magnitude decision."* It is not. `CombatDerivedReader.cs:9-51`
reads **`omni + element(e)` additively**, and `ElementPayload.Validate` enforces `Σ weights = 1.0` — so
`+X` to omni and `+X` to all six elements contribute **the same X**, the latter at 6× the authoring
cost. A single element slot contributes `w_e · X ≤ X`, and **zero** against an untyped attack.

Two facts make element slots outright wrong for a *universal* modifier: **parry, block and reflection
are read omni-only** (their element halves are *"registered and unread"*,
`CombatDerivedReader.cs:53-57`), and the untyped-attack path is omni-only
(`OverlayCombatCalculator.cs:87-111`).

`PatronAuraOverlay.cs:22` writing `"combat.power." + aura.ElementPrimary` (`:27` is the *defense* secondary) is **not a counter-precedent**
— its element *is* its content (`PatronPolicy.cs:5-6`, *"the patron's element channels"*). The relevant
precedent is `BattleStatComposer.cs:8-11`: *"**level formulas fill the omni halves**, element affinity
fills the actor's own element channels."*

**Rule: universal/level-derived → omni; elemental identity → element slots.**

---

## D7 — `CooldownMath` and `CooldownChannel` are stubs with zero callers

**Severity: low — already documented, recorded here for completeness.**

`DerivedStatRegistry.cs:179` says it plainly: *"No reader: `CooldownMath.ApplyReduction` and
`ActionEnvelope.CooldownChannel` both exist with zero callers — the action/timeline layer that would
wire them is unbuilt."*

Note for anyone planning cooldown content: `CooldownMath.ApplyReduction` implements **percentage
reduction** (`base × (1000 − reductionPm) / 1000`).

⚠️ **Corrected 2026-08-30.** An earlier revision of this entry warned that switching to the divisive
form would *"replace"* determinism-guarded shipped code and called it *"a real decision."* **It is
neither.**

- **Nothing is replaced** — `ApplyReduction` has zero production callers (see above). It is a stub.
- **The guard does not forbid it.** `TimelinePurityGuardTests.Kernel_sources_contain_no_wall_clock_rng_or_floating_point`
  is a **source scan** for `DateTime` / `Random` / `double ` / `float ` — it bans floating-point
  **types**, not division. `baseCD * 1000 / (1000 + hasteMilli)` is pure `long`.
- **Integer divisive haste already ships in the same directory under the same guard** —
  `TurnReadiness.EffectiveRate` is `speed * NominalHasteMilli / haste` (`NominalHasteMilli = 1000`),
  with a registered `turn.haste` channel, a live consumer (`ReadinessDriver`), and tests.

So the divisive form **matches** the kernel's existing convention; percentage reduction is the outlier.
Whoever wires cooldowns should prefer divisive — constant marginal value, and therefore no cap needed,
which is precisely why Riot could delete theirs after the same switch.

---

## What this audit does not claim

- **No fix is proposed as authorized.** Each "suggested fix" is a starting point for a spec, not a task.
- **D1 and D2 are coupled.** Fixing D1 alone makes D2 live. Sequence accordingly.
- **D5 is not a bug.** The quarantine is deliberate and self-documented; the defect is that planning
  documents do not reflect it.
- One earlier claim in this program's own documents — a *"three-way default inconsistency"* in the
  `SYS-*` cheat flags — was **fabricated and has been retracted**. All three registries agree
  (`CheatSchema.cs:99-101`, `CheatRegistry.cs:74-76`, `CheatState.cs:150-152`). Recorded here so the
  retraction outlives the spec that carried it.
