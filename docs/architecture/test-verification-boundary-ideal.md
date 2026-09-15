# Test verification boundary — the ideal

**Status:** idea phase, 2026-09-15. Not a spec. No build authorized.

## Which loop this extends

Not applicable — this is developer-tooling/CI architecture, not a player-facing RPG system. Saying
so rather than force-fitting a loop name: `docs/guide/the-loops.md` describes gameplay loops, and
none of them cover "how fast does verification run for a code change." The binding principle that
*does* apply here is the repo's own: **"the test is made to protect the repo, not burden the repo"**
(the owner's own framing for this idea phase) — a test suite exists to catch regressions cheaply; if
running it costs more than the regression it would have caught, the *verification process* is the
defect, not the tests.

## What this is

The owner's concern: `FusionRpg.Core.Tests` has 13,513 test cases in one assembly, and a scoped
verification run for a two-file change (`DebugActions.cs`, `DebugEndpoints.cs`) fell back to running
**all** of it (plus all 442 of `Server.Tests`) because the registry-driven selector
(`scripts/verify-change.ps1`) had no narrower mapping for those paths. The owner asked: is this my
(the agent's) mistake, or an architecture problem, and how does the industry solve it at this scale.

## What already exists

### Built

- **`scripts/verify-change.ps1` + `scripts/verification-boundaries.v1.json` + `scripts/guard-verification-boundaries.ps1`**
  — a hand-authored, integrity-guarded path→test mapping. This *is* a lightweight, repo-specific
  version of the Bazel-style "target graph" pattern the industry uses (see Prior art) — not a naive
  fallback. `verify-change.ps1:74-99` resolves each changed path to its most-specific owner boundary
  and the seams layered on it; `guard-verification-boundaries.ps1:76-80` fails the registry itself if
  any `src/**` file has zero owner. The mechanism **works today**, proven by the `data-item-socket` /
  `data-item-socket-test` pair (`verification-boundaries.v1.json:14-15`): a change to
  `RpgStore.Sockets.cs` runs `dotnet test ... --filter VerificationId=data.item-socket`, not the whole
  `Data.Tests` project.
- **Category-trait exclusion** (`DiskSemantics`, `Heavy` — `docs/contributing/testing-standard.md`
  §6) — cross-cutting *concern* filtering (disk-bound? slow?) applied uniformly across every project.
  This matches the industry-standard xUnit `[Trait]` pattern for concerns that cut across every
  subsystem, as opposed to per-subsystem selection.
- **Four test profiles** (`default`/`full`/`gate`/`nightly`, same doc §6) — a clean local-loop vs.
  CI-vs-release separation, matching the common "fast inner loop, full outer loop" industry shape.
- **Two internal measurement audits already ran and ruled things out**
  (`docs/contributing/test-burden-audit.md`, `test-architecture-audit.md`, both 2026-09-13). They
  measured, on this exact repo: `Core.Tests` — 13,369 tests, **89s wall, 4.6ms mean per test** — and
  explicitly recommend *leaving its structure alone* (`test-burden-audit.md` §6, "Leave Core/Server
  serialization alone… already efficient"). The measured burden is **`Data.Tests`: 96.7% of all
  summed test time**, caused by a **process-global mutex in SQLite's in-memory VFS**
  (`sqlite3MutexAlloc(SQLITE_MUTEX_STATIC_VFS1)`, `src/memdb.c`) that serializes every in-memory
  database open in the process — proven by a 4-arm discriminator test
  (`test-architecture-audit.md` §3) and fixed *in principle* by process-level sharding (23.9s
  concurrent vs. 47.4s sequential, §5), not a thread cap and not smaller assemblies.

### Wiring gap

- The registry mechanism that already works is applied to **2 of dozens of legitimate seams** in the
  whole repo. `verification-boundaries.v1.json:14-16` — the only two *focused* (narrow,
  `VerificationId`-filtered) boundaries that exist. Lines `17-23` are seven *module*-level fallbacks
  (no `VerificationId`) that cover the entirety of `src/FusionRpg.Core/**`,
  `src/FusionRpg.Data/**`, `src/FusionRpg.Server/**`, `src/FusionRpg.Injector*/**`, etc. — each one
  runs its *whole* project's default filter. This is exactly a wiring gap in the skill's sense: the
  machinery is proven and cheap to extend (one JSON entry + one `[Trait("VerificationId", …)]`
  attribute, per the working example), it is just not populated.
- `guard-verification-boundaries.ps1:76-80` only walks `src/**` for unmapped-file enforcement.
  `tests/**` has **no equivalent enforcement**, so a test file can go unmapped indefinitely with
  nothing failing loudly. Found live this session: `tests/FusionRpg.Server.Tests/LawnQuickStartEndpointTests.cs`
  had zero registry entry until today, despite `LawnQuickStartEndpointTests` itself already existing
  and being exercised repeatedly by prior sessions.
- `verification-boundaries.v1.json` has a `"seam"` boundary *kind* already defined
  (`verify-change.ps1:89-91` applies seam matches on top of the owner match) but **zero seam entries
  exist in the registry today**. Seams are exactly what the Bazel tooling research below calls out as
  the easy thing to miss: shared fixtures, tuning JSON, generated trees — inputs outside the plain
  source-import graph.

### Real gap

- No automated "which tests actually exercise this changed code" dependency graph exists. Today's
  registry is 100% hand-authored glob matching with **no verification that a glob's claimed owner
  still covers the changed code** — a boundary can silently rot (someone adds a new subsystem folder
  under `src/FusionRpg.Core/NewThing/**`, the `core-fallback` glob still matches by coincidence, but
  nothing checks that `Core.Tests` genuinely exercises it beyond "it's in the same project").
- No per-subsystem split of `FusionRpg.Core.Tests` exists (one `.csproj`, 855 files, 26 subsystem
  folders already in place as natural boundaries — see the sibling response in this session for the
  concrete migration cost). This is a real gap **only if build-time isolation is the goal** — the
  measured evidence (above) says it is not the current bottleneck.
- Microsoft's native .NET **Test Impact Analysis** (VSTest/Azure Pipelines) is not wired here, and —
  per the research below — has a **documented incompatibility with xUnit/NUnit data-driven tests**.
  This repo has 403 `[Theory]` declarations, 1,583 `[InlineData]` rows, and 26 `[MemberData]` sources
  contributing a meaningful share of the 13,513 `Core.Tests` count. Adopting VSTest TIA directly would
  silently under-select coverage for that share. Stated explicitly so nobody proposes it later as an
  obvious automated fix.

## Prior art

- **Microsoft (internal, cited via testμ 2026 talk)**: a **100-package monorepo, 14,000 tests** —
  before optimization, every PR ran the whole suite and developers waited **3–4 hours**. The fix was
  test selection that "maps a code change to its dependencies and risk, with the team approving the
  selection rules" — a **hand-curated, team-reviewed mapping**, not a fully automatic graph. This is
  structurally the same shape as this repo's `verification-boundaries.v1.json` +
  `guard-verification-boundaries.ps1` integrity check (a human-authored, mechanically-validated
  registry) — the number (14,000 tests, ~100 packages) is close enough to this repo's own scale
  (13,513 tests, 26 subsystem folders across ~8 assemblies) to treat as directly comparable.
- **Google / Meta / Microsoft, cross-company study**: all three run **tens of thousands of projects**
  in single monorepos successfully — monorepos at this scale are a solved, common pattern; the
  enabling factor named across all of them is build-graph tooling (Bazel/Buck-style), not splitting
  the repo.
- **Bazel ecosystem** (`bazel-diff`, `target-determinator`, `rdeps`): the canonical implementation of
  "diff two git revisions → compute the set of affected test targets" via a real dependency graph.
  Their own stated caveat is the one most relevant here: *"[the selector] must also account for
  inputs that sit outside the ordinary import graph, such as schemas, environment templates, code
  generators, shared fixtures, container images, and CI configuration."* This is precisely the
  `"seam"` boundary kind this repo's own registry already defines but has never populated.
- **.NET-native Test Impact Analysis** (Azure DevOps / VSTest): automatic, coverage-instrumentation-based
  test selection exists as a first-party feature — but Microsoft's own docs state that **data-driven
  xUnit/NUnit tests cannot use it** (Rerun-failed-tests, multi-agent distribution, and Test Impact
  Analysis all exclude them). Given this repo's heavy `[Theory]`/`[InlineData]`/`[MemberData]` usage,
  this feature would degrade rather than solve the problem here.
- **xUnit's own documented design bias**: the framework's authors prefer *"different test types into
  different assemblies, rather than traits for filtering"* — but acknowledge the tradeoff: multiple
  assemblies raise project-maintenance overhead, and traits integrate more smoothly with
  `dotnet test`/MSBuild pipelines. This repo already chose traits for its two cross-cutting concerns
  (`DiskSemantics`, `Heavy`) — consistent with the "flexibility over purity" side of that documented
  tradeoff, for exactly the concerns traits suit (cutting across every subsystem) rather than
  subsystem boundaries (which suit projects/registry entries).

Sources: [Endor Labs — Bazel dependency management](https://www.endorlabs.com/learn/5-tips-for-managing-bazel-dependencies-without-losing-friends) ·
[Tinder/bazel-diff](https://github.com/Tinder/bazel-diff) ·
[bazel-contrib/target-determinator](https://github.com/bazel-contrib/target-determinator) ·
[QASkills.sh — affected package detection](https://qaskills.sh/blog/monorepo-testing-affected-package-detection) ·
[testμ 2026 — scaling test automation at Microsoft](https://www.testmuai.com/blog/scaling-test-automation-microsoft/) ·
[Graphite — why top tech companies move to monorepos](https://graphite.com/guides/why-top-tech-companies-are-moving-to-monorepos) ·
[Microsoft Learn — Test Impact Analysis, Azure Pipelines](https://learn.microsoft.com/en-us/azure/devops/pipelines/test/test-impact-analysis?view=azure-devops) ·
[xunit/xunit#610 — trait filtering discussion](https://github.com/xunit/xunit/issues/610) ·
[Span of Reference — filtering xUnit tests](https://edgamat.com/2024/03/16/Filtering-Xunit-Tests.html)

## The shape

Three **independent** levers, matching what the internal audits and the industry research both
converge on — independent because each targets a different, separately-measured cost:

1. **Deepen the existing registry (cheap, already proven, do this first).** Add focused
   (`VerificationId` + `[Trait]`) boundaries for every path that is genuinely single-concern —
   starting with every test class (test files are *always* single-concern by construction, unlike a
   1,400-line multi-route `DebugEndpoints.cs`). Extend `guard-verification-boundaries.ps1` to require
   an owner mapping for `tests/**` the same way it already requires one for `src/**`, closing the gap
   that let a real test file go unmapped. Populate `"seam"` entries for cross-cutting non-source
   inputs (tuning JSON, generated trees, shared fixtures) per the Bazel-diff caveat above.
2. **For the *actually measured* burden (`Data.Tests`), follow the audit's own conclusion: process
   sharding, not project splitting.** `test-architecture-audit.md` §5 already proved 2× wall-clock
   improvement from two concurrent `dotnet test` processes vs. one — because the bottleneck is a
   process-global SQLite mutex that a smaller assembly does not route around (splitting `Data.Tests`
   into N projects would just create N processes serializing on the same in-process mutex *within
   each* run unless they are literally separate `dotnet test` invocations).
3. **Splitting `Core.Tests` — corrected 2026-09-15 after a peer review caught an overclaim, then
   measured directly (see "Compile-time measurement" below).** The first draft of this doc said
   "13,369 tests in 89s at 4.6ms mean is not a burden, so don't split" — that conflated *test
   execution time* (which the burden audit measured and which splitting does not help) with *compile
   time* (which nobody had measured, and which splitting genuinely does help — see the numbers
   below). Corrected verdict: **compile time is real and scales with file count, but it is not the
   dominant cost for the specific incident that started this doc** (running whole-project *test
   execution* — 85s + 100s — dwarfs the ~2-3s compile step either way). Splitting `Core.Tests` is a
   legitimate, independently-justified lever for **incremental-build latency** and **architectural
   enforcement** (a project reference is a compiler-checked boundary; a folder is not) — it is just
   not the fix for the run-time selection problem lever 1 already fixes more cheaply. Both are worth
   doing; they solve different costs. See "What this deliberately does not decide" for the honest
   scope of what is still unmeasured (a full 26-project migration was not attempted, only the
   underlying cost model).

### Compile-time measurement (2026-09-15, this repo, this machine)

Measured directly rather than assumed, per `DESIGN-GATE.md`'s own rule ("test the constraint before
you declare it"). Method: `dotnet build <proj> -c Debug /clp:PerformanceSummary`, reading the
`CoreCompile`/`Csc` target duration (the actual C# compiler invocation) separately from total wall
time (which also includes MSBuild's restore/reference-resolution machinery). Three runs per no-op
case to check for noise; single runs for the real-recompile case since the signal was unambiguous.

| Project | Files | No-op rebuild (nothing changed) | Real recompile (1 file touched) — `Csc` time alone |
|---|---|---|---|
| `FusionRpg.Guard.Tests` | 56 | ~0.9–1.2s (×3) | **139ms** |
| `FusionRpg.Core.Tests` | 855 | ~1.4–2.2s (×3) | **1,969ms** |

Two separate, real findings, both load-bearing:

1. **`Csc` compile time scales almost exactly linearly with file count**: 855/56 ≈ 15.3× the files,
   1969/139 ≈ 14.2× the compile time. Splitting `Core.Tests` into per-subsystem projects would let a
   change to one subsystem recompile only that subsystem's slice — e.g. a ~30-file project would
   compile in roughly 30/855 × 1969ms ≈ 70ms instead of ~2s, matching the Guard.Tests-scale number
   directly. **This is the real, previously-unmeasured, genuine win the peer review identified** — my
   first draft's dismissal of "compile time: Yes" was wrong because I never measured it.
2. **But the no-op/restore overhead (~1–2s) is roughly constant regardless of project size** — Guard
   (56 files) and Core (855 files) differ by only ~30-70% on this axis despite a 15× file-count gap.
   This is `ResolveProjectReferences`/restore-graph-walk machinery, proportional to the *project
   reference graph shape*, not to source file count. It means splitting into **many** projects adds
   this fixed cost **once per project touched in a build**, which can offset or reverse the Csc
   savings for an operation that spans several of the new smaller projects at once (a full/clean
   solution build, or a change that legitimately crosses two subsystems).

**Net, honest verdict**: for the single most common case — one or a few files changed in one
subsystem, verify just that subsystem — splitting turns roughly "1.5s fixed + 2s Csc ≈ 3.2s" into
roughly "1–1.2s fixed + 0.07–0.2s Csc ≈ 1.1–1.4s." A real, measured, ~55-65% reduction for that step —
genuine, but "moderate and measured," not the peer review's "potentially massively" framing, and it
is a *compile-time* win layered on top of lever 1's run-time win, not a replacement for it. The
architectural-enforcement benefit (a project reference is checked by the compiler; a folder convention
is not) is real but was not itself measured here — it is a design-quality argument, not a speed one.

### Alternative considered and rejected: splitting the repo into multiple smaller repos

The owner's own escalation option, raised explicitly. Rejected for now:

- This is four tightly-coupled modules (Launcher/Injector/Server/Web) plus Core/Data/Contracts/CheatCore
  underneath, per `AGENTS.md`'s own module table — a single session routinely touches Injector +
  Server + Python tooling + their tests together (this session did exactly that). A multi-repo split
  would turn every one of those ordinary cross-cutting changes into a multi-repo coordination problem,
  trading today's real-but-fixable verification-scoping cost for a permanent cross-repo
  versioning/dependency cost on the *common* case.
- Neither measured bottleneck (the registry's coverage gap; `Data.Tests`' SQLite VFS mutex) is a repo-
  boundary problem. Splitting repos fixes neither; it just relocates where the same gaps live.
- The Google/Meta/Microsoft prior art above is a direct counter-example at far larger scale: all three
  keep tens of thousands of projects in ONE repo and solve this with build-graph tooling, not
  repo-splitting.

### If lever 3 is commissioned: the recommended shape, not a blind folder→project script

A peer review of this doc's first draft proposed a concrete migration shape, reviewed here and
endorsed with one correction. **Do not** run `folder → project` as the entire algorithm — that
decides architecture by coincidence of today's folder layout. Instead, two separate, small,
deterministic tools, each doing one job:

1. **A read-only analyzer first.** Walk the existing folders (`Actions`, `Combat`, `Effects`, `Stats`,
   `World`, … — the 26 that already exist under `tests/FusionRpg.Core.Tests/`), and for each candidate
   boundary report file count, test count, and — critically — **cross-boundary symbol references**
   (does anything in `Combat/` reach into `World/`'s internals in a way a project reference would
   refuse?). Produces a report, mutates nothing. This is the same "observe before fix" discipline this
   repo already applies elsewhere (`docs/DESIGN-GATE.md`, the goal cycle's own READ→BUILD→OBSERVE
   order).
2. **A deterministic apply step, gated on a human-approved manifest** — a policy file naming exactly
   which folders become which project and what each may reference, e.g.:
   ```json
   { "project": "Combat.Tests", "include": ["Combat/**"], "references": ["Combat", "Actor", "TestSupport"] }
   ```
   The tool then does the boring, mechanical part only: create the `.csproj`, move the files, add
   `ProjectReference`s, remove the old `Compile` items, rebuild, run the moved tests, and refuse if a
   moved file references something outside its declared boundary. **The tool never decides
   groupings** — that stays a human/design call, exactly the same separation of concerns
   `data/tuning/*.json` + its deterministic readers already use everywhere else in this repo
   (`tunables-ssot.md`), and exactly what `verification-boundaries.v1.json` +
   `guard-verification-boundaries.ps1` already are for test *selection*. This is not a new pattern for
   the repo to learn — it is the existing one, applied to project structure instead of tuning numbers.

**Build on Roslyn/MSBuild's own APIs (`Microsoft.CodeAnalysis`, `MSBuildWorkspace`), not a
hand-rolled C# parser.** Roslyn is Microsoft's own compiler platform, mature and stable, and already
exposes the syntax tree, symbol/reference model, and project-reference manipulation this needs.
Existing tools reviewed and found to solve *adjacent*, not identical, problems: `Roslynator` (500+
analyzers/refactorings, general-purpose); ReSharper/Rider's "Move Types into Matching Files" (real,
mature, but a no-op here — see "What already exists → Real gap" above, this repo's giant files are
one type each, not many types in one file); `dotnet-depends`/`DotNetCobble` (dependency
*inspection*/solution *assembly*, not project *splitting*). None of these do the apply step above
end-to-end; a thin orchestration layer over Roslyn/MSBuild is the only path, matching what "build
ourselves" already meant in this doc's first draft — the correction is *what* to build (an
orchestration layer honoring a human-approved manifest) and *why* (architectural enforcement +
measured compile-time win), not "because 13,513 is scary."

## Tunables

None. Nothing here is a runtime/balance number — it is verification-process architecture (registry
entries, CI invocation shape, process count for a sharded `Data.Tests` run). Any concrete shard count
or CI parallelism setting that comes out of implementing lever 2 would be a structural/CI config value
(named and justified in its own commit), not a `data/tuning/*.json` entry.

## What this deliberately does not decide

- The exact `Data.Tests` shard count or CI-invocation shape for process sharding — `test-architecture-audit.md`
  §6 already states the shard count was not measured (2 halves proved the principle; 4 or 8 were not
  tried). That is execution work for a `/plan`, not an idea-phase decision.
- Which specific seams/test files get focused boundaries first, or the full replacement text for
  `verification-boundaries.v1.json` — an ordered task list, not an idea.
- Whether `guard-verification-boundaries.ps1`'s `tests/**` enforcement (recommended above) should be a
  hard CI failure immediately or phased in with a ratchet/baseline (matching `test-substrate-baseline.txt`'s
  own precedent for a similar rollout). That is a rollout-mechanics decision for the spec/plan phase.
- The actual boundary manifest for a `Core.Tests` split (which folders become which projects, what each
  may reference) — the compile-time measurement above justifies *doing* lever 3 eventually; it does not
  by itself define the 26-project (or however many) boundary set. That is exactly what the read-only
  analyzer step (above) is for, and it has not been run.
- Whether the cross-boundary-reference check the analyzer would need (do any of `Combat/`'s tests reach
  into `World/`'s internals?) turns up violations that make some of today's 26 folders **not** clean
  boundaries yet. Not measured — the folder split was observed to exist, not verified to be
  reference-clean.

## Open questions

None that are genuinely undecided at this phase — the owner's questions ("what caused this," "is
13,000+ tests itself the problem," "should we split the repo," and, after peer review, "does splitting
help compile time") are all answered above with measured evidence, not left open. Two real decisions
for the owner, sequencing only:

1. **Commission lever 1 (deepen the registry) now** — cheap, already proven, directly fixes what
   happened this session. Low risk either way if deferred, but there is no real reason to wait.
2. **Whether lever 3 (the `Core.Tests` split) is worth its one-time migration cost now, given the
   measured ~55-65% compile-time reduction per touched subsystem plus the architectural-enforcement
   value** — this is a real tradeoff (a multi-day-scale migration effort) against a real, now-measured,
   but moderate benefit. This doc takes no position on timing; it only insists the decision be made
   from the numbers above, not from "13,513 is scary" or "compile time obviously doesn't matter."
