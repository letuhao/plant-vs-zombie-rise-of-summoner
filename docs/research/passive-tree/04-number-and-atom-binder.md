# Passive trees, stage 3 — the number and atom binder

**Status:** research, 2026-09-05. **Not a spec. No build authorized.** Stage 3 of the pipeline
[passive-tree-ideal.md](../../architecture/passive-tree-ideal.md) §6 sets out: the deterministic plan
fixes structure (stage 1), the language stage fills vocabulary and pools (stage 2), and **a second
deterministic engine binds each node to real atoms with real magnitudes** — this document.

Owner's question, verbatim: *"then how our other deterministic engine solve the number and concrete
atom effort [effect]?"*

**Owner constraint, 2026-09-05:** the tree catalog is **static and identical for every player**.
Concrete numbers are baked before the game runs, learnable and planable. Not the loot model.

Claims are marked **FACT** (verified against code this session, cited), **INFERENCE** (reasoned from
facts), or **RECALL** (from a document, not re-derived).

---

## 0. Answer up front

1. **The vocabulary is bigger than the design gate says.** Verified by counting in code today:
   **7 attach points, 16 kinds, 13 triggers** (11 of the 13 authorable). `DESIGN-GATE.md:40` says
   *5 / 12 / **8***. All three numbers are stale, and the row is the file that "wins over any spec".
   §1 has the count, the citation, and the list.

2. **A passive node is an affix, inside a `skill` container.** Not a bare atom, not a new entity.
   The container schema already carries every field a node needs — including the tier window that
   D20's tier gate maps onto. The layer is **reachable**: `effect-pipeline` module 4 has landed and
   has real production callers (§2).

3. **The binder emits a coefficient, not a magnitude.** One integer per node — the node's share of
   `P(Θ)`, in **per-million** — baked into the catalog. The concrete number is derived per actor at
   compile time by the shipped `ValueSpec.PowerLadder` source. That is what makes one static catalog
   correct for every player: everyone's tier-5 fire-power node stores the same coefficient, and the
   numbers differ only because `Θ` differs. §3 gives the formula chain and a worked example.

4. **The shipped coefficient field is per-mille, and per-mille is too coarse.** `PowerLadderKMilli`
   rounds a tier-1 node's coefficient with a **~17% error** (§3.5, arithmetic shown). This is the one
   real change stage 3 needs: a per-million sibling field, resolved by the same three lines at
   `AtomCompiler.cs:456-466`. Small, reviewed, and better found now than at build.

5. **The soul track adds to `Θ`; it never multiplies the coefficient.** `Θ_node = Θ_actor +
   Ws·soulLevel`. That is the only shape that keeps §10.5's linear-in-effort property; multiplying
   the coefficient gives power ∝ √effort and a decaying reward rate. §4 proves both.

6. **Yes, the soul track needs exactly one new row in `ssot-power-scale.md` §10.2** — for the
   soul→`Θ` weight `Ws`, by direct precedent of rows 18 and 19. It needs **no** new row for the
   magnitude function, which is `P(Θ)` read through the shared `PowerLadder` (row 16's own precedent).

7. **Four of thirteen unit classes are legal targets for a "+X" node.** `SigmoidPoints`,
   `SigmoidMultiplierPoints`, `StatusPotencyPoints`, `PerMilleRatio`, `Flag`, `Count`, `Milliseconds`,
   `LadderIndex` and `AptitudePoints` each need different treatment or refuse outright. §5 is the rule
   table; a naive `+X` on any of them is a design error, and two of them fail *silently*.

8. **Conversion nodes (D16) are the one genuine new capability.** There is no element-payload writer
   among the 16 kinds. This is **not** a wiring gap — it is a 17th kind, i.e. a reviewed change to
   `decisions.md`'s "Atom attach points" row. §6 says exactly what it must write.

9. **Two of three mechanism archetypes are executable today; one is executable on the lawn and in
   battle but inert in sim** — which matters, because the sweep that produced §3.5's conclusion runs
   in sim. §7.

---

## 1. The atom vocabulary — counted, not quoted

**FACT.** Verified in `src/` this session, and each const is pinned to its real collection by a test,
so these are counts, not literals:

| | Count | Declaration | Guard test |
|---|---:|---|---|
| Attach points | **7** | `AtomKindRegistry.cs:21` (`AttachPointCount`); enum at `AtomKind.cs:8-30` | `AtomKindRegistryTests.cs:31` — `AttachPointCount == Enum.GetValues<AttachPoint>().Length` |
| Kinds | **16** | `AtomKindRegistry.cs:31` (`KindCount`); 16 `new("…")` rows at `:476-869` | `AtomKindRegistryTests.cs:30` — `KindCount == All.Count` |
| Triggers | **13** | `AtomKindRegistry.cs:36` (`TriggerCount`); `AtomTriggers.All` at `AtomKind.cs:95-99` | `AtomKindRegistryTests.cs:112` — `TriggerCount == AtomTriggers.All.Length` |

> ⛔ **`DESIGN-GATE.md:40` is stale on all three.** It reads *"5 attach points, 12 kinds, **8**
> triggers (`AtomKindRegistry.TriggerCount`)"*, and warns that the row said 7 until 2026-09-03. The
> row was corrected for `OnActivate` but not for E34's five match/board-economy triggers, nor for
> E35/E36/E37/E41's four kinds and two attach points. The class-level XML doc on
> `AtomKindRegistry.cs:6` still says *"5 attach points, 12 kinds"* too. **Code beats documentation;
> documentation beats comments** — the numbers above are what the registry builds.
> `decisions.md:112` (*"Atom attach points", 2026-09-04*) already has the correct seven.

### The seven attach points

`Stat` · `Resource` · `Status` · `Shield` · `Board` · `Match` · `Ui` (`AtomKind.cs:10-29`).

### The sixteen kinds

| Kind | Attach | Triggers it may carry | Lawn / Battle / Sim | Line |
|---|---|---|---|---|
| `stat.modify` | Stat | 6 (`AllTriggers`), **optional** | Full / Full / PlanOnly | `:476` |
| `stat.derived` | Stat | **none** — permanent modifier | Full / Full / None | `:505` |
| `resource.delta` | Resource | 6 (`AllTriggers`) | Full / Full / PlanOnly | `:541` |
| `resource.economy` | Resource | 9 (events + match + economy) | Full / None / PlanOnly | `:575` |
| `status.apply` | Status | 6 (`AllTriggers`) | Full / Full / PlanOnly | `:610` |
| `status.clear` | Status | 4 (`Events`) | Full / None / PlanOnly | `:638` |
| `shield.grant` | Shield | 6 (`AllTriggers`) | Full / Full / None | `:650` |
| `spawn.entity` | Board | 9 | Full / None / PlanOnly | `:683` |
| `board.action` | Board | 9 | Full / None / PlanOnly | `:732` |
| `grid.spawn` | Board | 9 | Full / None / PlanOnly | `:747` |
| `grid.clear` | Board | 9 | Full / None / PlanOnly | `:763` |
| `box.set` | Board | 9 | Full / None / PlanOnly | `:780` |
| `bullet.modify` | Board | **none** — permanent modifier | Full / None / None | `:813` |
| `match.modify` | Match | 3 (`MatchEvents`) | Full / None / None | `:828` |
| `wave.control` | Match | 3 | Full / None / None | `:846` |
| `ui.present` | Ui | 6 (`AllTriggers`) | Full / None / None | `:869` |

All line numbers are `src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs`.

### The thirteen triggers

`OnSpawn` · `OnDamageDealt` · `OnDamageTaken` · `OnDeath` (the four board events, `AtomKind.cs:102`) ·
`OnTimer` · `OnActivate` (`:118`) · `OnWave` · `OnMatchStart` · `OnMatchEnd` (match-scoped, `:121`) ·
`OnSunCollect` · `OnGridPlace` (board economy, `:124`) · **`OnGranted` · `OnRemoved`**, which are in the
enum but are **runtime lifecycle states no atom may name** — authoring either is `TriggerNotAllowed`
(`AtomKind.cs:104-111`, `definitions.md` §14.2). **So 11 of the 13 are authorable.**

**This is the entire expressive space a passive node has.** Everything in §7 is built from it.

---

## 2. What a passive node IS, structurally

**A node is an affix. A tree branch's skill is a `skill` container. The tree itself is content, not an
entity in the atom layer.**

**FACT.** `definitions.md` §4a: *"The pool's roll unit is an **affix** — a named bundle of atom refs
(which may include slots) that share the container's resolved slots and are drawn **together as one
roll**. `effect_container_pool` rows reference affixes, not bare atoms."* An affix is the right unit
because a real node is usually more than one atom — a reflect node is two `stat.derived` atoms that
must arrive together (§7, M2), and one-atom-per-row cannot correlate two draws.

**A node is not a container** — `container_kind` is a closed six-value set (`item | trait | skill |
species-passive | patron | world-buff`, `spec-container-schema.md`), and a node is a part of a skill,
not a peer of an item.

### The fields a node needs, and where each already lives

| Node needs | Shipped home | Note |
|---|---|---|
| An identity | `affix` id + `atom_id` = `{family}[.{variant}].t{tier}`, **derived not authored** (`definitions.md` §1) | An id that disagrees with its columns is `IdMismatch` |
| Its atoms | affix bundle → `effect_container_pool` rows (`spec-container-schema.md`) | |
| Its magnitude | `ValueSpec` on each atom's `amount` param | §3 |
| Its depth | the atom's `tier` column, plus the container's `min_tier`/`max_tier` window | D20's tier gate maps onto `min_tier`/`max_tier` directly |
| Its channel | `channel` param, validated against a live vocabulary (`AtomKindRegistry.cs:84-85` for derived, `:71` for primary) | An unregistered channel is refused at load, not silently written |
| Its condition | `when_json` predicate — 12 leaves, 2 subjects, depth ≤ 4, ≤ 16 nodes (`PredicateNode.cs:17-31`, `definitions.md` §3) | §7 |
| D14's exclusion property | `tags_json` on the container + the atom's own family/variant | D22's stated payoff: *"hands D14's property-keyed exclusions an existing property space: atom tags"* |

**Nothing here is passive-specific.** That is D22 satisfied by construction, not by discipline.

### The layer is reachable — module 4 has landed

**FACT, and the design gate explicitly asks for this check.** `DESIGN-GATE.md:41` warns that
`Instantiator`/`TryInstantiate` were *"built but had zero production callers until `effect-pipeline`
module 4 wires the call"*.

**Module 4 has landed.** `InstanceProducer.Compose`
(`src/FusionRpg.Core/Effects/Atoms/InstanceProducer.cs:28`) is the module-4 payoff, and it has two
real production callers outside its own tests:

- `src/FusionRpg.Core/Demons/Materialise/SpeciesMaterialiser.cs:55`
- `src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:341` (`ProduceAndBind`, which wraps the
  composed `InstanceRow` in a `BindingRow` and persists both)

It resolves the pool through `Resolver.Resolve` — the affix-aware five-step order — rather than
`Instantiator.Draw`'s single-ref path (`InstanceProducer.cs:20-24`).

**INFERENCE:** a passive-tree node therefore has a live path from catalog row to bound atom on a real
actor. There is no "the atom layer is unreachable" caveat to carry into stage 3.

### One caveat worth stating

**FACT.** A `skill` container uses the **fixed core alone** — `spec-container-schema.md` is explicit
that `trait.{traitId}` and `skill` containers do not roll a pool, while item templates and
`species-passive` containers do. That is exactly right for a static, learnable tree: a node's atoms
are fixed, and `prefix_rolls`/`suffix_rolls` are both `0`. **The affix bundle is the authoring unit;
the draw never runs.** So a passive tree uses the affix vocabulary without inheriting the loot model —
which is what the owner's 2026-09-05 constraint asks for.

---

## 3. The number binder

### 3.1 What the binder receives and what it emits

```text
IN   from stage 1 (plan)      treeShare, tree budget, tier weight, node count, potency ceiling
     from stage 2 (language)  kind_id, channel_id, op, tier, trigger, predicate

OUT  one integer per node:    kMicro  — the node's share of P(Θ), in per-million
```

**It does not emit a magnitude.** A magnitude would have to be baked at some Θ, and then either be
wrong for everyone else or be re-scaled a second time — which PS-2 forbids (*"a magnitude is scaled
exactly once"*).

### 3.2 The runtime read is already shipped

**FACT.** `ValueSpec` carries a `PowerLadder` source with a `PowerLadderKMilli` coefficient
(`src/FusionRpg.Core/Effects/Atoms/ValueSpec.cs:90-93`), added by `seed-to-concrete` T6.2 and
owner-approved 2026-09-02. It resolves at compile time in `AtomCompiler.ResolvedParams`:

```csharp
var pThetaValue = new PowerLadder(powerTuning).Value(theta);
result[key] = checked((int)((long)spec.PowerLadderKMilli * pThetaValue / 1000));
```
`src/FusionRpg.Core/Effects/Atoms/AtomCompiler.cs:463-464`

That single line already satisfies four of CLAUDE.md's five overflow rules: `PowerLadder.Value`
returns `long`, the coefficient is **widened before multiplying** (`(long)spec.…`), the divide by 1000
is **last and exactly once**, and the narrowing is `checked` so it **throws, never wraps**. It refuses
rather than guessing when no owner `Θ` is in scope (`:458-461`).

**This is the right source for a passive, and `Min/Max` + `contentScale` is the wrong one.**
`ContentScale` is applied once inside `Instantiator` and freezes at the moment of instantiation
(`src/FusionRpg.Core/Power/ContentScale.cs:5-9`) — correct for a dropped item, wrong for a passive
that must track its owner's `Θ` for the rest of the game.

### 3.3 The formula chain — budget share to stored integer

All inputs are integers. **One division, at the end.**

```text
inputs (all long, all from data/tuning/passive-tree.v1.json or the plan's emitted shape):

  treeShareMilli        how much of an actor's power the tree layer carries at full investment
  treeBudgetMilli       this tree's share of that (1000 for every tree — D15, equal expected value)
  tierWeight(t)         the plan's per-tier weight.  D20's binding pairing rule fixes this: LINEAR,
                        so tierWeight(t) = t
  weightTotal           Σ tierWeight over every node in the tree  (branches × nodesPerTier × Σt)
  channelAnchorMilli    the channel's own pin at Θ=20, over hp's pin:  pin_ch · 1000 / pin_hp

  num    = treeShareMilli * treeBudgetMilli * tierWeight(t) * channelAnchorMilli
  denom  = 1000 * weightTotal
  kMicro = roundHalfAwayFromZero(num, denom)
```

*(Dimensionally: `k` is a dimensionless fraction of `P(Θ)`; three of the four inputs are per-mille, so
the product carries `1000³`, and expressing the answer in per-million cancels `1000²` of it — leaving
one `1000` in the denominator. Written this way there is exactly one division, as rule 4 requires.)*

`num` is bounded by `1000 · 1000 · t · ~1000` — for any plausible tier count that is well under
10¹⁰, eight orders inside `long`. **FACT:** `long`, widened before multiplying, divided once, and the
rounding is half-away-from-zero — the same convention `PowerLadder.RoundHalfAwayFromZero`,
`ContentScale.Apply` and `ChannelLadder.RoundHalfAwayFromZero` all use, so nothing new is invented.

**`channelAnchorMilli` — why it is needed.** `P(Θ)` is hp-shaped: it is pinned at `P(20) = 680`, which
is `BattleRuleset.BaseHp(20)` (`ssot-power-scale.md` §4.3, and `data/tuning/power-scale.v2.json`'s
`curve.pinValue`). `combat.power` is atk-shaped and `combat.defense` is defense-shaped, and the shipped
tuning file already publishes their pins: `atk` 92, `defense` 22
(`data/tuning/power-scale.v2.json`, `channels` block). So `channelAnchorMilli(atk) = 92·1000/680 = 135`
and `channelAnchorMilli(defense) = 22·1000/680 = 32`.

> **The exact alternative, named rather than hidden.** `ChannelLadder`
> (`src/FusionRpg.Core/Power/ChannelLadder.cs`) already computes a channel's own ladder exactly —
> `B_ch = B · pin_ch / pin_hp`, carried as one `long` numerator over a `long` denominator and rounded
> once. It is *more* correct than folding a constant ratio into `kMicro`: the ratio approach reproduces
> the pin exactly at Θ=20 and diverges slightly at low Θ (for atk at Θ=0 it gives 10.8 against
> `C_ch = 12`), converging as Θ grows. But `ValueSpec.PowerLadder` reads `PowerLadder.Value` only, so
> using `ChannelLadder` needs a **second reviewed `ValueSpec` source**. Recommendation: fold the ratio
> (zero code change, bounded and monotone error, concentrated where numbers are smallest), and record
> `ChannelLadder` as the upgrade if the low-Θ divergence ever shows up in play.

### 3.4 Worked example, end to end

Plan output for one tree: 2 branches × 7 tiers × 2 nodes per tier per branch = **28 nodes**.
`weightTotal = 2 × 2 × (1+2+…+7) = 112`.

Tunables: `treeShareMilli = 1000` (a fully-invested tree is worth one `P(Θ)` of derived power),
`treeBudgetMilli = 1000`.

Node: **tier 5, offensive branch, `combat.power.fire`, `stat.derived`, op `flat`.**
`channelAnchorMilli = 135` (atk anchor).

```text
num    = 1000 · 1000 · 5 · 135        = 675_000_000
denom  = 1000 · 112                   =     112_000
kMicro = round(675_000_000 / 112_000) =       6_027      (0.6027% — i.e. 6.027‰ — of P(Θ))
```

Stored in the catalog: `"kMicro": 6027`. Nothing else about the magnitude is stored.

At runtime, per actor:

| Θ_actor | `P(Θ)` (whole, B=0.4) | magnitude = `kMicro · P(Θ) / 1e6` |
|---:|---:|---:|
| 20 (the pin) | 680 | **4** fire power |
| 50 | 1,880 | 11 |
| 100 | 4,680 | 28 |
| 500 | 63,080 | 380 |
| 1,000 | 226,080 | 1,363 |

`P(Θ)` values are `ssot-power-scale.md` §4.5's own published table for the shipped dial
(`bMilli = 400`, `cMilli = 80000`, `A` derived to 26200 by the pin — `data/tuning/power-scale.v2.json`).

**The node is 0.6% of the actor's atk-equivalent power at every Θ, by construction.** That is the
whole point: the catalog number is a share, the ladder supplies the scale, and there is exactly one
ladder.

### 3.5 The one real problem the binder surfaces — per-mille is too coarse

**FACT.** The shipped field is `PowerLadderKMilli`, an `int` in **per-mille**
(`ValueSpec.cs:92`). Re-run §3.4 at per-mille resolution:

| tier | exact k (‰) | stored `kMilli` | error |
|---:|---:|---:|---:|
| 7 | 8.438 | 8 | −5.2% |
| 5 | 6.027 | 6 | −0.4% |
| 3 | 3.616 | 4 | **+10.6%** |
| 1 | 1.205 | 1 | **−17.0%** |

A 17% error on a tier-1 node is not a rounding detail — it is larger than the gap between two tiers,
so the plan's *linear* per-tier power (D20's binding pairing rule) is destroyed at the shallow end,
and with it §10.5's flat reward-per-point property. Raising `treeShareMilli` does not fix it; the error
is scale-invariant in the ratio and only moves which tier is worst.

**The fix, stated as a reviewed change and not smuggled in:** a per-million sibling on `ValueSpec` —
`PowerLadderKMicro`, mutually exclusive with the existing sources exactly as `PowerLadder` already is
(`ValueSpec.Validate`, `:126-135`), resolved by the same three lines at `AtomCompiler.cs:456-466` with
`/ 1_000_000`. At per-million the worst error above becomes **0.04%**.

**INFERENCE, using CLAUDE.md's own framing rule:** this is a **wiring gap**, not an architectural wall.
The path exists end to end, it is executable today, and what is missing is one field's resolution.

### 3.6 Where the balance numbers live

Per `tunables-ssot.md` §1-2 and invariant 12: `data/tuning/passive-tree.v1.json`, with the standard
`schemaVersion` / `version` / `_meta.owner` / `_meta.rebalance` header.

| Key | Class | Why |
|---|---|---|
| `treeShareMilli` | **tunable** | the ideal §3.3 flags this as *"currently unknown and worth deciding deliberately"* — it is the single biggest balance dial the tree layer has |
| `tierWeightShape` | tunable | D20 fixes it linear *today*; a balance pass could change the slope without changing the shape |
| `channelAnchorMilli` per channel family | **derived, not authored** | computed from `power-scale.v{n}.json`'s own pins at bake time, so a dial change cannot leave it stale |
| `Fmax`, `w` (D4, D8) | tunable | already named as tunable by the ideal §8 |
| `Ws` — soul weight into `Θ` | tunable | §4 |
| `weightTotal`, node counts | **structural** | they are the plan's own shape; changing one changes what the tree *is*, not how it feels. Comment must say so |

**No cap anywhere in this chain.** The only ceilings are the two absolute bounds in §4.3, and both
throw.

---

## 4. The soul track (D3) — the deepen ladder

### 4.1 What one soul level multiplies: nothing. It adds to `Θ`.

```text
Θ_node   = Θ_actor + Ws · soulLevel(node)          Ws tunable, integer, per-mille if fractional
magnitude = kMicro · P(Θ_node) / 1_000_000
```

The node's coefficient `kMicro` is **fixed by the catalog and never changes**. Soul levels move the
index the ladder is read at.

### 4.2 Why the naive alternative is wrong, with the arithmetic

The obvious design is *"each soul level adds x% to the node's bonus"* — i.e. soul level scales
`kMicro`. Compare the two against §10.5's property.

Arithmetic soul cost (D3, *"unlimited, arithmetic cost"*): cumulative cost of `L` levels is
`Σ(first + (k−1)·step) ≈ (step/2)·L²` — **quadratic in L** (`ssot-power-scale.md` §10.5).

| Design | Power after `L` levels | Power per unit effort |
|---|---|---|
| **Coefficient scaling** (naive) | `k·L · P(Θ)` — **linear in L** | `∝ L / L² = 1/L` — **decays.** Hour 500 buys a fifth of what hour 5 bought |
| **Index offset** (this design) | `k·[P(Θ₀+L) − P(Θ₀)] = k·[A·L + B·(Θ₀L + L(L−1)/2)] ≈ k·(B/2)·L²` | `∝ L²/L² = k·B/step` — **constant** |

The second line is §10.5's proof, applied to a node instead of an actor:

```text
cumulative soul cost   ≈ (step/2)·L²          quadratic
power gained           ≈ k·(B/2)·L²           quadratic
                       ⇒ power ∝ souls spent  LINEAR in effort
```

**INFERENCE:** the index offset is not one option among several. It is the only shape that preserves
the property the ideal §4 already claims for this track, and the ideal's own §4 arithmetic is exactly
this proof written for the actor rather than the node.

### 4.3 Overflow at extreme soul counts

**FACT.** Two absolute bounds sit on this path. Both **throw**; neither clamps.

**Wall 1 — the compiled parameter is `int`.** `AtomCompiler.cs:464` narrows with `checked((int)…)`,
so an `OverflowException` is thrown the moment `kMicro · P(Θ_node) / 1e6 > 2,147,483,647`.

With the shipped dial, `P(Θ) = 0.2Θ² + 26Θ + 80` (whole units, from `cMilli=80000`, `AMilli=26200`,
`bMilli=400`).

| node coefficient | `P(Θ)` at the wall | **Θ_node at the wall** |
|---|---:|---:|
| `kMicro = 1_000_000` (a whole-`P(Θ)` node) | 2.147×10⁹ | **103,557** |
| `kMicro = 6_027` (§3.4's tier-5 node) | 3.56×10¹¹ | **1,334,000** |

**103,557 is exactly CLAUDE.md's own published figure** for `int` whole units — the passive tree hits
the table's row 3 without needing a new analysis.

**Wall 2 — the ladder itself.** `PowerLadder.Guard` throws `PowerIndexOverflow` above `MaxIndex`
(`src/FusionRpg.Core/Power/PowerLadder.cs:100-104`), and `MaxIndex` is *computed from the loaded curve*
by binary search (`:63-80`), never a constant. For `bMilli = 400`: `ValueMilli ≈ 200Θ²` reaches
`long.MaxValue` at **Θ ≈ 214,748,300** — again exactly CLAUDE.md's published `long` row.

**So: with `Ws = 1`, a `long` would overflow at soul level ≈ 214,748,300 − Θ_actor.** At an arithmetic
cost ladder of step `s`, reaching it costs ≈ `(s/2)·(2.15×10⁸)² ≈ s · 2.3×10¹⁶` souls. That is not a
ceiling anyone reaches; it is a type boundary, and it throws.

**The wall that actually binds is wall 1, and it is a wiring gap.** CLAUDE.md rule 1 is *"`long` for
any magnitude"*; `AtomCompiler.cs:464` produces an `int`. Widening the `powerLadder` path's result to
`long` moves the first refusal from Θ ≈ 103,557 to Θ ≈ 214,748,300, and costs one cast. It is worth
naming here because the soul track is the first system that can push a single node's magnitude that
far — every prior consumer of this line was an item affix at content depth.

Related and already on the books: `ssot-power-scale.md` §11.1 lists `ShieldMath.MaxInput` and
`ResourceDeltaMath.AmountCap` (both `1_000_000_000`) as **conflicts that must change** — the first
clamps silently. A tree node feeding either path inherits that wall long before it inherits `int`'s.

### 4.4 Does the soul track need a new §10 row? **Yes — exactly one.**

**Definitive answer, with the precedent for each half:**

| Half of the design | New §10 row? | Precedent |
|---|---|---|
| **The magnitude function** — `magnitude = k · P(Θ_node)` | **No.** | Row 16's `pThetaTermMilli` (patron aura, T22, owner-signed 2026-08-30): *"it calls the shared `PowerLadder`, not a private `f(level)`, so §10's anti-duplication clause is satisfied."* Reading the one ladder is never a new scale |
| **The soul→`Θ` weight `Ws`** — how a soul level becomes ladder index | **Yes — one row in §10.2.** | Row 18 (`thetaOffset`, species threat rung) is the exact shape: *"lives **inside `Θ`** itself, additive, before `P(Θ)` runs — not a bounded display value scaled a second time"*, and it got its own row. Row 19 (action unlock ladder) got one for a non-`Θ` progression input on the same principle |

The row must also record that `Θ_node` is **derived at the read site and never persisted as a second
actor `Θ`**, and `Ws` belongs in the tuning file alongside §5's other weights — not in
`power-scale.v{n}.json`'s `weights` block, because those compose `Θ_actor` and this one does not.

**What §5 does *not* need:** a change. `Θ_actor`'s composition is untouched. A per-node index is a
local offset, and merging it into `Θ_actor` would make one node's souls raise every other node's
magnitude — the *"spend all in one is risk and reward"* design (owner, 2026-09-04) requires the
opposite.

---

## 5. Channel legality

### 5.1 How many channels there are, and how they are classified

**FACT.**

- **23 primary channels** — `StatChannels.All`, `src/FusionRpg.Core/Stats/ModifierOp.cs:68-75`
  (11 since E16, 23 since E38). This is `stat.modify`'s vocabulary (`AtomKindRegistry.cs:71`).
- **267 registered derived channels** — asserted in three separate test files
  (`AtomCatalogSsotDriftTests.cs:46`, `ElementHubDocDriftTests.cs:73`, `SeedCatalogTests.cs:28`,
  `StatTaxonomyTests.cs:183`), and the combat block is asserted **as the formula**
  `CombatChannelFamilies.Count × (ElementRoster.Concrete.Count + 1)`, not a literal
  (`DerivedStatRegistryTests.cs:24`). This is `stat.derived`'s vocabulary
  (`AtomKindRegistry.cs:84-85`, resolved fresh on every `Validate` so the vocabulary widens with the
  registry and no guard edit is needed).
- **Nine open-ended prefix families** resolve dynamically in `TryResolveChannel`
  (`spec-derived-stat-sheet.md` §1) — unbounded, and the sheet's own count of what the shipped
  21-status catalog could expand them to is **+126**.

**Two classifications exist and both are normative. There is no third.**

1. **`UnitClass` — thirteen classes** (`src/FusionRpg.Core/Stats/Derived/StatClass.cs:29-100`,
   `docs/design/spec-magnitude-and-units.md` §3). *"What arithmetic is this channel, and how does it
   render."* **Note:** `spec-magnitude-and-units.md` §3's heading says thirteen and the enum's own doc
   comment at `StatClass.cs:26` still says *"ten-class"* — the comment is stale; the enum has 13
   members and `DESIGN-GATE.md:34` says *"nine-class"*, staler still. Counted, not quoted.
2. **`StatClass` — four values** `Contest | Race | Pool | Feeder` (`StatClass.cs:9-22`), explicitly
   *"orthogonal to `UnitClass`"*.
3. **Six render states** — `active | default | capped | stub | no-producer | unregistered`
   (`spec-derived-stat-sheet.md` §3).

`DESIGN-GATE.md:34` warns that inventing a third classification is a known past failure. **Stage 3
invents none.** The table below is a *use* of `UnitClass`, keyed by it.

### 5.2 The rule table — which classes a "+X" passive node may target

| `UnitClass` | Example channels | Legal as a magnitude node? | The rule the binder applies |
|---|---|---|---|
| **`GameUnits`** | `combat.power.*`, `combat.defense.*`, `combat.shield.{capacity,toughness,pen}.*`, `combat.{parry,block}.{strength,shred}.*`, `hp` `maxHp` `atk` `defense` `arm*` | ✅ **the canonical case** | `kMicro · P(Θ_node) / 1e6`, `channelAnchorMilli` from the channel's own pin |
| **`GameUnitsPerSecond`** | `combat.shield.regen.*` | ✅ | same read, but the anchor must be a **per-second** pin. Folding the hp pin in here silently multiplies the node by the tick rate |
| **`ReciprocalPoints`** | `combat.{penetration,absorption,amplification,reduction}.*` | ✅ **with care** | uncapped points feeding `PierceFactor(d,s) = 1/(1+max(0,d)/s)` — asymptotic. `P(Θ)`-scaling is legal (nothing clamps), but the plan must budget the **factor**, not the points: doubling points past the scale buys almost nothing |
| **`SigmoidPoints`** | `combat.accuracy.*` `dodge` `crit.rate` `crit.resist` | ⛔ **`+X · P(Θ)` is a design error** | **PS-1**: `contentScale` never touches a rate input. **PS-3**: contests read `Θ`, linear. A node here grants `k·Θ_node`, never `k·P(Θ_node)`. Getting this wrong destroys the parity invariance the rate tests lock |
| **`SigmoidMultiplierPoints`** | `combat.crit.damage.*` `crit.resist.damage.*` | ⛔ **worse — it saturates** | Same PS-1/PS-3 rule, **plus** the multiplier is bounded to (1.0×, 2.0×) (`spec-magnitude-and-units.md` §3, §4.1's table). Past ≈+250 points a node buys almost nothing, so a magnitude node here is measurably worthless at depth *and* the soul track on it is dead. **Fails silently** — the number on the sheet goes up |
| **`StatusPotencyPoints`** | 30 registered `status.{power,resist,duration,durationReduction,intensity,intensityReduction}.*` defs + 6 of the 9 open families | ⛔ | `StatClass.Contest` with a declared `CounterpartOf` — a contest, so `Θ`-linear. **Also capped:** `status.resist.{dot,cc,contagion}` carry `_categoryResistCap` (`DerivedStatRegistry.cs:106-110`, and again for the sparse path at `:330`), and `status.resist.omni` deliberately does **not** — so two cells in the same row behave differently. A node stacking past the cap does literally nothing (`spec-derived-stat-sheet.md` §3, the `capped` state) |
| **`PerMilleRatio`** | `combat.reflect.{rate,damage,resist.rate,resist.damage}.*`, `combat.{parry,block}.{rate,break}.*` | ⚠️ **flat points only** | Bounded ratio — **exempt from PS-8 by nature**, and clamped in code: `Math.Clamp(…, 0.0, 1.0)` at `CombatDamageDispatcher.cs:99,104` and `Math.Max(0, …)/1000` at `OverlayCombatCalculator.cs:183-184`. A node grants **flat per-mille points planned against the clamp**. `P(Θ)`-scaling one saturates it in a few tiers and kills the soul track on that node. **Say so in the node's comment** — a bounded ratio must declare it (PS-8) |
| **`Milliseconds`** | durations, `icd_ms` | ⛔ as a magnitude | A duration is not a power magnitude. `P(Θ)`-scaling it produces unbounded uptime, which is a *mechanism* change dressed as a number |
| **`Count`** | `count`, `maxTargets` | ⛔ | Discrete. A `P(Θ)`-scaled count is an unbounded spawn/target explosion and collides with §11.3's per-frame runtime caps, which are **perf protection and legitimately hard** |
| **`Flag`** | `status.immune.{tag}`, `status.immuneReduction.{tag}` | ⛔ — **never a number** | `MaxPriorityFlag`, cap 1 (`spec-magnitude-and-units.md` §3). A node here is a switch; the plan must budget it as a mechanism, not a magnitude |
| **`LadderIndex`** | `progression.power`, `progression.realm` | ⛔ **forbidden** | This **is** `Θ`. A node writing it is a private second ladder — the exact defect §10 exists to end. Both are also in the `stub` render state today, pinned at 1.0 (`spec-derived-stat-sheet.md` §3) |
| **`AptitudePoints`** | the twelve aptitudes | ⛔ **structurally impossible, and that is a feature** | An aptitude is a **SOURCE, not a registered channel** (`decisions.md:103`), so it is not in `DerivedChannels()` and `stat.derived` refuses it at load. This is the same construction D11 relies on: *items grant POINTS, not node unlocks* |
| **`LoamUnits`** | loam | ⛔ | Not a derived channel. `resource.economy`'s vocabulary is `sun\|money\|points\|maxSun\|maxMoney`; loam/soul/essence/shard *"share no member with this vocabulary and are not atom-authorable"* (`AtomKindRegistry.cs`, `resource.economy` note) — a hard load-time refusal |

### 5.3 The three that fail silently — flag these to the generator

**INFERENCE, and this is the part a generated corpus will get wrong:**

1. **`SigmoidMultiplierPoints`.** The sheet shows the number rising; the multiplier does not.
   Nothing errors.
2. **Capped `StatusPotencyPoints`.** A node past `_categoryResistCap` composes, renders, and does
   nothing. The `capped` render state exists precisely to make this visible — but only if the node's
   own budget was priced against the cap in the first place.
3. **`takeDmgMultiplier`.** `LowerIsBetter` in the **bearer frame** — *"this channel is NOT the
   authoring surface for 'enemies take more damage'"*, and raising your own prices **negative**
   under the cost function's sign flip (`ModifierOp.cs:57-64`). A generator reading the channel name
   and writing `+X` authors a self-nerf that the power vector correctly prices as a penalty and the
   node's own copy describes as a buff.

**The plan's property vocabulary (D13/R7 step 2) is the right place to encode all three**, because
D14's exclusions already key on properties and `UnitClass` is a property the plan can emit.

---

## 6. Conversion nodes (D16)

### 6.1 What `ElementPayload` actually is

**FACT.** `src/FusionRpg.Core/Combat/Element/ElementPayload.cs`:

- A `sealed class` with a **private constructor** (`:12-13`) — the only way in is `From(components)`,
  which runs `Validate` (`:18-22`).
- `Validate` throws on an empty list, on **any weight ≤ 0**, and unless the weights sum to `1.0`
  within `WeightSumEpsilon = 1e-6` (`:24-37`). `WeightSumEpsilon` is declared structural with a
  comment saying so (`:7`).
- A component is `ElementPayloadComponent(ElementTypeId Element, double Weight)`
  (`ElementPayloadComponent.cs:5`).

### 6.2 Why D16 is right — the failure is silent

**FACT.** `OverlayCombatCalculator` reads **element-keyed derived channels per component, looping the
payload's own component list**:

```csharp
foreach (var c in request.Components)
{
    ...
    weightedOffense += c.Weight * (power + componentBonus);
    var accuracyDelta = CombatDerivedReader.Accuracy(request.Attacker.Derived, c.Element) - ...
```
`src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs:128-172`

**INFERENCE:** an element-keyed affix contributes **only through components present in the payload**.
A node that converted 40% of a hit to ice by changing a magnitude and not the payload would leave the
player's `combat.power.ice.*`, `combat.crit.rate.ice.*` and `combat.accuracy.ice.*` reading a payload
with no ice component — every one of them contributes exactly zero, forever, with no error. That is
D16's *"a conversion that changed only the number would silently create dead stats"*, confirmed at the
loop that causes it.

### 6.3 What a conversion node must write

A conversion node emits a **payload rewrite**, applied between the attacker's payload construction and
`OverlayCombatCalculator`:

| Rule | Why |
|---|---|
| Move weight from source element to target element, then build the new list through `ElementPayload.From` | The private ctor already forces this — there is no bypass to design against |
| **Drop** components whose weight reaches zero; never keep a zero-weight entry | `Validate` throws on `Weight <= 0` (`:31-32`). A 100% conversion removes the source element entirely |
| Apply all conversions on a payload in one pass and normalise **once** at the end | Two 60% conversions applied sequentially produce weights that fail the sum check. One pass, one normalise |
| Order conversions deterministically — by `(priority DESC, container_id ASC, seq ASC)`, ordinal | The actor effect list's own order (`definitions.md` §5). Anything content-derived; never `binding_id`, which is generated |
| Author the share as **integer per-mille**; derive the `double` weight at the `ElementPayload.From` boundary | `double` is legal in composition (§10.7) but the payload reaches a hashed report, so the authored number must be integer |
| A conversion is `PerMilleRatio` and must **say in a comment that it is a bounded ratio** | PS-8's exemption requires the declaration |

### 6.4 The honest finding — this is a new capability, not a wiring gap

**FACT.** None of the 16 kinds writes an element payload. `resource.delta` has an `element` param
(`AtomKindRegistry.cs:552`) but that *names* an element on a delta; it does not rewrite a payload.
There is no `Element` attach point.

**Applying CLAUDE.md's three-question ladder honestly:**

1. Does the RPG layer already have a channel/atom/runtime for conversion? **No.** The mechanism
   (weighted components) exists; the *writer* does not.
2. Is a path inert? **N/A** — there is no path.
3. Is this a genuinely new capability? **Yes.**

So the correct word here is **new capability**, not wiring gap, and the correct process is a reviewed
change to `decisions.md`'s "Atom attach points" row (`decisions.md:112`, which says in terms:
*"Growing this list is a reviewed change to this row"*). The cheapest shape is a **17th kind on the
existing `Board` or `Stat` attach point** rather than an eighth attach point — but that is a decision
for whoever specs it, not for this document.

**Consequence for the plan:** until that kind exists, the distribution engine must not allocate budget
to conversion nodes, or a slice of every tree's budget buys nothing. Stage 1 owes a flag for this.

---

## 7. Mechanism nodes — three worked examples

§3.5 of the ideal is the constraint: *"A focus build cannot be rescued with MAGNITUDE. It can only be
rescued with MECHANISM."* The named targets are `class-system-map.md:351` — *"A passive scaling damage
with damage taken, a reflect build, an anti-turtle status"*.

Each example below uses **only** kinds and triggers from §1.

### M1 — damage scaling with damage taken

```jsonc
// affix: "vengeance.t5" — one atom
{
  "kind": "stat.modify",
  "when": { "trigger": "OnDamageTaken" },
  "icd_key": "vengeance",          // one clock, shared if the tree ever splits this across atoms
  "params": {
    "channel": "atk",              // StatChannels.All — one of the 23 primary channels
    "op": "flat",                  // StatOps = { flat, increased, more }
    "amount": { "powerLadder": true, "kMicro": 6027 }
  }
}
```

**Executable today: YES, on both real runtimes.** `stat.modify` carries `AllTriggers`, which includes
`OnDamageTaken` (`AtomKindRegistry.cs:46-48`, `:497`), and its support matrix is
**Lawn = Full, Battle = Full, Sim = PlanOnly** (`:496`). Lawn: `EffectBag` → FA1 `ModifyStat` →
`InjectorEffectActionSink` → `EntityStatWriter` (`EffectBag.cs:416-428`). Battle: A18e's
`BattleStatModifierLedger` composes triggered `stat.modify` grants through the same
`PhasedComposeStrategy` the overlay uses (`AtomKindRegistry.cs:484-495`,
`src/FusionRpg.Core/Battle/BattleStatModifierLedger.cs`).

**Honest caveat, and it is a wiring question not a wall:** stacking and decay are **grant** properties
(`max_stacks`, `icd_ms`), and the automatic un-apply fires only for `OnRemoved` on a `Passive` def
(`EffectBag.cs:424-428`). A stack that decays *on a timer* rather than on grant withdrawal is a grant
shape to specify, not a kind to add.

### M2 — a reflect build

```jsonc
// affix: "thornmail.t6" — TWO atoms, drawn together (this is why the unit is an affix, not an atom)
[
  { "kind": "stat.derived",         // no trigger: permanent modifier (definitions.md 14.2)
    "params": { "channel": "combat.reflect.rate.omni",   "op": "flat", "amount": 180 } },
  { "kind": "stat.derived",
    "params": { "channel": "combat.reflect.damage.omni", "op": "flat", "amount": 220 } }
]
```

Both channels are `PerMilleRatio` (`DerivedStatChannels.cs:320-323`), so per §5's table the amounts are
**flat per-mille points**, not `P(Θ)`-scaled — and the node must say so in a comment.

**Executable: on lawn and battle, YES. In sim, NO.**

- The reader is live: `CombatDamageDispatcher.TryReflect` reads `ReflectRate`/`ReflectResistRate`,
  clamps linearly, rolls, then reads `ReflectDamage`/`ReflectResistDamage` and dispatches a real
  reversed `DamagePacket` through the Funnel (`CombatDamageDispatcher.cs:98-124`).
- `stat.derived` support is **Lawn = Full, Battle = Full, Sim = None** (`AtomKindRegistry.cs:534`).
  Lawn's executor is `AtomDerivedSubsystem`, registered on `ActorHub` at `ActorHub.cs:155` and wired
  from the injector at `src/FusionRpg.Injector/CheatState.cs:55`. Battle's is `BattleStatComposer`
  through `TraitAtomSource`.
- **Sim is `None` and that matters here more than anywhere else.** The `tools/HybridViability` sweep
  that produced the ideal's §3.5 conclusion runs against the closed form, and the ideal already says
  so (*"the closed form reads allocation only"*). So a reflect build cannot be scored by the very
  measurement that concluded mechanism is the answer. **Wiring gap, in the precise sense** — the
  quarantine of `Sim` was deliberate (D6, `AtomKindRegistry.cs:511-533`: *"SIM stays None —
  `SimEffectHost` still has no consumer"*), and it is a missing consumer, not a wall.

**Consequence:** the ideal §3.5's *"re-measure only once mechanism nodes exist in the resolver"* has a
prerequisite nobody has written down — a sim consumer for `stat.derived`, or the sweep runs against
the lawn instead.

### M3 — an anti-turtle punish

Two forms; both are buildable from §1's vocabulary, and they answer different halves of the problem.

**M3a — the stat form (defeat the defensive layers directly):**

```jsonc
// affix: "siegebreaker.t7" — three atoms, one bundle
[
  { "kind": "stat.derived", "params": { "channel": "combat.parry.break.omni",  "op": "flat", "amount": 150 } },
  { "kind": "stat.derived", "params": { "channel": "combat.block.break.omni",  "op": "flat", "amount": 150 } },
  { "kind": "stat.derived", "params": { "channel": "combat.shield.pen.omni",   "op": "flat",
                                        "amount": { "powerLadder": true, "kMicro": 8438 } } }
]
```

**Executable: YES on lawn and battle** (same `stat.derived` matrix and the same `Sim = None` caveat as
M2). The readers are live and subtractive:
`Math.Max(0, ParryRate(def) − ParryBreak(atk))` and the same for block
(`OverlayCombatCalculator.cs:183-184`); shield pen is `GameUnits` into `ShieldMath`.

**This is the mechanism the ideal §3.5 asked for, and it is the reason it works.** §3.5's finding was
that *"defensive layers compose multiplicatively"* and *"more Might does not fill an empty defensive
layer."* `break`/`pen` do not add to the attacker's own layer — they **subtract from the defender's**,
which is the only thing that reaches a multiplicative stack. `parry.break` and `block.break` are
`PerMilleRatio`; `shield.pen` is `GameUnits`, so only the third takes a `P(Θ)` magnitude.

**M3b — the status form (the map's own wording):**

```jsonc
{
  "kind": "status.apply",
  "when": { "trigger": "OnDamageDealt" },
  "predicate": { "leaf": "hpAboveMilli", "subject": "target", "value": 800 },
  "params": { "status": "<catalog id>", "duration": 4.0, "level": 3 }
}
```

**Executable: the mechanism, YES** — `status.apply` is **Lawn = Full, Battle = Full, Sim = PlanOnly**
(`AtomKindRegistry.cs:624`), and its vocabulary is the live 21-status catalog resolved fresh per
validate (`AtomKindRegistry.cs:87-91`).

**Two honest limits:**

1. **Whether the *specific* anti-turtle status exists is a content question, not a mechanism one.**
   `DESIGN-GATE.md:48`: *"`StatusCatalog` is ADR-locked code-first. 21 declared, ~13 functional."* The
   node above is only as real as its `status` id, and that must be checked against `StatusCatalog`
   before the plan budgets it.
2. **No predicate leaf reads a derived channel.** The closed leaf list is 12 —
   `SideIs, TypeIdIs, TypeIdIn, ActorIsKiller, HasStatus, HpBelowMilli, HpAboveMilli, ElementIs,
   RowIs, ColIs, IsMindControlled, HoldsStock` (`PredicateNode.cs:17-31`) — so a node **cannot** say
   *"if the target has high block."* The shipped, executable proxy is **`hpAboveMilli` on `Target`**:
   a turtle is, definitionally, the thing that stays near full HP. That is a genuinely good fit and it
   needs no new leaf.

### Summary

| Example | Lawn | Battle | Sim | Verdict |
|---|---|---|---|---|
| **M1** damage scaling with damage taken | ✅ Full | ✅ Full | plan only | **Executable today.** Stack decay shape is a grant question |
| **M2** reflect build | ✅ Full | ✅ Full | ⛔ None | **Executable today** in play; **wiring gap in sim**, which is where the balance sweep lives |
| **M3a** anti-turtle, stat form | ✅ Full | ✅ Full | ⛔ None | **Executable today**, same sim gap |
| **M3b** anti-turtle, status form | ✅ Full | ✅ Full | plan only | **Mechanism executable today**; the status *id* is a content gap |
| Conversion node (§6) | — | — | — | **New capability** — no kind writes an element payload |

---

## 8. What one baked node looks like

`data/seed/passive-tree/trees/force.might.v1.json`, one node, fully concrete. Every number here is
either produced by §3.3's formula or copied from a shipped file.

```jsonc
{
  "nodeId": "might.offense.t5.a",
  "treeId": "primary.might",
  "branch": "offense",
  "tier": 5,

  // D20: req(t) = 10 + 2.5·t·(t−1) -> 10,15,25,40,60,85,115. Both branches share one requirement.
  "tierRequirement": 60,
  "gateQuantity": "allocation.might",     // AllocationScope.Commander; base allocation only (D12)

  // Stage 2 fills these; stage 1's property vocabulary constrains them.
  "name": "Whetted Fury",
  "properties": ["offense", "element:fire", "unitClass:gameUnits", "magnitude"],

  // The affix bundle. A skill container's fixed core: prefix_rolls = suffix_rolls = 0.
  "affix": {
    "affixId": "affix.whetted-fury.t5",
    "atoms": [
      {
        "seq": 0,
        "atomId": "atom.tree-power.fire.t5",
        "kindId": "stat.derived",
        "when": null,                      // permanent modifier: no trigger (definitions.md 14.2)
        "params": {
          "channel": "combat.power.fire",
          "op": "flat",
          // The ONLY magnitude number in this file. Per-million share of P(Theta_node).
          // = treeShareMilli(1000) * treeBudgetMilli(1000) * tierWeight(5) * anchorMilli(135)
          //   / (1000 * weightTotal(112))
          "amount": { "powerLadder": true, "kMicro": 6027 }
        }
      }
    ]
  },

  // D3 track 2. Adds to the ladder INDEX, never to kMicro (see 4.2).
  "soulTrack": {
    "thetaOffsetPerLevel": 1,             // Ws — tunable, data/tuning/passive-tree.v1.json
    "costFirst": 40,                      // arithmetic cost ladder: cost(L) = first + (L-1)*step
    "costStep": 8,
    "maxLevel": null                      // PS-8: unlimited. Absolute bounds THROW (4.3), never clamp
  },

  // D14: property-keyed, O(1), printed as a runtime no-op. Never a named-pair list.
  "exclusion": {
    "predicate": "damageConverted(fire)",
    "escalation": "nullification",
    "printed": "No effect while your fire damage is converted."
  },

  "links": ["might.offense.t4.b", "might.offense.t6.a"],

  "_provenance": {
    "plan": "passive-tree-plan.v1",       // stage 1: shape, budget, tier weights, potency ceiling
    "vocabulary": "passive-tree-lang.v1", // stage 2: name, properties, channel choice
    "binder": "passive-tree-bind.v1",     // stage 3: kMicro, anchor, unit-class legality check
    "powerTuning": "power-scale.v2",      // pinValue 680, bMilli 400 — kMicro is invariant to bMilli
    "catalogRevision": 0
  }
}
```

**Units stored, stated once and plainly:**

| Field | Unit |
|---|---|
| `amount.kMicro` | **per-million of `P(Θ_node)`** — a share, never a magnitude |
| a `PerMilleRatio` channel's `amount` (M2, M3a) | **integer per-mille points**, flat, not ladder-scaled |
| a `SigmoidPoints` channel's `amount` | **resolver points**, `Θ`-linear (`k·Θ_node`), never `P(Θ)` |
| `tierRequirement` | whole allocation points |
| `soulTrack.costFirst` / `costStep` | whole souls |
| `thetaOffsetPerLevel` | whole ladder index per soul level |
| durations | integer ms — except `status.apply.duration`, which FA2 reads as **float seconds** (`definitions.md` §13/D7) |

**Nothing in this record changes per player.** The catalog is byte-identical for everyone; `Θ_actor`
and `soulLevel` are the only per-player inputs, and both arrive at compile time.

---

## 9. Open — what stage 3 hands back

### Needs a decision

1. **`treeShareMilli`.** The single biggest dial the tree layer has, and the ideal §3.3 already names
   it as undecided: *"its leverage depends on what share of total power trees carry — currently
   unknown and worth deciding deliberately."* §3.4 used `1000`; that is a placeholder chosen so the
   arithmetic runs, not a balance decision.
2. **The conversion kind (§6.4).** A genuinely new capability and a reviewed change to
   `decisions.md:112`. Until it exists, stage 1 must not allocate budget to conversion nodes.

### Named, small, and mechanical

3. **`ValueSpec.PowerLadderKMicro`** — the per-million sibling (§3.5). Without it, tier-1 nodes carry a
   17% error and D20's linear per-tier power does not survive the bake.
4. **Widen `AtomCompiler.cs:464`'s `powerLadder` result from `int` to `long`** (§4.3). CLAUDE.md rule 1;
   moves the first refusal from Θ ≈ 103,557 to Θ ≈ 214,748,300.
5. **A sim consumer for `stat.derived`** (§7, M2) — otherwise the re-measurement the ideal §3.5 schedules
   cannot see the mechanism nodes it is scheduled to measure.
6. **One new row in `ssot-power-scale.md` §10.2** for `Ws` (§4.4).

### Corrections owed to other documents — found while verifying, not assumed

7. **`DESIGN-GATE.md:40`** — 5/12/8 → **7/16/13**. It is the row that "wins over any spec", so nothing
   downstream can fix it by being right.
8. **`AtomKindRegistry.cs:6`** — class doc still says *"5 attach points, 12 kinds"* against its own
   consts of 7 and 16.
9. **`DESIGN-GATE.md:34`** — *"nine-class `UnitClass` ledger"* → thirteen.
   **`StatClass.cs:26`** — *"ten-class unit ledger"* → thirteen. `spec-magnitude-and-units.md` §3 is
   already correct.
10. **`ValueSpec.cs:24-26`** — still says *"`+10 fire power` is ten resolver points on a sigmoid
    scale"*. `spec-magnitude-and-units.md` §2 flagged this on 2026-08-22 and `definitions.md` §2
    corrected the rule on 2026-09-03; the comment in the file an author is most likely to read is
    still wrong.
11. **`AptitudeTuning.cs:282-294`** — `PositiveMilli` / `NonNegativeMilli` are **misnamed**: they read
    the raw integer and never multiply by 1000 (contrast `PositiveMilliFromDouble` at `:299-306`,
    which does). So `AptitudeGrant.SkillPointsPerThetaMilli` holds `1` for
    `"skillPointsPerTheta": 1` — whole points in a `…Milli` field. This is exactly the misreading
    `UnitClass.LoamUnits` was created to make unrepresentable
    (`spec-magnitude-and-units.md` §3's `LoamUnits` note). Cosmetic today because the field has no
    consumer; a trap the moment the passive tree becomes its first one.

---

## 10. Pre-proposal checklist

```
[x] I identified the subsystem(s) this touches — atom layer, derived stats, power ladder,
    effect pipeline, class system.
[x] I read every doc in the DESIGN-GATE §1 row(s) for those subsystems, this session:
    effect-atom/definitions.md (full), effect-atom/spec-container-schema.md,
    effect-atom-map.md (module table), power/ssot-power-scale.md (§3, §4, §10, §11),
    design/spec-magnitude-and-units.md (§1-4), design/spec-derived-stat-sheet.md (§1-3),
    architecture/passive-tree-ideal.md, architecture/tunables-ssot.md (§0-2),
    architecture/decisions.md (relevant rows), CLAUDE.md, DESIGN-GATE.md.
[x] I checked decisions.md for a lock covering this — "Atom attach points" (:112) and
    "Class system" (:103) both apply and are honoured.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments — and found four stale comments and one
    stale gate row doing so (§9).
[x] I read the surrounding section of every rule I quoted (PS-1/PS-3/PS-8, definitions §14.2,
    §10.5, §11.1).
[x] Counts verified by counting: 7 / 16 / 13 atom vocabulary, 13 UnitClass, 23 primary
    channels, 12 predicate leaves, 267 derived channels (test-asserted in four files).
[ ] I tested (not assumed) any constraint I am reporting. **NOT DONE — stated honestly.**
    No test suite was run for this document. Every runtime-support claim comes from the
    RuntimeSupportMatrix declarations and their executors read in source; nothing here
    claims a golden would or would not move.
[x] Nothing contradicts a §2 invariant. The one thing that comes close is §6's conversion
    kind, and it is named explicitly as a NEW CAPABILITY requiring a reviewed decisions.md
    change — not smuggled in as a wiring gap.
[ ] Corrections propagated to prose, Structure, Testing, Boundaries, map, and tasks.
    **NOT DONE — this is a research document and edits no other file.** The eleven
    corrections owed are listed in §9 (items 7-11) for whoever lands them.
```
