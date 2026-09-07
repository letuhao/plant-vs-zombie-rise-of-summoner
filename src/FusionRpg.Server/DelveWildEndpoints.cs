using FusionRpg.Contracts;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Data;

namespace FusionRpg.Server;

/// <summary>
/// D4.8 (spec-wild-room.md §2, §6, §7) — the wild-room HTTP surface: `POST …/rooms/{id}/talk`,
/// `…/pray`, `…/cage`. `{id}` is the room's own `sectorId` (`rpg_delve_rooms.sector_id`) — every
/// route resolves `row`/`col` from that room's real, persisted `RowIndex`/`ColIndex` rather than
/// trusting the caller to restate them (this schema has no per-room `Θ` column anywhere, so
/// `thetaRoom` itself still arrives from the caller, matching `RecordClear`'s own identical shape).
///
/// <para><b>Honest scope, stated plainly rather than implied.</b> `pray` is complete end to end —
/// <see cref="RpgStore.PullAtAltar"/> composes the whole §6 transaction (price, spend, roll, pity,
/// pending-haul append) from real inputs this endpoint resolves itself. `talk`/`cage` are NOT a
/// complete multi-verb resolver — no Core function decides "which verb was legal, what did the
/// outcome draw" end to end today: `TalkTree.cs`'s real, shipped shape has `Offered`/`StanceShift`
/// but no `Step`-style orchestrator, despite the spec's own §Interface table citing a
/// `TalkTree.Step(...) → TalkStep` that does not exist in the tree (confirmed by reading the file in
/// full). What IS real and closed is `RpgStore.TalkJoin`'s own debit+mint transaction — so these two
/// routes are narrowly scoped to COMMITTING an already-resolved `joins` outcome (the caller supplies
/// the already-assembled <see cref="DemonMintSpec"/> and price, exactly what
/// `RecruitMint.Build`/`OfferPricing` would have produced upstream), not to resolving a talk turn
/// from a bare verb choice. A real player-facing `talk`/`cage` panel needs that missing orchestrator
/// built first — a genuine, precise, additional gap, named here rather than silently assumed away or
/// forced by inventing a resolver this task was never scoped to build.</para>
///
/// <para><b>The "steered party" check.</b> Confirmed nothing in this codebase persists "which party
/// is currently steered" outside one single, ephemeral, PER-BATTLE-SESSION concept:
/// `RaidIntentSource` (D2.13) takes its steered/automated split as a constructor argument built fresh
/// per `BattleEngine.Resolve` call, and `WebMatchService.cs`'s own comment says the interactive/
/// autopilot distinction "lives only in the in-memory `BattleSessionRegistry`, never persisted" — and
/// that registry exists only for an ALREADY-STARTED fight, not for pre-fight room-standing actions
/// like `talk`/`pray`/`cage`. The other place "steered" appears in production-shaped code,
/// `PackDtoProjection.Project`'s own `partySteered` parameter, has ZERO non-test callers anywhere
/// (confirmed by grep) — so there is no live signal to read even indirectly today. The steered check
/// is wired here as a caller-supplied <see cref="Func{T1,T2,TResult}"/>, matching this program's own
/// established idiom for a real-but-not-yet-computable fact (`DelveEndpoints.cs`'s own
/// `BuildDelveStartLive`, several delegates named the identical way for an upstream content gap) — the
/// PRODUCTION wiring below (<see cref="ProductionIsPartySteered"/>) returns `true` unconditionally,
/// with this comment attached, meaning today's real, checked boundary is OWNERSHIP (this delve
/// belongs to the calling player), not a genuine steered-vs-autopilot distinction. For a `solo` raid
/// (one party) that is the exact right answer, since no other party exists to confuse it with; for
/// `duo`/`quad` it is a real, named, narrower guarantee than "true steering," pending `delve-stage`'s
/// own session-layer work landing a live signal this delegate can finally read.</para>
///
/// <para><b>Deliberately NOT checked:</b> that <c>partyEntityId</c> is already a member of
/// `delve.Parties` — `parties_json` starts empty at `CreateDelve` and grows lazily, "first room, first
/// row" (`WritePartyMembers`'s own doc comment), so a party whose FIRST-EVER room happens to be a wild
/// room legitimately has no row yet; requiring one would wrongly refuse that real case. Both
/// `RpgStore.TalkJoin`/`PullAtAltar`/`AppendPartyHaulUnlocked` already handle a first-ever
/// `partyEntityId` correctly (the same upsert every other `parties_json` writer uses).</para>
/// </summary>
public static class DelveWildEndpoints
{
    public static void MapDelveWild(this WebApplication app)
    {
        var g = app.MapGroup("/api/delve");
        Func<long, long, bool> steered = ProductionIsPartySteered;

        g.MapPost("/rooms/{id}/talk", (string id, DelveWildJoinRequest body, RpgStore store) =>
            HandleJoin(id, body, store, steered));
        g.MapPost("/rooms/{id}/cage", (string id, DelveWildJoinRequest body, RpgStore store) =>
            HandleJoin(id, body, store, steered));
        g.MapPost("/rooms/{id}/pray", (string id, DelveWildPrayRequest body, RpgStore store) =>
            HandlePray(id, body, store, steered));
    }

    /// <summary>Ownership is the only real, always-computable check today — see this file's own doc
    /// comment. Returns `true` unconditionally rather than inventing a persisted schema concept no
    /// live-session caller exists yet to feed.</summary>
    static bool ProductionIsPartySteered(long delveId, long partyEntityId) => true;

    /// <summary>Shared player/delve/room resolution both routes need — extracted so the two handlers
    /// below hold only their own action-specific logic, mirroring `DelveEndpoints.cs`'s own
    /// extracted-handler idiom.</summary>
    static IResult? ValidateRoom(
        string sectorId, long? bodyPlayerId, long delveId, long partyEntityId,
        RpgStore store, Func<long, long, bool> isPartySteered,
        out DelveRoomRow? room, out long playerId)
    {
        room = null;
        playerId = bodyPlayerId ?? store.GetCurrentPlayerId();
        if (!store.PlayerExists(playerId)) return Results.NotFound(new { reason = "player.unknown" });

        var delve = store.LoadDelve(delveId);
        if (delve is null || delve.PlayerId != playerId) return Results.NotFound(new { reason = "delve.not-found" });

        var found = store.LoadDelveRooms(delveId).FirstOrDefault(r => string.Equals(r.SectorId, sectorId, StringComparison.Ordinal));
        if (found is null) return Results.NotFound(new { reason = "room.not-found" });
        room = found;

        if (!isPartySteered(delveId, partyEntityId)) return Results.Conflict(new { reason = "wild.party-not-steered" });

        return null;
    }

    // ---- talk / cage: commit an already-resolved `joins` outcome (see this file's own doc comment) --

    internal static IResult HandleJoin(string sectorId, DelveWildJoinRequest body, RpgStore store, Func<long, long, bool> isPartySteered)
    {
        if (body is null) return Results.BadRequest(new { reason = "body.missing" });
        var precheck = ValidateRoom(sectorId, body.PlayerId, body.DelveId, body.PartyEntityId, store, isPartySteered, out _, out var playerId);
        if (precheck is not null) return precheck;

        if (body.Spec is null) return Results.BadRequest(new { reason = "spec.missing" });
        if (body.Price <= 0) return Results.BadRequest(new { reason = "price.invalid" });
        if (string.IsNullOrWhiteSpace(body.SinkKey)) return Results.BadRequest(new { reason = "sinkKey.missing" });

        var (ok, reason, specimen, soulsUnbanked) = store.TalkJoin(body.DelveId, playerId, body.Price, body.SinkKey!, body.Spec);
        if (!ok) return Refusal(reason);
        return Results.Ok(new { specimen, soulsUnbanked });
    }

    // ---- pray: the full §6 altar-pull transaction, real end to end ----------------------------------

    internal static IResult HandlePray(string sectorId, DelveWildPrayRequest body, RpgStore store, Func<long, long, bool> isPartySteered)
    {
        if (body is null) return Results.BadRequest(new { reason = "body.missing" });
        var precheck = ValidateRoom(sectorId, body.PlayerId, body.DelveId, body.PartyEntityId, store, isPartySteered, out var room, out var playerId);
        if (precheck is not null) return precheck;

        if (string.IsNullOrWhiteSpace(body.AltarBannerId)) return Results.BadRequest(new { reason = "altarBannerId.missing" });
        ElementTypeId? focus = null;
        if (!string.IsNullOrWhiteSpace(body.FocusElementId))
        {
            if (!ElementRoster.TryParse(body.FocusElementId, out var parsed)) return Results.BadRequest(new { reason = "focusElement.unknown" });
            focus = parsed;
        }

        var (ok, reason, result, soulsUnbanked) = store.PullAtAltar(
            body.DelveId, body.PartyEntityId, room!.RowIndex, room.ColIndex, body.ThetaRoom, body.AltarBannerId!, focus);
        if (!ok) return Refusal(reason);
        return Results.Ok(new { result, soulsUnbanked });
    }

    /// <summary>Same mapping shape `DelveEndpoints.Refusal`/`ContractEndpoints.Refusal` already use:
    /// a price the player cannot meet is a conflict, not malformed input; a missing row is a 404;
    /// everything else (a named content refusal like `altar.banner-unknown`) is a 400.</summary>
    static IResult Refusal(string reason) => reason switch
    {
        "delve.souls-insufficient" => Results.Conflict(new { reason }),
        "delve.not-found" or "player.unknown" or "player.not-found" => Results.NotFound(new { reason }),
        _ => Results.BadRequest(new { reason }),
    };

    public sealed class DelveWildJoinRequest
    {
        public long? PlayerId { get; set; }
        public long DelveId { get; set; }
        public long PartyEntityId { get; set; }
        public long Price { get; set; }
        public string? SinkKey { get; set; }
        public DemonMintSpec? Spec { get; set; }
    }

    public sealed class DelveWildPrayRequest
    {
        public long? PlayerId { get; set; }
        public long DelveId { get; set; }
        public long PartyEntityId { get; set; }
        public int ThetaRoom { get; set; }
        public string? AltarBannerId { get; set; }
        public string? FocusElementId { get; set; }
    }
}
