# Passive trees — red team (2026-09-05)

Adversarial read of [../../architecture/passive-tree-ideal.md](../../architecture/passive-tree-ideal.md)
(D1–D24, §3–§10) and [../passive-tree-prior-art-2026-09-04.md](../passive-tree-prior-art-2026-09-04.md)
(R1–R8), against the shipped code.

Claims are marked **FACT** (read in `src/`/`data/`/`tests/` this session, with `file:line`),
**INFERENCE** (drawn from a fact), or **RECALL** (general knowledge, not verified in-repo — treat as a
lead). Every hole below is demonstrated with a citation or a worked number. Sections I attacked and
could not break are listed in §12, because that is useful information too.

**Design-gate reading done this session:** `DESIGN-GATE.md`, `decisions.md` (rows 94/97/103/107/108),
`ssot-power-scale.md` §10.1–§10.7, `passive-tree-ideal.md`, `passive-tree-prior-art-2026-09-04.md`,
plus the code cited in each finding. Not read end to end: `spec-point-economy.md`,
`spec-primary-stats.md`, `item-ideal.md` (only the two sections cited). Where a finding rests on a spec
I did not read in full, it says so.

---

## 1. Findings, ranked

| # | Sev | Finding | Evidence | Fix |
|---|---|---|---|---|
| F1 | **Critical** | Cross-unlock is not a second concentration reward — it is a **breadth** reward that beats `F` by ~3× and makes the doc's own "10× is arithmetically impossible" bound false | `passive-tree-ideal.md:255-258` vs `:98-101`; `tools/HybridViability/Program.cs:261` credits only the tree's own points | Credit cross-tree points at a discount `c`, put `c` in the sweep model, re-run before `b`/`Fmax` are argued again |
| F2 | **Critical** | The balance proof cannot cover the only nodes that work. `DominanceGuard.Measure` takes `IReadOnlyList<AptitudeAllocation>` — a mechanism node is not expressible as an input | `src/FusionRpg.Core/Balance/Guards/DominanceGuard.cs:37`; `passive-tree-ideal.md:207,216` | Name a second gate (battle-engine sweep with real atoms) as a deliverable, or state plainly that deep tiers are playtested, not proved |
| F3 | **High** | D19's fifth `AllocationScope` is already booked by the item program, flips a live gate, and cannot name a status anyway | `Items/Power/AptitudeAffixPrice.cs:32`; `item-ideal.md:1443`; `ItemPowerReadsTests.cs:206`; `AptitudeAllocation.cs:8,38,103`; `PointBudget.cs:37` | Do not extend `AllocationScope`. Give status trees their own mastery counter, the way `element_mastery` is specced |
| F4 | **High** | D8's self-spent-only rule is a breadth exploit, and two of D2's four sources are unruled by it | `passive-tree-ideal.md:34,40` | `H` reads every point the player *chose*; if gear stays excluded, rule the other two sources in writing |
| F5 | **High** | D20 ships **one** tier ladder against **four incommensurable** gate quantities, three of which do not exist in `src/` | `PointBudget.cs:13-15`; zero `src/` hits for `element_mastery` / almanac XP; `RpgStore.cs:433-440` | One `req()` per gate quantity, in that quantity's own units, named per tree category |
| F6 | **High** | The tree roster is not 40–60. Counted: 12 + 6 + 21 + **699 distinct `family` strings**, and no canonical family enum exists | `Aptitude.cs:40-51`; `ActorElementTypes.cs:3-11`; `StatusCatalogBootstrap.cs` (21 registrations); corpus count in §7 | Decide the family taxonomy first; D23 becomes a shared node pool plus a small per-species unique cap |
| F7 | **Medium** | D18 contradicts a locked decision, and misprices a respec that is already free and unpriced today | `decisions.md:103`; `RespecPolicy.cs:11-20`; `aptitudes.v5.json:30`; `AptitudeEndpoints.cs:30-56` | Keep the locked "a resource fighting also costs" wording or change the lock explicitly; scale the price off `Θ`; say which scope a reset covers |
| F8 | **Medium** | D21's real cost is not storage — it is the per-battle read path and an already-unpaged roster DTO | `WebMatchService.cs:414-417`; `RpgStore.Aptitudes.cs:116-118`; `RpgStore.Demons.cs:154-171`; `ContractPolicy.cs:166-176` | One batched per-squad tree-state read; page the roster DTO before tree state joins it |
| F9 | **Medium** | §4's "power ∝ effort" property is quoted, not proved — nothing says the node bonus is quadratic in soul level, and §8 says it reads `P(Θ)` | `passive-tree-ideal.md:240-250` vs `:341-343`; `ssot-power-scale.md:691-704` | State the bonus function; if it reads a per-node soul index, open the `ssot-power-scale.md` §10 row in the same change |
| F10 | **Low** | Five doc-integrity defects, one load-bearing (D20 cites a section that does not exist), plus a list of unpriced work | `passive-tree-ideal.md:52` cites §3.4; headings jump 3.3 → 3.5; `:77` vs D5; `:300` vs §10.1; §7 skips items 4–5 | Write §3.4 or fold D20's justification into §3.5; propagate D5 and D24 |

---

## 2. F1 — cross-unlock is a breadth reward, and it outguns `F`

**What the design says.** §4 (`passive-tree-ideal.md:255-258`):

> *"skill points spent in another tree of the same posture can satisfy a tier requirement. This is a
> second concentration reward, on the cost side. It compounds with `F`, and **both must sit inside the
> same closed form** or the combined effect goes unmeasured."*

**It is currently inside no closed form.** FACT: the only model that scores tree power is
`tools/HybridViability/Program.cs`, and its tier function reads one tree's own points and nothing else:

```csharp
// Program.cs:231-236
static int TierFor(double points) { var t = 0; while (10 + 2.5 * (t + 1) * t <= points) t++; return t; }
// Program.cs:259-261
var p = pts[id] / total * budget;
var tier = TierFor(p);
```

There is no posture term, no cross-credit term and no `--cross` switch anywhere in the file (288 lines,
read in full). So §3.5's published sweep — the one that produced *"not one cell reverses the ordering"* —
was run with cross-unlock **off**. The requirement §4 states about itself is unmet today.

**And it points the wrong way.** `H` is computed per **tree** (`Program.cs:255`); cross-unlock pays out
per **posture**. Inside one posture the two rewards are in direct opposition: `F` pays you for putting
everything in one tree, while cross-unlock pays you for spreading across the posture's trees, because
every tree in the posture then sees the *whole* posture spend at its gate.

**Worked number, in the sweep's own units.** Θ=100 → 300 aptitude points (`Program.cs:227-229`); FORCE
holds exactly 4 aptitudes (`Aptitude.cs:40-43`); `W = b·T(T+1)/2`, `req(t) = 10 + 2.5·t(t−1)` (D20):

| Build | Own points/tree | Tier without cross | Tier **with** cross | `W` total | `H` | `F` (Fmax 1.25) | `F·W` |
|---|---|---|---|---|---|---|---|
| Pure — all 300 in Might | 300 | 11 | 11 | 66·b | 1.00 | 1.250 | **82.5·b** |
| 4-way FORCE spread | 75 each | 5 | **11** | 264·b | 0.25 | 1.0625 | **280.5·b** |

The posture-spread build gets **3.40× the pure build's tree power**. `F`'s entire swing over the same
pair is 1.250 / 1.0625 = 1.18×. Cross-unlock beats the multiplier it is supposed to compound with by
roughly **2.9 : 1, in the opposite direction.**

**It also breaks the design's own bounding claim.** §3.1 (`:98-101`):

> *"`F ≤ Fmax` is provable at any resource level, so a 10× build is arithmetically impossible rather
> than merely unlikely."*

INFERENCE: with `k` trees in a major category and full cross-credit, every tree reaches the tier the
*whole category spend* buys, so tree power scales as `k` while `F` moves by at most `Fmax`. The ratio is
`k / Fmax` — 3.2× at `k = 4`, and **10.2× at `k = 12`**. A 10× build becomes arithmetically possible the
moment cross-unlock ships, and §3.1's proof does not catch it because it bounds `F` only and says nothing
about the cost side.

**Fix.** Cross-credit at a discount: a posture-mate's point counts as `c < 1` toward another tree's gate,
and `c` enters `HybridViability` as a swept parameter alongside `b` and `Fmax`. A cheaper option that
adds no dial: cap the cross-credited tier at *own tier + 1*, which preserves the intended "your posture
opens the door one tier early" feel and removes the multiplication entirely. Either way §3.5's sweep is
not a closed result until cross-unlock is in the model.

---

## 3. F2 — the balance claim and the only nodes that work are disjoint

This is the one to read twice. It is not a bug; it is a contradiction in what the program promises.

**The promise.** *"there are no skill tree that so op"* (owner framing, `:9`), restated by the prior-art
doc as our one genuine novelty: an explicit bounded multiplier is *"the reason our claim 'no tree is OP'
can be proved in 2.3 seconds rather than argued"* (`passive-tree-prior-art-2026-09-04.md` §3.1).

**The prover.** FACT — `src/FusionRpg.Core/Balance/Guards/DominanceGuard.cs:37`:

```csharp
public static DominanceReport Measure(IReadOnlyList<AptitudeAllocation> builds, long theta)
```

Its only build input is an `AptitudeAllocation`: a `long` per `(AllocationScope, aptitudeId)`
(`AptitudeAllocation.cs:30`), with `aptitudeId` restricted to the twelve (`:38-39`). Anything that is not
a quantity of one of twelve aptitudes **cannot be passed in.** That is a type-level fact, not a coverage
gap that more measurement time would close.

**The finding.** §3.5 (`:207`) concludes:

> *"A focus build cannot be rescued with MAGNITUDE. It can only be rescued with MECHANISM."*

and then (`:216`) explains why the prover cannot see mechanism: *"they are outside its saturating ratio
math by construction."*

**Composed, those two statements say:** the class of node that the measurement identifies as the only one
that works is exactly the class the measurement cannot score. Everything the closed form *can* prove
balanced is, by the same sweep, *"measurably worthless"* for a focused build (`:211`). The program's
headline property — provable balance — holds only over the nodes the program has decided not to rely on.

**Not fatal, and not fixable by tuning.** Three honest routes:

1. **Accept and say so.** Deep tiers are playtested, not proved. Cheapest, and it costs the
   "machine-checkable no-OP-tree" claim — a claim worth losing rather than faking.
2. **Build the second gate.** A sweep over `BattleEngine` with real atoms attached can score mechanism.
   The harness pattern exists: `ResolverMatchesSimulatorTests` already runs `tools/CombatSim` as a
   subprocess against live tuning. This is a real, unpriced module (§11).
3. **Constrain mechanism to a scoreable subset** — mechanisms expressible as a channel delta the resolver
   already reads. INFERENCE: likely to reproduce the magnitude problem, since the sweep's failure was
   precisely that the resolver's existing channels saturate.

Whichever is chosen, the ideal should stop asserting provable balance as a settled property of the whole
tree layer. Today it asserts it in §3.1 and denies it in §3.5, two sections apart.

---

## 4. F3 — D19's fifth `AllocationScope` collides with a live gate, and cannot express a status

Three separate problems, all verified.

**(a) The slot is already booked by another program.** FACT — `item-ideal.md:1443`, under *"Needs another
program"*: *"A **13th atom kind or `aptitude.*` channel family**, and a **fifth `AllocationScope`**, for
D8"*. `item-ideal.md:1236` records the owner ruling that created that need (an aptitude affix grants a
share delta).

**(b) Adding one flips a live gate to true.** FACT —
`src/FusionRpg.Core/Items/Power/AptitudeAffixPrice.cs:32`:

```csharp
public static bool VocabularyReady => AptitudeVocabularyLanded && Enum.GetValues<AllocationScope>().Length > 4;
```

The item program wrote the member count into its readiness predicate deliberately (`:22-28`) so that a
fifth value landing is *caught*. `tests/FusionRpg.Core.Tests/Items/ItemPowerReadsTests.cs:206` asserts
`Assert.Equal(4, Enum.GetValues<AllocationScope>().Length)` and goes red the same day. That is the canary
firing, not a test to update.

**(c) A `status_mastery` scope cannot name a status.** FACT — `AptitudeAllocation.cs:8` declares
`enum AllocationScope { Commander, DemonType, Aspect, UniqueDemon }`; `:103` does
`Enum.GetValues<AllocationScope>()` into `AllScopes`, and `Total`/`GrandTotal`/`Share` (`:49-85`) iterate
it. `AptitudeAllocation.Single` (`:38-39`) throws on any id that is not one of the twelve aptitudes. So a
fifth scope is automatically summed into the **aptitude** share vector, and its points must be labelled
with an aptitude id. There is no way to write "mastery of `wither`" into this type.

The category error underneath: the four existing scopes are levels of **actor ownership** — who the
points belong to (`RpgStore.Aptitudes.cs:22-29`). `status_mastery` is a **category of tree**. Different
axes. Putting a tree category into an owner enum is what makes (c) unfixable without redefining what the
enum means.

**Two more concrete breakages if it is done anyway.** `AptitudeTuning.cs:196-203` builds the byScope
dictionary from exactly four hardcoded keys; `PointBudget.cs:37` then indexes it
(`AptitudePointsPerThetaMilliByScope[scope]`), so a fifth scope throws a bare `KeyNotFoundException`
instead of the named `AptitudeTuningRejection` the loader uses for every other missing key.
`RpgStore.Aptitudes.cs:50-67` hardcodes the four text values in both directions. The DB is *not* the
obstacle — the column is plain `TEXT` with no `CHECK` (`:36-43`).

**Correction to a stale recollection, and it changes the cost.** `AllocationStore` no longer has zero
production callers. FACT: `AptitudeEndpoints.cs:52,80`, `AuraDerivedEndpoints.cs:59` and
`WebMatchService.cs:264,417` all call `SaveAllocation`/`LoadAllocation` today — **but every one passes
`AllocationScope.Commander` and nothing else.** The store is live; the other three scopes are still
unreached. D19 would be adding a fifth to a set of which one is wired.

**Fix.** Leave `AllocationScope` at four. Status trees need a gate quantity, not an ownership scope — a
`StatusMastery` counter keyed on `(actor, statusId)`, the same shape `element_mastery` is specced as
(`spec-aspect-scope.md:251`), sharing no enum with anything.

---

## 5. F4 — D8's self-spent-only rule is a breadth exploit

**The rule.** D8 as amended (`:40`): *"`H` reads spent points + souls — amended 2026-09-04 (R2):
self-spent only. Gear-granted points add power, never focus."* The stated reason is sound: a good
off-build drop must never lower your multiplier.

**The exploit.** D2 (`:34`) lists four acquisition sources: skill points, aptitude thresholds,
items/affixes, demon aspect. The amendment names **only gear**. So:

> Self-spend 100% of your skill points in one tree → `H_points = 1` → `F = Fmax`.
> Take all your breadth from gear, aptitude thresholds and demon aspect.
> You now hold a twelve-tree build **and** the pure build's focus multiplier.

That strictly dominates an honest pure build: same `F`, more total tree power. The trap D8's amendment
closed on one source is wide open on the other three — and it is worse on the two unnamed ones:

- **Aptitude-threshold grants are self-directed.** A player choosing where aptitude points go is choosing
  which trees receive free tree points. Calling those "not self-spent" is a fiction; the player spent
  something, just in a different currency.
- **Demon-aspect grants** are per-actor, and under D21 every actor has its own tree state — so "whose `H`
  do they enter" is not even asked.

**Fix.** `H` should read every point the player *chose*, and exclude only points the player did not
choose. Gear is chosen: equipping is a decision, and letting it move `F` is the trade-off, not the trap —
the off-build-drop objection dissolves because nobody is forced to equip it. If the owner wants gear
excluded regardless, the rule must be written over all four sources explicitly, because "self-spent" has
no defined meaning for a threshold grant.

---

## 6. F5 — one tier ladder, four incommensurable gates, three of which do not exist

**D20** (`:52`) locks a single sequence: `req(t) = 10 + 2.5·t·(t−1)` → 10 · 15 · 25 · 40 · 60 · 85 · 115.

**§4** (`:236-238`): a tier opens on *"the actor's own base allocation in that tree's gate quantity."*

**§5** (`:276-278`) maps four different gate quantities onto the four scopes. FACT, checked in code:

| Tree category | Gate quantity | Exists in `src/`? | Evidence |
|---|---|---|---|
| Primary (12) | `Θ_player` → aptitude points | **Yes** | `AptitudeEndpoints.cs:47` (`powerIndex.ActorIndex`) |
| Elemental (6) | `element_mastery` | **No** | `PointBudget.cs:15` — *"does not exist yet"*; zero `src/` hits outside comments |
| Demon family | almanac XP | **No** | Zero `src/` hits; `rpg_demon_codex` has no xp column (`RpgStore.cs:433-440`) |
| Demon species | specimen level | **Yes** | `rpg_unique_actors.level` (`RpgStore.cs:395`) |
| Status (21) | — | **No scope, no quantity** | `passive-tree-ideal.md:280` |

**(a) §5's own warning is understated.** It flags status trees as *"the one category this mapping does not
cover"* (`:280`). In fact **three of five categories have no working gate today** — elemental and
demon-family trees have a *scope* but no *source*, which is the same practical hole. `PointBudget.cs:15`
already says the Aspect source does not exist. §5 read *"three of four scopes ship today"* (`:277`) as if
the scopes were the gates. They are not: the scopes are rate rows; the sources are what a gate reads.

**(b) The thresholds are not comparable across the four.** `req(6) = 85` means 85 aptitude points for a
primary tree, 85 *specimen levels* for a species tree, 85 units of an unbuilt mastery counter for an
elemental tree and 85 units of unbuilt almanac XP for a family tree. Four quantities, four growth rates,
one shared table silently asserting they are interchangeable. FACT that they are not: the shipped rate
table already prices the scopes differently on purpose — `commander: 3, demonType: 4, aspect: 4,
uniqueDemon: 6` (`aptitudes.v5.json:24-27`) — and `PointBudgetTests.cs:78-95` asserts that ordering as a
real claim about the data, not about arithmetic.

Worked consequence: a Θ=30 commander holds 90 aptitude points and reaches tier 6 only by spending
*every* point in one aptitude; a level-20 specimen at 6 points/level holds 120 and clears tier 7
comfortably. Species trees would open two tiers deeper than primary trees for the same play time, from
the shipped rate table alone, before any content decision is taken.

**Fix.** A `req()` per gate quantity, in that quantity's units, living in the tuning file next to the rate
row it pairs with. If the owner wants one visible ladder, the four sources must first be normalised into
one index — and that normalisation is itself a power-shaped scale needing a reviewed row in
`ssot-power-scale.md` §10.

---

## 7. F6 — the roster is 39 trees or 738, not 40–60, and D23 does not survive it

**Counted, not quoted** (evidence rule 5):

| Category | Count | Evidence |
|---|---|---|
| Aptitudes | **12** | `src/FusionRpg.Core/Stats/Aptitudes/Aptitude.cs:40-51` (4 per posture) |
| Elements | **6** | `src/FusionRpg.Core/Stats/Derived/ActorElementTypes.cs:3-11` |
| Statuses | **21** | `src/FusionRpg.Core/Status/StatusCatalogBootstrap.cs` — 21 `Register`/`RegisterWithOptions` calls |
| Demon families | **699 distinct strings** | counted over 503 species files / 841 entries in `data/seed/demons/species/` |
| Species | **841** | same count; matches §9 exactly |

There is **no canonical family vocabulary in `src/`** — `grep -rn "DemonFamily\|enum.*Family" src` returns
only `RpgEffectFamily`, an unrelated type. `family` is free text in the seeds with a long tail: `undead`
64, `artillery-flora` 17, `fungal-artillery` 16, then hundreds of near-singletons (`carnivorous flora` 9,
`vessel-flora` 9, …).

So D9's *"each demon family"* (`:41`) resolves to either **0** family trees (n = 39) or **699** of them
(n = 738). `n ≈ 40–60` is not reachable from the shipped corpus without a taxonomy decision nobody has
made. INFERENCE: this is the same defect class §9 documents for aptitude/element skew — a free-text
LLM-authored field being read later as though it were a closed vocabulary.

**Content volume at ~29 nodes per tree** (§7 item 1's own Last Epoch reference point):

| | Trees | Nodes |
|---|---|---|
| Generic catalog, n = 39 | 39 | 1,131 |
| Generic catalog, n = 738 | 738 | **21,402** |
| D23 species trees, *"nodes no other tree has"* | 841 | **24,389** |
| Total at n = 738 | | **≈ 45,800 authored nodes** |

At 30 seconds of human review per node — and D23 buys *unique* content, the kind that most needs review —
45,800 nodes is roughly **380 hours**. That is the honest price of *"better to spend effort for it now,
maybe deploy agent to enrich it"* (`:56`), and it appears nowhere in the ideal.

**D23 does not survive contact with 841 species in its stated form**, and the reason is D24, not
generation throughput. §10.2 item 1 now requires *"The generator's output is reviewed, then committed."*
D24 makes review mandatory; D23 makes the review surface 24,389 unique nodes. The two decisions are in
direct tension and neither names the other.

**Fix — two moves, both cheap:**

1. **Decide the family taxonomy before the roster.** A canonical ~20–40 family enum, with the 699 strings
   mapped onto it, makes `n ≈ 60–80` real rather than aspirational. It is independently needed anyway:
   the `DemonType` scope key is a `typeId` (`RpgStore.Aptitudes.cs:26-28`), which is neither a `family`
   nor a `species_id`, so demon-family trees have no key in the shipped scope model today.
2. **Restructure D23 as a shared pool plus a small unique cap.** A species tree draws most nodes from a
   reviewed pool keyed on its (primary, element, status) triple and carries perhaps 3 genuinely unique
   nodes. That is 2,523 unique nodes instead of 24,389 — a tenth of the review cost — while keeping the
   identity payoff D23 is buying, because three nodes nobody else has is still *"unobtainable elsewhere."*

---

## 8. F7 — D18 contradicts a locked decision and misprices a respec that is already free

**The lock.** `decisions.md:103`, Class system row (read in full, not the line alone):

> *"**No aptitude cap and no respec cap** (PS-8): respec is available, unlimited, and **priced in a
> resource fighting also costs**."*

**D18** (`:50`) prices it *"in **souls**."* FACT: souls are a fighting **faucet**, not a fighting cost —
`decisions.md:97` describes the earn as *"+1/kill cap 50, victory 100 w/ repeat decay"*. Pricing respec in
the currency that fighting *pays you* is a different mechanic from one fighting *drains*, and the shipped
code chose the latter deliberately. `RespecPolicy.cs:11-18` writes the reason out:

> *"`Resource` is a documented placeholder (Hunger — the closest existing 'a resource fighting also
> costs') … WHICH pool is a structural/mechanism decision … not a magnitude a balance pass would dial."*

So D18 is not a tuning change — it changes a **mechanism** choice a locked decision constrains. That is a
legitimate thing to want, but it is a change to `decisions.md`, and the ideal presents it as reusing what
already exists (*"`pointEconomy.respecPrice` already exists"*).

**What that price actually is.** FACT: `data/tuning/aptitudes.v5.json:30` → `"respecPrice": 10`, flat, and
`AptitudeTuning.cs:36-38` documents it as *"the SAME price regardless of which scope is being
respecced."* A flat 10 against a budget that grows without bound (`PointBudget.PointsFor` is
`sourceValue × rate`, no cap, `PointBudget.cs:31-39`) is **a flat rate facing a scaling sink** — which
`CLAUDE.md`'s own cap definition names explicitly as a cap that survives sweeps because it does not look
like one. Not a ceiling in the usual direction, but it does mean the respec cost trends to zero in real
terms, which is exactly the *"a free respec makes a build a menu selection"* failure `RespecPolicy.cs:8-9`
says the price exists to prevent.

**And it is already free.** FACT: `RespecPolicy` has **zero production callers** —
`grep -rn "RespecPolicy\." src tools tests` returns only its own file and `RespecPolicyTests.cs`.
Meanwhile `POST /api/aptitudes/allocate` (`AptitudeEndpoints.cs:30-56`) accepts an arbitrary full share
map, checks the budget, and calls `SaveAllocation`, which *"deletes this key's prior rows first"*
(`RpgStore.Aptitudes.cs:69-72`). That **is** a full respec: unlimited, instant, unpriced. So D18's claim
that a full reset dissolves the Grim Dawn order-sensitivity problem is correct, but its pricing half is a
change to a shipped free endpoint, not a reuse of an existing charge.

**One thing D18 does not say, and D21 makes it matter.** Is a "full reset" scoped to one
`(scope, scopeKey)` or global? Under D21 each actor has its own tree state, so the difference is resetting
one demon versus resetting the entire roster in one transaction — and the shipped store is per-key by
construction (`SaveAllocation(scope, scopeKey, …)`). At a 2,000-demon roster a global reset is 2,000
delete-and-reinsert transactions under one lock. This is answerable, so it is a task rather than a risk:
pick per-actor and price it per-actor.

**Fix.** Price respec off `Θ` — `DiscardPolicy.cs:10` already does exactly that for a sibling cost
(*"scales with the actor's power (`Θ`) rather than being flat"*) — keep the resource decision inside the
lock or change the lock explicitly, and state which scope a reset covers.

---

## 9. F8 — D21's real cost is the read path, not the rows

The suspicion was that ~50 trees × ~29 skills × N actors breaks storage. It does not. **Sparse storage is
genuinely sufficient**: the shipped allocation table already skips zeros (`RpgStore.Aptitudes.cs:89-99`),
and a few hundred thousand narrow rows is unremarkable for SQLite. Three other things break first.

**(a) The roster is uncapped, so N is not 50.** FACT — `ContractPolicy.cs:166-176`:

```csharp
/// T3.6 … no ceiling — the escalating price … was always the real scarcity control …
/// A roster of 2,012 costs 600,300,000 cumulative souls; that is the limit, not a hard-coded 48.
public static int Capacity(int purchasedSlots) => BaseSlots + Math.Max(0, purchasedSlots);
public static bool CanBuySlot(int purchasedSlots) => true;
```

And **unbound** demons are free and unlimited: `RpgStore.Contracts.cs:82` notes that a specimen *"simply
arrives unbound when capacity is full."* D21 says *every* actor carries tree state, and an unbound demon
is still an actor. D21's own "~50 trees × ~29 skills … per actor" arithmetic is right per actor and wrong
about how many actors exist.

**(b) The per-battle read path is serialized and per-key.** FACT — today the **whole squad** shares one
allocation read: `WebMatchService.AptitudeChannelMods` (`:414-417`) does a single
`LoadAllocation(Commander, "player:{id}")`. D21 turns that into one read per actor, and every
`LoadAllocation` takes the global `lock (_gate)` and opens a **fresh connection**
(`RpgStore.Aptitudes.cs:116-118`). A naive per-(actor, tree) read is `N × n` round-trips per battle setup
— at a 6-demon squad and n = 39 that is 273 lock-serialized queries before the first turn, on the
standalone path where battles *are* the loop. This is the concrete "what breaks first" answer.

**(c) The roster DTO is already unpaged.** FACT — `RpgStore.Demons.cs:154-171`: `ListDemonRoster` selects
every non-retired specimen for a player with no `LIMIT` and no cursor, and `DemonEndpoints.cs:43-47`
returns it whole. At a 2,012 roster that response is already large, and the "compare my demons' builds"
screen D21 implies is precisely the surface that would join tree state onto it.

**RECALL, flagged as unverified in-repo:** ASP.NET Core SignalR's default `MaximumReceiveMessageSize` is
32 KB for client→server messages. What *is* FACT: `Program.cs:25` reads
`builder.Services.AddSignalR().AddJsonProtocol();` with no `HubOptions`, and
`grep -rn "MaximumReceiveMessageSize" src` finds nothing — so whatever the default is, it is unchanged,
and any future injector→server hub invocation carrying tree state inherits it.

**Fix.** Batch: one `LoadTreeState(playerId)` returning every actor's sparse entries in a single query,
shaped the way the design already intends the rows to be. Page `ListDemonRoster` before tree state joins
it. Neither is hard; neither is budgeted.

---

## 10. F9 — the "power ∝ effort" property is quoted, not proved

§4 (`:240-250`) claims the two-track ladder *"earns a proven property"* and quotes
`ssot-power-scale.md` §10.5 verbatim.

FACT — the source (`ssot-power-scale.md:691-704`) derives that identity from **one index**: cumulative XP
to level `L` is quadratic in `L`, power at index `Θ` is quadratic in `Θ`, and their ratio is linear
*because `Θ` is derived from `L`*. It is one ladder read twice.

§4's Deepen track is a **different** index — a per-skill soul level — and the doc never states the bonus
function over it. §8 (`:341-343`) says only *"Tree bonus power reads `P(Θ)`"*, which leaves two readings,
and both have a problem:

- **`Θ` is the actor's power index.** Then souls do not enter the power side at all: cost is quadratic in
  soul level, power is `P(Θ_actor)`, and souls buy a multiplier the doc has not specified. If that
  multiplier is linear in soul level — the natural reading of *"bonus power scale"* — power ∝ √effort.
  Reward per hour **decays**, which is the opposite of the property §4 claims and the opposite of what
  PS-8's endless grind requires.
- **`Θ` is the per-node soul level.** Then the property holds, but a per-skill soul index fed through
  `P()` is a new power-shaped scale, and `ssot-power-scale.md` §10's inventory is closed —
  *"a power-shaped number that is not in this table does not have permission to exist"*, which §8 quotes
  at itself (`:341-343`). No such row exists.

Either way §4's *"proven property"* is currently an assertion. Cheap to close: state the bonus function,
and if it reads a per-node index, open the §10 row in the same change.

---

## 11. F10 — doc integrity, and the unpriced work

**Five defects, all real, one load-bearing.**

1. **D20 cites §3.4, which does not exist.** `:52` says *"See §3.4"* for the quadratic-threshold /
   linear-power pairing rule. Headings run 3.1, 3.2, 3.3, **3.5** — there is no 3.4. The pairing rule is
   the binding half of D20 and its justification is nowhere in the file.
2. **§3.1 still runs at the superseded `Fmax`.** `:77` reads `Fmax = 1.5 (tunable)` and the table at
   `:88-94` is computed at 1.5, after D5 (`:37`) revised it to 1.15–1.25. A reader taking §3.1's numbers
   gets a multiplier 2–3× the decided one on the margin.
3. **§6 still contradicts §10.1.** `:300`: *"This is the repo's binding **seed → concrete → per-player**
   principle applied to trees."* §10.1 (`:419-429`) corrects exactly this — the pipeline is a
   *build-time authoring* pipeline whose output is committed data, not a runtime roller. §6 is the section
   a generator spec would read first, and the D24 correction was not propagated into it (evidence rule 6).
4. **§7's numbered list skips 4 and 5.** Items run 1, 2, 3, then 6, 7, 8, then 9. Two open items were
   removed without renumbering, so any downstream reference to "open item 4" is dangling.
5. **`docs/research/passive-tree/01-static-vs-rolled.md` does not exist.** §10.1's closing line points at
   it as where the freeze line gets worked out; the directory was empty before this file.

**Unpriced work.** Each item below is a real deliverable implied by a decision already taken, and none of
them is costed in the ideal:

| Implied by | Unpriced cost |
|---|---|
| D19 | A status-mastery counter, its accrual rule, its store and its endpoint — a module, not a field |
| §5 | `element_mastery` and an almanac-XP counter: **two** gate quantities that do not exist in `src/` |
| D21 | A batched per-squad tree-state read; a paged roster DTO |
| D24 §10.2 item 2 | A node-id stability rule and a regeneration-migration path for saved allocations |
| D24 §10.2 item 3 | A tree **UI** — the whole learnability half. `web/fusion-rpg-web/src` has an `AptitudesPage`/`AptitudesLayer` and nothing tree-shaped. `DESIGN-GATE.md` §1's UI row applies (GG-1: menus open over where the player already is) |
| Prior art §3.1 | The effective-tree-count surface (*"effective trees: 2.3 → +17%"*). Without it `F` is unfelt — and D24 §10.2 item 3 has now made that an acceptance criterion rather than a nice-to-have, which is a task, not an open risk |
| F2 | A second balance gate that can score mechanism nodes |
| D23 | ~24,389 unique authored-and-reviewed nodes, or the restructure in F6 |
| F6 | A canonical demon-family taxonomy over 699 free-text strings |

---

## 12. What is sound

Attacked and could not break. This list is as useful as the findings.

- **§9's corpus measurement is exactly right.** Re-counted independently over
  `data/seed/demons/species/`: **503 files, 841 entries**; `Onslaught` 332 (39.5%), `Bulwark` 133,
  `Retribution` 113, … `Ferocity` 2 (0.2%); `earth` 379 (45.1%), `air` 56 (6.7%). Every figure in §9's
  table matches. The 166:1 ratio and the *"decouple thematic favour from mechanical lock"* corollary are
  the strongest paragraphs in the document.
- **§3.1's "no `1/n` normalization" argument is correct, and gets stronger under F6.** At n = 738 the
  dropped term is 0.00136. Dropping it really does let the roster grow without re-scaling anybody's build.
- **D11 (items grant points, not node unlocks) is right, for the reason given.** It makes the tier gate
  true by construction rather than by a special case, and it mirrors the shipped aptitude rule. Note it is
  D11, not D12, that carries the weight — see the caveat below.
- **D14's property-keyed exclusion is right.** O(1) versus O(n²) as content grows, covering nodes that do
  not exist yet, is the only form that survives generated content. Reroute → Precedence → Nullification is
  a real mechanism, not a preference.
- **D24 and §10.1 are correct and correctly reconciled.** The owner's learnability constraint is folded
  properly, and §10.1's reconciliation with `seed → concrete → per-player` is exactly what
  `DESIGN-GATE.md` §1 row 45 says (*"Species stats are deterministic and shared; only effects roll, per
  player, at runtime"*). All four consequences in §10.2 are real. The only defect is that §6 was not
  updated to match (F10.3).
- **§3.3 and §3.5 are the document's best work.** They ran a measurement that refuted the design's own
  starting assumption, published the table, and changed a locked decision (D5) because of it. *"A focus
  build cannot be rescued with MAGNITUDE"* is the most valuable sentence in the file. F2 criticises what
  that finding implies for the balance *claim*, not the finding.
- **Standalone-first holds. I attacked this specifically and could not break it.** Every gate quantity
  (`Θ_player`, `element_mastery`, almanac XP, specimen level, status use), every currency (skill points,
  souls), the store (`rpg_aptitude_allocation`) and every resolution step (`AptitudeResolver`,
  `DominanceGuard`, `Predictor`) are Core/Server-side. `WebMatchService.cs:414-417` already resolves
  aptitudes into a web battle with the game closed. Nothing in D1–D24 reads a Unity field or needs a lawn
  event. The injector's only role would be enrichment — lawn-observed status uses feeding the same mastery
  counter web battles feed — which is exactly what `decisions.md:94` permits.
- **D2's "zero consumers" claim is true.** `grant.skillPointsPerTheta` is parsed
  (`AptitudeTuning.cs:156`), ships as `1` (`aptitudes.v5.json:17`), and grep finds no reader outside the
  parser, `tools/CombatSim`'s own copy, and test fixtures. The tree layer really is its first spender.
- **D15 (equal expected value, not equal shape) and R7 (bound node potency) are correct**, and they read
  EHG's stated intent accurately. A perfectly uniform tree set is perfectly predictable.

**One caveat inside the sound list.** D12 (*"tier gates read base allocation, never item bonuses — already
true by construction"*, `:44`) holds only because gates read **points**. `item-ideal.md:1236` records an
owner ruling that an aptitude affix grants a **share delta**, precisely so items *can* move aptitude
shares without diluting. So items will move the share vector that `F`, the resolver and the closed form
all read, even though they never touch a gate. §3.3 already states the closed form reads allocation only;
this is the same caveat, and it belongs in D12 rather than leaving D12 reading as an unqualified guarantee.
