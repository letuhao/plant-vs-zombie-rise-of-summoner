# Plan: fe-essentials program

Source: [fe-essentials-map.md](../docs/architecture/fe-essentials-map.md) (3 modules, approved) and its
three specs under [fe-essentials/](../docs/architecture/fe-essentials/), all grounded against current
code 2026-08-29 — not just the plate mockups (see each spec's own Assumptions section for what
grounding changed before this plan was drafted).

Task list: [fe-essentials-todo.md](fe-essentials-todo.md). Paths are prefixed per this repo's
parallel-programs convention — `tasks/plan.md`/`tasks/todo.md` belong to the perf stream.

---

## 1. Shape of the work

**3 modules, 3 phases, 7 tasks.** Every task is one complete build-and-verify path, not a horizontal layer.

```text
Phase 1  onboarding-first-run       T1        (independent)
Phase 2  actor-menu-scope-picker    T2-T6     (independent of Phase 1)
Phase 3  hide-legacy-entry          T7        (verification only — needs both above shipped)
```

### 1.1 Orderings that matter, and why

1. **Phase 1 and Phase 2 do not depend on each other.** They touch disjoint files (`stages/sanctum/`
   vs. a new `ui/scope/`) and were built in this order only because `onboarding-first-run` is the
   smaller of the two — reorder freely if that ever helps.
2. **Inside `actor-menu-scope-picker`, the container (T2) goes first — everything else wires into it.**
   T3 (Target/UniqueDemon) and T4 (Type) are independent of each other once T2 exists; T5 (cross-mode
   contract) needs all three modes present; T6 (demo page) needs the whole component assembled.
3. **`hide-legacy-entry` (T7) is last and small on purpose.** Grounding already found it has nothing to
   build (see the map's own "Corrected during grounding" section) — it verifies that both replacements
   landed cleanly rather than doing removal work of its own.

### 1.2 What's explicitly not in this plan

The broader gap-audit backlog (10 missing entities, 29 new Class-A components, migrating Relics/Pacts/
Sector/Metrics off legacy code) — deferred to its own later program per the owner's "ship essentials
first" scoping. Any backend wiring for the commander/aura-skill feature — `actor-menu-scope-picker`
ships FE-only, ahead of a consumer that doesn't exist yet (same precedent as `ActorLadderDemoPage.tsx`
shipping ahead of Creatures/Sanctum). A real display-name-write endpoint — `onboarding-first-run` ships
without the plate's name input specifically because that endpoint doesn't exist anywhere in the FE yet.
Touching `DemonsPage.tsx` / `/demons` — a real, larger legacy candidate, deliberately left named-but-
untouched (spec's own Assumption 2).

---

## 2. Architecture decisions

- **`actor-menu-scope-picker`'s Target and UniqueDemon modes share one internal component**
  (`ActorListPickerPanel`), parameterized by candidate list and the `kind` tag on the emitted value —
  not two independent list implementations. This is what the spec's own testing strategy already
  requires ("a test asserting `target` and `uniqueDemon` modes both render via the real `ActorRow`
  component, not a lookalike") and matches this repo's "one entity, one ladder, no forks" rule
  (`docs/design/README.md`).
- **`FocusCard`'s existing `data-testid`s are preserved exactly** (`focus-card-first-run`,
  `focus-card-cta`) when its zero-creature branch delegates to the new `FirstRunReveal` — this is what
  keeps `SanctumStage.test.tsx`'s existing assertions passing unmodified, proving the swap is additive,
  not a rewrite.
- **No task builds a new list-selection primitive where `ActorRow` already exists** — Target/
  UniqueDemon compose it; only Type mode gets new UI (`TypeMultiSelect`, over the existing `Checkbox`
  primitive), because no existing component covers a species/type multi-select (confirmed by this
  session's own FE audit: zero `TypeChip`/`TypeToken` anywhere in the tree).
- **Demo page ships alongside a real route**, matching `ActorLadderDemoPage.tsx`'s exact precedent
  (own doc comment: a temporary proof surface, expected to be swept into or replaced by whatever
  screen later consumes the component for real) — not a Storybook-style island outside the router.

## 3. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| `ActorListPickerPanel`'s shared-component design (T3) turns out to need mode-specific behavior once real UniqueDemon candidate data is wired (e.g. durable specimens might carry fields live board ptrs don't) | Low–Medium | The component only needs `ActorRungState` + an id-extraction callback either way (`actorRungState.ts`'s shape is already uniform); if a real difference surfaces, it's a prop, not a fork |
| The demo page's fixture data (T6) doesn't exercise every `ActorRungState` variant (loading/empty/error), leaving those paths only unit-tested, not visually proven | Low | T6 can reuse the `?mock=1` pattern `ActorLadderDemoPage.tsx` already established rather than inventing a new fixture convention |
| `hide-legacy-entry`'s grep-based verification (T7) could miss a reference that isn't the literal string (e.g. a rewritten copy elsewhere) | Low | Scope is explicit and narrow (this exact copy string, this exact component tree) — a broader legacy sweep is out of this program by design, not a gap in this task |

## 4. Open questions

- **`DemonsPage.tsx` / `/demons`** — real, larger legacy candidate (full summon/roster/codex page,
  still directly routed at `/demons` unlike almost every other legacy route, own working nickname
  mechanism via `useSetDemonNickname`). Recommendation stands (leave untouched, defer to the already-
  named later legacy-migration program) unless the owner says otherwise.
- **Naming**, generally — once a real display-name-write endpoint exists anywhere in the FE, both
  `onboarding-first-run`'s dropped input and `DemonsPage`'s existing nickname mechanism become relevant
  to reconcile. Not this program's problem to solve now.
