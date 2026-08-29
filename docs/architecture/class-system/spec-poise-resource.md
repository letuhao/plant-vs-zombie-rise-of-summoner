# Spec: `poise-resource` — register the sixth resource

**Module id:** `poise-resource` · **Program:** [class-system-map.md](../class-system-map.md) ·
**Status: AUTHORIZED 2026-08-26 -- owner's /goal directive commands execution of the class-system plan to completion; supersedes this "awaiting owner review" header, which was never flipped after that directive landed.**

**Depends on:** nothing · **⛔ Blocks `guard-economy` completely**

---

## 1. Objective

Make `poise` a **registered actor resource**, so the guard economy has a pool to spend.

**The decision is already taken** — *"register `poise`, five → six"*, owner, 2026-08-26, decision 2 of
the pre-spec gate. **This module is how**, not whether. It was briefly mis-filed as an owner action;
the owner's part was the decision, and the amendment is spec-and-code work like any other.

**Users:** `guard-economy`, which cannot be built or tested without it; the actor sheet; anything that
enumerates resources.

**Success is measurable:** `resource.max`, `resource.regen` and `resource.efficiency` all resolve for
**poise** through `DerivedComposer` without a rejection — asserted by §8 test 2, which is the only
check that can actually detect this module's absence (§4).

---

## 2. Why this module is here, when `aspect-scope` was handed away

A fair objection, since [class-system-map.md](../class-system-map.md) §2b just established that **a
module editing none of its own program's files is not that program's module**. This module edits
`decisions.md`, `resource-hub-ssot.md`, `roster.json` and `DerivedStatChannels.cs` — none of them
class-system files.

**Two differences, and both matter:**

| | `aspect-scope` | `poise-resource` |
|---|---|---|
| Is there a program to hand it to? | **Yes** — an active map, and `demon-core` already owns *"species link, rarity, variants, trait slots, element typing"* | **No.** [resource-hub-ssot.md](../resource-hub-ssot.md) is a locked SSOT doc; `resource-hub-ideal.md` is superseded. **No map, no queue, no owning module** |
| What kind of change? | A **migration** of a live schema, with battle-golden risk | **Purely additive** — one array element and one JSON row |

> **The rule is "hand it to the program that owns it", not "never touch another program's files."**
> With no program to hand this to, handing it away means it does not happen — and `guard-economy` stays
> blocked on nobody.

---

## 3. It really is a row, not a system — the code already promises this

[resource-hub-ssot.md](../resource-hub-ssot.md) §5: *"The registry is data. Adding a sixth resource
costs a row, not a system. That property is the reason this file exists before any of it is built."*

**Verified in code 2026-08-26, and the promise holds:**

```csharp
// DerivedStatRegistry.cs:165-171 — registration is a LOOP over the id list
foreach (var resourceId in DerivedStatChannels.ResourceIds)
{
    Register(new(ResourceMax(resourceId),        FlatSum,      0, Class: StatClass.Pool));
    Register(new(ResourceRegen(resourceId),      FlatSum,      0, Class: StatClass.Pool));
    Register(new(ResourceEfficiency(resourceId), SumIncreased, 0,
                 DerivedStatPolicy.ResourceEfficiencyCap, Class: StatClass.Pool));
}
```

```csharp
// DerivedStatChannels.cs:475 — the whole edit, on the code side
public static readonly IReadOnlyList<string> ResourceIds =
    new[] { "hp", "stamina", "hunger", "spirit", "qi" };          // + "poise"
```

**One array element registers all three channels**, with the right compose kinds, the right cap and
`StatClass.Pool` — no new registration code, no new class, no new policy.

### 3.1 The roster row

[data/seed/resources/roster.json](../../../data/seed/resources/roster.json) is *"the authored mirror;
this is the code-side list registration walks. Kept in ordinal order to match that file's `ordinal`
field."* So the two must move together, and the ordinal must be **5**.

```jsonc
{
  "id": "poise",
  "class": "body",
  "ordinal": 5,
  "exhaustion": true,
  "actionCost": true,
  "pays": "guard",
  "paysNote": "Raising a guard costs a flat commit; absorbing drains it in proportion to what the guard stopped (spec-guard-economy.md §3). Spent poise converts to damage on release.",
  "labels": { "plant": "Poise", "zombie": "Poise" }
}
```

Each field, and why:

| Field | Value | Reason |
|---|---|---|
| `class` | `body` | It is stopped by the body, alongside `stamina`. The `essence` pair is `spirit`/`qi` and neither describes a guard |
| `ordinal` | **5** | After `qi` = 4. Appending, never reordering — the code comment ties `ResourceIds` order to this field |
| `exhaustion` | `true` | Guard broken. **Every resource except `hp` has one** (§10) — and `hp`'s exemption is death, which does not transfer |
| `actionCost` | `true` | Reading C: a flat commit cost. `hunger` and `spirit` are `false`; `stamina` and `qi` are `true` |
| `pays` | `guard` | A fourth value alongside `physical` / `skills` / `none` |
| `labels` | `Poise` / `Poise` | Labels are content and the only faction difference. A flavourful pair is a later content edit, not a schema change |

**No `polarity` field** — the roster entries carry none, and §6 records that all resources are `asset`
under the locked set. `poise` fills up, you spend it, empty is bad: the same.

### 3.2 ⚠️ `stamina`'s row changes too, and it is easy to miss

`stamina` currently claims the guard:

```jsonc
"pays": "physical",
"paysNote": "Move, basic attack, guard, reposition."
```

[resource-hub-ssot.md](../resource-hub-ssot.md) §2's table says the same: *"`stamina` — Physical
actions — move, basic attack, **guard**, reposition."*

> **Registering `poise` moves `guard` off `stamina`.** Both the roster note and §2's table lose it in
> the same change, or two documents disagree about who pays for a guard — which is exactly the drift
> the SSOT exists to prevent.

**Until this module lands, `stamina` paying for guard is correct and is `guard-economy`'s documented
fallback.** After it lands, that fallback is wrong and must be removed with it.

---

## 4. What `SpecChannelClaimTests` did and did not tell us

**Run at the start of this spec pass, it was RED — nine tokens across three files:**

```text
Failed!  - Failed: 1, Passed: 1
  class-system-ideal.md      status.applyShape x2 · status.applyOffsetK x2 · resource.max.poise
  class-system-map.md        resource.max.poise
  spec-guard-economy.md      resource.max.poise x3
```

**It was red on `HEAD` too** — the committed `class-system-ideal.md` already carried five of those
tokens, from the earlier status-apply-shape and `poise` work. Not introduced by this spec pass.

**Two causes, and neither needed the remedy the message suggests.** The guard offers *"resolve, mark
PROPOSED, or add to `KnownNonChannelTokens`"*; both turned out to be **documentation errors** that a
correct spelling fixes:

| Cause | What was actually wrong | Fix applied |
|---|---|---|
| The status apply-shape keys, written with a `status.` prefix | **There is no such key.** `data/tuning/status.v1.json` holds `applyShape` and `applyOffsetK` at top level, beside `applyScaleK`, `applyScaleFloor`, `applySteepnessDefault`. The prefix was a domain shorthand that was imprecise *and* collided with the channel namespace | Write the real key name. **No allowlist entry needed** |
| The poise channel, written as a channel id | The docs were **asserting a channel that does not exist**. `resource.max` *is* a registered family; only the `.poise` suffix is fictional | Name poise outside the backticks — *"a `resource.max` claim for poise"* — which says what is true |

**The guard is green as of this pass.**

### 4.1 ⚠️ Green does not mean poise is registered — say so plainly

The guard went green **from documentation corrections alone**. Nothing was registered, `ResourceIds`
is still the five, and `guard-economy` is still blocked.

> **That is the guard working correctly, not being gamed.** Its job is *"no spec may claim an
> unregistered channel"* — and the docs had genuinely been claiming one. They no longer do; the claim
> was the defect, and it is fixed. **It was never a poise tracker.**

**Consequence for this module: the guard cannot detect its absence.** §8 test 2 is the only check that
can — which is exactly why it exists, and why success criterion 5 is stated as *"three channels
resolve"* rather than *"the guard is green"*. A green guard beside an unregistered resource is
precisely the shape `distribution-reconcile` §3.9 warns about: **declared, documented, and inert, with
a passing test beside it.**

---

## 5. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "SpecChannelClaim|ActorChannels|Resource"
dotnet test tests\FusionRpg.Core.Tests --filter "StatTaxonomy|DerivedStat"
.\scripts\guard-stat-pairs.ps1
python scripts\audit-magic-numbers.py --domain status
```

---

## 6. Project structure

```text
docs/architecture/decisions.md                              Resource model row: five -> six
docs/architecture/resource-hub-ssot.md                      SS1, SS2 (stamina loses guard), SS3, SS5
data/seed/resources/roster.json                             the sixth entry, ordinal 5
src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs     ResourceIds gains "poise"
tests/FusionRpg.Core.Tests/ActorHub/SpecChannelClaimTests.cs  two tuning keys -> KnownNonChannelTokens
tests/FusionRpg.Core.Tests/Stats/ActorChannelsTests.cs       poise resolves like the other five
```

**No new registration code.** If this module adds a `Register(...)` call, a policy, or a class, the
"row not a system" promise has been broken and that is the finding, not the workaround.

---

## 7. Code style

**The array is the edit.** Append, never reorder — the `ordinal` field in `roster.json` is tied to the
list's order by the code comment above it, so reordering silently desynchronises two files.

**The exhaustion debuff is a container of atoms, never a hardcoded channel list**
([resource-hub-ssot.md](../resource-hub-ssot.md) §10) — and it carries that section's one hard rule:

> **An exhaustion debuff must never touch a channel feeding its own resource's regen.** That is the
> only true spiral, and it is rejectable by validation.

For `poise` that means the broken-guard debuff **may not touch poise's own `resource.regen` channel** — which would
be the natural, wrong instinct ("your guard is broken so it comes back slower"), because it makes the
break permanent in a long fight.

---

## 8. Testing strategy

| # | Test | Asserts |
|---|---|---|
| 1 | `SpecChannelClaimTests` stays green | A regression check only. It went green during the spec pass from doc corrections and **cannot detect this module's absence** (§4.1) |
| 2 | `Poise_registers_all_three_channels` | `max`, `regen`, `efficiency`, from the one array edit |
| 3 | `Poise_channels_match_the_other_five` | Same compose kinds, same `StatClass.Pool`, same efficiency cap — proving no special case was added |
| 4 | `Roster_and_ResourceIds_agree_in_order` | The two files stay in ordinal lockstep. **A canary, because nothing else would notice** |
| 5 | `Stamina_no_longer_claims_guard` | §3.2 — roster note and SSOT §2 both updated |
| 6 | `Poise_exhaustion_does_not_touch_poise_regen` | §7's spiral rule, as an assertion |
| 7 | `Six_resources_everywhere` | No surviving literal `5` or hand-listed five-tuple in code, tests or docs |
| 8 | `Zero_goldens_move` | Registering an unfed channel changes no value |

**Test 2 is the headline and test 4 earns its place.** Test 2 is the only check that fails while poise
is unregistered (§4.1). Test 4 guards two files against one ordering whose coupling lives only in a
code comment.

---

## 9. Boundaries

**Always** — append to `ResourceIds`; move `roster.json` in the same change; state `poise`'s exhaustion
as a status, not a channel list.

**Ask first**

- A **seventh** resource. This grant is for `poise`, not for the category — §5 of the hub is explicit
  that the id set is closed and adding one is an ADR.
- Player-facing labels other than `Poise`/`Poise`.

**Never**

- Reorder `ResourceIds` (§7).
- Add registration code (§6).
- Let the poise exhaustion debuff touch poise's own `resource.regen` channel (§7).
- Leave `stamina` claiming guard after this lands (§3.2).
- Exempt `poise` from exhaustion the way `hp` is exempt — `hp`'s reason is death, and a broken guard is
  not death.

---

## 10. Success criteria

1. `decisions.md`'s **Resource model** row reads six, and names `poise`.
2. `resource-hub-ssot.md` §1, §2, §3 and §5 agree with it — including `stamina` losing guard.
3. `roster.json` has the sixth entry at `ordinal: 5`; `ResourceIds` has it appended.
4. Three `poise` channels resolve, identical in shape to the other five.
5. Three `poise` channels resolve through `DerivedComposer` (§8 test 2). **Not** *"the guard is
   green"* — it already is, and it cannot see this module (§4.1).
6. No new registration code, no new class, no new policy.
7. **Zero goldens move.**
8. `guard-economy` is unblocked — its §2 table of seven registry fields is satisfied.

---

## 11. Design-gate checklist

```
[x] Subsystems identified: resources, stats/derived channels, status (exhaustion), tunables.
[x] Read this session: DESIGN-GATE.md, decisions.md (Resource model, Actor Hub SSOT, Status SSOT,
    Stats rows), resource-hub-ssot.md (FULL - SS1/SS2/SS3/SS5/SS6/SS10), actor-hub-ssot.md SS7 + ban
    list, derived-stats-map.md, tunables-ssot.md.
[x] Every factual claim cites file:line.
[x] Verified against CODE and DATA, not documentation: DerivedStatChannels.cs:475 (the five-element
    array), DerivedStatRegistry.cs:165-171 (the registration LOOP that makes "a row, not a system"
    literally true), data/seed/resources/roster.json (all five entries and their field shape),
    data/tuning/status.v1.json (applyShape and applyOffsetK ARE tuning keys - the SS4 false positive),
    SpecChannelClaimTests.cs:1-45 (the guard's own three remedies).
[x] Read the surrounding section of every rule quoted - resource-hub SS10's spiral rule in full, and
    SS2's pays-for table, which is where SS3.2's stamina catch came from.
[x] Constraints TESTED, not assumed - the headline claim is a RUN result. SpecChannelClaimTests was
    executed this session: Failed 1, Passed 1, nine tokens listed. The claim that applyShape is a
    tuning key was verified by parsing status.v1.json, not by reading a doc.
[x] Nothing contradicts a SS2 invariant. PS-8: poise is an uncapped pool; resource.efficiency.poise
    inherits the existing 0..1 cap, a BOUNDED RATIO already classified and commented in the registry.
[x] Corrections propagated - this spec exists BECAUSE the poise amendment was mis-filed as an owner
    action; class-system-map.md gains the module and guard-economy's SS2 now points here instead of
    naming an unowned edit.
```

---

## 12. Related

- [resource-hub-ssot.md](../resource-hub-ssot.md) §5 (the registry-is-data promise this proves), §2 (the pays-for table), §10 (exhaustion and the spiral rule)
- [spec-guard-economy.md](spec-guard-economy.md) — the module this unblocks, and the seven fields §2 there requires
- [decisions.md](../decisions.md) — *Resource model (2026-08-22)*, the row this amends
- [derived-stats/spec-unbuilt-reconcile.md](../derived-stats/spec-unbuilt-reconcile.md) — the standing guard §4 shows working
