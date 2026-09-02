# Spec review — effect-atom Waves 7/8, 2026-09-03

**Adversarial review of all 15 specs**, ~60 `file:line` claims opened and checked. **Baseline verified:**
`AttachPointCount = 5`, `KindCount = 12`, `TriggerCount = 8`, 12 opcodes, 267 channels, 21 shipped atoms,
98 authored families, **33 rejection codes**.

**Structural completeness: all fifteen** carry what-exists-with-`file:line`, a contract, boundaries, at
least one planted-violation test, numbered criteria and dependencies. No spec is missing a section.

---

## 1. Fixed 2026-09-03 — every one was in a spec I wrote

| # | Finding | Fix |
|---|---|---|
| **D1** | **E30 invented a 34th rejection code**, `UnknownPool`. `definitions.md` §10: *"Thirty-three codes. Adding one is a reviewed change."* `AtomKindRegistryTests.cs:33` asserts `Assert.Equal(34, …)` (33 + `None`) and would have gone red — **and `spec-projectile-control.md` §3 states the same closure independently**, so my spec contradicted another | ✅ `BadParamValue`, reason recorded |
| **C4** | **E30 forbade building the resolver its own tests assert.** §4 said *"do not implement the resolver"*; tests 1–3 require resolution behaviour. **A spec cannot forbid the thing its acceptance depends on** | ✅ E30 declares a dependency on **effect-pipeline module 2**; those become integration tests, and Checkpoint I is verified jointly |
| **C3** | **E29's rule-4 example was false.** It claimed `wither` is *"inert on one runtime"*. `wither` is `StatusKind.OverTime`/`PulseHp` — one of the **13 overlay-authored** statuses resolved in `StatusRuntime`, which **is** mounted in the injector (`EffectRuntime.cs:19,31`). The **8** is the Unity CC switch size, not what `status.apply` reaches. `spec-plant-side-status.md` §2c had it right | ✅ Corrected. **The rule itself was sound and is unchanged** |
| **O1** | **E26's headline CI claim is not delivered by its own contract.** The parity test compiles only shipped `fx-*.json`, all 21 classify Compiled, and §4 forbids widening `Compilability.Classify` — so `compiled.Runtime` stays empty and the assertion never fires | ✅ E26 owes a stated choice: **ship a runner-shaped fixture** (preferred — a repaired path nothing exercises is D6's exact failure) or hand the assertion to E43 |
| **C5** | E30 and E38 filed **E42 as a hazard** where map §12/§14 make it a **prerequisite** | ✅ E30 declares it. **E38 still to do** |
| **M6** | E30 had **no criterion** for reconciling the 98 authored families, which map §12 assigns it | ✅ Criterion 8 added |
| **O4** | E27 called the species→element lookup *"the one genuinely new piece of work"* without noting `ResolveElementTypesFromHub` **already** captures a board snapshot per resolve — it **is** the per-hit scan the perf audit blamed | ✅ Reframed as a **repair**: leave the path faster than found |

Plus line-cite corrections in E26, E28 and E29.

### Closed later the same day — the whole of the former §2

Every item below was a *"required before build"* finding. Each is now written into the spec it names,
carrying a **⛔ CORRECTED 2026-09-03** note with its reason, so the wrong version stays visible
rather than being quietly overwritten.

| # | Finding | Fix, and where it lives now |
|---|---|---|
| **C1** | **The Wave 8 counts were absolutes and none of them agreed.** E35 said `5 → 6` / `12 → 13`, E36 `13 → 14`, E37 *"kind #13"* **and** *"No new attach point. Five today"* in one section, E41 *"becomes 6"* / *"exactly 6 (or 7 with E35)"* with **no `KindCount` at all**, E40 *"`KindCount = 12` … `AttachPointCount = 5` … untouched"* — false the moment E35 lands | ✅ **Every count claim in all five is now a delta.** The Wave 8 end state (`AttachPointCount = 7`, `KindCount = 16`, from `5` and `12` today) is stated **once**, in `spec-match-modify.md` §2.1, and referenced by the other four. E37's self-contradiction and E41's hedged *"(or 7 with E35)"* are gone; E40's criterion 8 reads *"not changed **by this module**"*. Both guards are `Const == BuiltCount` self-consistency checks (`AtomKindRegistryTests.cs:22-23`), so every test asserts a delta and none carries a literal. **And the 15 was wrong — corrected to 16 the same day.** The four Wave 8 kinds sum to **12 + 4 = 16**; the 15 originated in this review record and propagated into every spec citing it. The agent applying these corrections used it as directed and **flagged the discrepancy instead of reconciling it silently**, which is the only reason it was caught. `AttachPointCount = 7` was always right |
| **D2** | **E41's `ui.present` (`PowerCategory.None`) cannot pass `AtomKindRegistryTests.cs:71`**, which asserts `kind.Categories != PowerCategory.None` with no exemption list | ✅ New §2b.1 in `spec-ui-attach-point.md` names the test and its method, and states the amendment exactly: a `cosmetic` exemption set beside the existing `permanentModifiers` one at `:53`, with `:71` made conditional in **both** directions — a cosmetic kind must price to no category, every other kind must not. Rejects the alternative (give it a category) with its reason: a zero coefficient is a different claim from no category. Criterion 1 and two test rows carry it |
| **D3** | **E37's `bullet.modify` (`AtomTriggers.None`) cannot pass the same test**, which allows an empty trigger list only for `permanentModifiers = { "stat.derived" }` (`:53`, `:66-69`) | ✅ New §2b.1 in `spec-projectile-control.md` quotes `:53` and `:66-69`, states it goes red at `:69` with the exact message, and specifies the amendment: `permanentModifiers` becomes `{ "stat.derived", "bullet.modify" }` with a comment saying why. Rejects giving it a trigger — nothing raises one, which is the `status.expose.*` defect. Criterion 4, a test row and the CI-gates hazard row carry it |
| **D4** | **E35's `long` could not survive the path E35 mandates.** `zombieStartAmmor` is specced `long`, written via `CheatState.SetFloat(id, **double**)`, read back as `IVal` (**int**), round-tripped through `FVal` (**float**) | ✅ **A choice is made and written.** `spec-match-modify.md` §2.3 requires a **`long` channel on `CheatState`** (`SetLong`/`LVal`, `E-ZARM` only), keeping the `checked` narrow to the host `int` as a throwing bound. The rejected option — *"prove it bounded"* — is rejected with its reason: the `int` ceiling is real, but `FVal`'s `(float)` cast stops being integer-exact at **16,777,216**, far below it, so the value dies silently before the bound is ever reached. Cites `CheatState.cs:277-288`, `:309-312`, `CheatActions.cs:657`, `:684`. Criterion 7 and a round-trip test row carry it |
| **D5** | **E35's match-end restore clobbers operator cheat state.** `LoadBoardConfigIntoCheats` writes **all eleven** `E-*` keys and `SetFloatQuiet` sets `IsSet = true`, so every match end replaces a hand-set cheat value with the level's own | ✅ `spec-match-modify.md` §2.6 carries the correction with `CheatActions.cs:677-687` and `CheatState.cs:388-394`, and replaces the blanket call with a **scoped** restore: only ids a live `match.modify` grant wrote, cleared rather than overwritten, `BoardConfigLocked` cleared only when nothing else holds an `E-*` key user-set. The two shipped callers (`GameHooks.cs:471-477`, `CheatCommandRunner.cs:650`) are explicitly untouched. Added to §3's boundaries, criterion 8, and a third planted-violation test |
| **D6** | **`OnGridPlace`/`OnSunCollect` can match a `zombie:{tid}` grant** — that branch names no triggers, and E34 sets `TypeId` = grid item type | ✅ `spec-trigger-vocabulary.md` §2.4 rewritten. It shows the plant branch is narrowed (`EffectProcAndOwner.cs:14-28`) and the zombie one is **not** (`:30-44` — side-only gate at `:32-40`, unconditional return at `:41-43`), names the concrete leak — *"a `zombie:7` grant fires on every placement of grid item type 7"* — and arms **both** branches for **all five** new triggers, `BoardEconomyEvents` included. Criterion 8 rewritten, plus three test rows and a planted violation that drops only the zombie half |
| **D7** | **E33's claim that zombie type-keyed grants never see `OnActivate` is false.** That branch is not narrowed, Battle raises `OnActivate`, and *"add a clause to both branches"* is a behaviour change dressed as a wiring fix | ✅ `spec-activation-edge.md` §1 and §2.3 corrected. §2.3 splits the work in two: the plant branch is a **wiring fix**, the zombie branch is a **narrowing behaviour change on a branch Battle's live path flows through** (`BasicAttack.cs:87-94`), to be named in the commit and the rollout note. It also records *why* nothing misfires yet and why that is luck — Battle's emit sets no `Side` and no `TypeId` (`EffectDtos.cs:66-83`), while **E33's own `actor.activate` capture sets both**. Criterion 5 rewritten; the planted-violation set grew from two to four and covers both branches |
| **C2** | **E33 and E34 asserted mutually exclusive contract counts** — 8 and 13 — and only E34 mentioned the other | ✅ `spec-activation-edge.md` §2.1 states the merge order from its own side (E33 → 8, E34 → 13, or merge both edits into one change) and rewrites the assertion as *"publishes every constant declared in `EffectTriggers`, and no others"*, which is green at both counts. `spec-trigger-vocabulary.md` §2.1 points back at it and says not to replace it with a literal. New hazard row in E33 |
| **M3** | **`/effects/contract`'s `actions` array also lies** — ten of twelve opcodes, `GrantShield` and `ModifyDerivedStat` missing, under `frozen = true` — and both modules that made *"a published list that lies"* their principle fixed `triggers` only | ✅ **Assigned to E33**: new §2.1a in `spec-activation-edge.md` repairs both existing holes and writes the assertion in the same *"every constant in `EffectActions`, and no others"* form, so later opcodes need no further test edit. New criterion 3. E34 states it adds no opcode and names E33 as the owner. **E35 §2.5, E36 §2.1 and E37 §2b.2 each now state that adding an opcode means growing the array**, each with a criterion asserting it |
| **O5** | **E35 criterion 1 was unsatisfiable** — it required amending a `decisions.md` attach-point row, and `grep -in "attach"` returns **nothing**, though `AtomKind.cs:4` says *"Five, guarded by ADR"* | ✅ `spec-match-modify.md` §2.1 now says E35 **creates** the row, and specifies what it must record: the closed list, its guard test, and that growth is a reviewed change to that row. Criterion 1 and the §6 hazard row rewritten. `spec-ui-attach-point.md` §2a states the same from E41's side — whichever lands first creates it, the other amends it |
| **smaller** | E38 missed four of its twelve fields' guard shapes, and **`P-ATK-ADD` has no value guard at all** | ✅ New table in `spec-entity-fields-12plus.md` §2b: **three** shapes, not one — seven keys `>= 0`, four keys `> 0` (`EntityStatWriter.cs:117`, `:119`, `:145`, `:147`), and `P-ATK-ADD` unguarded (`:113-114`). Each shape states what the promotion must preserve, and `P-ATK-ADD` gets a written decision rather than a copied guard. Criterion 5 and three test rows |
| **smaller** | E38 did not name the pricing-sign trap `LowerIsBetter` creates on its own headline channel | ✅ New subsection in §2c. §2c said *"the two countdowns"* while §2a marks **three** channels `LowerIsBetter`; the third is `takeDmgMultiplier`, and the flip at `CostFunction.cs:74-75` prices a **raise** — the *"takes +X% damage"* debuff the module exists for — as **negative power**, the same failure `:60-63`'s own comment records happening once already. Two frames laid out, the bearer frame recommended, a test added on the non-obvious direction. Criterion 6 rewritten |
| **smaller** | E28's *"84%"* was unsourced, though the mechanism was verified | ✅ Number dropped for the checkable claim — *"every non-zombie spawn prices at exactly zero"* — with all three `file:line` cites (`CostFunction.cs:193`, `AtomKindRegistry.cs:294-295`, `:297-298`) opened and verified. The note says a share of the corpus is a count someone runs, not a figure a spec asserts |
| **smaller** | E37 asserted a five-member `moveWay` set with **no assembly sweep**, where E39 and E40 both mandate one first | ✅ `spec-projectile-control.md` §2a: only **two** `BulletMoveWay` members appear anywhere in `src/` — `Track` (`CheatPrefixes.cs:87`) and `MoveRight` (`DebugActions.cs:146`). The set is marked **UNVERIFIED**, an `Assembly-CSharp` sweep is E37's first task, and the E17 precedent is cited the way E39 cites it (`StatusCatalogBootstrap.cs:36-50`). New criterion 0, a test row and a boundary |

**Still open, and named rather than closed:** `EffectAtomCatalogGeneratedTests` (exactly-16-ids) is
covered by no spec; map §16 assigns it to E43, and E43 is where it belongs. That was the one item in
the former §2's *"Smaller, recorded"* paragraph that is not a Wave 7/8 spec's to fix.

---

## 2. Required before build

**Empty as of 2026-09-03.** Every finding that stood here has been applied to the spec it named and
moved into §1 above, with the fix and its location recorded per item. The specs edited were
`spec-activation-edge.md` (E33), `spec-trigger-vocabulary.md` (E34), `spec-match-modify.md` (E35),
`spec-wave-control.md` (E36), `spec-projectile-control.md` (E37), `spec-entity-fields-12plus.md` (E38),
`spec-spawn-non-grid.md` (E40), `spec-ui-attach-point.md` (E41) and `spec-param-parity.md` (E28).

Two things carried forward rather than closed, both named in §1:

- **`EffectAtomCatalogGeneratedTests`** (exactly-16-ids) belongs to **E43** per map §16.
- **The Wave 8 kind arithmetic.** The end state is recorded as `KindCount = 16`, and the four kinds
  Wave 8 adds sum to **16**. `spec-match-modify.md` §2.1 is the single place that states a total, and
  it flags the discrepancy for the owner. Every module states only its own delta, and both guards are
  self-consistency checks, so no test depends on which number is right.

---

## 3. What the review verified as correct

**E38's twelve-field table — all 12 cites exact.** E26's runner chain and route list, complete for
today's code. E27's element chain, including *"no call site passes `elementTypes:`"* — **confirmed**.
E28's six of seven rows, plus the `boxType: 1` content defect and the `selector: "last"` naming lie —
**both confirmed**. E29's `resource.economy` correction (`SetEconomy` has **no `default:` arm**, so the
silent no-op is real). E33's plant-branch claim, exactly right. E34's **eight** emit sites, all verified,
and the `wave.change`/`wave.spawn` double-fire. E35's eleven sink arms and its set-only reasoning.
E36's `30f` floor — **the "`hold`, not `freeze`" correction is right** — and a well-founded `ChainDepth`
guard. E39's 21 = 8 + 13 split. E41's `ActorHudResources.Meters` finding: declared, serialized, **no
producer anywhere**.

**E32 is the strongest spec in the set** — all four breaks verified, and the `"atom"`→`AffixId` window is
genuinely open (3 container files, 6 entries, **zero `pool` keys**). **E39 is second**, and its *"sweep
the assembly before wiring an unverified game symbol"* discipline is the model E37 should have copied.
**E40's *"widen `kind`, don't add a kind"* is the best-reasoned scope decision in Wave 8.**
