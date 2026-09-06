namespace FusionRpg.Core.Delve.Loot;

/// <summary>Two ways a room hands `loot-pack` something: a real, rolled item grant, or a deterministic
/// lane key (no roll — spec §5, verbatim: "no roll, fires on clear").</summary>
public enum DropResultKind
{
    Item,
    Key,
}

/// <summary>
/// `dungeon-loot` (spec-dungeon-loot.md §7, "Haul and the at-risk ledger", `:203`) — one grant, room-
/// scoped, ready for `loot-pack` to place: "Every grant becomes a `DropResult(kind, refId, instanceId?,
/// count, row, col, grantIndex)` row emitted to `loot-pack`, which owns capacity, arrangement, the
/// floor list and the D26 reconciliation. This module never places, floors or reads a cell count."
///
/// <para><b>PARTIALLY BUILT — the record only.</b> The spec's own cited factory methods
/// (`DropResult.From(manifest, room)`, `DropResult.Key(laneId, room)`, `spec-dungeon-loot.md:309-310`)
/// both take a `RoomLootInput` for their own `Row`/`Col` — `RoomLootInput` is `DelveLoot.RollRoom`'s own
/// still-blocked parameter type (D3.11's already-named gap: `RollRoom` itself cannot be built without
/// `RarityShift.Apply`, which does not exist). Building the factories now would mean inventing
/// `RoomLootInput`'s own shape ahead of the task that actually owns deciding it. This record's own
/// fields are stable and buildable regardless — a plain data carrier, callable directly wherever a
/// `RoomLootInput`-free construction is possible (e.g. `RoomTableBinding.For`'s own key-grant case,
/// D3.14).</para>
/// </summary>
public sealed record DropResult(
    DropResultKind Kind, string RefId, string? InstanceId, long Count, int Row, int Col, int GrantIndex);
