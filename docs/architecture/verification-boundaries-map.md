# Verification boundaries — capability map

**Status:** proposed 2026-09-13. This is a contributor-tooling program, not a player feature. It
does not add, remove, or alter a game loop.

## 1. Objective

An agent changing a narrow path must be able to ask the repository for the **smallest correct local
verification set**, rather than guessing between one test and an unfiltered suite. The answer must
be explicit, reproducible, safe in a shared worktree, and preserve CI/nightly as the owner of full
regression evidence.

The target user is an automated contributor working one recorded session at a time. Their job is:

> Given the paths I changed, what focused tests, module/seam tests, and static guards prove this
> change locally — and what evidence remains owned by CI?

This program extends the session-boundary discipline: `paths` answers *what this session may edit*;
verification boundaries answer *what evidence those edited paths require*.

## 2. Verified current state

| Finding | Evidence | Consequence |
|---|---|---|
| The routine profile excludes `DiskSemantics` and `Heavy`, but no-argument `test-fast.ps1` selects four projects. | `scripts/test-fast.ps1:1-11,37-43` | The local default is still broader than a narrow source edit needs. |
| `deploy-play.ps1` invokes the no-argument routine profile. | `scripts/deploy-play.ps1:203-207` | A live-deploy workflow accidentally inherits that broad test scope. |
| Current xUnit traits are profile/special-purpose labels, not positive ownership labels. | `rg "[Trait(" tests` finds `DiskSemantics`, `Heavy`, and `BalanceGuard`; no `VerificationId`. | A source path cannot select its focused test family by stable contract. |
| Existing architecture specs contain focused filters, but only as distributed prose. | e.g. `docs/architecture/species-gear-chain/spec-socket-combat-wiring.md:249-250` and `docs/architecture/passive-tree/spec-gate-counters.md:466`. | These are useful migration evidence, not an executable authority. |
| Test-project references cross behavioral ownership boundaries for fixtures/tool apphosts. | `tests/FusionRpg.Core.Tests/FusionRpg.Core.Tests.csproj`; `tests/FusionRpg.Data.Tests/FusionRpg.Data.Tests.csproj`. | A project-reference graph cannot safely derive the required behavior tests. |
| A shared worktree can contain another session's changes. | `docs/contributing/session-boundary.md` §1, §4; active-session inventory checked 2026-09-13. | A planner must accept explicit paths; it must not treat ambient `git diff` as this session's changes. |
| Full CI is deliberately unfiltered. | `.github/workflows/ci.yml:195-216`. | Local narrowing must not weaken full, disk, cold-process, or E2E coverage. |
| `--no-build` after a source edit can test stale dependent assemblies. | `docs/contributing/test-burden-audit.md` §5.2. | The canonical runner must build the selected test project before it tests it. |

## 3. Decisions this program makes

1. **Path ownership is authoritative.** A versioned registry maps repository-relative production
   paths to verification requirements. It is not inferred from assembly references, file names, or
   the dirty worktree.
2. **A test group has a stable positive ID.** xUnit's existing `Trait` mechanism carries a
   `VerificationId`; `DiskSemantics` and `Heavy` remain execution-profile traits, not ownership
   traits.
3. **Ownership is exclusive; seams compose by union.** Each production path resolves to exactly one
   most-specific `owner` boundary. Separate, explicitly additive `seam` boundaries contribute extra
   proof. Requirements then union across the resolved owners and seams for all changed paths. A broad
   legacy fallback therefore cannot make a narrow edit run both its focused group and a whole module.
4. **Missing coverage fails closed and fast.** An unmapped production path reports a
   verification-boundary defect. It never falls back to a full local suite.
5. **Only the runner constructs commands.** Registry data selects from allowlisted project IDs,
   guard IDs, profile IDs, and verification IDs; it cannot execute arbitrary PowerShell.
6. **Full evidence stays independent.** CI/nightly/release continue to run unfiltered tests. The
   planner may state that fact, but cannot claim a local focused pass is full evidence.
7. **An explicit proof can override a broad profile exclusion.** `DiskSemantics` and `Heavy` stay
   excluded from broad routine/module runs. A boundary may mark one as `requiredOnChange` when it is
   the direct proof of the changed behavior (for example, a WAL or archive writer change). That runs
   one relevant test family, not the full suite.
8. **A session's scope remains explicit.** Agent invocations supply `-Session` with `-Paths`; it
   validates every supplied path against the active record, but never silently expands broad session
   globs or inspects another contributor's diff. Interactive maintainers may omit it only for
   read-only/planning use outside an agent session.
9. **Selection is a declared impact contract, not a heuristic.** A boundary owns the direct
   behavior it changes; an explicitly declared seam owns a cross-boundary contract. Import graphs,
   file names, test duration, and an agent's anxiety are not reasons to add unrelated projects.
   If the declared evidence looks incomplete, the agent changes the mapping/spec or reports a
   boundary defect; it does not compensate by launching every suite.
10. **A test edit has the same proof obligation as a production edit.** A changed test source must
    resolve to its behavior group (or a declared test-support boundary), so a broken/reduced test
    cannot evade verification simply because no production path changed in that commit.
11. **Failure does not auto-escalate scope.** A failing selected check is evidence to diagnose that
    check. The runner stops and reports it; it never responds by trying a module, full, or unrelated
    suite. A broader check is permitted only when a registry mapping explicitly selects it or an
    owner explicitly requests broad validation.

## 4. Evidence levels

| Level | Meaning | Local owner |
|---|---|---|
| `focused` | Direct behavioral proof for the changed source boundary. It may explicitly require one disk/heavy family. | Every behavior change. |
| `module` | Regression suite for a shared implementation boundary. | Only when the registry requires it. |
| `seam` | Named cross-module contract proof. | Only when a seam-owned path changes. |
| `full` | Unfiltered regression, disk/cold-process/E2E checks. | CI, nightly, release — never routine agents. |

The existing `default`/`full` profiles are a separate axis: profiles decide whether an otherwise
selected test is eligible locally; evidence levels decide **which** test groups are selected at all.

### Agent decision rule

For a normal code or test edit, the agent supplies the exact edited files to the planner (and its
active session when available), then runs precisely the returned plan. The outcome has only four
valid branches:

| Planner outcome | Agent action |
|---|---|
| Focused boundary | Run its selected guards and focused group. |
| Migration boundary | Run its explicitly selected module fallback; add a focused boundary when the behavior is understood. |
| Focused boundary plus seam | Run the union, once per deduplicated check. |
| Unmapped/out-of-session/invalid registry | Stop before tests; repair or request the mapping/scope. Never substitute a full suite. |

CI/nightly/release subsequently supply full evidence. A local focused pass is enough to proceed with
the narrow implementation task; it is never described as proof that the whole repository is green.

## 5. Capability map

| Module id | Responsibility | Depends on |
|---|---|---|
| `boundary-inventory` | Inventory source roots, test projects, known guards, existing focused-filter evidence, and cross-project references. Its output is advisory; it never invents a requirement. | — |
| `test-group-contract` | Define stable `VerificationId` conventions and prove the runner can select a group. Seed the first mapped test groups. | — |
| `boundary-registry` | Define the versioned declarative registry and add initial curated mappings from inventory evidence. | `boundary-inventory`, `test-group-contract` |
| `verification-planner` | Normalize explicit paths, validate session scope when supplied, resolve matching registry entries, union requirements, and emit a deterministic plan. It does not execute tests. | `boundary-registry` |
| `verification-runner` | Execute planner-selected, allowlisted guards and test commands; build selected test projects; emit compact evidence. | `verification-planner` |
| `verification-integrity-guard` | Reject unmapped in-scope production roots, stale selectors, unknown projects/guards, invalid patterns, and conflicting registry entries. | `boundary-registry`, `verification-planner` |
| `workflow-adoption` | Make the canonical runner the agent workflow; replace accidental broad local defaults with explicit opt-in; make deploy consume a supplied verification plan; preserve CI's full profile. | `verification-runner`, `verification-integrity-guard` |

Build order:

```text
boundary-inventory ─┐
                    ├─ boundary-registry → verification-planner
test-group-contract ┘                             ↓
                                         verification-runner
                                                  ↓
                                  verification-integrity-guard
                                                  ↓
                                        workflow-adoption
```

## 6. Program contract

### Registry shape

The implementation owns the exact JSON schema, but every entry must be able to express:

```text
boundary id
  owner or additive-seam path patterns
  optional test-source path patterns
  focused/module/seam verification group IDs
  allowlisted test project IDs
  applicable static guard IDs
  execution profile
  full-evidence owner
  rationale / source spec reference
```

Patterns are normalized repository-relative paths. The registry contains no shell snippets, no
absolute machine paths, and no generated population counts.

Each registry release also declares its **enforced roots**. The integrity guard fails closed for an
unmapped path under an enforced root; roots not yet adopted are reported as unsupported rather than
being silently assigned a nearby C# project. This makes rollout honest: a v1 registry may begin with
the production roots it can prove, then add web, tools, generated data, and build/config paths with
their own fixed validators. It must not claim those roots are covered before their commands and
positive groups exist.

### Planner output

For explicit changed paths the planner must print:

```text
matched boundaries
required focused/module/seam groups and why each is required
applicable guards
excluded profile categories and each explicit `requiredOnChange` override
CI-owned full evidence
unmapped paths, if any
```

Output order is deterministic: normalized path, boundary ID, then check ID. The same path list must
produce byte-identical plan data regardless of argument order.

### Runner output

The runner owns fixed construction of `dotnet test` and guard invocations. It must:

- refuse an unmapped production path before running a broad test fallback;
- invoke source-only guards before selected test projects;
- build a selected project/configuration before testing after source edits;
- apply the default profile filter to broad module runs, but honor an explicitly selected
  `requiredOnChange` disk/heavy proof;
- report each attempted check, exit code, elapsed time, and evidence level;
- stop at the first failed guard or test project;
- never invoke unfiltered tests unless the caller explicitly requests the owner-only full mode.

## 7. Initial adoption boundary

The program target covers every current top-level production root, with curated focused subdomains
landing first:

- `src/FusionRpg.Core/**`
- `src/FusionRpg.Data/**`
- `src/FusionRpg.Server/**`
- `src/FusionRpg.Contracts/**`
- `src/FusionRpg.CheatCore/**`
- `src/FusionRpg.Injector/**` and its three loader-host projects
- `src/FusionRpg.Launcher/**`
- `web/fusion-rpg-web/**`
- `tools/**` and generated-data roots through their existing generator/check commands

Adoption is phased. Each registry release lists only the roots that its integrity guard enforces; it
must include an explicit, shrinking migration register for historical subdomains in those roots that
do not yet have a curated focused group. A migration entry has an owner and a safe temporary
module-level fallback; it cannot silently claim focused coverage it does not have. Web, tools,
generated data, shared build files, and workflow files are separate adoption slices, because their
correct validators are not necessarily `dotnet test`.

New production roots are not permitted to enter without a registry entry and at least one positive
test-group proof. This is a contract/closed-vocabulary guard, not a population-count assertion;
see `architecture/validation-ssot.md`.

## 8. Non-goals

- Automatically infer behavioral test ownership from C# project references or file names.
- Delete, weaken, or locally reclassify legitimate disk, cold-process, E2E, or full-suite tests.
- Make `git diff` the source of another session's changed paths.
- Store arbitrary commands in JSON/YAML.
- Map every legacy test before the first usable workflow ships.
- Change game runtime behavior, game data, balance values, or production module ownership.

## 9. Program acceptance

1. A changed Data item path resolves to only its mapped focused group(s), required Data/static
   guards, and any explicitly required module/seam checks — not Core, Server, or E2E by default.
2. A narrow owner mapping overrides its broad migration fallback; two changed paths in distinct
   boundaries produce the deterministic union of their resolved requirements.
3. A path outside the supplied session scope is rejected before test execution.
4. An unmapped production path fails in seconds with a mapping action and no full-suite fallback.
5. A renamed/deleted test group or unknown guard/project makes the integrity guard fail.
6. CI remains unfiltered, including `DiskSemantics` and `Heavy` coverage.
7. The canonical agent workflow cannot select the historical four-project local default without an
   explicit broad-validation option.
8. A selected test run rebuilds after source changes, avoiding the stale-assembly trap.
9. A directly selected disk/heavy proof runs when its boundary marks it `requiredOnChange`, while
   unselected disk/heavy tests remain CI-owned.
10. A changed test file, missing mapping, invalid session scope, or selected-check failure cannot
    cause an implicit broad-suite retry.

## 10. Design-gate record

| Checklist item | Evidence |
|---|---|
| Subsystem identified | Contributor verification and session boundaries. |
| Required documents read | `architecture/software-architecture.md`, `architecture/decisions.md`, `contributing/session-boundary.md`, `contributing/testing-standard.md`, `architecture/validation-ssot.md`, burden/architecture audits. |
| Code verified | `test-fast.ps1`, `deploy-play.ps1`, CI workflow, test-project files, trait inventory, and session records inspected 2026-09-13. |
| Session boundary | `tasks/sessions/verification-boundaries-20260913-6f31.json`. |
| Boundary checker | Run before this map. It reports one pre-existing unclaimed legacy worktree (`worktree-agent-a4497053e6313d493`); it does not overlap this session's documentation paths. |
| Test claims | No new timing or compatibility claim is made without an implementation spike. Existing performance facts are cited to their measured audits. |
| SOLID | Planner resolves data; runner executes it; registry is declarative; integrity validation is separate. No parallel source of test ownership is created. |
| Validation | Future guard validates registry contract and closed allowlists, never test/project population totals. |

## 11. Next artifacts

Module specs live in `architecture/verification-boundaries/` and trace to the module IDs in §5.
Plans and tasks, after module-spec approval, use `tasks/verification-boundaries-plan.md` and
`tasks/verification-boundaries-todo.md`.
