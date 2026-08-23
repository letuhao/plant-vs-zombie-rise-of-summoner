# Overflow triage — the 92 A3 findings

Executes power-todo.md **P0.3**. Every `int`-on-a-magnitude finding `audit-overflow.py` reported in
its `A3` category (baseline run, 2026-08-23), classified **LADDER** / **BOUNDED** /
**NOT-A-MAGNITUDE**, per [power-plan.md](../../../tasks/power-plan.md) Phase 0.

**Result: 92 → 75.** Three regex defects in `scripts/audit-overflow.py` accounted for all 17
NOT-A-MAGNITUDE verdicts; each is fixed in the script itself (below), not waived in this doc — the
count dropped because the tool got more precise, not because findings were excused. The remaining 75
split **56 LADDER** (P0.4 widens these to `long`) and **19 BOUNDED** (each names its proven cap; P0.4
touches none of these).

`75 == 56 + 19` — the identity `--targets A3` count == LADDER + BOUNDED, required by P0.3's accept
criterion, holds.

---

## 1. The three regex fixes

All three are in `scripts/audit-overflow.py`, already applied.

### 1.1 `hp` matched the accidental substring "hP" (case collision)

`MAGNITUDE`'s `hp` alternative ran case-insensitively, so it matched not just genuine `Hp`/`HP`/`hp`
but also **`hP`** — lowercase `h` immediately followed by uppercase `P`, which arises whenever a word
ending in "h" abuts a word starting with "P". `PatronPolicy.KillEarnWithPatron` triggered this
exactly: `...Wit` + `h` + `Patron...` reads as `...hP...` under `re.I`.

Genuine `hp` usage is always `Hp` (capital H, lowercase p, as its own camelCase word) or all-lowercase
`hp` — never the inverted case pair. Fix: match `hp` as an explicit case-sensitive alternation
`(?:Hp|HP|hp)`, pulled out of the case-insensitive group that covers the rest of `MAGNITUDE`. Verified
against every genuine hit in this doc (`HpFlat`, `MaxHp`, `ZombieHp`, `HpBase`, `HpRemaining`, the bare
`hp` parameter names) — none depend on matching `hP`, so nothing real was lost.

**Removed:** `PatronPolicy.cs:55` (`KillEarnWithPatron` — the method name, not the soul math inside it).

### 1.2 A3 had no per-mille RATIO exclusion (A2 already had one)

A2's own comment states the rule: *"A per-mille RATIO (chance, stability, share) is bounded 0..1000
and is SAFE in int forever. A per-mille MAGNITUDE (hp, damage, yield) is unbounded."* A2 checks this;
A3 never did, because A3's regex only asks whether the identifier contains a magnitude word — not
whether it also carries a `Milli` suffix that turns it back into a bounded ratio. `DefenseMilli`,
`SoulLootMilli`, `EssenceProcMilli` all contain a magnitude substring (`defense`, `soul`, `essence`)
*and* end in `Milli`, and every one of them is a bonus, a proc chance, or a multiplier — not an
accumulating total.

Fix: apply the same exclusion A2 already has — a name ending in `Milli`/`PerMille` is dropped from A3
unless it also matches `UNBOUNDED_MILLI` (stock/total/sum/balance/treasury/banked/accrued/lifetime/
cumulative), mirroring A2's own escape hatch for genuine per-mille magnitudes.

**Removed:** `BattleModels.SoulLootMilli`, `TraitBattleCatalog.{OnKillHealMilli, SoulLootBonusMilli,
EssenceProcMilli, EssenceRiderMilli}`, `StarPolicy.PerStarDefenseMilli`, `PatronPolicy.{DefenseMilli,
SecondaryDefenseMilli}`, `FactReader.HpMilli` (×2 — the field and its accessor),
`RunnerEventMapper.FullHpMilli`, `ExpeditionResolver.FoundSoulsCeilMilli`,
`StructureCatalog.YieldMultiplierMilli`, `LoamPolicy.WellYieldMultiplierMilli` — 14 findings.

### 1.3 `NOT_MAGNITUDE` had no entry for structural counts/tiers ending in "Unit"/"PerActor"

`ElementTable.ShieldUnit` returns an elemental matchup **tier** (`-1`/`0`/`1`/`2`, the same shape as
the neighbouring `CombatUnit`), not a shield HP amount — matched only because "shield" is a magnitude
word and the function name happens to contain it. `ShieldPolicy.MaxShieldsPerActor = 3` is a **slot
count**, not a magnitude — matched the same way. `NOT_MAGNITUDE` already excludes trailing
`count/size/cap/max/min/limit`-style suffixes; `unit`/`units`/`peractor` fit the same pattern and are
added to the same list, no new mechanism.

**Removed:** `ElementTable.ShieldUnit`, `ShieldPolicy.MaxShieldsPerActor` — 2 findings.

**Total removed: 14 + 2 + 1 = 17.** `92 − 17 = 75`, confirmed by re-running the audit.

---

## 2. LADDER — 56 findings, widen to `long` in P0.4

Grouped by the pattern that explains them, not file order — the pattern is the finding.

### 2.1 The systemic gap: Hp already widened, Atk/Armor left behind

**The single largest, most consistent defect in this triage.** Five separate files show the exact
same shape: `Hp`/`MaxHp` fields are already `long` (someone widened them, likely when `BattleEffects`
and friends first went in), but the sibling `Atk`/`Arm1`/`Arm2`/`Defense` fields on the *same record*
were never touched. Once one channel of a combat record is `long`, treating the others differently is
not a design choice — it is the same defect P0.4 exists to close, just not yet reached everywhere.

| File | Type | Hp/MaxHp | Left as `int` |
|---|---|---|---|
| `Core/Stats/EntityBaseline.cs:4` | `EntityBaseline` | already `long` (:6-7) | `Atk`(:8) `Arm1`(:9) `Arm1Max`(:10, not flagged¹) `Arm2`(:11) `Arm2Max`(:12, not flagged¹) |
| `Core/Stats/EntityBaseline.cs:25` | `EntityFinal` | already `long` (:27-28) | `Atk`(:29) `Arm1`(:30) `Arm1Max`(:31¹) `Arm2`(:32) `Arm2Max`(:33¹) `DefenseFlat`(:37) |
| `Core/SimModels.cs:15` | `SimEntity` | already `long` (:21-24) | `AttackBase`(:25) `Attack`(:26) `ArmorBase`(:27) `Armor`(:28) `ArmorMax`(:29, not flagged¹) |
| `Injector/Stats/EntityStatWriter.cs:16` | `AppliedFinal` | already `long` (:18-19) | `Atk`(:20) |
| `Injector/Stats/EntityStatWriter.cs:303,317-318` | `Remember`/`ProofWrite` params | already `long` | `atk`/`atkBefore`/`atkAfter` |

¹ `Arm1Max`/`Arm2Max`/`ArmorMax` are the *ceiling* for the same channel `Arm1`/`Arm2`/`Armor` carries —
the audit didn't flag them (no magnitude word matches "Max" alone), but P0.4 must widen them alongside
their paired field or the pair becomes inconsistent again immediately. Called out here so the
implementer doesn't have to re-discover it; **not** a new class of finding.

**Also downstream of this same gap:**
- `Core/StatMath.cs:7` `ScaleHpOrAtk` returns `int`, sitting next to `ScaleHp` (same file, already
  `long`) — the "Atk" half of the function's own name is the un-widened half.
- `Core/StatMath.cs:13` `ScaleIncoming(int damage, ...)` — computes incoming damage after defense;
  same channel.
- `Core/Stats/StatComposer.cs:117` `ComposeDefense(..., out int defenseFlat)`.
- `Core/Stats/StatSystem.cs:178` `ScaleCurrentHp(int previousHp, int previousMax, int newMax)`.
- `Injector/GameDumps.cs:40,89` — `Plant(...)`/`Zombie(...)` dump functions already take `long
  hpBase, long maxHpBase` but `int attackBase` (and, for Zombie, `int armorBase, int
  armorMaxBase`) — the exact same asymmetry, one layer up (captured-data dump feeding
  `DemonSpeciesGenerator`).

### 2.2 The battle-engine combat channels (T2.1's exact target)

`Battle/BattleEffects.cs:9-10` (`IBattleHpTarget.Hp`/`MaxHp`), `Battle/BattleEngine.cs:48-50`
(`Hp`/`MaxHp`/`DamageDealt`), `Battle/BattleModels.cs:16-18` (`BattleActorSetup.MaxHp/Atk/Defense`),
`:45` (`BattleChannelMod.Amount` via `MagnitudePerPulse` on the same line — a status DoT/regen's
per-pulse HP delta, an absolute amount, not a per-mille ratio), `:61-63` (`BattleRuleset.BaseHp/
BaseAtk/BaseDefense` — **the exact three functions `battle-magnitude` (T2.1) replaces with
`PowerLadder.Value`**), `:121` (`BattleActorResult.HpRemaining`). `Battle/TraitBattleCatalog.cs:47`
(`BerserkerRampMilli(TraitBattleDef def, int hp, int maxHp)` — the flagged identifier is the bare `hp`
parameter, which mirrors the same Hp channel; the function's own *return value*, a ramp multiplier, is
correctly a bounded ratio and is not this finding). `Battle/Timeline/BattleTrace.cs:60` (`State(...,
int hp, long shieldAbsorbed)` — `shieldAbsorbed` on the same line is already `long`; identical
asymmetry to §2.1, one more instance of it).

These are the load-bearing findings for Phase 2's Checkpoint — "hp/atk/defense travel Θ → P(Θ) →
`BattleRuleset` end to end" is not possible while `BaseHp`/`BaseAtk`/`BaseDefense` return `int`.

### 2.3 Souls and other currency/summon magnitudes

`Demons/Patron/PatronPolicy.cs:18` (`SwitchCostSouls = 100`) and `:57` (`SoulsAfter`, the local
function inside `KillEarnWithPatron` computing the running soul total — distinct from the method name
itself, which §1.1 already cleared). Both are souls, and `Demons/Fusion/StarPolicy.cs:34`
(`FusionCost.Souls`, not itself flagged — already `long`) is the precedent both should match. Feeds
directly into T3.6's earn-formula work (SSOT §11.7a) — widening these now means T3.6 isn't also doing
a type migration while it changes the formula.

`Effects/Atoms/Power/ActorPowerCache.cs:109` (`PriceBody(int hp, int atk, ...)` — the doc comment is
explicit: *"A 5000 hp summon is worth 5000 hp of survivability"*, and summon stats are meant to scale
without a ceiling). `Effects/Atoms/Power/CostFunction.cs:181` (`MeanMagnitude` — computes via
`(long)spec.Min + spec.Max` then **narrows back to `int` on return**, the exact cast-after-widen
anti-pattern the ladder makes dangerous; keep it `long` through the return). `Core/Effects/
SimEffectHost.cs:181` (`HitDealt(..., int damage = 20)` — a production `Core/Effects` helper
constructing `EffectEventDto.Damage`; check that DTO field's own declared type while touching this
site, since it wasn't independently flagged).

### 2.4 World entities and DTOs

`Contracts/Dtos.cs:8,10,12` (`StatMod.HpFlat/AttackFlat/DefenseFlat` — the cheat/config flat-stat
modifiers applied to base-game plants/zombies via `CheatState.Stats`; a cheat console can type an
arbitrary flat value, and it feeds the same `int` arithmetic as everything else in §2.1).
`Contracts/WorldDtos.cs:169` (`WorldEntityMemberDto.Hp`) and `Core/World/WorldState.cs:208`
(`WorldEntityMember.Hp`, the in-memory mirror of the same shape) — world legion/warband member HP,
uncapped world-tier growth per the caps register. `Core/World/Loam/LoamPolicy.cs:145`
(`UnmadeMemberHp = 120` — a spawned warband member's HP; same channel as the two above, currently a
small literal but not distinguishable in kind from `WorldEntityMember.Hp`).

### 2.5 Debug/cheat entry points that write into the live combat pipeline

`Injector/CheatActions.cs:772` (`SetSelectedZombieHealth(int hp)` — unlike the fixed-literal debug
scenarios in §3, this is a **live cheat command** that calls `EntityStatWriter.ForceSetZombieHp(z, hp,
...)`, which already takes `long`; the cheat entry point itself is the un-widened link in that chain).

`Injector/Stats/EntityStatWriter.cs:215` (`ForceSetPlantHp(Plant p, int hp, ...)`) — **note the
asymmetry with its own sibling**: `ForceSetZombieHp` two methods down (`:230`) already takes `long
hp`. Widening `ForceSetPlantHp` to match is not just "change int to long" — the current body writes
`p.thePlantHealth = hp` directly (Unity's own field, `int`-typed per the `ClampToInt32` pattern used
everywhere else in this file); after widening, that write needs the same explicit
`ZombieCombatFields.ClampToInt32(hp)` `WritePlant`/`AddPlantHp` already use, or the widened method
won't compile against Unity's field type. This is the one LADDER item in this triage that is "widen
+ add the boundary clamp," not "widen" alone — flagged so P0.4 doesn't treat it as the generic case.

**Dead code, delete rather than widen:** `Injector/Stats/EntityStatWriter.cs:438,441`
(`UnityStatWriter.WritePlant/WriteZombie`) — the class comment calls itself a *"Backward-compatible
alias"*; a repo-wide grep for `UnityStatWriter` finds only its own definition, zero call sites. Same
shape as `IProgressionPowerProvider` (T1.4: "deleted, zero `SetLevel` callers"). P0.4 should delete
this class, not widen its signature — simpler, and the repo's own style guide already prefers deletion
over carrying dead compatibility shims.

### 2.6 The debug-scenario `SimModels`/`SimEngine` seed values

`Core/SimModels.cs:5-9` (`SimDefaults.{PlantHp, PlantAttack, ZombieHp, ZombieAttack, HitDamage}`).
These look like fixed test literals (matching the pattern in §3's debug scenarios), **but they are
not**: `SimEngine`/`SimModels` implement *"Server-side board simulation (no Unity)"*
(`SimEngine.cs:8`) — a production server-side simulation subsystem (`FusionRpg.Server/
SimEndpoints.cs` is a live caller), not a debug fixture. `SimDefaults` seeds `SimEntity`'s already-long
`Hp`/`MaxHp`, so these five constants are LADDER for the same reason as §2.1's fields: they feed a
production magnitude channel that is already half-widened.

---

## 3. BOUNDED — 19 findings, each with its proven cap. P0.4 touches none of these

### 3.1 Harmony hook signatures — bounded by the base game's own compiled method (10 findings)

`Injector/CheatPrefixes.cs:13,43,53` (`Prefix(ref int damage)` / `Prefix(ref int theDamage)` ×2),
`Injector/GameCaptureHooks.cs:217,246` (`Prefix(Plant __instance, int damage)` /
`Prefix(Zombie __instance, int theDamage)`), `Injector/GameHooks.cs:619,724`
(`PlantTakeDamage.Prefix`/`ZombieTakeDamage.Prefix`, both `ref int`).

**Proven cap:** Harmony patches a prefix/postfix by reflecting the target method's exact parameter
types. `GameHooks.cs:618`'s own comment states the target signature verbatim: *"Plant signature: (int
damage, IDamageMaker damageFrom, DamageType damageType, PlantType reportType, bool fix)"* — this is
`Plant.TakeDamage`, compiled into the PVZ Fusion Unity assembly. Changing `ref int damage` to `ref
long damage` would not compile against Harmony's reflection-matched target; it would require patching
the base game's own compiled method, forbidden outright by AGENTS.md ("Never download or patch the
PVZ Fusion game binary"). The base game's own per-hit damage (a pea's ~20, a zombie bite's modest
flat amount) is bounded by the vanilla game's own numbers and was never RPG-scaled at this boundary —
RPG-scale damage is applied *before* this point (writing `p.attackDamage`/`z.theAttackDamage` via
`EntityStatWriter`, itself already `long`-aware per §2.1) and *clamped* at the exact place values
cross into Unity's own fields (`ZombieCombatFields.ClampToInt32`, already implemented). This clamp is
a genuine external-system boundary, not a progression ceiling — the structural-limit exemption
(CLAUDE.md's caps table) applies, and it is already commented as such at each Harmony hook via the
signature-match doc comments.

**Same lineage, downstream of the Harmony-bounded value, never independently larger:**
`Injector/DebugActions.cs:778` (`EmitBoardAction(..., int damage, ...)` — a debug payload emitter),
`Injector/GameHooks.cs:824` (`fallbackDamage` — passed the same Harmony `damage`/`theDamage` at both
call sites), `Injector/Effects/EventDrainHost.cs:45,97` (`TryRecordDealtFromBullet`/`TryRecordTaken`
— both called with the Harmony-bounded `damage` from `PlantTakeDamage`/`ZombieTakeDamage`, confirmed
by reading the call sites in `GameHooks.cs:654-658,762-766`).

### 3.2 A Unity-field snapshot tuple — same boundary, one more site

`Injector/Stats/EntityStatWriter.cs:358` (`ConcurrentDictionary<IntPtr, (int hp, int max)>
BeforeCall`, inside `PlantLimHealthPolicy`). **Proven cap:** populated directly from
`(__instance.thePlantHealth, __instance.thePlantMaxHealth)` (`:372`) — Unity's own `Plant` fields,
the identical boundary as §3.1. A before/after snapshot of a Unity field must be typed to match that
field.

### 3.3 Debug/cheat scenario fixtures — authored literals, never touch the ladder

`CheatCore/DebugScenarios.cs:1177,1274` (`ZombieSlot`'s `int hp = 8000` tuple field, and the same
tuple type reused as a `params` array). **Proven cap:** hand-authored literals in a CheatCore debug
scenario builder — every call site types its own hp value directly in source; nothing here reads
`ContentScale`/`PowerLadder`, and the values are fixed at the file's own literals (today, all ≤ 8000).

### 3.4 One-time, dev-only species-generation input

`Demons/Generation/DemonSpeciesGenerator.cs:7` (`CapturedTypeSeed.HpBase`). **Proven cap:** per the
file's own doc comment, this feeds a **dev-time generation pass** run once against captured vanilla
PvZ Fusion data — *"the emitted C# is committed so a fresh install needs no game data
(gameless-first)."* The base game's own zombie/plant HP values are modest (hundreds, per
`SimDefaults.ZombieHp = 270` in §2.6), and the output of this pass is committed source, never
re-scaled by Θ at runtime. Same boundary reasoning as §3.1 — sourced from the unmodifiable base game,
not from an RPG-scaled channel.

### 3.5 AI utility scores — explicitly clamped to per-mille by their own arithmetic

`Core/World/Ai/ValueMap.cs:10` (`ValueWeights.Yield = 1000`). **Proven cap:** the record's own doc
comment: *"How much of each axis a policy cares about. **Per-mille**; only the ratios matter."* A
policy weight, not a resource magnitude.

`Core/World/Ai/ValueMap.cs:22` (`SectorValue.Yield`) and `:136` (`YieldOf`, the function that produces
it). **Proven cap:** `YieldOf` returns `(int)Math.Min(1000, total / Math.Max(1,
believed.Slots.Count))` (`ValueMap.cs:152`) — an explicit clamp to `[0, 1000]` in the same function
the audit flagged. `SectorValue`'s other five per-mille axes (`Strategic`, `Defensibility`, `Cost`,
`Risk`, `Curiosity`) are produced by sibling functions with the identical shape (`Math.Min(1000, ...)`
/ `Math.Max(0, 1000 - ...)`), which is why `SectorValue.Total`/`Overextension`/`HabitabilityPenalty`
— the values actually derived by *summing weighted axes* — are already `long` in the same record.
This is existing, correct design: the six axis components are bounded by construction, and only their
weighted combination needed widening, which it already got.

### 3.6 A retention tail, not a magnitude

`Data/Policies/SealedCompactionPolicy.cs:8` (`SoulRetainTailPerPlayer = 5_000`). **Proven cap:** the
class's own doc comment: *"Sealed hot-path retain limits and snapshot schema versions."* This is a
**compaction retention depth** — how many soul-ledger records per player survive a compaction pass —
not a souls magnitude. Retention tails are the explicit structural exemption named in CLAUDE.md's caps
table (*"per-frame/runtime caps, retention tails"*) and in SSOT §11's exempt categories. Sits alongside
`ActivityRetainTail = 10_000` and `XpRetainTailPerActor = 5_000` in the same class, both the same
shape and neither flagged (their names don't contain a magnitude word) — recorded here so a future
sweep doesn't waste time re-deriving the same verdict for its two siblings.

---

## 4. Verification

```text
python scripts/audit-overflow.py                    # A3=75 (was 92), 0 critical
python scripts/audit-overflow.py --targets A3 | wc -l   # 75
```

75 = 56 (§2, LADDER) + 19 (§3, BOUNDED). Every finding in the original 92-line baseline is accounted
for in exactly one of §1 (regex fix, 17), §2 (LADDER, 56), or §3 (BOUNDED, 19).

**Consumed by:** P0.4 widens every §2 site to `long` (or deletes, for the one dead-code exception in
§2.5), touches nothing in §3, and additionally fixes SSOT §11.2a's three narrowing casts
(`EffectBag.cs:707`, `EventDrain.cs:458,475` — out of this triage's scope, already named directly in
power-todo.md).
