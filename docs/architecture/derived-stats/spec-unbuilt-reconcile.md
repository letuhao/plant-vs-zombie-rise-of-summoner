# Spec — `unbuilt-reconcile`

**Program:** `derived-stats` · **Map:** [../derived-stats-map.md](../derived-stats-map.md)
**Depends on:** every other module · **Runs last**
**Status:** Spec — awaiting review. Not built.

---

## 1. Objective

**Correct every document the 157 new channels invalidate, while correcting them is still free.**

This module adds **no mechanics**. It removes disagreement. Six subsystems are approved-but-unbuilt or
mid-build, and adding a catalog underneath them silently falsifies parts of their specs. After they
ship, the same correction is a rebalance.

It runs **last on purpose**: reconciling against a moving target means doing it twice.

**Two of the eight findings below are not "unbuilt" at all — they are shipped specs describing retired
math.** Those are worse, because they are read as current. They are in scope here for the same reason:
this is the module that will be looking.

---

## 2. The register

Every row was found by reading the file this session and verified against code where it makes a claim
about behaviour.

### F1 — `action-map.md` is about to invent a channel we own ⚠️ highest value

[action-map.md:177](../action-map.md): *"Our envelope has `SpeedChannel` but **no bounds and no
cooldown-reduction channel** — a real gap this program should close."* D3 (line 200) schedules adding
one to `ActionEnvelope`.

**`skill.cooldown.{category}` is that channel.** Two programs answering one question with two
mechanisms is the failure the power ladder was written to end.

**Fix:** repoint D3 at the catalog; the envelope gains a `CooldownChannel` *reference* mirroring
`SpeedChannel`. Mark :177's gap closed. Landing in
[spec-skill-modifiers.md](spec-skill-modifiers.md); verified here.

### F2 — `spec-defence-actions.md` called a stat an action ✅ already reconciled

A8's reaction shape was named **block** while this program specified `block.rate` as a passive stat.
**Resolved 2026-08-24**: A8's category is **guard** — an action granting a timed buff to defensive
channels; `block`/`parry` stay stats. They compose — guarding raises `block.rate`.

Both specs now carry the boundary and both assert the naming ban. **Verify the ban holds** at the end
of the program; a vocabulary collision that happened once will happen again.

### F3 — `element-hub-ssot.md` §6 is stale by 44 channels

A hand-written table ending *"Catalog size (v1): 40 combat derived channels."* Shipped reality is
**84** — missing `light`/`dark` (2026-08-21) and all four `combat.shield.*` families.

**Fix:** [spec-element-families.md](spec-element-families.md) replaces it with the generation rule and
a test that fails on drift. Verify the test exists — prose alone is what let it rot for three months.

### F4 — `status-ssot.md` §6 describes retired math (4 statements, code-verified)

| §6 says | Shipped | Evidence |
|---|---|---|
| `effectiveApplyScale = max(Floor, K × matchPower)` | `matchPower` **dropped** | `ResistanceEvaluator.cs:151` — *"T3.2 (audit F3): no longer scaled by matchPower"* |
| `ResistFromPowerRatio` *"ratio 0 v1 stub"* | **`1.0`** | `data/tuning/status.v1.json` |
| *"v1 hardcoded `progression.power = 1.0` stub"* | **`Θ`**, `0` un-hydrated | ADR P1 |
| *"`effectiveApplyScale = 100`"* follows from the above | — | same |

Landing in [spec-status-potency.md](spec-status-potency.md). **Shipped code, stale doc** — the worst
combination, since a reader has no signal it is wrong.

### F5 — two copies of the "Deferred from Chaos" list

[combat-damage-ssot.md](../combat-damage-ssot.md) §5 **and**
[element-hub-ssot.md](../element-hub-ssot.md) §6 both list `Penetration`, `Absorption`, `Reflection`,
`Parry*`, `Block*` as not-in-v1 — this program's entire subject.

**Fix:** both retitle to *"v1 shipped / v2 planned"*. R3 originally named only the first;
[spec-element-families.md](spec-element-families.md) §5 found the second. **Verify both.**

### F6 — the atom corpus roughly doubles, and it is not our scope

[atom-family-library.md §3.2](../effect-atom/atom-family-library.md) sizes `stat.derived` at
*"12 generated families (~420 rows)"* = `12 × 7 × 5`. At 28 families that is **~980**, and each of the
16 new families needs flavour names per element (*Ember / Frost / Gale / Stone / Radiant / Umbral*).

**Fix:** update the sizing, and **hand the authoring to the item corpus explicitly** — a named handoff,
not a number left to be discovered. `stat.derived` is also **quarantined (D6)** until **E12**, so the
channels are code-readable before they are content-bindable. Record it; do not block on it.

### F7 — `battle-turn-ideal.md`'s speed family is now classified

[:241](../battle-turn-ideal.md) reserves `speed` · `haste` · `moveSpeed` · `climbSpeed` · `swimSpeed` ·
`flightSpeed` · `jumpHeight`. All **`Race`** class — none ever needed a pair, which is now a stated
rule rather than an unexamined absence.

[:153](../battle-turn-ideal.md)'s `… / Speed` is a `Race` stat in a **divisor** and needs
[spec-stat-taxonomy.md](spec-stat-taxonomy.md) §2.4's floor — **structural, PS-8 exempt**, with the
overflow hazard inverted to small values.

**Fix:** cite the taxonomy instead of re-deriving. `turn.*` stay **unregistered** — the battle stream
registers them when it gives them a reader.

### F8 — `resource-hub-ssot.md` becomes buildable

Its channels stop being hypothetical when [spec-actor-channels.md](spec-actor-channels.md) lands.

**Fix:** update §8 to point at the registered ids, and mark §3G's *"four exhaustion debuffs stacking
has never been tested"* closed by that module's test — **or leave it open and say so.** Do not let a
known-untested case silently acquire a checkmark.

### F9 — a third doc has hardcoded channel counts, and the gate does not point at it

[design/spec-derived-stat-sheet.md](../../design/spec-derived-stat-sheet.md) §1 counts **84 / 99 / ~141**,
which become **196 / 256 / ~298** — the same trap as F3, in a document nobody in this program had read.

**Fix:** [spec-element-families.md](spec-element-families.md) §4.1 extends its drift test to cover this
file too. Two further corrections there:

- Its §3 unregistered list names **three** `turn.*` channels — itself a correction to
  `actor-hub-ssot.md` §11.4, which says two. With `move.range` it is **four** until
  [spec-actor-channels.md](spec-actor-channels.md) registers that one.
- **Add `spec-derived-stat-sheet.md` to [DESIGN-GATE.md](../../DESIGN-GATE.md)'s §1 *Stats* row.** It
  currently names only `stat-system.md` and `actor-hub-ssot.md`, so a session touching derived stats
  gets no pointer to the document that renders them — which is how this spec set nearly shipped
  believing there was no UI for 157 new channels. **The gate's own topic index is the defect here**,
  and it is a one-line fix.

**No UI work is owed.** All 157 land in the `no-producer` state that spec already defines and has copy
for; the grid renders 28 × 7 by construction.

### F11 — the unit ledger had no class for `Θ` ✅ resolved 2026-08-24

[design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md) §3's classes covered
every magnitude — but **`progression.power` is not a magnitude, it is an index.** `Θ` is read linearly
by contests and never rendered as points, units, a ratio or a flag, so none of the nine fits, and
`progression.realm` inherits the problem.

This is the **single most load-bearing derived channel in the game** carrying no unit class.

**Resolved.** Owner authorised the tenth class the same day, and it landed **in that spec**, not ours:
`LadderIndex` — authoritative `Θ 20`, context `→ 680 power`
([spec-magnitude-and-units.md §3.2](../../design/spec-magnitude-and-units.md)). It is the **only class
whose context part is exact rather than an estimate**, because `P(20) = 680` is the shipped pin, not a
sample against a reference specimen.

**One contract change is owed and is not this program's to make:** the `UnitClass` union in
[web/fusion-rpg-web/src/contract/types.ts](../../../web/fusion-rpg-web/src/contract/types.ts) gains
`"ladderIndex"`. Recorded here so it is scheduled rather than discovered.

### F10 — the double-clamped cap ✅ owned by `cap-consolidation`

The `0.95` resist cap is enforced twice — hardcoded at compose, tunable at apply — so
`categoryResistCap` **cannot raise it, only lower it**. Found during this audit, verified in shipped
code, and given its own module ([spec-cap-consolidation.md](spec-cap-consolidation.md)) because it must
land **before** 157 more capped channels multiply it. Listed here so the register is complete.

---

## 3. What this module must NOT do

Named because a reconcile module is where scope creep hides.

| Not this | Why |
|---|---|
| Add a mechanic | Every mechanic belongs to a module that specs it |
| Decide a deferred question | Primary stats, `element_mastery`, heal-element roster — all out of scope by the map §7 |
| Author atom rows | F6 hands off; it does not do the work |
| Register `turn.*` | Battle stream's, when it has a reader |
| Re-bless a golden | If one moves here, a *previous* module was wrong — go fix that one |

---

## 4. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Guard.Tests
.\scripts\guard-stat-pairs.ps1
.\scripts\guard-power.ps1
python scripts\audit-overflow.py
python scripts\audit-magic-numbers.py --summary
```

---

## 5. Testing strategy

Mostly documentation, so the tests are **consistency assertions**, not behaviour:

| Test | Asserts |
|---|---|
| `NoSpecClaimsAnUnregisteredChannel` | Every `combat.*` / `status.*` / `resource.*` / `progression.*` id appearing in `docs/architecture/**` either resolves or sits under a heading marked PROPOSED. **The check that would have caught F3 and F5 automatically** |
| `NoBlockOrParryInActionModule` · `NoGuardInEvasionModule` | F2's naming ban, both directions |
| `Section6MatchesGeneration` | F3's drift test still green |
| `DeferredListsRetitled` | Neither doc still lists this program's subject as banned (F5) |
| `AtomFamilyCountMatchesCatalog` | F6's `~420` tracks `CombatChannelFamilies`, not a frozen literal |
| **`git status tests/` clean** | Documentation module. A moved golden means an earlier module was wrong |

`NoSpecClaimsAnUnregisteredChannel` is the one worth building properly — it converts this whole module
from a one-time sweep into a standing guard, and both F3 and F5 are exactly what it catches.

---

## 6. Success criteria

- [ ] All eight findings resolved or explicitly deferred **with a reason**.
- [ ] F1: D3 repointed; `action-map.md:177` marked closed.
- [ ] F2: both specs carry the boundary; naming ban asserted **both ways**.
- [ ] F3: §6 generated, drift test green.
- [ ] F4: four corrections landed **with their code citations**.
- [ ] F5: **both** deferred lists retitled.
- [ ] F6: sizing updated; corpus authoring handed off **by name**; E12 dependency recorded, not blocked on.
- [ ] F7: speed family classified `Race`; divisor floor cited, not re-derived.
- [ ] F8: resource-hub updated; the exhaustion-stacking gap **either closed by test or still marked open**.
- [ ] `NoSpecClaimsAnUnregisteredChannel` shipped and green.
- [ ] `git status tests/` clean.

---

## 7. Open questions

**None of this module's own.** Every finding has a fix, and the two questions the program still carries
— [spec-healing-pair.md](spec-healing-pair.md) §9's §4.3 reading, and
[spec-actor-channels.md](spec-actor-channels.md) §9's exhaustion tuning — belong to those modules and
are answered before this one runs.
