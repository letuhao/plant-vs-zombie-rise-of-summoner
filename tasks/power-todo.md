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

- [x] **P0.3 — Triage the 92 A3 findings** — done 2026-08-23. 92 → 75: three regex defects fixed in
  `scripts/audit-overflow.py` (hp/"hP" case collision — `KillEarnWithPatron`; A3 missing A2's
  per-mille-ratio exclusion — 14 `*Milli` bonuses/chances; `NOT_MAGNITUDE` missing `unit`/`peractor`
  — `ShieldUnit`, `MaxShieldsPerActor`), 17 findings removed by the fix, not waived. Remaining 75 =
  **56 LADDER** + **19 BOUNDED**, each BOUNDED verdict naming its proven cap (Harmony signature match
  ×10, Unity-field snapshot ×1, debug-scenario literal ×2, dev-time generation input ×1, clamped AI
  utility score ×3, retention tail ×1, plus their downstream lineage). Full breakdown:
  `docs/architecture/power/overflow-triage.md`. Verified: `--targets A3 | wc -l` == 75 == 56+19; A1/A7
  unchanged (0/14)

- [x] **P0.4 — Widen the LADDER bucket to `long`** — done 2026-08-23. All 56 triaged LADDER sites
  widened across `Core`, `Contracts`, `Data`, `Injector` (Stats/EntityBaseline, SimModels, StatMath,
  Battle/*, Demons/Patron/PatronPolicy, Effects/Atoms/Power/*, World/*, Contracts/Dtos+WorldDtos+
  EffectDtos, Injector/GameDumps+EntityStatWriter+CheatActions+GameHooks). Two dead-code sites deleted
  instead of widened (`UnityStatWriter`, `StatSystem`'s `int` `ScaleCurrentHp` overload — both zero
  callers, same shape as `IProgressionPowerProvider`). Fixed SSOT §11.2a's three narrowing casts
  (`EffectBag.cs:707`, `EventDrain.cs:458`/`:475`) plus the same defect at `EntityStatWriter.
  ForceSetPlantHp` and `WritePlant`/`WriteZombie`'s Atk/Arm1/Arm2 writes — all now clamp explicitly
  at the Unity-field boundary (`ZombieCombatFields.ClampToInt32`) instead of narrowing silently.
  Harmony-hook `int damage` sites (BOUNDED, §3.1 of the triage) deliberately untouched — clamped
  *into*, never widened, since the type is forced by the base game's own compiled method signature.
  - Verify: `audit-overflow.py` → A3 92→**19**, exactly the triage's 19 BOUNDED sites, line-for-line
    (confirms every LADDER site is resolved and nothing BOUNDED was touched); A1/A7 unchanged (0/14)
  - `ALL` (Core 2971, Data 470, Guard 73) green + Launcher 162, CheatCore 40 also run clean. One
    pre-existing failure fixed in passing (`DebugScenarios.AllowedStepNames` missing two step names —
    confirmed via `git status` to predate this work, zero relation to typing/overflow)
  - **Wire hash proof**: `BattleGoldenTests.cs`'s four named hashes (`StompHash`/`CloseHash`/
    `WipeHash`/`SeedSweepHash`, computed from `BattleReport` JSON built out of the widened
    `BattleActorSetup`/`BattleModels` types) passed **unchanged** — direct evidence the widening moved
    no golden, not an inference from "tests passed"
  - `git status --short tests\` is not literally empty — pre-existing, unrelated WIP already sits in
    the tree (`UniqueEquipmentCatalogTests.cs`, `UniqueActorStoreTests.cs`, confirmed via diff content
    to be a different, unrelated feature stream). This program's own test diff is exactly 3 files:
    2 compile-fixes preserving identical behavior (`BattleEffectHostTests.cs`'s `FakeTarget`,
    `PatronPolicyTests.cs`'s `long souls`) + 1 defect-fix explicitly sanctioned by this task's own
    accept criteria (`EventDrainIntegrationTests.cs` — the test asserted the `int32` clamp §11.2a
    exists to remove; rewritten to assert the exact merged value instead)

- [x] **P0.5 — A7 `double` in stat composition — decided: it stands** (SSOT §10.7). Range is not the issue (`double` is exact to Θ≈6.7M); determinism is, and `decisions.md:40` already mitigates it with the `BattleReport` platform stamp + cross-arch refusal. `Increased`/`More` are genuinely fractional — composing ratios in integers would be wrong. The `long` rule binds *magnitudes*, not ratio arithmetic

- [x] **P0.6 — Arm the overflow audit in CI** — done 2026-08-23. `scripts/guard-overflow.ps1`
  (new, matches the other 4 guards' shape) wraps `audit-overflow.py`; wired into both
  `.github/workflows/ci.yml` (new step, right after checkout — fails fast before the long test run)
  and `scripts/deploy-play.ps1` (5th guard, alongside single-writer/DAL/secondary-no-unity/funnel-delta).
  A3/A7 non-blocking by construction — the script's own exit code already reflects CRITICAL-only
  (A1/A2/A4); the guard just surfaces it as a build failure
  - Verify: planted a temporary `float probeHp` in `Core/_GuardOverflowProbe.cs` → guard exited 1
    (`A1=1`, `OVERFLOW GUARD FAILED`); removed it → exited 0 again (`OVERFLOW GUARD OK`). Both
    `deploy-play.ps1` and `guard-overflow.ps1` PowerShell-parse clean; `ci.yml` YAML-parses clean

### ✅ Checkpoint 0 — passed 2026-08-23
- [x] Audit exits 0, A3 = BOUNDED-only (19/19, triage §3) · triage doc complete
  (`docs/architecture/power/overflow-triage.md`) · `ALL` (Core 2971 + Data 470 + Guard 73) green,
  Launcher 162 + CheatCore 40 also clean · A7 recorded (SSOT §10.7, stands) · CI armed (guard red→green
  proven)

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

- [x] **M.1 — `contracts` (34)** — done 2026-08-23/24. `data/tuning/contracts.v1.json` (41 values:
  loyalty thresholds/gains/decay, rank-bonus Milli×3, slots, settlement, personality rates ×5×3,
  base-upkeep ×4, ritual price ×4). `ContractPolicy.cs`'s consts became config-backed static
  properties (kept the same public names/signatures — `ContractPolicy.WinGain` etc. still read the
  same everywhere, now via `Tuning.Loyalty.WinGain`); switch-expression literals became
  `ContractTuning` dictionary lookups (`RankFor`, `RankBonusMilli`, `Rates`, `BaseUpkeepPerDay`,
  `RitualPrice`), each still throwing the original exception shape on an invalid enum. New
  `ContractTuning.cs` (Core, pure parser — `ContractTuningLoader.Parse(string)`, no I/O per §7.2) with
  typed rejections naming the missing path (T5). Hosts load + inject: Server's `Program.cs` (file read
  + `ContractPolicy.Configure`, JSON copied next to the exe via a `.csproj` `<Content>` item matching
  the sqlite-data convention already there) and the shared `RpgHost.Initialize` (covers both BepInEx
  and MelonLoader hosts identically, same copy-item pattern in all three host `.csproj`s). Tests
  construct one inline (§7.2) via a `[ModuleInitializer]` bootstrap duplicated into `Core.Tests`,
  `Data.Tests`, `E2E.Tests` — same literal C# values as the JSON, not a file read.
  Minimal publish path built (§7.1): `tools/tuning/publish.py <domain> <dotted.key>=<value>` reads
  the latest `vN`, refuses an unknown key or a no-op, writes `v{N+1}`, leaves `vN` on disk (T4).
  Smoke-tested (`loyalty.winGain 15→99` published to a throwaway v2, both refusal paths hit their
  exit-1 branch), then the throwaway v2 deleted — only the real v1 ships.
  - Verify: `audit-magic-numbers.py --domain contracts` → **0/0/0/0** (was 15/10/0/9). Every test suite
    in `ci.yml` green post-migration: Core 2971, Data 470, Guard 73, Launcher 162, CheatCore 40,
    Server.Tests 15, E2E 194, ItemSeedValidator 71, AtomImporter 21, ElementEnumGen 14 — 4031 total,
    0 failures. `ContractPolicyTests.cs` alone asserts every migrated value directly
    (`Rates(Loyal)==(120,80,100)`, `Capacity(36)==48`, `RitualPrice(Legendary)==400`, …) — concrete
    byte-identical proof, not an inference from "tests passed"
- [x] **M.2 — `loam` (21) + `world` (30)** — done 2026-08-24.
  **loam**: `data/tuning/loam.v1.json`, 30 values (all of `LoamPolicy.cs` — its own class comment
  already declares every constant a provisional placeholder, so all 30 moved, not just the 21 the
  regex happened to flag; 9 escaped M2/M4 via a `capacity`-is-structural-vocabulary collision with
  `STRUCTURAL_WORD`, caught by the same manual full-file read discipline as P0.3). Same
  `LoamPolicy.Configure(LoamTuning)` shape as `ContractPolicy`.
  **world**: heterogeneous — 8 files, two different kinds of number. Migrated the 5 that are genuinely
  global policy (`data/tuning/world.v1.json` + `WorldTuningHub.Configure`, one call covers all five):
  `LaneTypeCatalog` (per-type cost), `WorldSizeCatalog` (per-tier node range), `StrengthBandCatalog`
  (per-band floor/ceiling/midpoint), `PlaceholderBattleResolver`, `TurnCalendar`. Row ids/names/
  structural flags stayed in C# (schema, not balance); only numeric fields moved — `Seed` on all three
  catalogs went from a `static readonly` field to a property so it reads the tuning lazily instead of
  at type-init (before `Configure` could plausibly have run).
  **Deliberately not migrated, with reasoning, not a skip**: `WorldTemplateCatalog.cs` (+ its
  `.TwoHearths.cs` partial) is one hand-authored starting scenario per template — sector layout, lane
  geometry, entity placement — not a reusable balance table despite the shared `*Catalog.cs` suffix;
  fragmenting a few Hp/LoamStock numbers into a flat JSON while the surrounding scenario stays in C#
  would cost readability for no tuning benefit a same-file edit doesn't already have. Recorded as an
  explicit, named exemption in `audit-magic-numbers.py` (`CONTENT_FILE`), not silently dropped.
  **Two more audit-tool bugs found and fixed**, same class as P0.3's: `TurnStartMilli` false-matched
  `BALANCE_WORD`'s `star` inside "turn**Star**t"; fixed with the same `(?![a-z])` word-boundary
  technique already used for the overflow audit's `hp`. `SectorTypeFlags.Fortress = 16` (a `[Flags]`
  bit value, not a balance number) is fixed by rewriting it `1 << 4` — idiomatically clearer *and*
  single-digit-exempt, no exemption list needed.
  - Verify: `--domain loam` and `--domain world` both **0/0/0/0** (were 21 and 30). Full suite green
    post-migration: Core 2971, Data 470, E2E 194, Server.Tests 15, Guard 73 = 3723, 0 failures.
    `audit-overflow.py` unchanged (A3 still exactly 19, the P0.3 BOUNDED set) — confirms the `1<<4`
    rewrite didn't touch anything overflow-relevant
- [x] **M.3 — `souls` + `patron` (26)** — done 2026-08-24. `data/tuning/souls.v1.json` (`SoulEarnPolicy`,
  full file: kill/match-end/discovery-by-rarity/codex, 13 values) + `data/tuning/patron.v1.json`
  (`PatronPolicy`, full file: switch cost, aura clamp, per-star, kill-soul cap, rarity-base×4, 8
  values) — two files, matching tunables-ssot §2's own domain examples ("contracts, **souls**...
  **patron**..."), not one merged file. `killCapPerMatch` and `killSoulCap` (T3.6's deletion targets,
  audit F11's `victoryFullPerDay` too) are now config rows, not hardcoded consts — **the coordination
  with T3.6 this task names is exactly that**: when Phase 3 is authorized, T3.6 deletes three JSON
  keys instead of fighting hardcoded consts, so this migration is what makes that a clean deletion.
  **Two more audit-tool false positives found and fixed** (same class, third and fourth of the
  session): `RarityBaseMilli`'s `_ => 60` (Legendary, PatronPolicy) is a genuine tunable the regex
  can't see behind a wildcard arm — migrated anyway, not left for the regex to decide. Both
  `SoulEarnPolicy.KillDelta = 1` and `PatronPolicy.PerStarMilli = 10` are single/exempt-digit
  literals the tool would never flag — migrated for consistency with SSOT §11.7a, which names
  `KillDelta` explicitly as a term in the future earn formula.
  - Verify: `--domain patron` **0/0/0/0** (was 8). `SoulEarnPolicy`'s 9 findings gone from the
    `demons` bucket (17→8; the remaining 8 are `SummonBannerCatalog`/`SummonRoller`, untouched, a
    later task's scope). Full suite green: Core 2971, Data 470, E2E 194, Server.Tests 15, Guard 73 =
    3723, 0 failures
- [ ] **M.4 — `status`, `fusion`, `shield`, `overlay`, `stats`, `combat`, `expeditions`** · M · deps: M.1
  - `fusion` (6→0), `shield` (3→0), `combat` (3→0) done 2026-08-24. `data/tuning/{fusion,shield,combat}.v1.json`.
    `CombatPolicy` is architecturally different (mutable per-match-override instance, not static
    consts) — `Configure()` assigns into the `Default` singleton's settable properties instead of
    backing them with computed reads. Found and fixed **three more default-parameter compile breaks**
    (a default parameter value must be a compile-time constant, so converting a `const` to a
    config-backed property breaks any consumer using it as one): `ShieldInnateDef`'s `Priority`
    default (record primary-constructor parameter — removed the default, made it required, 2 call
    sites updated), `FoundationHarness.GrantShield`'s `priority` default and a local test helper's
    same pattern (both switched to `int? priority = null` + `?? ShieldPolicy.PrioritySkill` in the
    body — zero of the ~20 existing callers needed touching). Also hit the inverse failure mode:
    removing `CombatPolicy`'s literal property defaults entirely (matching every other Policy class's
    T5 "no built-in default") broke `TargetResolverTests`/`CombatCounterTests`, which construct
    `new CombatPolicy { OneField = x }` directly and implicitly relied on the *other* fields' sane
    defaults — object-initializer construction that a `new CombatPolicy()`-only grep doesn't catch.
    Fixed properly: kept `Default`'s literal fallbacks (still overwritten by `Configure()` for the
    shared singleton) and added `CombatPolicy.FromDefault()` so a one-off override copies real values
    instead of zeroing everything else; both tests now build from that. Two more audit-tool false
    positives fixed the same way as the session's earlier ones: `ShieldMath.cs`'s `1_000_000` (a
    per-mille² renormalization denominator, same class as the already-exempt `1000`) and
    `ElementPayload.WeightSumEpsilon` (a floating-point comparison tolerance matched via "weight",
    not a balance number — added `epsilon` to `STRUCTURAL_WORD`).
    Verify: `--domain shield`/`combat`/`fusion` all 0/0/0/0. Full suite green: Core 2971, Data 470,
    E2E 194, Server.Tests 15, Guard 73 = 3723, 0 failures (one real regression caught and fixed along
    the way — see above)
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
