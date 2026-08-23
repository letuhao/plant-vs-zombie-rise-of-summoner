# Tasks — power program

Plan: [power-plan.md](power-plan.md) · Map: [../docs/architecture/power-map.md](../docs/architecture/power-map.md)
Specs: [../docs/architecture/power/](../docs/architecture/power/) · Standards: [tunables-ssot.md](../docs/architecture/tunables-ssot.md)

**Authorized:** Phase 0, Phase M, Phase D. **Phases 1–4 wait on owner approval of the map and specs.**

Shorthand — `CORE` = `dotnet test tests\FusionRpg.Core.Tests` · `ALL` = Core + Data + Server + Guard ·
`CLEAN` = `git status --short tests\` shows **no golden modified**.

---

## Phase 0 — numeric overflow

Thresholds: `float` breaks at **Θ 232** · `int` per-mille **3,213** · `int` whole **103,557** · `long` **214,748,300**.
Baseline: **0 critical, 92 A3, 14 A7**.

- [x] **P0.1** Standard in `CLAUDE.md`, `AGENTS.md`, `DESIGN-GATE.md`, the spec skill, and the patch record
- [x] **P0.2** `scripts/audit-overflow.py` — 7 categories, precision gate, `--targets` / `--fix A4`

- [ ] **P0.3 — Triage the 92 A3 findings** · S · deps: none
  - Accept: each classified **LADDER** / **BOUNDED** / **NOT-A-MAGNITUDE**; a BOUNDED verdict names its proven cap; a NOT-A-MAGNITUDE verdict **tightens the regex** rather than waiving the finding
  - Verify: `--targets A3` count == LADDER + BOUNDED
  - Files: `docs/architecture/power/overflow-triage.md`, `scripts/audit-overflow.py`
  - Order by concentration: `Core/Battle` 20 · `Injector` 12 · `Core` 11 · `Core/Stats` 9 · `Injector/Stats` 8 · `Core/World` 7 · `Core/Demons` 7 · `Core/Effects` 6 · `Contracts` 4 · rest 8

- [ ] **P0.4 — Widen the LADDER bucket to `long`** · M · deps: P0.3
  - Accept: every LADDER finding is `long`; **no golden moves** — widening preserves values
  - Verify: `ALL` + `CLEAN`; any `Contracts/` finding additionally proves its wire hash unchanged
  - ⚠ `BattleSetup` field changes move all four expedition hashes (`decisions.md:42`) — internal rename or alias
  - Also fixes SSOT §11.2a's three narrowing casts: `EffectBag.cs:707`, `EventDrain.cs:458`/`:475`

- [x] **P0.5 — A7 `double` in stat composition — decided: it stands** (SSOT §10.7). Range is not the issue (`double` is exact to Θ≈6.7M); determinism is, and `decisions.md:40` already mitigates it with the `BattleReport` platform stamp + cross-arch refusal. `Increased`/`More` are genuinely fractional — composing ratios in integers would be wrong. The `long` rule binds *magnitudes*, not ratio arithmetic

- [ ] **P0.6 — Arm the overflow audit in CI** · S · deps: P0.4
  - Accept: in `ci.yml` + `deploy-play.ps1`; fails on CRITICAL; A3/A7 non-blocking
  - Verify: plant an A1 → red; remove → green

### ✅ Checkpoint 0
- [ ] Audit exits 0, A3 = BOUNDED-only · triage doc complete · `ALL` + `CLEAN` · A7 recorded · CI armed

---

## Phase 1 — the ladder *(inert — no callers)*

- [ ] **T1.1 — `PowerTuning` + loader + typed rejections** · S · deps: none
  - Accept: `A` derived from the pin at load; **odd `bMilli` rejected** naming `b±1`; `PinBroken` / `FixedConstantChanged` / `NegativeWeight` / `TuningMissing`; **no fallback constants**; `WmMilli: null` legal at rest
  - Verify: `CORE --filter PowerTuning`
  - Files: `Core/Power/PowerTuning.cs`, `PowerTuningLoader.cs`, `PowerRejection.cs`, `data/tuning/power-scale.v1.json`
  - Core parses a stream; **the host does the file read** (tunables §7.2)

- [ ] **T1.2 — `PowerLadder.Value`** · S · deps: T1.1
  - Accept: `B=0 → A=30`, `Value(L) == 80+30L` across `[0,5000]`; `Value(20)==680` for every legal `B`; closed form ≡ iterated `ΔP` sum to Θ=2000; `maxIndex` from `B`, throws above
  - Verify: `CORE --filter PowerLadder`; source scan — no float, no `Math.Pow`, no literal outside the loader

- [ ] **T1.3 — `IPowerIndexProvider` + composer** · M · deps: T1.2
  - Accept: `Θ_actor`/`Θ_content` weighted, rounded **once**; `Explain(ctx).Total == ActorIndex(ctx)`; **`Wf != Wa` rejected**; uncapped runs asserted; `Wm` null → `ContentIndex` throws, `ActorIndex` works
  - Verify: `CORE --filter PowerIndex`

- [ ] **T1.4 — Host providers; delete `IProgressionPowerProvider`** · M · deps: T1.3
  - Accept: injector + server hydrate and inject; old provider **deleted** (zero `SetLevel` callers); un-hydrated injector returns `0`, matching old behaviour exactly
  - Verify: `ALL` + `CLEAN`; `.\scripts\guard-dal.ps1`

### ✅ Checkpoint 1
- [ ] Pin holds for every legal `B` · odd `B` rejected · `Wf = Wa` enforced · `ALL` + `CLEAN`

---

## Phase 2 — adoption at `B = 0` *(zero goldens move)*

- [ ] **T2.1 — `battle-magnitude`** · M · deps: T1.2
  - Accept: per-channel `C`/`A` with **`B_ch = B × pin_ch / pin_hp`** (audit F1 — a shared absolute `B` gives defense `A = −2.8`); all three exact across `[0,5000]`; every channel's `A > 0` for every legal `B`
  - Verify: `ALL` + `CLEAN`; shield suite green; no shield file references `BattleRuleset`
  - ⚠ `BaseHp` means two things — the ladder, and shield HP across 8 files. **No grep-and-replace**

- [ ] **T2.2 — `battle-rates`** · S · deps: T1.2
  - Accept: arithmetic unchanged; `P(hit)` parity `0.90±0.02` at Θ ∈ {1,5,10,20,100,1000,**10000**}; **PS-3 tripwire** — outputs identical at `B=0` and `B=1000`
  - Verify: `ALL` + `CLEAN`
  - ⚠ Must **never** call `PowerLadder.Value` — under a dial that makes a fixed gap unboundedly decisive

- [ ] **T2.3 — `content-authoring`** · S · deps: T1.3
  - Accept: values unchanged (1/3/6/10); expedition inheritance through the wave chain **asserted**; `BattleSetup` rename **internal only** (F7)
  - Verify: `ALL` + `CLEAN`; `BattleSetup` hashes byte-identical
  - Includes D.4 (`ssot-generation.md` §4.1 correction)

### ✅ Checkpoint 2 — the vertical proof the program rests on
- [ ] hp/atk/defense travel **Θ → P(Θ) → `BattleRuleset`** end to end
- [ ] All three exact vs shipped formulas across `[0,5000]`
- [ ] Parity holds to **Θ = 10,000**; PS-3 tripwire passes
- [ ] `ALL` + **`CLEAN`** — a moved golden here means the ladder is wrong, not the golden

---

## Phase 3 — fixes and new consumers *(goldens move, knowingly)*

- [ ] **T3.1 — `ResistFromPowerRatio` 0 → 1.0** · XS · deps: T1.4
  - Accept: matched pair contests at `delta = 0` at every Θ — **including under the un-retired curve**; `delta` antisymmetric; the shipped test asserting `delta == 1.0` for two identical actors updated to `0.0` (it encoded the bug)
  - **Land before T3.2** — it makes the system safe to look at while the curve is in review

- [ ] **T3.2 — Retire the curve, the divisor, the netFactor cliff** · M · deps: T3.1
  - Accept: `progression.power = Θ`; `effectiveApplyScale = ApplyScaleK` (F3 — the scaled divisor makes a fixed gap *decay* under linear Θ); `netFactor = 1 + delta/NetFactorScale`, `delta==0` branch deleted; **red test flips `4096 → 1.0`**
  - Verify: `ALL`; `.\scripts\prove-status-full.ps1`; every moved golden attributed

- [ ] **T3.3 — Delete `RpgXpPowerScale`; propagate docs** · S · deps: T3.2
  - Accept: no `src/` reference; kill XP unchanged (stub returned 1.0, removal inert); `rpg-progression.md` + `actor-hub-ssot.md` amendment notes flipped from pending to current
  - Verify: `ALL` + `CLEAN`

- [ ] **T3.4 — `content-scale`** · M · deps: T1.3, T2.3
  - Accept: `contentScale = P(Θc)/pinValue` **from tuning**, not a `680` literal (F5); after the roll, in `Instantiator` only; instance records scale + Θ; **corpus byte-identical at Θc=20**; missing Θ rejects, never a silent 1.0
  - Verify: `CORE`, `Data`, `python -m seedsmith validate`; double-application and `PowerVector`-unscaled tripwires fire when violated

- [ ] **T3.5 — `caps-reconcile`: the three bounds** · M · deps: T3.4
  - Accept: **derived, not `1e9`**, and they **throw, never clamp**; `MaxSoulAward` is **dynamic** (`int64Max − balance`, F12) not a constant; one policy across `AwardSouls` and the expedition path (which clamps today); each bound declares what it reads, graph acyclic (F13)

- [ ] **T3.6 — `caps-reconcile`: the deletions + the earn formula** · M · deps: T3.5
  - Accept: `MaxSlots`, `KillCapPerMatch`, `KillSoulCap`, **`VictoryFullPerDay`** (F11) deleted; slot 512 purchasable at 150,300; warden property holds (it depended on price, not the ceiling); **stall-farm regression green on the new formula**; all 41 exempt caps carry a class comment
  - Formula lands **in the same commit** as the deletion (SSOT §11.7a): `soulsPerKill = KillDelta × contentScale(Θ_enemy)`, victory/defeat likewise. Constants unchanged → **no-op at Θ=20**, no inflation window
  - Regression asserts **souls-per-minute**, not per-match — what the original incident measured

### ✅ Checkpoint 3
- [ ] 3a matched pair at `delta=0` every Θ · 3b red test flips + corpus identical at the pin · 3c bounds throw, soul bound dynamic, stall-farm green on the new formula

---

## Phase 4 — seal

- [ ] **T4.1 — `power-guard`** · M · deps: Phases 1–3
  - Accept: four checks (no literal curve · no private `f(level)` outside `Core/Power` · **no new curve vs `inventory.json`** — F6, a scanner diffs a baseline, it cannot judge "power-shaped" · pin holds for every tuning version); **false-positive survey before arming**; every allowlist entry carries a reason; fails closed
  - Known gap, stated not hidden: scans `src/` only — `tools/` holds no C#

- [ ] **T4.2 — `power-dial`: `B` 0 → 400** · S · deps: T4.1
  - Accept: **one field, one commit**; `RulesetVersion` 2→3; `v1` retained; every moved hash **triaged before re-blessing**; re-bless is a *separate* commit with the before/after table
  - Verify: **zero rate goldens move** — one that does means `battle-rates` has a PS-3 violation and **the module stops**. Nothing at the pin moves. `v1` revert restores every pre-dial hash

### ✅ Checkpoint 4
- [ ] Guard armed and proven · dial moved zero rate goldens · nothing at the pin moved · revert proven

---

## Phase M — magic numbers *(parallel with 1–4, after Phase 0)*

Baseline **329**: M1 111 · M2 80 · M3 88 · M4 50, across 37 balance-surface files.
**Values unchanged in every task** (T7) — extract, prove byte-identical, tune separately.

- [x] SSOT + standard in `CLAUDE.md`, `AGENTS.md`, `DESIGN-GATE.md`, spec skill
- [x] `scripts/audit-magic-numbers.py` — 4 categories, `--summary` / `--domain` / `--targets`

- [ ] **M.1 — `contracts` (34)** · M · deps: none
  - Accept: `data/tuning/contracts.v1.json`; loyalty thresholds, rank bonuses, `PersonalityRates`, slot price all config; **behaviour byte-identical**; builds the minimal publish path (tunables §7.1 — the first domain builds the tool, not a general CLI up front)
  - Verify: `CORE` + `CLEAN`; `--domain contracts` clean
- [ ] **M.2 — `loam` (21) + `world` (30)** · M · deps: M.1
- [ ] **M.3 — `souls` + `patron` (26)** · M · deps: M.1 — coordinate with T3.6
- [ ] **M.4 — `status`, `fusion`, `shield`, `overlay`, `stats`, `combat`, `expeditions`** · M · deps: M.1
- [ ] **M.5 — `vfx` (68)** · M · deps: M.4 — largest count, lowest stakes, deliberately last
- [ ] **M.6 — Arm the magic-number audit in CI** · S · deps: M.5 — blocking on M1/M2 **for migrated domains only**

### ✅ Checkpoint M *(per domain)*
- [ ] Numbers in config · behaviour byte-identical · `CLEAN` · audit clean for that domain

---

## Phase D — doc reconciliation *(parallel, no code, no dependencies)*

All four are **unbuilt features**. Reconciling now is free; after they ship it is a rebalance.

- [ ] **D.1 — Enhancement `+X`: uncap** · S — risk formula as the soft cap: falling success rate, break / level-loss on failure, all configurable. The shipped bands (Safe +1–8 / Risk +9–14 / Peril +15– / level-drop from +17) are already this shape and stop at 20 for no reason · `ssot-enhancement.md`
- [ ] **D.2 — Rarity promotion: soft cap** · S — per-rarity adjustable cost; the ladder extends with future rungs, so the ceiling is a table row, not a constant · `ssot-rarity.md`
- [ ] **D.3 — PvZ item drop caps: remove** · XS — 2/run and 12/day. A daily cap is a stamina gate, and `standalone-rpg-map.md` already ruled *"with no monetization a stamina gate has no honest job"* · `ssot-generation.md` §4.6
- [ ] **D.4 — `ssot-generation.md` §4.1** · XS — describes three `contentLevel` sources that do not exist; expeditions inherit through the wave chain · folded into T2.3 if that lands first

### ✅ Checkpoint D
- [ ] Four specs corrected · no code touched · each states it is a pre-build reconciliation

---

## Review gate

Not a blocker — Phases 0, M and D are authorized and need nothing from anyone.

- [ ] **Owner approves the map + 10 specs** → unlocks Phases 1–4

**Welcome, not owed:** the world program may confirm or move `Wm = 5` · the demon/economy stream may
retune the soul constants it already owns (unchanged today, so silence is a valid answer).

**Everything else from this session is decided** — the earn formula (SSOT §11.7a), `Wm`, the §10.4
economy split, and the ADR P1 amendment (written into `decisions.md`).

---

## Notes

- **`tools/` is not a C# blind spot here.** `--paths src tools` returns identical counts because
  `tools/` holds no `.cs`. The `guard-dal.ps1` gap in `DESIGN-GATE.md` §1 is about SQL in Python
  tooling and does not apply to these audits.
- **Both audit tools' first runs were mostly false positives** — overflow reported 121 criticals (all
  wrong), magic-numbers 365 (comment text and single-digit arithmetic). Precision gates were added
  both times. Worth remembering before trusting any new scanner's first number.
