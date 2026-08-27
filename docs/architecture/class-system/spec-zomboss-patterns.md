# Spec: `zomboss-patterns` — the class layer, moved off the player and onto the AI

**Module id:** `zomboss-patterns` · **Program:** [class-system-map.md](../class-system-map.md) ·
**Status: AUTHORIZED 2026-08-26 -- owner's /goal directive commands execution of the class-system plan to completion; supersedes this "awaiting owner review" header, which was never flipped after that directive landed.**

**Depends on:** `aptitude-resolve` · **Blocks:** nothing

---

## 1. Objective

Ship the nine named allocations that make an opponent **readable**, as **content** resolved by id.

**The player has no class** (owner, 2026-08-25). Classes survive here, and the inversion is the point:

| | On the player | On the AI |
|---|---|---|
| Its value | **negative** — it forbids builds they can see | **positive** — it makes an opponent readable |
| Removing it costs | nothing | **the player's ability to learn the game** |

[world/spec-ai-commander.md](../world/spec-ai-commander.md) already settled this at the strategic
layer, in these words: *"He needs to be **legible** first; a blind opponent that visibly acts on old
information is more interesting than a sharp one, and it is the only version that can be tuned."* **A
Zomboss pattern is that principle one layer down** — a combat build the player can read, name, and
prepare for.

**Users:** encounter authoring; the player, indirectly and importantly.

**Success is measurable:** nine patterns resolve by id, each generates a complete allocation at any
`Θ` with no per-level authoring, and an unknown id is a startup error rather than a silent default.

---

## 2. Four things a pattern buys that a random allocation does not

1. **It teaches the cycle.** The player learns FORCE → BASTION → FINESSE by *fighting* it. Three arrows
   are unlearnable against opponents whose builds are noise.
2. **It gives a generator a shape.** One pattern varies into a Zomboss at any `Θ` — **the shares are
   fixed and `P(Θ)` supplies the scale**. No per-level authoring, and that is PS-3 doing the work.
3. **It makes a counter-build a real decision.** *"This one parries — bring guard-breaks"* is a decision
   only if the pattern is stable enough to recognise, and fair only if it is announced **before** the
   fight rather than discovered during it.
4. **It is the anti-cheat on difficulty.** A pattern is an allocation from **the same finite pool the
   player draws on**, so a harder Zomboss is a *higher `Θ`* or a *better allocation* — never a stat
   nobody could have had. Difficulty stays inside the rules.

**Point 4 is the one worth defending in review.** It is what stops "make it harder" becoming "give it
numbers the player cannot reach", which is the failure every difficulty knob eventually offers.

---

## 3. The roster — 3 pure + 6 mixed = 9

Nine sits in the **5–9 band** every shipped game uses for a base tier (GW2 9, PoE 7, Diablo 2 7, Lost
Ark 5). The band is worth matching for the reason it exists: **it is how many distinct opponents a
player can hold in their head.**

| Mix | Reads as | What the player should bring |
|---|---|---|
| FORCE-defence + BASTION-breaks | armoured counter-puncher — soaks, then lands unerring crits | crit denial, penetration |
| FINESSE-defence + FORCE-breaks | evasive guard-breaker — never hit, smashes through blocks | accuracy, not guard |
| BASTION-defence + FINESSE-breaks | parrying armour-piercer | guard-break, not mitigation |

> **⚠️ A self-cancelling pattern must not be authored.** A pattern taking a defence **and** the break
> that beats it — BASTION-defence + FORCE-breaks, i.e. guard *and* guard-breaking — spends points
> against itself. On a player that would be a trap worth banning; **on a Zomboss it is simply a bad
> pattern**, which is a much cheaper problem: a content review, not a rule. Test 5 catches it.

### 3.1 Elements stay off pattern identity

Adding a seventh element is **free in the catalog** (channels are roster-generated —
[decisions.md](../decisions.md) *Element Hub SSOT*: *"the count is derived, not fixed"*) but
**quadratic in any class system keyed on elements**. Posture-shaped patterns, with element chosen per
Zomboss, keeps that cost at zero.

This is the same rule as ideal §4.1's: **a pattern is a mechanism, an element is a flavour.**

---

## 4. Where a pattern lives — and the catalog it must not be merged into

**Not in this program's code, and not in `aptitudes.v{n}.json`.** A pattern is **content** — a named
allocation, like a zombie type — so it lives in seed data beside the roster it draws on, and the AI
resolves it by id the way [FactionPolicies.Resolve](../../../src/FusionRpg.Core/World/Ai/FactionPolicies.cs)
already resolves a strategic policy: known ids only, unknown rejects.

**Copy that file's shape, including its rationale**
([FactionPolicies.cs:27-33](../../../src/FusionRpg.Core/World/Ai/FactionPolicies.cs)):

> *"Throws rather than returning null. A null would read as 'this faction has no brain', which is
> indistinguishable from the human — and a typo would then look like a design decision for the rest of
> the campaign."*

The same is true of a pattern: a null reads as "this Zomboss has no build".

> **Two different catalogs, deliberately.** `PolicyId` decides *what a faction does on the map*; a
> pattern id decides *what a body is made of*. **Collapsing them would make "cautious" and "armoured"
> the same axis** — and `WorldValidation` already checks every world against `FactionPolicies.All`,
> which is the validation shape to copy rather than the list to extend.

### 4.1 Newly symmetric with the player

A pattern is an allocation at the type / aspect tier — **which is exactly what an authored enemy is**,
now that [spec-point-economy.md](spec-point-economy.md) gives the player allocation at those same
tiers. The AI and the player run the same machinery, from the same pool, through the same resolver.

That symmetry is what makes point 4 of §2 true rather than aspirational.

---

## 5. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter ZombossPattern
dotnet test tests\FusionRpg.Core.Tests --filter WorldValidation   # the validation shape being copied

cd tools\CombatSim
dotnet run --no-build -- trinity --actions basic -a force --theta 100    # do the nine cycle?
```

---

## 6. Project structure

```text
data/seed/zomboss/patterns.json                          the nine, as content
src/FusionRpg.Core/Battle/Ai/ZombossPatterns.cs          Resolve(id) - throws, never null
tests/FusionRpg.Core.Tests/Battle/Ai/ZombossPatternTests.cs
```

**Shares, never point counts.** A pattern authors *proportions*; the budget at a given `Θ` comes from
`point-economy`. Authoring counts would make every pattern `Θ`-specific and re-create the per-level
authoring §2 point 2 exists to avoid.

---

## 7. Code style

```csharp
/// <summary>
/// In ordinal id order, so anything that enumerates patterns is reproducible.
/// </summary>
public static IReadOnlyList<string> All { get; }
```

Copied from `FactionPolicies`, comment included — **reproducible enumeration is what keeps a seeded
encounter generator deterministic**, and it is the kind of property that is free to keep and expensive
to add back.

**No balance number in this module.** A pattern's shares are content; the coefficients they multiply
are `aptitude-tuning`'s. If a `kMilli` appears here, it is in the wrong file.

---

## 8. Testing strategy

| # | Test | Asserts |
|---|---|---|
| 1 | `Nine_patterns_resolve` | Three pure, six mixed, computed from the posture set rather than a literal |
| 2 | `Unknown_pattern_id_throws` | Never null (§4) |
| 3 | `A_pattern_generates_at_any_theta` | Same shares at `Θ` 10 and 5,000; only `P(Θ)` scales. §2 point 2 |
| 4 | `A_pattern_never_exceeds_the_player_budget` | §2 point 4 — **the anti-cheat, as an assertion** |
| 5 | `No_pattern_is_self_cancelling` | No pattern holds a defence and the break that beats it (§3) |
| 6 | `Pattern_ids_do_not_collide_with_faction_policy_ids` | §4's two catalogs stay two |
| 7 | `Patterns_carry_no_element` | §3.1 |
| 8 | `Enumeration_is_ordinal_and_reproducible` | §7 |

**Test 4 is the one that earns its place.** It is the only automated check that difficulty stays inside
the rules, and it is exactly the kind of property that erodes quietly under encounter-tuning pressure.

---

## 9. Boundaries

**Always** — author shares; resolve by id; throw on unknown; keep enumeration ordinal.

**Ask first**

- A tenth pattern. Nine is chosen against a stated band, not arbitrarily.
- Any pattern drawing on a budget larger than the player's (§2 point 4) — that is a **product
  decision** about whether difficulty may leave the rules.

**Never**

- Merge with `FactionPolicies` (§4).
- Put an element on a pattern (§3.1).
- Author point counts instead of shares (§6).
- Author a self-cancelling pattern (§3).
- Return null from `Resolve` (§4).

---

## 10. Success criteria

1. Nine patterns, resolved by id, unknown throws.
2. Each generates at any `Θ` from fixed shares.
3. No pattern exceeds the player's budget, asserted.
4. No pattern is self-cancelling, asserted.
5. Pattern ids and faction policy ids are disjoint.
6. This module contains no balance coefficient.

---

## 11. Open

**11.1 Do the nine actually cycle?** They are *designed* to teach FORCE → BASTION → FINESSE, and
[spec-balance-guard.md](spec-balance-guard.md) can measure whether they do in milliseconds. **Run it
before shipping the roster** — a pattern set that does not cycle teaches the wrong lesson, confidently.
That is a task, not an unknown.

---

## 12. Design-gate checklist

```
[x] Subsystems identified: battle, world AI, stats, elements, power scale.
[x] Read this session: DESIGN-GATE.md, decisions.md (Element Hub SSOT, Battle time model,
    Combat resolution SSOT rows), class-system-ideal.md §6-§6.3, ssot-power-scale.md §4.6.
[x] Every factual claim cites a file, a line or a document section.
[x] Verified against CODE: FactionPolicies.cs:11-33 read in full - the ById dictionary, the ordinal
    All property and its comment, and the throw-not-null rationale quoted verbatim in §4.
[x] Read the surrounding section of every rule quoted - spec-ai-commander's legibility line under
    its own heading, so it is quoted as the principle it is rather than as a stray sentence.
[~] Constraints tested, not assumed. PARTIAL and NAMED: whether the nine cycle is §11.1, written as
    a task with the command that answers it, not claimed either way.
[x] Nothing contradicts a §2 invariant. PS-8: §2 point 4's "same finite pool" is a BUDGET, not a cap
    - a Zomboss at higher Theta has more points, which is the intended escape valve.
[x] Corrections propagated - §4.1's symmetry claim depends on spec-point-economy.md's four tiers and
    cites it; the map's module 10 row carries the same wording.
```

---

## 13. Related

- [class-system-ideal.md](../class-system-ideal.md) §6 — the inversion, the roster, and where a pattern lives
- [FactionPolicies.cs](../../../src/FusionRpg.Core/World/Ai/FactionPolicies.cs) — the shape being copied
- [world/spec-ai-commander.md](../world/spec-ai-commander.md) — legibility, settled one layer up
- [spec-point-economy.md](spec-point-economy.md) — the tiers that make §4.1 true
