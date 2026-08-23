# Effect atoms — completeness audit

**Status:** audit, 2026-08-23. Read against `src/` and the running suites, not against the specs.
Scope: all 21 rows of [tasks/effect-atom-todo.md](../../../tasks/effect-atom-todo.md), every one marked `[x]`.

Map: [effect-atom-map.md](../effect-atom-map.md) · Definitions: [definitions.md](definitions.md)

---

## 0. What the suites say

Everything is green, and that is why this audit exists — nothing here is a failing test.

| Suite | Result | In CI |
|---|---|---|
| `FusionRpg.Core.Tests` | 2797 passed | ✅ |
| `FusionRpg.Data.Tests` | 423 passed | ✅ |
| `FusionRpg.Guard.Tests` | 61 passed | ✅ |
| `FusionRpg.AtomImporter.Tests` | 10 passed | ✅ |
| `FusionRpg.Server.Tests` | 15 passed | ❌ **not in CI** |
| `FusionRpg.E2E.Tests` | 183 passed | ❌ **not in CI** |
| `guard-dal` / `guard-funnel-delta` / `guard-single-writer` / `guard-secondary-no-unity` | all OK | ✅ |

## 1. The finding, in one paragraph

Twenty-one modules built a kind vocabulary, two schemas, a predicate tree, a compiler, a runner, a
transport, a content hash, a price model and a validator. **Almost none of it is joined to the running
game**, because three links were never built: a **loader** (a host that reads a content table into the
static that Core actually consults), an **importer run** (anything that puts `data/seed/**` into a
database), and a **producer** (anything that creates an `effect_instance` or an `effect_binding`).

Every table falls back to a hardcoded copy in Core. Every push resolves zero bindings. The program's
own recurring defect — *declared, registered, zero readers, dead* — is now the shape of the program
as a whole. The tests pass because they build their own inputs; production has no inputs at all.

This is not a claim that the work is wrong. The vocabulary, the arithmetic and the transport are
sound and well tested. It is a claim that **completeness was measured at the module boundary and
never at the seam**, and that the todo and map read as finished when the layer is inert.

---

## 2. Critical — a module's headline capability does not reach the game

### A1. E17 shipped a parser with no applier

`StatusStatPayload.ToModifiers` and `SourceIdOf` have **zero callers outside their own test file**.

The chain that exists: `StatusEffectBridge.ParseStatMods(overlay)` → `StatusApplyInput.StatMods` →
`StatusInstance.StatMods`. The chain stops there. There is no status stat plugin
(`src/FusionRpg.Core/Stats/Plugins/` holds four, none of them status-aware), nothing composes a live
instance's mods into the bag, and nothing withdraws `status:<instanceId>` on expiry.

So `rally`, `expose`, `command` and `shatter` — the four statuses E17 exists for — **still change no
stat**. The payload is now parsed, validated, rejected on a bad channel, deterministically ordered,
and stored on the instance, and then it is dropped.

This is the same defect the module was written to fix, moved one link down the chain. The todo row
says the key "lands here WITH its consumer (`StatusStatPayload`), never before it: an allowlisted key
nothing reads is the defect, not the fix." A parser is not a consumer. The consumer is whatever turns
a modifier into a composed number, and it does not exist.

**Severity: critical** — it is the one module whose stated deliverable is a behaviour change, and the
behaviour does not change.

### A2. No host loads any content table

| Static Core consults | Loader call site |
|---|---|
| `ElementTable.Current` | `ElementTable.Use(...)` — **tests only** (4 call sites, all `UseScoped`) |
| `PowerTables.Current` | `PowerTables.Use(...)` — **zero call sites anywhere**, including tests |
| `DerivedStatRegistry` caps | reads code constants; `GetChannelPolicies()` has **zero callers** |
| `TraitAtomSource` (E12) | `BattleStatComposer.Traits` defaults to `TraitAtomSource.Shipped()`, a C# literal |

`RpgStore.GetElementTable()` and `GetPowerTables()` both fall back to the shipped/authored code copy
when their table is empty, which is correct and also means an empty table is indistinguishable from a
configured one. Nothing in `src/FusionRpg.Server/Program.cs` touches `ElementTable`, `PowerTables`,
`ImportContent` or channel policy.

Consequence: **editing a roster row, a matrix cell, a coefficient or a policy row changes the content
hash and changes no behaviour.** E18's headline — "a seventh element generates its 12 channels with no
code change" — is true of the mechanism and false of the shipped system.

### A3. The importer is never run

`tools/AtomImporter` appears in CI **only as its own unit tests**. It is not in
[deploy-play.ps1](../../../scripts/deploy-play.ps1), not in a CI step, not at server startup, not in
any runbook step that executes.

So `data/seed/atoms/*.json` (4 files), `data/seed/containers/*.json` (2) and
`data/seed/elements/*.json` (2) reach **no database, ever**. Combined with A2, the content-as-data
pipeline is schema-only end to end: tables are created, registered in the hash at v4, and stay empty.

### A4. Nothing creates an instance or a binding

`Instantiator` has zero production callers. `RpgStore.SaveInstance`, `Bind`, `ListBindings`,
`ClearSessionScopedBindings` and `CountOrphanInstances` have zero production callers.

Therefore `ResolveBindings` returns empty for every owner, `AtomPushService.Build` compiles nothing,
the injector's `AtomPushReceiver` installs nothing, and `AtomRunner` never receives an entry. E6, E7,
E15 and E19 are tested end to end and **unreachable** end to end.

The map does say items are greenfield and out of scope, and that is a defensible boundary. What is
missing is the sentence saying so: nothing in the map, the todo or the checkpoints records that the
runtime has no producer and is inert until another program binds a container.

---

## 3. Important

### B1. `effect_channel_policy` is a hashed table with no reader and no author path

Created (`EnsureChannelPolicySchemaUnlocked`), validated on write, registered in the content hash at
**v4** — and `GetChannelPolicies()` is called by nothing. `DerivedStatRegistry.cs:46-48` still
hardcodes the `0.95` resist cap, which is the exact duplication E16 existed to remove.

It is also **unauthorable**: `SeedScanner.OwnedFolders` is `{atoms, containers, curves, rarity,
elements}` — no channel-policy folder — so the only way to fill the table is a direct
`UpsertChannelPolicies` call, and nothing makes one.

This is precisely the shape E1's code-or-data rule refuses, and the shape E1 was corrected for at E19
(`capPerMatch` refused at load for a counter E15 had already shipped).

### B2. Two sources of truth for every migrated value

| Value | Copy 1 (live) | Copy 2 (inert) |
|---|---|---|
| 16 effect defs | `EffectSeedCatalog.CreateAll()`, 5 production call sites | `data/seed/atoms/fx-*.json` |
| `critical-hunter` +150 crit | `TraitAtomSource.Shipped()`, C# literal | `data/seed/containers/trait-critical-hunter.json` |
| element roster + 2 matrices | `ElementTable.Shipped()`, C# literal | `data/seed/elements/*.json` |
| `ElementTypeId` enum | hand-written | roster rows |

Parity tests pin each pair together, so **drift is caught** — that part is done properly. But
Checkpoint D's success criterion #2 (*"`EffectSeedCatalog` is deleted"*) is unmet, and the "a new
effect costs one row" claim is true only inside the test corpus.

The generator that closes three of these four rows (`tools/ElementEnumGen`, carrying E18's enum mirror
and E11's Step 4) is the one piece of debt both modules already record as owed.

### B3. `AllCombatChannelIds` rebuilds 84 interpolated strings on every read

```csharp
public static IReadOnlyList<string> AllCombatChannelIds =>
    BuildAllCombatChannelIds(ElementTable.Current.Elements.Where(e => e.Enabled).Select(e => e.ElementId));
```

Uncached property, `Where`/`Select`/`ToList`, then 12 × 7 string interpolations into a fresh `List`.
Callers:

- `BattleStatComposer.Compose:19` — builds a `HashSet` from it **per actor composed**. Battle sweeps
  compose thousands of actors.
- `StatusStatPayload.IsKnownChannel:125` — `.Contains(channel)`, an **O(n) scan over a
  freshly-allocated 84-element list**, once per channel per parse.
- `DebugCombatActions.cs:318` — injector debug dump.

The doc comment justifies not caching because "the roster is loaded after startup on a host with a
database" — a load that A2 shows never happens. E13 spent a whole module proving 27 ns/atom on the
predicate path; this sits on the compose path with no budget and no guard.

### B4. E14b runs only inside its own tests

`ContentValidation`, `ContentReport` and `ContentFinding` have zero production callers. There is no
`--validate` on the importer, no CI step, no server endpoint. `--check` runs the import and rolls
back; it does not run budget, drift or lint.

And because `data/seed/rarity/` does not exist, the `rarity` table is empty, so the budget check
evaluates nothing. `ContentReport.Evaluated` was built precisely so an empty pass could not look
green — and then nobody reads the report, because nothing produces one.

### B5. Two suites are outside CI

`FusionRpg.Server.Tests` (15, E19's server half) and `FusionRpg.E2E.Tests` (183, including E8's
content-hash stamps) are not in [ci.yml](../../../.github/workflows/ci.yml). Both pass locally today,
so this is drift-risk rather than rot.

*(Checked and cleared: `tests/FusionRpg.Bench` is an `Exe` and `dotnet test` finds nothing in it —
that is correct and documented. E13's ≤ 50 ns/atom budget is enforced by `AtomBenchGuardTests` inside
`Core.Tests`, which does run in CI.)*

---

## 4. Minor

- **C1.** `ClearSessionScopedBindings` and `CountOrphanInstances` have no callers — housekeeping
  declared, never scheduled. Moot while A4 holds; a leak the moment it does not.
- **C2.** Nothing checks that a status carrying a `stat` overlay declares `ModifyStat`. The shipped
  `blight-row.overlay.json` carries one on a DoT status, and it validates.
- **C3.** `SeedScanner.OwnedFolders` declares `curves` and `rarity`; neither folder exists. Harmless
  (`Where(exists)`), but two hash-covered tables have no authored content at all.
- **C4.** `UpsertPowerTables` and `UpsertChannelPolicies` do not bump `catalog_revision`. Consistent
  with E8's deliberate "not cached on the revision" decision, but it means an E19 receiver will not
  re-negotiate after a policy edit.
- **C5.** E10's BigInteger exactness is justified in-code by "this number is stamped into hashed
  reports". Nothing stamps `PowerScalar` anywhere. The implementation is right; the rationale is
  unearned and will mislead the next reader.

---

## 5. Checked and clean

Stated so the audit is falsifiable rather than a list of complaints.

- **Content-hash registry vs DDL:** all 12 covered tables' column lists match their `CREATE TABLE`
  exactly, in order. No uncovered column on a covered table; `power_coefficient_proposal` correctly
  absent.
- **Idempotency:** skip-when-identical (`IS NOT`) guards present on atom, container (whole-container,
  children included), curve, rarity, roster and channel-policy writes. The two defects found during
  the build (rarity, roster) are the only two, and both are closed.
- **`AtomPushService` wiring:** passes `curves: id => _store.GetCurve(id)` and a real `ownerLevel`;
  the compiler is not being handed nulls.
- **Overlay allowlist:** `stat` sits on `ApplyResourceDelta`, which is the action the shipped
  `fx.overlay_damage` example actually uses. Correct placement.
- **Boundary guards:** all four green, including the DAL rule with SQL now in four new store files.

---

## 6. What this changes about the program's status

The todo, the map and `docs/README.md` all read **complete**. Against the code, the accurate statement
is:

> **Built and proven in isolation: 21 of 21. Reaching the running game: 3 of 21.**

The three that do reach it are E16's injector half (three real channels composed and written), E17's
`poison` CC fix and its three Unity CC branches, and E12's `contentHash` stamp on the battle report.
Everything else is a correct implementation waiting on a seam.

## 7. Proposed wave 6 — the seams

Six modules, in dependency order. Each is small; the value is that each one turns an existing built
thing from inert to live.

| id | Name | Closes | Depends on |
|---|---|---|---|
| **E20** | `content-boot` | A2, A3 — a host-side loader that reads roster / power / policy into their statics at startup, and an importer step in `deploy-play.ps1` + CI so `data/seed/**` reaches a database | — |
| **E21** | `status-stat-applier` | A1 — a status stat plugin (or composer hook) calling `ToModifiers`, and withdrawal by `SourceIdOf` on expiry, with a test that a live `rally` moves a composed number | E20 |
| **E22** | `channel-policy-reader` | B1 — `DerivedStatRegistry` reads the policy table with the code constants as the fallback; a `policy` seed folder | E20 |
| **E23** | `content-codegen` | B2 — `tools/ElementEnumGen`: the `ElementTypeId` mirror, `EffectSeedCatalog`'s deletion (E11 Step 4), and the trait/roster literals generated rather than hand-kept | E20 |
| **E24** | `validation-in-ci` | B4, B5 — `AtomImporter --validate` runs `ContentValidation` and fails on a validation finding; Server + E2E suites added to CI | E20 |
| **E25** | `compose-channel-cache` | B3 — cache `AllCombatChannelIds`, invalidated by `ElementTable.Use`; a compose-path ns budget guard beside E13's | E20 |

**A4 is deliberately not in this list.** A producer of bindings is an item / skill / trait *feature*,
which §7 of the map assigns to those programs. What belongs here is one sentence in the map and the
todo recording that the runtime is inert until one of them binds a container — so the next reader is
not misled by twenty-one green rows.

### The one guard worth adding

A test that **every table in `ContentHashRegistry.Current` has a production reader**. It would have
caught B1 on the day the table landed, and it generalises the rule this program keeps rediscovering
one module at a time.
