# Spec: `aspect-scope` — move element off the species, and make it an allocation tier

**Module id:** `aspect-scope` · **Program:** [demon-system-map.md](../demon-system-map.md) ·
**Status: APPROVED by the owner 2026-08-31. Authorized to build.**

> Approved while resolving the open questions of
> [seedsmith-demons-ideal.md](../seedsmith-demons-ideal.md) §5 Q2: that feature ships an `aspect`
> `KindSpec` alongside item / action / commander / environment, and aspect-generated content has
> nowhere to live until this module lands. The approval is of **this spec as written** — including
> its byte-identical migration path (§3.1), which is what makes today's trait pools reproducible
> after element moves down a tier.
>
> The class-system program's `point-economy` was the original requester and is unblocked by the same
> approval ([class-system-map.md](../class-system-map.md) §2b).

**Depends on:** `demon-core` · **Blocks:** `point-economy` (its third scope) in the class-system program

> **⛔ Ownership moved 2026-08-26, by owner decision.** This spec was written as class-system module 4
> and now belongs to the **demon program**, because **every file it edits is that program's**:
> `DemonSpeciesCatalog`, `DemonSpeciesGenerator`, and the checked-in generated catalog. The demon map's
> `demon-core` already owns *"species link, rarity, variants, trait slots, **element typing**"* — this
> is a migration inside that territory, not a new subsystem.
>
> **The class system is the requester, not the owner.** It needs the tier to exist so
> [point-economy](../class-system/spec-point-economy.md) has a third allocation scope; it does not need
> to be the one that builds it. The requirement, the byte-identical migration path (§3.1) and the
> tests are supplied here so the demon program schedules a specified change rather than a request.
>
> **Consequence the class system accepts:** `point-economy`'s scope 3 now waits on another program's
> queue. That is the honest cost of correct ownership, and it is recorded in
> [class-system-map.md](../class-system-map.md) §2b rather than hidden in a dependency arrow.

---

## 1. Objective

Make **`aspect`** — an actor's element typing, plus the trait bias and starting skills that follow from
it — the **third of four allocation scopes**, by moving two fields down one tier.

**Owner, 2026-08-26:** *"one plant type maybe have many element type… not only element types, maybe
affect trait / initial skills or something? strong and weakness?"*

**The one real migration.** [DemonSpeciesCatalog.cs:16-23](../../../src/FusionRpg.Core/Demons/DemonSpeciesCatalog.cs)
carries `ElementPrimary`, `ElementSecondary` **and** `TraitPool` **on the species**:

```csharp
public ElementTypeId ElementPrimary { get; init; }
public ElementTypeId? ElementSecondary { get; init; }
public IReadOnlyList<string> TraitPool { get; init; } = Array.Empty<string>();
```

So today **one species is one element** — a fire Peashooter and an ice Peashooter would be two species,
not two aspects of one. That is a schema and generator change, and it is the single largest piece of
work the four-scope decision creates (ideal §7c.5).

**Users:** `point-economy` (scope 3); the demon roster; whoever later builds `element_mastery`, which
[spec-primary-stats.md](../class-system/spec-primary-stats.md) §3.3 assigns to this tier.

**Success is measurable:** `Peashooter` resolves to **six** aspects with different elements, different
trait bias and different starting skills, from **one** species row and **one** extra generator argument.

---

## 2. Derive it, never author it

**This is the whole discipline, and the repo already has the machinery.**

```csharp
// DemonSpeciesGenerator.cs:65 today
TraitPool = TraitsFor(rarity, row.TypeId)

// one more argument
TraitPool = TraitsFor(rarity, row.TypeId, element)
```

[DemonSpeciesGenerator.cs:125-140](../../../src/FusionRpg.Core/Demons/Generation/DemonSpeciesGenerator.cs)
already picks from a combat pool and a personality pool by a stable `Hash(typeId, salt)`, adds a third
combat trait, and layers `void-touched`/`chaos-marked` at Epic and `immortal` at Legendary. **Adding
`element` to the salt gives every aspect a different pool from the same deterministic function.**

Species are **generated from captured game data, output checked in**
([decisions.md](../decisions.md) *Demon program*). So 20 species × 6 elements is **120 generated
aspects, not 120 authored ones** — and the alternative is the fifth content system the atom program
exists to stop. **One generator argument versus a content project.**

### 2.1 Strengths and weaknesses need nothing at all

They already ship: the ring `fire → ice → earth → air → fire` plus `light ⇄ dark`, with
`MatchupShareK = 0.25` ([decisions.md](../decisions.md) *Element Hub SSOT*).

Ideal §2 is explicit that the posture cycle *"needs no second matchup table competing with the element
ring"* — and this needs no third.

> **An aspect's strength and weakness ARE its element's.** There is nothing to design and nothing to
> author. Anyone proposing a per-aspect matchup table is proposing the repo's third one.

### 2.2 The name — `aspect`, and why the two obvious words are taken

| Candidate | Verdict |
|---|---|
| `race` | **Taken** — `StatClass.Race` |
| `variant` | **Taken and means something else.** `DemonSpeciesCatalog.KnownVariants` ([DemonSpeciesCatalog.cs:31-34](../../../src/FusionRpg.Core/Demons/DemonSpeciesCatalog.cs)) is a shipped closed list — `normal · ancient · mutated · corrupted · blessed · cursed · shiny` — i.e. **cosmetic-rarity finishes**. Adopting it creates exactly the collision `race` was avoided for |
| `affinity` | Was the natural pick while the tier was element-only. **Now too narrow** — the tier carries traits and starting skills, and `affinity` already names a divisor share in `BattleStatComposer` |
| **`aspect`** | **Chosen.** Free everywhere in `src/`, reads correctly (*"the fire aspect of Peashooter"*), implies more than a damage type, and carries no biological framing — which matters because zombies and demons take element typings too |

---

## 3. What already ships, and what has to move

Correcting the earlier claim that this tier had no shipped home. **It has more than any other.**

| Piece | Ships as | Moves? |
|---|---|---|
| An actor's element identity | `ActorElementTypes` — `Primary` + `Secondary`, validated (a secondary requires a primary; the two must differ) | **No** |
| Element routing a share of stats onto its own channels | `BattleStatComposer` — `PrimaryAffinityDivisor` +25%, `SecondaryAffinityDivisor` +12.5% | **No** |
| Strength / weakness | the element ring + `ShieldElementMatrix` | **No** |
| Trait pools, generated per species | `DemonSpeciesGenerator.TraitsFor(rarity, typeId)` | **Gains an argument** |
| `ElementPrimary` / `ElementSecondary` / `TraitPool` | on `DemonSpeciesDef` | **Down one tier — this is the migration** |

> **`BattleStatComposer`'s affinity is this tier's shipped precedent, with a fixed share instead of a
> budget.** The aspect tier is the same idea made allocatable: instead of a divisor handing you +25% on
> your own element, you **spend points** there.

### 3.1 The golden question, answered before it is asked

`decisions.md`'s *Golden ordering across streams* row is explicit that the battle grid and other
schema-touching work **join a combined re-bless** and never ride along with a freezer.

**This module's honest posture:** moving a field that participates in a battle hash is a golden move.
So either (a) `DemonSpeciesDef`'s element fields do not reach a hashed path, in which case the
migration is field-only and `RulesetVersion` is unchanged — the `loam-model` precedent; or (b) they do,
and this module **joins the combined re-bless rather than calling its own**.

**Established 2026-08-26 — and there is a byte-identical path.**

`BattleActorSetup` carries `ElementPrimary`, `ElementSecondary` and `TraitIds` as **values**, and it is
serialized into the hash — proven by its own `[JsonIgnore]` on `Index`, whose comment records that a
first draft without it *"moved ExpeditionResolverTests.Tier_goldens_are_locked's hash, because
System.Text.Json serializes get-only computed properties by default."*

> **So the schema move alone moves nothing** — the setup's shape is unchanged; only where its values are
> *sourced from* changes. **What moves a hash is regenerating the trait pools**:
> [WaveCatalog.cs:67](../../../src/FusionRpg.Core/Battle/WaveCatalog.cs) does
> `TraitIds = species.TraitPool`, so different traits → different `ChannelMods` → different resolved
> stats → moved hash.

**The repair, and it should be a design requirement rather than a discovery:** seed the element salt so
that for each species' **own current element**, `TraitsFor(rarity, typeId, currentElement)` reproduces
today's `TraitsFor(rarity, typeId)` exactly. The 20 existing species stay byte-identical and the ~100
new aspects are purely additive — **a field-only change, the loam-model precedent**, rather than a
golden move needing a combined re-bless.

---

## 4. Commands

```powershell
# Regenerate the species catalog and diff the checked-in output
dotnet run --project tools\... -- generate-species        # the shipped generator entry point
git diff --stat src/FusionRpg.Core/Demons/DemonSpeciesCatalog.Generated.cs

dotnet test tests\FusionRpg.Core.Tests --filter "Species|Aspect|Element"
dotnet test tests\FusionRpg.Core.Tests --filter Golden      # task 1: does anything move?
```

---

## 5. Project structure

```text
src/FusionRpg.Core/Demons/DemonSpeciesCatalog.cs             ElementPrimary/Secondary/TraitPool leave the def
src/FusionRpg.Core/Demons/AspectCatalog.cs                   new: (speciesId, element) -> traits, starting skills
src/FusionRpg.Core/Demons/Generation/DemonSpeciesGenerator.cs TraitsFor gains `element`; generates aspects
src/FusionRpg.Core/Demons/DemonSpeciesCatalog.Generated.cs    regenerated, checked in
tests/FusionRpg.Core.Tests/Demons/AspectCatalogTests.cs
```

---

## 6. Code style

Match `DemonSpeciesCatalog`'s shipped shape exactly — a `sealed record` of `init` properties, a
`static partial` catalog with the generated rows in a sibling file, and **`Resolve` that throws rather
than returning null**, for the reason `FactionPolicies` states in its own comment
([FactionPolicies.cs:27-33](../../../src/FusionRpg.Core/World/Ai/FactionPolicies.cs)): *"A null would
read as 'this faction has no brain', which is indistinguishable from the human — and a typo would then
look like a design decision for the rest of the campaign."* The same is true of a missing aspect.

**Determinism is the property to protect.** `TraitsFor` is a pure function of `(rarity, typeId, salt)`.
Adding `element` to the salt must keep it pure — no time, no RNG, no ordering dependence — because the
output is checked in and a diff has to be reviewable.

---

## 7. Testing strategy

| # | Test | Asserts |
|---|---|---|
| 1 | `One_species_yields_many_aspects` | `Peashooter` resolves six, differing in element **and** trait pool |
| 2 | `Aspect_generation_is_deterministic` | Same inputs, same output, twice, in a fresh process |
| 3 | `Aspect_strength_is_its_elements` | No second matchup table exists; the ring and `ShieldElementMatrix` are the only sources (§2.1) |
| 4 | `Aspect_resolve_throws_on_unknown` | Never null (§6) |
| 5 | `Element_validation_survives_the_move` | `ActorElementTypes`' rules still hold at the new tier: a secondary requires a primary, and the two differ |
| 6 | `Variant_and_aspect_do_not_collide` | `KnownVariants` is untouched and means what it meant (§2.2) |
| 7 | `Golden_impact_is_established_not_assumed` | §3.1 — a test that **records** whether the moved fields reach a hashed path, so the answer is in CI rather than in someone's memory |

---

## 8. Boundaries

**Always** — derive traits and starting skills from `(species, element)`; keep the generator pure;
regenerate and check in rather than hand-editing `*.Generated.cs`.

**Ask first**

- Anything that moves a battle golden (§3.1) — and if it does, joining the combined re-bless rather
  than calling a separate one.
- Adding a seventh element. Generation makes it nearly free; **deciding it is Element Hub's**, not this
  module's.

**Never**

- Author an aspect by hand. 120 authored rows is the content system this design exists to avoid.
- Add a per-aspect matchup table (§2.1).
- Reuse the word `variant` (§2.2).
- Let an aspect reach a channel an aptitude reaches. Ideal §4.1 rule 2: **aptitudes reach mechanisms,
  aspects and skills carry flavour.** Both halves stay additive and they never come from one currency.

---

## 9. Success criteria

1. `ElementPrimary`, `ElementSecondary` and `TraitPool` no longer sit on `DemonSpeciesDef`.
2. One species resolves to N aspects; the generator gained exactly one argument.
3. Generation is deterministic and the checked-in diff is reviewable.
4. No new matchup table exists.
5. §3.1 is **answered with a test result**, and if a golden moves, this module joined the combined
   re-bless rather than calling its own.
6. `point-economy` can address scope 3 by `(speciesId, element)`.

---

## 10. Open

**10.1 Starting skills do not exist yet.** The tier is specified to carry them, and the skill layer is
in [class-system-map.md](../class-system-map.md) §5's reserved list. **This module derives the *pool*;
it does not build a skill system.** Until skills exist, an aspect's "starting skills" is a derived id
list with no consumer — which is a legitimate state (`status.expose.*` is the shipped precedent for
registered vocabulary with no reader) provided it is labelled, not quietly shipped as working.

**10.2 `element_mastery` lands here later.** Assigned to this tier by
[spec-primary-stats.md](../class-system/spec-primary-stats.md) §3.3, with two conditions attached: it owes a
[power/ssot-power-scale.md](../power/ssot-power-scale.md) §10 row or a proof it is not power-shaped,
and PS-3 applies to it. **Not this module's build** — recorded so it is not rediscovered.

---

## 11. Design-gate checklist

```
[x] Subsystems identified: demons/species, elements, stats, battle goldens, power scale.
[x] Read this session: DESIGN-GATE.md, decisions.md (Demon program, Element Hub SSOT, Shield layer,
    Golden ordering across streams, Battle time model rows), class-system-ideal.md §7c,
    derived-stats-map.md, ssot-power-scale.md §10.
[x] Verified against CODE: DemonSpeciesCatalog.cs:16-23 (the three fields on the def),
    DemonSpeciesCatalog.cs:31-34 (KnownVariants, the seven cosmetic finishes),
    DemonSpeciesGenerator.cs:65 and :125-140 (TraitsFor and its hash salts),
    FactionPolicies.cs:27-33 (the throw-not-null rationale quoted in §6). All read, not grepped.
[x] Read the surrounding section of every rule quoted - the Golden ordering row in full, including
    why the L25 batch was batched.
[x] Constraints TESTED, not assumed - CLOSED 2026-08-26 by reading BattleModels.cs:20-45 (the
    [JsonIgnore] comment recording that a computed property DID move Tier_goldens_are_locked) and
    WaveCatalog.cs:67 (TraitIds = species.TraitPool). Answer: the schema move is field-only; the
    trait REGENERATION is the mover, and salt-seeding makes even that byte-identical. §3.1.
[x] Nothing contradicts a §2 invariant.
[x] Corrections propagated - §3 corrects the earlier "this tier has no shipped home" claim in place.
    OWNERSHIP moved to the demon program 2026-08-26: demon-system-map.md gains its module row,
    class-system-map.md §2b records it as an external dependency, and every class-system spec that
    referenced it as a sibling now points here.
```

---

## 12. Related

- [class-system-ideal.md](../class-system-ideal.md) §7c.4–7c.7 — the tier, the name, and the element-neutralisation caveat
- [element-hub-ssot.md](../element-hub-ssot.md) — the ring, and why it is the only matchup table
- [spec-primary-stats.md](../class-system/spec-primary-stats.md) §3.3 — `element_mastery`'s assignment to this tier
- [decisions.md](../decisions.md) — *Demon program*, *Element Hub SSOT*, *Golden ordering across streams*
