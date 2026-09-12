# Crafting Coverage Engine

**Status:** Approved idea. This is a product and authoring direction, not an
architecture lock, schema, implementation plan, or permission to generate new
production recipes.

## Problem statement

How might Rise of Summoner provide memorable, fixed craft recipes that give
players deliberate access to the item ecosystem, while making recipe coverage
grow from the authored corpus rather than from an arbitrary recipe count?

This extends the **item collection and progression** spine loop in
[the loops](../guide/the-loops.md): find, equip, compare, craft, socket, and
salvage.

## The problem with the current shape

Recipes currently sit as one open-ended item-generator kind. That treats a
recipe as an independently chosen piece of flavour content. It cannot answer
the question the player actually asks:

> What can I intentionally obtain or improve, and why is that the complete
> craftable catalog?

An arbitrary count such as 30, 67, or 1,000 cannot answer that question. It
will fail again when the base, set, gem, material, rarity, or operation corpus
changes. It also permits a generator to add mutation recipes without creating
any new path to acquire the base item a player wants.

The corpus contains different kinds of things with different crafting meaning:

- a base type is a possible item chassis;
- a set is a group of members referring to existing chassis, not a new chassis
  per member;
- gems, materials, rarity, sockets, enhancement, and rerolls each have their
  own legal operation shapes; and
- salvage is a deterministic conversion rule, not a random recipe theme.

The existing crafting direction already draws this distinction: forge produces
a specific Normal base, while the cost vocabulary and its formulas resolve the
price. See [materials and crafting](../architecture/item/ssot-materials-crafting.md)
§2, §5, and §6. The current server has no forge executor yet, so a valid recipe
row alone is not a playable acquisition path.

## Recommended direction

Create a separate **Crafting Seed** program. It runs after the source item
corpora are validated and produces a fixed, reviewed recipe catalog.

```text
base types       sets       gems       materials       rarity
     \             |          |             |             /
      \------------+----------+-------------+------------/
                           crafting demand planner
                                      |
                    fixed recipe-subject plan + exclusions
                                      |
             Seedsmith authors static recipe identity and presentation
                                      |
                  recipe, economy, and runtime-readiness validation
```

"Fixed" means that a published recipe has a stable id, target, name, flavour,
and revision. It does not mean every row must be typed by hand. Seedsmith may
author the identity of a planner-selected recipe, but it may not choose whether
that recipe exists, what it targets, which operation it represents, or any
numeric cost.

The final number of recipes is therefore an output:

```text
requiredRecipeSubjects = craftingCoveragePolicy(validatedCorpus)
```

not a configuration such as `targetRecipeCount = 1_000`.

## Coverage model

The planner classifies every possible crafting concern before any model call.

| Concern | Planner decision | Result |
|---|---|---|
| Base acquisition | Is this base type craftable under the policy? | One static forge recipe, or an explicit loot-only exclusion |
| Set member | Is this member independently craftable, set-only, or loot-only? | A fixed recipe or a recorded policy exclusion |
| Gem acquisition | Is this gem family craftable at this progression point? | A fixed forge-gem recipe or a recorded exclusion |
| Material refinement | Is the material transition legal? | A fixed upcycle/refinement recipe where applicable |
| Upgrade | Which target states can be elevated, tempered, socketed, or rerolled? | A fixed operation recipe with deterministic cost resolution |
| Salvage | Is the target salvagable? | A deterministic yield rule, not a generated recipe |

Each planned subject must carry its reason: source ids, operation, target shape,
eligibility decision, cost resolver, upstream dependencies, and resulting
recipe id. Each excluded subject must carry a policy reason. No item may become
silently uncraftable merely because the generator stopped early.

## Tier-aware cross-pipeline

Crafting coverage is not only a base-item problem. A player needs a coherent
path from a low-tier drop to a stronger chosen item, and every step needs a
source of materials appropriate to that step. This makes item generation and
crafting generation two halves of one pipeline:

```text
source item corpora
  -> classify content power, item level, frame, role, rarity, and legal operations
  -> material-tier planner
  -> base/gem acquisition planner
  -> upgrade-transition planner
  -> static crafting-catalog authoring
  -> graph, economy, and runtime validation
```

The crafting program consumes validated source corpora; it must not discover
their meaning by prompting a model. Conversely, an item generator must publish
the tier metadata and legal transition facts the crafting program needs.

### Four distinct axes

The word "tier" currently risks hiding four different facts. They must remain
separate in the planner:

| Axis | Answers | Crafting use |
|---|---|---|
| Content power | Where this content sits on the endless `Θ → P(Θ)` ladder | Determines the power/magnitude context and which progression stage can expose a target; never a private recipe curve |
| Item level and material grade | What physical material quality a target needs | Determines substrate family, grade gates, salvage returns, and legal refinement paths |
| Rarity rung | How much affix opportunity and tier window the item has | Determines legal elevation and rarity-cost variants; it is not identical to item level |
| Enhancement / mutation state | What has happened to one concrete item | Determines legal temper, reroll, socket, and similar transitions |

The intended feeling can be: later content exposes stronger targets, higher
rarity routes need scarcer materials, and each upward transition consumes
lower- and current-tier ingredients. It must **not** become `high item level =
high rarity`. The rarity design deliberately has overlap, while material grade
is a gate that protects high-level content from low-level volume farming.

### Gem grade, socket capacity, and combination circuits

Gems, sockets, and Strains/Splices participate in the same collection
progression, but they are not one overloaded tier. The planner needs three
separate, inspectable outputs:

| Output | Deterministic owner | Meaning |
|---|---|---|
| Gem-grade availability | Gem acquisition and material-tier policy | Which gem grades may drop, be forged, or be upgraded at a progression stage |
| Host socket capacity | Role and rarity socket tuning | The host's `socketMax` in the structural range 0–8, plus its current opened count |
| Combination eligibility | Pure socket evaluator | Which complete four-socket circuits contain a valid Strain or Splice |

The eight-socket ceiling doubles *capacity*, not recipe length. The persisted
zero-based socket index determines its circuit:

```text
circuitIndex    = socketIndex / 4
circuitPosition = socketIndex % 4
```

Each circuit independently evaluates resonance and at most one fixed
four-ingredient Strain/Splice. A host with eight opened sockets therefore has
two readable build decisions, not one unmemorable eight-ingredient demand. No
combination may use gems across the index 3/4 boundary. The structural circuit
size and capacity ceiling belong to the socket contract; role capacities,
rarity drop ranges, opening costs, gem-grade availability, and actor-level
combination budget belong in versioned tuning.

The combination catalog also does **not** multiply into one copy per gem grade.
The planner selects a fixed combination identity from its aptitude pattern;
the evaluator derives its effective grade from the installed ingredients under
a documented grade rule (for example, the lowest contributing gem grade) and
adds the existing affinity bonus only when its circuit is attuned. That keeps
the vocabulary memorable while allowing higher-grade gems to matter. A grade
rule, its material costs, and the active-combination budget require a balance
spec and measurements before implementation; they are not model choices.

The first socket-planning policy should use progression stages as eligibility
inputs rather than promise a socket count for every item:

| Collection stage | Gem access | Socket/circuit intent | Combination intent |
|---|---|---|---|
| Early | Starter grade | 0–2 sockets | Inserts and simple resonance only |
| Developing | Next legal grade | 2–4 sockets | First complete circuit becomes possible |
| Advanced | Higher legal grades | 4–6 sockets | One circuit plus build flexibility |
| Peak collection | Highest legal grades | Role-permitted 6–8 sockets | A second independent circuit is possible only on an 8-capacity host |

This is a planner policy shape, not a statement that all late items are rare
or have eight sockets. The existing overlapping rarity bands and role ceilings
remain the final deterministic resolution; a published corpus records the
result rather than asking a model to decide it.

### Set topology is planned, not a four-piece draw

Sets are a source corpus for Crafting Seed, but they also need their own
deterministic planning stage. The current corpus's strong concentration at four
members reflects the old authoring default; it is not evidence that four is the
right shape for every progression role. A model must never decide a set's
member-role count merely because a prompt asks it to make a set.

The planner creates a set template before it creates member subjects. Its four
inputs are deliberately distinct:

| Planning input | Meaning | Why it affects set shape |
|---|---|---|
| Content-power stage | A declared stage on the shared `Theta` ladder, not a private set curve | Later stages may support a larger long-term collection goal |
| Rarity rung or allowed rarity band | The item's opportunity band, separate from item level | Higher-rarity paths may justify a more specialised commitment; they must not automatically be larger |
| Set class | `general`, `family`, or `unique-species` | Determines the set's identity and its baseline commitment |
| Set progression tier | A catalog-policy tier within that class | Determines which approved slot-growth template applies |

The result is a declared template, not an arithmetic rule hidden in generator
code:

```text
(contentPowerStage, rarityBand, setClass, setProgressionTier)
  -> memberRoleCount, bonusTierCeiling, thresholdTemplate, eligibility rules
```

The lookup data lives in a versioned set-planning tuning file. It may return no
template when the source corpus cannot support a complete, legal set. The
planner may not replace a missing row with an LLM guess or a generic four-piece
fallback.

`memberRoleCount` means distinct equipped roles, not raw JSON member rows. A
role may have different frame-specific base types, but it still supplies one
point to the set counter. This preserves the existing membership model and
makes the planner validate that every intended body can complete the set with
real compatible roles.

#### Proposed classes and defaults

The following are proposed catalog defaults for the first set-planning tuning
revision. They are a design decision to validate, not an instruction to rewrite
the current set corpus.

| Set class | Identity | Lowest member-role count | Default maximum bonus tiers | Intended scale |
|---|---|---:|---:|---|
| `general` | Broad theme available across ordinary item families | 2 | 4 | Starts as a small, splashable pair; later set-progression templates may claim more roles and expose more thresholds |
| `family` | A declared family/theme with a narrower, coherent collection | 5 | 3 | Starts as a meaningful build choice and grows through later templates |
| `unique-species` | One declared unique creature species; never inferred from a display name | 10 or 15 | exactly 2 | A parameterized species-bound signature/full kit with thresholds at 2 and the selected final role count; hybrids are ineligible |

`general` and `family` therefore have more bonus-tier capacity than the
high-volume `unique-species` class. The latter needs a small default because
there are already 900+ potential creature-species subjects; ten or fifteen members times a
long bonus ladder would multiply both authoring and balance debt faster than it
adds meaningful build choices.

The defaults are **floors and ceilings**, not a promise that every class/tier
combination exists. A low-power general set begins at two roles and can only
have its reachable 2-piece activation. As its approved progression template
adds roles, it can expose up to its four-tier ceiling. A family or general set
with fewer roles than its requested bonus-tier count is invalid rather than
silently receiving duplicated thresholds.

#### Slots and activation tiers are separate outputs

A set has a fixed member-role count once published. It does not grow new slots
on a player's existing set. A later catalog template creates a different set
subject with its own fixed member roles and thresholds.

After choosing the member roles, the planner resolves a cumulative threshold
list from the template:

```text
member roles:     10 or 15
bonus-tier count: 2
threshold list:   [2, memberRoleCount]
```

Every list must be strictly increasing, start at two, end at or below the
member-role count, and contain no more rows than the class's bonus-tier ceiling.
The first threshold carries the set's distinctive capability; later thresholds
add its numerical reinforcement, preserving the existing set-bonus direction.
Threshold templates are data, not language-model choices and not a progression
formula coded beside the generator.

#### Required planner proofs

For every proposed set, the deterministic stage must prove:

1. **Slot legality:** every planned role exists, is distinct, and is legal for
   every frame or species the template promises to support.
2. **Completion:** each declared eligible body has a real member selection for
   every required role; a set cannot become a partial-only trap.
3. **Threshold reachability:** the cumulative threshold list obeys the member
   count and the class's bonus-tier ceiling.
4. **Build-space budget:** composable sets retain their class-policy non-set
   roles; a unique-species set proves its species-bound total budget. Its
   15-role template may deliberately fill the entire eligible body, while its
   10-role signature template follows its declared remaining-role policy.
5. **Crafting linkage:** every craftable member has a valid acquisition/material
   path, and every loot-only member has an explicit source reason.

A unique-species set is a deliberately distinct class, not an oversized general
set. Its template chooses ten or fifteen roles and exactly two thresholds. It
is valid only for the declared non-hybrid unique creature species/body, and only
with the class-specific total budget. The fifteen-role template must cover the
complete body map; the ten-role template must retain its exact declared map. It
can never be emitted as a generic or family template. The planner treats an
incomplete, cross-body, or hybrid unique set as invalid rather than silently
shrinking it.

#### Deterministic set-plan contract

The set-planning output joins the crafting plan as an inspectable input. One
row contains at least:

```text
setSubjectId
setClass
declaredFamilyOrSpeciesId?
contentPowerStage
rarityPolicy
setProgressionTier
memberRoleCount
memberRoleSelectors
bonusTierCeiling
thresholds
frameOrSpeciesEligibility
setIdentityRequirement
craftingDisposition
architectureStatus
```

Only after this row passes the proofs may Seedsmith receive a vocabulary brief
for a set name, member naming theme, and flavour. It cannot add or remove a
role, decide a full-kit collection is safe, select a species, or turn an
incompatible body into a member.

### The tier tree is a graph, not a list of recipes

The planner should build a directed graph of legal, player-visible progression
paths. Its nodes are target states and material states; its edges are fixed
crafting or salvage operations.

```text
low-grade material
  -> refine / obtain higher-grade material
  -> forge a chosen Normal base
  -> elevate its rarity and improve it
  -> socket, temper, or reroll its concrete state
  -> salvage returns lower-tier material, never a free loop
```

For a higher-tier target, the recipe policy can require a deterministic mix of
the target tier's material and a defined lower-tier predecessor. This captures
the desired hunt-and-upgrade feeling: earlier materials remain meaningful, but
cannot manufacture a top-tier target by themselves. The exact ingredient
relation, quantities, and unlock gates are tuning and policy—not language-model
choices and not literals in code.

Every graph edge needs four proofs:

1. **Legality:** the operation, target state, and material classes are valid.
2. **Reachability:** all input materials have at least one real faucet at or
   before the intended progression stage.
3. **No economy exploit:** refinement and salvage cannot form a net-positive
   cycle or bypass the grade lock.
4. **Runtime readiness:** the server has an executor that can actually perform
   the stated mint or mutation atomically.

### Material ancestry: higher tiers remember their predecessor

The proposed default rule for an upward tier edge is **one current-tier input
plus one immediate predecessor input**. It gives a higher-tier craft a visible
history without turning every recipe into a list of every material ever found.

```text
root tier T0
  -> current-tier material only

upgrade / forge at tier T, where T > T0
  -> material resolved for T
  + material resolved for parent(T)
  + operation catalyst and any separately legal rarity ingredient
```

`parent(T)` is a deterministic edge in the tier graph, not an LLM judgment and
not necessarily a string subtraction. The policy maps each content-tier node to
the relevant existing material grade or to a future approved material-tier
node. It may also say that a target has no craft path at that stage.

Using only the immediate predecessor is deliberate:

- it keeps earlier hunts relevant at every later step;
- it keeps recipe cards readable and bounded in length;
- it allows the planner to prove a path one edge at a time; and
- it avoids an ever-growing all-ancestor shopping list that becomes a wiki
  recipe instead of a player decision.

The exact quantities, grade mapping, unlock condition, and conversion ratios
belong in versioned tuning. The existing material taxonomy and grade lock are
the starting vocabulary; this idea does not silently add monster-specific
materials or a new currency class. If a future material tier is needed, it is
an explicit economy/catalog decision with faucets, sinks, salvage behavior, and
content-hash coverage—not a generator convenience.

### Rarity is a transition policy, not the tier itself

Rarity must stay distinct from content and material tier. A later content tier
may expose stronger or rarer outcomes, but the planner may not infer that every
high-tier item has a high rarity, or that every rarity rung can be purchased.

The policy table is therefore a relation, not a global craft cap:

```text
(content-tier, item-material-tier, target-rarity-rung, operation)
  -> craftable | loot-only | hybrid | unavailable
```

- **craftable** means a fixed static recipe subject is required; its shard and
  material inputs resolve deterministically.
- **loot-only** means no recipe is planned and the report records the content
  source that must provide it.
- **hybrid** means a recipe can improve or specialise an existing item, but an
  upstream drop-only ingredient, base, or prior state remains required.
- **unavailable** means the transition is intentionally outside that stage;
  it is not an accidental missing row.

This gives the designer a clean way to preserve aspirational drop-only rarity
while still allowing a player to target a desired normal base, build its
material foundation, and make deliberate progress through permitted upgrades.

### Deterministic tier-plan contract

Before recipe authoring, the planner emits a revisioned, inspectable plan. One
row represents one fixed player-facing recipe subject or one explicit exclusion:

```text
subjectId
sourceIds
operation
targetState
tierNode
parentTierNode?
requiredMaterialSelectors
rarityTransitionPolicy
unlockPolicy
expectedExecutor
coverageDisposition
```

For an eligible concrete base, `targetState` is a precise Normal base target.
For an operation contract, it is the legal state transition rather than a
duplicated row for every possible concrete item. `requiredMaterialSelectors`
are resolved by deterministic code from the tier nodes, target frame, grade,
rarity band, and operation; they are never a model-proposed ingredient list.

The plan must fail closed when a tier has no parent edge, a required material
has no faucet, an alleged loot-only target has no declared source, a rarity
transition has no policy row, or an executor is missing.

### Authoring vocabulary after planning

Once the tier plan is valid, Seedsmith sends the model a narrow recipe brief:

```text
fixed subject id + operation + exact target + resolved material concepts
+ allowed discovery/theme vocabulary
-> name, flavour, concise player-facing description
```

The model does not receive an invitation to design a recipe. It cannot select a
different tier, target, material, rarity transition, price, or operation. A
second deterministic pass validates names and text against the fixed subject,
then writes the static catalog row. The same plan can be regenerated without
changing coverage; only deliberately requested presentation repair reopens a
published row.

### Tier-aware coverage formula

The planner's output remains corpus-derived, but now it is derived over graph
obligations rather than only over item identities:

```text
requiredCraftingSubjects =
  acquisitionRoots(eligibleBaseTypes, eligibleGems)
  + materialTransitions(legalMaterialTierEdges)
  + upgradeEdges(legalItemTierAndRarityTransitions)
  + operationContracts(legalConcreteItemMutations)
  - explicitLootOnlyOrUnsupportedSubjects
```

The actual catalog size grows when new bases, material tiers, gems, or legal
transitions are introduced. It does not grow merely because a model happened to
draw another recipe. Re-running the planner on unchanged input must produce the
same ordered subject set and explain every difference after a source revision.

### Distribution is path coverage

The new diversity report must measure more than raw item counts. It should
answer, for each intended role/frame/content stage and material/rarity path:

- Is there at least one acquisition root?
- Can its required material tier be obtained without an impossible jump?
- Does the target have legal upgrade and recovery paths where policy promises
  them?
- Does each higher-tier path consume the required lower-tier and current-tier
  materials?
- Is the path intentionally loot-only, explicitly disabled, invalid, or not
  yet executable?

This turns a missing recipe from a vague count deficit into a concrete failure,
for example: `plant / armament-secondary / grade 3: no forge root`, or
`rarity elevation to rung 6: required shard faucet absent`.

## Deterministic and authored responsibilities

### The deterministic planner owns

- discovering every relevant source corpus recursively;
- calculating craftable and intentionally excluded subjects from policy;
- selecting the exact operation and target for each subject;
- resolving legal cost classes, material variants, gating, and numeric costs
  from versioned tuning;
- proving every cost has a real faucet and every output has a real executor;
- assigning stable ids, ordering, and regeneration keys; and
- emitting a coverage report that fails when a required subject is missing.

### Seedsmith authoring owns

- memorable recipe names;
- flavour and presentation text; and
- optional discovery or thematic grouping labels from a closed planner-provided
  vocabulary.

It cannot invent an operation, target, material, cost, rarity, or eligibility
exception. It cannot create a row outside the deterministic plan.

## Recipe families, not a flat pile of rows

Not every player-facing recipe needs the same granularity.

- **Acquisition recipes** are concrete and memorable: a player chooses a known
  base or gem and receives exactly that target.
- **Transformation recipes** are fixed operation contracts. A single stable
  elevation, socket, temper, or reroll recipe can resolve legal variants from
  the selected target's validated frame, band, grade, and state.
- **Salvage** remains an operation with a deterministic yield preview, never a
  pile of generated recipe rows.

This prevents both bad extremes: a tiny catalog that does not cover the world,
and one million redundant copies of the same upgrade rule.

## Minimum evidence before authoring recipes

The first deliverable is a no-model crafting coverage report. For every source
subject it must show one of these states:

```text
craftable: planned recipe subject
loot-only: explicit policy reason
not-ready: missing executor, target container, or source dependency
invalid: broken reference or illegal cost/operation relationship
```

Only after that report is complete may Seedsmith author the corresponding fixed
recipe rows. A successful JSON import is insufficient: the report must also
prove that the runtime can perform the operation and mint or mutate the stated
output.

For tier-aware authoring, the report additionally proves that every planned
edge has a reachable input path and that every source target is either connected
to its policy-required tier tree or explicitly excluded.

## Key assumptions to validate

- [ ] The product policy can define which base types, set members, gems, and
  uniques are deliberately loot-only without leaving an unexplained player gap.
- [ ] The server can execute forge before any forge recipe is counted as a
  playable path.
- [ ] Existing cost formulas can price every planner-selected target without
  adding a private level or power curve.
- [ ] The player can browse and understand a large fixed catalog through
  discovery, categories, and target selection rather than a wiki-like list.
- [ ] Source corpora expose enough stable metadata to classify eligibility and
  progression without asking an authoring model to infer it.

## MVP scope

1. Produce the no-model crafting coverage report from the current item corpus.
2. Emit a no-model tier-tree report connecting item level, material grade,
   rarity, and legal operations without collapsing their meanings.
3. Define and validate the craftability policy for base types, sets, gems,
   materials, and uniques.
4. Implement one complete static recipe family: eligible Normal base acquisition
   with its material-tier path.
5. Implement and test the server forge executor for that family.
6. Add coverage, cost-faucet, runtime-readiness, no-positive-cycle, and
   regeneration tests before authoring the remaining recipe families.

## Not doing

- No fixed recipe-count target.
- No random runtime recipe offers.
- No unscoped recipe LLM draws.
- No automatic decision that every set member or unique is craftable.
- No LLM-authored numeric costs, operations, targets, or eligibility rules.
- No recipe generation before the corresponding runtime executor exists.
- No new item-system specification in this idea phase.

## Open questions for discussion

1. Which content is intentionally loot-only: sets, uniques, particular gems,
   or selected base categories?
2. Does every ordinary base type receive a direct forge recipe, or are there
   explicit progression/discovery gates?
3. Which source dimensions define meaningful acquisition diversity beyond
   `role × frame × band`?
4. Which upgrades are universal operation contracts and which deserve distinct
   static recipes?
5. How should the workshop expose a large static catalog without becoming a
   wiki or hiding desired targets?
6. What is the authored material-tier relation for an upward step: which lower
   tier remains required, which current-tier material is required, and where do
   their faucets live?
7. Which rarity transitions are player-craftable, and which remain drop-only at
   the top of the ladder?
8. Is one immediate predecessor material the right default ancestry rule, or
   are there deliberate branch, boss, or set exceptions that need an explicit
   policy shape?

## Design-gate evidence

- Product loop read: [the game](../guide/the-game.md) and
  [the loops](../guide/the-loops.md).
- Architecture read: [software architecture](../architecture/software-architecture.md)
  and [architecture decisions](../architecture/decisions.md).
- Item and economy sources read: [materials and crafting](../architecture/item/ssot-materials-crafting.md),
  [sets](../architecture/item/ssot-sets.md), [rarity](../architecture/item/ssot-rarity.md),
  and [inventory and workshop](../design/spec-inventory-and-workshop.md).
- Power sources read: [power-scale SSOT](../architecture/power/ssot-power-scale.md)
  and [power capability map](../architecture/power-map.md). Current material
  grade and refinement tuning is in [materials.v1.json](../../data/tuning/materials.v1.json).
- Current implementation checked: [ItemWorkbench.cs](../../src/FusionRpg.Server/ItemWorkbench.cs)
  identifies forge as not yet executable; [CostClassMatrix.cs](../../src/FusionRpg.Core/Items/Materials/CostClassMatrix.cs)
  is the current closed operation vocabulary.
