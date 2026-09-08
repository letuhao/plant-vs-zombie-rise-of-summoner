# Spec: `overlay-combat-enable`

**Program:** aura-skill · **Map:** [../aura-skill-map.md](../aura-skill-map.md) ·
**Ideal:** [../aura-skill-ideal.md](../aura-skill-ideal.md)
**Status:** built and closed 2026-08-30 (aura-skill T8) — C1–C13 green on a real MelonLoader 3.9 lawn
(`docs/runbook/melon-live-checklist.md` §8b, raw JSON `docs/research/effect-runtime/_prove-overlay-combat.json`),
`OVERLAY-COMBAT` promoted to default-on in all three cheat registries, zero goldens moved (full
7-suite .NET re-run green). Foundation module, independent of the others.

---

## 1. Objective

Close a loop that has been open since **2026-08-20**: re-prove the overlay combat calculator on a live
lawn — **including the heal path, which the original proof does not cover** — then flip
`OVERLAY-COMBAT` to default-on.

**Eight of the twelve auras are gated behind this flag.** Without it, only Vigor and Pierce are live
(`ShieldGate` is toggle-independent); `combat.power`, `defense`, `accuracy`, `dodge`, `crit.*`,
`parry.*`, `block.*` are all read exclusively by `OverlayCombatCalculator`, which
`ConditionalOverlayCombatMath` bypasses entirely when the flag is off.

### The evidence that this is unfinished wiring, not a guard

`OverlayCombatFeature.cs` is 14 lines with **no comment justifying the off default** anywhere in it or
at any call site. It was **born default-off in commit `3e9fe96`, whose subject is *proving* the
feature**. `docs/research/effect-runtime/_prove-overlay-combat.json` is committed, timestamped four
minutes later, and records **10/10 PASS (C1–C10) on a real lawn**.

Yet `debug-live-checklist.md:277-286`'s Pass/Fail columns are **blank**, and
`04-proof-results.md:131` still reads *"PENDING operator"*.

Against a permanent guard: no `decisions.md` row, no spec sentence, no code comment says it should stay
off; `decisions.md:38` calls it *"Shipped (flag-gated)"*; `decisions.md:40` mandates **"one combat
formula set + one apply path, everywhere"**; and the sibling `SYS-*` flags **were** promoted to
default-true (`CheatSchema.cs:99-101`), so the move exists and was simply never made here.

**Verdict: nobody closed the loop.**

---

## 2. Commands

```powershell
$env:FUSIONRPG_GAME_DIR = "<game folder>"
.\scripts\prove-overlay-combat.ps1 -OutJson docs\research\perf\_prove-overlay-combat-rerun.json
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Guard.Tests
dotnet test tests\FusionRpg.Data.Tests
```

The prove script is a real harness, not a toy: it enables the cheat (`:116-118`), runs C1–C10 against a
live lawn (matchup bonuses, forced miss, heal, flag-off, forced crit), and writes `-OutJson`. **Case C6
(`:220-240`) deliberately toggles the flag off, asserts no overlay emit, then toggles back on** — so the
harness already proves both states.

---

## 3. Project structure

| Path | Change |
|---|---|
| `src/FusionRpg.CheatCore/CheatSchema.cs` | edit — promote `OVERLAY-COMBAT` to `T(id, true)` |
| `src/FusionRpg.CheatCore/CheatRegistry.cs` | edit — same default |
| `src/FusionRpg.Injector/CheatState.cs` | edit — same default |
| `scripts/prove-overlay-combat.ps1` | edit — **add heal cases C11–C13** |
| `docs/runbook/debug-live-checklist.md` | edit — fill the Pass column |
| `docs/research/effect-runtime/04-proof-results.md` | edit — replace PENDING with the result |
| `docs/README.md` | edit — `:73` says *"overlay CombatMath deferred"*, stale |

⚠️ **The default lives in three registries, and flipping it is not a one-word edit.**
`OVERLAY-COMBAT` sits inside a shared `foreach … T(id)` default-false loop in all three files, so
**there is no per-id default to change.** Flipping it requires either pulling the id out of the loop
or adding an explicit promotion line after it — the pattern `SYS-EMIT-PROOF`, `SYS-DAMAGE-FX` and
`SYS-ELEMENT-FX` already use (`CheatSchema.cs:99-101`, `CheatRegistry.cs:74-76`,
`CheatState.cs:150-152`). Do it the same way in all three, or the three registries will disagree.

> **Retraction (2026-08-30).** An earlier draft of this spec asserted a *"pre-existing three-way
> inconsistency"* in those `SYS-*` defaults and made reporting it an owner obligation and a boundary
> rule. **That defect does not exist** — all three registries set all three flags to `true`. The claim
> was fabricated by mis-reading the shared loop for the whole story, and the obligations built on it
> are removed. Recorded rather than quietly deleted, because an invented finding costs the same trust
> as a missed one.

---

## 4. Design

### 4.1 What changes when the flag flips — live lawn only

`ConditionalOverlayCombatMath.cs:21-26` is a strict either/or, and `PassThroughCombatMath`
(`ICombatMath.cs:15-16`) is literally `=> signedAmount`. Flipping swaps in
`OverlayCombatMath.Finalize` (`:37-65`) for the live lawn. Three cases:

1. **Damage with an `elementPayload`** — the full resolver: element matchup, `combat.power`/
   `combat.defense` delta, hit/parry/block bands, crit.
   ⚠️ **A hit can now deal 0.** `OverlayCombatCalculator.cs:219` has a real miss branch, and the
   overlay profile has **no chip floor** — `CombatProfiles.cs:12`, `Overlay = new(0)`, versus **50‰**
   for battle/sim. A fully-mitigated overlay hit resolves to zero where today it always lands.
2. **Damage with no payload** — unchanged (`OverlayCombatMath.cs:42-47` returns `signedAmount`).
3. ⚠️ **Heals — the gap.** `Finalize` checks `signedAmount > 0` at `:39`, **before** the payload check,
   and routes to `FinalizeHeal`, which adds `combat.heal.power`. `git log -S FinalizeHeal` dates that to
   **2026-08-25 — five days after the C1–C10 run.** The proof's own C5 result (*"no overlay breakdown;
   heal pass-through"*) **no longer describes enabled behaviour.** Every overlay heal on the lawn would
   begin scaling with the healer's `heal.power`, on a path never live-tested.

### 4.2 No goldens move — verified, not assumed

- `OverlayCombatFeature` lives in `FusionRpg.Injector`; `tests/FusionRpg.Core.Tests/*.csproj:24`
  references **only** `FusionRpg.Core`. The test assembly cannot see the flag.
- `BattleGoldenTests` / `DominanceBaselineTests` run through `BattleRunState.cs:108` —
  `Calculator = new OverlayCombatCalculator();`, **unconditional**. Battle and sim never consult it.
- The flag has exactly two consumers, both injector-only: `EffectRuntime.cs:425-428` and
  `DebugCombatActions.cs:139,176,368`.

**This is a claim to re-test, not to trust.** Run the full suite before and after.

### 4.3 New proof cases — the actual work

| Case | Asserts |
|---|---|
| **C11** | An overlay heal **with** a payload scales with `combat.heal.power` |
| **C12** | An overlay heal with **no** payload — confirm whether it still routes to `FinalizeHeal` (the `signedAmount > 0` check precedes the payload check, so it likely does; **this is the specific behaviour nobody has observed**) |
| **C13** | A fully-mitigated overlay hit resolves to **0**, and the game handles a zero-damage hit without misbehaving (no division, no zero-damage death, no stuck state) |

C13 exists because the missing chip floor is a **behavioural** change, not just a numeric one.

---

## 5. Code style

Cheat-id defaults are data, not logic — one call each in the three registries, no branching. Script
additions match the existing case shape in `prove-overlay-combat.ps1` (named case, structured
`pass`/`detail`, JSON out).

---

## 6. Testing strategy

**Automated:** full Core, Guard and Data suites before and after the flip; diff the results. Any golden
move is a **stop-and-ask**, because §4.2 predicts none.

**Live (owner-run, cannot be automated):** `prove-overlay-combat.ps1` with C1–C13 on a real lawn, all
green, `-OutJson` committed. Then fill `debug-live-checklist.md`'s Pass column and replace
`04-proof-results.md`'s PENDING row with the real result.

> Paperwork is part of the deliverable here. The reason this module exists at all is that a passing
> proof sat on disk for nine days while the checklist said PENDING.

---

## 7. Boundaries

**Always**
- Re-run the proof on a real lawn before flipping. A nine-day-old green result is not sufficient for a
  path that changed five days after it.
- Set the default identically in all three registries.
- Commit the new proof JSON and update the checklist in the same change.

**Ask first**
- Flipping the default if **any** of C1–C13 fails.
- Changing `CombatProfiles.Overlay`'s chip floor. It is a separate balance decision — this module
  reports the zero-damage consequence, it does not fix it.

**Never**
- Flip the default without new heal coverage.
- Claim "no goldens move" without having re-run the suites.
- Edit `_prove-overlay-combat.json` by hand.

---

## 8. Success criteria

- [ ] C11–C13 exist in the prove script.
- [ ] C1–C13 all pass on a real lawn; JSON committed.
- [ ] `OVERLAY-COMBAT` defaults on in all three registries.
- [ ] Full Core/Guard/Data suites green, **no golden moves**, verified by running them.
- [ ] `debug-live-checklist.md` Pass column filled; `04-proof-results.md` PENDING replaced;
      `docs/README.md:73` corrected.
- [ ] ⛔ **This module does not unblock any aura on its own.** It gates the *reader*
      (`OverlayCombatCalculator`); the *writer* is blocked by R4 (`stat.derived` is `RuntimeState.None`
      on Lawn and Sim). An earlier draft claimed this module "unblocks 8 of 12 auras" — false. Flipping
      the flag with R4 unaddressed changes nothing about auras.

## 9. Open questions

1. **Should the overlay profile get a chip floor to match battle/sim's 50‰?** Out of scope here, but
   the zero-damage case (C13) is the evidence that would inform it. Owner call.
2. **Is a zero-damage overlay hit acceptable on the lawn**, or does it read as a bug to a player? C13
   answers the mechanical half; the feel half is the owner's.
