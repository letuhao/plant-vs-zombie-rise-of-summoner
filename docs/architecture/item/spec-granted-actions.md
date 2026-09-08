# Spec: `granted-actions`

**Module id:** `granted-actions` · **Program:** [item](../item-map.md) · **Build order:** 19 of 21
**Depends on:** `equip-assign` (4), `base-types` (6) · ⛔ **`X3`**
**Rulings:** D14, D16, D26 · lane [G4 `ssot-granted-actions.md`](ssot-granted-actions.md)

⚠ **Dependency reconciled 2026-09-04 — the map understates this row.** [item-map.md](../item-map.md)
§4 lists module 19 as *"4, **X3**"*. **Module 6 is real and load-bearing:** `item_granted_action`'s
`container_id` is an FK to `item_base_type(container_id)` (§(a) below), and **gate GA2 is blocked by
module 6** in this spec's own gate table. The map's 2026-09-04 reconciliation note lists five
understated rows and does not include this one. **Ask: add `6` to item-map.md §4 row 19** — the same
kind of one-line map edit already recorded for modules 8, 12, 13, 16 and 21.

## Objective

G4 is **one seam and nothing else**: an item declares `(action_id, grant_role)`, and equipping it writes
an `rpg_action_grant` row. Everything that answers *when*, *how much*, *at whom* or *how often* stays in
the action layer.

> The item supplies an **identifier and a role**. A seam that quietly specifies a cooldown has failed,
> and the way it fails is by growing a column — so §5.3's *Never* list is the load-bearing half of this
> module, not an appendix.

## Design

### ⭐ The handshake shipped. Six of nine items are done, and one names this lane in its own DDL

`ssot-granted-actions.md` §5.5 is the lane's stated main product: nine numbered items the action program
must expose. **The action runtime has since been built** — `src/FusionRpg.Core/Actions/` holds 30+ files
and `RpgStore.Actions.cs` creates five tables. Verified item by item:

| # | Handshake item | State | Evidence |
|---|---|---|---|
| 1 | A resolvable `action_id` namespace | ✅ **shipped** | `rpg_action(action_id TEXT NOT NULL PRIMARY KEY)` — `RpgStore.Actions.cs:22-23` |
| 2 | A per-action `grantable` flag | ✅ **shipped** | `RpgStore.Actions.cs:31`; `ActionRow.Grantable`; refusal `ActionRejectionReason.ActionNotGrantable` (`ActionRejection.cs:54-55`) |
| 3 | `default_attack_eligible`, **separate from (2)** | ✅ **shipped, and separate** | `RpgStore.Actions.cs:32`; `ActionRow.cs:31`; refusal `ActionNotDefaultAttackEligible` (`ActionRejection.cs:57-58`) |
| 4 | An action-set assembly entry point | ✅ **shipped** | `ActionSetAssembler.Assemble(basics, liveGrants, isDefaultAttackEligible)` — `Grants/ActionSetAssembler.cs:42`; `default-attack` replacement at `:80-85` |
| 5 | **A grant table that is not `effect_binding`** | ✅ **shipped — option (a), verbatim** | `rpg_action_grant` — `RpgStore.Actions.cs:82-91`. Its DDL comment cites this lane by name: *"No `instance_id` column: a granted action has no instance and no rolls (spec-action-model.md §5 — **the correction from item/ssot-granted-actions.md §5.5 item 5**)"* |
| 6 | A named snapshot moment | ✅ **shipped** | `FrozenActionSet.FreezeAtRunStart` — `Grants/FrozenActionSet.cs:27-28` |
| 7 | Written removal semantics per FSM state | ⚠ **partial → ✅ claimed by this module**, below | `FrozenActionSet.cs:11` records the shape — *"the underlying `rpg_action_grant` row can be marked withdrawn at any moment"* — but the per-state table §3.5 proposes is not written down anywhere the kernel can be held to. **It was assigned to nobody; this spec claims it** |
| 8 | **A cap policy and its number** | ⛔ **open** | `ActionSetAssembler.cs:30` — *"Pure — no cap enforcement (item 8 / T24's own job)"*. Nothing enforces one |
| 9 | A written refusal of per-grant overrides | ⛔ **not recorded** | Nothing in `decisions.md`. §5.6's *"one thing that can be done today"* was never done |

**So the lane's §5.6 — *"four independent reasons, any one alone sufficient"* — is half wrong:**

| §5.6 reason | Verdict |
|---|---|
| 1. *"`rpg_action` does not exist. No table, no `src/FusionRpg.Core/Actions/` directory, no rows."* | ⛔ **False.** Both exist |
| 2. *"`item_base_type` does not exist either"* | ✅ **Still true.** Module 6 `base-types` owns it; this module keys on it |
| 3. *"Eleven of twelve kinds are `Battle = None`; one is `Partial`"* | ⛔ **False, and the §3.6 table is stale end to end.** **Five kinds are `Battle = Full` today:** `stat.modify` (`AtomKindRegistry.cs:217`), `stat.derived` (`:255`), `resource.delta` (`:290`), `status.apply` (`:344`), `shield.grant` (`:396`). No kind is `Partial`. The remaining **seven** stay `Battle = None`: the **five** `AttachPoint.Board` kinds — `spawn.entity` (`:403`), `board.action` (`:431`), `grid.spawn` (`:445`), `grid.clear` (`:460`), `box.set` (`:476`) — plus `resource.economy` (`:296`) and `status.clear` (`:358`). ⚠ **Corrected 2026-09-04: this row previously said "six board kinds", which would make thirteen.** Five Board + five Full + `resource.economy` + `status.clear` = the twelve registered kinds |
| 4. *"There are no real weapons"* | ✅ **Still true.** Three stubs (`UniqueEquipmentCatalog.cs:23-25`) and four relics (`RelicCatalog.cs`) |

⭐ **And §3.6's headline conclusion inverts.** The lane wrote: *"the weapon fantasy is currently split
across two runtimes with no overlap"* — the numbers half lawn-only, the action half battle-only. **Both
halves now execute in battle.** A weapon's `stat.modify` and `stat.derived` atoms compose there
(`BattleStatModifierLedger` per A18e, `BattleStatComposer` per E12), and an action resolves there. The
lawn gap is unchanged and option **(b)** — *battle-frame content, honestly tagged* — still stands, but
the case for it is now "the lawn has no queue", not "no runtime executes both halves".

### ⭐ Handshake item 7 — per-FSM-state removal semantics. **Claimed here.**

§5.5 item 7 was marked *partial* and assigned to nobody: `ssot-granted-actions.md` §3.5 *proposes* the
per-state table, and the audit's own words are *"not written down anywhere the kernel can be held
to."* **This module claims it**, on the lane's own reasoning — *"written now because it is free now
and expensive after someone builds mid-match equip."*

**Why it is ours and not the timeline program's:** the rule says what a *grant removal* means, and
grants are this module's whole product. The kernel supplies the states; it owes no opinion on an item
leaving. The one thing the kernel must do — **never accept an inventory event as an `InterruptCause`**
— is a refusal this module *requests* (§9.10), not a behaviour it adds.

**It is unreachable today, which is exactly what makes it cheap.** Equipment cannot change mid-run:
`UniqueActorService.PutEquipment` refuses unless the actor's phase is `Roster`, returning
`phase.not_roster` (`src/FusionRpg.Server/UniqueActorService.cs:43-44`), and `ClearEquipment` routes
through the same method (`:62-64`). So the shipping rule is unchanged:

> **The actor's granted-action set is assembled at run start and is immutable for the run.**

**The table below is the contract for the day that stops being true.** Verified against the shipped
FSM — `TurnState` is eight values (`Battle/Timeline/TurnState.cs:14-24`) and `TurnTransitions.Legal`
declares every edge in the same file:

| Actor state at removal | Rule | Why |
|---|---|---|
| `Charging`, `Ready` | the action leaves the selectable set **immediately** | nothing has been paid — the intent source simply stops offering it (`IntentSource.cs`) |
| `Committed`, `Resolving` | **the run completes**: costs stay paid, resolve handles fire, cooldown starts | *"Committing is what costs, not landing"* — cancelling here needs a refund path that rule forbids |
| `Recovering` | applies at the `Recovering → Charging` transition | the only edge out of it |
| `Downed`, `Dead`, `Withdrawn` | **recorded**, applied if the actor returns | `Downed → Charging` is legal, so a revive must not resurrect a removed grant |

**Three invariants — this is the part a kernel can be held to:**

| # | Invariant | Evidence / refusal |
|---|---|---|
| 1 | Removal applies at the **next quiescent point**, never mid-commitment | the two-row split above |
| 2 | Removal **never cancels a committed action** | no refund path exists, by rule |
| 3 | ⛔ An inventory event **never becomes an `InterruptCause`** | the enum is `CrowdControl` and `Damage` (`Battle/Timeline/ActionRunner.cs:41-45`); a third cause puts an item concern inside the kernel's slot accounting — the one place with a zero-allocation contract and a byte-identical gate in front of it |

⚠ **Nothing needs reverting.** A granted action creates no binding, so the apply/revert lifecycle
`stat.modify` and `stat.derived` carry does not apply here. **And cooldown survives removal for
free:** `CooldownLedger` keys on `CooldownSlot(ActorKey, Slot)` (`CooldownLedger.cs:8`), not on the
item, so unequip-then-re-equip does not reset it. That closes the classic swap exploit and nobody
should "fix" it.

**Ships as:** this table, the four tests below, and a `decisions.md` request — the same shape as
handshake item 9. There is no enforcement code because there is nothing to enforce until mid-match
equip exists; **`ItemGrantLandedFlags.MidRunEquipLanded = false`** carries that, so the FSM tests skip
**against a flag** rather than being silently absent.

### ⭐ R2 — the granted-action power budget. **Claimed, as an import-time validation.**

**Module 9 built the read and handed the consumer here; this module never picked it up**, so the read
had no consumer and module 9 was carrying a requirement nobody would use.
`spec-item-power-reads.md`'s R2: *"`grantedActionPrice(actionId) := Reference.ScaleMilli(rungOf(actionId).QPowerMilli)`,
the same path, reported against the item's rarity ceiling as a **share with a band**"* — and *"this
read is reportable today and **gating only when module 19 `granted-actions` lands**."*

**Picked up. It is one call at import:**

| | Rule |
|---|---|
| **When** | at import of an `item_granted_action` row — the same moment `ActionNotGrantable` and `ActionNotDefaultAttackEligible` are checked. Never at drop, never at bind |
| **What** | the R2 price against the base type's rarity ceiling, as a **share with a band**, never a threshold — an action's price and an affix bundle's price come from different shapes, so the error does not cancel (module 9's own cross-shape note) |
| **Refusal** | over the ceiling → `GrantedActionOverBudget`, naming the offender and the band, exactly as an over-budget implicit is reported |
| ⛔ **No resolvable rung** | `unpriced` → **refused**, never read as `0`. G4's stated fear is *"pricing it at zero would make every action-granting item strictly dominant"*; module 9 answers it by never pricing at zero, and **this is the enforcement half of that answer** |
| **Never** | a generation input. It fails a lint; it does not silently shrink an item at drop time |

⚠ **Inert in the same way GA3 is, and for the same reason.** With **X3** unresolved nothing produces
actions, so there are no rungs to price. **The validation ships with GA2** — DDL, validator, reason
codes, zero content rows — and its first real exercise is GA3's one weapon. That is why module 9's
read is *reportable* before it is *gating*.

**So module 9 keeps R2**, and its sensitivity note is now true in both directions rather than pointing
at a module that declined to look.

### What is actually missing — three things, and one of them is not ours

#### (a) The item side does not exist

No table declares that an item grants an action. `item_granted_action` is this module's whole build:
six columns keyed on the base type (§4.4 — **never** on an instance; an action id is identity, and
putting it on a rolled instance would drag it into SC5's determinism contract for no gain).

| Column | Type | Notes |
|---|---|---|
| `container_id` | TEXT | FK → `item_base_type(container_id)`; the container must be `ContainerKind.Item` |
| `seq` | INT | stable authoring and display order. PK is `(container_id, seq)` |
| `action_id` | TEXT | FK → `rpg_action(action_id)`. **This is the entire seam** |
| `grant_role` | TEXT | `default-attack` \| `granted`. The constants already ship: `ActionGrantRoles.DefaultAttack = "default-attack"` (`Grants/ActionSetAssembler.cs:10`) |
| `enabled` | INT | content is disabled, never deleted |
| `revision` | INT | joins the E8 content hash |

Plus one index, `ix_item_granted_action_action`, so the action layer can answer *"what grants this"*.

#### (b) ⛔ `UpsertGrant` has zero production callers — the pipe is connected at the far end and nothing feeds it

This is the wiring gap, and it is precise:

| Half | State |
|---|---|
| **Write** — `RpgStore.UpsertGrant` (`RpgStore.Actions.cs:512`) | ⛔ **zero production callers.** Verified by grep: only `tests/FusionRpg.Data.Tests/ActionStoreTests.cs:249,259,273-275` and `ActionCatalogStoreTests.cs:156` |
| **Read** — `RpgStore.ListGrants` (`:538`) | ✅ **live in production** — `WebMatchService.EquippedActionIdsFor` (`WebMatchService.cs:390`) reads grants for `OwnerKind.Entity` + the specimen's instance id and feeds `GetLoadoutOrAutoEquip` |
| **Delete by source** (`:571`) | exists, no production caller |

**So this module is small and its shape is already decided by the shipped read path.** The build is:
module 4's assign/unassign transaction also writes and deletes `rpg_action_grant` rows, at
`OwnerKind.Entity` + the specimen instance id, with `source = the item's container_id` — matching the
scope `WebMatchService.cs:387-390` already reads and the `source` index that already exists (`:91`).

#### (c) ⛔ X3 — nothing produces actions, and its named owner has not accepted it

**`ActionSeeder.Generate` has zero production callers.** Verified by grep across `src/`, `tools/` and
`tests/`: every call is in `tests/FusionRpg.Core.Tests/Actions/ActionSeedingTests.cs` (`:85`, `:86`,
`:100`, `:127`, `:147`, `:159`), plus two doc-comment mentions (`Instantiator.cs:164`, `Resolver.cs:27`)
and two seedsmith comments. No production path turns a seed into a concrete action.

**A grant can only name an action that exists.** With no producer, `item_granted_action` would hold rows
pointing at a table nothing fills — SC7's *"a row no code consumes is not content; it is a lie in a
table"*, arriving from the other direction.

> ⚠ **X3 is an external dependency this module waits on.** `action-corpus` owns it and is building.
> **We do not track their progress from their documents, and we do not propose work inside their
> program** (D36).

~~The table below inspected `action-corpus-map.md` to argue the dependency was unowned. That inspection
was the boundary violation D36 corrects; it is kept struck rather than deleted so the reasoning error
stays visible.~~

| Where it would be | What is there |
|---|---|
| §7 *Cross-program dependencies* | Five rows — `OnActivate` on the lawn (E33), channel pools (E30), binding production, species anchors, the rung window. **No row for a production caller of `ActionSeeder.Generate`** |
| §8 *What stays out* | *"**The action runtime.** Shipped. This program authors content for it."* — the runtime, including the seeder's call site, is explicitly out of scope |
| §2 *What already exists* | `ActionSeeder.Generate → Instantiator.Draw` is listed **"built"** with no inertness note, while `Instantiator.TryInstantiate` on the next row *is* flagged *"built, inert — zero production callers"*. **So the seeder's inertness is invisible from that side too** |

✅ **RESOLVED 2026-09-04 — D36. Out of scope, and the earlier framing was a boundary violation.**

> **Owner:** *"action corpus is take care by other agent and building, it is not item scope, fix your
> boundary, avoid to touch other agent work."*

⛔ **This spec previously listed three options, two of which proposed changes inside `action-corpus`**
— amending its §8 exclusion, or having the item program build a call site in its runtime. **Neither was
ours to propose.** `action-corpus` is actively under construction by another owner; the observations
above about its map being silent on the seeder's inertness are **struck**, because they were written to
justify reaching across a boundary.

**The correct posture, and the only one:**

| | |
|---|---|
| What we do | **consume `ActionSeeder.Generate` when action-corpus ships a production caller.** Nothing more |
| What we do not do | build a caller, amend their map, file a row in their program, or infer their schedule from their docs |
| If it never lands | this module ships **DDL + validator + zero content rows** (gate GA2 below) — honest, useful, and not pretending the seam is live |

**X3 therefore has no owner-decision attached and never needed one.** It is an ordinary external
dependency: we wait, and the module's build order accommodates the wait.

⛔ **We do not read `action-corpus-map.md`'s approval state, module list or schedule to reason about
our own work.** That program is under active construction by another owner; its documents are theirs to
change, and inspecting them to infer whether our dependency will land is the same boundary violation in
a quieter form (D36).

### Gates — what ships in what order, unchanged from §5.6 and now checkable

| Gate | Ships | Proof it is real | Blocked by |
|---|---|---|---|
| **GA1** | ✅ **already done** — `rpg_action` with flags (2) and (3), and the assembly entry point returning intrinsic-only | an actor with no items has exactly one action, and it is the species' basic attack | — |
| **GA2** | `item_granted_action` DDL + validator + reason codes. **Zero content rows** | a planted bad row is rejected by id with its rule — a validator with no planted-violation test is an untested validator | module 6 `base-types` |
| **GA3** | **One** weapon base type with `grant_role = 'default-attack'`, driven through a battle | the actor's attack changes because of the item, visibly, in the battle trace | ⛔ **X3** |
| **GA4** | The `granted` role. Cap, dedup and two-items-one-action get their first real test | two items granting the same action produce **one** entry in the set | ⛔ **X3**, and handshake item 8 |

**GA2 is buildable today and GA3 is not.** That is the honest split, and it is what makes option 3 above
a real answer rather than a shrug.

### The four rules the assembler already enforces, and the one it does not

`ActionSetAssembler` (`Grants/ActionSetAssembler.cs`) is pure and already implements §3.7's answers:
dedup by `action_id`, `default-attack` replacing the species intrinsic, and a refusal when a grant
declares `default-attack` for an action whose `DefaultAttackEligible` is false (`:80-85`).

⛔ **The cap is not enforced anywhere** — `:30` says so outright. §3.7(d) is explicit that the failure
mode is *truncation*, and that the answer is **reject at bind, never truncate**, because truncation makes
an equipped item silently do nothing. The number is the action layer's call (handshake item 8, proposed
8, `default-attack` never counting against it). **This module contributes the requirement and a test,
not the enforcement** — the same pattern `CrossProgramLandedFlags` already uses for P0.2–P0.5.

### `default-attack` is `armament-primary` only — a tightening of I2, flagged

§4.3 option (C): the replacement role is legal **only** on `armament-primary`, so the 1H + off-hand
conflict is *unrepresentable* rather than *arbitrated*. I2 asserted the column is legal on both armament
roles (`ssot-equip-slots.md:205`); this narrows it. That is a tightening of I2's assertion, not a
contradiction of its principle (*"a weapon supplies numbers and legality; it never supplies
activation"* — the item still supplies only a string).

⚠ **And the content-budget mitigation is a rule, not a schema field:** `default-attack` is authored
**per weapon class per frame** — roughly 3 × 2 = 6 actions — never per base type, which would be 344
hand-authored actions and an unaffordable budget invented by accident.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~GrantSeam"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ItemGrantedAction"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ActionStore"

# X3's own check: does anything in src/ call the seeder yet?
rg -n "ActionSeeder\.Generate" src\
```

## Project structure

```text
src/FusionRpg.Core/Items/Grants/ItemGrantedActionRow.cs   new — the six columns
src/FusionRpg.Core/Items/Grants/ItemGrantValidator.cs     new — the content + cross-row checks, plus
                                                             R2's import-time budget call into
                                                             ItemPowerReads (module 9)
src/FusionRpg.Core/Items/Grants/EquippedGrantProjection.cs new — assign/unassign -> UpsertGrant /
                                                             delete-by-source. THE WIRING GAP (b)
src/FusionRpg.Data/Sqlite/RpgStore.ItemGrants.cs          new — item_granted_action DDL
src/FusionRpg.Core/Items/Grants/ItemGrantLandedFlags.cs   new — const bool ActionCorpusProducerLanded
                                                             = false (X3) and MidRunEquipLanded = false
                                                             (handshake item 7), mirroring
                                                             CrossProgramLandedFlags
src/FusionRpg.Core/Items/Grants/GrantRemovalPolicy.cs     new — the per-TurnState table, item 7. Pure,
                                                             no kernel edit, unreachable until mid-run
                                                             equip exists
tests/FusionRpg.Core.Tests/Items/ItemGrantedActionTests.cs new
```

`rpg_action_grant`, `ActionSetAssembler`, `FrozenActionSet`, `ActionValidator.ValidateGrant` and
`RpgStore.UpsertGrant` are **shipped — verify and call, do not rebuild.**

## Code style

```csharp
// The scope is not a choice: WebMatchService.EquippedActionIdsFor (WebMatchService.cs:387-390) already
// READS grants at OwnerKind.Entity + the specimen's own instance id, and says why -- "two specimens of
// the same species held by one player can carry different loadouts". Writing at any other scope would
// produce rows the shipped reader never sees. `source` is the item's container id so unassign is a
// delete-by-source against the index that already exists (RpgStore.Actions.cs:91).
static ActionGrantRow GrantFor(ItemAssignmentRow a, ItemGrantedActionRow g) =>
    new(OwnerKind.Entity, a.SpecimenInstanceId, g.ActionId,
        Source: a.ContainerId, GrantRole: g.GrantRole);
```

## Testing strategy

| Test | Asserts |
|---|---|
| `equipping_an_item_with_a_grant_row_writes_one_action_grant` | the wiring gap (b), closed — `UpsertGrant` gains its first production caller |
| `unassigning_deletes_by_source_and_leaves_other_grants` | delete-by-source, over two items granting different actions |
| `the_grant_scope_matches_what_WebMatchService_reads` | `OwnerKind.Entity` + instance id — asserted against the shipped reader, not against a constant |
| `two_items_granting_one_action_produce_one_set_entry` | §3.7(a), through the **shipped** assembler |
| `removing_one_of_two_sources_leaves_the_action` | provenance is rows; the set is a group-by |
| `an_item_granting_an_action_the_species_already_has_is_reported_not_swallowed` | §3.7(b) — dedup, plus an "already known" flag module 20 renders |
| `default_attack_replaces_the_species_intrinsic` | precedence is declared, not emergent |
| `default_attack_is_refused_on_any_role_but_armament_primary` | §4.3(C) — the conflict is unrepresentable |
| `a_grant_naming_a_non_grantable_action_is_refused_at_import` | `ActionNotGrantable`, at import — never discovered at runtime |
| `a_default_attack_grant_on_an_ineligible_action_is_refused` | `ActionNotDefaultAttackEligible` — the shipped refusal at `ActionSetAssembler.cs:82-85` |
| `at_most_one_default_attack_per_container` | the child table's own constraint |
| `the_item_side_carries_no_cooldown_cost_target_or_condition_column` | §5.3's Never list, **as a schema test over the DDL text** |
| `the_granted_cap_rejects_rather_than_truncates` | the requirement; `Skip` with a reason while handshake item 8 is open, and the reason names it |
| `display_order_is_role_ordinal_then_seq_then_action_id` | ordinal comparison, never a generated id |
| `an_inventory_event_never_becomes_an_InterruptCause` | the kernel refusal, as a guard on `ActionRunner`'s enum |
| `cooldown_survives_unequip_and_re_equip` | `CooldownLedger` keys on `(ActorKey, Slot)`, not on the item — the swap exploit is closed for free |
| `removal_in_charging_or_ready_drops_the_action_from_the_selectable_set` | ⭐ item 7, rows 1–2 |
| `removal_in_committed_or_resolving_lets_the_action_complete` | ⭐ item 7 — *"committing is what costs, not landing"*; no refund path |
| `removal_in_recovering_applies_at_the_transition_to_charging` | the only edge out |
| `removal_while_downed_is_recorded_and_survives_a_revive` | `Downed → Charging` is legal — a revive must not resurrect a removed grant |
| `a_granted_action_over_its_rarity_ceiling_is_refused_at_import` | ⭐ R2 — `GrantedActionOverBudget`, with the band |
| `an_action_with_no_resolvable_rung_is_refused_as_unpriced_never_zero` | R2's dominance answer, enforced rather than reported |
| `X3_is_unresolved_and_the_flag_says_so` | `ItemGrantLandedFlags.ActionCorpusProducerLanded == false`, and GA3's tests are skipped **against that flag**, never silently absent |
| `mid_run_equip_is_unlanded_and_the_FSM_tests_skip_against_the_flag` | item 7's contract is written and inert — skipped by flag, never quietly missing |

## Boundaries

**Always:** store an identifier and a role, and nothing else. Write the grant at
`OwnerKind.Entity` + the specimen instance id with `source = container_id`. Assemble through the
shipped `ActionSetAssembler`. Freeze the set at run start. Reject rather than truncate. Carry a
`battle-only` presentation tag so module 20 can render it (an item whose headline property silently does
nothing in the mode the player is standing in is `status.expose.*` moved to the UI).

**Ask first:** **Adding `6` to [item-map.md](../item-map.md) §4 row 19** (a one-line map edit; this
module reads `item_base_type`). **The granted cap and its number** (handshake item 8). **Recording in `decisions.md` that the item
side of a grant is a reference and a role, never a definition** (handshake item 9) — a doc change with
no code that stops `A1` negotiating the seam mid-build. Amending I2's *"legal on both armament roles"*
(§9.3, R4's).

**Never:** ⛔ **per-grant overrides.** §4.1 option (C) is refused by name because it is the option a
reasonable person will propose later for a good reason: it is option (B) arriving one column at a time,
and by the third override the item table is a partial copy of `rpg_action` with none of its validation.
Never put an action's atoms in the item's container — one container cannot hold two populations with two
ordering laws and stay debuggable. Never put the grant on an instance. Never let an inventory event
become an `InterruptCause`. Never implement the action-set merge here.

## Success criteria

- [ ] `item_granted_action` exists with **exactly six columns**, and a schema test proves none of §5.3's
      forbidden names appears in the DDL.
- [ ] Equipping writes an `rpg_action_grant` row and unequipping deletes it by `source` — **`UpsertGrant`
      has a production caller for the first time**, at the scope `WebMatchService.cs:390` already reads.
- [ ] Two items granting one action produce one set entry, through the shipped assembler; this module
      implements no merge of its own.
- [ ] `default-attack` is refused on every role but `armament-primary`, and at most one per container.
- [ ] A grant naming a non-grantable or default-attack-ineligible action is refused **at import**.
- [ ] **X3 is recorded as an ordinary external dependency** — in this spec and in `item-map.md` §3.
      **No request is filed against `action-corpus`** (D36). GA3 and GA4 wait for a production caller to
      exist; they do not wait for an answer, because no question was asked.
- [ ] GA2 ships standalone: DDL, validator, tests, **zero content rows**, and the module says so rather
      than authoring rows that point at an empty table.
- [ ] `ssot-granted-actions.md` §3.6's runtime matrix and §5.6's reasons 1 and 3 are corrected in the
      lane, with `AtomKindRegistry.cs:217/255/290/344/396` and `RpgStore.Actions.cs:22` cited — and the
      board-kind count stated as **five**, not six.
- [ ] ⭐ **Handshake item 7 has an owner and a written table**: per-`TurnState` removal semantics, the
      three invariants, four tests skipped against `MidRunEquipLanded`, and a `decisions.md` request.
- [ ] ⭐ **R2 is enforced at import**, not merely reported: over-ceiling refuses with
      `GrantedActionOverBudget`, and an action with no rung refuses as `unpriced` — never priced at zero.
- [ ] Module 19's dependency on module 6 is reconciled with [item-map.md](../item-map.md) §4 row 19.
