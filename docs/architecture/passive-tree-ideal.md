# Passive skill trees — the ideal

**Status:** idea phase, 2026-09-04. **Not a spec. No build authorized.** This is the "later discuss"
conversation that [class-system-map.md](class-system-map.md) §5 reserved by name: *"there are many sub
features for class system, includes passive skills, will be added later"* (owner, 2026-08-26).

**Owner framing, 2026-09-04:** *"we will make have many build tree"* · *"every skill tree will cost and
award same and we decide it by math functions"* · *"there are no skill tree that so op"* · *"spend all in
one is risk and reward — become stronger but become weaker too (lack of defense)"*.

> Quotes are lightly normalized for two recurring input-method artifacts only (`althemetic` → arithmetic,
> `desterministic` → deterministic). No wording was otherwise changed.

---

## 1. What this program is for

The class system ships a balanced **allocation** layer and stops there. Its own acceptance record says
why that is not enough: the dominance matrix is **soft red — `Bulwark` beats all 11 corners** — and the
named fix was always a later layer, *"a passive scaling damage with damage taken, a reflect build, an
anti-turtle punish"* (class-system-map §4b). This is that layer.

It is also where **identity** lives. Under free build there is no class name to carry meaning, and an
aptitude is deliberately *"a MECHANISM, never a FLAVOUR"* — **168 of 259 channels (65%) are reserved for
the skill/item layer** (class-system-ideal §4.1). Trees are what spend that reservation.

---

## 2. Decisions locked by the owner (2026-09-04)

| # | Decision | Consequence |
|---|---|---|
| D1 | **Free build stays.** No player class; classes remain Zomboss patterns | Confirms the 2026-08-25 correction; trees add identity without adding a class container |
| D2 | **All four acquisition sources**: skill points · aptitude thresholds · items/affixes · demon aspect | The `skillPointsPerTheta: 1` grant, minted since 2026-08-26 with **zero consumers**, finally has a spender |
| D3 | **Every skill has two unlock tracks**: skill points unlock *new bonuses* (discrete); souls scale *bonus power* (unlimited, arithmetic cost) | Matches the shipped two-ladder economy exactly — see §4 |
| D4 | **Concentration is rewarded by a bounded Herfindahl multiplier** | `F = 1 + (Fmax−1)·H`, `H = Σ(shareᵢ)²` — see §3 |
| D5 | **`Fmax = 1.5`, tunable** | Pure build is +50% over full spread, +22% over a 2-way hybrid. Under the owner's own 2× red line |
| D6 | **The multiplier applies to all trees equally** | Offensive and defensive commitment are equally valid; symmetric and explainable |
| D7 | **Hybrids stay Neutral, not Penalized** | A 2–3 way build sits 18–24% behind pure — behind, but alive. Spreading across everything is deliberately weak |
| D8 | **`H` reads spent points + souls** | Both currencies are commitment. Requires the two-index blend of §3.2 to stay sound |
| D9 | **Tree roster**: 12 primary + all elemental + all status + each demon family (+ demon species, deferred) | `n ≈ 40–60`, which *simplifies* the math — see §3.1 |
| D10 | **Same shape everywhere**: every tree is 2 branches (offensive/defensive) × tiers | One generator archetype, one set of math functions |
| D11 | **Item-granted skills respect the tier gate** — they may only land in tiers the actor's own allocation already opened | Items save skill points, never progression. Keeps "the tier gate reads base allocation" true with no exception |
| D12 | **Tier gates read base allocation, never item bonuses** | Already true by construction — see §5 |
| D13 | **Generation is deterministic-first**: math decides tree shape, power ladder, unlock requirements and skill links; **only then** does an LLM fill vocabulary, categories, atom pools and bonuses | Balance is a property of the plan, not of the generated content |

**Deferred by the owner to its own round:** demon **species** trees — *"consider as reward; building it
takes advantage but will block some builds… demon species tree will define how to block."* A tree that
*costs* options is a different mechanic and is not specified here.

---

## 3. The concentration function

### 3.1 The function

```text
H = Σ (shareᵢ)²                     Herfindahl index over invested trees
F = 1 + (Fmax − 1) · H              the focus multiplier on tree-derived power
Fmax = 1.5 (tunable)
```

Per-tree power stays **linear in investment**; `F` is a pure *shape* function of how commitment is
spread, never of how much was spent.

**Why no `1/n` normalization.** The textbook form normalizes by tree count, `H* = (H−1/n)/(1−1/n)`. With
D9's roster (`n ≈ 50`) that term is a rounding error — and dropping it removes every `n` dependence, so
**the roster can grow forever without re-scaling anybody's existing build.** A tree with zero investment
contributes zero to `H`. This was the one real objection to a wide roster, and it disappears.

| Trees invested (even) | H | F | vs pure |
|---|---|---|---|
| 1 | 1.000 | **1.500** | — |
| 2 | 0.500 | 1.250 | −18% |
| 3 | 0.333 | 1.167 | −24% |
| 4 | 0.250 | 1.125 | −27% |
| 12 | 0.083 | 1.042 | −31% |

Uneven splits fall out naturally: 70/30 → `F = 1.29`; 50/25/25 → `F = 1.19`.

**Why concentration needs convexity.** Concave ("diminishing returns per tree") curves mathematically
*reward spreading* — that is what concavity means. Rewarding focus requires convexity somewhere, and a
bounded multiplier is the safest place to put it: `F ≤ Fmax` is provable at any resource level, so a
10× build is arithmetically impossible rather than merely unlikely.

**The risk side needs no math.** All-in on Might means zero Fortitude and zero Bulwark. The gaps are the
punishment; `F` is only the compensation.

### 3.2 Blending the two currencies (D8)

Points and souls are different units, and **souls are unlimited while points are not** — summed directly,
souls would eventually swamp the share vector and point allocation would stop affecting focus. Compute
one index per currency and blend:

```text
H = w · H_points + (1 − w) · H_souls          w tunable
```

Units never mix, souls cannot dominate, and soul commitment still counts. The multiplier does then move
as souls are spent — acceptable, and arguably good feedback, **because `F` is bounded**: the
spend → stronger → farm → spend loop terminates at `Fmax` instead of running away.

### 3.3 What `Fmax` must be solved against, not guessed

A uniform shape multiplier gives **every pure corner the same 1.5×**, so corner-vs-corner ordering — what
the dominance matrix actually tests — is largely preserved. `Fmax`'s real effect is **corner vs hybrid**.

> ⚠️ **`balance-guard` tests corners only and would not see this.** A hybrid row is owed, or D7's Neutral
> property is asserted and never measured.

The closed form solves a balanced cycle in **2.3 seconds**, so `Fmax` should be *swept* — the largest
value at which every corner stays beatable and hybrids stay viable — and recorded as a measured constant
with a date, not a design opinion.

---

## 4. Structure

**Two unlock tracks per skill** (D3):

| Track | Currency | Shape | Buys |
|---|---|---|---|
| Unlock | Skill points | Discrete, finite (`skillPointsPerTheta`) | A **new bonus** |
| Deepen | Souls | **Unlimited**, arithmetic cost | **Bonus power scale** |

This is the shipped ladder pairing, and it earns a proven property. `ssot-power-scale.md` §10.5:

```text
cumulative cost   Σ(first + (k−1)·step) ≈ (step/2)·L²      quadratic
power at index    C + A·Θ + B·Θ(Θ−1)/2  ≈ (B/2)·Θ²         quadratic
                  ⇒ power ∝ total investment               LINEAR in effort
```

An hour of play buys the same absolute power at hour 5 and hour 500. Souls are therefore **uncapped by
design** (PS-8, "endless grind is the SSOT"), and the arithmetic cost is a **cost ladder** — explicitly
exempt from the one-ladder rule (§10 row 6). The *bonus* it buys is not exempt and must read `P(Θ)`.

**Tiers** (D10): every tree is 2 branches × tiers. A tier opens on (a) the tier below being unlocked, and
(b) the actor's own base allocation in that tree's gate quantity. **Both branches share one tier
requirement** — which is the pure-build discount: one investment opens offence *and* defence.

**Cross-unlock within a major category:** skill points spent in another tree of the same posture can
satisfy a tier requirement. This is a *second* concentration reward, on the cost side. It compounds with
`F`, and **both must sit inside the same closed form** or the combined effect goes unmeasured.

---

## 5. What this lands on that already exists

Nothing here needs a new vocabulary; four systems already carry the load.

| Need | Already shipped |
|---|---|
| 3 major categories × 4 stats | **Postures** FORCE / FINESSE / BASTION, twelve aptitudes (`spec-primary-stats.md` §2) |
| The share vector `H` reads | `share = points in aptitude / points across all aptitudes` (`spec-aptitude-tuning.md` §2.1) |
| "Tier gate ignores item bonuses" | **True by construction.** An aptitude is a SOURCE, not a channel (§3.1): items cannot feed aptitude points, because the share denominator is the actor's own total — `+5 Might` on an item *"would be a nerf to eleven other stats"* |
| Per-category budgets and gate quantities | `AllocationScope { Commander, DemonType, Aspect, UniqueDemon }` with a shipped per-scope rate table and **no caps** (`PointBudget.cs`) |
| The two-ladder grind property | `ssot-power-scale.md` §10.5 |
| A spender for skill points | `grant.skillPointsPerTheta: 1`, parsed, zero consumers |

**The scope alignment is close to one-to-one** and is the most promising answer to "what gates a
non-primary tree": Commander (`Θ_player`) → primary trees · Aspect (`element_mastery`) → elemental trees ·
DemonType (almanac XP) → demon family trees · UniqueDemon (specimen level) → demon species tree. Three of
four scopes ship today; Aspect *"lights up the moment a caller has a real value to pass"*.

⚠️ **Status trees have no scope and no gate quantity yet** — the one category this mapping does not cover.

---

## 6. Generation (D13)

Order is the balance mechanism, not a preference:

1. **Deterministic plan** — math functions decide tree shape, tier ladder, unlock requirements, skill
   links, and the power each node may carry.
2. **Distribution engine** — allocates that budget across trees so every tree costs and awards the same.
3. **LLM pipelines** — fill vocabulary, category, atom effect pool and bonus *within* the plan's budget.

This is the repo's binding **seed → concrete → per-player** principle applied to trees. The
`seedsmith-design` skill must be loaded before this pipeline is specced — it carries the generation
principles and the failure modes this repo has already paid for.

---

## 7. Open — for the next rounds

1. **Demon species trees** — the reward tree that *blocks* builds (owner-deferred).
2. **Status trees' gate quantity** — the one category with no `AllocationScope`.
3. **`w`** — the points/souls blend weight in §3.2.
4. **`Fmax` sweep** — solve, don't guess; needs a hybrid row in `balance-guard` first.
5. **Sparse soul state** — per-skill soul levels across ~50 trees must store only non-zero levels, or an
   actor's row grows with the content catalog rather than with what they did.
6. **Tier threshold shape** — the function mapping tier index → required allocation.

## 8. Inherited constraints (non-negotiable)

- **One ladder.** Tree bonus power reads `P(Θ)`; a new power-shaped scale needs a reviewed row in
  `ssot-power-scale.md` §10 — *"a power-shaped number that is not in this table does not have permission
  to exist."*
- **No caps on magnitudes.** Souls are unlimited by design; absolute bounds throw, never clamp.
- **`long` for every magnitude**, widen before multiplying, overflow throws.
- **Balance numbers are config**, never `const` — `Fmax`, `w`, tier thresholds all live in `data/tuning/`.
- **Twelve is a measured outcome, not a decision** — the generator reads the aptitude roster, never
  hardcodes 12.
