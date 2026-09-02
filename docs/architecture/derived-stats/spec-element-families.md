# Spec — `element-families`

**Program:** `derived-stats` · **Map:** [../derived-stats-map.md](../derived-stats-map.md)
**Depends on:** `catalog-extension` · **Unblocks:** `mitigation-chain`, `evasion-chain`, `reflection`
**Status:** Spec — awaiting review. Not built.

---

## 1. Objective

**Give the 16 new element-typed families their semantics, and stop §6 from drifting again.**

`catalog-extension` registers the ids. This module answers what each *means* against the matchup
matrix — the split [decisions.md](../decisions.md) locks: *"Element Hub owns matrix semantics; Actor
Hub registers channels."*

### 1.1 §6 has already drifted, by 44 channels

[element-hub-ssot.md](../element-hub-ssot.md) §6 is a **hand-written table** ending
*"Catalog size (v1): 40 combat derived channels."* Shipped reality is **84**. It is missing `light`
and `dark` (added 2026-08-21) and all four `combat.shield.*` families (approved 2026-08-21).
[actor-hub-ssot.md](../actor-hub-ssot.md) §3E already carries the correction and says why:

> *"The list is **generated, never hand-listed** — adding an element or a family changes the count by
> construction, which is why the assertion is on the generated total rather than on a literal list."*

§6 is the counter-example living in the same doc set — a hand-listed table that has been wrong for
three months. **This module replaces it with the generation rule.** Restating it as another literal
table of 196 rows would reproduce the defect at 2.3× the size.

---

## 2. What each family means

All eight pairs are **`Contest`** class, resolved as differences with **neither half capped**
([spec-stat-taxonomy.md](spec-stat-taxonomy.md) §2.2).

| Pair | Contest over | Where in §6.7 | Role |
|---|---|---|---|
| `penetration ↔ absorption` | damage surviving mitigation | with the delta | standard |
| `amplification ↔ reduction` | final damage multiplier | **after** mitigation | standard |
| `parry.break ↔ parry.rate` | is the hit parried | **before** the delta | inverted |
| `parry.shred ↔ parry.strength` | how much a parry removes | before the delta | inverted |
| `block.break ↔ block.rate` | is the hit blocked | before the delta | inverted |
| `block.shred ↔ block.strength` | how much a block removes | before the delta | inverted |
| `reflect.resist.rate ↔ reflect.rate` | does damage bounce | after damage is final | inverted |
| `reflect.resist.damage ↔ reflect.damage` | size of the bounce | after damage is final | inverted |

**Inverted** = the *defender* owns the half that raises an outcome. The rule holds (one raises, one
lowers, same quantity); only ownership flips. Per Q2 the genre names stay and the seed catalog's
`role` field carries the truth for tooling.

**Exact placement in the pipeline is `mitigation-chain` and `evasion-chain`'s to specify.** This
module fixes only the column above — *which side of mitigation* — because that is what decides
`Contest` vs `Feeder` and therefore whether the pair is required at all.

### 2.1 Omni stays additive-only **[Ban removed 2026-09-02 — see `element-hub-ssot.md` §7; the omni combination is a tunable, default still additive.]**

`totalX = X.omni + X.{element}` for every new family, exactly as §7 locks for the shipped eight.
~~The bans extend unchanged~~ **Bans removed 2026-09-02** — the default stays `omni + element` for penetration, parry, block and reflection.

### 2.2 Matchup interaction — the one real design question, answered

Do the new families read the matchup matrix?

> **No. Only `power`/`defense` consume `componentBonus`.** §6.2's `matchupBonus` is already folded into
> `weightedDelta`; letting penetration or parry read the matrix too would apply typed advantage
> **twice** to one hit.

This preserves §8.6's authority rule and keeps the matrix a single-application concern. A fire attacker
against an ice defender gets its advantage once, in the delta — not again in the parry contest.

### 2.3 The shield matrix is not the combat matrix

§13's ban lifted for shields via the Shield layer row, and
[shield-system-spec.md](../shield-system-spec.md) ships a **separate `ShieldElementMatrix`** —
asymmetric with the combat ring, which [DESIGN-GATE.md](../../DESIGN-GATE.md) names as the thing
sessions get wrong about elements. **`block.*` reads neither matrix** (§2.2), so the question does not
arise for it — stated because "block is shield-like" would otherwise suggest it should.

---

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Element"
.\scripts\guard-stat-pairs.ps1
.\scripts\guard-power.ps1
dotnet test tests\FusionRpg.Core.Tests
```

---

## 4. Project structure

| Path | Change |
|---|---|
| `docs/architecture/element-hub-ssot.md` §6 | **replace the 40-row table with the generation rule** + the family list (§1.1) |
| `docs/architecture/element-hub-ssot.md` §6 "Deferred from Chaos" | **second copy of the R3 list** — see §5 |
| `docs/architecture/element-hub-ssot.md` §7 | extend the omni ban list to the new families |
| `data/seed/derived-stats/catalog.json` | `role` populated per family (Q2) |

**No code.** `catalog-extension` did the registration; this module is semantics and documentation.

### 4.1 A second doc has the same hardcoded-count defect

[design/spec-derived-stat-sheet.md](../../design/spec-derived-stat-sheet.md) §1 is a counted table —
**84 / 99 / ~141** — which becomes **196 / 256 / ~298**. Same trap as §6, so it gets the same fix: the
drift test covers **both** documents, because one test that guards two counts is cheaper than two that
guard one each and drift apart.

Two smaller corrections in the same file, both already right and now merely incomplete:

- Its §3 unregistered list names **three** `turn.*` channels — correcting `actor-hub-ssot.md` §11.4,
  which said two. With `move.range` it is **four**, until
  [spec-actor-channels.md](spec-actor-channels.md) registers that one and it drops back to three.
- **`spec-derived-stat-sheet.md` is not in [DESIGN-GATE.md](../../DESIGN-GATE.md)'s §1 topic index.**
  The *Stats* row names `stat-system.md` and `actor-hub-ssot.md` only, so a session touching derived
  stats gets no pointer to the document that renders them — which is exactly how this spec set nearly
  shipped believing there was no UI. **Add it to the Stats row.**

**No UI work is owed.** The sheet already renders *"what the snapshot holds, laid out by a rule, not by
a list"*, so 28 families × 7 slots renders by construction, and all 157 new channels land in the
`no-producer` state that spec already defines and already has copy for (*"nothing grants this yet"*).

---

## 5. R3 applies to **two** documents, not one

[actor-hub-ssot.md](../actor-hub-ssot.md) §H.8 R3 names `combat-damage-ssot.md` §5's *"Deferred from
Chaos"* list. **[element-hub-ssot.md](../element-hub-ssot.md) §6 carries a second copy** —
`StatusProbability`/`StatusDuration`/`StatusIntensity`, `ElementPenetration`, `ElementAbsorption`,
`ElementReflection`, `Parry*`, `Block*` — with the justification *"those extra combat families would
widen the surface too early."*

Both get the same treatment: retitle to **"v1 shipped / v2 planned"** and move the five, preserving
the v1 record that attributes a moved golden. Missing the second copy would leave the element SSOT
telling the next reader that this program's whole subject is banned.

---

## 6. Testing strategy

| Test | Asserts |
|---|---|
| `Section6MatchesGeneration` | A test reads the §6 family list and asserts it equals `CombatChannelFamilies` — **the doc cannot drift again silently**. This is the fix for §1.1, not the prose |
| `StatSheetCountsMatchGeneration` | The same test, second target — see §4.1 |
| `OmniAdditiveForNewFamilies` | Every new family composes `omni + element` — planted `omni × element` fails |
| `MatchupAppliedOnce` | A typed hit with non-zero penetration and parry applies `componentBonus` exactly once (§2.2) |
| `NoGoldenMoves` | Semantics only; nothing reads these yet |

`Section6MatchesGeneration` is the load-bearing one. A doc correction that nothing enforces is how §6
got 44 channels out of date in the first place.

---

## 7. Boundaries

**Always** — generate, never hand-list. Omni additive-only. **[Ban removed 2026-09-02 — `element-hub-ssot.md` §7; combination is tunable, default additive.]** Cite §8.6 when touching matrix authority.

**Ask first** — any new family reading the matchup matrix (§2.2 says none do). Adding a 7th element
(free by generation, but Element Hub's decision).

**Never** — apply `componentBonus` twice to one hit. Let `block.*` read `ShieldElementMatrix` (§2.3).
Restate the channel list as a literal table.

---

## 8. Success criteria

- [ ] §6 states the **generation rule**, not a channel table; `Section6MatchesGeneration` passes and fails on a planted drift.
- [ ] §6's stale *"40 combat derived channels"* gone.
- [ ] **Both** deferred lists retitled (§5).
- [ ] All 8 pairs classified `Contest`, both halves uncapped, `role` populated.
- [ ] §7's omni bans cover the new families.
- [ ] `git status tests/` clean.

---

## 9. Open questions

**None.** §2.2 was the only real one and it answers itself: applying the matchup twice is a defect, not
a balance choice.
