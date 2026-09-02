# Spec: content-boot (E20)

**Status: BUILT 2026-08-23, retrospective spec written 2026-09-03.** Module **E20** in the
[effect-atom map](../effect-atom-map.md) §3, Wave 6, Checkpoint F. This document records what shipped;
it is not a plan. Acceptance evidence: [tasks/effect-atom-todo.md](../../../tasks/effect-atom-todo.md)
(search `E20: content-boot`). Scoped from
[completeness-audit.md](completeness-audit.md) findings A2 and A3.

> Reads [definitions.md](definitions.md), which wins where it and this document disagree.

## What it owns

The one call that makes an imported content table the thing a running process actually reads, and the
one deploy step that puts rows in the table before that call happens. `RpgStore.LoadContentIntoRuntime()`
reads the element roster, both matchup matrices, both power tables and the channel-policy rows through
the store's own existing getters, and hands each to the Core static its consumers already read
(`ElementTable.Use`, `PowerTables.Use`, `ChannelPolicyTable.Use`). `Program.cs` calls it once, right
after `store.Init()`. `deploy-play.ps1` runs `tools/AtomImporter` against the live data dir before the
server starts. Nothing else in this module has behaviour.

## What it closed

Waves 1–5 built a write path, a hash, a validator and an importer for six content tables, and every
one of them was read only by its own tests. `ElementTable.Current` and `PowerTables.Current` default to
a shipped code copy and stay on it forever unless a host calls `Use` — and until this module, **no host
did.** Editing an imported roster row, a matchup cell or a power coefficient moved the content hash and
changed no composed number in any process. That is finding A2. A3 is its sibling: nothing ran the
importer, so even a host that had called `Use` would have loaded an empty table.

## The contract as shipped

`src/FusionRpg.Data/Sqlite/RpgStore.ContentBoot.cs:22-32` — one public method on the store partial:

```csharp
public void LoadContentIntoRuntime()
{
    ElementTable.Use(GetElementTable());
    PowerTables.Use(GetPowerTables());
    var directions = GetChannelPolicies().ToDictionary(r => r.ChannelId, r => r.Direction, StringComparer.Ordinal);
    ChannelPolicyTable.Use(new ChannelPolicyTable(directions));
}
```

- **It adds a source of truth, never removes the default one.** Both getters fall back on their own:
  `RpgStore.Elements.cs:50-64` returns `ElementTable.Shipped()` when the roster table is empty, and
  `RpgStore.Power.cs:61-71` returns `PowerTables.Authored()` when no coefficient rows exist. A process
  booting against a store with nothing imported therefore behaves exactly as it did before this module.
- **The statics it writes are the ones production already read.** `ElementTable.Current`
  (`ElementTable.cs:66`) is what `BattleStatComposer`, `ElementRingMatrix` and `ShieldElementMatrix`
  consume; `PowerTables.Current` (`CoefficientTable.cs:166`) is what `CostFunction`/`ActorPowerCache`
  consume. E20 wrote no consumer — it connected the ones that existed.
- **One caller, in the server.** `src/FusionRpg.Server/Program.cs:145`, immediately after
  `Init()` on line 141 and before the demon catalogs are forced. The C1 boot sweep
  (`ClearSessionScopedBindings`, `CountOrphanInstances`) was added to the same block later in the same
  session (`Program.cs:163-168`).
- **The deploy step.** `scripts/deploy-play.ps1:217-220` runs
  `dotnet run --project tools/AtomImporter -c Release -- --db $DataDir` after the server publish and
  before the server start, and throws if the importer refuses. Idempotence is E14a's existing
  skip-when-identical behaviour; E20 did not re-implement it.
- The third `Use` call (`ChannelPolicyTable`) and its `GetChannelPolicies()` read are **E22's**,
  landed into this same method. Their contract is in [spec-channel-policy-reader.md](spec-channel-policy-reader.md).

## What it does NOT do

- **It does not run in the injector.** The injector process holds no content rows by design (E19's
  guarantee) and has no `RpgStore`; it stays on the shipped code fallbacks. Nothing in the module tries
  to change that.
- **It does not create bindings.** Loading a table is not producing content for an owner — A4, the
  missing binding producer, is explicitly out of scope (map §7).
- **It is not a reload.** There is no watcher, no revision poll and no second call site; content is read
  once per process, at boot. A content edit needs a restart.
- **It does not validate.** The importer refuses bad rows before they land (E14a/E24); the loader trusts
  the store.

## How it is verified today

- **Unit** — `tests/FusionRpg.Data.Tests/ContentBootTests.cs`, 3 tests: an empty store loads the shipped
  defaults and changes nothing; an imported roster row is what `ElementTable.Current` reflects; an
  imported coefficient row is what `PowerTables.Current` reflects. It runs in a dedicated collection and
  resets both statics in `Dispose` (`ContentBootTests.cs:34`), because `Use` is process-global.
- **Seam** — `tests/FusionRpg.E2E.Tests/ContentBootE2ETests.cs`, 2 tests, both driving the real importer
  against a temp SQLite file with the real `data/seed/**`:
  `A_real_import_through_the_real_host_store_is_what_the_loader_reflects` and
  `A_seeded_element_the_shipped_table_does_not_have_survives_the_real_import_chain`.
- **Guard** — `tests/FusionRpg.Guard.Tests/ContentTableReaderGuardTests.cs`, 3 tests (shared with E22).

**Coverage is honest but narrow in one place:** the `deploy-play.ps1` half is covered by nothing
executable. Its acceptance was a parse check and a manual run, so a future edit that drops or reorders
the import step fails no test.

## Known residuals

- **Nothing outside `deploy-play.ps1` runs the importer.** A repo-wide sweep for `AtomImporter` across
  `src/` and `scripts/` returns only `scripts/deploy-play.ps1:218`. The launcher and the player zip have
  no import step, so a player install boots on the shipped code fallback and the whole content layer is
  inert there. Not a defect of the loader; a gap in the delivery path nobody has claimed.
- **The reader guard does not do what the todo says it does.** The todo describes
  `ContentTableReaderGuardTests` as asserting "every table name in `ContentHashRegistry.Current` has an
  entry" in a maintained reader map. The shipped test
  (`ContentTableReaderGuardTests.cs:39-97`) instead makes six text assertions against
  `RpgStore.ContentBoot.cs` plus a fixed-list trip-wire naming **18** registry tables by hand
  (`:77-85`, grown from 12 as later programs registered more). The effect is close — a nineteenth table
  fails the trip-wire and forces a human to look — but the guard cannot tell whether a table has a
  reader, only whether the list was edited.
- **A4 — no production binding producer**, tracked in the todo's "Unowned" section and reconfirmed on a
  live lawn 2026-08-30 (`select count(*) from effect_binding` → `0`). The loader and importer both exist
  and both ran; the binding producer does not. Out of this module's scope, recorded here because "the
  layer is live" is only true up to the point where something binds a container to an owner.
- **The importer reports "nothing changed" when only compiler code changed**, because the content hash
  covers seed data. Harmless while `effect_binding` is empty; a silent-staleness trap once it is not.
