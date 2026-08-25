# Spec — `healing-pair`

**Program:** `derived-stats` · **Map:** [../derived-stats-map.md](../derived-stats-map.md)
**Depends on:** `catalog-extension` · **Parallel with:** the rest of the band
**Status:** Spec — awaiting review. Not built.

> **Module name kept, scope narrowed.** Owner decision 2026-08-24: `heal.power` ships **unpaired**.
> The name is retained because every downstream artifact references it; the "pair" is now
> `heal.power` and the **status** that suppresses it (§2.2), not a second channel.

---

## 1. Objective

**Make healing scale with the power ladder, and finish the atom that has been half-built since the
catalog shipped.**

One channel:

| Channel | Class | Cap |
|---|---|---|
| `combat.heal.power` | **`Pool`**, magnitude, `long` | **none** — a magnitude, PS-8 |

Grepped `src/`: **zero `heal*` derived channels exist.** `lifesteal` is an atom only
([atom-family-library.md:132](../effect-atom/atom-family-library.md)), and
[atom-catalog-ssot.md:113](../effect-atom/atom-catalog-ssot.md) records `leech` as
**Partial — *"damage half only — the heal half was never built"*.**

Without this channel healing is a flat number in a game where every other magnitude rides `P(Θ)`. In
an endless grind that is a mechanic that decays to irrelevance — the failure "one power ladder" exists
to prevent, arriving by omission rather than by a private curve.

---

## 2. `Pool`, not `Contest` — and why that is the correct classification

`heal.power` is the healer's **own output capacity**. It meets no opposing value in a roll or a delta.
That is the definition of `Pool` in [spec-stat-taxonomy.md](spec-stat-taxonomy.md) §2.1, and the
shipped precedent is exact: `combat.shield.capacity` and `combat.shield.regen` are the owner's own
and have never carried counterparts.

**H.0's pairing requirement binds `Contest` only, so nothing is being waived.** `guard-stat-pairs.ps1`
must pass with `heal.power` declared `Pool` and no counterpart — and a test asserts that, so a future
reader does not "fix" it by inventing one.

### 2.1 This dissolves the §4.3 question rather than answering it

An earlier draft proposed `heal.power − heal.reduction` and flagged that the defender-side term might
reopen [combat-damage-ssot.md](../combat-damage-ssot.md) §4.3's locked boundary — *"Funnel transport
only, no matchup/hit/crit."*

**With no defender term there is no delta on the heal path at all.**

```text
effectiveHeal = baseOverlayHeal + heal.power(healer)
              → signedAmount = +effectiveHeal → Funnel → FA10
```

One mailbox, unchanged. No matchup, no roll, no opposed term — strictly less than §4.3 bans. **§4.3 is
untouched and no `decisions.md` amendment is owed.** The open question this spec carried is closed by
the classification, not by a reading.

### 2.2 Anti-heal stays expressible — as a status

The counter to a `Pool` is a **status**, the same resolution Q4 gives root and drain. A "grievous
wounds" status suppresses incoming healing; it needs no channel, and it keeps **one** way to say
"reduce incoming healing" instead of two.

**Not built here.** This module ships the channel; the status is content the status stream authors when
a design calls for it.

### 2.3 Flat, not element-typed

Q5. Element-typing healing needs a **heal-element sub-roster** — light and dark plausibly heal, fire
does not — which is a different roster from the 7-slot combat axis, and *that* would genuinely reopen
§4.3.

---

## 3. Finishing `leech`

`leech` declares both halves and ships one. The heal half lands here because this is the module that
creates something for it to scale.

| | |
|---|---|
| Today | damage applies; the heal is absent |
| After | the heal half emits `+effectiveHeal` through the §2.1 path |
| Constraint | **a separate signed packet, not a negative damage.** §4.3's one-mailbox rule is about the mailbox, not about folding two effects into one packet |

**`lifesteal` is deliberately untouched.** It is an `OnDamageDealt` atom with real item rows behind it
([ssot-affixes.md:1006](../item/ssot-affixes.md) `atom.lifesteal.t3`). Rewiring it onto `heal.power`
would move item behaviour — a corpus change, not a channel one. It reads the channel when the item
stream chooses to; this module does not force it.

### 3.1 The dependency that gates content, not code

`stat.derived` is **quarantined (D6)** until the atom program's **E12** wires `BattleStatComposer`. So
`heal.power` will be registered, composable and readable by code, but **not authorable as an atom that
binds** until then.

This module does **not** block on E12 and must not claim to. `leech`'s heal half is a *runtime* payload,
not a `stat.derived` atom, so it lands regardless.

---

## 4. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Heal|FullyQualifiedName~Leech"
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-stat-pairs.ps1
```

---

## 5. Project structure

| Path | Change |
|---|---|
| `src/FusionRpg.Core/Combat/DamageApplyPipeline.cs` | `+ heal.power` on the heal branch |
| `src/FusionRpg.Core/Status/…` (`leech` payload) | the heal half (§3) |
| `docs/architecture/combat-damage-ssot.md` §4.3 | record that the addition is healer-side only and the boundary is unchanged |
| `data/seed/derived-stats/catalog.json` | `class: Pool`, `role: healer`, **no counterpart** |

---

## 6. Testing strategy

| Test | Asserts |
|---|---|
| **`NoGoldensMoveAtZero`** | `heal.power = 0` → every heal identical. The no-op proof |
| `HealPowerScalesHeal` | The channel raises the applied amount, `long` throughout |
| `HealIsPoolNotContest` | `guard-stat-pairs.ps1` passes with **no counterpart declared** — and a planted `Contest` reclassification **fails**, so the classification is load-bearing rather than incidental |
| `HealStillOneMailbox` | Still `+signedAmount → Funnel → FA10`; `guard-funnel-delta.ps1` green |
| `NoMatchupNoHitNoCrit` | The heal path consults none of §4.3's three names — §2.1 made falsifiable |
| `HealNeverNegative` | A heal floors at zero; **an overlay heal can never become damage** |
| `LeechHealsAndDamages` | Both halves fire; the heal is a **separate signed packet** |
| `HealIsNotNegativeDamage` | Absolute HP never emitted from an overlay snapshot — invariant 3 |
| `LifestealUnchanged` | Shipped item behaviour byte-identical (§3) |

---

## 7. Boundaries

**Always** — one mailbox, signed deltas. Floor the heal at zero. Keep the channel flat and `Pool`.

**Ask first** — element-typing healing (needs the heal-element roster, §2.3). Rewiring `lifesteal`
(moves shipped item behaviour, §3).

**Never** — add a `heal.reduction` channel; anti-heal is a status (§2.2). Cap `heal.power` — it is a
magnitude. Emit absolute HP from an overlay snapshot (invariant 3). Add matchup, hit or crit to the
heal path — that is the actual §4.3 ban.

---

## 8. Success criteria

- [ ] `combat.heal.power` live, classified **`Pool`**, uncapped, `role: healer`, **no counterpart**.
- [ ] `guard-stat-pairs.ps1` green unpaired; a planted `Contest` reclassification fails.
- [ ] `git status tests/` clean at zero.
- [ ] Heal path provably free of matchup/hit/crit; `guard-funnel-delta.ps1` green.
- [ ] **`leech` heals** — the half declared since the catalog shipped, as a separate signed packet.
- [ ] `lifesteal` behaviour unchanged.
- [ ] §4.3 records that the boundary is **unchanged**, so the next reader inherits the reasoning rather than re-deriving it.

---

## 9. Open questions

**None.** The §4.3 reading this spec previously carried was **dissolved by the owner's unpaired
decision** (§2.1) — with no defender-side term there is no delta on the heal path, so the boundary was
never at issue.
