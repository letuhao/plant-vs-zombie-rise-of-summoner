# Spec: channel-policy-reader (E22)

**Status: BUILT 2026-08-23, retrospective spec written 2026-09-03.** Module **E22** in the
[effect-atom map](../effect-atom-map.md) §3, Wave 6, Checkpoint F. Depends on E20 (shares the boot call
and the reader guard). This document records what shipped; it is not a plan. Acceptance evidence:
[tasks/effect-atom-todo.md](../../../tasks/effect-atom-todo.md) (search `E22: channel-policy-reader`).
Scoped from [completeness-audit.md](completeness-audit.md) finding B1.

> Reads [definitions.md](definitions.md), which wins where it and this document disagree.

## What it owns

The read path and the author path for `effect_channel_policy`. `ChannelPolicyTable` is a Core static
holding a channel → direction map, swapped in by the host at boot the way `ElementTable` and
`PowerTables` are; `StatChannels.DirectionOf` consults it before its code switch. On the author side,
E22 owns `SeedEntryKind.ChannelPolicy`, the `channel-policy` seed folder, and the import transaction's
validate-and-write of policy rows.

## What it closed

E16 shipped `effect_channel_policy` with a write method (`RpgStore.UpsertChannelPolicies`), a place in
the content hash at registry **v4**, and a write-time refusal of unknown channels — and **zero readers**
and no author path. It was hashed, versioned and content-checked, none of which requires anything to
ever read it. `StatChannels.DirectionOf` stayed the hardcoded switch it had always been.

The build also corrected the plan's own premise. The plan named `DerivedStatRegistry` as the consumer;
`DerivedStatRegistry` registers **derived** channel ids (`status.resist.dot` and friends — the family is `status.resist`, with no `combat.` prefix) while
`effect_channel_policy` is validated against `StatChannels.All`, the **primary** channels. The two sets
never overlap, so that consumer could not exist however it was wired. Checking further:
`default_value`, `cap_milli` and `compose_kind` have no consumer anywhere, for any channel —
`StatComposer` applies no per-channel cap to primary channels at all. E22 therefore shipped the claim
that is true (direction is live) instead of the one the plan wanted.

## The contract as shipped

**`src/FusionRpg.Core/Stats/ChannelPolicyTable.cs:28-73`:**

- Constructed from `IReadOnlyDictionary<string,int>`; any value other than
  `(int)ChannelDirection.LowerIsBetter` normalises to `HigherIsBetter` (`:36-39`) — an out-of-range
  direction is defensively benign, never a throw.
- `Empty` (`:43`) is the default and what every host with nothing imported runs on.
- `TryGetDirection(channel, out direction)` (`:45`) is the only read.
- Statics match `ElementTable`'s shape exactly: `Current` = `Scoped.Value ?? _global` (`:51`),
  process-wide `Use` (`:54`), `ResetToEmpty` (`:57`), and `AsyncLocal` `UseScoped` returning a restoring
  `IDisposable` (`:60-73`) so one test cannot disturb another running beside it.

**The consumer** — `src/FusionRpg.Core/Stats/ModifierOp.cs:59-75`:

```csharp
public static ChannelDirection DirectionOf(string? channel)
{
    if (channel is not null && ChannelPolicyTable.Current.TryGetDirection(channel, out var stored))
        return stored;
    return channel switch { AttackInterval or ProduceInterval => LowerIsBetter, _ => HigherIsBetter };
}
public static bool IsLowerBetter(string? channel) => DirectionOf(channel) == ChannelDirection.LowerIsBetter;
```

An imported row overrides the code default for an **existing** channel; an empty table falls through
unchanged. `IsLowerBetter` has two production readers today: `CostFunction.cs:74` (direction-aware
pricing) and `ContentValidation.cs:256`.

**The boot call** — `src/FusionRpg.Data/Sqlite/RpgStore.ContentBoot.cs:29-31` builds the map from
`GetChannelPolicies()` (`RpgStore.ChannelPolicy.cs:45`) and calls `ChannelPolicyTable.Use`.

**The author path**, built in the same change because a reader with no way to write rows is the same
gap upside down:

- `SeedEntryKind.ChannelPolicy` and `ChannelPolicySeedRow(ChannelId, Direction)` in
  `src/FusionRpg.Core/Effects/Atoms/AtomSeedFile.cs:15,47`, parsed by `ReadChannelPolicy` (`:377-389`)
  and keyed on the JSON kind string `"channel-policy"` (`:468`).
- `"channel-policy"` joins `SeedScanner.OwnedFolders` (`tools/AtomImporter/SeedScanner.cs:15`).
- `data/seed/channel-policy/defaults.json` documents the two already-lower-is-better channels
  (`attackInterval`, `produceInterval`) as data — deliberately zero design change, so importing it is
  verifiably a no-op.
- `RpgStore.ImportContent` validates and writes policy rows inside the same transaction as everything
  else, split into `ValidateChannelPolicyRows` + `UpsertChannelPolicyRowUnlocked` following E14a's
  container/curve/rarity extraction pattern.

## What it does NOT do

- **It reads one column of four.** `default_value`, `cap_milli` and `compose_kind` are still written,
  still hashed, and still read by nothing. The table's own doc comment says so
  (`ChannelPolicyTable.cs:11-20`) rather than implying coverage it does not have.
- **It cannot name a derived channel.** The table is scoped to `StatChannels.All`, the eleven primary
  channels (`ModifierOp.cs:46-50`). Derived-channel caps are `DerivedStatDef.Cap` from the
  code-registered derived catalog — a different, already-consumed mechanism.
- **It does not add or remove a channel.** E1's code-or-data rule: changing a value on an existing
  channel is data; adding a channel needs a reader and stays code.
- **It does not compose.** `StatComposer` still owns the interval floor and the arithmetic; this table
  only answers which way is better.

## How it is verified today

- **Unit** — `tests/FusionRpg.Core.Tests/Stats/ChannelPolicyTableTests.cs`, 5 tests: empty-table
  fallthrough, a stored direction overriding the code default, `IsLowerBetter` reading through,
  `UseScoped` restoring on dispose, and direction `2`-and-above treated as higher-is-better rather than
  thrown.
- **Seam** — `tests/FusionRpg.E2E.Tests/ChannelPolicyE2ETests.cs`, 3 tests through the real import
  transaction: the shipped seed file imports clean and changes no behaviour; a seeded direction flip the
  code default does not have survives the real chain (on a throwaway temp store, kept off the shared
  fixture); an unknown channel is refused by the real import, not silently accepted.
- **Store** — `ChannelPolicyStoreTests` (11 tests, E16 plus C4's revision-bump pair).
- **Guard** — `tests/FusionRpg.Guard.Tests/ContentTableReaderGuardTests.cs:90-97` asserts
  `DirectionOf` consults `ChannelPolicyTable.Current` **before** the switch, and `:39-56` asserts the
  loader makes the `ChannelPolicyTable.Use`/`GetChannelPolicies()` connection.

## Known residuals

- **Three of the table's four columns remain unread**, exactly as before this module. Making them live
  is a design question (what a primary-channel cap or default would even mean), not wiring.
- **`ChannelPolicyTable.Use` accepts any channel string.** Unknown-channel refusal lives in the import
  transaction, so a caller constructing a table directly in code can register a direction for a channel
  that does not exist. Harmless — nothing reads a direction for a channel it never composes — but the
  refusal is not on the type.
- **The reader guard's registry trip-wire is a hand-maintained list**, not a derived check; see
  [spec-content-boot.md](spec-content-boot.md) "Known residuals".
