# Capability map: `effect-pipeline`

**Status:** proposed 2026-09-01, from [effect-pipeline-ideal.md](effect-pipeline-ideal.md) — nine
questions closed, seven attacks answered. **Ten modules** (`mods-absorption` and `patron-absorption` added 2026-09-01 per Q11 and Q13). **Awaiting approval before any
module spec is written.**

> **The program in one sentence.** Build the producer that turns a committed seed into a concrete,
> per-player effect list — and in doing so, switch on four modules that are built, tested, and currently
> unreachable.

---

## 1. Why this program exists — in the effect-atom program's own words

`effect-atom-map.md:213` lists what that program still lacks:

> **A producer of instances/bindings** — anything that actually calls `Instantiator`, `SaveInstance`,
> or `Bind` with a real owner … Until then the runtime this program built (E6/E7/E15/E19) is
> **inert**: `ResolveBindings` returns empty for every owner, so `AtomPushService` compiles nothing and
> `AtomRunner` never receives an entry — **proven correct end to end by tests, unreachable end to end
> in production.**

**This is that producer.** Verified by grep: `Instantiator.TryInstantiate`, `RpgStore.SaveInstance` and
`ActionSeeder.Generate` all have **zero production callers**. Four built modules, one missing call.

**⚠️ But the game is not effect-less today — there are four paths, and only one of them is the atom
layer.** The **atom** layer is inert; the game is not.

| # | Path | Disposition |
|---|---|---|
| 1 | `Instantiator` → `effect_binding` | the new one — built, inert |
| 2 | `rpg_unique_stat_mods.mods_json`, from equipped slots | **absorbed** — module 5 |
| 3 | `PatronSecondaryPlugin` → `GrantId = "patron:aura"` | **absorbed** — module 6 |
| 4 | `AuraContentCatalog` → grant | **deferred by its owning program**, with evidence — `AuraContentCatalog.cs:10-16` calls it *"a stated scope limit, not an oversight"* |

**Stating a disposition per path is the point.** Path 2 went unnoticed until a sweep found it; paths 3
and 4 were found only by looking for more. Until every path is dispositioned, the invariant *an actor
never receives the same source through two paths* is the thing holding the design together.

**That is why the payoff sits at module 4 of 9, not at the end.** The runtime stops being inert as soon as one
fixture container is rolled and bound — before any content pipeline exists.

---

## 2. The four-layer model this program implements

Owner, 2026-09-01: the seed must not say `+10 hp`; it must say *"+X of a derived stat from this pool."*

| Layer | Decides | Status |
|---|---|---|
| **L1** container shape | how many effects, chance each appears | **BUILT** — `pool_rolls`, `weight`, `group` |
| **L2** the channel pool | *which* derived stats | **⛔ missing — the core of this program** |
| **L3** value range | the min/max a magnitude rolls into | **BUILT** — the value spec |
| **L4** resolve | pick atoms, pick stats, freeze numbers | **BUILT but inert** — `Draw` + `TryInstantiate` |

L2's absence is not cosmetic. Without it, `+15% to all resistances` must become six atoms in six groups,
**consuming six pool rolls — the entire budget of a rung-100 item — for one affix line.**

---

## 3. The modules

| # | Module id | Responsibility | Model calls | Depends on |
|---|---|---|---|---|
| 1 | `affix-schema` | The **affix** entity (a named bundle of atom refs) and the **slot** declaration. Splits `pool_rolls` into `prefix_rolls` / `suffix_rolls`. Amends `definitions.md` and `spec-container-schema.md` | — | — |
| 2 | `resolution-order` | The resolver: **slots → affixes → atoms → tiers → values**, with a **named RNG stream per layer** (following shipped `SeededRng.DeriveStream(seed, "system:purpose")`). Also owns **variant shifts** — a variant moves the tier window or a roll count, and authors nothing (Q12) | — | 1 |
| 3 | `affix-library` | Rule-generates the single-family affixes from the 28 authored atom families — the same rule that already turns 28 families into ~980 atom rows. **Zero model calls** | — | 1 |
| 4 | ⭐ `instance-producer` | **The missing call.** Rolls a container and writes an instance + binding for a real owner. **Un-inerts E6/E7/E15/E19** | — | 2, 3 |
| 5 | ⚠️ `mods-absorption` | **A migration, sequenced after the proof.** Move equipped-slot effects from `rpg_unique_stat_mods.mods_json` onto `effect_binding`, as E6 always planned. Live, save-affecting unique-actor data (Q11) | — | 4 |
| 6 | ⚠️ `patron-absorption` | `PatronSecondaryPlugin` becomes a `patron.*` container. **`data/seed/containers/patron.json` already exists** — an empty `patron.aura` carrying `marker: fx.patron_aura`, the exact EffectId the plugin emits — so this fills a staked container rather than creating one. A kind already legal, on the same Secondary layer `AtomRunner` occupies. **Byte-identical output must be proven across the full (rarity × star × level × Θ) grid**, or the patron program's SIM results are invalidated (Q13) | — | 4 |
| 7 | `world-seed` | Per-player world seed: created once, shown in the UI, composed as `hash(worldSeed, stream, targetId)` | — | 2 |
| 8 | `eligibility-tags` | Tag-based affix eligibility plus a per-container allow/deny override — what PoE does | — | 1, 3 |
| 9 | `affix-authoring` | The seedsmith pipeline for **named, multi-atom, slotted** affixes — *"Master of Fire and Ice"*. Identity is a judgement; magnitude never is | **yes** | 1, 6 |
| 10 | `dev-reforge` | `POST /api/debug/reforge-world` — re-derive a roster from the current catalog against the same world seed. Debug surface only | — | 4, 6 |

### Dependency graph

```text
                          ┌──► mods-absorption      (independent migration)
                          ├──► patron-absorption    (independent migration)
affix-schema ──┬──► resolution-order ──► instance-producer ──┐
               │              │                    ▲          └──► dev-reforge
               │              └──► world-seed ─────┴─────────────────┘
               ├──► affix-library ───────────────────┘
               │          │
               └──► eligibility-tags ──► affix-authoring
```

No cycles.

### Build order

```text
affix-schema → resolution-order → affix-library → instance-producer
  → mods-absorption → patron-absorption → world-seed → eligibility-tags
  → affix-authoring → dev-reforge
```

**Rationale.** Modules 1-3 make no model calls and touch no player data. Module 4 is the payoff and
comes as early as it can: with a schema, a resolver and a rule-generated library, **one fixture
container proves the whole chain end to end** — the thing four built modules have never had.

**The two absorptions (5, 6) sit immediately after the proof and never inside it.** The producer's job is
to work where there is no shipped data to break. **Two risks in one change is how a proof becomes a
post-mortem.** They are separate modules because they are different problems: 5 migrates **stored save
data**; 6 relocates a **hot-path plugin whose output is under an open LIVE gate**. Different data,
different risk, different proof.

**`affix-authoring` (8) is late** because it is the only expensive stage, and by then everything it feeds
is proven.

---

## 4. How this interleaves with `demon-seed`

**Neither program can finish alone**, and pretending otherwise is how one of them stalls at its last task.

| | Needs from the other |
|---|---|
| `demon-seed` module 15 `species-effects` | this program's **affix library, slot mechanism, container schema** — modules 1, 3, 8 |
| `demon-seed` module 16 `player-materialise` | this program's **resolver and producer** — modules 2, 4, 7 |
| this program's module 9 `affix-authoring` | nothing from demon-seed — it authors against the atom library |

**The join is: `effect-pipeline` 1-4 + 7-8 → `demon-seed` 15 → `demon-seed` 16.** The two absorptions
(5, 6) are **not** on that path — they are independent migrations that can run in parallel. Everything before that in
`demon-seed` (anchors, stats, import, catalog) runs independently and does not wait.

---

## 5. What this program deliberately does not do

| Excluded | Why |
|---|---|
| feature **content** — which affixes a species or an item carries | §6.1: this program owns the SDK and the schema; each feature owns its content pipeline |
| the passive skill graph, hybrid element typing | owner-named unbuilt programs this eventually meets |
| item and action container authoring | their own programs, using module 9's pipeline shape |
| any change to the atom catalog | `atom_id` derivation and the `(family_id, tier, variant)` key stay exactly as they are |

---

## 6. Amendments owed before building

Every row is a change to a document that **wins over any spec**.

| Document | Change | Owed by |
|---|---|---|
| `effect-atom/spec-container-schema.md` | *"species passives use the core alone"* — **superseded**; they roll | module 1 |
| `effect-atom/spec-container-schema.md` | `pool_rolls` → `prefix_rolls` + `suffix_rolls`; one-per-group applies within each class | module 1 |
| `effect-atom/definitions.md` | the slot declaration; the pool's unit becomes an **affix bundle** | module 1 |
| `effect-atom/definitions.md` | the resolution order and per-layer RNG streams, stated normatively | module 2 |
| `item/ssot-rarity.md` | rarity bands **per affix class**, not one count | module 1 |
| `item/seed-contract.md` §2.1 | `affixClass` derivation extended to bundles: **a mixed bundle consumes one of each budget** | module 1 |
| `effect-atom-map.md` E6 | its *"absorbs today's `mods_json` grant blobs"* promise stops being aspirational and names module 5 | module 5 |
| `demons/spec-patron-demon.md` | the aura becomes a `patron.*` container; the SIM equality proof is its acceptance gate | module 6 |
| **`action/spec-action-seeding.md` (A13)** | it quotes *"Rarity selects the `pool_rolls` count and the tier window… No third mechanism"* as its own foundation (`:42-43`). Splitting `pool_rolls` per affix class changes that foundation, and A13 is an **approved** spec — **found 2026-09-01; it was missing from this table** | module 1 |
| `AGENTS.md` no-caps rule | the variant tier shift **saturates at t5** — a *structural* limit (no t6 row exists), exempt but **required to say so in a comment** | module 2 |

### How big is the `pool_rolls` split, measured

**Not a fear — a number.** `data/seed/` holds **149 committed seed files**, of which **133 are item
seeds**. Files that actually declare `poolRolls`: **eight.**

So splitting `pool_rolls` into `prefix_rolls` / `suffix_rolls` migrates **8 files**, not 133 and not 149.
That is small enough to do by hand and review in one sitting — and it is small **today**. Every container
authored before module 1 lands adds to it.

**Timing is the argument for all seven now: almost no containers exist yet.** Today each is a schema edit
plus eight files; after content, each is a migration of everything ever authored.

---

## 7. Two defects the adversarial review found, both closing in module 1

| | Defect | Fix |
|---|---|---|
| **A1** | an affix bundle can carry both classes, so `affixClass` has no derivation | a mixed bundle **consumes one prefix roll and one suffix roll** — well-defined, no new authored field |
| **A2** | `core` affinity silently dilutes — twelve `core` affixes against `pool_rolls = 3` means nine never appear | `core` maps to the **fixed core** (`effect_container_atom`), which already means *always*; it carries its own rarity band |

---

## 8. Related

- Ideal: [effect-pipeline-ideal.md](effect-pipeline-ideal.md) — 784 lines, nine questions closed, seven attacks
- [effect-atom-map.md](effect-atom-map.md) — line 213 names this program's reason to exist
- [effect-atom/definitions.md](effect-atom/definitions.md) — **wins over every spec here**
- [demon-seed-map.md](demon-seed-map.md) — the first consumer; its modules 15 and 16 gate on this
- [../research/arpg-effects/](../research/arpg-effects/) · [../research/ai-native-generation/](../research/ai-native-generation/)
