# Combat power number — the ideal

**Status:** idea phase, enriched 2026-09-12 (one-compose SSOT; dual-engine ADR **defect**). **Q1 accepted.** Specs under program `actor-hub-and-combat-power-solid-fixing` — fuse-first; not a substitute for module specs.  
**Program id:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [actor-hub-and-combat-power-solid-fixing-map.md](actor-hub-and-combat-power-solid-fixing-map.md)  
**Policy:** Hub/atom/FSM are designed well; dual compose and private combat folds are **wrong use**. Hard rules: DESIGN-GATE · AGENTS.md · CLAUDE.md · `scripts/guard-actor-hub.ps1`.

---

## Which loop this extends

From [the-loops.md](../guide/the-loops.md) / [the-game.md](../guide/the-game.md):

| Loop | Why |
|---|---|
| **Spine A — Level up and power** | Players need one honest “how strong is this actor in combat?” read that tracks combat-affecting derived, not specimen level and not Θ alone |
| **Place — Lawn** | Bound UniqueActors must feed the **same** ActorHub aptitude input as sheet/web; dual aptitude input violates sole Hot compose |
| **Place — Sanctum / ActorSheet** | Standing / combat power on the sheet must match Hub combat channel totals |

Rise of Summoner is RPG + empire building. Features live in the **RPG layer** (ActorHub, derived channels, E9 pricing) — never by rewriting PvZ Unity fields. Contests read **Θ**; magnitudes read **P(Θ)** — that ladder is not the combat power *display* number.

**One ActorHub compose / one read (SSOT).** Lawn, sheet, battle, delve, siege, and sim must contribute once and read Hub (or Hub-only after fusion). **Dual compose** (Hub vs `BattleStatComposer`) is an **ADR defect** overturned 2026-09-12 — grandfathered debt until fused, **never** a pattern to copy. Scattering equipment/aptitude apply per feature is the same defect class (SOLID / DRY).

---

## What this is

When a player asks “how strong is this creature?”, the answer is a **combat power number**: a **matrix price** over **combat-affecting** derived stats — **~196** element-typed combat channels (`CombatChannelFamilies` × omni+elements) **plus** combat-support outside that roster (e.g. `skill.cooldown.*`), loaded from **tuning coefficient tables**, composed the way E9 already prices channel-grouped grants. (Total registered derived may be ~261+; that full registry is **not** the combat roster.)

**Product rule:** every combat-affecting derived raises that number. An actor with **100000 evasion** and **100 omni attack** is still **very high power** (magnitude × coeff). Omni attack is one family among many. Combat-support (e.g. `skill.cooldown.*`) counts. Drop / magic-find / experience-bonus style stats are **non-combat** — they must **not** raise combat power (examples may be unimplemented; the class still exists).

That number is **not**:

- specimen or species **level** (Aptitudes chip today literally shows specimen level under the word “power”)
- **Θ** / `progression.power` (ladder index for contests and `P(Θ)` magnitudes)
- a glance at a single channel **`combat.power.omni`** (one attack contest family)

---

## Vocabulary — three “power” words

| Name | What it is | Must not be confused with |
|---|---|---|
| **Θ** (`progression.power`) | Ladder index from player axes ([ssot-power-scale.md](power/ssot-power-scale.md), [spec-power-index.md](power/spec-power-index.md)) | Combat threat on a menu chip |
| **`combat.power.*`** | One contest family vs `combat.defense.*` — live damage input ([spec-magnitude-and-units.md](../design/spec-magnitude-and-units.md)) | The full combat power number |
| **Combat power number** | E9 `actorPower` → sheet **Standing** (`PowerVector` five axes) → player label from O+S+C only ([definitions.md](effect-atom/definitions.md) §7, [spec-power-vector.md](effect-atom/spec-power-vector.md)) | Level, Θ, omni attack alone, or Utility/Economy |

**Display lock (D3):** Condition tab keeps the five-axis Standing vector. A string labeled **“combat power”** = **Offense + Survivability + Control** only. Utility/Economy stay on the vector for inspect/AI — they do **not** enter that label. `PowerScalar` geomean is the **item-card** read today ([`ItemPowerReads`](../../src/FusionRpg.Core/Items/Power/ItemPowerReads.cs)); the UniqueActor sheet does not geomean Standing.

---

## What already exists

### Built

| Piece | Evidence |
|---|---|
| Element-typed combat channel roster | `CombatChannelFamilies` (28) × (omni + 6 elements) → **~196** ids via `AllCombatChannelIds` / `IsCombatChannel` (`DerivedStatChannels.cs`) |
| Combat-support families beside the element roster | `skill.cooldown.*`, `skill.effectiveness.*` (actor-hub §H.3; **not** in `AllCombatChannelIds` by design, still combat-affecting) |
| Non-combat / progression channels | `progression.*` (incl. Θ); actor-hub Non-combat pairing rules |
| E9 price + ActorPowerCache | `CostFunction`, `CoefficientTable`, `ActorPowerCache.Compose(AtomRow[])` — channel-grouped price; **no** Derived-snapshot overload |
| Coeff / trigger tuning | `data/seed/power/coefficients.v1.json`; DB via `RpgStore.Power` — generic `stat.derived` row prices all such channels equally (undifferentiated $/point) |
| Sheet Standing projection | `UniqueActorHubCompose.ProjectStanding` → `ActorStandingDto`; FE Condition tab (`foldConditionSurfaceVm` standing-radar / bars) |
| UniqueActor Hub aptitude input (sheet/battle) | `UniqueActorHubCompose.Build`: `commander + UniqueCreature(instanceId)` — never CreatureType (`UniqueActorHubCompose.cs` ~43–60) |
| Aptitude → derived on sheet | `AptitudeSubsystem` via Hub; audit tests expect `aptitude.*` on `combat.power.omni` |
| Lawn Hot uses ActorHub | `EntityApply` → `CheatState.ActorHub.Resolve` — Hub is present; aptitude **input** is the gap |
| Bound ptr→instanceId binding | `MatchUniqueBindingsFacet.TryGetByPtr` exists; aptitude Hot never joins it today |
| Creature ownership lock | `decisions.md` Creature progression source (2026-09-08) — unique never empire species fallback |
| ActorHub sole Hot compose | `decisions.md` ActorHub sole Hot compose gate (2026-09-07 + 2026-09-12 amend) |
| unique-lawn-wire **spec** (intent) | [aptitude-sheet/spec-unique-lawn-wire.md](aptitude-sheet/spec-unique-lawn-wire.md) — Bound = commander+UniqueCreature |
| Item `PowerScalar` | Production via `ItemPowerReads.CardPower` (E10 “no production caller” comment is stale) |

### Wiring gap

| Gap | Evidence | Why not a wall |
|---|---|---|
| **HF-lawn** — Bound lawn aptitude input omits UniqueCreature | Lawn **does** use ActorHub; `CheatState.SpeciesAllocation` = commander + species by `typeId` only (`CheatState.cs` ~157–162). Bound bindings unused for aptitude | Spec `unique-lawn-wire` + Server Hub path exist. **Severity:** wiring that **violates** sole-Hot + ownership — not “Hub missing on lawn” / not UniqueActor FSM absent |
| **HF-standing** — Standing ignores aptitude (and other non-atom Hub combat writers) | `ProjectStanding` = equip + tree atoms only (~237–246); Hub Derived includes aptitude | See **Standing fold lock (D2)** below — synthetic `AtomRow`s + membership filter, then same `Compose` |
| **HF-chip** — Aptitudes scope chip **is** specimen level labeled “power” | Unique GET has **no `theta`**; FE `theta ?? specimenLevel` → ``power ${n}`` (`AptitudeEndpoints`, `AptitudesTab`, `foldAptitudesSurfaceVm`) | Copy / fold honesty; do not put combat power on this chip until HF-standing |
| **HF-copy** — surfaces that treat omni attack as “the power” | Design drift risk | Player “combat power” = O+S+C Standing compose |
| Undifferentiated `stat.derived` coeff + O\|S\|C trisect | One kind-level coeff; dodge thirds across Offense/Survivability/Control | Tuning enrichment (per-family weights / category masks) — not a new ladder; product “high dodge dominates” is magnitude×coeff, axis identity blurred today |
| Standing already exposes Utility/Economy from equip kinds | Compose does not filter combat for axes | Combat-power **label** excludes U/E (D3); vector keeps all five |

### Real gap

| Gap | Notes |
|---|---|
| Loot / magic-find / set-drop derived families | Product class exists; when they land they stay **out** of combat power pricing |
| Exact combat-support / status-potency membership beyond cooldown/effectiveness | Principle locked; list owed to `/spec` (**Q5**) |
| Per-family coeff / category-mask tuning so dodge reads as Survivability-weighted | Tuning stream after HF-standing |

---

## Prior art

- **Pokémon GO CP** — geomean-shaped display; documented as misleading across species. E10 already copies the shape and refuses the claim of precision ([spec-power-reads.md](effect-atom/spec-power-reads.md)). Sheet Standing does **not** use that geomean today.
- **Do not** invent a second level→power curve. Combat power prices **grants / combat-affecting derived**, not Θ. Magnitudes still scale once via `P(Θ)` where the ladder applies.
- E9 **“base stats contribute nothing”** stands: EntityBaseline / level curve are not Standing inputs. Aptitude **grants into derived** are not “base” — they belong in combat power once HF-standing lands.

---

## The shape

### One compose, one read, all places (SSOT — binding)

**End state:** every place that needs actor combat derived / AppliedCombat goes through **`ActorHub` only**. Contribute via `IActorStatSubsystem` / registered atom readers. Read the Hub snapshot — never a per-module fold.

**Defect (overturned ADR):** “One equipment corpus, two compose engines (Hub vs `BattleStatComposer`)” was written as intentional. It is **not**. It is a SOLID violation (SRP / OCP / DIP / DRY) that forces “implement equipment N times” and “100 read paths.” Delve / siege / web / expedition that run `BattleEngine` inherit `BattleStatComposer` — they inherit the **defect**. **Must fuse** under a mandatory shared-contribution `/spec`. Until then the battle path is **grandfathered debt**; citing it as permission for a **new** parallel composer = DESIGN-GATE / CI fail (`guard-actor-hub.ps1`).

| Principle | Violation today |
|---|---|
| SRP | Two composers own “actor combat numbers” |
| OCP | Modes extend by forking compose instead of Hub contributors |
| DIP | Battle depends on a parallel concrete composer |
| DRY | Equip/aptitude/tree re-packaged per seam |

| Place | Aptitude | Equip | Tree | Classification |
|---|---|---|---|---|
| Sheet Hub Derived | Built UniqueCreature | Built shared atoms | Built Server | Compliant |
| Sheet Standing | Wiring → `standing-compose` (W2) | Built | Built | Specced |
| Lawn Hot Hub | Wiring → `lawn-aptitude-parity` (W3) | Built grants→Hub | Wiring → `lawn-tree-hydrate` (W3) | Specced |
| Bound loadout | — | Wrong use → `bound-loadout-hub` (W3) | — | Specced |
| Battle / web / delve / siege board | Debt → `battle-hub-fuse` (W1) | Ops → `battle-ops-parity` (W1) | Tree → fuse/ops | Specced |
| Star/Loyalty/Zomboss ChannelMods | — | — | — | → `channelmods-hub` (W1) |
| Dual Cold equip stub vs rolled | — | → `cold-equip-one` (W1) | — | Specced |
| World PlaceholderBattleResolver | Orthogonal Hp×Level | — | — | W5 **delete** + track `world-actor-combat` (out of scope) |
| Sim ActorDerivedLookup | — | Named Partial | — | → `sim-hub-parity` (W4) |

**Rules:**

1. Contribute once — no private combat-number fold per feature.
2. Read once — consume Hub (post-fusion: battle too).
3. Entity class changes **allocation identity** only (Bound UniqueCreature vs empire species) — never which composer exists.
4. Temporary packaging during fusion migration may exist; **end-state is Hub only**.

### One Hub aptitude identity (all modes)

| Entity | Aptitude input into Hub |
|---|---|
| Bound UniqueActor | `commander + UniqueCreature(instanceId)` |
| Empire general | `commander + CreatureType(species)` |
| Commander aura host | Commander only |

Lawn Hot **already** resolves through ActorHub. If Bound lawn feeds `commander + species` while the sheet feeds `commander + UniqueCreature`, **consumers share Hub; aptitude input does not** — wiring that violates sole Hot + ownership (HF-lawn). Join Bound `instanceId` when `TryGetByPtr` hits.

### Combat-affecting membership (D2 filter)

**Do not** use `IsCombatChannel` alone — it would **drop** `skill.cooldown.*` and break the product rule.

| Include | Exclude |
|---|---|
| Element combat families (~196 via `CombatChannelFamilies`) | `progression.*` (esp. Θ / `progression.power`) |
| Combat-support: `skill.cooldown.*`, `skill.effectiveness.*` | Future loot / MF / XP-bonus class |
| Status potency channels that combat uses (exact list → `/spec`, Q5) | EntityBaseline / Hub “base stats” (E9 base contributes nothing) |

**Naive “price the Hub Derived snapshot” is rejected** — the snapshot includes `progression.power` (Θ) and would fold the ladder into Standing.

### Standing fold lock (D2 — closes former Q4)

**Chosen:** Build synthetic `stat.derived` `AtomRow`s for aptitude (and any other Hub combat writers not already atoms) — same bridge as tree in `ProjectStanding` — apply the membership filter above, then `ActorPowerCache.Compose`. No second composer. No private FE fold.

### Combat-facing display (D3 — closes former Q2 / Q3)

- Five-axis Standing remains the vector SSOT (Condition tab).
- Label **“combat power”** = Offense + Survivability + Control only.
- Aptitudes scope chip (**HF-chip**): **`Lv {specimenLevel}`** first; optional ladder copy as **`Θ {n}`** with that letter — never “power” for level. Do **not** put combat power on that chip until HF-standing makes Standing honest.

### Coeff honesty (D4)

Generic `stat.derived` means channels are **priced**, not missing — but **undifferentiated** $/point, and kind categories trisect O|S|C so dodge does not land mainly on Survivability. Product rule (high dodge → high combat power) still holds via magnitude × coeff; axis identity and per-family weights are **Wave 4** [`standing-coeff-tuning`](actor-hub-and-combat-power-solid-fixing/spec-standing-coeff-tuning.md).

---

## Tunables

| Number | Owner |
|---|---|
| Channel / kind coefficients, reference scales | Existing power coeff tables (`coefficients.v1` / `power_coefficient`) |
| Trigger frequency / conditionality | Existing `power_trigger_frequency` |
| Ladder Θ / `P(Θ)` | `power-scale.v{n}.json` — **unchanged**; not the combat power matrix dial |
| Aptitude edge kMilli into derived | `aptitudes.v{n}.json` — feeds channels that then raise combat power |

No new `f(level)`. No combat-power literal in Policy/Catalog code.

---

## What this deliberately does not decide

- Exact numeric coeff table rows (owned by Wave 4 `standing-coeff-tuning` implementation)
- Materialize / preset / Mode A–C allocate UX (owned by `aptitude-sheet`)
- Injector fetch HTTP shape beyond S4 already locked in `unique-lawn-wire`

Q5 membership and Q7 Cold path are **locked on the program map**. Place-matrix leftovers → Waves 4–5 module ids on the map.

---

## Open questions (owner)

| # | Question | Status |
|---|---|---|
| Q1 | Accept this ideal (incl. one-compose SSOT + ADR overturn) before HF-* / fusion `/spec`? | **Closed — accepted 2026-09-12.** Program: `actor-hub-and-combat-power-solid-fixing` |
| Q2 | Standing vs PowerScalar for combat-facing UI? | **Closed (D3)** |
| Q3 | Include Utility/Economy in “combat power”? | **Closed (D3)** — **no** |
| Q4 | Snapshot vs synthetic atoms for Standing? | **Closed (D2)** |
| Q5 | Exact combat-support / status-potency membership beyond cooldown/effectiveness? | **Closed — full membership** (map lock: ~196 + cooldown/effectiveness + status power/resist Apply reads; exclude progression / loot / resource pools). Breaking OK |
| Q6 | Fuse `BattleStatComposer` into ActorHub? | **Closed — mandatory.** Wave 1 fuse-first under the program map |
| Q7 | Sole Cold equip path — stub UniqueEquipment vs rolled projection? | **Closed — stub is debt;** rolled/atom path; serious gaps in `cold-equip-one` |

---

## Audit adversarial findings (2026-09-12)

Do not rediscover these in a later session:

1. Standing ≠ Hub combat writers today — aptitude raises Derived; Standing is equip+tree only.
2. Snapshot vs atoms is a hard API fork — `Compose(AtomRow[])` only; “widen” means synthetics or extend E9.
3. Naive snapshot pricing imports Θ via `RpgProgressionSubsystem`.
4. `IsCombatChannel` alone fights combat-support (drops cooldown).
5. Coeff sparsity is undifferentiated pricing + O|S|C trisect — not “unpriced dodge.”
6. Combat roster ≈ **196**, not ~260+ (260+ conflates total derived registry).
7. Chip fiction is stronger than “optional Θ” — unique GET has **no** Θ; chip **is** level labeled power.
8. HF-lawn: Hub present on lawn; defect is aptitude input / unused Bound join.
9. **Dual compose Hub vs BattleStatComposer is an ADR defect**, not intentional SSOT — SOLID/DRY; delve/siege inherit it.
10. Star/Loyalty/Zomboss ChannelMods and Bound loadout absolutes are **Wrong use** of good Hub/atom/FSM design.
11. Battle tree slot unused; lawn tree hydrate missing; battle equip ops coerced flat (Partial lie).
12. `guard-actor-hub.ps1` expanded to block **new** parallel composers; battle path is grandfathered debt only.

---

## Hotfix / defect register (prose only — later code streams)

| Id | Bucket | Intent |
|---|---|---|
| **HF-chip** | Wiring | Scope chip: `Lv`; optional `Θ`; never bare “power” for level |
| **HF-standing** | Wiring + filter | Synthetics + membership filter → `ActorPowerCache.Compose` |
| **HF-lawn** | Wiring (SSOT) | Bound → UniqueCreature via unique GET + `TryGetByPtr` |
| **HF-copy** | Docs | “combat power” = O+S+C Standing |
| **HF-bound-loadout** | Wrong use | Bound combat via Hub/atoms — not Writer-absolute beside Hub |
| **HF-battle-tree** | Wiring / debt | Wire `Battle.TreeAtomSource` into composer or delete dead slot (prefer fuse into Hub) |
| **HF-battle-ops** | Debt Partial | Battle equip ops must match Hub or stay explicitly Partial until fusion |
| **HF-channelmods-writers** | Wrong use | Star/Loyalty/etc. → Hub/atoms, not private omni ChannelMods |
| **HF-cold-equip-one** | Wrong use | One Cold equip materialize (Q7) |
| **HF-lawn-tree** | Wiring | Injector PassiveTree hydrate into Hub |
| **FUSE-battle-hub** | ADR debt | Mandatory `/spec` then code: retire `BattleStatComposer`; battle/delve/siege read Hub |

Program map + **all** module specs (Waves 1–5): [actor-hub-and-combat-power-solid-fixing-map.md](actor-hub-and-combat-power-solid-fixing-map.md). **Fuse first** — do not extend Standing/chip on the dual-compose seam.

| Gap (beyond HF register) | Module |
|---|---|
| Sim Named Partial | `sim-hub-parity` (W4) |
| D4 coeff / axis | `standing-coeff-tuning` (W4) |
| Unique GET Θ | `unique-theta-wire` (W4) |
| Stale dual-compose docs | `stale-compose-docs` (W4) |
| Prove path | `prove-hub-combat` (W4) |
| World PlaceholderBattleResolver | `placeholder-battle-hub` (W5 delete + track `world-actor-combat`) |

---

## Handoff

1. Ideal **accepted** (Q1). Program id: **`actor-hub-and-combat-power-solid-fixing`**.
2. Module specs W1–5 written under scope clear: world combat **out of scope**. Fuse-first; Wave 5 stub delete + track `world-actor-combat`. Stub law: [actor-hub-and-combat-power-solid-fixing-ideal.md](actor-hub-and-combat-power-solid-fixing-ideal.md) (**B1–B4 locked**).
3. SOLID non-negotiable: PO/ADR cannot bless SOLID forks; fuse before honesty features that would deepen debt.
4. Prove path owned by `prove-hub-combat`.
5. Next: `/plan` → `tasks/actor-hub-and-combat-power-solid-fixing-plan.md` / `-todo.md`.

**Build authorized only via the program map + module specs — not from this ideal alone.**
