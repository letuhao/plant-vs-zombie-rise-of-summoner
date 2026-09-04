# Spec: timeline-tunables

Module id `timeline-tunables` (T14) in the [battle timeline map](../battle-timeline-map.md). Depends
on nothing built; sequenced after the reconciliation pass's document repairs so it inherits a correct
`RulesetVersion` history. **Written 2026-09-04** as part of bringing the battle engine to current repo
standards.

## Objective

The kernel was specced 2026-08-21. [tunables-ssot.md](../tunables-ssot.md) landed 2026-08-24 and made
the balance surface data. The kernel's numbers were never triaged against it — `battle.v1.json` has a
`ruleset` section and a `traits` section and **no timeline section at all**, while
`BattleModeProfileCatalog` and `DerivedTurnChannels` hold values in code.

This module triages every kernel number into exactly one of two outcomes: **published to
`battle.v{n}.json`**, or **kept as a `const` that says in a comment why it is not tunable**. There is
no third outcome, and "leave it and move on" is not one of them.

**`audit-magic-numbers.py` does not flag any of these** (M1 = 0, repo-wide). This module is therefore
a judgment call under the standard, not a violation being repaired — which is exactly why it needs a
spec rather than a sweep.

## The test this module applies

From [tunables-ssot.md](../tunables-ssot.md), unchanged and not reinterpreted:

> **Would a balance pass ever want to change this number?** If yes it is a **tunable**. If changing it
> breaks *whether the system works* rather than *how the game feels*, it is **structural**.

Applied to the kernel, that test cuts in an unobvious place, and stating where is most of this
module's value.

## Design

### 1. The `BaseSpeed` split — the one genuine finding

`DerivedTurnChannels.BaseSpeed = 100` serves **two roles**, and they land on opposite sides of the
test. Publishing or keeping it whole gets one of them wrong either way.

| Role | Where it is used | Verdict |
|---|---|---|
| **The readiness formula's scale unit** | `TurnReadiness.TicksFor` — `remainingWork × BaseSpeed / rate`, and `OneTurnWork = BaseSpeed` so `TicksFor(BaseSpeed, BaseSpeed) == BaseSpeed` | **Structural.** It is the resolution at which a turn is measured. Changing it rescales work and rate together and cancels — it does not make anything faster or slower, it changes tick granularity |
| **The default value of the `turn.speed` channel** | `DerivedStatRegistry.cs:110` registers `turn.speed` with `BaseSpeed` as its base | **Tunable.** "How fast is a baseline actor" is the first number a balance pass reaches for |

> ### ⛔ Corrected during B28's build (2026-09-04) — `defaultSpeed`'s home is derived-stats, not battle
>
> This spec originally said the published key would be `battle.v2.json`'s `timeline.defaultSpeed`, and
> gave a `publish.py` command line for it. **Both were wrong, and the code said so:**
>
> 1. **`publish.py` cannot create a key.** Its `set` path "refuses to invent a key by design" (its own
>    docstring) — a new section is a *schema* change, authored as part of the module, and only later
>    balance edits go through the tool.
> 2. **Battle tuning is the wrong domain for it.** `DerivedStatRegistry` reads this value at
>    registration, which is byte-for-byte the role `categoryResistCap` already plays from
>    `derived-stats.v{n}.json` — whose own `_meta` describes this exact kind of change: *"DerivedStatRegistry
>    reads it at registration … this module moves the cap's home, it does not move the number."*
> 3. **The decisive evidence.** Every consumer that builds a registry already calls
>    `DerivedStatPolicy.Configure`, but several tools deliberately never configure `BattleTuningHub` —
>    `tools/ProveAptitude/Program.cs` says so in as many words ("needs no BattleTuningHub.Configure").
>    Sourcing the value from battle tuning would have broken them.
>
> **Shipped as** `derived-stats.v2.json`'s `turnDefaultSpeed`. §3's config shape below covers the
> profile magnitudes (B29), which *are* battle numbers read by `BattleModeProfileCatalog` and stay in
> `battle.v{n}.json`.

**Resolution:** split the constant by role. A structural `TurnReadiness.SpeedScale` (documented as the
formula's unit, PS-8 exempt as a scale factor, not a ceiling), and a published
`timeline.defaultSpeed` that the registry reads for the channel base. They hold the same value today,
so the split is byte-identical on day one — the point is that the next balance pass can move one
without the other.

### 2. The triage table

Every number under `Battle/Timeline/`, with its ruling and its evidence.

| Number | Location | Ruling |
|---|---|---|
| `BaseSpeed` as formula unit | `TurnReadiness` | **Structural** — see §1. Needs the comment it does not have today |
| `BaseSpeed` as `turn.speed` base | `DerivedStatRegistry.cs:110` | **Tunable** → `timeline.defaultSpeed` |
| `NominalHasteMilli = 1000` | `DerivedTurnChannels` | **Structural** — it is the definition of "per-mille nominal", the same 1000 that means 1.0 everywhere else in the repo. Moving it would not rebalance haste, it would redefine the unit. Needs the comment |
| `ReactionLane.DepthLimit = 3` | `ReactionLane.cs:52` | **Structural — already correct.** Carries a full comment citing `tunables-ssot.md` §1's own recursion-depth example. **Do not touch it**; it is the model for what the others should look like |
| `CooldownMath.MinTicksFloor = 1` | `CooldownMath.cs:27` | **Structural — already correct.** Names itself PS-8 exempt with the reason (a zero-tick cooldown is an infinite loop, not a balance outcome) |
| `TimelineDrive.MaxPopPerPass = 256` | `TimelineDrive.cs:55` | **Structural — already correct.** Per-frame runtime cap, explicitly exempt in `tunables-ssot.md` §1 and in `spec-injector-kernel-drive.md` §5 |
| `DeltaTickAdvance.MicrosPerTick = 1000` | `DeltaTickAdvance.cs:34` | **Structural — already correct.** The tick unit itself; changing it is `spec-virtual-time-core.md`'s named **Ask first** |
| Profile `W`, `WReact`, `PassQuantum` | `BattleModeProfileCatalog` × 3 | **Tunable** → `timeline.profiles.<id>.*` — but for `W` this is the **default**, not the whole answer. See §2a |
| `ActionPointsEconomy(maxPoints: 2)` | `BattleModeProfileCatalog:100` | **Tunable** → `timeline.profiles.hybrid-atb.maxPoints`. A per-round action budget is the definition of a feel number |
| `AdvancePolicy`, `WScope`, `DefaultCommitment`, `Economy` type, profile ids | `BattleModeProfileCatalog` | **Structural.** These are *which mechanism runs*, not how much of it. The map's own acceptance — "adding a mode adds a row, never a branch" — is about the record shape, and a row of structure in code is still a row |
| `InterruptCooldownMilli = 1000` default | `ActionEnvelope.cs:128` | **Per-action content, not a kernel tunable.** It is a default on a record whose real values come from `rpg_action`. Out of scope here; it belongs to the action corpus |

**Nine already-correct or out-of-scope rows, four real moves.** Stating the ones that need nothing is
deliberate — it is what stops the next pass from re-litigating them.

### 2a. `W` is per-wave content — owner decision, 2026-09-04

The ideal's open question 3 ("is `W` content-configurable per encounter, or fixed per profile?")
closed in favour of **per wave**. `decisions.md`, *Battle engine open questions*.

That makes `W` **two things**, and the split mirrors §1's:

| | Where it lives | Why |
|---|---|---|
| **The profile's default `W`** | `timeline.profiles.<id>.w` — this module | What a wave gets when it says nothing. A balance dial |
| **A wave's own `W`** | `WaveCatalog` — **T15's**, not this module's | Content. "This boss fight is strictly serialized" is a design statement about one encounter |

**This module ships the default only.** It must not add a field to `WaveCatalog` — that is content
schema and it rides with the profile migration, where the same lookup already resolves
`WaveCatalog.Get(waveId).Profile`. Doing it here would put a content field in a tuning module and
give `W` two owners.

**The consequence to accept honestly:** per-wave `W` widens the test matrix, and that was the known
cost of the decision. Every wave becomes a scheduling variant, so T15 carries a matrix test rather
than a single case — stated so the widening is budgeted rather than discovered.

### 3. The config shape

A new `timeline` section in `battle.v2.json`, published by the standard tool:

```json
"timeline": {
  "defaultSpeed": 100,
  "profiles": {
    "classic-round": { "w": 1, "wReact": 0, "passQuantum": 1 },
    "galaxy-sync":   { "w": 2, "wReact": 0, "passQuantum": 1 },
    "hybrid-atb":    { "w": 4, "wReact": 0, "passQuantum": 1, "maxPoints": 2 }
  }
}
```

**These are read from `BattleModeProfileCatalog.cs:67-100`, not chosen.** `wReact` is 0 and
`rendezvousEnabled` false on all three today — they are record defaults no profile overrides, and
this module publishes them at their current values rather than turning anything on. The structural
fields stay in code and are listed here only so the reader is not left wondering where they went:
`classic-round` and `galaxy-sync` are `NextEvent`/`LateBound`; `hybrid-atb` is
`FixedIncrement`/`EarlyBoundWithFallback`, and `galaxy-sync` alone scopes `W` `PerSide`.

Values are **read from the shipped code at authoring time, not chosen** — the same discipline
`battle.v1.json`'s own `_meta` records: *"Extracted byte-identical."* Any value in this file that
differs from what `BattleModeProfileCatalog` holds today is a bug in this module.

## Commands

```powershell
python tools\tuning\publish.py battle timeline.defaultSpeed=100 ...   # never hand-edit
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Timeline"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle"
python scripts\audit-magic-numbers.py --summary
python scripts\audit-overflow.py
```

## Structure

```
data/tuning/battle.v2.json                              (new timeline section; v1 stays for revert)
src/FusionRpg.Core/Battle/BattleTuning.cs               (bind the timeline section)
src/FusionRpg.Core/Battle/Timeline/DerivedTurnChannels.cs   (split BaseSpeed; add the two comments)
src/FusionRpg.Core/Battle/Timeline/TurnReadiness.cs     (SpeedScale, structural, commented)
src/FusionRpg.Core/Battle/Timeline/BattleModeProfile.cs (profiles read magnitudes from tuning)
tests/FusionRpg.Core.Tests/Battle/Timeline/             (byte-identity + binding tests)
```

## Testing strategy

1. **Byte-identity is the acceptance, not a nice-to-have.** All eight goldens (four battle, four
   expedition) unchanged, `RulesetVersion` stays **4**. This module moves values between code and
   config without changing one, so a moved golden is a defect in the module — not a re-bless.
2. **A binding test per published key**: the value the profile carries at runtime equals the value in
   `battle.v2.json`, so a key that silently stops being read fails.
3. **A "config is loaded" negative**: with the tuning hub unconfigured, the profile catalog throws
   rather than falling back to a hardcoded default — the same shape every other `*.Configure` hub
   already enforces. A silent fallback would make the whole module cosmetic.
4. `audit-magic-numbers.py` M1 stays **0**; `audit-overflow.py` A1/A2 stay **clean**.
5. The four boundary guards stay green.

## Boundaries

- **Always:** publish through `tools/tuning/publish.py`; keep the old version on disk as the revert
  target; give every retained `const` the comment the standard requires.
- **Ask first:** changing any published value away from what the code holds today — that is a balance
  change wearing this module's clothes, and it needs a golden re-bless and its own reasoning.
- **Never:** hand-edit `battle.v*.json`; introduce a hardcoded fallback for a published key; move
  `MicrosPerTick`, `MinTicksFloor`, `MaxPopPerPass` or `DepthLimit` into config — all four are
  structural and three already say so correctly.

## Success criteria

1. Every number under `Battle/Timeline/` is either published or carries a comment saying why it is
   not. 2. `BaseSpeed`'s two roles are separated, and the separation is byte-identical today.
3. Eight goldens unchanged and `RulesetVersion` still 4. 4. A balance pass can change baseline speed,
   any profile's `W`, and `hybrid-atb`'s action budget by editing config and restarting — no rebuild.
5. M1 = 0 and A1/A2 clean, unchanged from the pre-module baseline.
