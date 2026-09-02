# seed-to-concrete: open architecture decisions

Two remaining audit items (T6.1, T6.2) each hit a genuine, reviewed-vocabulary boundary — not a
missing implementation, a missing *decision*. AGENTS.md's own hard boundary: *"Architecture changes
that lock behavior need `decisions.md` first."* Both closed vocabularies (`OwnerKind`, `CurveInput`)
are deliberately small and reviewed (their own doc comments call them "the seven owner scopes a
binding may attach to" and name "adding a curve input" as an explicit ask-first line) — extending
either is exactly that class of change. This file proposes both plainly, drafted so a decision can be
made in minutes, not because the direction is unclear but because AGENTS.md requires the ask before
the code, not after.

Once decided, promote the chosen answer into `docs/architecture/decisions.md` and this file can be
deleted — it is scratch space for the ask, not a permanent record.

---

## Decision 1 — a new `OwnerKind` for a persistent unique actor (blocks T6.1)

✅ **Resolved 2026-09-02** — owner approved the recommendation below via `AskUserQuestion`
("Approve OwnerKind.UniqueActor (Recommended)"). Built and tested the same day; promoted into
[../docs/architecture/decisions.md](../docs/architecture/decisions.md) (`OwnerKind.UniqueActor` row).
Kept here for the reasoning trail — see `tasks/seed-to-concrete-todo.md` T6.1 for the current
build/test evidence and the one remaining wiring gap (no live consumer yet).

**Where:** `src/FusionRpg.Core/Effects/Atoms/OwnerScope.cs`. Today's seven values —
`Match, Plant, Zombie, Entity, Player, Sector, Slot` — cover a live match, a PvZ board unit, a
session-scoped Unity pointer, a player account, and two world-map concepts. None represents a
**persistent, per-actor RPG entity** (`rpg_unique_actor`, identified by a stable `instance_id`,
outliving any one session).

**Why it matters:** `mods-absorption` (T6.1) needs equipped-item effects to bind through
`effect_binding` instead of the legacy `mods_json` blob. Binding needs an `OwnerScope`. The only
existing candidate that superficially fits is `OwnerKind.Entity` — but `OwnerScope.cs`'s own doc
comment is explicit: *"entity: bindings are session-scoped and never durable — the pointer is
reused."* Using it would make every unique actor's equipped-item bonuses **vanish on the next session
boundary** (`ClearSessionScopedBindings()` already deletes every `entity:` binding on purpose). That
is not a style mismatch, it is a real, silent data-loss bug the first time a player logs back in.

**Recommendation:** add `OwnerKind.UniqueActor`, keyed on the actor's own `instance_id` (already a
stable, non-reused string — unlike `Entity`'s IL2CPP pointer, which is exactly why `Entity` cannot be
reused for this). Grammar: kebab/alnum id, matching `Sector`/`Slot`'s own `IdRe` pattern (their key
grammar, not their durability semantics — a unique actor's binding is durable like `Player`'s, just
scoped to one actor rather than the whole account).

**What changes if approved:** `OwnerScope.cs` gains one enum value + one `Validate`/`Name` case
(small, mechanical, already-proven-safe pattern — the same shape every existing kind already follows).
`RpgStore.UniqueActors.cs`'s `RebuildUniqueModsFromEquipmentUnlocked` gains the reconciliation logic
already drafted and reverted this session (produce/withdraw bindings for atom-backed items,
`OwnerScope(OwnerKind.UniqueActor, instanceId)`), restorable from this session's own working
implementation. `SessionScoped`/`ClearSessionScopedBindings()` stay untouched — a `UniqueActor`
binding is explicitly NOT session-scoped, matching the module's own "no steady state where both
paths are live" invariant surviving a restart.

**If this is not the right shape**, the two live alternatives are: (a) reuse `OwnerKind.Player` with
the actor id folded into the binding's own `Slot` string instead of a real owner key — smaller diff,
but conflates two different concepts into one field and was not pursued further because it is itself
a second, less clean design decision, not a smaller version of the same one; (b) leave `mods-
absorption` unbuilt for unique actors specifically and scope T6.1 down to whatever content has no
durability requirement — narrows the task's own acceptance line, which is itself a call only the
audit's owner should make.

---

## Decision 2 — extending `CurveInput` for `patron-absorption` (blocks T6.2), or accepting the module cannot ship as specified

▶ **Direction chosen 2026-09-02** — owner selected **option 2** below ("scope a real redesign") over
this file's own recommendation (option 3, "don't migrate as specified"), via `AskUserQuestion`. This
is a direction, not yet a resolved mechanism: option 2 itself says a real design pass is still needed
before code — see `tasks/seed-to-concrete-todo.md` T6.2 for what that pass still has to answer
(how/where `P(Θ)` gets cached off the hot path, and whether `CurveInput.Star` is added alongside it).

**Where:** `src/FusionRpg.Core/Effects/Atoms/CurveTable.cs`. `CurveInput` is `{ Level, Rarity, Tier }`.
E2's own spec (`spec-value-spec-and-curve.md`) lists its boundaries explicitly: *"Ask first: adding a
roll policy; adding a curve `input`."*

**Why it matters:** `patron-absorption`'s own spec says the container "keys its curve on star/level"
— but `AuraMilli`'s real formula (`PatronPolicy.cs`) needs THREE things no existing curve input
reads: `star` (not in the enum at all), a per-**rarity** flat base (rarity IS in the enum, this part
is fine), and — the harder piece — `P(Θ)`, read through the shared, **quadratic** `PowerLadder`. A
per-mille linear-interpolated curve cannot reproduce a quadratic function exactly across an unbounded
Θ range without literally tabulating thousands of points, and `PowerReads.IntegerFifthRoot`'s own
standing warning in this program says a `BigInteger` binary-search power read must stay OFF hot
paths — exactly where this migration would put it (the objective's own words: *"relocates a
**hot-path** plugin").

> ⚠️ **Correction, 2026-09-02 — the "hot path" half of this section's own premise was wrong,**
> found by actually reading `spec-power-ladder.md` and `PatronPolicy.cs` instead of trusting this
> file's own earlier paraphrase. `PatronPolicy.AuraMilli` reads `PowerLadder.Value(pTheta)`
> (`src/FusionRpg.Core/Demons/Patron/PatronPolicy.cs:58-59`) — **not**
> `PowerReads.IntegerFifthRoot`. Those are two different functions in two different modules that
> happen to share the letters `P`/`Θ`/`Power`: `PowerLadder.Value` is `spec-power-ladder.md`'s pure,
> integer, **O(1)** closed-form arithmetic (`C + A·Θ + B·Θ(Θ−1)/2`, one rounding at the end) —
> explicitly tested for "no allocation on the hot path" and "same index, 1000 calls, identical
> result," and its own module doc says nothing forbids calling it on a hot path; the doc-comment on
> `AuraMilli` itself says it reads "the SAME shared `PowerLadder` every other magnitude in this
> codebase reads." `PowerReads.IntegerFifthRoot` is a *different, unrelated* function
> (`Effects/Atoms/Power/PowerReads.cs`) — a `BigInteger` binary search used only by `PowerScalar.Of`
> (E10's *display scalar*, which the E10 spec itself says has no production caller today) — and
> `spec-player-materialise.md`'s "Standing warning" names **that** function by name, not
> `PowerLadder`. **`AuraMilli` never calls `IntegerFifthRoot` at all.** There is no BigInteger, no
> binary search, and no hot-path cost problem in migrating the `P(Θ)` term — it is exactly as cheap
> as every other `PowerLadder.Value` call already shipped in this codebase.
>
> **What is still a real, open, structural gap (not performance):** `CurveTable.MultiplierAt`/
> `ApplyMilli` express **one per-mille multiplier applied to a base value**. `AuraMilli`'s real shape
> is `clamp(RarityBaseMilli(rarity) + PerStarMilli·star + level, 0, AuraClampMilli) +
> PThetaKMilli·P(Θ)/1000` — an **additive**, **clamped**, **two-independently-scaled-term** formula.
> A single multiplier cannot reproduce a clamp, and cannot reproduce an *added* (not multiplied) term
> alongside it. Adding `CurveInput.Star` (mechanical, same shape as Decision 1) closes the missing-
> input half; it does **not** close this shape mismatch — `effect_curve`/`CurveTable` would need a
> genuinely new curve **kind** (additive-with-clamp-and-a-power-term), which is real vocabulary
> growth under E2's own "Ask first: adding a curve input" boundary, just a differently-shaped ask
> than the one this section originally described.

**This is a harder call than Decision 1** — it is not "add one enum value," it is "the mechanism this
spec assumed existed does not, for the harder half of the formula." Three real options (the
performance framing below is superseded by the correction above; the curve-*shape* mismatch is the
real reason option 2 is nontrivial, not a hot-path cost):

1. **Add `CurveInput.Star`** (mechanical, same shape as Decision 1) for the flat/star/level part —
   this alone does NOT resolve the P(Θ) half, since the shape mismatch (below) is separate from the
   missing input.
2. **Extend `effect_curve`/`CurveTable` with a new curve kind that can express an additive, clamped,
   two-term formula** (`clamp(base, 0, cap) + K·PowerLadder.Value(Θ)/1000`) rather than only "one
   multiplier on a base value." Calling `PowerLadder.Value` directly inside atom resolution is cheap
   (corrected above — no BigInteger, no hot-path cost, no precompute/cache needed); the actual work
   is a curve-kind vocabulary extension under E2's own "Ask first: adding a curve input" boundary — a
   real design change to E2's own boundary, not a one-line addition — needs its own spec pass, not a
   decision made in this file.
3. **Do not migrate `patron-absorption` as specified.** Keep `PatronSecondaryPlugin.cs` computing
   `AuraMilli` inline (unchanged, already shipped, already tested) and treat T6.2 as correctly
   descoped from this program rather than forced through a mechanism that cannot reproduce it exactly
   — the module's own boundary explicitly forbids an approximation (*"Never: approximate the
   curve"*), so "ship something close" was never a legal fourth option.

**Owner chose option 2, 2026-09-02** (via `AskUserQuestion`, over this file's original recommendation
of option 3) — recorded here for the reasoning trail; see `tasks/seed-to-concrete-todo.md` T6.2 for
the corrected, current scope of what option 2 actually requires now that the hot-path premise above
is fixed.

> ⚠️ **Second correction, same day — option 2's own mechanism was mis-scoped too.** Reading
> `spec-value-spec-and-curve.md` (E2) in full (not just the boundary line already quoted) surfaced its
> own **"Event-linked magnitudes" section (P0.2, landed 2026-08-28)** — the SAME class of problem,
> already solved once, with a shipped precedent this file's earlier options didn't use. Lifesteal
> ("heal for 50% of the damage this attack dealt") needed a magnitude `ValueSpec`'s three roll
> policies couldn't express (nothing in scope has the firing event to read). The shipped fix was
> **not** a new roll policy and **not** a new curve `input`/`kind` — it was a new, closed,
> mutually-exclusive `ValueSpec` **marker shape** (`{"eventField":"damage","multiplierMilli":500}`),
> baked at compile time by `AtomCompiler.ResolvedParams` into a marker object in place of a plain
> number, unwrapped by the one specific consumer that has what the marker needs in scope
> (`DamagePacketBuilder.FromOverlay`) — scoped to exactly the one kind that needs it
> (`resource.delta`), explicitly owner-authorized (action-ideal.md §8.5) as its own small ask.
>
> `AuraMilli`'s `P(Θ)` term is structurally the same shape: a magnitude that needs something no
> `ValueSpec` roll policy or `CurveTable` row can supply (Θ isn't a firing event field, but it's the
> same "the thing this magnitude needs isn't in a `ValueSpec`'s own scope" problem `eventField` solved
> for damage-events). The right mechanism, by direct analogy: a **new closed marker member**, e.g.
> `{"powerLadder": true, "kMilli": N}`, baked by `AtomCompiler.ResolvedParams` into a marker object,
> unwrapped by whichever consumer resolves the `progression.bonus.*`/aura channel this feeds (reads
> the actor's own Θ, computes `kMilli · PowerLadder.Value(Θ) / 1000`, adds it to the clamped flat part
> — which itself resolves through the ordinary `CurveInput.Star` extension, ask-first but mechanical,
> same shape as Decision 1). **Not a new curve kind** (my own first correction's proposal) — a
> `ValueSpec` marker, matching the ONE precedent this program already has for "a magnitude needs
> something outside `ValueSpec`'s normal scope," reviewed and shipped the same way `eventField` was.
>
> This is still real design work needing its own ask (E2's boundaries: `eventField` itself was a
> reviewed, owner-authorized addition, not something built unilaterally) — not resolved here, but
> now scoped against the actual, shipped precedent rather than an invented mechanism. See
> `tasks/seed-to-concrete-todo.md` T6.2 for the current state.

---

## What this file does NOT cover

`T3.8`'s remaining half (declared metric targets) and `T2.11` (the real classification run) are not
architecture decisions — they are content/balance calls and an owner-run action respectively, already
recorded with that framing in `tasks/seed-to-concrete-todo.md`. Nothing to propose here; they need a
number and a terminal session, not a design choice.
