# Spec: validate-gate-ci (E47)

**Status: DRAFTED 2026-09-03** — from the Wave 6 retrospective backfill, which found it while writing
E24's record. Module **E47**, effect-atom. **No dependencies.**

**What it owns: making the content validation gate actually gate.** E24 `validation-in-ci` shipped
`AtomImporter --validate` and wired two missing test projects. **It never wired the validate step
itself**, so the gate the module is named for is reachable only from a hand-run command line.

---

## 1. The defect

Verified 2026-09-03. `.github/workflows/ci.yml`'s only tool invocations are `DemonSpeciesGen --check`,
`DemonCorpusDump --verify` and `ItemSeedValidator`. It runs **`FusionRpg.AtomImporter.Tests`**
(`ci.yml:97`) — the tool's *tests* — and never the tool.

**So `ContentValidation.Lint` and `.Drift` run over the real shipped corpus in nobody's pipeline.**

**Sorted: wiring gap.** Everything works; one step is missing. E24 delivered **B5** (the two missing test
projects plus the general guard for the next unwired suite) and **B4's gate is unwired** — the module's
name overstates what shipped, which is exactly the kind of thing a retrospective spec exists to catch.

> **The irony is worth keeping:** E24's own general guard exists to catch *"the next unwired suite."*
> It did not catch E24.

---

## 2. What exists today

| Thing | State | Evidence |
|---|---|---|
| `AtomImporter --validate` | **built** | `tools/AtomImporter/` |
| `ValidationGate.Decide` — turns findings into an exit code | **built** | `tools/AtomImporter/ValidationGate.cs` |
| `ContentValidation.Lint` / `.Drift` | **built**, production-shaped | — |
| Three `ShippedSeed()` tests sweeping the real tree | **built**, and they **do** run in CI | `ContentValidationTests` |
| `--validate` as a CI step | ⛔ **absent** | `ci.yml` — no such invocation |
| Budget checking | **skipped** — no ceiling data exists in the schema | E24's own record |

**Note what this means:** the *lint rules* are partly covered by the `ShippedSeed()` tests already in
CI. What is not covered is the **gate** — the exit code that stops a merge.

---

## 3. The contract

### 3.1 The step

One step in `ci.yml`, in the same block as the four boundary guards, following their exact shape —
run, check `$LASTEXITCODE`, `throw` with a message naming what failed.

**It must run against the real `data/seed/` tree**, not a fixture. A gate over a fixture proves the gate
works and nothing about the content.

#### ⛔ DECIDED 2026-09-03 (owner removed themselves as a gate) — the invocation is `--check --validate --db <scratch>`

```yaml
      - name: Content validation gate (E47)
        shell: pwsh
        run: |
          dotnet run --project tools/AtomImporter -c Release -- --check --validate --db "$env:RUNNER_TEMP/atom-validate-db"
          if ($LASTEXITCODE -ne 0) { throw "content validation gate failed - see the lint / power drift lines above" }
```

**Three flags, each forced by a line in `Program.cs`:**

| Flag | Why it is not optional |
|---|---|
| `--db <scratch>` | Without it the tool resolves `$FUSIONRPG_DATA`, then `FindUp("dist", "FusionRpg.Server", "data")` (`tools/AtomImporter/Program.cs:42-44`). A fresh CI checkout has no `dist/`, so `dataDir` is null and the tool **returns 2 before reading a single seed file** (`:45-49`). A gate that exits 2 on every run is a gate nobody reads |
| `--check` | `if (validate)` runs at `:126`, **after** `store.ImportContent(collected.Content, dryRun: check)` at `:107`. `--check` sets `dryRun: true`, so the batch is validated inside a transaction that is rolled back — the gate reads the real corpus and **writes nothing**. Without it CI would be performing a real import into a throwaway database, which is a slower way to reach the same verdict and one more thing that can fail for reasons the content did not cause |
| `--validate` | The gate itself (`:126-137`) |

**No code change is needed to wire the step**, and that is the point of choosing this shape: a
`--check --validate` pair is two existing flags composed, and the scratch directory is created by
`RpgStore.Init()` itself (`src/FusionRpg.Data/Sqlite/RpgStore.cs:47`), so the step needs no `mkdir`
either. **What would overturn it:** if `ImportContent`'s dry-run path is ever changed to skip work the
non-dry path does — at which point `--check` would validate less than a real import, and the step
should drop `--check` and keep the scratch `--db`.

### 3.2 ⛔ What the gate must do about today's findings

**This is the part that needed deciding, not just wiring — decided below, 2026-09-03.** At today's 21 atoms the lint is quiet. **After
E43 emits ~490 rows it will not be** — the review already measured the shape: `orphan` and `orphan-affix`
fire once per atom that no container references, and a generated corpus is exactly that until containers
catch up.

So the gate needs a stated policy **before** it is wired, or its first real run is a wall of noise
someone will disable:

> **⛔ CORRECTED 2026-09-03 — the table below was written against a finding class that does not exist,
> and against a rule count that was wrong.** `ContentValidation` emits **nine** rule strings, not four:
> seven lints (`tier-gap`, `flat-tier`, `duplicate-affix`, `backwards-interval`, `lonely-group`,
> `orphan`, `orphan-affix` — `ContentValidation.cs:165-171`), plus `drift` (`:108`, `:122`) and
> `budget` (`:71`). **There is no structural finding class at all**, so row 1 was unwritable as
> written. The corrected table below is the shipped set.

#### ⛔ DECIDED 2026-09-03 (owner removed themselves as a gate) — the policy, against what actually exists

**Structural defects never reach the gate, because they already fail earlier.** A file that will not
parse, an unknown kind, a bad param and a duplicate id are refused by `AtomSeedFile.Collect` and
reported at `Program.cs:78-83` (exit 1); a row the catalog refuses is reported at `:120-124` (exit 1).
Both are **upstream of `if (validate)` at `:126`**, so the step already fails the build on them and no
gate policy is needed — or possible. **Say this in the step's comment**, because "structural findings
fail" reads like an unimplemented requirement rather than a closed one.

| Finding class | Emitted where | Gate behaviour | How it gets that behaviour |
|---|---|---|---|
| **Structural** — unparseable file, unknown kind, bad param, duplicate id | `Program.cs:78-83` / `:120-124` | **Fails the build already** | Exit 1 before the gate runs. Nothing to wire |
| **`drift` without a `powerNote`** | `ContentValidation.cs:108`, `:125` | **Fail** | Already `Blocking: true`; `ValidationGate.Decide` already returns `Ok: false` |
| **`drift` with a `powerNote`** | `:124-125` | **Report** | Already `Blocking: false` — the note is the author saying the cost function is wrong here, which is the running list E14b wanted |
| **All seven lints** | `:165-171` | **Report** | Already `Blocking: false` by construction (`:148` — *"Every one warns; none blocks"*) |
| **`budget`** | `:71` | **Not run** | §3.3 |

**So the gate as shipped already implements this policy, and E47's work is the step plus the words.**
That is the honest finding, and it is better than the alternative: every row above is a fact about
`ContentValidation`'s own `Blocking` flags, so the policy cannot drift from the code by being restated
here — it *is* the code.

**What would overturn it:** a lint whose findings turn out to be reliably real defects rather than
usually-typos. That is an argument for changing `Blocking` on that rule in Core, with its own test —
not for a CI-side promotion list.

#### ⛔ DECIDED 2026-09-03 — the `tier-gap` fail-switch is dropped

The original row read *"**Report** while a corpus is being built out; **fail** once E43's output is
stable"*, and left *"stable"* undefined. **It is dropped rather than defined**, for a reason that is
about where the rule lives rather than about tier gaps:

`tier-gap` is a **lint**, and `ContentValidation`'s own doc comment states the split as a rule —
*"Validations fail; lints warn. A budget breach is a mistake; a tier gap is usually a typo but
occasionally deliberate. Filing them together would mean either blocking on a guess or shrugging at a
real error"* (`ContentValidation.cs:37-39`). A CI switch that promotes one lint to blocking would put a
second, contradicting answer to that question in a YAML file, where the reasoning is invisible to
everyone reading Core.

**And no definition of "stable" was available to write.** Nothing in the repo measures corpus
stability, so any threshold would have been a number invented to fill the slot.

**If E43's output later shows tier gaps that are real defects**, the change is a `--strict-lint <rule>`
promotion list in `ValidationGate.Decide` (`tools/AtomImporter/ValidationGate.cs:14-28`) — tool-local,
one file, one flag, reversible by deleting the flag from the CI step. **That is a follow-up with a
trigger, not an open question**: the trigger is a named tier-gap finding that turned out to be a defect.

**A gate that fires 83,100 times on its first real run is a gate that gets commented out.** Naming the
policy now is what stops that.

### 3.3 Budget stays out, and says so

E24 skipped budget checking because no ceiling data exists in the schema. **That is still true** — the
rung `powerBudgetMilli` A-G1 introduces is the first such data, and `ContentValidation.Budget` is
rarity-keyed with zero production callers. **E47 does not wire budget**, and records why, so the next
reader does not mistake the omission for an oversight.

---

## 4. What this module must NOT do

- **Wire the gate without a finding policy.** §3.2 — the first noisy run decides whether the gate
  survives. **Decided 2026-09-03**; the policy is the shipped `Blocking` flags, restated.
- **Change a `Blocking` flag in `ContentValidation` to make the step behave.** The policy reads Core;
  it never edits it to fit a YAML file.
- **Run against a fixture.** §3.1.
- **Wire budget checking.** §3.3 — it has no data and no caller.
- **Widen `Drift`'s ±25%** to make the step pass.
- **Duplicate the `ShippedSeed()` tests.** They cover the rules; this covers the **gate**.

---

## 5. Testing strategy

| # | Test | Proves |
|---|---|---|
| 1 | The CI step runs `--validate` against the real seed tree and exits 0 today | The gate is wired and currently green |
| 2 | **Planted violation:** an atom with an unknown kind **fails the build** | The gate gates — and the test asserts it fails at `Program.cs:78-83`, *before* the gate, which is where §3.2 says structural defects die |
| 3 | **Planted violation:** an orphan atom **reports and does not fail** | §3.2's policy is real, not aspirational |
| 3b | The step run with **no `--db`** exits 2, and a test or the step's own comment names why | §3.1 — the flag is load-bearing, not decoration |
| 4 | A `drift` beyond ±25% fails | The agreed tolerance is enforced |
| 5 | The step's output names **what it evaluated**, so an empty pass cannot look green | E14b's own rule, applied to the gate |
| 6 | **Planted violation:** removing the CI step **fails a guard** | E24's general guard, finally covering E24 |

**Test 6 is the point.** E24 built a guard for *"the next unwired suite"* and was itself the next unwired
suite. Test 6 is that guard pointed at this step.

---

## 6. Acceptance criteria

1. `ci.yml` runs `AtomImporter --check --validate --db <scratch>` against the real `data/seed/` tree
   (§3.1), with a `throw` on failure, and writes nothing to the repo tree.
2. The finding policy in §3.2 is implemented — which today means **verified against the shipped
   `Blocking` flags and written down**, not new code. Structural defects fail upstream of the gate;
   un-noted `drift` fails; every lint reports.
3. The step is green on today's corpus.
4. A planted unknown-kind atom fails CI; a planted orphan does not.
5. Output names what was evaluated.
6. Removing the step fails a guard (test 6).
7. Budget checking stays out, with its reason recorded.
8. No `tier-gap` fail-switch ships. The `--strict-lint` follow-up is recorded with its trigger (§3.2).

---

## 7. Dependencies and cross-program hazards

| | |
|---|---|
| **Depends on** | Nothing |
| **E24** `validation-in-ci` | This closes its **B4** half. E24's record is corrected to say what it actually delivered |
| **E43** `family-expand` | Its ~490 rows are what make §3.2's policy load-bearing rather than theoretical |
| **A-G1** `tier-access-gate` | Introduces the first budget data. **When it lands, revisit §3.3** — budget checking becomes wireable |
| **CI runtime** | The three `ShippedSeed()` tests already add ~3 s; the gate adds a full parse and lint. Measure it rather than assume |
