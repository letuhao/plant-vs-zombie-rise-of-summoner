# Spec: ui-attach-point (E41)

Module **E41** in the [atom effect map](../effect-atom-map.md) §13 (Wave 8). **Independent** — no
dependencies (map §14). Ideal: [effect-atom-ideal.md](../effect-atom-ideal.md) §W8.4 row 7.

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit.
> Where this spec and the definitions disagree, **the definitions win**.

## Objective

Every other Wave 8 row changes what happens; this one changes what the player *knows* happened. There
is **no UI attach point of any kind** — the five are Stat, Resource, Status, Shield, Board
(`AtomKind.cs:7-14`) — so content can make a number happen and cannot make it visible. E41 adds **a new,
read-only** point (one of two Wave 8 adds — E35's `Match` is the other, and the map leaves their order
free, so neither is "the sixth"): show a number, flash a banner, drive a meter. A HUD shows state; it never
owns it, and that is the whole boundary of this module.

## 1. What exists today

### Built — a read-only present path already exists, with no atom entry

| Fact | Where |
|---|---|
| `DamageFxDto` — *"GUI-only present. Not an FA opcode; never writes HP."* Carries `TargetPtr`, `Amount`, `Tag`, `Fx`, `MergedCount`, optional elements | `src/FusionRpg.Contracts/DamageFxDtos.cs:18-29` |
| `DamageFxTag` — a closed set of eleven presentation tags (Heal, Weak, Resist, Null, Absorb, Reflect, Dodge, Crit, Penetrate, Block, Neutral) | `DamageFxDtos.cs:3-16` |
| `IDamageFxSink` plus a no-op and a recording implementation for tests | `src/FusionRpg.Core/Effects/DamageFx.cs:5-20` |
| The injector adapter that turns a `DamageFxDto` into a Unity present | `src/FusionRpg.Injector/Fx/DamageFxCueAdapter.cs:11-17` |
| A per-actor HUD: snapshot, composer, cache with a dirty set, wire serializer, tuning | `src/FusionRpg.Core/Hud/` and `src/FusionRpg.Injector/Hud/ActorHudBuilder.cs:17-60` |
| HUD tuning is already a data file | `data/tuning/actor-hud.v1.json` |

### Wiring gap

| Gap | Where |
|---|---|
| `ActorHudResources.Meters` is declared **and serialized to the wire** and **has no producer anywhere** — `ActorHudComposer` never fills it | declared `src/FusionRpg.Core/Hud/ActorHudSnapshot.cs:19`, `:33`; serialized `ActorHudWireSerializer.cs:48`; producers: none |
| `IDamageFxSink` is reachable only from the combat path; no kind, no plan item, no opcode addresses it | `EffectActions` lists eleven opcodes and no present (`src/FusionRpg.Contracts/EffectDtos.cs:22-44`) |

`Meters` is the exact failure E1's code-or-data rule names — *"a row that no code consumes is not
content"*, the `status.expose.*` shape — except inverted: a **consumer with no producer**. E41 becomes
its first producer, which is the cheapest possible way to add a HUD channel because the wire and the
cache already carry it.

### Real gap

| Gap | Note |
|---|---|
| No UI attach point exists; `AttachPointCount` reads `5` today and is a guarded constant | `src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs:16` |
| Health-bar visibility (`board.showPlantHealth`, `board.showZombieHealth`, `docs/research/stat-fields.md` §"Board-level numbers") has **no writer in `src/`** — a grep finds none | Deferred out of v1, §2d |

## 2. The contract

### 2a. The new attach point

**E41 adds one attach point and one kind.** `AttachPointCount` (`AtomKindRegistry.cs:16`, `5` today)
gains **+1**; `KindCount` (`:18`, `12` today) gains **+1** for `ui.present`. See the count hazard in
§6 — E35 `match-modify` adds `Match` on the same constant.

> **⛔ CORRECTED 2026-09-03 — this said *"becomes 6"*, §4 said *"exactly 6 (or 7 with E35)"*, and
> no section mentioned `KindCount` at all** — though `ui.present` is a kind and moves it. `6` is only
> true if E41 lands before E35, and the hedged *"(or 7 with E35)"* is a test that cannot be written.
> **Both claims are deltas now**, and the Wave 8 end state is stated once, in
> `spec-match-modify.md` §2.1: `AttachPointCount = 7`, `KindCount = 16`, from `5` and `12` today.

**The attach-point ADR row is E35's to create, and E41's to amend.** `AtomKind.cs:4` says the list is
*"Five, guarded by ADR"* and `decisions.md` has **no such row** — `grep -in "attach"` returns
nothing. `spec-match-modify.md` §2.1 writes it; E41 adds `Ui` to it when it lands, in the same change.
If E41 lands first, E41 creates the row on the same terms.

**`Ui` is read-only by construction, not by convention.** A kind on this attach point may not appear in
any `EffectActionPlanItem` that reaches `InjectorEffectActionSink`'s stat, resource, status, shield or
board arms; it has its own sink, and that separation is what a guard test asserts.

### 2b. The kind — `ui.present`

| | |
|---|---|
| Attach point | `Ui` |
| Params | `op` (`String`, required: `number` \| `banner` \| `meter`) · `amount` (`Value`, `number` only) · `tag` (`String`, one of the eleven `DamageFxTag` names, lowercased) · `bannerId` (`String`, `banner` only) · `meterId` (`String`, `meter` only) · `ratio` (`Value`, per-mille 0–1000, `meter` only) · `durationMs` (`Int`, optional) |
| Triggers | the full event set (`AllTriggers`, `AtomKindRegistry.cs:27-29`) — a present is a reaction to something happening |
| Runtime matrix | `(Lawn: Full, Battle: None, Sim: None)`. Battle has no present sink today; record it **pending**, never `never` (E1's living-table rule) |
| Power categories | `PowerCategory.None` (`AtomKind.cs:111`) — **this requires amending a shipped test, see §2b.1** |
| Executor | `op: number` → `IDamageFxSink.Show`. `op: meter` → an `ActorHudMeter` on the target's snapshot, then `ActorHudCache.MarkDirty`. `op: banner` → a match-scoped present, same sink, no target ptr |

### 2b.1. `PowerCategory.None` cannot pass `AtomKindRegistryTests` as it stands

> **⛔ ADDED 2026-09-03 — the spec chose a shape a shipped test forbids and never named the test.**

`Every_kind_declares_a_runtime_a_trigger_and_a_power_category`
(`tests/FusionRpg.Core.Tests/Atoms/AtomKindRegistryTests.cs:36-73`) walks every registered kind and
ends with:

```csharp
Assert.True(kind.Categories != PowerCategory.None, $"{kind.KindId} prices to no category");  // :71
```

`ui.present` with `PowerCategory.None` goes red at `:71` — *"ui.present prices to no category"* — the
moment it registers. The assertion has no exemption list, unlike the trigger check two lines above it.

**The amendment, stated exactly:** the test gains a `cosmetic` exemption set alongside the existing
`permanentModifiers` one at `:53`, holding `{ "ui.present" }`, and the `:71` assertion becomes
conditional on it — a cosmetic kind asserts `Categories == PowerCategory.None`, every other kind
asserts the opposite. That keeps the guard's meaning in both directions: a kind cannot quietly price
to nothing, and a cosmetic kind cannot quietly acquire a category it would then be budgeted for.
The comment beside the set says why `ui.present` is there — a present writes no state (§3), so a
power category on it would put a floater into a container's budget.

**Not an alternative:** giving `ui.present` a category to keep the test green. §2c prices it at
**exactly zero with verdict `Priced`**, and a category with a zero coefficient is a different claim
from no category — it says the atom contributes to that axis and happens to contribute nothing,
which is the reading a later coefficient edit would silently turn into a real number.

This is a deliberate edit to a guard, in the same commit as the kind, said out loud in the commit
message.

**`bannerId`, never free text.** A content-authored string on screen is unlocalisable, unreviewable and
a place for text nobody approved to appear in a screenshot. Banner ids resolve against a table in
`data/tuning/actor-hud.v1.json`; an unknown id is refused at load by E29's value guard.

`tag` reuses `DamageFxTag` rather than inventing a palette — the colours already exist
(`DamageFxPalette.Rgb`, `src/FusionRpg.Core/Effects/DamageFx.cs:23-35`).

### 2c. Pricing — zero, explicitly, never missing

A cosmetic atom is worth nothing, and **"unpriced" is not "zero"**: `CoefficientTable.Find` falls back to
the kind's channel-less row and a null result must be treated as unpriced, not free
(`CoefficientTable.cs:70-82`). So `ui.present` ships with an **authored coefficient row whose
coefficient is 0**, and a test asserts the atom prices at exactly zero with verdict `Priced` — not
`unpriced`, and not a fallback.

That matters in both directions: a UI atom must not become a free rider that inflates a container's
apparent budget, and it must not tax one either.

### 2d. Out of v1, with reasons

- **Health-bar toggles.** `board.showPlantHealth` / `showZombieHealth` are documented board fields with
  **no writer in this repo**. Adding one is a Unity write path, which is a different review than a
  read-only present. Named, not silently dropped.
- **Battle.** No present sink exists there. `RuntimeState.None`, marked pending.
- **Anything that reads back.** A `ui.*` atom never returns a value to content. See §3.

## 3. What it must NOT do

- **Never write state.** No stat, resource, status, shield, board cell, currency or Unity combat field.
  A guard test asserts no `Ui`-attached kind produces a plan item any state executor handles.
- **Never read state back into content.** A HUD is an output. A predicate that reads what the HUD shows
  would make presentation load-bearing, and the first frame it lags the game desyncs the content.
- **No free text on screen.** `bannerId` only (§2b).
- **Do not make it a second HUD.** `ActorHudBuilder` / `ActorHudCache` own composition; E41 contributes a
  meter and marks dirty. Building a parallel overlay is how two HUDs disagree.
- **Do not put a present on the per-hit path uncached.** The 2026-08 perf audit blamed uncached per-hit
  resolves for combat lag; a `ui.present` on `OnDamageDealt` fires at combat frequency, so it goes
  through the same merge/throttle the damage floater already uses (`DamageFxDto.MergedCount`,
  `DamageFxDtos.cs:25`).
- **`long` for any magnitude it displays, never `float`** — a displayed number is a magnitude that has
  already overflowed if it was ever a `float`. Widen before multiplying; **divide by 1000 exactly once,
  last** for `ratio`; **overflow throws**.
- **No hard ceiling.** No cap on a displayed number — a capped display lies about a magnitude the game
  is actually using, which is worse than a long string. `ratio`'s 0–1000 bound is a **bounded ratio**
  and exempt; the meter-count limit is **structural** (a fixed HUD row) — both must say so in a comment.
  Throttles and durations a balance or feel pass would change live in `data/tuning/actor-hud.v1.json`,
  never as literals.

## 4. Testing strategy

| Case | Expect |
|---|---|
| `ui.present{op:number, amount:{min:250,max:250}, tag:"crit"}` on `OnDamageDealt` | one `DamageFxDto` with `Amount = 250` and `Tag = Crit`, via `RecordingDamageFxSink` (`DamageFx.cs:16-20`) |
| **Planted violation:** make a `Ui` kind emit a `ModifyStat` plan item | the read-only guard test fails. This is the module's central invariant and must fail loudly |
| **Planted violation:** remove `ui.present`'s coefficient row | the pricing test fails with `unpriced`, never quietly falling back to the channel-less row |
| `ui.present` prices | exactly zero, verdict `Priced` |
| `op:meter` | `ActorHudResources.Meters` is non-null for that ptr, and the wire serializer emits it (`ActorHudWireSerializer.cs:48`) — the first producer that path has ever had |
| `bannerId: "not-a-banner"` | `BadParamValue` at load |
| `op:number` without `amount`, or `op:meter` without `ratio` | `MissingParam` |
| `ratio: 1500` | `BadParamValue` — bounded 0–1000 |
| `ui.present` bound in Battle | `RuntimeUnsupported` at bind |
| `AttachPointCount == Enum.GetValues<AttachPoint>().Length`, both one higher than before this module | pass; `AtomKindRegistryTests.cs:23` is a self-consistency check, so the test asserts the **delta**, never a literal |
| `KindCount == AtomKindRegistry.All.Count`, both one higher than before this module | pass (`AtomKindRegistryTests.cs:22`) |
| `Every_kind_declares_a_runtime_a_trigger_and_a_power_category` (`AtomKindRegistryTests.cs:36-73`) | **green with the `cosmetic` exemption added** (§2b.1). Unamended it goes red at `:71`; the amendment is deliberate and named in the commit |

**The injector is not built by CI** (`.github/workflows/ci.yml:75-103` — ten test projects, no injector
build). Everything above except the Unity present asserts in `FusionRpg.Core.Tests` against
`RecordingDamageFxSink` and a fake HUD cache; the adapter itself is confirmed by an owner-run lawn look,
because a present is a thing you verify with your eyes — the VFX program's own recorded lesson.

## 5. Acceptance criteria

1. `AttachPoint.Ui` exists; `AttachPointCount` and `KindCount` are each **one higher than before this
   module** and equal what the registry builds; `AtomKindRegistryTests.cs:71`'s power-category
   assertion is amended with a `cosmetic` exemption holding `ui.present` (§2b.1); and the
   `decisions.md` attach-point row names `Ui` — the row itself is created by whichever of E35/E41
   lands first, because there is none today. Every guard edit is deliberate and named in the commit.
2. `ui.present` is registered with the params in §2b and its full param set is refused when malformed.
3. A `Ui`-attached kind cannot produce a state-writing plan item, asserted by a guard test.
4. `op:number` shows a floater on the lawn with the authored amount and tag.
5. `op:meter` fills `ActorHudResources.Meters` and reaches the wire — the first producer for a field that
   is declared and serialized and has never been written.
6. `op:banner` resolves through `data/tuning/actor-hud.v1.json`; no free text ever reaches the screen.
7. `ui.present` prices at exactly zero with verdict `Priced`, never `unpriced`.
8. Nothing in the module reads HUD state back into content, and no cap truncates a displayed number.

## 6. Dependencies and cross-program hazards

| Item | Detail |
|---|---|
| **E35 `match-modify` — the count collision** | Both modules increment `AttachPointCount` (`AtomKindRegistry.cs:16`) and `KindCount` (`:18`). Whichever lands second must edit the guard to the combined value **and say so in its commit** — the guard exists so growth is noticed, and two modules quietly bumping it in sequence is how it stops noticing. Map §14 has them independent, so either order is legal |
| **E37 `projectile-control`** | Also adds a kind (`bullet.modify`). Same `KindCount` coordination |
| **VFX program** | E41 emits through the same present path VFX v3 owns. A new floater source changes what an open blind-identity trial sees — sequence them, and trust the owner's eyes over event telemetry (the VFX program's own recorded lesson) |
| **actor-hud program** | Owns `ActorHudSnapshot` and its wire shape. E41 produces into `Meters`; it must not change the record or the serializer |
| **Perf** | A present on a combat-frequency trigger is the one way this read-only module can cost frames. Merge and throttle through the existing floater path; re-probe if a scenario shows heat |
| **`AtomImporter` staleness** | E41 is registry **code**; the importer reports "nothing changed" because its hash covers seed data. State it in the rollout note |
| **Stale instances** | A `catalog_revision` bump makes every rolled `effect_instance` unbindable (`StaleInstance`) |
