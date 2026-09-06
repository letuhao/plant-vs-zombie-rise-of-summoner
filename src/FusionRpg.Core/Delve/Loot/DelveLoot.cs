using FusionRpg.Core.Battle;
using FusionRpg.Core.Dungeon.Tuning;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items.Drops;
using FusionRpg.Core.Power;

namespace FusionRpg.Core.Delve.Loot;

/// <summary>
/// Everything <see cref="DelveLoot.RollRoom"/> needs for one room's own loot roll — the caller's own
/// job to assemble (D3.11): resolve <see cref="SourceKind"/>/<see cref="TableId"/> via
/// <see cref="RoomTableBinding.For"/> first, and the room-kind/once-entry floor-and-shift sources from
/// `DungeonTuning` (§4) before constructing this. Deliberately NOT a raw room-kind string plus a
/// tuning object: each field here is exactly what one call site inside `RollRoom` reads, so nothing
/// downstream re-derives a fact this record already carries.
/// </summary>
/// <param name="Rung">The rung's own `RarityFloor`/`RarityShiftRungs` (`DifficultyRungTuning`,
/// `spec-difficulty-ladder.md` §2) — one of up to four floor sources <see cref="RarityShift.Apply"/>
/// composes, and one of two shift sources it adds.</param>
/// <param name="RoomKindRarityFloor">`loot.rooms.{elite,boss}.rarityFloor` — `null` for a room kind
/// that authors none (fight/cache carry none today).</param>
/// <param name="RoomKindRarityShiftRungs">`loot.rooms.boss.rarityShiftRungs` — 0 for every kind but
/// boss today. Added to <see cref="Rung"/>'s own shift before <see cref="RarityShift.ToWeightShift"/>
/// runs once ("kind and rung shifts add", spec §4, verbatim — shifting by two amounts in sequence is
/// the same ladder movement as shifting once by their sum).</param>
/// <param name="OnceEntryBossRarityFloor">`domain.onceEntry.bossRarityFloor` — populated by the caller
/// ONLY when this room genuinely is the once-domain's own boss row; `null` otherwise. Never the raw
/// tuning value applied unconditionally — that field is non-nullable and always authored
/// (`DomainOnceEntryTuning.BossRarityFloor`), so passing it through for every room would apply a boss
/// floor to rooms that have nothing to do with it.</param>
/// <param name="KeyForLaneId">Non-null only for a room whose clear deterministically unlocks a lane —
/// spec §5's own "the key" row: "no roll, fires on clear."</param>
public sealed record RoomLootInput(
    string PlayerId,
    string SourceKind,
    string SourceId,
    string TableId,
    int Row,
    int Col,
    ulong DelveSeed,
    int ThetaRoom,
    int ThetaActor,
    DifficultyRungTuning Rung,
    string? RoomKindRarityFloor = null,
    int RoomKindRarityShiftRungs = 0,
    string? OnceEntryBossRarityFloor = null,
    string? FirstClearGrant = null,
    string? KeyForLaneId = null,
    long CatalogRevision = 0,
    long DropTableRevision = 0);

/// <summary>One room's sealed loot outcome — the manifest `LootPipeline.Resolve` produced, plus the
/// room-scoped `DropResult` rows `loot-pack` actually places (spec §7: "this module never places,
/// floors or reads a cell count"). <see cref="Key"/> is non-null only when the room's own
/// <see cref="RoomLootInput.KeyForLaneId"/> was supplied.</summary>
public sealed record RoomLootResult(LootManifest Manifest, IReadOnlyList<DropResult> Grants, DropResult? Key);

/// <summary>
/// `dungeon-loot` D3.15 (spec-dungeon-loot.md §3's own "wiring gaps" table, row 1) — PARTIALLY BUILT:
/// this file owns instantiating the boss's own first-clear grant through the REAL
/// `Instantiator.TryInstantiate`, on its own reserved stream, "never flat" (D3.15's own acceptance
/// line).
/// </summary>
public static class DelveLoot
{
    /// <summary>
    /// D3.11 (spec-dungeon-loot.md §3's own literal Code-style pseudocode, verified line by line
    /// against real code rather than trusted): synthesizes the room's own `LootSourceRow`/`LootRequest`
    /// on the reserved `dungeon:loot:{r}:{c}` stream, composes the room's live table through
    /// <see cref="RarityShift.Apply"/> (never the pipeline's own shared `Tables` map, so one room's
    /// floor/shift never leaks into another's), closes `Mint` over `room.ThetaRoom` (spec §3: "Mint
    /// closes over the room Θ"), then defers everything else to the real, unmodified
    /// <see cref="LootPipeline.Resolve"/> — the two Θ reads (content via `ThetaRoom`, count via
    /// `ThetaActor`) are never swapped because each is read exactly once, at its own named call site.
    /// </summary>
    public static AtomRejection RollRoom(
        RoomLootInput room,
        LootContentView view,
        DropVolumeTuning drops,
        LootPityState pity,
        Func<LootGrant, int, LootMintResult> mintAt,
        out RoomLootResult? result)
    {
        result = null;
        if (room is null) throw new ArgumentNullException(nameof(room));
        if (view is null) throw new ArgumentNullException(nameof(view));
        if (mintAt is null) throw new ArgumentNullException(nameof(mintAt));

        var source = new LootSourceRow(room.SourceKind, room.SourceId, room.TableId, room.ThetaRoom, room.FirstClearGrant);
        var seed = SeededRng.DeriveStream(room.DelveSeed, $"dungeon:loot:{room.Row}:{room.Col}").NextULong();
        var request = new LootRequest(room.PlayerId, room.SourceKind, room.SourceId, seed, room.ThetaActor,
            room.CatalogRevision, room.DropTableRevision);

        var bound = view with
        {
            Sources = new Dictionary<string, LootSourceRow>(StringComparer.Ordinal) { [source.Key] = source },
            Tables = RarityShift.Apply(view.Tables, view.Ladder, room.TableId,
                checked(room.Rung.RarityShiftRungs + room.RoomKindRarityShiftRungs),
                room.Rung.RarityFloor, room.RoomKindRarityFloor, room.OnceEntryBossRarityFloor),
            Mint = grant => mintAt(grant, room.ThetaRoom),
        };

        var rejection = LootPipeline.Resolve(request, bound, drops, pity, out var manifest);
        if (!rejection.IsOk) return rejection;

        result = new RoomLootResult(manifest!, DropResult.From(manifest!, room),
            room.KeyForLaneId is { } lane ? DropResult.Key(lane, room) : null);
        return AtomRejection.Ok;
    }

    /// <summary>
    /// Spec's own "wiring gaps" table, verbatim: "host instantiates it through `TryInstantiate` at
    /// `Θ_boss` on `DeriveStream(manifest.LootSeed, LootStreams.RollSeed(grant.Index))` — the
    /// pipeline's own stream at the grant's own index."
    ///
    /// <para><b>Deliberately a NEW, standalone function — `LootPipeline.cs`'s own existing first-clear-
    /// grant code (`:225-231`, "appended flat, RollSeed 0, no Mint") is NOT edited here.</b> Spec's own
    /// text marks that exact edit "ask-first: it changes every manifest with a `FirstClearGrant`" —
    /// moving every existing golden hash for a delve boss clear, a cross-cutting change `LootPipeline
    /// .cs` shares with web-wave/expedition-tier/world-sector, not a call this task can make alone.
    /// This function is the real, tested mechanism an owner-approved wiring would call into; it never
    /// touches the shared path other source kinds already rely on.</para>
    ///
    /// <para><paramref name="origin"/> defaults to <see cref="InstanceOrigin.Drop"/> — spec's own
    /// stated v1 permission ("`InstanceOrigin` has no delve member; filed on the effect-atom program
    /// … v1 reads `Drop` with the binding source carrying scope") — never a fabricated new enum
    /// member.</para>
    /// </summary>
    public static AtomRejection InstantiateBossFirstClearGrant(
        ContainerRow container,
        Func<string, AtomRow?> lookupAtom,
        Func<string, AffixRow?> lookupAffix,
        ulong lootSeed,
        int grantIndex,
        int thetaBoss,
        PowerTuning tuning,
        long catalogRevision,
        out InstanceRow? instance,
        InstanceOrigin origin = InstanceOrigin.Drop)
    {
        if (container is null) throw new ArgumentNullException(nameof(container));
        if (tuning is null) throw new ArgumentNullException(nameof(tuning));

        var streamName = LootStreams.RollSeed(grantIndex);
        var rollSeed = unchecked((long)SeededRng.DeriveStream(lootSeed, streamName).NextULong());

        return Instantiator.TryInstantiate(
            container, lookupAtom, lookupAffix, rollSeed, thetaBoss, tuning, out instance,
            origin, catalogRevision);
    }
}
