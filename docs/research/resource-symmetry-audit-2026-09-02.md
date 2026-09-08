# Resource coverage audit — six resources, three layers, three different answers

**Captured 2026-09-02.** Owner-requested audit: *"any derived stats that affect resource need cover 6
resource."* Measured against `DerivedStatChannels.cs`, `DerivedStatRegistry.cs`,
`data/tuning/aptitudes.v2.json` (486 edges) and `DominanceGuard.cs`.

**For the resource-hub / class-system / derived-stats programs.** Deliberately not fixed inside the
action feature — owner, 2026-09-02: *"it need to fix from derived stats and 12 primary stats
distribution."*

---

## The finding in one paragraph

**There are six resources — `hp`, `stamina`, `hunger`, `spirit`, `qi`, `poise`** (`ResourceIds`,
`DerivedStatChannels.cs:510`, poise appended 2026-08-26). The **channel registry is correct**: all three
resource families expand across all six, so 18 channels exist. **Nothing downstream agrees with it.**
Aptitude edges cover 5, 5 and 3 of six; `DominanceGuard`'s hardcoded list covers 4, 4 and 3. **`poise`
has zero aptitude edges anywhere**, so the guard economy has a pool no build can invest in. And
separately, `combat.heal.power` is an HP-only *active restoration* channel with no equivalent for the
other five resources — which is the asymmetry the owner named: *"don't make healing for only 1 resource
and don't support other resources, it is wrong design."*

---

## 1. The audit table

Six resources: `hp` · `stamina` · `hunger` · `spirit` · `qi` · `poise`

| Layer | `resource.max` | `resource.regen` | `resource.efficiency` |
|---|---|---|---|
| **Registered channels** — `DerivedStatRegistry.cs:204`, loops `ResourceIds` | **6 / 6** ✅ | **6 / 6** ✅ | **6 / 6** ✅ |
| **Aptitude edges** — `aptitudes.v2.json` | **5 / 6** — no `poise` | **5 / 6** — no `poise` | **3 / 6** — no `hp`, `spirit`, `poise` |
| **`DominanceGuard` list** — hardcoded, `:85-87` | **4 / 6** — no `hp`, `poise` | **4 / 6** — no `hp`, `poise` | **3 / 6** — no `hp`, `spirit`, `poise` |

**The registry is the only layer that got it right.** It expands generically over `ResourceIds`, so
adding poise to that array gave it channels for free. Every layer that *hand-lists* resources drifted.

### 1.1 The three defects, in severity order

**⛔ D1 — `poise` has zero aptitude edges.** Not in max, not in regen, not in efficiency. `poise` is the
guard economy's pool (`spec-guard-economy.md`), and `spec-poise-resource.md:6` states it **"⛔ Blocks
`guard-economy` completely."** So **Bulwark — whose entire `Role` is guard — has a resource no build can
increase.**

This is **already a named open gap, not a new discovery**: `class-system-todo.md` records the test
`SeededResolve_emitsPoiseRegen` as *"0 on the real tree since no aptitude edge feeds poise yet, P7.2's
own still-open, named gap"*, and `PhaseModel.cs:238` and `Predictor.cs:153` both carry the same comment
in code. **It is tracked and it is still open.**

**⚠️ D2 — `DominanceGuard` hand-lists resources and is missing three.** `hp` and `poise` are absent from
its max and regen lists; `hp`, `spirit` and `poise` from efficiency. **A guard that enumerates by hand
cannot catch a dominance problem on a resource it does not know about** — and it will drift again the
next time a resource is added. It should loop `ResourceIds` the way the registry does.

**⛔ D3 — `resource.efficiency` covers 3 of 6, and this is a DEFECT (owner, 2026-09-02):** *"missing 3
other resources for `resource.efficiency` is a defect not a feature… it make the game only have 3 type
of resource an action can consume, but it is 6 not 3."*

There are **four efficiency edges in the entire game** — Agility→`stamina`, Focus→`hunger`, Focus→`qi`,
Focus→`stamina`. Meanwhile `ActorResourcePools.cs` states *"All six resource pools for one actor"*, so
**an action can cost any of six resources while only three have a stat that reduces that cost.** No
document ever claimed three was correct; the gap is emergent, and nothing stated the invariant that
would have caught it. **That invariant now exists** — the six-coverage rule in
`resource-hub-ssot.md`.

## 2. "We have regen — why do we still need heal?"

The owner's question, answered precisely. **They are not duplicates, but the asymmetry is real.**

| | `resource.regen.hp` | `combat.heal.power` |
|---|---|---|
| What it is | **passive** per-tick pool regeneration | scales an **active** restoration effect at resolution |
| Where it lives | `resource.*` family, generic over all six | `combat.*` family, **HP only** |
| Live consumers | `ExhaustionPolicy.cs:59` (generic over `resourceId`), `Predictor.cs:148` | `OverlayCombatMath.cs:81` |
| Per-resource equivalent | **yes — all six** | **none** |

**So they do not conflict mechanically** — passive drip versus active grant are different things, and a
game normally wants both. **The defect is that only HP has the active half.** An action that restores
stamina, qi, spirit or poise has no channel that scales it, while an action that restores HP has a
dedicated one.

Three facts say the generic mechanism is already there and only the *scaling* channel is missing:

1. **`resource.delta` is already an atom kind** (`AtomKindRegistry.cs:167`) on `AttachPoint.Resource`,
   compiled to `EffectActions.ApplyResourceDelta`. Applying a signed delta to any resource is solved.
2. **There is no healing system to preserve.** `decisions.md`: *"positive = heal — one pipeline, no
   separate heal feature."* Healing is already just a sign on `DamagePacket`.
3. **`StatusEffectBridge.cs:89`** routes heals *"through the SAME dispatch path as any other heal"*.

> **Restated in the owner's terms: HP is a resource, healing is resource generation, and the game
> should have one concept for generating any of the six — not a special case for one.**

### 2.1 A doc-vs-code discrepancy found while checking this

`DerivedStatRegistry.cs:207-209` annotates every resource channel with *"No shipped reader for any
resource id… Action/resource economy unbuilt."* **That is stale.** `ExhaustionPolicy.cs:59` reads
`ResourceRegen(resourceId)` generically, and `Predictor.cs:148,156` reads the `hp` and `poise` members.
The note should say which readers exist, or the `UnitClass` verdict it justifies is resting on a
statement that is no longer true.

## 3. Options

| # | Option | Closes | Cost |
|---|---|---|---|
| **A** | **Add poise aptitude edges** — the minimum that unblocks the guard economy | D1 | Edge rows in `aptitudes.v2.json`; baselines re-blessed. **Decide which aptitudes feed poise** — Bulwark and Fortitude are the obvious owners |
| **B** | **Make `DominanceGuard` loop `ResourceIds`** instead of hand-listing | D2, and prevents recurrence | Small code change; may surface dominance findings that were previously invisible |
| **C** | **Generalise `combat.heal.power` → `resource.restore.{resource}`**, heal becomes the `hp` member | §2's asymmetry | New channel family; `OverlayCombatMath` reads the `hp` member; edges re-pointed; baselines re-blessed |
| **D** | **Give resource generation an owning aptitude** — `combat.heal.power` is currently fed only by Ferocity (k=12000) and Composure (k=8000), whose roles are *"breaks crit-denial"* and *"crit-denial"*. **It is the one combat channel no aptitude owns** | the healer-build gap | Edge rows + one `Role` string. Fortitude (*"mitigation — take less of everything"*) is the closest existing fit |
| **E** | **Add efficiency edges for `hp`, `spirit`, `poise`** — D3 is a defect, so this is a fix, not a documentation task | D3 | Edge rows; **decide which aptitudes own efficiency** — today only Agility and Focus feed it at all |

**A + B are the cheap, obviously-correct pair** — one unblocks a shipped-but-dead economy, the other
stops the whole class of drift. **C + D are the design decisions**, and C is what the owner's framing
actually asks for.

## 4. Blast radius

| Artifact | Effect |
|---|---|
| `data/tuning/aptitudes.v2.json` | edge rows (A, D) |
| `DominanceGuard.cs:85-87` | loop `ResourceIds` (B) |
| `DerivedStatChannels.cs` · `DerivedStatRegistry.cs:189,204` | the generalised family (C) |
| `OverlayCombatMath.cs:81` · `StatusEffectBridge.cs:89` | read the generalised channel (C) |
| `_baseline-residual.json` · `_baseline-dominance.json` · `_baseline-goldens.json` | **re-bless required** for A, C, D |
| `scripts/prove-aptitude.ps1` | re-run — fails on any non-zero per-channel delta |
| `class-system-todo.md` P7.2 | A closes this named gap |

**Not affected:** the action corpus, the atom catalog, the rung table, every closed action vocabulary.
`resource.delta` already exists, so no new effect vocabulary is needed — this is a *scaling channel and
edge coverage* change, not a new mechanic.

## 5. The questions this audit asks

1. **Which aptitudes feed `poise`?** (D1 — blocks `guard-economy`, which is Bulwark's whole role.)
2. **Should `combat.heal.power` generalise to per-resource generation power?** (§2, option C.)
3. **Should one aptitude own resource generation?** (Option D — Fortitude is the closest fit.)
4. ~~Is `resource.efficiency` missing `hp` and `spirit` deliberate?~~ **CLOSED 2026-09-02 — it is a
   defect.** The open part is *which aptitudes should feed efficiency for `hp`, `spirit` and `poise`*,
   since only Agility and Focus feed it at all today.

---

## Appendix — a terminology rule set the same day

Owner, 2026-09-02: *"change the 'sun' in rpg game to hunger, no more misconcept, keep the 'sun' in pvz."*

**The RPG layer never says "sun".** The plant-facing pool is `hunger` at actor scope. "Sun" means the
lawn's match-scoped `pvz.*` bank and nothing else. `resource-hub-ssot.md` §104 already records the
mapping (*"Lawn sun | The plant's `hunger` pool"*); this rule makes the naming one-way so the two can
never be conflated in a doc, a channel id, or a UI string.


---

## 6. ⭐ RESOLVED 2026-09-02 — what was built, and the bug it uncovered

Phase 0 executed. **All 18 (family × resource) cells are now fed**, and closing the gap surfaced a real
arithmetic defect in the shipped resolver.

### 6.1 The tool could not fix the file

`publish.py` refuses to add a key by design — *"refusing to invent a new key (T5 spirit: publish edits
existing tunables, it does not add undocumented ones)"* — and `aptitudes.v{n}.json`'s own `rebalance`
note forbids hand-editing. **So a resource family that was never given a row had no legal way to gain
one.** That is why earlier passes only touched documents.

**Fixed:** `publish.py` gained `--add-edge`, deliberately narrow — it appends one
`{channel, source, kMilli}` and refuses a duplicate `(channel, source)`, an unknown source, a brand-new
channel family, or a malformed spec. All four refusals verified.

### 6.2 ⭐ The real bug — `EffectiveKMilli` truncated where the house rule rounds

Closing the gap made `ResolverMatchesSimulatorTests` fail: `resource.regen.poise`, **core=13 vs
sim=15.88, 22% against a 1.5% tolerance**. Both sides read the same file, so this was Core-vs-POC
divergence, and **it could not be tuned away** — every value large enough to close it reintroduced
unending duel pairs.

Root cause, in `AptitudeResolver.EffectiveKMilli`:

```
Core:  checked(kMilli * scaleMilli) / 1000     integer division - PER EDGE
POC:   kMilli * (scaleMilli / 1000.0)          float - no truncation
```

**With `recovery.scaleMilli = 374`, any edge with `kMilli <= 2` scaled to exactly zero** — a silently
dead edge the POC still honoured. The method's own doc comment already stated the rule it was not
following: *"widen before multiplying, divide by their combined scale once"*, and this repo's per-mille
house rule is round-half-away-from-zero (`effect-atom/definitions.md` §2).

**Fixed:** `EffectiveKMilli` now rounds. Measured improvement against the POC's float model:

| kMilli | truncate err | round err |
|---:|---:|---:|
| 5 | 46.5% | **7.0%** |
| 10 | 19.8% | **7.0%** |
| 21 | 10.9% | **1.9%** |
| ≥30 | 0.1–2% | unchanged |

Large coefficients are untouched; only the small ones that were being discarded change.

### 6.3 The coverage change is balance-neutral

`poise` regen was **solved against the termination invariant, not guessed** — the first attempt
(floor 300, Bulwark 1200) produced **48 unending pairs** against a baseline of 0, because
`Predictor.cs:153` records that poise regen *"resolves to 0 for every actor today"* and the coverage
pass switched the guard economy on for the first time. `spec-guard-economy.md` §4 has the rule:
`r = poiseRegen / peerPressure`, where **`r ≥ 1` is "the same defect the termination invariant names"**.

Final values — `max.poise` Bulwark 28000 / Fortitude 10000 / Retribution 8000 / floor 5000;
`regen.poise` Bulwark 60 / Fortitude 30 / floor 2; `efficiency` sparse (owner + Focus).

| | v2 baseline | v3 |
|---|---|---|
| Dominant corners | 0 | **0** |
| Unending pairs | 0 | **0** |
| All twelve win-counts | — | **unchanged** |

### 6.4 The drift guards that make this unrepeatable

- **`EveryResourceIsFedInEveryResourceFamily`** — asserts coverage, never a coefficient, over
  `ResourceIds`. A seventh resource fails it until fed.
- **`EveryEdgeSourceIsAKnownAptitude`** — catches a typo'd `--add-edge` before a resolve.
- **`DominanceGuard.ReservedFamilies` now derives from `ResourceIds`** instead of hand-listing eleven
  of eighteen. (It is reporting metadata, not an input to the wins math — verified by a control run.)
- **`AptitudeHostInjectionTests` made version-agnostic** — it pinned the literal `aptitudes.v2.json`
  and broke on v3, which is a guard editing itself for no reason on every bump.

### 6.5 State

**Core.Tests 5021/5021 · Guard.Tests 161/161.** Open items from §3: **A, B, D3 and the tool gap are
closed**; **C (generalise `combat.heal.power` → `resource.restore.{resource}`) is approved but not built**,
and **D (which aptitude owns resource generation) is still an open design question.**


---

## 7. 0.8 CLOSED 2026-09-02 — `combat.heal.power` → `resource.restore.{resource}`

**All four resource families now cover all six resources.** `resource.restore` is active restoration; hp keeps
its former coefficients exactly (Ferocity 12000, Composure 8000), so the rename moved no balance.

| Family | hp | stamina | hunger | spirit | qi | poise |
|---|--:|--:|--:|--:|--:|--:|
| `max` | 12 | 12 | 12 | 12 | 12 | 12 |
| `regen` | 12 | 12 | 12 | 12 | 12 | 12 |
| `efficiency` | 2 | 3 | 2 | 2 | 1 | 2 |
| **`gen`** | **2** | **1** | **1** | **2** | **1** | **2** |

**Verdict unchanged from baseline:** 0 dominant corners, 0 unending pairs, **no aptitude's win-count
moved**. Core 5027/5027, Guard 161/161.

### 7.1 Three decisions taken during the work

1. **A retirement shim, not a deletion.** `combat.heal.power` stays **registered with no reader and no
   edges**. Retiring the id outright makes `aptitudes.v1/v2/v3.json` — which are revert points and still
   name it — **unloadable**, which would have deleted `TerminationGuardTests`' deliberate v1 pins. Same
   pattern as `DemonRarity`'s retired four-value ladder. Channel count 261 → **267** (+6 members, +1
   shim, −0).
2. **`gen` vs `regen` is a readability hazard, and it is only that.** `regen` is the passive drip, `gen`
   scales an active grant. Prefix tests are `StartsWith` and neither string is a prefix of the other, so
   `recovery.families = ["resource.regen"]` cannot capture `resource.restore.*`. Recorded in the channel's
   own doc comment.
3. **Only `resource.restore.hp` has a reader** (`OverlayCombatMath`, GameUnits). The other five are
   registered and unread — like max/regen/efficiency — and are reserved in `DominanceGuard`.

### 7.2 ⭐ The systemic problem 0.8 exposed: the version literal was pinned in ~15 places

Publishing v3 and v4 broke a long cascade of tests, and **almost none of them were about the change** —
they had hardcoded `aptitudes.v1.json` or `aptitudes.v2.json` while meaning *"whatever ships"*.

**Fixed to resolve the highest `aptitudes.v*.json`:** `AptitudeMatrixTests`, `DominanceGuardTests`,
`ReaderCensusTests`, `scripts/audit-reader-census.py`, and `AptitudeHostInjectionTests` (which pinned the
literal in a *guard*, so every bump edited a guard for no reason).

**Deliberately left pinned:** `TerminationGuardTests` against v1 — those prove historical facts about a
specific file, which is a different thing from "the current config".

**Still pinned and correct:** the two hosts. `RpgHost.cs`/`Program.cs` naming an exact version *is* the
ship decision — "reverting is pointing this back at v3".

### 7.3 What is still open

**Which aptitude should OWN resource generation.** `resource.restore.hp` is fed by Ferocity (12000) and
Composure (8000) — inherited unchanged from `combat.heal.power`, and both are named for crit. The other
five follow the ownership rule (each resource's `max`/`regen` owner, plus Focus). **So the family is now
internally inconsistent in exactly one place**, which is the original "no aptitude owns healing" finding —
no longer invisible, now a visible one-row anomaly in a table. That remains an owner decision.
