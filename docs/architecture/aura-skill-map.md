# Capability map: aura-skill

**Status: revised 2026-08-30 after an adversarial audit.** Pending owner approval. No build authorized.

Ideal: [aura-skill-ideal.md](aura-skill-ideal.md) · Defects found while specifying this program:
[derived-pipeline-audit-2026-08-30.md](derived-pipeline-audit-2026-08-30.md) · FE surface audit:
[../research/commander-fe-audit-2026-08-30.md](../research/commander-fe-audit-2026-08-30.md)

> **Revision note.** The first version of this map had **ten modules' worth of work in seven**, claimed
> `overlay-combat-enable` *"unblocks 8 of 12 auras"* (false — it gates the *reader*; the *writer* is
> blocked by R4), asserted *"aura level is the existing action rung"* (overstated — rung is an authored
> column nobody advances), and carried **no module for delivery, equipping, or the battle host**. It
> also allowed every module to pass while the feature did nothing. All corrected below.

---

## What this program delivers

A commander runs a **continuously-active aura** that buffs their own whole side. It occupies one of five
equipped-skill slots, costs resource every tick it is on, and only one may be active at a time
(tunable). Eleven side-wide auras, one per aptitude, plus **Focus**, which reverses direction and buffs
the commander's own actions instead.

It also lands the HoMM3 half: **the commander's level and primary-stat allocation actually reaching
game entities**, which today is disconnected at two points.

## ⛔ The acceptance rule that governs every module

**No module may be marked done on internal criteria alone.** The first version of this map failed a
basic test: every success criterion in all seven specs could be ticked with **zero entities ever
buffed**. So:

> **Program-level acceptance:** *enabling an aura raises a named channel on a real friendly entity by a
> hand-computed expected value, and disabling it returns the channel to its prior value.* Until an
> automated test asserts exactly that, the program is **not done**, regardless of module status.

Each module below names its share of that obligation. A module whose criteria can all pass while the
end-to-end assertion still fails is mis-specified.

## Decisions this map is built on (owner, 2026-08-29/30)

| # | Decision |
|---|---|
| Q7 | An aura grants to its **own side only**. The enemy is affected through the contest differential, not a second grant. |
| Q7a | Each scope gets a **buff/debuff bucket** accumulating modifiers — extending the actor stats bucket, not a parallel structure. |
| Q8 | **One aura active at a time**, `maxActiveAuras` tunable; on overflow the **oldest** switches off. |
| Q10 | **Two axes multiply**: the aura's own level **and** the commander's primary-stat share. *"2 commanders with same aura level but one having higher primary stats should buff stronger."* |
| Q10a | The aura's level axis is a **declared rung mapping**, per `spec-rung-table.md:137` — *"a new mechanism that grants actions declares its mapping; it does not invent a rung scale."* |
| Focus | **Focus reverses.** It buffs the **commander's own other actions** (cooldowns), not the units — which is why it is the only aptitude with no opposed channel. |
| Scope | W1+W2, the derived-bucket work, and the `OVERLAY-COMBAT` flip are all in scope. |

**The magnitude rule, corrected.** The aura's contribution goes through the **same shared read
function** every other aptitude consumer uses — `AptitudeReadFunctions.Magnitude(k, share, γ, P(Θ))`
= `k · share^γ · P(Θ)` — with the rung supplying `k`. `guard-class-system.ps1` G5 fails the build if a second
**`class AptitudeReadFunctions`** appears under `src/`. ⚠️ It matches the **class name only** — a
copy-pasted `k·share^γ·P(Θ)` inside another class passes G5 green. Reuse is the rule; the guard is a
partial backstop, not a guarantee.

- ✅ `Total = (k_allocation + k_aura) · share^γ · P(Θ)` — linear in the `k`s, rides the ladder.
- ⛔ **Never** a percentage of the actor's existing derived total. That is the only shape that
  compounds, and per-tick re-assertion of it is **geometric in tick count** (D2 in the audit).
- ⚠️ An earlier draft specified a **ladder-independent flat value**. That was wrong in the opposite
  direction: with no `P(Θ)` term the aura decays to irrelevance as `Θ` grows — a progression ceiling by
  arithmetic, which the endless-grind SSOT forbids.

⚠️ **`consumption` first appears at rung 7** (`action-rungs.v1.json`), so a `perTick` aura **cannot
exist below rung 7**. The usable span is 4 rungs (`QPowerMilli` 5359→12407, **2.3×**), not 10 (12.4×).
The declared mapping must live inside that band.

---

## Modules

| Module id | Responsibility | Depends on |
|---|---|---|
| `derived-modifier-bucket` | Per-source provenance on derived channels; `OverlayAdd` (audit D1) and the idempotence rule (D2). | — |
| **`aura-delivery-path`** ⭐ | **NEW.** Make a derived-channel aura *deliverable at all* — R4/audit D5: the runtime-support decision, a trigger vocabulary or an alternative to one, the `ScopeCompatibility` row, and a sink arm. **Nothing ships without this.** | `derived-modifier-bucket` |
| **`aura-equip-path`** ⭐ | **NEW.** Authoring an action row, a loadout write endpoint, and the equip UI. Today `SetLoadout` has no production caller, no endpoint exists, and no action row has ever been authored. Also owns audit **D3** (the first Skill grant throws every web battle). | — |
| `commander-lawn-bridge` | W1 + W2: the allocation delegate and `Θ` hydration. Delivers the HoMM3 half with zero aura content. | — |
| `overlay-combat-enable` | Re-prove C1–C13 (**including heals**, uncovered by the 2026-08-20 proof) then flip the flag. **Does not unblock any aura by itself** — it gates the reader, not the writer. | — |
| `aura-action-shape` | Enable/disable, the active set, `maxActiveAuras`, FIFO eviction as a typed visible outcome, `perTick` upkeep. | `aura-equip-path` |
| `aura-magnitude` | `k(rung) · share^γ · P(Θ)` through the shared read function, the declared rung mapping, the anchor, `aura.v1.json`. | `derived-modifier-bucket`, `commander-lawn-bridge` |
| `aura-content` | Eleven side-wide auras + Focus, as `world-buff.*` containers. Channels are **omni** (settled — see below). | `aura-delivery-path`, `aura-action-shape`, `aura-magnitude` |
| `aura-surface` | Active-aura state, eviction messaging, and the GG-49 contributions readout. | `derived-modifier-bucket`, `aura-content` |
| `aura-binding-producer` | **Added 2026-08-31** — [spec](aura-skill/spec-aura-binding-producer.md), ⛔ awaiting owner review. Writes the `effect_instance` + `effect_binding` rows on aura enable/disable so the shipped push chain has something to carry. Also closes two wiring gaps found while tracing it: the atom push fires **only on `Hello`** (`RpgHub.cs:43`), and active auras are **RAM-only** (`AuraRuntimeEndpoints.cs:31`) while bindings are durable. **This module was previously mis-recorded as `effect-atom` E20–E25** — those six shipped and are a different six things; see the spec's §1. | `aura-content`, `aura-action-shape` |

**Build order:**
`derived-modifier-bucket` · `commander-lawn-bridge` · `overlay-combat-enable` · `aura-equip-path`
(parallel) → `aura-delivery-path` · `aura-action-shape` → `aura-magnitude` → `aura-content` →
`aura-surface`

## Host order — battle first, and it is not optional

`decisions.md:92` is **binding**: *"every RPG feature must be fully playable and CI-provable with the
game closed — the injector may **enrich** a feature, never **gate** one."* `spec-standalone-charter.md:23`
is blunter: *"a feature that only works with the game open is **incomplete by definition**."*

And the evidence points the same way: `stat.derived` is `Battle: Full`, `Lawn/Sim: None`. Commander
allocation **already reaches battle** (`WebMatchService.AptitudeChannelMods`). `BattleStatComposer`
already applies arbitrary `ChannelMod`s validated against every registered channel.

**So battle is both the required host and the cheap one.** Every module delivers battle first; the lawn
is an enricher slice afterward. `commander-lawn-bridge` and `overlay-combat-enable` are lawn-only by
nature and must not sit on the critical path to first working aura.

---

## Decisions — all questions closed 2026-08-30

Nothing in this program is waiting on an answer. Recorded here so a future reader sees the reasoning,
not just the outcome.

| # | Was | Decided |
|---|---|---|
| 1 | Which rung band? | **Tier-mapped onto rungs 7–10** (`consumption` floor at 7, `cap: 10`), and **the mapping itself is a tunable** — owner: *"tunable is requirement… game balance for future."* |
| 2 | Omni or element slots? | **Omni** — and it was never a magnitude decision. `CombatDerivedReader` reads `omni + element(e)` **additively** and `ElementPayload.Validate` enforces `Σ weights = 1.0`, so +X to omni and +X to all six contribute **the same X**. One element contributes `w_e·X ≤ X` and **zero** against an untyped attack. Four of the twelve auras name families read **omni-only** (parry, block, reflection). `PatronAuraOverlay` is not a counter-precedent — its element *is* its content. |
| 3 | Who runs Zomboss's auras? | **Each `ZombossPattern` names its aura** (T17). Authored data, tunable, no AI logic. ⚠️ Dynamic AI aura control — Zomboss casting and swapping based on board advantage — is a **separate, larger feature** needing a control surface the repo lacks. Deferred by name in the todo. |
| 4 | W4 — reflect is dead | **Wire `actorResolve` at the five production call sites** (T20). The math exists and is test-exercised; only the argument is missing. Fixes a shipped-looking feature that never fires, independent of auras. |
| 5 | `commanderOnly` vs auras | **Keep both, relationship defined.** Banner = **gear** (found/crafted, item progression, 100‰ item budget); aura = **skill** (chosen/invested, aptitude progression, aura budget). **They stack; budgets stay separate.** |
| 6 | Own-side resolution | **Build the real specimen-ownership bridge** (T21), not a narrower selector. Unlocks `RelationKind.Ally` — the property that makes one authored row serve both factions. |
| 7 | Toggle model | **Add a recompose seam to `BattleEngine`** (T4). Battle is match-frozen today — `Derived` is get-only with one `Compose` call site. ⚠️ Kernel work; **stop-and-ask if goldens move.** |
| 8 | Commander identity (R3) | **Real actors in empire legions** — Crazy Dave and Dr. Zomboss, two for now, broader roster later. |
| 9 | D3 landmine | **Both** — degrade now (T3), wire the `ActionCatalog` when content lands (T19). |
| 10 | `patron.aura` outscaled | **Give it a `P(Θ)` term** (T22). ⚠️ Spec-locked system and **row 16 of `ssot-power-scale.md` §10** — a reviewed edit, not a tuning tweak. |
| 11 | Aura upkeep pool | **Per-aura cost lists, 1 to 6 resources.** Required correcting a design defect first — see below. |
| 12 | Eviction at `maxActiveAuras > 1` | **Pure FIFO, oldest always goes.** No pin, no refusal. |
| 13 | Budget shape | **Shared default with a per-aura override.** Parity by default, deliberate outliers possible. |
| 14 | Is an aura a fourth action kind? | **No — a property of a skill.** `ActionKind.Skill` with continuous/toggleable/battlefield-scoped flags. Decision 25 untouched. |

**One decision reached outside this program.** Q11 collided with *"`hp`, `hunger` and `spirit` are never
action costs"* — and the owner ruled the rule itself a **design defect**: it made an HP-sacrifice action
unbuildable, made a sun-priced plant action unbuildable (`hunger` **is** Sun on the plant side), and left
`spirit` with **no sink at all**. **All six resources are now legal action costs**, corrected in
`decisions.md`, `resource-hub-ssot.md` and `concrete-action-roster.md` **before** the aura work, with two
rules attached: every resource documents what spending it *means*, and `hp` costs **floor at 1** unless an
action opts into lethality.

## Boundaries this program inherits

- **RPG layer only.** No PvZ engine changes.
- **One power ladder.** Reads `Θ`/`P(Θ)` through the shared read function; adds no curve. ⚠️ Do **not**
  assert "no new §10 inventory row" as settled — `PatronPolicy.AuraMilli` **was** added as row 16, and
  `guard-power.ps1`'s heuristic keys on `level|lvl|index`, so a parameter named `rung` would slip past.
  A green guard there proves the regex missed it, not that the design is inside the inventory. **Test
  the constraint; take the row question to the owner.**
- **Balance surface is data.** Every number in `data/tuning/aura.v1.json` or an existing tuning file.
- **No hard progression ceilings.** Soft caps configurable; absolute bounds throw.
- **Provenance must survive the bucket** — auras are withdrawn mid-run by eviction.
- **Git hands-off.** The owner commits.
