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

**This is a harder call than Decision 1** — it is not "add one enum value," it is "the mechanism this
spec assumed existed does not, for the harder half of the formula." Three real options:

1. **Add `CurveInput.Star`** (mechanical, same shape as Decision 1) for the flat/star/level part —
   this alone does NOT resolve the P(Θ) half.
2. **Extend the atom-resolution path to read `PowerLadder` directly, off the hot path** — e.g.
   precompute `P(Θ)` once per relevant Θ band and cache it, rather than reading a curve per resolve.
   A real, larger design change to E2/E9's own boundary, not a one-line addition — needs its own
   spec pass, not a decision made in this file.
3. **Do not migrate `patron-absorption` as specified.** Keep `PatronSecondaryPlugin.cs` computing
   `AuraMilli` inline (unchanged, already shipped, already tested) and treat T6.2 as correctly
   descoped from this program rather than forced through a mechanism that cannot reproduce it exactly
   — the module's own boundary explicitly forbids an approximation (*"Never: approximate the
   curve"*), so "ship something close" was never a legal fourth option.

**Recommendation:** option 3, unless there is appetite for option 2's own real design work (which
this file is not the place to scope). Option 1 alone would leave the module half-migrated with the
harder, riskier half (the one under an open LIVE gate) still unmoved — worse than not starting,
because it would look done.

---

## What this file does NOT cover

`T3.8`'s remaining half (declared metric targets) and `T2.11` (the real classification run) are not
architecture decisions — they are content/balance calls and an owner-run action respectively, already
recorded with that framing in `tasks/seed-to-concrete-todo.md`. Nothing to propose here; they need a
number and a terminal session, not a design choice.
