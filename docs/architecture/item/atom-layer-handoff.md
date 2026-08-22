# Handoff to the effect-atom program

**Status:** Written 2026-08-22 at the end of the item enrichment round (R4 reconciliation). **This is a
handoff, not an edit.** Nothing in the effect-atom program's own documents or task lists has been
changed by this round — every correction below is proposed, with its evidence, for that program's owner
to apply.

Source material: [defect-register.md](defect-register.md) (R1, suites run), the four `decision-d*.md`
files, and the seventeen `ssot-*.md` lane documents. Index: [README.md](README.md).

**Test state at handoff:** 2 664 tests green (Core 2 257, Data 353, Guard 54), 0 failures, 0 skipped,
all four boundary guards OK, verified after commit `c4c9908`. `FUSIONRPG_GAME_DIR` is **not** required
for these suites.

---

## 1. The correction that matters most — units

**`definitions.md` §2 is wrong for half the derived families, and it is the document that wins over
every spec.** Left standing, the next session to read it will author tier bands wrong by an order of
magnitude, exactly as one lane in this round did.

The document says every derived-channel magnitude is *"resolver points — sigmoid scale,
`AccuracyScale = CritRateScale = 100.0`"*. Verified against the readers this session:

| Family group | Actually | Evidence |
|---|---|---|
| `combat.power.*`, `combat.defense.*` | **Flat game units.** `(power − defense)` is summed into `weightedDelta` and added directly: `powerAdjusted = BaseOverlayDamage + weightedDelta` | `src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs:84-89`, `:104` |
| `combat.shield.*` (capacity, toughness, pen, regen) | **Flat hit points** | shield runtime |
| accuracy / dodge, crit rate / crit resist, crit damage / crit resist-damage | **Genuinely sigmoid** | `CombatProbability.Sigmoid(delta, …Scale)` at the same call site |

The decisive negative evidence: **`CombatProbabilityPolicy` declares only `AccuracyScale`,
`CritRateScale`, `CritDamageScale` and `Steepness` — there is no `PowerScale` and no `DefenseScale`**
(`src/FusionRpg.Core/Stats/Derived/CombatPolicies.cs:9-14`), and `CombatDerivedReader.Power` is a plain
sum of the omni and element channels with no sigmoid anywhere
(`src/FusionRpg.Core/Combat/CombatDerivedReader.cs:9-10`).

So **`+10 fire power` is +10 damage** — the peer of `+10 hp`, not a tenth of it. The sigmoid half is
confirmed, not dismissed: the 7.6% → 26.9% crit calibration in `definitions.md` reproduces exactly.

**Proposed edit to `definitions.md` §2**, replacing the single "Derived-channel magnitudes" row:

| Kind of value | Unit |
|---|---|
| Primary-channel magnitudes | game units (hit points, attack points) |
| `combat.power.*` · `combat.defense.*` · `combat.shield.*` | **game units** — additive damage / hit points |
| `combat.accuracy.*` · `dodge` · `crit.rate` · `crit.resist` · `crit.damage` · `crit.resist.damage` | **resolver points** — sigmoid scale, `AccuracyScale = CritRateScale = CritDamageScale = 100.0` |

The same correction is owed to [atom-family-library.md](../effect-atom/atom-family-library.md) §2a,
whose worked example asserts the wrong order of magnitude in the same words.

**Everything downstream that this touches:** `atom-family-library.md`'s tier-band guidance, E9's
`normalize(magnitude, referenceScale)` coefficient table, and this round's
[ssot-affixes.md](ssot-affixes.md), which authored five-tier bands on the wrong premise for six families.

---

## 2. Confirmed defects

Verdicts from [defect-register.md](defect-register.md), which ran the suites. **Nine of the ten are
latent**: the E6 instance/binding layer has **zero production consumers outside `FusionRpg.Data`** —
nothing reads `InstanceRow`, `ResolveBindings`, or `values_json`, and `AtomCompiler` is referenced only
by its own test. That converts these from an emergency into a fix-before-the-first-item-row list.

**One is live today.**

| # | Defect | Where | Note |
|---|---|---|---|
| **A1** | **`player:` scope buffs both sides.** It falls through to match-wide, and `match` matches plants *and* zombies | `StatApplyScope.cs:52-53`, `:81-82`, `:88-92`; `EffectProcAndOwner.cs:59-60` | **LIVE.** A green test named `..._player_stub_is_match_wide` (`StatSystemTests.cs:423-432`) asserts a `player:9` +4 atk gives the plant **and the zombie** 14. Fixing this turns a blessed test red on purpose — do not let a later session "fix" the test instead |
| **A2** | **First content import unequips everything.** `ResolveBindings` compares instance `catalog_revision` by **equality**, so any bump refuses every existing binding | `RpgStore.AtomInstances.cs:288-295` | Latent only because nothing calls it yet. Proven by a green test. Found independently by D1 and D2 |
| **A3** | **Unbinding deletes the instance.** The orphan sweep removes every `effect_instance` with no binding, and runs after every withdraw | `RpgStore.AtomInstances.cs:414`, `:460-472` | Correct for session-scoped `entity:` grants; catastrophic for owned gear. Needs a second reachability root before any item row exists |
| **A4** | **`level_req` is never enforced, anywhere.** The gate skips the check when `OwnerLevel` is null — and `BindContext.OwnerLevel` has **no production writer**, while `ResolveBindings(…, int? ownerLevel = null)` never uses its parameter | `BindGate.cs:47`; `RpgStore.AtomInstances.cs:264` | Worse than the original claim. Tests cover "met" and "absent", never "present but unknown" |
| **A5** | **The effect-list tiebreak is a generated GUID.** The third sort key is `instance_id` — precisely the mistake `definitions.md:182` rejects for `binding_id` | comparer + `EffectBag.cs:84` | Reproducibility, not totality. `EffectBag.cs:84` is additionally a **culture-sensitive** sort |
| **A6** | **Disabled atoms are drawable.** `Instantiator.Draw` filters only on `Weight > 0`; `ContainerValidator` never reads `AtomRow.Enabled`, so a disabled atom is drawn and then bind-rejected `StaleInstance` | `Instantiator.cs:131`, `:135` | Latent drop bug |
| **A7** | **Curve `input` is ignored.** `AtomCompiler.MultiplierFor` calls `MultiplierAt(ownerLevel)` unconditionally — a `rarity` curve *and* a `tier` curve are silently evaluated at the owner's level | `AtomCompiler.cs:230-236` | Live and wrong, not dead code. Found by D3 |
| **A8** | **`catalog_revision` is not a faithful label.** A direct upsert changes content without bumping it | `RpgStore.ContentHash.cs:10-13` | Undermines every guarantee keyed on the revision |
| **A9** | **The rarity table is unenforced and unread.** `effect_container.rarity` is free TEXT with no FK and `ContainerValidator` never validates it; "append-only ordinals" is not enforced, because `ON CONFLICT … SET ordinal = excluded.ordinal` can move an existing rung; and `RarityRow.PoolRolls`/`MinTier`/`MaxTier` have **zero production readers** | `RpgStore.Containers.cs:52-58`, `:79-82` | Shipped this week, already SC7 scar tissue |
| **A10** | **`min_tier`/`max_tier` are authoring assertions, not runtime filters.** The validator rejects the whole container; the draw never consults the window | `ContainerValidator.cs:88-92`; `Instantiator.cs:131`, `:135` | Not necessarily a bug — G1 shows the fixed-core exemption is load-bearing and deliberate. But the docs describe it as a filter |

### Refuted — do not act on these

Both were reported to the owner during the round and are withdrawn:

- **`effect_instance` lacks `origin_catalog_revision`.** It has `catalog_revision`, written, read and
  tested (`RpgStore.AtomInstances.cs:60`, `:107-117`, `:144`, `:337`). The lane searched for the wrong
  name.
- **`effect_instance_atom` cannot hold an unresolved value spec.** `values_json` already carries it
  verbatim, pinned by a passing test (`Instantiator.cs:206`; `InstantiatorTests.cs:130-142`).

### Filed but not verified — three more, from G3

Read from source, never executed. **R1 did not cover these**; they need the same treatment.

- **C1** — the `Increased`/`More` unit boundary. SC4 mandates integer per-mille; `StatComposer.cs:25-32`
  reads fractions; no `/1000` conversion was found in `AtomCompiler`. If real, `+15%` composes as ×151.
- **C2** — `ResistanceEvaluator.ComputeNetFactor` (`:212-217`) clamps a delta to `[0, 10000]` and uses
  it as a direct multiplier on magnitude **and** duration (`:164-165`) against a `1.0` baseline, so
  **`+1 status power` doubles every status.** Blocks tier bands on two affix families.
- **C3** — `effect_atom.name` is unvalidated; empty names load clean.

### Structural, found by D1

- `effect_binding` has **zero production consumers** — only `RpgStore.AtomInstances.cs` and two test files.
- `definitions.md` §6 promises an `ON DELETE CASCADE` FK on `effect_binding` that **the shipped DDL does
  not declare at all.**
- `Reset()` deletes `rpg_unique_actors` but **not** `effect_binding` (`RpgStore.cs:600-621`); no FK is
  possible on a polymorphic `owner_key`.
- `ClearSessionScopedBindings` has **no caller in `src/`**.
- A web-battle actor has **no legal `entity:` key** — ptrs are `web:{matchKey}:{n}`
  (`BattleReportEmitter.cs:23`) and `entity:` requires `^[0-9a-f]+$` (`OwnerScope.cs:118-122`).

---

## 3. Counts to correct

| Claim | Reality | Where it is written |
|---|---|---|
| "71 authored families" | **70** rows; by the document's own rule the *authorable* total is **69** — §3.4's header carries a status count (21) into a family count (20 rows) | [atom-family-library.md](../effect-atom/atom-family-library.md) §6, and the map |
| Reason codes: 33, closed | Still 33 (+`None` = enum length 34, the literal at `AtomKindRegistryTests.cs:33`) — but this round proposes **68 distinct new codes**, taking it to **101** | §4 below |

---

## 4. The reason-code problem

Thirteen lanes each proposed codes against a closed list of 33, and none could see the others' tables.
The register enumerated them row by row: **70 raw proposals, 68 distinct → 101 codes.**

Only **one** exact-name collision across thirteen independent documents (`FrameMismatch`, three lanes),
which says the contract's boundary cuts worked — but there are four clusters of near-duplicates, and two
codes have no owner at all.

**Tripling an operator-facing error surface by accumulation is not a decision anyone made.** G1 proposed
the fix independently: a single `ContentRuleViolated` code carrying a namespaced rule id, so content
lints stop consuming the enum. Recommended, and it needs the effect-atom program's agreement because the
enum and its count guard live there.

---

## 5. Named requests against the closed vocabulary

Neither is assumed; both are written up as requests with their justification in the lane docs.

> ⚠ **One gap in this document went stale within a day, and an authoring agent caught it.** The
> catalog SSOT's gap G4 says `capPerMatch` is in the FA9 allowlist "with no implementation anywhere",
> and that claim was repeated into a stage-1a brief. It is **no longer true**: `AtomRunner.cs`,
> `RunnerState.cs` and `tests/FusionRpg.Core.Tests/Atoms/CapPerMatchTests.cs` all ship it — verified
> this session, not taken on trust. The real remaining gap is narrower and worth stating precisely:
> **`bands.v1.json` has no channel-family curve that resolves a `capPerMatch` value from a band**, so
> the no-raw-numbers rule still stops an author writing one.
>
> The lesson generalises past this one row. The briefing rule learned in wave 0a was *carry the owner's
> scope decisions, because lane documents are a snapshot*. Its companion is now proven: **verify a
> hazard is still real before repeating it into a brief.** A stale warning costs an agent real effort
> designing around a constraint that no longer exists.

| Request | From | Why |
|---|---|---|
| **An eighth trigger, `OnUse`** | [ssot-consumables.md](ssot-consumables.md) | An instant consumable has no trigger it may legally name — yet `EffectBag.Grant` already fires all actions immediately for a `Passive` def (`EffectBag.cs:194-204`, `:417`). **The runtime does it; the schema forbids it.** The triggerless-`Passive` workaround was rejected because it gives "no trigger" two opposite lifetimes |
| **`damage.convert`** (13th kind) | [ssot-uniques.md](ssot-uniques.md) | Conversion/replacement uniques are unauthorable without it. Filed as depended-on-by-nothing and blocked on a damage-applier spec that has no owner. **Not** requested for v1 |

Also relevant: **the atom layer has no binding with a lifetime.** No expiry column on `effect_binding`,
no duration on `EffectGrantDto`; the only two real clocks are `StatusRuntime.DurationMs` and
`BattleInnateShield.DurationMs`. A timed buff must therefore be a status — and the payload it needs,
`StatusPayloadKind.ModifyStat`, has **zero consumers**. The locked resource model owes the identical
mechanism for exhaustion debuffs, so two programs want one piece.

---

## 5b. One reservation the atom layer must honour

`entry-shapes.md` §6, following [ssot-enhancement.md](ssot-enhancement.md) §5.5, mints the `+X`
enhancement track's families into a **reserved stem no affix pool may ever draw from**:
`atom.enhance-*`. Ten such families now exist on disk (`enh.001`–`enh.010`).

The reservation is what stops a rolled item colliding with a milestone on `(family_id, variant)`,
and it only holds if the atom layer honours it too. Two asks, both cheap:

- **Never mint `atom.enhance-*` from the affix side.** The item validator enforces this on the seed
  corpus (`RuntimeFamilyStem`, `RuntimeFamilyCollision`), but the item corpus is not the only thing
  that writes atom families.
- **Treat the stem as excluded from pool rolls**, not merely classified. A milestone is neither
  prefix nor suffix; `affixClass` on one is a category error, not a value to derive.

Worth flagging because the stem reads like a naming convention and is actually a uniqueness
mechanism — the sort of thing that survives review and dies to a later refactor that "tidies" it.

---

## 6. Corrections owed to other programs

- **`spec-action-model.md` §5** says granted actions reuse `effect_binding` with "no second binding
  concept". They cannot: `effect_binding.instance_id` is `TEXT NOT NULL` and points at an
  `effect_instance` carrying `roll_seed`/`values_json`/`power_json`
  (`RpgStore.AtomInstances.cs:76-77`), and a granted action has no instance. The *vocabulary* is
  reusable; the *table* is not. See [ssot-granted-actions.md](ssot-granted-actions.md).
- **`ContentHashRegistry.cs:33`** already reserves version 2 for E18 and 3 for E9. The item program needs
  a bump and is not in that sequence — someone must allocate across three programs.
- **`stub.hp_charm`** (`UniqueEquipmentCatalog.cs:25`) is a `trinket` **equip** stub, not a charm. It
  should not carry the charm name forward.

---

## 7. Suggested order, if a fix wave is authorized

Nothing here is authorized. This is the dependency order the register derived, for when it is.

1. **Stage 0 — free, no design dependency:** A5 (tiebreak + the culture-sensitive sort), A6, A9, A7, C3,
   and the count corrections in §3. Each is small and isolated.
2. **Stage 1 — before the first item row exists:** A3 and A2. Both are unavoidable for durable items and
   both are cheap *now*, because nothing calls the code yet.
3. **Stage 2 — needs a decision first:** A1 (turns a green test red on purpose), A4 (needs a writer for
   `OwnerLevel`, which is really D1's projector), and the reason-code surface in §4.
4. **Verify C1 and C2 before either is used to justify anything.** C2 in particular changes what a status
   magnitude means.
