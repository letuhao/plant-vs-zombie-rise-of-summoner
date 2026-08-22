# Decision D1 — durable ownership: `actor:{instanceId}` scope, or the assign/bind split?

**Status:** Debate **D1** from [reconciliation-plan.md](reconciliation-plan.md) §R2, settled 2026-08-22.
Bound by [enrichment-contract.md](enrichment-contract.md). **Decides one question and states what it
rejected.** Where this file and a lane doc disagree on *durable ownership only*, this file wins until the
R4 reconciliation folds it in; on everything else the lane docs stand.

**Decision: Option B — the assign/bind split.** `actor:{instanceId}` is **reserved, not added**, under
the four conditions in §8.3. Three amendments to shipped code are mandatory and are named in §9.

---

## 1. The question

Five lanes are blocked on the same sentence. Equipping a specimen is supposed to be a binding, and a
binding needs an owner key, and **no owner key names a specimen durably**:

| Scope | Why it is not a specimen |
|---|---|
| `entity:{ptr}` | contractually session-scoped and never durable (`src/FusionRpg.Core/Effects/Atoms/OwnerScope.cs:38-39`; definitions §6 stale-owner table) |
| `plant:N` / `zombie:N` | type-wide — equipping one Peashooter equips every Peashooter (`OwnerScope.cs:109-116`) |
| `player:N` | the whole account, and in the stat layer it degrades to match-wide (`src/FusionRpg.Core/Stats/StatApplyScope.cs:82-83`) |
| `match` · `sector:` · `slot:` | the run, the world map, a construction slot |

Two fixes are on the table:

- **A** — add `actor:{instanceId}` as an eighth owner scope in E6. Bindings become durable; equipping is
  one insert.
- **B** — a durable `rpg_item_assignment` table owned by the item program; E6's bindings stay
  session-scoped and are rebuilt as a full projection at deploy. The atom layer's scope list is untouched.

They are not equivalent, and the tiebreaker turns out not to be taste. It is §5.

---

## 2. What is actually shipped — the evidence both options are judged against

Read before either option, because four of these facts decide the outcome and three of them are not in
any lane doc.

**F1 — `effect_binding` has no production consumer.** `ResolveBindings` / `ListBindings` are called from
`RpgStore.AtomInstances.cs` itself and from two test files
(`tests/FusionRpg.Data.Tests/AtomInstanceStoreTests.cs`, `BindResolutionTests.cs`) and **nowhere else in
`src/`**. E6 is schema plus a gate. Nothing executes a binding today, of any scope.

**F2 — the compiler has no owner seam.** `AtomCompiler.Compile` takes atoms, a runtime and a catalog
revision (`src/FusionRpg.Core/Effects/Atoms/AtomCompiler.cs:26-33`) — never a binding, never an owner. The
`EffectGrantDto` it emits sets `GrantId`, `EffectId`, `PluginId`, `Priority`, `Overlay` and **leaves
`OwnerKind` and `OwnerKey` unset** (`AtomCompiler.cs:149-156`). It compiles a catalog revision, not a
wearer.

**F3 — the hot matcher silently drops unknown keys.** `StatApplyScope.Matches` handles `match`, `plant:`,
`zombie:`, `entity:`, `player:` and then `return false;` (`StatApplyScope.cs:85`). No throw, no rejection
code, no log. `IsKnownOwnerKey` (`:96-111`) exists but its only caller in `src/` is one cheat action
(`src/FusionRpg.Injector/CheatActions.cs:66`).

**F4 — the projection already ships, and it discards the durable id on purpose.**
`UniqueOwnerBinder.ToEntityKey` takes `instanceId` and writes `_ = instanceId; // retained for call-site
clarity` before returning `entity:{ptr}` (`src/FusionRpg.Core/Match/UniqueOwnerBinder.cs:12-18`). It is
called from `UniqueLoadoutSpec.BindToPtr` (`src/FusionRpg.Core/Match/UniqueLoadoutSpec.cs:91`) on every
deploy. `decisions.md:47` locks this: *"Live lawn uses MatchRuntime UniqueBindings (`instanceId ↔ ptr`)
then FA1 `entity:{ptr}`"*, and [unique-actor-runtime.md](../unique-actor-runtime.md):82 states the rule —
*"Durable `instance:` must not appear in hot Resolve until a thin deploy binder rewrites it."*

**F5 — equipment is already a full-rebuild projection.** `UpsertUniqueEquipment` writes one durable row
then calls `RebuildUniqueModsFromEquipmentUnlocked` (`src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs:658`),
which reads **every** slot and regenerates the whole `mods_json.grants` array via
`UniqueEquipmentCatalog.BuildModsJson` (`src/FusionRpg.Core/Match/UniqueEquipmentCatalog.cs:62-118`).
Nothing is patched incrementally. The shipped code already implements Option B's core mechanic against
the stub table.

**F6 — the orphan sweep deletes any instance with no binding.** `Withdraw` deletes one binding then calls
`CollectOrphanInstancesUnlocked` in the same call (`RpgStore.AtomInstances.cs:414`), which `DELETE`s every
`effect_instance` and `effect_instance_atom` row that no binding points at (`:460-472`). Its comment is
the premise: *"An instance is reachable only through a binding"* (`:437-441`).

**F7 — a web battle actor has no hex pointer.** Battle actors get the synthetic ptr `web:{matchKey}:{n}`
(`src/FusionRpg.Core/Battle/BattleReportEmitter.cs:23`). `OwnerScope.Validate` requires `^[0-9a-f]+$` for
`entity:` (`OwnerScope.cs:118-122`), so `entity:web:exp-3-1:2` is `BadOwnerKey`. There is no legal
`entity:` key for a web battle.

**F8 — no runtime executes an equipment atom yet.** `stat.modify` is `Full / None / PlanOnly` —
*"Battle's sink ignores FA1, so battle is not supported"*
(`src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs:88-93`); `stat.derived` is quarantined
`None/None/None` (`:104-106`); only `status.apply` is `Partial` in battle (`:159`). Charms already say this
out loud ([ssot-charms.md](ssot-charms.md) §2).

---

## 3. Option A — add `actor:{instanceId}` as an eighth owner scope

### 3.1 What gets built

| Change | File |
|---|---|
| `OwnerKind.Actor` enum member (append — **ordinal 7**, never renumber) | `src/FusionRpg.Core/Effects/Atoms/OwnerScope.cs:11-20` |
| `Name(OwnerKind.Actor) => "actor"` | `OwnerScope.cs:43-53` |
| `Validate` case: 32 lowercase hex, matching `instance_id`'s grammar (definitions §1) | `OwnerScope.cs:102-140` |
| `IsSessionScoped` stays `Kind == Entity` — `actor:` is durable by construction | `OwnerScope.cs:38-39` |
| definitions §6 owner-key table gains a row; the stale-owner table gains *"specimen retired with live `actor:` bindings"* | `docs/architecture/effect-atom/definitions.md:196-231` |
| E6 spec: "Owner-key scopes — 7" becomes 8 | `docs/architecture/effect-atom/spec-instance-and-binding.md:46-52` |

No new table. No DDL change: `effect_binding.owner_kind` is `TEXT` and `owner_key` is `TEXT`
(`RpgStore.AtomInstances.cs:75-85`).

### 3.2 Lifecycle

| Moment | Write |
|---|---|
| Equip | one `Bind(BindingRow { InstanceId, OwnerKind.Actor, OwnerKey = guid, Slot = role, Priority = 0, Source = "equip" })` |
| Unequip | `Withdraw(bindingId)` |
| Deploy | nothing durable; the host resolves `actor:{guid}` to a live target |
| Recover | nothing durable |
| Process restart | bindings survive — that is the point |
| Specimen retired / fused | must delete the bindings by hand: `owner_key` is polymorphic text, so no FK and no cascade is possible |
| Save reset | `RpgStore.Reset()` (`src/FusionRpg.Data/Sqlite/RpgStore.cs:600-621`) deletes `rpg_unique_actors` and **does not touch `effect_binding`** — every `actor:` row survives its specimen |

### 3.3 Who writes what

The item program writes `effect_binding` rows directly, at equip time, into a table the atom program owns.
Set tiers, socket-combination containers and enhancement containers each add their own `actor:` rows with
distinct `source` values ([ssot-sets.md](ssot-sets.md) §4.5 already specifies `source = 'set:{set_id}'`).

---

## 4. Option B — the assign/bind split

### 4.1 What gets built

One new table in the item program, `rpg_item_assignment`, as specified in
[ssot-inventory.md](ssot-inventory.md) §4.4:

```text
rpg_item_assignment(
  player_id    INT  NOT NULL,
  owner_kind   TEXT NOT NULL,   -- 'specimen' | 'player'   (item-program vocabulary, NOT OwnerKind)
  owner_key    TEXT NOT NULL,   -- rpg_unique_actors.instance_id, or '' for player
  role         TEXT NOT NULL,   -- I2 role id, or a pouch role
  ref_kind     TEXT NOT NULL,   -- 'rolled' | 'stock'
  ref_id       TEXT NOT NULL,   -- effect_instance.instance_id  |  effect_container.container_id
  assigned_utc TEXT NOT NULL,
  revision     INT  NOT NULL DEFAULT 0,
  PRIMARY KEY (player_id, owner_kind, owner_key, role))
```

plus a partial unique index on `ref_id WHERE ref_kind = 'rolled'` (one rolled item, at most one cell) and
`ON DELETE CASCADE` on the specimen — possible here precisely because `owner_key` is typed by
`owner_kind` inside one program.

**The `owner_kind` / `owner_key` columns must not reuse `OwnerKind` or `OwnerScope`.** They are the item
program's grammar, spelled the same way on purpose and deliberately not the same type — the identical
discipline definitions §6 applies to `slot:`.

### 4.2 Lifecycle

| Moment | Write |
|---|---|
| Equip | one `INSERT` into `rpg_item_assignment` + one `rpg_item_event` row, one transaction |
| Unequip | one `DELETE` + one event row, one transaction. **No `effect_binding` write at all** |
| Deploy | full projection: read the assignment set → resolve each `ref_id` to an instance → `Bind` at the host's live scope → hand the compiled grants to the runtime |
| Recover | withdraw the projection; the assignment set is untouched |
| Process restart | nothing to reload — the projection is derived, and the boot sweep clears whatever a crash left behind |
| Specimen retired / fused | one helper releases every cell to the armoury and writes one `released` event each, in the retire transaction; cascade is the backstop, not the mechanism |
| Save reset | `rpg_item_*` joins the `Reset()` delete list |

### 4.3 Who writes what

- **Item program** writes `rpg_item_assignment` and reads it. That is the only durable record of equip
  state, and nothing else may hold one.
- **The deploy path** writes `effect_binding`, session-scoped, always as a full rebuild, never a delta.
- **The atom program** owns the scope list and does not change it.

### 4.4 The one place B is under-specified today, and its answer

[ssot-inventory.md](ssot-inventory.md) §2.4 names the projection target as `entity:{ptr}`. By **F7**
there is no legal `entity:` key for a web battle, and by **F8** no battle executes an equipment atom
anyway. So the honest scope of B on the day it lands is:

> The projection targets `entity:{ptr}` **on the lawn only**, which is exactly the shipped path (F4). The
> web-battle and sim projection targets are **not decided here** — they are blocked behind E12's
> `BattleStatComposer`, which is where every per-actor stat is already waiting.

That deferral costs nothing today because F8 means there is nothing to project into. It is §8.3's
condition 1 for reopening `actor:`.

---

## 5. Does Option A actually work end to end?

Traced from the equip click to the actor. **It stops twice, and the second stop is silent.**

| Step | What happens with `actor:9f3c…` |
|---|---|
| `OwnerScope.TryParse` | today `BadOwnerKey` — *"unknown owner kind 'actor'"* (`OwnerScope.cs:86-87`). With A's enum member and `Validate` case: **parses.** OK |
| `RpgStore.Bind` | validates, inserts, returns Ok (`RpgStore.AtomInstances.cs:183-222`). OK |
| `ListBindings(actor:…)` | matches on `(owner_kind, owner_key)` via `ix_effect_binding_owner`. OK |
| `BindGate.Check` | needs no `actor:` case. The world-host check is `Sector`/`Slot` only (`BindGate.cs:188-190`); `CheckScope` rejects `stat.modify` on `defense` for every non-`match` scope, which is correct and already covers `actor:` (`BindGate.cs:239-252`). OK |
| `ResolveBindings` | refuses every binding whose instance was rolled against an older `catalog_revision` (`RpgStore.AtomInstances.cs:288-295`). A-neutral, but see §9.3 |
| **`AtomCompiler.Compile`** | takes atoms and a runtime; **no binding, no owner** (F2). The emitted grant carries no owner key at all (`AtomCompiler.cs:149-156`). **Stop 1** — there is no seam to carry `actor:` through. |
| **`StatApplyScope.Matches`** | if something sets `OwnerKey = "actor:9f3c…"` on the grant anyway, the matcher falls through to `return false` (`StatApplyScope.cs:85`). No rejection code, no log. **Stop 2 — a silent no-op.** |
| `UniqueBoundLoadout` fail-closed skip | tests `WouldRejectOnHot`, which is `IsInstanceOwnerKey` — `instance:` only (`src/FusionRpg.Injector/Match/UniqueBoundLoadout.cs:28-29`, `UniqueOwnerBinder.cs:54-55`). An `actor:` grant is **not** skipped loudly; it is enqueued into the Funnel and then matched against nothing. |

To clear stop 2, `StatApplyScope` needs an `actor:` branch, and that branch needs an `instanceId → ptr`
map on the hot path. That map is `MatchUniqueBindingsFacet`, in the Injector. Teaching Core's hot matcher
about a durable id is exactly what `unique-actor-runtime.md:82` and `decisions.md:47` forbid — and the
sanctioned alternative they name is *"a thin deploy binder rewrites it"*, which is a projection.

> **Option A does not remove the projection. It adds a durable scope on top of one.** The durable row is
> real; everything downstream of `ListBindings` still has to be rewritten to a live owner at deploy. A
> buys one insert at equip time and pays for it with an eighth scope that nothing between the compiler and
> the Funnel can consume.

### 5.1 What the shipped stub tells us

`UniqueEquipmentCatalog.Grant` writes `OwnerKind = "instance"`, `OwnerKey = "instance:pending"`
(`src/FusionRpg.Core/Match/UniqueEquipmentCatalog.cs:124-125`). Both lanes read this as *"the shipped code
already invented an eighth scope"*. It is the opposite.

`"pending"` is not an id. It is a **template marker**, and there is a shipped function whose only job is to
substitute it: `UniqueOwnerBinder.BindGrant` → `BindOwnerKey` → `ToEntityKey`, which throws if there is no
ptr and **discards the instanceId** (`UniqueOwnerBinder.cs:12-36`, called at `UniqueLoadoutSpec.cs:91`).
The key is unparseable *because it is never meant to be parsed* — it is a hole in a template with a
rewrite step that fills it at deploy.

The last engineer who faced this question did not add a scope. They wrote a placeholder and a projection.
That is Option B, shipped in W5-B and W8-A, against a three-column stub table.

---

## 6. Option C — considered, and why it is not the answer today

**C = both**: `rpg_item_assignment` as the durable SSOT *and* `actor:{instanceId}` added now, replacing
`entity:{ptr}` as the projection target so the lawn, web battles and sim all name the wearer the same way.

The argument for it is F7: `entity:{ptr}` is a lawn concept, IL2CPP pointers do not exist in a web battle,
and `web:{matchKey}:{n}` is not hex. A single runtime-neutral wearer key would let a host resolve
`actor:{guid}` its own way and would make [ssot-sets.md](ssot-sets.md) §4.4's *"a tier binds at its pieces'
owner scope"* true in every runtime rather than only on the lawn.

**Rejected for now, on F8.** No runtime executes an equipment atom: battle is `None` for every kind except
`status.apply`, and `stat.derived` is quarantined everywhere. Adding a scope to serve a projection target
in a runtime that runs nothing would create the exact defect SC7 names — *a row no code consumes is not
content; it is a lie in a table* — and the atom program already carries that scar as `status.expose.*`.

C is the right answer **later**, on the conditions in §8.3. Naming it now is not hedging; it is recording
the trigger so the next lane does not have to re-derive this argument.

A fourth shape — **neither**: put equip state on `rpg_unique_equipment` and never touch the atom layer —
was rejected in one line. Three columns, no `player_id`, no FK, a hardcoded `weapon|armor|trinket`
allowlist (`RpgStore.cs:356-361`) and a three-item stub catalog cannot carry ~15 roles × two frames × 48
specimens, and it would make SC1 false by putting a second modifier path beside the atom layer.

---

## 7. The comparison, on named axes

| # | Axis | Option A | Option B |
|---|---|---|---|
| 1 | **Reaches the actor end to end** | **No.** Stops at the compiler (F2) and then silently at `StatApplyScope.cs:85`. Completing it means teaching the hot matcher a durable id, which `decisions.md:47` forbids | **Yes**, on the lawn, via the shipped binder (F4). Web battle deferred to E12, which blocks it anyway (F8) |
| 2 | **The orphan sweep (F6)** | **Worse.** `Withdraw` deletes the binding then sweeps in the same call (`:414`), so **unequipping deletes the item**. An unworn item in the armoury has no binding at all and is swept on the next withdraw | **Also broken, but narrower.** A projected `entity:` binding is withdrawn at every recover, orphaning every rolled item. Both need §9.1; A additionally makes the *routine* unequip destructive |
| 3 | **Reproduction contract (definitions §5)** | Neutral. `(container_id, catalog_revision, roll_seed)` ⇒ identical atoms; the owner is not an input | Neutral, and **strictly cleaner**: the durable record holds no rolled values, so re-projecting is by construction identical |
| 4 | **UniqueActor FSM** (`Deploying → ActiveBound → Recovering`) | The durable binding exists in all five phases including `Roster` and `Retired`. Nothing withdraws it on retire; `owner_key` is polymorphic so no FK can cascade. `Reset()` does not clear `effect_binding` (`RpgStore.cs:600-621`) → bindings outlive their specimen | Maps exactly onto the shipped handshake: project at `Deploying → ActiveBound`, withdraw at `Recovering`, assignment untouched (`unique-actor-runtime.md:145-171`). Retire calls the release helper; cascade is the backstop |
| 5 | **Expedition soft-lock** | A specimen is locked for up to 20 h across restarts (`docs/architecture/standalone/spec-expeditions.md:18,47`) while its `actor:` bindings sit durable and unexecuted. Harmless, and it hides the fact that nothing ran them | The lock is a membership row and the gear is an assignment row — two cold tables in two programs, neither pretending to be live. Expedition battles run through `WebMatchService`, which by F7/F8 projects nothing yet |
| 6 | **Is unequip atomic?** | **No.** `Withdraw` is one delete plus a sweep, outside a transaction (`:404-417`), and it must be repeated per set-tier and per socket-combination binding. Multi-row, multi-call, partially observable | **Yes.** One `DELETE` plus one event row, one transaction ([ssot-inventory.md](ssot-inventory.md) §5.4). Derived state has no delta to lose |
| 7 | **Process restart** | Bindings survive — genuine, and A's best property. But nothing consumes them (F1), and a crash mid-run leaves the *runtime's* view stale with no sweep, because `ClearSessionScopedBindings` only clears `entity:` (`:423-435`) and **has no caller in `src/`** | Nothing durable to reload. Requires a boot call to `ClearSessionScopedBindings` (§9.2), which today does not exist for either option |
| 8 | **Lawn vs web battle** | One durable key for both — A's strongest argument. Neither host can execute it (F2/F8), so the advantage is unrealised | Lawn works today. Web battle has no legal target (F7) and nothing to run (F8). B must say so, and §4.4 does |
| 9 | **Boundary cost** | **Ask-first** under E6 *"Ask first: adding an owner scope"* (`spec-instance-and-binding.md:123`), plus edits to definitions §6, which *"wins over any spec"* | No scope change. But **not free**: §9.1 and §9.2 change shipped E6 behaviour, and the reconciliation plan already flags the orphan-sweep fix as a build needing separate authorization |
| 10 | **Reversibility** | See §10 — expensive and lossy to unwind | See §10 — additive to unwind |

---

## 8. Recommendation

### 8.1 Adopt Option B

**`rpg_item_assignment` is the only durable record of equip state. `effect_binding` stays session-scoped
and is rebuilt in full at deploy. The owner-scope list stays at seven.**

Three reasons, in order of weight:

1. **A does not work end to end and B does** (§5). A durable binding that no compiler carries and no
   matcher matches is a row that looks like a feature. B's path — durable record, full rebuild, rewrite to
   the live owner at deploy — is shipped code with a shipped test surface (F4, F5).
2. **Unequip is atomic under B and is not under A** (axis 6), and under A the routine unequip destroys the
   item through the orphan sweep (axis 2). The item program's most common write must not be the dangerous
   one.
3. **The cheap change is the reversible one** (§10). B → A later is additive; A → B later is lossy,
   because `effect_binding` has no `player_id`, its `slot` column is unvalidated free text, and its rows
   would by then be the sole record of what a demon is wearing.

### 8.2 The rejected case, stated in full

Option A is not a bad design and its advocates in [ssot-equip-slots.md](ssot-equip-slots.md) §9.1 and
[ssot-requirements.md](ssot-requirements.md) §5.6 are right about the gap. Everything below is true and is
being given up:

- **One act instead of two.** Equipping becomes one insert into one table. B needs an assignment write, a
  projection at deploy, and a discipline that nothing else may write equip state. That discipline is a
  standing invitation to a future defect: the second someone patches a binding incrementally "just for
  this case", B's atomicity claim is false and nothing in the schema stops them.
- **One durable name for every runtime.** A's key works the same on the lawn, in a web battle and in sim.
  B has a lawn projection and an undecided one for battle (§4.4). That is a real hole; it is empty only
  because F8 keeps it empty.
- **The conceptual model in [item-ideal.md](../item-ideal.md) §6.4 — *"Equipping is a binding"* — is
  simply true under A.** Under B that sentence is wrong and must be corrected in R4. Making a document
  wrong is a cost, not a rounding error.
- **`ListBindings(owner)` answers "what is this demon wearing" in one indexed query.** Under B that
  question is answered by `rpg_item_assignment`, and the binding table answers a *different* question.
  Two tables that both look like "what is equipped" is precisely the confusion the terminology lock in §1
  of the contract exists to prevent.
- **A is honest about the gap.** B is, in one reading, a workaround that lets the atom program keep a
  scope list that does not describe the game. If the projection ever has to run in three runtimes with
  three different live-owner grammars, A's single durable name will look like the better call in
  hindsight.

The rejection rests on §5, not on preference. **If `AtomCompiler` carried an owner and `StatApplyScope`
had an `actor:` branch that resolved through the shipped `instanceId ↔ ptr` facet, A would win this
debate.** It does not, and building both is §6's Option C, which F8 makes premature.

### 8.3 `actor:{instanceId}` is reserved, not refused

The name is reserved now so no lane invents `specimen:`, `unique:` or `demon:` for the same thing.
Reopen this decision — as an E6 ask-first change — when **all four** hold:

1. A runtime that executes equipment atoms exists and **has no pointer** to project onto (E12's
   `BattleStatComposer`, or the sim).
2. `AtomCompiler` gains a per-binding compile path, so an owner can survive compilation (F2).
3. `StatApplyScope` (or its successor) rejects unknown owner keys with a reason code instead of
   `return false` (F3) — otherwise a new scope is a new silent failure.
4. §9.1 has landed, so a durable binding cannot be swept.

Until then: `actor` is a **reserved owner-kind name**. No lane may use it, and no lane may mint a synonym.

---

## 9. Three mandatory amendments to shipped code

B is not free. These are the price, and they are build items, not design items. Per
[reconciliation-plan.md](reconciliation-plan.md) *"Not in this plan: the fixes"*, they need the owner's
authorization separately.

### 9.1 The orphan sweep needs a second reachability root — **blocking**

`CollectOrphanInstancesUnlocked` deletes every instance no binding points at
(`RpgStore.AtomInstances.cs:460-472`), on the premise *"An instance is reachable only through a binding"*
(`:437-441`). The item program falsifies that premise: `rpg_item.instance_id` is a reachability root, and
an item sitting unworn in the armoury has zero bindings **by design**.

Without this fix, under B the first `Withdraw` after any recover deletes the player's armoury. Under A the
first unequip deletes the item. The fix is the same either way — the sweep must exclude instances
referenced by `rpg_item` — and it must land **before** the first `rpg_item` row exists.

### 9.2 Something must call `ClearSessionScopedBindings` — **blocking**

`ClearSessionScopedBindings` (`:423-435`) is the only thing that clears `entity:` rows and it **has no
caller in `src/`**. Under B a crash mid-run leaves a dead session's projection in the database, and the
next deploy's full rebuild will not remove it, because rebuild writes — it does not diff. Call it at
server boot and at match end.

### 9.3 The catalog-revision refusal will refuse every rolled item — **must be routed, not fixed here**

`ResolveBindings` refuses any binding whose instance carries a `catalog_revision` other than the current
one (`:288-295`). Rolled items are frozen at the revision they dropped at, so **the first content import
after a player owns anything refuses all of it** with `StaleInstance`. This is orthogonal to A vs B —
both keep frozen instances — and it is squarely **D2's** question (the mutation contract, and what
reproducibility can be promised without catalog archiving). Recorded here so D2 does not miss it, and so
nobody reads it as a cost of this decision.

**One more, non-blocking:** definitions §6 promises *"Instance deleted with live bindings | FK `ON DELETE
CASCADE`; bindings go with it"*. The shipped `effect_binding` DDL declares **no foreign keys at all**
(`RpgStore.AtomInstances.cs:75-85`). The behaviour is currently supplied by `Bind`'s existence check
(`:188-190`) and nothing else. That is a defect-register item.

---

## 10. Migration from the `rpg_unique_equipment` stub

Today: `rpg_unique_equipment(instance_id, slot, item_id)`, PK `(instance_id, slot)`, slot allowlist
`weapon|armor|trinket` (`RpgStore.cs:356-361`), three stub items
(`UniqueEquipmentCatalog.cs:23-26`), and `UpsertUniqueEquipment` → `RebuildUniqueModsFromEquipmentUnlocked`
(`RpgStore.UniqueActors.cs:621-668`) → `mods_json.grants` with `instance:pending` → deploy → `BindToPtr`
→ `entity:{ptr}` → Funnel. REST is `GET/PUT/DELETE /api/unique/actors/{id}/equipment`
(`src/FusionRpg.Server/UniqueActorEndpoints.cs:79,85,100`); the FE mirrors the allowlist at
`web/fusion-rpg-web/src/features/roster/RosterPage.tsx:33`.

Five steps. **No step changes observable behaviour until M4**, and each is independently revertible.

| Step | Change | Reverts by |
|---|---|---|
| **M0** | Land §9.1 and §9.2. No item dependency, no item rows yet | reverting two commits |
| **M1** | Create `rpg_item_assignment`. Copy every `rpg_unique_equipment` row: `player_id` from `rpg_unique_actors`, `owner_kind = 'specimen'`, `owner_key = instance_id`, `role` via I2's alias map (`weapon → armament-primary`, `armor → core-guard`, `trinket → jewel-minor-a`, [ssot-equip-slots.md](ssot-equip-slots.md) §5.7 step 1), `ref_kind = 'stock'`, `ref_id = item_id`. One-way and idempotent; the old table keeps its rows and stops being written | dropping one table |
| **M2** | Repoint `RebuildUniqueModsFromEquipmentUnlocked` (`:658`, `:682`) to read assignments instead of `rpg_unique_equipment`. **Output shape unchanged** — same `mods_json`, same `instance:pending`, same binder, same FE. This is the whole SSOT switch, and it is one query swap | one query swap back |
| **M3** | Widen the role vocabulary from `item_role_frame` instead of the static allowlist (`UniqueEquipmentCatalog.NormalizeSlot:50-56`), and return the role list inside the existing equipment GET payload so the FE drops its literal | keeping the allowlist |
| **M4** | Replace the stub grant template: for `ref_kind = 'rolled'`, read `effect_instance` and take compiled grants from E7 rather than `UniqueEquipmentCatalog.Items`. Owner key stays `instance:pending`, still rewritten at Bound. Then drop `rpg_unique_equipment` and `UniqueEquipmentCatalog.Items` | the drop is the only irreversible act; do it last |

The point of M2: **the assignment table becomes the SSOT before any atom-layer work happens.** If E7, E9 or
E12 slips, the item program is still correct and the roster still equips.

### What breaks if we choose wrong and have to switch later

**B → A later (if §8.3's conditions arrive): additive, cheap.** Assignments stay as they are. You add the
enum member and the `Validate` case, and the projection's target changes from `entity:{ptr}` to
`actor:{guid}` in one function (`UniqueOwnerBinder.BindOwnerKey`). Nothing has to be un-written, because B
never put durable equip state in `effect_binding`. Blast radius: `OwnerScope.cs`, one binder function,
definitions §6, E6 §46-52.

**A → B later: expensive and lossy.** Every `actor:` binding written between the choice and the reversal
is the *sole* record of what a demon wears, and it cannot be reconstructed into an assignment row without
guessing:

- `effect_binding` has **no `player_id`** — ownership must be recovered by joining `owner_key` back to
  `rpg_unique_actors`, which fails for any specimen deleted by `Reset()` (`RpgStore.cs:600-621` does not
  clear `effect_binding`, so those rows survive their owner and become unattributable).
- `slot` is `TEXT` with no FK and no per-frame validation (`RpgStore.AtomInstances.cs:75-85`) — nothing
  guarantees it holds a legal I2 role, so `role` cannot be trusted on the way out.
- `ref_kind` does not exist: a binding points at an instance, so the stock/rolled distinction that
  [ssot-inventory.md](ssot-inventory.md) §3.1 is built on has been erased and must be re-derived from
  `effect_instance.origin`.
- Set-tier and socket-combination bindings are indistinguishable from item bindings except by `source`
  string convention, so the reverse migration must parse `source` to know which rows are *derived* and
  must be dropped rather than converted.
- And the orphan sweep fires during the migration itself: deleting `actor:` bindings before writing
  assignments deletes the instances (F6).

**The asymmetry is the decision.** One direction is a function signature; the other is a lossy reverse
migration over the player's inventory.

---

## 11. What each blocked lane must change in its own document

| Lane | Change |
|---|---|
| **I13 — [ssot-inventory.md](ssot-inventory.md)** | §2.4 and its ask 11(a) are **accepted as written**; delete the "or add `specimen:{guid}`" alternative. Amend §2.4's projection target to *"`entity:{ptr}` on the lawn; the web-battle target is deferred to E12"* (§4.4 here, per F7/F8). Rename the reserved future scope from `specimen:` to **`actor:`** wherever it appears. Promote §9.1's sweep fix into §5.6's stale table as a **blocking** prerequisite, not a footnote. Ask 11(b) — stock refresh writing `effect_binding` from the importer — is **unaffected** by this decision and still needs its own sign-off |
| **I2 — [ssot-equip-slots.md](ssot-equip-slots.md)** | §9.1 is **answered: no scope is added.** Rewrite it as *"the durable record is I13's assignment row; the binding is its deploy projection"*. §5.7 **step 3 is unblocked** and its wording changes: `RebuildUniqueModsFromEquipment` does not "start creating and withdrawing `effect_binding` rows" — it reads assignments and emits the same `mods_json` template (M2 above). The proposed new nullable `instance_id` column on `rpg_unique_equipment` is **dropped**: that table is retired by M1, not extended. The §6 row *"A body-slot binding at `entity:` scope expected to survive a restart → `ScopeUnsupported`"* is correct and becomes load-bearing — it is the rule that stops anyone durably persisting a projection |
| **I5 — [ssot-sets.md](ssot-sets.md)** | §4.4's *"a set tier binds at exactly the owner scope its member pieces are bound to"* **survives unchanged** — it is now a statement about the projection scope, which is uniform by construction. §4.5's recount SQL is **correct as written** but must be re-labelled: it runs **during the deploy projection**, over the just-built binding set, not at equip time. The equip-time write is one assignment row. Ask 12 is **answered**. New requirement: because tiers are derived from a projection that is itself derived, the recount trigger list in §4.5 must add *"deploy"* and drop nothing |
| **I11 — [ssot-requirements.md](ssot-requirements.md)** | §5.6 is **answered and its conclusion inverted**: *"durable equipment on a demon specimen cannot be expressed by the shipped binding model"* is true, and is no longer a blocker, because durable equipment is not expressed by the binding model at all. Rewrite §5.6 to say the gate reads the wearer from `rpg_unique_actors` + `rpg_item_assignment` and never from an owner key. Ask 4 is closed. The `canEquip(item, specimen)` entry point I13 asks for (its ask 9) is now unambiguous — it takes a specimen id, not an owner scope |
| **I10 — [ssot-charms.md](ssot-charms.md)** | Least affected: `player:{id}` is a shipped scope and this decision does not touch it. Two consequences. (1) §3.1's recommendation C is **confirmed** — charms bind at `player:{id}`, and the assign/bind split gives the pouch the same shape as equipment: `rpg_item_assignment` with `owner_kind = 'player'` is the durable attunement record, and `charm_run_hold` plus the run-start bindings are its projection. §5's *"attunement is durable intent, not a runtime fact"* is exactly this decision, arrived at independently. (2) I13's ask 10 — *"is that a second projection path?"* — is **answered: yes, and it is the same path with a different owner scope.** One projector, two owner kinds. The `player:` → match-wide stub (`StatApplyScope.cs:82-83,88-92`) remains I10's own blocker and is untouched here |

---

## 12. Open questions for the owner

1. **Authorize the two blocking fixes (§9.1, §9.2)?** Both change shipped, green E6 behaviour. §9.1 is
   required before the first `rpg_item` row exists, or the armoury is deletable. Neither is an
   item-program change, so neither is covered by the enrichment round's mandate.

2. **Does §9.3 belong to D2 or to a defect-register build?** Every rolled item refusing after the first
   content import is severe enough that it may not want to wait for the mutation-contract debate.

3. **Is `actor` the right reserved name?** Alternatives considered: `specimen:` (I13's wording — accurate,
   but it means "demon" and the commander is not one), `unique:` (matches `rpg_unique_actors` but reads
   like a rarity), `instance:` (**rejected** — already taken by the legacy grant grammar,
   `StatApplyScope.cs:33-37`, and reusing it would make two vocabularies collide on one string).

4. **Should `rpg_item_assignment.owner_kind` carry a third value now?** `specimen` and `player` cover the
   roster and the pouch. A commander is `specimen` under [ssot-inventory.md](ssot-inventory.md)'s open
   question 4; if the commander is *not* a `rpg_unique_actors` row, this decision does not cover it, and
   I13's question 4 becomes load-bearing rather than cosmetic.

5. **Who owns the projector?** The deploy projection reads item-program tables and writes atom-program
   tables. I13's ask 11(b) raises the same boundary for stock refresh. One answer should cover both:
   either a projector module in the item program that is the only permitted writer of `effect_binding`, or
   an E6-side API the item program calls. Recommendation, not a decision: **the second**, so
   `guard-dal.ps1` and E6's boundaries keep meaning what they say.
