# Spec: `derived-modifier-bucket`

**Program:** aura-skill · **Map:** [../aura-skill-map.md](../aura-skill-map.md) ·
**Ideal:** [../aura-skill-ideal.md](../aura-skill-ideal.md)
**Status:** specced 2026-08-30, not built. **Foundation module — everything downstream depends on it.**

---

## 1. Objective

Give **derived** channels the per-source accumulation, provenance, and withdrawal that **primary**
channels already have. Today the two pipelines are asymmetric, and the derived side is the one auras
live in.

| | Primary — `ModifierBag` | Derived — `ActorDerivedSnapshot` |
|---|---|---|
| Structure | `Dictionary<key, StatModifier>`, key = `(sourceKind, sourceId, channel, op, applyOwnerKey)` (`StatModifier.cs:19-20`) | `Dictionary<string, double>` (`ActorDerivedSnapshot.cs:6`) — **one number per channel** |
| Provenance at compose | full; survives to `EntityFinal.Contributions` (`StatComposer.cs:109`) | **destroyed** — `DerivedComposer.Compose` folds the list into one double |
| Withdraw one source | `Withdraw(sourceKind, sourceId)` | **not expressible** |
| Accumulates across resolves | yes, `_sessionBag` | no — *"fresh per call in v1"* (`ActorHub.cs:51`) |

`DerivedModifier` **already carries** `Priority` and `SourceId` (`DerivedModifier.cs:3-8`). The
information exists; it is thrown away at the fold. This module stops throwing it away.

**Who it is for:** every future producer of derived channels. `spec-derived-stat-sheet.md:193-199`
already names four **unattributed** producers today — patron, stars, injuries, contracts — and says
they must say so. Auras would be a fifth. This module is what lets any of them be attributed.

### Why this is a prerequisite, not a nice-to-have

> ⚠️ **Defect numbering — corrected 2026-08-30.** An earlier revision numbered defects **locally**,
> colliding with the canonical numbering in
> [derived-pipeline-audit-2026-08-30.md](../derived-pipeline-audit-2026-08-30.md). The audit's numbers
> are authoritative everywhere; this spec now uses them. (The collision was: this file's old "D1" was
> the audit's **D6**, and its old "D2" was the audit's **D1**. A task list generated from the two
> together would have been mis-scoped.)

Three defects block a working aura, and all three live here — **audit numbering**:

- **D1 — `ActorDerivedSnapshot.Overlay` is replace, not add** (`ActorDerivedSnapshot.cs:47-53`,
  `next._channels[k] = v`). `PatronAuraOverlay.cs:37` compensates by hand with
  `derived.Get(channel) + milli/10.0`. **A second overlay written naively erases the first, silently.**
  ⚠️ **Six `.Overlay(` call sites exist in `src/`**, not one — five in `Core/Status/ActorDerivedProfiles.cs`
  (`:57, 81, 113, 121, 128`) plus the injector's. `ActorDerivedProfiles.Resolve` is production-reachable
  via `Injector/Stats/InjectorDerivedOverride.cs:26`, and
  `tests/FusionRpg.Core.Tests/Status/ActorDerivedProfilesTests.cs:87` **pins replace semantics**. That
  test is why this module *adds* `OverlayAdd` rather than changing `Overlay` — cite it, do not
  "fix" the test.
- **D2 — no idempotence rule for derived-channel re-assertion.** Shields have one
  (`decisions.md:41`); derived channels do not. **Fixing D1 removes the accident that currently
  provides it**, so the rule must land in the same change — see §4.2a.
- **D6 — a percentage on `combat.*` silently composes to nothing.** Every `combat.*` channel registers
  `DerivedComposeKind.FlatSum` (`DerivedStatRegistry.cs:249`), and `FlatSum` sums **only `Flat` ops**
  (`DerivedComposer.cs:42`). An `Increased` modifier on `combat.power.omni` is dropped with no error
  and no log.

All three are pre-existing and independent of auras. They are worth fixing regardless.

### 4.2a The idempotence rule (D2) — this module owns it

**Rule:** *a derived-channel contribution is a function of its inputs `(source, coefficients, Θ)` only
— never of the channel's current value.* Re-asserting it any number of times yields the same channel
value.

Without this, a producer that reads-then-adds is **geometric in re-assertion count**, which is the real
overflow path (audit D2). `Overlay`'s replace semantics make today's single producer *accidentally*
idempotent; `OverlayAdd` removes that accident.

**Test:** hold inputs fixed, apply the same contribution twice, assert the channel value is identical
to applying it once.

---

## 2. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Derived
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Guard.Tests
python scripts\audit-magic-numbers.py --summary
python scripts\audit-overflow.py
```

---

## 3. Project structure

| Path | Change |
|---|---|
| `src/FusionRpg.Core/Stats/Derived/DerivedContributionBag.cs` | **new** — the bucket |
| `src/FusionRpg.Core/Stats/Derived/ActorDerivedSnapshot.cs` | edit — carry the bag; add `OverlayAdd` |
| `src/FusionRpg.Core/Stats/Derived/DerivedComposer.cs` | edit — retain contributions while composing |
| `src/FusionRpg.Core/Effects/Atoms/AtomRowValidator.cs` **or** `BindGate.cs` | edit — the op × compose-kind rejection (D6). ⚠️ **Option (c)'s landing site; an earlier draft chose bind-time validation and then listed no file for it** |
| `src/FusionRpg.Injector/Effects/PatronAuraOverlay.cs` | edit — use `OverlayAdd`, drop the manual `Get +` **in the same change**, or the patron aura doubles |
| `src/FusionRpg.Core/Status/ActorDerivedProfiles.cs` | **triage only** — five `.Overlay(` sites; confirm each still wants *replace* semantics. `ActorDerivedProfilesTests.cs:87` pins them |
| `tests/FusionRpg.Core.Tests/Stats/Derived/DerivedContributionBagTests.cs` | **new** |
| `tests/FusionRpg.Core.Tests/Stats/Derived/DerivedOverlayAddTests.cs` | **new** — the D1 + D2 guards |

⚠️ **Sizing.** With the validator, the six `Overlay` sites, and the profile tests, this is **L, not M** —
ten-plus files against a ≤5 standard. It should split three ways before planning: **(i)** `OverlayAdd`
+ idempotence rule + patron migration; **(ii)** the bag + compose-time contribution retention;
**(iii)** the op × compose-kind validation.

---

## 4. Design

### 4.1 `DerivedContributionBag` — mirror `ModifierBag`, do not invent a new shape

Key on the same tuple shape the primary bag proved: **`(sourceId, channelId, op)`**. `SourceId` is
already on `DerivedModifier`; `ApplyOwnerKey` has no derived analogue and is deliberately **not**
added — derived resolution is already per-actor by construction.

```csharp
public sealed class DerivedContributionBag
{
    public void Upsert(DerivedModifier m);
    public bool Withdraw(string sourceId);                 // all channels from one source
    public bool Withdraw(string sourceId, string channelId);
    public IReadOnlyList<DerivedModifier> For(string channelId);
    public IReadOnlyList<DerivedModifier> All { get; }
}
```

**The invariant this module exists to hold** (`effect-funnel.md:100`, restated because a downstream
reader will not follow the link): *two sources contributing to one channel stay two entries.*

⚠️ **Corrected justification (2026-08-30).** An earlier draft justified this by *"auras are withdrawn
mid-run by eviction, so withdrawal must remove exactly one source."* **That reasoning does not hold
under this module's own scope limit.** `ActorHub.ResolveDerived` rebuilds its modifier list from
subsystem contributions and recomposes **from scratch on every call** (`ActorHub.cs:50-56`, *"fresh per
call in v1"*) — and §4.4 keeps it that way. In a fresh-per-call pipeline an evicted aura simply stops
contributing on the next resolve; **there is nothing to withdraw.**

**The real justifications, which do survive:**

1. **Provenance for GG-49** — *"'why did my attack drop?' is answerable from the interface"*. Today that
   holds only vacuously, because no derived value is shown at all. This is the module's primary reason
   to exist.
2. **Audit D1** — `Overlay` is replace-not-add, so a second producer silently erases the first.
3. **Audit D2** — no idempotence rule exists for derived-channel re-assertion, and fixing D1 *removes*
   the accident that currently provides it.

`Withdraw(sourceId)` is therefore specified as **API completeness and future-proofing**, not as a
correctness requirement of the aura path. It stays cheap; it is simply not the reason.

### 4.2 `ActorDerivedSnapshot` keeps its contributions

`Channels` stays exactly as it is — every existing reader is unaffected. The snapshot gains:

```csharp
public IReadOnlyList<DerivedModifier> ContributionsFor(string channelId);
public ActorDerivedSnapshot OverlayAdd(IEnumerable<KeyValuePair<string, double>> extra);
```

`OverlayAdd` **adds** to the existing value; `Overlay` (replace) is retained for genuine replacements
and gains an XML comment saying which to use when. This is the D2 fix.

⚠️ **`PatronAuraOverlay.cs:37` must switch to `OverlayAdd` and drop its manual `derived.Get(channel) +`
compensation in the same change** — leaving both would double the patron aura.

### 4.3 D1 — make a percentage on `combat.*` mean something

`FlatSum` sums only `Flat`. Two options; **this spec chooses (b)**:

- **(a)** Change `combat.*` to a compose kind that honours `Increased`. Rejected: it changes the
  meaning of every existing `combat.*` producer and would move battle goldens.
- **(b)** Make `DerivedComposer` **throw at compose time** on an op the channel's kind cannot express.
  ⚠️ **Rejected on audit** — see below.
- **(c) Validate at bind/author time.** ✅ **Chosen.** Reject the op×compose-kind mismatch where the
  authoring error originates (`AtomRowValidator` / `BindGate`), and leave `DerivedComposer` a pure fold.

**Why (b) was rejected.** It would convert a shipped, *deliberate* graceful degradation into a runtime
throw. `AptitudeResolver.cs:51-58` picks the op from the target channel's compose kind and says so
explicitly: *"No shipped edge targets a `MaxPriorityFlag` channel today; **Flat is still the
least-surprising fallback if one ever does**, since Flag/Replace/Increased in that kind's 'max of' set
would let one point silently overrule every other source."* But `ComposeMaxFlag`
(`DerivedComposer.cs:63-68`) filters to `Flag|Replace|Increased` — it **drops `Flat`**. So under (b),
the first aptitude edge to target a `MaxPriorityFlag` channel turns that documented fallback into a
throw deep inside `Resolve`. The earlier draft never mentioned that call site.

**And D1 is currently hypothetical.** The only `DerivedModifierOp.Increased` producer under `src/` is
`AptitudeResolver.cs:59`, which is guarded by construction. **No live producer emits `Increased` on a
`FlatSum` channel.** Introducing a runtime regression risk in the compose hot path to fix a defect
nothing triggers is the wrong trade.

**The rule must also be stated in full, not by one example.** `SumIncreased` drops `Flat`;
`FlatReplace` drops `Increased`; `MaxPriorityFlag` drops `Flat`. Enumerate **all four compose kinds ×
four ops** and say which combinations are rejected — the earlier draft tested one pair and left the
blast radius unmeasured.

> The repo precedent for rejecting rather than defaulting is real — `ActionShareTable.PermilleOf`
> *"throws — never returns a default"* (`ActionShareTable.cs:29-33`) — but note **it throws at lookup of
> authored data**, not inside a per-resolve fold. (c) matches that precedent; (b) does not.

### 4.4 What this module does NOT do

- **No caching or cross-resolve accumulation.** `ActorHub` stays *"fresh per call in v1"*. This module
  makes contributions *retainable within one resolve*, not persistent. Persistence is a separate
  decision with its own invalidation problem.
- **No web contract change.** `ActorChannelDetail.contributions` has no server producer today
  (`adapt.ts:37` is unconditionally pending); wiring it is `aura-surface`'s job.
- **No `More` op on the derived side.** `AtomKindRegistry.cs:149` states there is none; this module
  does not add one.

---

## 5. Code style

Match `ModifierBag.cs` and `DerivedComposer.cs`: `StringComparer.Ordinal` everywhere, `readonly`
fields, XML doc on every public member naming the spec section it implements. Deterministic ordering
for any tie-break (`SourceId` ordinal, as `StatComposer.cs:18-19` and `DerivedComposer.cs:54` both do).

---

## 6. Testing strategy

`tests/FusionRpg.Core.Tests/Stats/Derived/`, xUnit, matching the existing derived tests.

| # | Test | Asserts |
|---|---|---|
| 1 | Two sources, one channel | two entries, both retained, composed value is their sum |
| 2 | Withdraw one source | the other's contribution survives **unchanged** — the `effect-funnel.md:100` invariant |
| 3 | Withdraw a source that never contributed | no-op, returns false, no throw |
| 4 | `ContributionsFor` after compose | provenance survives the fold |
| 5 | **`OverlayAdd` accumulates** | two overlays on one channel sum; **the D1 regression guard** |
| 5b | **Idempotence (D2)** | applying the same contribution twice yields the same channel value as once |
| 6 | `Overlay` still replaces | the old behaviour is intact for real replacements |
| 7 | `Increased` on a `FlatSum` channel | **rejected at BIND/AUTHOR time** (`AtomRowValidator`/`BindGate`), naming channel and op (D6). ⚠️ **Not a compose-time throw** — §4.3 rejects option (b) because it would turn `AptitudeResolver`'s documented `Flat` fallback into a runtime throw |
| 7b | The full op × compose-kind matrix | all four kinds × four ops enumerated; each pair either accepted or rejected-with-reason. Testing one pair leaves the blast radius unmeasured |
| 8 | Patron aura value unchanged | `PatronAuraOverlay` through `OverlayAdd` produces the same number as today — no silent balance change |
| 9 | Full suite | `dotnet test tests\FusionRpg.Core.Tests` green; **no golden moves** |

**Test 8 is the one that would catch the most likely mistake in this module** (converting the overlay
and leaving the manual `+`).

---

## 7. Boundaries

**Always**
- Preserve per-source provenance through compose.
- Keep `Channels` shape-compatible — every current reader keeps working untouched.
- Deterministic ordering for tie-breaks.

**Ask first**
- Any change to an existing channel's registered `DerivedComposeKind` (moves battle goldens).
- Adding cross-resolve caching.

**Never**
- Fold two sources into one entry.
- Silently drop a modifier — reject it at bind/author time, naming the channel and op.
- Add a `More` op to the derived side.
- Touch PvZ. This is RPG-layer only.

---

## 8. Success criteria

- [ ] Two sources on one channel are two retained entries; withdrawing one leaves the other exact.
- [ ] `OverlayAdd` accumulates; a second overlay never erases the first.
- [ ] `PatronAuraOverlay` uses `OverlayAdd`, has no manual `Get +`, and produces the same value.
- [ ] `Increased` on a `FlatSum` channel is **rejected at bind/author time**, naming both — and the
      full op x compose-kind matrix is enumerated, not just this one pair.
- [ ] **Idempotence (D2) holds** and is tested; it lands in the SAME change as `OverlayAdd`, because
      `OverlayAdd` removes the accident that currently provides it.
- [ ] `ContributionsFor(channel)` returns every contributing modifier with its `SourceId`.
- [ ] Full Core + Guard suites green. No goldens move.

## 9. Open questions

1. **Should the bag be exposed on `ActorDerivedSnapshot` or returned alongside it?** Exposing it keeps
   one object; returning a pair keeps the snapshot a pure value. Leaning toward exposing, since every
   existing caller already threads the snapshot.
2. **Should `Withdraw` be idempotent-silent or report?** Chosen: returns `bool`, never throws — matches
   `EffectBag.Withdraw`'s shape.
