# Spec: loam-maps (wave 2)

**Status:** **Sealed 2026-08-23** — owner-approved.
 Module id `loam-maps` in the
[loam capability map](../loam-map.md). Depends on `loam-turn`.
**Design source:** [empire-economy-ssot.md](../empire-economy-ssot.md) §3–§4 ·
[loam-map.md](../loam-map.md) A5.

**This module ends at the program's ⭐ gate.** Everything after it is justified by what playing this
map tells us.

## Objective

Give the mechanism a map that can actually exercise it, and declare the size vocabulary.

Success looks like: a world where rootbeds are scarce and contested, corridors are barren and
transient, the Fracture is fierce in some places and mild in others, and both commanders have a
capital worth losing — with the map's teaching properties **asserted by tests**, not hoped for.

## Design

### A scoping correction, made honestly up front

The capability map lists *"the five-tier size ladder"* as this module's job. Hand-authoring it is not
possible: `WorldTemplateCatalog` runs about **33 lines per sector** for `first-light`'s six. A
`medium` map is ~500 lines, a `large` ~1000, and a `giant` around **4,000 lines of hand-written
sectors**. Nobody should write that, and nobody should review it.

> **So this module ships the ladder as a *catalog*, and authors only the tiers a human can write.**
> `large` and above are declared and **unavailable**, gated on `world-generator`. That module was
> always the one that produces maps at scale, and the loam program should not grow a second one.

| Id | Display | Nodes | This module |
|---|---|---|---|
| `small` | Pocket | ~8 | `first-light`, updated — kept as the regression map |
| `medium` | Fragment | ~16 | **`two-hearths`, newly authored — the gate map** |
| `large` | Expanse | ~32 | Declared, unavailable |
| `huge` | Abyss | ~64 | Declared, unavailable — and **A5 must be measured before it is offered** |
| `giant` | Maelstrom | ~128 | Declared, unavailable — needs the Tarjan-first optimisation first |

Ids are plain and display names are content, per `resource-hub-ssot.md` §3. That also dodges two real
collisions: `reach` appears in 31 source files (`ReachMap`, `SupplyReach`) and `hollow` is already a
sector id in `first-light`.

### `two-hearths` — the gate map, and what it is built to prove

Named for its structure: **two capitals, symmetric in kind but not mirrored in shape**, since ideal
§12.3 makes Zomboss run the same economy the player does.

> **"Capital" here means a dense cluster of rootbeds, not a second homeworld.** `WorldValidation.cs:149`
> requires exactly one `Flags.Home` sector per world and that rule stays untouched — an earlier draft
> of this spec would have been rejected by the validator on creation (map §7, **S6**). After the S3
> resolution nothing in the loam rules reads `Flags.Home` at all, so Zomboss's capital is simply the
> ground where his sources are concentrated. The map needs no model change whatsoever.

Design targets, each one a thing `first-light` could not do:

| Property | Target | Why |
|---|---|---|
| **Rootbed scarcity** | ~4 sectors of 16 carry one | If most ground is habitable, the settlement rule has no teeth (§8.10) |
| **Barren corridors** | ~6 sectors with no Seat and no rootbed | Transient ground — takeable, never keepable. The map needs places that are permanently nobody's |
| **A chaos gradient** | intensity from well below baseline at the capitals to well above in the middle | §12.6. A flat field teaches nothing, and it is what makes deep ground a *quality*, not a distance |
| **A severable waist** | at least one lane whose loss splits a faction's territory into two components | The S3 resolution makes this the sharpest play in the game; a map that cannot be cut cannot demonstrate it |
| **A hot sector** | at least one with **several** rootbeds, high intensity, between the two capitals | §12.5 — the profit centre both sides want, which is how a front line appears without anyone authoring one |
| **Two capitals** | both habitable, both dense, both losable — **as clusters, not as `Home` sectors** | §12.7. Zomboss's economy must be attackable or half the design is untested |
| **≥ 2 articulation points** | measured, not assumed | So `ReconnectionCost` reports something real and severing means something |

**`first-light` gets the minimum change to stay legal and stay useful**: a rootbed on the homeworld
(required by `loam-model`'s validation) and an authored intensity field. It stays the small regression
map that every existing world test runs against; it is explicitly **not** the map the mechanic is
judged on.

### Assert the map's teaching properties, not just its validity

This is the module's most important idea and it comes from a scar. W37 predicted that `first-light`
would under-exercise the AI, and it did — `Explore` fired three times and never again. The prediction
was written down and nothing enforced it, so the map drifted from the thing it was supposed to teach.

> **Every property in the table above becomes a test on the template.** Not "the map is valid" —
> `WorldValidation` already says that — but "the map can still teach what it was built to teach."

A future edit that quietly makes every sector habitable then fails a named test that says why it
matters, instead of silently making a later playtest meaningless.

### Authored, not generated, and deterministic either way

`two-hearths` is built the way `first-light` is: a code-authored template, deterministic in
`(templateId, seed)`, validated before it is returned. Intensity and rootbed placement are **authored
constants**, not seeded rolls — this is the map the mechanic is judged on, and a map that varies
between runs cannot be reasoned about when the economy misbehaves.

### Goldens move a second time, and it is the last time in this program

`loam-model` moved them for the fields; this moves them for the template. Two known moves, both with
written reasons, is the same shape W20 took. **A third would need explaining**, and the wave-1 and
wave-2 specs both say so.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World
$env:FUSIONRPG_BLESS_WORLD_FIXTURE=1; dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World
```

## Project structure

```
src/FusionRpg.Core/World/WorldSizeCatalog.cs        → the five tiers, availability flag, node budget
src/FusionRpg.Core/World/WorldTemplateCatalog.cs    → two-hearths; first-light's minimum update
src/FusionRpg.Core/World/WorldValidation.cs         → a template's node count matches its declared size
tests/FusionRpg.Core.Tests/World/TwoHearthsTests.cs → the teaching-property assertions
tests/FusionRpg.Core.Tests/World/WorldSizeTests.cs
web/fusion-rpg-web/src/.../world.fixture.json       → regenerated
```

If `WorldTemplateCatalog` passes ~700 lines with `two-hearths` in it, split per template — a file that
is one big authored map plus another is a file nobody reviews carefully.

## Code style

Authored data reads as data: one sector per block, slots in index order, lanes grouped by cluster, and
a comment above each cluster saying what it is *for*. The map is a design document that happens to
compile, and the next person to tune it needs to know which sector was meant to be the hot one.

## Testing strategy

**Teaching properties** — one test per row of the design-target table, each named for the property and
each failing if the map drifts.

**Determinism** — `two-hearths` builds twice from the same `(templateId, seed)` and is canonically
identical; every collection is in stable order.

**Playability, end to end** — a scripted run on `two-hearths` where a legion takes a rootbed sector,
holds it, is cut off, and loses it. If that story cannot be told on this map, the map is wrong and no
number will fix it.

**Regression** — every existing world test still passes on the updated `first-light`, with exactly one
golden re-bless and its reason recorded.

**Size catalog** — an unavailable tier refuses at creation with a reason naming the tier, rather than
building a map nobody can afford to compute.

## Boundaries

- **Always:** authored constants for the gate map; teaching properties asserted; ids plain and display
  names content; one re-bless with its reason.
- **Ask first:** authoring a `large` map by hand (the answer is `world-generator`); making `huge`
  available before A5 has actually been measured; any third golden move.
- **Never:** seeded randomness in `two-hearths`' rootbed or intensity placement; a template that
  cannot demonstrate every property in the table; letting `first-light` become the map the mechanic is
  judged on.

## Success criteria

1. `two-hearths` exists, is deterministic, and every teaching property has a passing named test.
2. The cut-off-and-lost story runs end to end on it.
3. `first-light` still passes every existing test, updated minimally.
4. Unavailable size tiers refuse with a reason.
5. Exactly one golden re-bless and one FE fixture regeneration.

## ⭐ The gate

With this module green, the program's question can be answered by playing rather than by arguing:

> **Does anchoring make the map interesting?** Is deciding what to hold and what to let go a decision
> worth making, ten turns in a row?

Owner-only. No test can sign it, the same way the AI program's legibility playtest could not be
signed.

### The playtest brief — read this before playing, not after

Written down because the framing is the fragile part, and a framing that has to be *remembered* is a
framing that will be forgotten (map finding **G-F**).

**Play ten turns on `two-hearths` and answer three questions:**

1. **Is choosing what to let go a real decision?** When a component cannot pay, did you have an
   opinion about which sector should go — or did it not matter which?
2. **Does the fade read as tense, or as bookkeeping?** Watching stability slide should feel like a
   clock. If it feels like an accountancy error, the mechanic is not landing.
3. **Is a split economy frightening?** Sever a lane deliberately. Does "half my empire is starving"
   arrive as a *shock*, or does it have to be worked out from numbers?

**What this playtest cannot tell you, and must not be blamed for.** The reward layer does not exist
yet: souls, essence and materials all arrive with structures, after the gate. **Territory currently
pays only in the ability to hold more territory.** So:

> A verdict of *"it works, but it feels pointless"* is **the expected result**, not a failure. It means
> the reward layer is missing — which we know. The mechanic is condemned only by *"I did not care which
> sector I lost"*, or *"I could not tell what was happening"*.

There is a thin expansion loop even now — rootbed-dense sectors are net-positive, so taking them buys
capacity to take more. That is enough to judge the *decision*. It is not enough to judge the *game*,
and nothing at this gate is being asked to.

**One known artefact:** Zomboss has a single survival rule and no loam strategy
(`loam-ai-survival`). If he plays passively or shrinks slightly, that is his one rule working, not the
economy failing. If he *dissolves*, that is a finding worth reporting. `loam-legions`, `loam-ai`, `structure-substrate` and everything after are justified by the
answer — and if the answer is no, the right column of the capability map's cut list is work we never
did.

## Decided (2026-08-23)

- **`medium` is a range, 14–18 nodes, not a number.** The authored map should land where its teaching
  properties want it; a size catalog that demands exactly sixteen would make the map serve the catalog.
  Built at exactly 16 sectors, comfortably inside the range.
- **`first-light` stays the default template until the gate is passed.** An unreviewed map should not
  become the thing every new save gets. `WorldTemplateCatalog.Build`'s dispatcher still resolves both
  ids explicitly; nothing defaults to `two-hearths`.
