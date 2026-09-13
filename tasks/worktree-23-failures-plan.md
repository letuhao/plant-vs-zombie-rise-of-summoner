# Plan: 23 worktree test failures (triage → fix)

Status: **triage only, no edits made.** Written before goal setup so the owner can
assign each phase to its owning program/session. One session = one problem; do NOT
execute this as a single bundle.

## 1. Goal

Green default profile (`Category!=DiskSemantics&Category!=Heavy`, = deploy-play's
`test-fast.ps1` gate) on the `solid-run` worktree branch
(`worktree-solid-run-20260912-eb53`), without weakening any guard.

## 2. Non-goals

- No product-behavior changes. Re-bless steps (P2/P3) only move a golden AFTER the
  diff is proven to be the intended engine change, following each test's own
  documented re-bless protocol.
- Live-probe scope is untouched. Live-probe's own commits
  (`36645a49..worktree-solid-run-20260912-eb53`) contain no Battle/Expedition/Item
  changes (verified by log); the fix work below belongs to other programs.
- `features/derived-stat-extension` is already green (Core/Server/Data/E2E/Guard
  verified this session) — nothing to do there.

## 3. Evidence (verified by running, 2026-09-13)

Worktree default-profile tally: **Core 16 + E2E 5 + Server 0 + Data 2 = 23.**

| Class | Failures | Root cause |
|---|---|---|
| A. CRLF checkout artifact (15) | Battle goldens ×14: `PreAdoptionTraceTests` ×3, `EventSequenceParityTests` ×3, `BasicAttackAdoptionTests` ×8; `MaterialCorpusTests.D24` ×1 | Blobs are `i/lf`; worktree checkout is `w/crlf` (`git ls-files --eol`). Serializers emit `\n` (`tests/.../Battle/Adoption/EventSequenceParityTests.cs:39`); `LoadOrCapture` compares raw bytes (`:55`,`:75`). D24 builds `broken` via a `"\n"`-literal replace (`tests/.../Items/MaterialCorpusTests.cs:75-76`), a no-op on CRLF bytes |
| B. Expedition hunt-hash drift (1) | `ExpeditionResolverTests.Tier_goldens_are_locked` (`hunt 68F5…`→`09F6…`, forage identical) | Real drift — hashes, not text. Cause on worktree not yet isolated (solid-run engine change vs catalog churn) |
| C. Turn-fixture drift (1) | `WorldTurnFixtureTests` (`stateHash 4cd7…`→`567e…`) | Real drift, engine output moved |
| D. Sweep contamination (4) | `WebMatchContentHashSweepTests` — 4/4 pass in class isolation, fail in full-suite runs only | Shared `[Collection("e2e")]` store (`tests/.../WebMatchContentHashSweepTests.cs:19-28`) + windowed sweep; contaminator not yet bisected. Adjacent to battle-timeline B40 |
| E. Armoury worktree blindness (2) | `ArmouryTests` (`expected exactly one RpgStore.Items.cs, found 0`) | `FindRepoRoot()` resolves to the worktree, then `IsScannedSourcePath` (`tests/.../Data.Tests/Items/ArmouryTests.cs:142-161`) rejects every path with a `.kilo` segment — unpassable from inside any worktree |

## 4. Phases

- **P0 — unblock the signal (class A).** Normalize `\r\n`→`\n` on read in
  `LoadOrCapture` / `PreAdoptionFixtures` / BasicAttack fixture loader; normalize
  `TuningJson()` before the D24 string surgery. Test-only. Accept: 15 green, zero
  golden-file edits. Likely owner: test-substrate program.
- **P1 — repo hygiene (class A, systemic).** `.gitattributes` `* text=auto eol=lf`
  (minimum: fixtures/tuning/seed), `git add --renormalize`, document the required
  setting in `docs/contributing/dev-setup.md`. Host-dependent test outcomes are the
  defect; `core.autocrlf=true` is context, not cause.
- **P2 — hunt hash (class B).** Verify-then-rebless per the test's own protocol
  (`ExpeditionResolverTests.cs:180-226`): prove RNG stream/pick unchanged, re-bless
  `HuntHash` + history comment. Also file the test's self-requested fixture-band
  decoupling as debt (population-coupled goldens are readings, not constants).
  Owner: expeditions program.
- **P3 — turn fixture (class C).** Same verify-then-rebless for `stateHash`
  (confirm only intended ActorHub magnitudes moved). Owner: world-turn program.
- **P4 — sweep contamination (class D).** Bisect E2E halves to find the
  contaminator; fix isolation (fresh store per class or drain unresolved rows in
  teardown). Hand to battle-timeline (B40 follow-up).
- **P5 — armoury blindness (class E).** Apply `IsScannedSourcePath` to the path
  *relative to* `FindRepoRoot()`: main runs still exclude nested `.kilo`
  checkouts, worktree runs include their own tree. Test-only. Owner: items program.
- **P6 — close the gate.** Re-run `test-fast.ps1` on the worktree; if manual
  injector+game steps ever proceed on red again, record branch, counts, this
  classification, and accepted risk.

## 6. Execution record (2026-09-13, session `goal-23failures-20260913`)

Worktree default profile: **23 → 0** (Core 13387, Server 413, E2E 221, Data 1225).
Substrate guard green. All changes uncommitted in the solid-run worktree (git hands-off).

- **P0 done:** `\r\n`→`\n` normalize on read in `PreAdoptionFixtures`,
  `EventSequenceParityTests.LoadOrCapture`, `ActionAdoptionFixtures`; D24 test
  normalizes `TuningJson()` before string surgery. 15 green, zero golden edits.
- **P1 done:** `.gitattributes` `* text=auto eol=lf` + `dev-setup.md` line-endings
  section. Existing checkouts still need one owner-run `git add --renormalize .`
  (git writes are hands-off for agents).
- **P2 done:** hunt AND warpath re-blessed after branch-vs-branch dump+diff proved
  the sole delta is T5 injury representation (`ChannelMods` → `HubInputs.Injuries`);
  ticks/picks/rewards identical, siblings green. History comment appended per file
  protocol. (Warpath move was hidden behind xunit truncation in triage — caught on
  re-run, same cause class.)
- **P3 reclassified, no re-bless:** isolated turn-fixture run plays byte-identical
  output (bless run wrote zero diff). The full-suite failure was class-D
  contamination, fixed via P4. Fixture NOT modified.
- **P4 done, root cause (source defect, not test isolation):** after Summon /
  WebGameGuard tests leave real summoned specimens in player 1's roster,
  `BuildSquad` emits non-null `HubInputs.Aptitude`, and the sweep's re-resolve
  crashed with `NotSupportedException` (`AptitudeAllocation` had no JSON
  constructible shape — serialized as `{}`). Fixed by adding a `[JsonConstructor]`
  entry-list ctor + canonical `Entries` property (validation parity with
  `Single`; legacy `{}` reads as `Empty`). New `AptitudeAllocationJsonTests`
  (3 tests, failed before, pass after). Siblings (`BoundDerivedAtom`,
  `StarLoyaltyContribution`, `DraughtMod`) are positional records — unaffected.
  Bisect trail: halves → B2 → {Summon,WebGameGuard} → both independently reproduce.
- **P5 done:** `FindSourceFile` judges paths relative to `FindRepoRoot()`; nested
  worktrees still excluded from main, own tree included from worktrees.
  `ArmouryTests` 14/14 on worktree.

## 7. Open items / warnings

- Main-tree `web/.../first-light-turn.json` shows workdir dirt (84+/40-) that is
  NOT this session's (touched 19:16 by another stream or earlier work) — left
  untouched, excluded from handoff. Confirm owner before anyone commits near it.
- `AptitudeAllocation` now serializes with an `Entries` array (was `{}`). Any
  golden hashing a setup with non-null aptitude would move — full Core/Data/E2E
  runs show none did. Rows persisted as `{}` still read as `Empty` (covered).
- Re-bless steps (P2) were executed by this session under `/goal` authorization;
  owning programs should still read their §1-row docs per the design gate.

## 8. Original triage gaps — closed

- [x] Session boundary recorded (`tasks/sessions/goal-23failures-20260913.json`).
- [ ] Subsystem rows still unread in-session (`battle-timeline-map`,
  `battle-turn-ideal`, `validation-ssot`, `tunables-ssot`) — P2/P4 owners to read
  on review. Stated, not hidden.
- [x] Class D contaminator identified (bisect → Summon + WebGameGuard → shared-store
  `AptitudeAllocation` deserialization defect, fixed). Class B drift isolated
  (T5 injury representation, re-blessed). Class C reclassified (no drift).
