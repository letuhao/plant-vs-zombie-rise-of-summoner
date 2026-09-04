# Power-coefficient sweep — 2026-09-05

**Module:** E44 `power-sweep` (`docs/architecture/effect-atom/spec-power-sweep.md`), acceptance
criteria 1 ("coefficients fitted from a recorded, reproducible sweep, replacing the flat 1000s")
and 3 ("each coefficient traces to its sweep run").

**Tool:** [`scripts/sweep-power-coefficients.py`](../../../scripts/sweep-power-coefficients.py) —
run it to reproduce every number below (`python scripts/sweep-power-coefficients.py`, repo root).

**Method, in one line:** load the real E43 `family-expand` corpus, take the median magnitude of
each channel's `flat`-op family (the one op class `ReferenceScale`'s own contract — "what one RAW
unit means for this channel" — actually governs), and fit `CoeffMilli` so that median-tier content
prices to the same reference value (1000 pts) across channels.

---

## 1. Inputs

- **Corpus:** `data/seed/atoms/generated/family-expand.g-armour.json` (10 rows),
  `.g-attack.json` (15 rows), `.g-life.json` (20 rows) — **45 atoms total, read and counted
  directly**, not trusted from any other document's figure. This is E43's entire real-content
  output as of this date; no other file under `data/seed/atoms/` carries any `power` value to fit
  against (confirmed by grepping the whole tree for a `"power"` key — zero matches anywhere in
  `data/seed`), so this is also the *only* real fitting corpus available.
- **Channels the corpus touches:** exactly 4 of `CoefficientTable.Authored()`'s 20 rows —
  `stat.modify` / `atk`, `defense`, `hp`, `maxHp`. All 45 atoms are `stat.modify`; nothing in the
  corpus touches `arm1`/`arm2`/`resource.*`/`status.*`/`shield.grant`/`spawn.entity`/board or grid
  kinds/`box.set`.
- **Pricing formula reproduced exactly:** `CostFunction.PriceForChannel`'s own arithmetic
  (`src/FusionRpg.Core/Effects/Atoms/Power/CostFunction.cs:85-117`) — `normalizedMilli =
  round(magnitude × 1000 / referenceScale)`, `points = round(normalizedMilli × coeffMilli / 1000)`,
  one rounding point, round-half-away-from-zero (`PowerMath.DivRound`,
  `src/FusionRpg.Core/Effects/Atoms/Power/PowerVector.cs:147-154`). Reimplemented in the sweep
  script in pure integer Python, matching the C# integer contract (no float anywhere).

## 2. What the corpus actually contains

Each of the three generated files holds 3 tiers-of-5 families sharing one channel, one family per
`op`:

| Channel | `flat` family (raw units) | `increased` family (%) | `more` family (%) |
|---|---|---|---|
| `atk` | `atom.might`: 3/5/9/16/28 | `atom.ferocity`: 35/61/107/187/327 | `atom.savagery`: 19/33/58/102/179 |
| `defense` | `atom.warding`: 1/2/4/7/12 | `atom.resilience`: 35/61/107/187/327 | — |
| `hp` | `atom.mending`: 24/42/74/130/228 | — | — |
| `maxHp` | `atom.vitality`: 24/42/74/130/228 | `atom.fortitude`: 35/61/107/187/327 | `atom.bulwark`: 19/33/58/102/179 |

(numbers are the tier-1..tier-5 mean magnitude, `round((min+max)/2)`.)

The `increased`/`more` families share **identical magnitude ranges across every channel they
appear on** (23-47 at tier 1 for every `increased` family regardless of channel; 13-25 for every
`more` family) — confirming these are percentage modifiers, unit-independent of the underlying
stat, while `flat` magnitudes are scaled per channel's own natural raw-unit range (small for
`defense`, larger for `hp`/`maxHp`). This is read directly from the JSON, not inferred.

## 3. The fit

`ReferenceScale` is left at its existing `CoefficientTable.Authored()` value for these four
channels (2, 2, 10, 10 — already a reasonable per-channel unit pick, and the spec's own §2 table
already lists `ReferenceScale` as the dial that "varies", never the flat one). Only `CoeffMilli` — 
the dial spec §2/§4.1 explicitly names as "the flat 1000s" — is fitted:

```
fittedCoeffMilli(channel) = round(1,000,000 / normalizedMilli(medianFlatMagnitude(channel)))
```

`1,000,000` = `TARGET_POINTS(1000) × PowerMath.One(1000)` — pinning one median-tier (`flat`) atom
to 1000 points, the same "one reference unit = 1000 pts" convention already used elsewhere in this
codebase (`RungPowerBudgetTests`' own `referencePower = PowerMath.One`, per
`data/tuning/action-rungs.v2.json`'s own `powerBudgetDerivation` note) — not a number invented for
this sweep. Median (= tier 3, the middle of 5) is used rather than mean for the same reason
`ssot-power-scale.md` §4.3 pins its own curve at a single representative point (`P(20)=680`) rather
than averaging a range no single tier actually is.

| Channel | Median flat magnitude (tier 3) | ReferenceScale (unchanged) | Fitted CoeffMilli |
|---|---|---|---|
| `atk` | 9 | 2 | **222** |
| `defense` | 4 | 2 | **500** |
| `hp` | 74 | 10 | **135** |
| `maxHp` | 74 | 10 | **135** |

Written to `data/seed/power/coefficients.v1.json` as four `stat.modify` rows, each carrying its own
`note` field citing this file, the script, the date, and the exact numbers above — so each
coefficient traces to this run without needing to open a second document (criterion 3).

## 4. Finding 1 — the fit measurably works, for the sub-corpus it can see

At tier 3 (the pin), the four channels' `flat`-op atoms price under the OLD flat-1000 baseline and
the NEW fitted `CoeffMilli`:

| Family (channel) | magnitude | price @ CoeffMilli=1000 (baseline) | price @ fitted CoeffMilli |
|---|---|---|---|
| `atom.might` (atk) | 9 | 4500 | **999** |
| `atom.warding` (defense) | 4 | 2000 | **1000** |
| `atom.mending` (hp) | 74 | 7400 | **999** |
| `atom.vitality` (maxHp) | 74 | 7400 | **999** |

A measured **3.7× cross-channel spread collapses to under 0.1%** — reasonably uniform
power-per-atom across the corpus, for the class of content `ReferenceScale`/`CoeffMilli`'s own key
granularity (kind × channel, no `op` axis) can actually distinguish. Reproducible: re-run the
script, same numbers.

## 5. Finding 2 — a real, structural limit, reported per spec §7 criterion 7 (not fixed here)

`increased`/`more` atoms on these same four channels are **not** brought into line by this fit, and
**cannot be** by any choice of `CoeffMilli`/`ReferenceScale` at this table's current key
granularity — the ratio between a `flat` atom and an `increased`/`more` atom on the same channel is
*scale-invariant*: rescaling either dial moves both op-classes by the same factor.

| Channel | `flat` T1 magnitude | `increased` T1 magnitude | `more` T1 magnitude |
|---|---|---|---|
| `atk` | 3 | 35 (11.7×) | 19 (6.3×) |
| `defense` | 1 | 35 (35×) | — |
| `maxHp` | 24 | 35 (1.5×) | 19 (0.8×) |

`CoefficientTable.Find` keys on `(kindId, channel)` only (`src/FusionRpg.Core/Effects/Atoms/Power/
CoefficientTable.cs:125-132`); `op` is not part of the key, so a `flat` (raw-unit) atom and an
`increased`/`more` (percentage) atom on the same channel share one coefficient row by construction.
Reconciling this needs an `op` axis added to the coefficient key — a `CostFunction`/
`CoefficientTable` lookup-key change, which is a Core/Power code change outside this module's
data-only scope (spec §5: "change `CostFunction`'s integer contract" and the broader instruction to
touch only what a coefficient-data change legitimately requires both point the same direction).
Recorded here so a later module owns it explicitly rather than rediscovering it from scratch.

## 6. Coverage — 16 of 20 rows have no real corpus, and are honestly left unfitted

Only 4 of `CoefficientTable.Authored()`'s 20 channel rows have any real generated content to fit
against today. The other 16 (`arm1`, `arm1Max`, `arm2`, `arm2Max`, `stat.modify` generic,
`stat.derived` generic, `resource.delta`, `resource.economy`, `status.apply`, `status.clear`,
`shield.grant`, `spawn.entity`, `board.action`, `grid.spawn`, `grid.clear`, `box.set`) are left at
their existing authored values, explicitly marked "NOT sweep-fitted, no real corpus" in
`coefficients.v1.json`'s own per-row notes — per spec §5, this sweep does not fit against synthetic
data alone, and inventing numbers for these would be exactly the "third refuted flat number" §3
already warns against.

## 7. A second, independently discovered defect fixed as a corollary

While tracing whether these 16 pass-through rows were actually necessary, direct inspection of the
live `dist/FusionRpg.Server/data/rpg-hot.sqlite` `power_coefficient` table showed **exactly the 14
rows this session's earlier E37/E38/E41 work had added, and none of `CoefficientTable.Authored()`'s
original 20**. `RpgStore.GetPowerTables` (`src/FusionRpg.Data/Sqlite/RpgStore.Power.cs:61-72`) falls
back to `PowerTables.Authored()` only when `power_coefficient` has **zero** rows — once any row is
imported, every Authored() channel the seed file does not also carry becomes silently unpriced in
any DB-backed pricing path (`ActorPowerCache.Compose` skips a missing coefficient rather than
pricing it at a flagged zero, `src/FusionRpg.Core/Effects/Atoms/Power/ActorPowerCache.cs:93-97`).
This was already live and undetected before this sweep — `PowerCoefficientImportTests.cs` tests the
merge mechanism but never asserts that the *other*, untouched Authored() channels still resolve
after an import. The 16 pass-through rows close this: after this change, importing
`coefficients.v1.json` into a fresh database and querying every `(kind, channel)` pair the real
shipped catalog actually uses resolves to a coefficient with **zero missing** (verified directly
against a real import, §8 below).

## 8. Verification run (2026-09-05)

```
dotnet build tools/AtomImporter -c Release                         # builds clean
dotnet run --project tools/AtomImporter -c Release -- \
    --check --validate --db <scratch dir>                          # exit 0, 0 FAIL findings
dotnet run --project tools/AtomImporter -c Release -- \
    --validate --db <scratch dir2>                                 # real import, exit 0
```

Both runs report `power drift: 0 evaluated` (expected — no atom in the seed tree carries a stored
`power` value yet, `data/seed/atoms/generated/*.json` included, so `ContentValidation.Drift` has
nothing to compare against; this is the same pre-existing state
`ContentValidationTests.The_shipped_corpus_has_no_unexplained_power_drift`'s own comment already
documents) and `lint: ... 0 failure(s)` (warnings only, pre-existing orphan-atom warnings for the
E43 corpus, unrelated to this sweep).

Direct SQL against the real import's resulting database confirmed `power_coefficient` now carries
34 rows (14 prior + 20 from this sweep), and every distinct `(kind_id, channel)` pair used by any
atom in the real shipped catalog resolves to a coefficient row (`SELECT DISTINCT kind_id,
json_extract(params_json,'$.channel') FROM effect_atom` cross-checked against `power_coefficient` —
zero misses).

`dotnet test tests/FusionRpg.Core.Tests` (filtered to Power/CostFunction/ContentValidation/
PowerInteraction/ActorPowerTests/RungPowerBudgetTests): **260/260 passing**. Full suite: **6558/6572
passing** — the 14 remaining failures are pre-existing and unrelated (Battle/Demons.StarPolicy/
Expeditions/ClassSystem.ProveAptitude, all failing on an unconfigured `BattleStatComposer`/tuning
bootstrap gap from other uncommitted work this session, none touching Power/Atoms/
ContentValidation). One genuinely related, pre-existing stale assertion was found and fixed:
`EntityFieldsTwelvePlusTests.The_seed_file_carries_exactly_the_twelve_plus_ui_presents_own_row`
hardcoded `coefficients.v1.json`'s row count at 13, which was already wrong before this sweep (the
file already held 14 rows, including E37's `bullet.modify`, uncounted) — updated to 34 alongside
this sweep's own 20 additions.

`FusionRpg.Data.Tests` could not be run: `ContractTuningTestBootstrap.cs` (Data.Tests' own copy)
fails to compile (`CS0103: The name 'DefaultSiege' does not exist`) — a pre-existing, unrelated
break from other uncommitted work this session (a base-defense/siege-tuning bootstrap edit missing
its own field), confirmed via `git status`/`git log` on that file and independent of every file this
sweep touched. `PowerCoefficientImportTests.cs`'s own 8 cases could not be re-run for this reason;
the direct AtomImporter + SQL verification above is the substitute evidence for the same claim
(coefficients import correctly and the fallback gap is closed).
