# Spec: `lawn-combat-calibration`

**Program:** `lawn-combat-wire` · **Map:** [../lawn-combat-wire-map.md](../lawn-combat-wire-map.md)
**Depends on:** `basic-attack-seed` (the row whose share is authored), `resource-subtick` (makes a
regen rate expressible)

> Added 2026-09-13. Every other spec correctly refused to invent balance numbers, and the emergent
> result was a feature with **no magnitude at all**. This module owns the numbers — by **deriving**
> them from shipped anchors, not inventing them.

---

## Objective

Give the lawn basic attack defensible starting values, with the derivation written down so a later
balance pass can redo it rather than reverse-engineer it.

**Calibration is not balance.** D1 keeps balance in a separate program, and that stands: this module
does **not** run win-rate sweeps or tune for feel. It answers the narrower question *"what number is
defensible on day one, and why"*, so the feature is playable and its numbers are traceable.

### ⚠ Correction — the first draft of this spec was wrong about its own headline

**It claimed `sharePermille` was a hard blocker that would throw on first use. It is not.**

`ActionShareTable` has **zero callers** in `src/` — only its own definition file
(`Actions/Seeding/ActionShareTable.cs`). It belongs to the **seeding** path, whose entry point
`ActionSeeder.Generate` also has zero production callers (`ItemGrantedActionRow.cs:111-119` asserts
`ActionCorpusProducerLanded = false`). Neither `BasicAttack.cs` nor `BattleRunState.cs` mentions
`sharePermille` at all.

**The basic attack's base damage does not come from a share.** It comes from the attacker's own atk:

```csharp
BaseOverlayDamage = attacker.LiveAtk(state.Ledger)                         // BasicAttack.cs:369
public long LiveAtk(…) => ledger.Recompose(Setup.Key, "atk", Setup.Atk);   // BattleEngine.cs:101
```

So `anchor(Θ) = sharePermille × P(Θ)/1000` is **not** the formula in play, and the
`atom.poison-rider = 300` precedent is irrelevant to this module. That error came from inferring a
call path from a component's existence instead of verifying it — the same mistake that produced the
first `lawn-action-bridge` draft, made twice in one hour.

## The numbers — three, now that the open question below is resolved

| # | Number | Home | Status |
|---|---|---|---|
| 1 | `stamina` cost per swing for `act.attack` | Kind-aware cost template (`basic-attack-seed`) | **missing — real** |
| 2 | Lawn `stamina` regen rate (per-mille/tick) | `data/tuning/battle-resources.v1.json` | **missing — real**; blocked until `resource-subtick` makes it expressible |
| 3 | Lawn `resource.max.stamina` | derived | **already derivable** — `BaseHp(Θ) × poolShareMilli/1000`; only the seeding is missing (`basic-attack-cost` wire 2) |
| 4 | `atom.fx-overlay-damage` amount | `data/seed/atoms/fx-core.json:33` (or the grant's overlay, or an `eventField` marker — the module's choice) | **missing — real, confirmed 2026-09-13** (see resolved question below): the atom resolves to a hardcoded `0` today, an independent magnitude to author |

### RESOLVED (2026-09-13): (a) — the atom needs an authored amount. Today it resolves to a hardcoded zero.

**Correction to this spec's own prior citation:** the line range `DamagePacketBuilder.cs:17-28` (quoted
below, and in `tasks/lawn-combat-wire-todo.md` Task 1) is **stale** — that range is `FromOverlay`'s
signature and packet construction. The method that actually decides the amount, `ResolveAmount`, is at
`DamagePacketBuilder.cs:63-83` in this checkout. Cite the method, not a drifted line number.

**The trace, file:line by file:line:**

1. `EffectBag.cs:507-514` — for the `ApplyResourceDelta` action, `FireGrant` calls
   `DamagePacketBuilder.FromOverlay(merged, ev, ...)` with **no `amountOverride`** (the parameter is
   left at its `null` default), so the amount is decided entirely by `ResolveAmount`.
2. `merged` comes from `EffectOverlayMerge.TryMerge` (`EffectProcAndOwner.cs:322-323, 331-344`): it
   copies `actionParams` (the atom's compiled row) into `merged`, then folds in `grant.Overlay` **only
   for keys `grant.Overlay` already contains**. It never invents an `"amount"` key.
3. The atom's actually-shipped compiled row — `EffectAtomCatalog.Generated.cs:205-222`
   (`EffectId = "fx.overlay_damage"`, `Action = "ApplyResourceDelta"`) — has
   `Params = { ["channel"] = "hp" }`. **No `amount` key, at the compiled/runtime level, not just the
   seed source** (`data/seed/atoms/fx-core.json:33-40` matches it exactly).
4. `basic-attack-grant`'s standing bind (`spec-basic-attack-grant.md`) never sets `grant.Overlay["amount"]`
   either — that spec's own "what the grant carries" section only bakes `elementPayload` and the
   `entity:{ptr}` owner key. The grant is bound **once at spawn** and never re-created per swing, so
   there is no per-hit call site that could inject a fresh `amount` into it even if one wanted to.
5. So `merged` reaching `ResolveAmount` (`DamagePacketBuilder.cs:63-83`) never contains an `"amount"` key
   at all. `ResolveAmount`'s first branch (`:65-80`, the P0.2 event-linked marker) is gated on
   `overlay.TryGetValue("amount", ...)` succeeding — it does not — so it is skipped entirely. Execution
   falls to the final line, `DamagePacketBuilder.cs:82`:
   `return (long)JsonOverlay.GetDouble(overlay, "amount");` — and `JsonOverlay.GetDouble`
   (`EffectModels.cs:234-238`) returns its `fallback` parameter (default `0`) when the key is absent.
6. Back in `EffectBag.cs:518-521`, the only re-read of `merged["amount"]` is itself gated on
   `merged.ContainsKey("amount")` — also false here — so nothing overwrites the zero.

**Verdict: as authored and as compiled today, `atom.fx-overlay-damage` resolves to a literal, hardcoded
`0` on every hit through the `basic-attack-grant` path.** It does **not** read the vanilla/RPG hit's own
damage automatically. This is answer **(a)** — a real, confirmed gap, not (b).

**One nuance worth recording so the next session doesn't reinvent it:** the mechanism that
answer (b) describes — reading the firing event's own `Damage` field — already exists and is exactly
built for this shape (`ValueSpec.EventField`/`P0.2`, `docs/architecture/effect-atom/spec-value-spec-and-curve.md`
"Event-linked magnitudes", **not** under `lawn-combat-wire/` — that doc-path citation in the todo/map
was also wrong). It is just not invoked by this atom: the seed row authors no
`"amount": {"eventField": "damage", "multiplierMilli": ...}` marker. Whoever authors the missing amount
for `basic-attack-calibration` has two legitimate shapes to choose between — a flat authored number
(closer to the `atom.poison-rider = 300` precedent already used elsewhere in this file) or wiring the
existing `eventField` marker so the rider scales off the vanilla hit's own damage — and that choice is
this module's to make, not something this task decides. What this task closes is only the factual
question of what happens **today**, unauthored: zero, always.

### DECIDED (2026-09-14, T11 build): the amount is explicitly NOT authored in T11

T11's own declared file scope (`tasks/lawn-combat-wire-todo.md` Task 11 — `tools/tuning/publish.py`,
`battle-resources.v{n}.json`, `action-corpus-cost-templates.v{n}.json`) does not include either
candidate home for this number, and for a real reason, not an oversight:

- **`data/tuning/action-shares.v1.json` (the flat-authored-number shape).** `ActionShareTable`
  (`src/FusionRpg.Core/Actions/Seeding/ActionShareTable.cs`) is the only reader of that file, and it has
  **zero production callers** — confirmed the same way this spec's own retracted `sharePermille` claim
  was confirmed false, by reading who calls it, not by the file's existence. Adding a row there would
  be inert data: nothing compiles it into `EffectAtomCatalog.Generated.cs`'s `fx.overlay_damage` row,
  so `ResolveAmount` would still see no `"amount"` key and still return the hardcoded `0` traced above.
- **The `eventField` marker (the scaling shape).** Authoring `data/seed/atoms/fx-core.json`'s `amount`
  key is editing **generated seed data** (CLAUDE.md/AGENTS.md hard rule: fix the generator and
  regenerate, never hand-edit the emitted row) and then requires re-running the atom importer to
  regenerate `EffectAtomCatalog.Generated.cs` — a materially different, larger change than "author a
  tuning number," and it touches the same atom-compile pipeline `lawn-action-bridge`/T9's own
  `DamagePacketBuilder.cs` work sits next to.

Both shapes are real wiring, not tuning-file authoring, and neither is reachable from the three files
T11 actually owns. **T11 leaves the atom's amount at its confirmed-today value: 0, unauthored,
unwired.** This is the second disjunct of this task's own acceptance line ("magnitude authored, **or
explicitly not authored**") — taken deliberately, not by default. A follow-up task (name suggested:
`lawn-combat-rider-amount`) owns picking the shape and doing the wiring; until it lands, the standing
`basic-attack-grant` fires with zero elemental rider damage, exactly as it does today — no regression,
because nothing about T11 changes this atom's compiled row.

## Shipped anchors — derive from these, do not invent

| Anchor | Value | Source |
|---|---|---|
| Power pin | `BaseHp(Θ=20) = 680` | `power-scale.v2.json` `curve.pinValue` |
| Atk pin | `atk(Θ=20) = 92` | `power-scale.v2.json` `atk.pinValue` |
| Full strike share | `atom.strike = 1000` (100% of anchor) | `action-shares.v1.json` |
| **Rider share** | `atom.poison-rider = 300` (30%) | `action-shares.v1.json` — **the closest precedent; ours is a rider on a vanilla hit, not a replacement for it** |
| Pool share | `500‰` uniform → pool 340 at Θ=20 | `battle-resources.v1.json` |
| Pool sizing precedent | `reaction-lane` poiseSpend 100 ⇒ ~3 counters from a pinned pool | `battle-resources.v1.json` `_meta.sizing` |
| Cost is authored against **regen**, never max | — | `action-ideal.md:223` |
| Vanilla pea | 20 damage, `thePlantAttackInterval` **1.5 s** | measured live 2026-09-13 |
| Vanilla NormalZombie | 270 hp | measured live 2026-09-13 |

**Derived sanity check, stated so the calibration can be argued with:** a vanilla Peashooter needs
`270 / 20 ≈ 14` peas ≈ **20 seconds** to kill one basic zombie. Any rider that changes that to under
~2 seconds has made vanilla damage irrelevant; one that changes it to 19 seconds is decorative. The
starting share should land the combined time-to-kill somewhere a player would notice and a designer
would recognise as "my element matters", and the spec must state the resulting TTK it chose.

## Method

1. **Share (#1).** Start from the `atom.poison-rider = 300` precedent, since our basic attack is a
   rider layered on a vanilla hit. Compute the resulting per-hit rider at the pin (Θ=20) and the
   resulting TTK against a 270 hp zombie. Adjust only to land a defensible TTK, and **record the
   arithmetic in the tuning file's `_meta`**.
2. **Cost (#2) and regen (#3) together.** `action-ideal.md:223` — cost is authored against **regen**.
   A Peashooter swings every **1.5 s**, so on the 100 ms kernel the sustainable cost is
   `regenPerSecond × 1.5`. Pick regen first from the pool size (a pinned pool of 340 should sustain
   continuous fire for a plant that is *meant* to fire continuously), then set cost at or just under
   the sustainable rate, so exhaustion is a **burst-fire** consequence and not a permanent stop.
3. **Prove the exhaustion case is reachable but recoverable** — the falsifier in
   `lawn-combat-live-proof` proof 4 requires an actor that actually runs dry and actually recovers. A
   cost far under sustainable makes that proof unreachable; a cost far over makes every plant inert.
   **The numbers must make that proof runnable.**

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "Category=BalanceGuard"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~LawnCombatCalibrationGuardTests"
python tools/tuning/resource_ownership.py --check
```

## Project structure

**Built 2026-09-14 (T11).** `data/tuning/action-shares.v1.json` is deliberately **not** touched — see
"DECIDED" above; the atom's amount stays a named, explicit follow-up.

| Path | Duty |
|---|---|
| `tools/tuning/publish.py` | Gains `--add-regen-block`, mirroring the existing bespoke `--add-rung-power-budget` shape (T11's own citation) — the sanctioned way to add a first-time key `battle-resources.v1.json`'s own convention otherwise refuses |
| `data/tuning/battle-resources.v2.json` | Gains `regenPerSecondShareMilli` — `stamina=50` (‰ of pool/s), the other four ids explicit `0`; v1 kept on disk |
| `data/tuning/action-corpus-cost-templates.v2.json` | `kinds.basic.baseAmountAtRung1` re-derived from the pool/regen envelope (20 → 25); v1 kept on disk |
| `src/FusionRpg.Core/Battle/BattleResourceTuning.cs` | Parses the new `regenPerSecondShareMilli` block (optional at parse time — absent ⇒ all-zero, so every v1-pinned reader stays byte-identical) |
| `src/FusionRpg.Core/Battle/BattleModels.cs` | `BaseResourceRegen` reads the tuning instead of a hardcoded `0`; returns `double` (units/tick) now that S10.1 makes a sub-tick rate meaningful |
| `src/FusionRpg.Server/Program.cs` | Both tuning file reads bumped to `v2` — the real, only production consumer this task needed to move |
| `tests/FusionRpg.Core.Tests/Balance/LawnCombatCalibrationGuardTests.cs` | New `Category=BalanceGuard` tests reading the real v2 files, asserting the contract (never a pinned number) |

## Code style

Every number lands in **data**, never a `const`. Follow each file's own rebalance convention: several
say *"Never hand-edit this file"* and require `python tools/tuning/publish.py` writing a `v{n+1}`, with
the old version kept for revert. **Read the target file's `_meta.rebalance` before editing it** —
these conventions differ per file and are not decorative.

Every authored value carries, in `_meta`: the anchor it derived from, the arithmetic, and the word
`UNMEASURED`. This repo's posture, quoted: *"shipping a guess is fine, calling it balance is not."*

## Testing strategy

| Level | Cases |
|---|---|
| BalanceGuard | `StaminaCostNeverExceedsSustainableRegenAtThePin` — the sustainable-fire invariant `cost ≤ regenPerSecond × attackInterval` at the pin, computed from the real shipped v2 files, never a hardcoded expected cost/regen pair |
| BalanceGuard | `OnlyStaminaRegeneratesEveryOtherResourceStaysExplicitlyZero` — the closed-vocabulary half: poise/hunger/spirit/qi stay an explicit, deliberate `0` |
| BalanceGuard | `V1StillParsesWithNoRegenBlockAndDefaultsEveryShareToZero` — the revert path: v1 stays on disk, still parses, defaults every share to 0 |
| Core unit | `BattleResourceSeedTests`/`ResourceSubTickRegenTests` — the assembly's own ambient (all-zero) fixture stays byte-identical; unaffected by the real v2 numbers, which no ambient test reads |
| Live | `lawn-combat-live-proof` proofs 2 and 4 become runnable only once `basic-attack-cost` (T12) actually charges/regens the numbers this task authored — T11 makes them defensible, T12 makes them consequential |

## Boundaries

- **Always:** derive from a named shipped anchor; record the arithmetic; mark `UNMEASURED`.
- **Always:** follow the target tuning file's own `_meta.rebalance` convention.
- **Ask first:** any value that cannot be derived from an existing anchor — that is a real balance
  decision and belongs to the balance program, not here.
- **Never:** a balance number in code; a test that pins an exact damage output; an immunity-style
  threshold; a second curve. Magnitude stays `anchor(Θ) × q(rung)` with `rungBand [1,1]`.

## Success criteria

- [x] **`ResolveAmount`'s behaviour for `atom.fx-overlay-damage` is read and recorded** — **(a)**,
      resolved 2026-09-13: as authored and as compiled, the atom resolves to a hardcoded `0` on every
      hit (no `amount` key anywhere in its compiled row or its standing grant's overlay reaches
      `ResolveAmount`); it does not read the vanilla hit's own damage. See "RESOLVED" above for the
      full trace.
- [x] **The amount is explicitly NOT authored in T11** (see "DECIDED" above) — both candidate homes
      (`action-shares.v1.json`'s dead `ActionShareTable`, or hand-editing generated atom seed data) sit
      outside T11's declared file scope and outside "author a tuning number." Named follow-up:
      `lawn-combat-rider-amount`.
- [x] `stamina` cost and lawn regen are authored (`battle-resources.v2.json`,
      `action-corpus-cost-templates.v2.json`), and satisfy `cost ≤ regenPerSecond × 1.5 s` at the pin —
      proven by `LawnCombatCalibrationGuardTests.StaminaCostNeverExceedsSustainableRegenAtThePin`
      reading the real shipped files, not a hardcoded pair.
- [x] **Exhaustion-reachability moved out of T11** (`tasks/lawn-combat-wire-todo.md` Task 11's own
      "Moved out of this task" note, 2026-09-13): the criterion cannot be decided until cost is
      actually *charged*, which is `basic-attack-cost` (T12)'s job. It now belongs to T12 and
      `lawn-combat-live-proof` proof 4. This spec is corrected to agree with the todo rather than
      still asking T11 to prove something it structurally cannot.
- [x] Every value marked `UNMEASURED` and traceable to a named anchor (`battle-resources.v2.json`
      `_meta.regenDerivation`; `action-corpus-cost-templates.v2.json` `_meta.kindsNote`).
- [x] No exact-damage assertion anywhere in the suite — the new BalanceGuard tests assert the
      inequality and the zero/non-zero split, never a pinned cost or regen number.
