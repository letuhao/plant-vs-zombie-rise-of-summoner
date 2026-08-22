# Item enrichment — defect register (wave R1)

**Status:** Verification report, 2026-08-22. Deliverable of **R1** in
[reconciliation-plan.md](reconciliation-plan.md). Bound by
[enrichment-contract.md](enrichment-contract.md).

**This document designs nothing and fixes nothing.** It settles the thirteen defect claims the
enrichment lanes raised against shipped code, with `file:line` evidence and executed test results.
Every lane left the design gate's *"I tested the constraint"* box unticked; this closes it.

Where a claim is refuted, the refutation is stated plainly and the *real* defect underneath it —
where there is one — is written down separately, because several lanes were pointing at something
true while naming the wrong thing.

---

## 0. The finding that reframes almost every other one

**The E6 instance/binding layer has zero production consumers outside `FusionRpg.Data`.**

Verified by exhaustive grep across `src/`: no reference to `InstanceRow`, `InstanceAtomRow`,
`ResolveBindings`, `GetInstance`, or `values_json` exists anywhere outside
`src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs` and
`src/FusionRpg.Core/Effects/Atoms/Instantiator.cs`. `AtomCompiler` and `RunnerEntry` are referenced
only by `tests/FusionRpg.Core.Tests/Atoms/AtomCompilerTests.cs`.

Consequences for this register:

- Claims **1, 5, 6, 7, 8, 10, 11** describe defects in a layer nothing executes yet. They are
  **latent**: no player data is being lost today, because no code path creates a player-owned item
  instance. They are still real, and every one of them blocks the item program at the moment it
  wires up.
- Claim **4** is the exception. `StatApplyScope` is on the **live** hot resolve path
  (`src/FusionRpg.Core/Stats/StatSystem.cs:160`, inside `StatSystem.Resolve`). Its defect ships today.

Severities below are graded on **"cost at the moment the item program lands"**, not on
"players are losing items right now", and each row says which it is.

---

## 1. What was executed

`FUSIONRPG_GAME_DIR` was **unset** for every command below. All three suites restored, built, and ran
without it. *A lane claiming these suites need the game directory is claiming an untested constraint;
they do not.* (The injector projects are a separate matter and were not built here.)

### Test suites

The owner committed **c4c9908** partway through this session, so both runs are recorded. The second
is the current one.

**Run 1 — `HEAD` 842907f plus the uncommitted worktree:**

```
Passed!  - Failed: 0, Passed: 2235, Skipped: 0, Total: 2235, Duration:  2 s - FusionRpg.Core.Tests.dll  (net8.0)
Passed!  - Failed: 0, Passed:  353, Skipped: 0, Total:  353, Duration: 22 s - FusionRpg.Data.Tests.dll  (net8.0)
Passed!  - Failed: 0, Passed:   54, Skipped: 0, Total:   54, Duration:  3 s - FusionRpg.Guard.Tests.dll (net8.0)
```

**Run 2 — `HEAD` c4c9908 plus the remaining worktree, re-run after the commit landed:**

```
dotnet test tests/FusionRpg.Core.Tests
Passed!  - Failed:     0, Passed:  2257, Skipped:     0, Total:  2257, Duration: 568 ms - FusionRpg.Core.Tests.dll (net8.0)

dotnet test tests/FusionRpg.Data.Tests
Passed!  - Failed:     0, Passed:   353, Skipped:     0, Total:   353, Duration: 23 s - FusionRpg.Data.Tests.dll (net8.0)

dotnet test tests/FusionRpg.Guard.Tests
Passed!  - Failed:     0, Passed:    54, Skipped:     0, Total:    54, Duration: 2 s - FusionRpg.Guard.Tests.dll (net8.0)
```

**Current: 2664 tests, 0 failures, 0 skipped.** Core gained **22** tests across the commit
(2235 → 2257); Data and Guard are unchanged. **Nothing went red at any point.**

Two notes against the repo's own working memory:

- `CLAUDE.md` records `LawnCoordsGuardTests.CellPos_delegates_to_LawnCoords_CellCenter` as fixed —
  confirmed, Guard is fully green.
- It also warns that two `Overlay_*` guard tests go red during the VFX migration. **They do not
  today.** Guard is 54/54.

### Boundary guards

```
scripts\guard-single-writer.ps1       SINGLE-WRITER GUARD OK — no combat field writes outside EntityStatWriter.cs        exit 0
scripts\guard-secondary-no-unity.ps1  SECONDARY NO-UNITY GUARD OK — plugins Grant/Withdraw only                          exit 0
scripts\guard-funnel-delta.ps1        FUNNEL DELTA GUARD OK — Secondary enqueue via Funnel only                          exit 0
scripts\guard-dal.ps1                 DAL GUARD OK — no SQLite/SQL outside FusionRpg.Data                                exit 0
```

**All four green.** Re-run after commit c4c9908 landed — still all four green, identical output. No
claim in this register is held back by a red guard.

### What was not executed

No new test file was written. The R1 brief permits exactly one output file, so every "minimal repro"
below is **described and traced through the code**, not run. Three of them (claims 3, 7, 11) are
single-assertion tests against existing fixtures and would each take under ten lines; they are named
in §5 as the first work of any fix wave.

---

## 2. The claims

### Claim 1 — the orphan sweep deletes unequipped items

> `CollectOrphanInstancesUnlocked` deletes every `effect_instance` with no binding and runs after
> every withdraw — therefore unequipping a player-owned item would DELETE it.

**Verdict: CONFIRMED** (latent — see §0).

**Evidence.**

- `src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:460-471` — `CollectOrphanInstancesUnlocked`
  issues two unconditional deletes over `effect_instance_atom` and `effect_instance`, keyed on
  `NOT EXISTS (SELECT 1 FROM effect_binding b WHERE b.instance_id = i.instance_id)`. There is no
  origin filter, no `container_kind` filter, no owner filter, and no grace period.
- `:404-417` — `Withdraw(bindingId)` deletes the binding and then calls the sweep on **any**
  successful removal (`if (removed) CollectOrphanInstancesUnlocked(db);`).
- `:423-434` — `ClearSessionScopedBindings()` calls it too, which is its designed purpose.

**What the code actually does.** Unbinding is treated as *destruction*, not as *unequipping*. The
doc comment at `:436-440` states the intent explicitly and correctly for the case it was written
for: `entity:` bindings are session-scoped, so their instances are unreachable garbage after a
match, and without the sweep "a durable database would grow by one instance per entity binding per
match, forever." The defect is that the sweep does not distinguish that case from an item the player
took off and put in a bag.

**Existing coverage — and this is the important part.** The behaviour is not merely untested, it is
**asserted as correct**:

- `tests/FusionRpg.Data.Tests/BindResolutionTests.cs:217-225`,
  `Withdrawing_the_last_binding_does_not_leave_an_orphan_instance` — binds one instance to
  `player:1`, withdraws it, asserts `CountOrphanInstances() == 0`. The instance is gone and the test
  says that is right.
- `:227-237`, `An_instance_with_a_surviving_binding_is_never_collected` — the only guard, and it only
  covers the *second* binding surviving.
- `tests/FusionRpg.Data.Tests/AtomInstanceStoreTests.cs:204-218` withdraws and asserts on the
  remaining bindings, never on the instance.

A fix therefore **moves a green test**, and the moved test is the one whose name asserts the current
behaviour is desirable. That is an owner decision, not a bug-fix.

**Minimal repro (described).** `BindOf("item.ember-band", OwnerKind.Player, "1")` → capture
`InstanceId` → `Withdraw(bindingId)` → `GetInstance(instanceId)` returns `null`. One assertion
against the existing `BindResolutionTests` fixture.

**Severity: HIGH** — latent today, **blocking** for I13 (inventory) and I2 (equip slots), both of
which assume unequip is reversible.

**Fix size: M.** Two candidate shapes, both listed by the plan as build work, not design work: a
retention predicate on the sweep (keyed on `container_kind` or on `origin`), or the
assign/bind projection split that **D1** is chartered to decide. Owner module: **E6**
(`spec-instance-and-binding.md`). Collides with the golden-ordering hazard flagged in
[../decisions.md](../decisions.md); the reconciliation plan already ring-fences this as
owner-authorised build.

---

### Claim 2 — no durable per-specimen owner scope exists

> None of the 7 owner scopes is a durable per-specimen scope; `entity:` is contractually
> session-scoped. Durable equipment on a specimen is not expressible.

**Verdict: CONFIRMED.**

**Evidence.** `src/FusionRpg.Core/Effects/Atoms/OwnerScope.cs:11-20` defines exactly seven kinds:
`Match`, `Plant`, `Zombie`, `Entity`, `Player`, `Sector`, `Slot`. Taken one at a time against
`Validate` (`:97-144`) and against [../effect-atom/definitions.md](../effect-atom/definitions.md) §6
(`:198-206`):

| Scope | Key grammar | Granularity | Durable? |
|---|---|---|---|
| `match` | empty | the whole match, **both sides** | n/a |
| `plant:N` / `zombie:N` | decimal typeId | every unit of that **type** | yes, but type-wide |
| `entity:hex` | lowercase hex pointer | one live object | **no** — `IsSessionScoped => Kind == OwnerKind.Entity` (`:39`) |
| `player:N` | decimal id > 0 | the account | yes, account-wide |
| `sector:` / `slot:` | kebab id | world map | yes, but a place, not an actor |

There is no key that names *this one plant, across restarts*. `entity:` is the only per-specimen
scope and it is contractually forbidden from being durable — `OwnerScope.cs:38`, `definitions.md:224`
(*"`entity:` bindings are session-scoped and never durable"*), and the reason is stated: IL2CPP
reuses the pointer, so a surviving binding would attach to whatever object took the address.

Corroborating evidence from the *other* side of the codebase: `StatApplyScope.cs:29-37` reserves an
`instance:{guid}` key for exactly this purpose and **hard-refuses it in hot resolve** (`:47-49`,
`S-INSTANCE-KEY-HOT`), commented *"Must not match in Hot Resolve until a binder translates to
`entity:{ptr}`."* Somebody already saw this gap and parked it. `src/FusionRpg.Core/Match/UniqueOwnerBinder.cs`
is the stub of the binder that would close it.

**Existing coverage.** `tests/FusionRpg.Core.Tests/Atoms/BindGateTests.cs:73` asserts
`Parse("player:1").IsSessionScoped == false`; the seven-scope grammar is covered by the `InlineData`
rows at `:39-97`. Nothing tests durability, because nothing implements it.

**Severity: HIGH** — this is the load-bearing gap for the whole equipment program. Not a bug; a
missing capability with a parked stub in two places.

**Fix size: L.** Owner module: **E6**, with a Core/Stats counterpart in `UniqueOwnerBinder`. This is
precisely question **D1** in the reconciliation plan (`actor:{instanceId}` scope vs the assign/bind
projection split) and should not be pre-empted here.

---

### Claim 3 — `BindGate` fails open on `level_req`

> `BindGate` skips the `level_req` check entirely when `ctx.OwnerLevel` is null — fails open. Tests
> cover "met" and "absent" but never "present but unknown".

**Verdict: CONFIRMED, and worse than claimed.**

**Evidence.**

- `src/FusionRpg.Core/Effects/Atoms/BindGate.cs:47-49`:
  ```csharp
  if (levelReq is { } req && ctx.OwnerLevel is { } level && level < req)
      return AtomRejection.Fail(AtomRejectionReason.LevelTooLow, ...);
  ```
  Both patterns must match. `levelReq = 30, ctx.OwnerLevel = null` falls straight through to the
  atom loop and the binding is accepted. A level-30 item binds to an owner of unknown level.

- The test gap is exactly as claimed. `tests/FusionRpg.Core.Tests/Atoms/BindGateTests.cs:166-175`
  covers `OwnerLevel: 3, levelReq: 10` → rejected. `:177-184`,
  `Level_req_is_met_or_absent`, covers `OwnerLevel: 10, levelReq: 10` → ok and
  `OwnerLevel: 10, no levelReq` → ok. The *name* of that test is the tell: "absent" means the
  **requirement** is absent, not the level. `levelReq: 10, OwnerLevel: null` is tested nowhere.

**The part the lane missed.** `OwnerLevel` has **no production writer at all.** Grepping the whole
tree, the only two assignments to `BindContext.OwnerLevel` are
`BindGateTests.cs:172` and `:180`. And the store's resolve entry point takes a level it never uses:

```
src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:264
public BindResolution ResolveBindings(OwnerScope owner, BindContext ctx, int? ownerLevel = null)
```

`ownerLevel` is referenced **nowhere** in the method body (`:264-321`). The value that reaches
`BindGate.Check(rows, owner, ctx, levelReq, null)` at `:310` is `ctx.OwnerLevel`, which every
production caller leaves at its `null` default.

So `level_req` is not "fails open in an edge case". **`level_req` is not enforced anywhere in
production, ever**, and there is a silently discarded parameter that looks like it is. The container
column is read (`:387-394`, `LoadContainerLevelReqs`) and threaded all the way to a check that can
never fire.

This is the E8 `status.expose.*` scar tissue that the enrichment contract's **SC7** names by hand:
*"a row no code consumes is not content; it is a lie in a table."*

**Minimal repro (described).** One line added to the existing test class:
`Bind(StatModify("maxHp"), OwnerScope.Match, new BindContext(RuntimeId.Lawn), levelReq: 99).IsOk`
returns `true`. Should be `LevelTooLow`, or a new "level unknown" refusal.

**Severity: MEDIUM** — latent (nothing sets `level_req` in production content either), but it is a
gate that I11 (requirements) is building three more reason codes on top of, and it does not work.
I11 already spotted the fail-open (`ssot-requirements.md:730` names "the fail-open fix in §2.5") but
not the dead parameter.

**Fix size: S** for the fail-open (invert the condition and pick a reason code); **S** for deleting
or wiring the dead `ownerLevel` parameter. Owner module: **E6**.

---

### Claim 4 — `player:` scope buffs both sides

> `player:{id}` parses but has no real consumer: `StatApplyScope.cs:81-82` returns true with a
> "stub → match-wide apply" comment, `:88-92` reports match-wide, `EffectProcAndOwner.cs:59-60`
> likewise — and `match` matches BOTH sides. A `player:`-scoped `+atk` would buff the enemy side.

**Verdict: CONFIRMED. This is the only claim in the register that is live today, and the behaviour
is covered by a green test that asserts it.**

**Evidence.** The three cited sites are all real, at slightly different line numbers than the claim
gave:

- `src/FusionRpg.Core/Stats/StatApplyScope.cs:82-83` —
  `if (key.StartsWith("player:", ...)) return true; // stub → match-wide apply`
- `:88-93` — `IsMatchWide` returns true for `match` **or** any `player:` key.
- `:52-53` — `match` returns `true` without consulting `side`, `typeId`, or `entityKey`. So
  match-wide genuinely means *both factions*, and `player:` inherits that.
- `src/FusionRpg.Core/Effects/EffectProcAndOwner.cs:59-60` —
  `if (key.StartsWith("player:", ...)) return true; // match-scoped for now; player filter is grant-time`

**It is on the live path.** `src/FusionRpg.Core/Stats/StatSystem.cs:156-163`, inside `Resolve`,
iterates the session bag and admits every modifier for which
`StatApplyScope.Matches(m.ApplyOwnerKey, ctx)` is true. `Resolve` is the hot combat stat path
(it opens with `PerfProbe.Measure(PerfSection.StatsResolve)` at `:151`).

**It is reachable from a shipped surface.** `src/FusionRpg.Injector/CheatActions.cs:63-66` normalises
an owner key and admits it if `IsKnownOwnerKey`, and `IsKnownOwnerKey` returns true for `player:`
(`StatApplyScope.cs:100`).

**Existing coverage.** `tests/FusionRpg.Core.Tests/StatSystemTests.cs:423-432`:

```csharp
public void Session_ApplyOwnerKey_player_stub_is_match_wide()
{
    ...
    sys.Upsert(f.Flat("t", "effect", "gp", StatChannels.Atk, 4, applyOwnerKey: "player:9"));
    Assert.Equal(14, sys.Resolve(sys.Contexts.ForPlant("P", y0, typeId: 0)).Atk);
    Assert.Equal(14, sys.Resolve(sys.Contexts.ForZombie("Z", y0, typeId: 0)).Atk);
}
```

The claim's worked example *is the test*. A `player:9` `+4 atk` gives the plant 14 and the zombie 14.
`StatSystemTests.cs:316` separately asserts `IsMatchWide("player:1")`.

This matters for the fix wave: the behaviour is a **named, deliberate, tested stub**, not an
oversight. Correcting it turns a green test red on purpose, and the test name will have to change
with it. That is a decision the owner should take knowingly.

**Severity: HIGH and LIVE.** Every item affix the enrichment round designs is `player:`-scoped or
`actor:`-scoped by intent. Bound as `player:`, a `+50 atk` sword arms the zombies.

**Fix size: S** in `StatApplyScope` (make `player:` resolve against the resolving side/actor rather
than returning `true`), **M** once you account for the two other stubs and the moved test. Owner
module: this is **not** an E-numbered module — it is `FusionRpg.Core/Stats` and belongs to the
combat-unification stream, with **E6** owning the scope contract it must satisfy. Overlaps **D1**.

---

### Claim 5 — `effect_instance` does not persist `origin_catalog_revision`

> `effect_instance` does not persist `origin_catalog_revision`, although definitions.md §5's
> reproduction contract names it as an input. Origin reproduction after any import is unverifiable.

**Verdict: REFUTED as stated. The column exists under a different name. A different and larger
defect sits underneath the claim, and no lane raised it.**

**The refutation.** `src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:56-63`:

```sql
CREATE TABLE IF NOT EXISTS effect_instance (
  instance_id TEXT NOT NULL PRIMARY KEY,
  container_id TEXT NOT NULL,
  roll_seed INTEGER NOT NULL,
  catalog_revision INTEGER NOT NULL DEFAULT 0,
  created_utc TEXT NOT NULL,
  origin TEXT NOT NULL DEFAULT 'drop'
);
```

The column is `catalog_revision`, and that is the name definitions.md §5 uses (`:170`: *"Same
`(container_id, catalog_revision, roll_seed)`"*). It is documented as exactly what the lane wanted —
`src/FusionRpg.Core/Effects/Atoms/Instantiator.cs:32-37`: *"The catalog the rolls were taken against.
Reproducibility is claimed over `(container_id, catalog_revision, roll_seed)` — without this column
there is nothing to compare."* It is written on insert (`RpgStore.AtomInstances.cs:109-116`), read
back (`:337`), and tested:
`tests/FusionRpg.Data.Tests/BindResolutionTests.cs:167-176`,
`An_instance_records_the_catalog_revision_it_was_rolled_against`.

`ssot-enhancement.md:210` states *"Not stored today, which means origin reproduction after any import
is currently unverifiable"* and `:69` *"which E6 does not store yet"*. Both are wrong. This is the
clearest case in the register of a lane searching for a name rather than reading the schema.

**The real defect underneath, part 1 — the upsert clobbers it.**
`RpgStore.AtomInstances.cs:111-113`:

```sql
ON CONFLICT(instance_id) DO UPDATE SET
  container_id = excluded.container_id, roll_seed = excluded.roll_seed,
  catalog_revision = excluded.catalog_revision, origin = excluded.origin;
```

Re-saving an instance overwrites its revision. I6's mutation model re-saves the instance after every
operation, so under that model the origin revision **would** be destroyed on the first enhancement.
The lane's conclusion is right for a reason it did not identify. Untested.

**The real defect underneath, part 2 — one import bricks every owned instance, and it is blessed.**
`RpgStore.AtomInstances.cs:288-295`:

```csharp
// An instance rolled against an older catalog no longer means what it meant. Reproducing
// it would need the catalog it was rolled against, which we do not keep.
if (instance.CatalogRevision != current)
{
    refused.Add(new BindRefusal(binding.BindingId, AtomRejectionReason.StaleInstance, ...));
    continue;
}
```

Equality, not `>=`, not a compatibility window. **Any** catalog bump refuses **every** pre-existing
binding. And there is a green test that says so:
`tests/FusionRpg.Data.Tests/BindResolutionTests.cs:178-191`,
`A_binding_rolled_against_an_older_catalog_is_refused_as_stale` — bind, `BumpCatalogRevision()`,
`Assert.Empty(resolved.Bindings)`.

Read against the item program this means: **the first content patch after launch unequips every item
every player owns.** The comment is honest that the catalog is not archived; nothing in the item
lanes accounts for the consequence.

**Severity: HIGH** for the underlying defect (blocking for any live-service item economy); the
claim as written is **REFUTED**.

**Fix size: S** for making the upsert preserve `catalog_revision`; **L** for the revision-compatibility
question (archive the catalog, or define a compatibility window, or re-key instances at import).
Owner modules: **E6** for the column, **E8** (content hash / import) for the revision policy. This is
squarely question **D2** ("what reproducibility can honestly be promised without catalog archiving") —
D2 should be told that the column exists and that the equality check is the actual battleground.

---

### Claim 6 — no column can hold a modified value spec

> `effect_instance_atom` has no column able to hold a modified value spec, so an `OnApply` affix can
> never be enhanced or rerolled.

**Verdict: REFUTED.**

**Evidence.** `src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:66-73`:

```sql
CREATE TABLE IF NOT EXISTS effect_instance_atom (
  instance_id TEXT NOT NULL,
  seq INTEGER NOT NULL,
  atom_id TEXT NOT NULL,
  values_json TEXT NOT NULL DEFAULT '{}',
  power_json TEXT,
  PRIMARY KEY (instance_id, seq)
);
```

`values_json` is a free TEXT column and it **already holds unresolved value specs**.
`src/FusionRpg.Core/Effects/Atoms/Instantiator.cs:201-207`:

```csharp
frozen[key] = spec.Roll switch
{
    RollPolicy.OnInstantiate => spec.Resolve(rng),
    RollPolicy.Fixed => spec.Min,
    // Left as authored: an OnApply range belongs to the hit, not the item.
    _ => raw,
};
```

For `OnApply` params the raw JSON object — the whole spec, bounds and all — is serialised straight
into `values_json`. Proven by
`tests/FusionRpg.Core.Tests/Atoms/InstantiatorTests.cs:130-142`,
`An_OnApply_value_is_left_unresolved_because_it_belongs_to_the_hit`, which parses `ValuesJson` and
asserts `amount` is a `JsonValueKind.Object` with `min: 100, max: 200`.

An enhancement that widens that range to `120–240` writes `{"amount":{"min":120,"max":240,"roll":"on_apply"}}`
into the same column. Mechanically nothing prevents it. The claim's premise — that the schema cannot
represent the mutation — is false.

**What is genuinely missing** (stated so the R2/R3 lanes are not misled by the refutation):

1. **Provenance.** Nothing distinguishes "authored spec, untouched" from "spec after three
   enhancements". `values_json` is one blob with no origin copy and no op log. I6 asks for
   `origin_values_json` and `overrides_json` (`ssot-enhancement.md:223, :733`) precisely for this,
   and that is a legitimate *addition*, not a repair of an impossibility.
2. **Nothing reads `values_json` at all.** Zero production consumers (§0). Whatever shape the
   mutation takes, no runtime currently honours the frozen values in either their original or their
   mutated form.

**Severity: LOW** as a defect (it is a design want, correctly scoped as new work); the **claim** is
refuted and should not be carried into the fix wave as a bug.

**Fix size: S–M** for the provenance columns. Owner module: **E6**, adopted by I6.

---

### Claim 7 — a disabled atom is drawable, then bind-rejected

> `Instantiator.Draw` filters only on `Weight > 0` and `ContainerValidator` never reads
> `AtomRow.Enabled`, so a disabled atom is drawable and then bind-rejected `StaleInstance`.

**Verdict: CONFIRMED, exactly as described.**

**Evidence.**

- `src/FusionRpg.Core/Effects/Atoms/Instantiator.cs:134-137` — the draw candidate list is
  `container.Pool.Where(p => p.Weight > 0)`. `Enabled` is never consulted; `lookupAtom` is called
  only to compute the group key.
- `src/FusionRpg.Core/Effects/Atoms/ContainerValidator.cs` — the string `Enabled` **does not occur
  in the file** (grep count: 0). The pool loop at `:73-98` checks weight sign, atom existence,
  core/pool collision, and the tier window. Not enabled-ness.
- `src/FusionRpg.Core/Effects/Atoms/AtomRow.cs:58` — `public bool Enabled { get; init; } = true;`
  the column exists and defaults true.
- `src/FusionRpg.Core/Effects/Atoms/BindGate.cs:57-58` — the *bind* path does check:
  `if (!atom.Enabled) return AtomRejection.Fail(AtomRejectionReason.StaleInstance, $"{atom.AtomId} is disabled");`

So the sequence is: content is disabled (per `definitions.md:228`, content is **disabled, never
deleted**) → a validated container still lists it in a drawable pool → an instance is minted
containing it → the instance can never bind. The player owns an item that refuses to equip, with the
reason code `StaleInstance` — which is honest but describes the wrong thing, since the instance is
brand new.

`definitions.md:227` covers the adjacent case correctly (*"Atom disabled beneath a live instance →
the instance keeps its frozen values; new binds reject"*). What it does not cover is disabling an
atom that is still **in a live pool**, which is the case here.

**Existing coverage.** `BindGateTests.cs:186-193` covers the bind-side rejection. No test covers
`Instantiator.Draw` or `ContainerValidator.Validate` against a disabled atom.

**Minimal repro (described).** Validate and instantiate a container whose only pool row references an
`AtomRow` with `Enabled = false` and `Weight = 10`. `ContainerValidator.Validate` returns `Ok`;
`Instantiator.TryInstantiate` returns `Ok` with that atom in the instance;
`BindGate.Check` on the result returns `StaleInstance`.

**Severity: MEDIUM** — latent, and self-inflicted only through a content action (disabling a family
that is still pooled), which is a routine live-service action.

**Fix size: S.** Two lines: exclude `!Enabled` rows from the drawable set in `Instantiator.Draw`, and
make `ContainerValidator` count only enabled rows toward the drawable-group total so
`PoolRollsExceedGroups` still fires honestly. Owner module: **E5**
(`spec-container-schema.md`, which owns the validator) with an **E6** touch.

---

### Claim 8 — the effect-list sort key is not total

> Two bindings of the same container on one owner produce identical
> `(priority DESC, container_id ASC, seq ASC)` sort keys, but definitions.md §5 requires that order
> to be TOTAL because RNG-stream consumption depends on it. Check whether the comparer is
> implemented at all.

**Verdict: PARTIALLY-CONFIRMED — and the real defect is different from, and worse than, the one
claimed.**

**Is the comparer implemented?** Yes, in SQL, not in C#.
`src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:237-244`:

```sql
SELECT b.binding_id, b.instance_id, b.owner_kind, b.owner_key, b.slot,
       b.priority, b.source, b.bound_utc, b.revision
FROM effect_binding b
JOIN effect_instance i ON i.instance_id = b.instance_id
WHERE b.owner_kind = $kind AND b.owner_key = $key
ORDER BY b.priority DESC, i.container_id ASC, b.instance_id ASC;
```

**The third key is `b.instance_id`, not `seq`.** And `instance_id` is a generated GUID —
`:99`, `Guid.NewGuid().ToString("N")`, in a method whose own doc comment at `:93-96` says the id is
generated *"and it is excluded from the reproducibility comparison precisely because it is."*

This is the exact defect `definitions.md:182` rejects, one identifier over:

> An earlier draft used `binding_id` as the tiebreak. That is wrong: `binding_id` is *generated*, so
> two runs of the same container against the same `roll_seed` produce different ids, sort
> differently, and consume the `atom.apply` stream in a different order — different trace bytes from
> identical inputs. **The tiebreak must be content-derived.**

`instance_id` is generated by the identical mechanism, in the identical store, for the identical
reason. The order is **total** (GUIDs collide only within one instance bound twice) but it is **not
reproducible**: two runs of the same content produce different orders. Totality was never the
problem; content-derivation was, and the fix that addressed `binding_id` did not address
`instance_id`.

Two supporting observations:

- **The test believes otherwise.** `tests/FusionRpg.Data.Tests/AtomInstanceStoreTests.cs:182-202`,
  `Ties_break_on_content_not_on_the_generated_binding_id`, binds two instances of **different**
  containers (`trait.stalwart`, `item.ember-band`) and asserts they order by container. The tie is
  broken at key two; key three is never exercised. The class header at `:10-13` states the guarantee
  the query does not deliver.
- **definitions.md already knows.** `:545` — *"Still open: the injector's per-match seed (E19 pushes
  it) and the ordinal comparer (§5). Both are testable the same way — run the suite before claiming
  either one moves anything."* This claim is a rediscovery of a logged open item.

**On the claim's `seq ASC`:** `seq` is a column of `effect_instance_atom`, not of `effect_binding`,
so it cannot appear in this ORDER BY — the *bindings* are ordered here and the *atoms within an
instance* are ordered by `seq` separately (PK `(instance_id, seq)`, `:72`). The claim conflated two
levels of the sort. The two-level order is nonetheless correct in intent.

**One further live instance of the same family of bug**, not claimed by any lane and worth recording:
`src/FusionRpg.Core/Effects/EffectBag.cs:84` —
`.OrderByDescending(g => g.Priority).ThenBy(g => g.GrantId)` with **no comparer**, so
`Comparer<string>.Default`, so **culture-sensitive**. `definitions.md:184` and `:539` call this out by
name. This one is on a live path.

**Severity: HIGH** — determinism is the atom program's central promise, and the item program's loot
reproducibility rests on it. Latent for instances, **live** for `EffectBag`.

**Fix size: M.** Replace the `instance_id` tiebreak with a content-derived key — the candidates are
`(container_id, roll_seed)` or a content hash of the instance; note that two byte-identical instances
of one container on one owner are *supposed* to be interchangeable, so a stable arbitrary order over
equal content is acceptable and a **generated** one is not. Add `StringComparer.Ordinal` at
`EffectBag.cs:84`. Owner module: **E6**, with an E7/runner check that nothing downstream depends on
the current order. Run the suite before claiming this moves goldens — definitions.md `:546` says so
explicitly, and the last time that assumption was made untested it cost a decision the owner never
needed to make (`:541-543`).

---

### Claim 9 — the `rarity` table has no production readers

> The `rarity` table has zero production readers — `ListRarities()` callers are only tests.

**Verdict: CONFIRMED for `ListRarities`. One indirect production consumer exists and should be
recorded.**

**Evidence.** Full-tree grep for `ListRarities`, `UpsertRarity`, and `RarityRow`:

| Symbol | Definition | Callers |
|---|---|---|
| `RarityRow` | `src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:93` | — |
| `UpsertRarity` | `src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs:68` | `tests/FusionRpg.Data.Tests/ContainerStoreTests.cs:190-221`, `tests/FusionRpg.Data.Tests/ContentHashStoreTests.cs:82,184,187,201,224` |
| `ListRarities` | `src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs:105` | `ContainerStoreTests.cs:194,215,224` |

**Tests only.** Nothing in `src/` outside the store's own file names any of the three.

**The indirect consumer.** The table *is* covered by the content hash —
`src/FusionRpg.Core/Effects/Atoms/ContentHashRegistry.cs:97-99` registers a `ContentHashTable("rarity", ...)`,
and `definitions.md:335` lists `rarity` among the covered tables. So an edit to the table moves the
content hash and can fail an import cross-check. That is a real consumer, but it consumes the
table's *bytes*, not its *meaning*: nothing reads `pool_rolls`, `min_tier`, or `max_tier` off a
rarity row to make a decision.

The three columns that carry the mechanism — `pool_rolls`, `min_tier`, `max_tier` — are written,
hashed, and never read. That is the enrichment contract's **SC7** failure verbatim (*"a row no code
consumes is not content; it is a lie in a table"*), and the header comment above the DDL
(`RpgStore.Containers.cs:50-51`) claims the ordinals are *"load-bearing for sorting and for the budget
lookup"* — there is no sort and no budget lookup.

**Severity: MEDIUM** — no misbehaviour, but I1 (rarity) is designing a long overlapping ladder on top
of a table nothing consults, and the contract forbids exactly that.

**Fix size: S** for the store side (nothing to change); the work is **content and a consumer** —
whoever makes `Instantiator` look up `pool_rolls` / tier window by the container's rarity. Owner
module: **E5**. Directly feeds **D3** (cost and rarity rebase) and its *"two `pool_rolls` sources of
truth"* question — confirmed here: `effect_container.pool_rolls`
(`RpgStore.Containers.cs:27`) and `rarity.pool_rolls` (`:55`) both exist, and only the first is read.

---

### Claim 10 — `min_tier`/`max_tier` are authoring assertions, not runtime filters

> `effect_container.min_tier`/`max_tier` are authoring assertions, not runtime filters:
> `ContainerValidator` rejects the whole container, and `Instantiator.Draw` never consults the window.

**Verdict: CONFIRMED.**

**Evidence.**

- `src/FusionRpg.Core/Effects/Atoms/ContainerValidator.cs:88-93` — a pool row whose atom falls
  outside the window returns `Fail(TierOutOfWindow, ...)`, and `Fail` is a **return**, so the whole
  container is refused. The comment at `:87` states the intent: *"The window governs what the POOL
  may offer; a fixed core says what the thing is."*
- `src/FusionRpg.Core/Effects/Atoms/Instantiator.cs:125-161` — `Draw` never mentions `MinTier` or
  `MaxTier` (grep: absent from the file). Its candidate set is `Weight > 0`, full stop.

So the window is a **load-time assertion on the author**, not a runtime narrowing. It cannot express
"this container has a wide pool and rarity selects the slice", which is what OD4 (overlapping rarity
ladder) and I8 (affix tier bands) both assume.

`spec-container-schema.md:25` describes it as *"the tier window the pool may offer — the mechanism
rarity previously only claimed"*, and `definitions.md:146-147` agrees. The code implements the spec.
**This is not a bug; it is a capability the item lanes assumed and the spec never promised.** Recorded
here because two lanes reasoned as though the window filtered.

**Existing coverage.** `TierOutOfWindow` is a registered reason code
(`src/FusionRpg.Core/Effects/Atoms/AtomRejection.cs:101`) and the validator path is exercised by the
container tests. No test asserts that `Draw` ignores the window, because it is not expected to honour it.

**Severity: LOW as a defect / MEDIUM as a design gap.**

**Fix size: M** if the window is to become a runtime filter — note that a filtering window interacts
with `PoolRollsExceedGroups`, since the drawable-group count would become rarity-dependent and could
no longer be validated once at load. Owner module: **E5**. Input to **D3**.

---

### Claim 11 — container `rarity` is unvalidated free text, and ordinals can still move

> `effect_container.rarity` is free TEXT with no FK and `ContainerValidator` never validates it; and
> "append-only ordinals" is not enforced — `RpgStore.Containers.cs:79-82` blocks a collision but
> `ON CONFLICT ... SET ordinal = excluded.ordinal` can still move an existing rung.

**Verdict: CONFIRMED, both halves.**

**Half one — no FK, no validation.**

- `src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs:23` — `rarity TEXT,` inside
  `CREATE TABLE effect_container`. No `REFERENCES rarity(rarity_id)`, and no foreign-key clause
  anywhere in the DDL block (`:18-59`).
- `src/FusionRpg.Core/Effects/Atoms/ContainerValidator.cs` — the string `Rarity` does not occur
  (grep: 0). The validator checks id grammar, kind prefix, tier-window inversion, seq duplicates,
  atom existence, override schema, weights, and group counts. Never rarity.
- The value is written through blind: `RpgStore.Containers.cs:154`,
  `("$rarity", (object?)c.Rarity ?? DBNull.Value)`.

So `rarity = 'legendry'` (typo) is accepted, stored, and silently means nothing.

**The spec says otherwise.** `spec-container-schema.md:24` — *"`rarity` | TEXT | nullable; **FK to a
`rarity` table** with explicit append-only ordinals."* There is no FK. This is a spec/code
divergence, and per DESIGN-GATE §3 code beats docs — the spec line is the one that is wrong.

**Half two — the ordinal can move.** `RpgStore.Containers.cs:77-92`:

```csharp
check.CommandText = "SELECT rarity_id FROM rarity WHERE ordinal = $o AND rarity_id <> $id;";
...
if (check.ExecuteScalar() is string taken)
    return (false, $"ordinal {r.Ordinal} already belongs to '{taken}' — ordinals are append-only");
...
ON CONFLICT(rarity_id) DO UPDATE SET
  ordinal = excluded.ordinal, pool_rolls = excluded.pool_rolls, ...
```

The pre-check refuses only an ordinal **already taken by a different id**. Re-upserting an existing
band with a *free* ordinal passes the check and the `DO UPDATE` moves it. `UpsertRarity(("common", 1, …))`
then `UpsertRarity(("common", 7, …))` returns `(true, "")` and `common` is now rung 7 — renumbered
underneath every container that names it, which is precisely what the comment at `:65-67` and the DDL
comment at `:50-51` say must never happen.

**Existing coverage — the gap is exact.**

- `tests/FusionRpg.Data.Tests/ContainerStoreTests.cs:197-207`,
  `An_ordinal_already_belonging_to_another_band_is_refused` — covers the collision case the
  pre-check handles.
- `:209-216`, `A_band_may_still_be_retuned_in_place` — re-upserts `common` with the **same**
  ordinal 1 and a changed `pool_rolls`. Deliberately not a move.

The one case between them — same id, different free ordinal — is tested nowhere.

**Minimal repro (described).** Two `UpsertRarity` calls as above; assert
`ListRarities().Single().Ordinal == 1` and watch it be 7.

**Severity: MEDIUM** — an unguarded content operation that silently re-prices every container naming
the band. The append-only rule is stated in three comments and enforced in one direction out of two.

**Fix size: S.** Refuse any upsert whose `rarity_id` already exists with a different `ordinal`; drop
`ordinal` from the `DO UPDATE` list. Add the FK (or an `UnknownRarity` validation in
`ContainerValidator`, which I1 proposes as a reason code — see §3). Owner module: **E5**.

---

### Claim 12 — the family library totals 70, not 71

> `atom-family-library.md` claims 71 authored families; counting every row in its §3.1–3.5 tables
> yields 70, the phantom being `charm_pulse`.

**Verdict: CONFIRMED. The count is 70.**

I counted every row by hand. Full working, so it can be checked:

| Section | Header claims | Families actually listed | Names |
|---|---|---|---|
| §3.1 `stat.modify` (`:79-96`) | 14 | **14** ✓ | vitality, fortitude, bulwark, might, ferocity, savagery, warding, resilience, plating, carapace, mending, quickening, flourishing, swiftness |
| §3.2 `stat.derived` table (`:98-123`) | 12 | **12** ✓ | elemental_power, elemental_defense, precision, evasion, keen_edge, cruelty, stoicism, padding, shield_capacity, shield_toughness, shield_pen, shield_regen |
| §3.2 status-channel prose (`:125`) | 4 | **4** ✓ | affliction, stalwart, immunity, susceptibility |
| §3.3 `resource.delta` (`:127-136`) | 6 | **6** ✓ | searing_strike, lifesteal, retribution, deathblast, regeneration, martyrdom |
| §3.4 `status.apply` (`:138-151`) | **21** | **20** ✗ | buttering, freezing, chilling, venomous, mesmerizing, withering, bloodletting, blighting, rotting, sparking, marking, sporing, embering, scalding, entangling, rallying, exposing, commanding, shattering, bonding |
| §3.5 board and economy (`:155-167`) | 14 | **14** ✓ | cleansing, warded, summoner, gardener, volley, cherry_bloom, dooming, firelining, flash_freeze, gravemaking, gravedigging, terraforming, sunbloom, midas |
| | | **= 70** | |

`atom-family-library.md:264` records **71** in its §6 count table, annotated *"(§3.1–3.5 tables,
counted)"*. `:17` and `:270` repeat ~71.

**The discrepancy is in §3.4 and the lane's diagnosis is right.** The header at `:138` says "21
families (all functional statuses after payload completion)" — that 21 is the count of **catalog
statuses**, not of families. `:140` explains why they differ, in the same document:

> `charm_pulse` has no vanilla method and is a def error to correct, not a branch to write … so all
> 21 catalog statuses become authorable rather than 13.

`charm_pulse` is the 21st status and it gets **no family row**, correctly, because the doc's own
decision is that it is a data error rather than a mechanic. The section header carried the status
count into the family count and §6 summed the headers.

**A second, smaller overcount worth flagging while the doc is open:** `susceptibility`
(`status.expose.*`) is counted among the 4 status-channel families at `:125`, and the same sentence
says it is *"declared with zero readers today, so **not authored at all** until it has a consumer."*
By the doc's own rule the **authorable** total is **69**, with 70 as the designed-and-parked total.

**Severity: LOW** — a documentation arithmetic error, no code impact. It matters only because **D4**
is chartered to total the authoring load, and it will start from this number.

**Fix size: S.** One doc edit: `:264` 71 → 70 (with a footnote for the 69), and `:17`/`:270` to match;
optionally retitle §3.4 to *"20 families covering 21 catalog statuses"*. Owner: the effect-atom
program's doc set, **not** an E-numbered module.

---

### Claim 13 — `CurveInput.Rarity` contradicts the container-schema boundary

> `CurveInput.Rarity` exists and definitions.md §2 legitimises a rarity curve input, while
> spec-container-schema.md's Boundaries forbid rarity changing an atom's magnitude. A genuine
> contradiction.

**Verdict: CONFIRMED. A genuine, three-way contradiction between shipped code and two specs.**

**Side A — code and definitions permit it.**

- `src/FusionRpg.Core/Effects/Atoms/CurveTable.cs:4-9`:
  ```csharp
  public enum CurveInput { Level = 0, Rarity, Tier }
  ```
  with the doc comment *"Adding one is a reviewed change (E2 boundaries)"* — so `Rarity` was
  reviewed in.
- `definitions.md:100-105`, the curve-input table: *"`rarity` | the container's rarity **ordinal**
  (§4) | container has no rarity → rejected at bind"*. It is not merely permitted; a rejection rule
  was written for its failure case.
- `definitions.md:98` establishes what a curve *does*: *"the curve scales `Min` and `Max` **before**
  the roll."* A curve keyed on rarity therefore scales magnitude by rarity, by construction.

**Side B — the container schema forbids it.** `spec-container-schema.md:145`, under **Boundaries**:

> **Never:** … let rarity change an atom's magnitude — **rarity picks count and tier, tier carries
> strength.**

Restated twice more in the same file, at `:62` (*"Rarity (on the container) selects the `pool_rolls`
count and the `min_tier`/`max_tier` window"*) and `:98` (*"rarity governs count and tier-window rather
than magnitude"*).

These cannot both hold. `CurveInput.Rarity` is the mechanism by which rarity changes magnitude, and
it is in shipped, registered, reviewed code with a spec table entry.

**Which one wins by the tree's own rules?** `enrichment-contract.md` §5 ranks
`definitions.md` as *"wins over any spec"*, which puts definitions §2 above
spec-container-schema's Boundaries and makes `CurveInput.Rarity` legal. But DESIGN-GATE's *"read the
section, not the line"* rule cuts the other way: the Boundaries section is where E5's ask-first rules
live, and a boundary is not a claim a sibling doc can quietly overrule. **This needs a decision, not
a precedence lookup.**

**Existing coverage.** `CurveInput.Rarity` is a live enum member; whether any curve row uses it is a
content question with no content today. No test asserts the boundary.

**Severity: MEDIUM** — no runtime misbehaviour yet, because no rarity curve exists. It becomes a
balance-model fork the moment I1 or I8 authors one, and the two would produce different games.

**Fix size: S** either way once decided (delete the enum member and the definitions row, **or** delete
the Boundaries line and its two restatements). Owner modules: **E2**
(`spec-value-spec-and-curve.md`) and **E5**. This is explicitly listed as part of question **D3**;
D3 should be handed this section rather than re-deriving it.

---

### Claim 14 — the reason-code count assertion

> `AtomKindRegistryTests` reportedly asserts an exact reason-code enum count (claimed 34). Find the
> assertion and record the real number.

**Verdict: CONFIRMED. The assertion exists, the literal is 34, and 34 means 33 codes plus `None`.**

**The assertion**, `tests/FusionRpg.Core.Tests/Atoms/AtomKindRegistryTests.cs:26-35`:

```csharp
[Fact]
public void Rejection_reasons_are_the_closed_list_of_thirty_three()
{
    // definitions.md §10 fixes the list at 33 (plus None). It is the operator-facing error
    // surface: a code added without review is a code no runbook explains.
    var reasons = Enum.GetValues<AtomRejectionReason>();

    Assert.Equal(34, reasons.Length);
    Assert.Contains(AtomRejectionReason.None, reasons);
}
```

**The budget, stated unambiguously for the five lanes that need it:**

| | Count |
|---|---|
| Real reason codes today | **33** |
| Plus the `None` sentinel (`AtomRejection.cs:9`, `None = 0`) | 1 |
| **Enum length — the number in the assertion** | **34** |

I verified both sides independently rather than trusting either:

- `src/FusionRpg.Core/Effects/Atoms/AtomRejection.cs:7-116` — enumerated the members: `None` plus 33
  named codes.
- `definitions.md:359` — the §10 list, split on `·`: **33** entries, matching the enum exactly.

**Consequence for a lane adding codes.** Adding *n* codes means editing `AtomRejection.cs`, changing
the literal `34` at `AtomKindRegistryTests.cs:33` to `34 + n`, renaming the test method (it spells
"thirty_three" in the name), and amending `definitions.md:359`. `ssot-sockets.md:457` already worked
this out correctly and cites the same line. Note also `AtomRejectionReason` values are **implicitly
numbered** — new members must be appended, not inserted, or every persisted integer shifts.

**Severity: not a defect.** Recorded as the budget baseline.

**Fix size: n/a.** Owner module: **E1** (`spec-atom-kind-registry.md`), which owns the closed list.

---

## 3. Reason-code budget — what the lanes collectively proposed

Every one of the thirteen `ssot-*.md` reason-code tables was enumerated row by row and each proposed
name checked against the 33 codes in `AtomRejection.cs`. Codes a lane explicitly **reuses** from the
existing 33 are excluded, as are authoring **lints** that lanes declared are not rejections, and as
are the dotted-lowercase player-facing strings I9 and I7 keep on a deliberately separate surface
(`ssot-materials-crafting.md:425-427`).

### 3.1 Per lane

| Lane | Doc | New | Codes | Anchor | Fold variant |
|---|---|---|---|---|---|
| I8 affixes | `ssot-affixes.md` | **2** | `AffixNotLegalHere`, `AffixClassRollsMismatch` | `:883`, table `:890-891` | none (6 lints declared not codes, `:893-905`) |
| I10 charms | `ssot-charms.md` | **5** | `CharmBudgetExceeded`, `CharmAxisOverflow`, `CharmInUse`, `CharmNotCarryable`, `CharmAtomNotPermitted` | `:430`, table `:436-440` | **yes → 3** (`:442-445`) |
| I6 enhancement | `ssot-enhancement.md` | **7** | `EnhanceCapReached`, `EnhanceNotSupported`, `OddsNotAcknowledged`, `OpSequenceGap`, `ReplayDivergence`, `OriginRevisionUnavailable`, `TransferRoleMismatch` | `:332-334`, table `:340-346` | none; argues 3 are shared mutation-model codes (`:334-336`) |
| I2 equip slots | `ssot-equip-slots.md` | **5** | `RoleUnknown`, `FrameMismatch`, `RoleFamilyIllegal`, `SlotLocked`, `SlotOccupied` | table `:573-578`, `:586` | none; `RoleHasNoFamilies` drafted then dropped for `UnsatisfiablePool` (`:591-592`) |
| I12 generation | `ssot-generation.md` | **8** | `UnknownDropTable`, `UnknownBaseTypeSet`, `UnknownCurrency`, `DropTableDepthExceeded`, `DropTableCycle`, `StandaloneRuleViolation`, `RarityUnsatisfiable`, `LootReplayMismatch` | table `:659-679`, `:684` | folding already applied (`:684-688`) |
| I13 inventory | `ssot-inventory.md` | **14** | `ItemNotOwned`, `ItemLocked`, `ItemAssigned`, `RoleOccupied`, `RoleNotOnFrame`, `FrameMismatch`, `SpecimenNotIdle`, `SpecimenRetired`, `StockDepleted`, `LoadoutConflict`, `SalvageWindowExpired`, `SalvageUndoInsufficientMaterials`, `InventoryCeiling`, `CharmPouchFull` | table `:565-578` | **yes → 12** (`:580-582`), lane recommends against |
| I3 item categories | `ssot-item-categories.md` | **6** | `BaseStatInPool`, `ImplicitCountExceeded`, `BaseStatOutOfBudget`, `BaseStatRoleForbidden`, `BandTierMismatch`, `CategoryHasNoConsumer` | `:457`, table `:461-466` | none (2 declared lints, `:471-475`) |
| I9 materials | `ssot-materials-crafting.md` | **5** | `UnknownMaterial`, `UnknownRecipe`, `UnknownOperation`, `CostClassForbidden`, `UnresolvedVariant` | table `:434-438`, `:448` | none |
| I1 rarity | `ssot-rarity.md` | **5** | owns `UnknownRarity`, `RarityBandViolated`, `RarityLadderMutated`; **proposes to I6** `RarityDemotion`, `RarityCeilingExceeded` | table `:436-441`, `:451`, `:454-455` | none |
| I11 requirements | `ssot-requirements.md` | **2 named / 3 asked** | `FrameMismatch`, `FactionMismatch` — *the third is never named* | `:159`, `:621`, `:623`, `:793` | none; reuses `ScopeUnsupported` at `:176` |
| I7 reroll | `ssot-reroll.md` | **2** | `NotRerollable`, `RerollLocked` | `:422`, `:431-432` | none |
| I5 sets | `ssot-sets.md` | **6** | `SetThresholdUnreachable`, `SetRoleCollision`, `SetRoleNotUniversal`, `SetRoleForbidden`, `SetTierForbiddenAtom`, `SetCapabilityMisplaced` | `:511-513`, table `:518-523` | none; explicitly rejects folding (`:513-514`) |
| I4 sockets | `ssot-sockets.md` | **3** | `NotSocketable`, `NoFreeSocket`, `SocketOccupied` | `:491`, table `:499-501` | none (`:495`) |

Three per-lane discrepancies worth recording, all verified against the source:

- **I13's own header undercounts itself.** `ssot-inventory.md:560` says *"Twelve"*; the table at
  `:565-578` has **fourteen** rows and the lane corrects itself two lines later at `:580` (*"That is
  fourteen"*) and again at `:782`. The table is authoritative.
- **I11 asks for three codes and names two.** `:159` and `:793` both say three; a full sweep of the
  file yields only `FrameMismatch` and `FactionMismatch`. The third is presumably the
  attribute-shortfall code parallel to `LevelTooLow`, but it does not exist as a token anywhere in
  the document. Also, the `§7.1` back-reference at `:793` points at a worked attribute sheet, not a
  code table.
- **`InsufficientMaterials` is proposed by nobody and assumed by two.** I6 names it at `:348` and
  `:711` and explicitly disclaims it (*"as **your** reason code … yours, not a new reason code from
  me"*), handing it to I9. I9 never mints it — its equivalent is the dotted-lowercase
  `materials.insufficient` (`ssot-materials-crafting.md:458`) on the separate player surface. **The
  code has an assumed owner and no actual one.** Excluded from the counts below.

### 3.2 Duplicates

**Exactly one exact-name collision across all thirteen lanes:**

| Code | Lanes | Lines |
|---|---|---|
| `FrameMismatch` | **I2 equip slots**, **I13 inventory**, **I11 requirements** — three lanes | `ssot-equip-slots.md:574` · `ssot-inventory.md:570` · `ssot-requirements.md:621` |

That the collision count is *one* is a genuine success of the contract's §4 boundary cuts. The three
lanes are also already mid-negotiation over it in prose — `ssot-equip-slots.md:783` says I11 should
register it, `ssot-inventory.md:728` says all frame/role/level checks must evaluate in one place, and
`ssot-requirements.md:748` says *"I11 assumes yes and would reject with `FrameMismatch`; I2 should
confirm that code choice."* All three intend one code; all three list it as their own new proposal.

**Near-duplicates — different names, one failure.** Not counted as duplicates, but these are the
merge candidates a reviewer should look at first:

| Concept | Competing names |
|---|---|
| the target slot/role already holds an item | `SlotOccupied` (`equip-slots:578`) · `RoleOccupied` (`inventory:568`) · `SocketOccupied` (`sockets:501`) |
| this frame does not have that role | `RoleNotOnFrame` (`inventory:569`) · `SlotLocked` (`equip-slots:576`, the `present = 0` row) |
| you cannot afford this | `SalvageUndoInsufficientMaterials` (`inventory:576`) · `InsufficientMaterials` (`enhancement:348`) · `materials.insufficient` (`materials:458`) — three spellings, three lanes, one concept |
| naming order | `RoleUnknown` (`equip-slots:573`) inverts the `Unknown*` prefix every other lane used |

### 3.3 The totals, and what they do to claim 14's budget

| | Count |
|---|---|
| Existing real codes | **33** |
| Current enum length — the literal at `AtomKindRegistryTests.cs:33` | **34** |
| Lanes proposing new codes | **13 of 13** |
| **Raw total** — per-lane proposals summed, duplicates counted (2+5+7+5+8+14+6+5+5+2+2+6+3) | **70** |
| **Distinct total** — after deduplicating `FrameMismatch` across three lanes | **68** |
| **Closed list if every proposal is accepted** | **33 → 101** |
| **Enum length, i.e. the new literal at `AtomKindRegistryTests.cs:33`** | **34 → 102** |

Sensitivity, so the owner can price the variants:

| Variant | Raw | Distinct | Enum length |
|---|---|---|---|
| As proposed | 70 | 68 | 102 |
| Counting I11's unnamed third code | 71 | 69 | 103 |
| …and counting `InsufficientMaterials` as real | 72 | 70 | 104 |
| Both stated folds accepted (charms 5→3, inventory 14→12) | 66 | 65 | 99 |

**The lanes have collectively proposed to triple the operator-facing error surface** — 33 codes
becoming 101 — and none of them saw another lane's table while doing it.

**Three lanes flagged it themselves**, unprompted, which is worth crediting:
`ssot-inventory.md:782` (*"Fourteen new reason codes is a lot against a closed list of 33"*),
`ssot-charms.md:726` (*"Five new reason codes against a closed 33 … the fold-to-three variant is
written out"*), and `ssot-requirements.md:730-732`, which correctly notes it *"cannot add reason
codes to a closed 33-item list on its own authority."* Two lanes (`ssot-reroll.md:425-427`,
`ssot-sockets.md:493-494`) even cite `AtomKindRegistryTests.cs:33` by line as the test their addition
would move — they did the homework claim 14 was asked to do.

**One observation for R4, since this document does not design:** `definitions.md` §10 calls adding
**one** code a reviewed change. Sixty-eight arriving in parallel is the exact condition **SC6** was
written to prevent from becoming a rubber stamp, and the near-duplicate table above suggests the
folded number is materially smaller than 68.

---

## 4. Summary, ordered by severity

| # | Claim | Verdict | Live? | Severity | Fix | Owner |
|---|---|---|---|---|---|---|
| 4 | `player:` scope buffs both sides | **CONFIRMED** | **LIVE** | **HIGH** | S–M | Core/Stats + E6 |
| 5 | origin catalog revision | **REFUTED**; real defect = any bump refuses every binding | latent | **HIGH** | S / L | E6 + E8 (**D2**) |
| 8 | effect-list tiebreak | **PARTIALLY-CONFIRMED**; tiebreak is a generated GUID | latent (+1 live in `EffectBag`) | **HIGH** | M | E6 |
| 1 | orphan sweep deletes unequipped items | **CONFIRMED** | latent | **HIGH** | M | E6 (**D1**) |
| 2 | no durable per-specimen scope | **CONFIRMED** | latent | **HIGH** | L | E6 (**D1**) |
| 3 | `level_req` fails open — *and is never enforced at all* | **CONFIRMED**, worse | latent | **MEDIUM** | S + S | E6 |
| 11 | container rarity unvalidated; ordinals movable | **CONFIRMED** ×2 | latent | **MEDIUM** | S | E5 |
| 7 | disabled atom drawable, then bind-rejected | **CONFIRMED** | latent | **MEDIUM** | S | E5 (+E6) |
| 9 | `rarity` table has no production reader | **CONFIRMED** (content-hash aside) | latent | **MEDIUM** | S + content | E5 (**D3**) |
| 13 | `CurveInput.Rarity` vs the E5 boundary | **CONFIRMED** | latent | **MEDIUM** | S | E2 + E5 (**D3**) |
| 10 | tier window is authoring-only | **CONFIRMED** (works as specified) | latent | **LOW** / design gap | M | E5 (**D3**) |
| 6 | no column for a modified value spec | **REFUTED** — `values_json` holds one today | latent | **LOW** | S–M (additive) | E6 |
| 12 | 71 families vs 70 | **CONFIRMED** — it is 70 (69 authorable) | doc only | **LOW** | S | doc set (**D4**) |
| 14 | reason-code assertion | **CONFIRMED** — 33 codes + `None` = enum length 34 | n/a | baseline | n/a | E1 |

**Score: 9 confirmed, 2 refuted, 1 partially confirmed, 2 recorded as baseline/design-gap.**
Nothing came back NEEDS-REPRO — every claim was decidable from source plus the executed suites.

---

## 5. What a fix wave would have to touch, in dependency order

Not a plan and not an authorisation — the reconciliation plan is explicit that the fixes are a
separate build needing the owner's sign-off. This is the dependency shape a plan would have to
respect, so that D1–D4 can price their options.

**Stage 0 — free, no decisions required, no behaviour change.**

1. **Write the four missing tests** that pin current behaviour before anything moves: level_req with
   a null owner level (claim 3), a disabled pooled atom (claim 7), a rarity ordinal move (claim 11),
   and the same-container binding tie (claim 8). Each is under ten lines against an existing fixture.
   Three will fail immediately; that is the point.
2. **Doc corrections with no code impact**: the family count (claim 12), and the
   `spec-container-schema.md:24` "FK to a rarity table" line that describes an FK the schema
   does not have (claim 11).
3. **Resolve the two census loose ends** in §3: I11 must name the third reason code it asks for
   (`ssot-requirements.md:159, :793`), and somebody must own or drop `InsufficientMaterials`, which
   I6 hands to I9 and I9 never takes.

**Stage 1 — blocked on decisions, must be settled before any code moves.**

4. **D1 → claims 1 and 2 and 4.** All three are the same missing concept from different angles:
   there is no durable per-actor owner. The orphan sweep, the absent scope, and the `player:` stub
   cannot be fixed independently without picking incompatible answers three times.
5. **D2 → claim 5.** Whether the equality check at `RpgStore.AtomInstances.cs:290` becomes a window,
   a re-key, or an archive determines whether `catalog_revision` needs preserving on upsert or
   splitting into two columns.
6. **D3 → claims 9, 10, 13.** One decision cluster: does rarity filter the pool at runtime, and may
   it touch magnitude. Answering it settles whether the `rarity` table gets a consumer, whether the
   tier window becomes a filter, and whether `CurveInput.Rarity` lives or dies.

**Stage 2 — mechanical, once stage 1 has answers.**

7. **Claim 8, the tiebreak.** Independent of D1–D3 and the highest-value isolated fix, because every
   determinism guarantee downstream rests on it. Includes the `StringComparer.Ordinal` repair at
   `EffectBag.cs:84`. Run the full suite before claiming it moves goldens — `definitions.md:546`
   instructs exactly this, and `:541-543` records what the last untested assumption here cost.
8. **Claim 7**, two lines in `Instantiator.Draw` and `ContainerValidator`.
9. **Claim 11**, the ordinal guard, plus the rarity FK or an `UnknownRarity` validation — whichever
   D3 implies.
10. **Claim 3**, invert the level_req condition and either wire or delete the dead `ownerLevel`
    parameter at `RpgStore.AtomInstances.cs:264`. Sequenced after D1 because "who is the owner whose
    level this is" is D1's answer.

**Stage 3 — additive, after the seam exists.**

11. **Claim 6**, the provenance columns I6 wants. Genuinely new work, not a repair.
12. **The reason-code fold**, one reviewed pass over the 68 distinct proposals (§3), then a single
    edit to `AtomRejection.cs` (**append only** — the enum is implicitly numbered),
    `AtomKindRegistryTests.cs:33`, that test's name, and `definitions.md:359`.

**The cross-cutting prerequisite (§0).** Nine of these fixes are to a layer with no production
consumer. Whoever wires E6 to a runtime should land **before or alongside** stages 2 and 3, or the
fixes will be verified only by their own unit tests — which is how this layer accumulated four of the
defects above in the first place.

---

## 6. What I could not verify, and why

Stated plainly rather than papered over.

1. **No repro was executed.** The R1 brief allows exactly one output file, so no test file could be
   written. Every "minimal repro" above is traced through the code and cited to `file:line`, but the
   three cheap ones (claims 3, 7, 11) are **described, not run**. They are stage-0 work in §5. I am
   confident in the traces — each is a straight-line read of a single method — but "traced" is not
   "executed" and this register should not pretend otherwise.

2. **Two reason codes in §3 have no clean owner, and that is the lanes' gap, not a measurement
   gap.** All thirteen tables were enumerated row by row, so 70 raw / 68 distinct is exact for what
   is *written*. But I11 asks for three codes and names two (`ssot-requirements.md:159, :793`), and
   `InsufficientMaterials` is disclaimed by I6 and never minted by I9. Both are reported as
   discrepancies rather than guessed at.

3. **The injector was not built.** Only Core, Data and Guard were compiled and run.
   `FUSIONRPG_GAME_DIR` was unset — deliberately, to test whether the suites need it. They do not.
   The injector projects (`FusionRpg.Injector.BepInEx`, `.MelonLoader`, `.MelonLoader.39`) reference
   the game's interop assemblies and were out of scope; claim 4's live path was verified by reading
   `CheatActions.cs:63-66`, not by running the injector.

4. **"Live" is a code-reachability judgement, not an observed one.** Claim 4 is marked live because
   `StatSystem.Resolve` is the hot path and `player:` is an accepted cheat-surface owner key. Whether
   any shipping content actually authors a `player:`-scoped modifier today was not established —
   grep found the key only in tests. If nothing authors one, claim 4 is latent too, but it is
   *reachable* in a way the other nine are not.

5. **Golden-file impact was not measured.** Several fixes (claims 1, 8) are flagged in
   [../decisions.md](../decisions.md) as potentially colliding with golden ordering. I confirmed the
   suites are green **now**; I did not attempt any fix, so I cannot say what moves. Per DESIGN-GATE
   §3.4 that remains an untested constraint on the fix wave's side, and stage 0's tests are the
   cheapest way to find out.

6. **The tree moved while this register was being written.** The suites in §1 ran against `HEAD`
   842907f plus an uncommitted working tree; partway through, the owner committed **c4c9908**
   *("Ship checksums, document the Unblock step, and correct the launcher trust guidance")*, which
   absorbed most of those changes. Seventeen files remain modified, four of them in the atom layer
   (`AtomCompiler.cs`, `ContentHashRegistry.cs`, `ContentHashStamp.cs`, `RunnerEntry.cs`) plus
   `definitions.md` and `spec-content-hash.md`.

   **Every `file:line` citation in this register was re-verified against the post-commit tree** and
   all eleven load-bearing ones still resolve to the exact line quoted. **The suites were re-run**
   rather than assumed — §1 records both runs, and Core moved 2235 → 2257, which is exactly why the
   re-run was worth doing. The boundary guards were also re-run post-commit and are still all four
   green. Nothing in this register rests on a pre-commit measurement.
