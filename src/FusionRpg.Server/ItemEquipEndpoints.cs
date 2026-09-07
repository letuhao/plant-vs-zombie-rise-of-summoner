using FusionRpg.Core.Items;
using FusionRpg.Data;

namespace FusionRpg.Server;

/// <summary>One durable assignment as a caller renders it — module 4's row, unchanged.</summary>
public sealed record ItemAssignmentDto(string Role, string RefKind, string RefId, string AssignedUtc);

/// <summary>
/// What one equip or unequip did, in the shape a surface can draw without a second read: whether it
/// happened, which named rule refused it when it did not, and the specimen's whole assignment list
/// afterwards so the paperdoll never has to guess what changed.
/// </summary>
/// <param name="Replaced">The occupant this write displaced, or <c>null</c> when the role was empty.
/// Equipping over a worn item is a swap, not a refusal — but it is never silent.</param>
public sealed record ItemEquipOutcomeDto(
    bool Ok,
    string Verb,
    string Reason,
    string SpecimenId,
    string Role,
    string RefKind,
    string RefId,
    ItemAssignmentDto? Replaced,
    IReadOnlyList<ItemAssignmentDto> Assignments);

/// <summary>
/// ⭐ <b>The equip executor</b> — the production caller item module 4 named as its own last blocker.
///
/// <para>Module 4 shipped <c>rpg_item_assignment</c>, <c>SaveAssignment</c>/<c>RemoveAssignment</c>,
/// <see cref="EquipGate"/> and <see cref="EquipProjector"/>, all tested, and <b>nothing outside
/// <c>tests/</c> called the two writes</b>. This class calls them against a real stored item and a
/// real specimen, after the gate has said yes.</para>
///
/// <para><b>Equip costs nothing, and that is the design, not an omission.</b> The workbench's six
/// verbs spend because craft, salvage, enhance and socket consume materials (module 14 prices them).
/// Putting an item you already own into a role you already have consumes nothing, so there is no
/// <c>correlationId</c> here either: with no debit there is no double-spend for one to protect
/// against, and the write is idempotent on its own — <c>SaveAssignment</c> upserts on
/// <c>(specimen_id, role)</c>, so a retried request lands the same row.</para>
///
/// <para><b>⛔ This route owns <c>ref_kind = "rolled"</c> rows and nothing else.</b> The four
/// hand-authored relics live in the same table since the 2026-09-06 row migration (D1 §10 M1), as
/// <c>ref_kind = "stock"</c>, and they are written by
/// <c>PUT /api/unique/actors/{id}/equipment/{slot}</c> — which also rebuilds <c>mods_json</c> and
/// reconciles the <c>unique-equip</c> atom bindings in the same call. Overwriting one of those cells
/// from here would delete the row and leave both of those derived states standing, so a role a relic
/// holds is <b>refused by name</b> and the player is pointed at the flow that owns it. Two flows,
/// one table, no shared writes.</para>
///
/// <para><b>Assign is this class's whole job; the Lawn-runtime sync lives one layer up.</b>
/// `spec-equip-assign.md` is explicit that the runtime binding is rebuilt as a full projection
/// <i>at deploy</i>, never patched at assign time — so this class itself deliberately does not call
/// <c>ApplyEquipProjection</c> (module 5) or <c>ApplyEquippedGrants</c> (module 19). ⭐ <b>Fixed
/// 2026-09-07 (P1.5-L):</b> those two calls are no longer callerless — <c>ItemEquipEndpoints</c>'s
/// own <c>/api/items/equip</c>/<c>/unequip</c> handlers call <c>SyncLawnRuntimeAsync</c> after every
/// successful outcome from this service, which calls <c>RpgStore.MaterializeRolledEquipRuntime</c>
/// (the method that runs both). A Lawn-bound specimen's equipped rolled items now materialize their
/// <c>effect_binding</c> rows and granted actions through the real production write surface, not only
/// through <c>WebMatchService.BuildSquad</c>'s battle-squad path.</para>
/// </summary>
public sealed class ItemEquipService
{
    /// <summary>I13 §4.4's kind for an assignment that pins one rolled copy — the <c>ref_id</c> is an
    /// <c>effect_instance.instance_id</c>. It is also the only kind
    /// <c>RpgStore.ApplyEquipProjection</c> turns into a binding, so writing anything else here would
    /// persist a decision module 5 could never project.</summary>
    public const string RolledRefKind = EquipRefKinds.Rolled;

    /// <summary>The relic flow's kind (a catalog id, not a rolled copy). Read here only to refuse —
    /// never written. Since 2026-09-06 the relic flow refuses the reverse case by name too
    /// (<c>slot.claimed_by_item</c>), so the two flows are symmetric rather than one-way.</summary>
    const string StockRefKind = EquipRefKinds.Stock;

    readonly RpgStore _store;
    readonly EquipGate _gate;

    public ItemEquipService(RpgStore store, EquipGate? gate = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _gate = gate ?? new EquipGate();
    }

    // ---- reads ---------------------------------------------------------------------------------

    public IReadOnlyList<ItemAssignmentDto> List(string specimenId) =>
        _store.ListAssignments(specimenId).Select(ToDto).ToList();

    // ---- equip ---------------------------------------------------------------------------------

    public ItemEquipOutcomeDto Equip(long playerId, string specimenId, string instanceId, string roleId)
    {
        if (!ItemRoles.TryParse(roleId, out var role))
            return Refuse("equip", specimenId, roleId, instanceId,
                $"equip.role-unknown: '{roleId}' is not one of the sixteen registry roles");

        if (!TryResolveSpecimen(playerId, specimenId, out var actor, out var specimenRefusal))
            return Refuse("equip", specimenId, roleId, instanceId, specimenRefusal);

        var item = _store.GetItem(instanceId);
        if (item is null)
            return Refuse("equip", specimenId, roleId, instanceId, $"item.unknown: no owned item '{instanceId}'");

        if (!string.Equals(item.PlayerId, PlayerKey(playerId), StringComparison.Ordinal))
            return Refuse("equip", specimenId, roleId, instanceId,
                $"item.not-owned: '{instanceId}' belongs to player '{item.PlayerId}'");

        // ⚠ `Locked` is deliberately NOT a refusal here. `RpgItemRow.Locked`'s own contract is
        // "refuse salvage/transfer while true" — it protects an item from being consumed, and wearing
        // one consumes nothing. The workbench checks it because every one of its verbs spends.
        if (!string.Equals(item.Disposition, "owned", StringComparison.Ordinal))
            return Refuse("equip", specimenId, roleId, instanceId,
                $"item.not-owned: '{instanceId}' is '{item.Disposition}'");

        var instance = _store.GetInstance(instanceId);
        if (instance is null)
            return Refuse("equip", specimenId, roleId, instanceId,
                $"item.instance-missing: no effect_instance '{instanceId}'");

        var container = _store.GetContainer(instance.ContainerId);
        if (container is null)
            return Refuse("equip", specimenId, roleId, instanceId,
                $"item.container-missing: '{instance.ContainerId}' is not in the catalog");

        var generation = _store.GetItemGeneration(instanceId);

        // The item's OWN role, from the pipeline's stamp first and the container's declared slot
        // second. Both are real stored decisions; neither is guessed. With neither, the answer is a
        // refusal rather than "any role will do" — the same rule the relic flow's own
        // `SlotMatchesItem` applies to the three legacy slots.
        var itemRoleId = generation?.Role is { Length: > 0 } g ? g : container.Slot ?? "";
        if (!ItemRoles.TryParse(itemRoleId, out var itemRole))
            return Refuse("equip", specimenId, roleId, instanceId,
                $"equip.item-role-unknown: '{instanceId}' declares no role — item_generation has no stamp " +
                $"and its container's slot is '{container.Slot}'");

        if (itemRole != role)
            return Refuse("equip", specimenId, roleId, instanceId,
                $"equip.role-mismatch: '{instanceId}' is a '{ItemRoles.Id(itemRole)}' item, not a '{roleId}'");

        // D19's surviving half, in the order the spec fixes: the unlock predicate answers "does this
        // specimen have this slot?" before frame/level/faction answer "may it wear this?".
        // ⚠ The frame arm is inert until X1 ships a species frame (`actor.Frame` is null today), and
        // no content sets a faction clause — both are passed real values anyway rather than being
        // skipped, so the day either lands the gate is already reading it.
        var refusal = _gate.Explain(role, actor, generation?.Frame, container.LevelReq, factionReq: null);
        if (refusal is { } r)
            return Refuse("equip", specimenId, roleId, instanceId, $"{ReasonCode(r.Reason)}: {r.Remedy}");

        var standing = _store.ListAssignments(specimenId);
        var occupant = standing.FirstOrDefault(a => a.Role == role);

        // Already in exactly this cell — nothing to write, and saying "done" is the truth.
        if (occupant is not null
            && string.Equals(occupant.RefKind, RolledRefKind, StringComparison.Ordinal)
            && string.Equals(occupant.RefId, instanceId, StringComparison.Ordinal))
            return new ItemEquipOutcomeDto(true, "equip", "equip.already-in-this-role", specimenId,
                roleId, RolledRefKind, instanceId, Replaced: null, List(specimenId));

        if (occupant is not null && string.Equals(occupant.RefKind, StockRefKind, StringComparison.Ordinal))
            return Refuse("equip", specimenId, roleId, instanceId,
                $"equip.role-held-by-relic: '{roleId}' holds '{occupant.RefId}', which was equipped through the " +
                "relic flow — take it off there first, so its mods and atom bindings come off with it");

        // One physical copy cannot be worn twice. The primary key already stops two items sharing a
        // role; this stops one item filling two, on this specimen or any other.
        var holders = _store.FindAssignmentHolders(new[] { instanceId }, RolledRefKind);
        if (holders.TryGetValue(instanceId, out var cell)
            && !(string.Equals(cell.SpecimenId, specimenId, StringComparison.Ordinal)
                 && string.Equals(cell.Role, roleId, StringComparison.Ordinal)))
            return Refuse("equip", specimenId, roleId, instanceId,
                $"equip.already-worn: '{instanceId}' is already in '{cell.Role}' on specimen '{cell.SpecimenId}'");

        _store.SaveAssignment(specimenId, role, RolledRefKind, instanceId);

        return new ItemEquipOutcomeDto(true, "equip", "", specimenId, roleId, RolledRefKind, instanceId,
            Replaced: occupant is null ? null : ToDto(occupant),
            Assignments: List(specimenId));
    }

    // ---- unequip -------------------------------------------------------------------------------

    public ItemEquipOutcomeDto Unequip(long playerId, string specimenId, string roleId)
    {
        if (!ItemRoles.TryParse(roleId, out var role))
            return Refuse("unequip", specimenId, roleId, "",
                $"equip.role-unknown: '{roleId}' is not one of the sixteen registry roles");

        if (!TryResolveSpecimen(playerId, specimenId, out _, out var specimenRefusal))
            return Refuse("unequip", specimenId, roleId, "", specimenRefusal);

        var occupant = _store.ListAssignments(specimenId).FirstOrDefault(a => a.Role == role);
        if (occupant is null)
            return Refuse("unequip", specimenId, roleId, "",
                $"equip.role-empty: nothing is in '{roleId}' on specimen '{specimenId}'");

        if (!string.Equals(occupant.RefKind, RolledRefKind, StringComparison.Ordinal))
            return Refuse("unequip", specimenId, roleId, occupant.RefId,
                $"equip.role-held-by-relic: '{roleId}' holds '{occupant.RefId}', which was equipped through the " +
                "relic flow — take it off there, so its mods and atom bindings come off with it");

        // §6.4's atomicity claim, exercised: unequip is one row deleted and no second writer. The
        // item itself is untouched — module 1's R1 ("unequip does not destroy the item").
        var removed = _store.RemoveAssignment(specimenId, role);
        if (!removed)
            return Refuse("unequip", specimenId, roleId, occupant.RefId,
                $"equip.role-empty: nothing is in '{roleId}' on specimen '{specimenId}'");

        return new ItemEquipOutcomeDto(true, "unequip", "", specimenId, roleId, occupant.RefKind, occupant.RefId,
            Replaced: ToDto(occupant), Assignments: List(specimenId));
    }

    // ---- shared --------------------------------------------------------------------------------

    bool TryResolveSpecimen(long playerId, string specimenId, out SpecimenActor actor, out string reason)
    {
        actor = default;

        if (string.IsNullOrWhiteSpace(specimenId))
        {
            reason = "equip.specimen-required: name the specimen to equip to";
            return false;
        }

        var row = _store.GetUniqueActor(specimenId);
        if (row is null)
        {
            reason = $"equip.specimen-unknown: no bound creature '{specimenId}'";
            return false;
        }

        if (row.PlayerId != playerId)
        {
            reason = $"equip.specimen-not-owned: '{specimenId}' belongs to player '{row.PlayerId}'";
            return false;
        }

        // ⚠ `UniqueActorDto.Level` is a `long` and `SpecimenActor.Level` is an `int` (module 4's own
        // shape, matching `ActorContext` and `BindGate`). A level is a ladder INDEX, not a magnitude,
        // so `int` is the repo's shape for it — but the narrowing still has to be checked rather than
        // wrapped or clamped: a clamp would silently admit an item the level gate should refuse.
        // `checked` makes an impossible level throw instead of lying.
        actor = new SpecimenActor(row.InstanceId, Frame: null, checked((int)row.Level), Faction: null);
        reason = "";
        return true;
    }

    static string PlayerKey(long playerId) => playerId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Module 4's internal refusal enum as a stable wire code. ⚠ <c>RoleLocked</c> is still
    /// unratified as I13's fifteenth official reason code — this is the module's own result
    /// vocabulary, which the spec distinguishes from that closed list.</summary>
    static string ReasonCode(EquipRefusalReason reason) => reason switch
    {
        EquipRefusalReason.RoleLocked => "equip.role-locked",
        EquipRefusalReason.RoleNotOnFrame => "equip.role-not-on-frame",
        EquipRefusalReason.LevelTooLow => "equip.level-too-low",
        EquipRefusalReason.FactionMismatch => "equip.faction-mismatch",
        _ => "equip.refused",
    };

    static ItemAssignmentDto ToDto(EquipAssignment a) =>
        new(ItemRoles.Id(a.Role), a.RefKind, a.RefId, a.AssignedUtc);

    ItemEquipOutcomeDto Refuse(string verb, string specimenId, string roleId, string refId, string reason) =>
        new(false, verb, reason, specimenId, roleId, RolledRefKind, refId, Replaced: null,
            Assignments: string.IsNullOrWhiteSpace(specimenId)
                ? Array.Empty<ItemAssignmentDto>()
                : List(specimenId));
}

/// <summary>
/// item module 4 (<c>equip-assign</c>) — the <b>write</b> surface for putting an item in a role.
///
/// <para>Its own file for the same reason <c>WorkbenchEndpoints.cs</c> is: module 20
/// (<c>ItemSurfaceEndpoints.cs</c>) is read-only by construction, and a write path through the
/// presentation layer is the "second surface" that module exists to prevent. These verbs belong to
/// module 4, and each route is a thin shell over <see cref="ItemEquipService"/> — no gate logic and
/// no persistence decisions live in this file.</para>
///
/// <para><b>No <c>correlationId</c>, and the asymmetry with the workbench is deliberate.</b> Every
/// workbench verb is a spend, and a spend without an idempotency key is a double-spend waiting for a
/// network retry. Equipping debits nothing, and <c>SaveAssignment</c> upserts on
/// <c>(specimen_id, role)</c>, so a retried equip lands the same row and a retried unequip finds the
/// role already empty.</para>
/// </summary>
public static class ItemEquipEndpoints
{
    public sealed record EquipRequest(long? PlayerId, string? SpecimenId, string? InstanceId, string? Role);

    public sealed record UnequipRequest(long? PlayerId, string? SpecimenId, string? Role);

    public static void MapItemEquip(this WebApplication app, ItemEquipService equip)
    {
        if (equip is null) throw new ArgumentNullException(nameof(equip));

        // What a specimen is actually wearing, across all fifteen roles. Module 4's own table, so it
        // sits with the writes rather than in module 20's read-only file — and unlike
        // `GET /api/unique/actors/{id}/equipment` it does not project through the three legacy slot
        // words, because an item can occupy any of the fifteen.
        app.MapGet("/api/items/assignments/{specimenId}", (string specimenId) =>
            Results.Ok(equip.List(specimenId)));

        app.MapPost("/api/items/equip", async (EquipRequest body, RpgStore store, UniqueActorService uniqueActorService) =>
        {
            if (body.SpecimenId is not { Length: > 0 } specimenId)
                return Results.BadRequest(new { error = "specimenId required" });
            if (body.InstanceId is not { Length: > 0 } instanceId)
                return Results.BadRequest(new { error = "instanceId required" });
            if (body.Role is not { Length: > 0 } role)
                return Results.BadRequest(new { error = "role required" });

            var playerId = body.PlayerId ?? store.GetCurrentPlayerId();
            var outcome = equip.Equip(playerId, specimenId, instanceId, role);
            if (outcome.Ok)
                await SyncLawnRuntimeAsync(store, uniqueActorService, specimenId, playerId).ConfigureAwait(false);
            return Render(outcome);
        });

        app.MapPost("/api/items/unequip", async (UnequipRequest body, RpgStore store, UniqueActorService uniqueActorService) =>
        {
            if (body.SpecimenId is not { Length: > 0 } specimenId)
                return Results.BadRequest(new { error = "specimenId required" });
            if (body.Role is not { Length: > 0 } role)
                return Results.BadRequest(new { error = "role required" });

            var playerId = body.PlayerId ?? store.GetCurrentPlayerId();
            var outcome = equip.Unequip(playerId, specimenId, role);
            if (outcome.Ok)
                await SyncLawnRuntimeAsync(store, uniqueActorService, specimenId, playerId).ConfigureAwait(false);
            return Render(outcome);
        });
    }

    /// <summary>
    /// P1.5-L (2026-09-07) — the Lawn half of module 5's equip wiring, found missing by reading
    /// <c>RpgStore.MaterializeRolledEquipRuntime</c>'s own doc comment: its only production caller was
    /// <c>WebMatchService.BuildSquad</c> (Battle/expedition), so a specimen bound to the live Lawn never
    /// had its rolled-item `effect_binding` rows materialized at all — equip/unequip persisted the
    /// assignment and changed nothing else. This closes the gap the same way Battle already does: run
    /// the full projection (bindings + granted actions), then re-push the compiled atom union so the
    /// injector's own <c>effects.grants.apply</c> path picks it up — the same call
    /// <see cref="UniqueActorService.PushAtomUnionAsync"/> makes for a bind/unbind transition.
    ///
    /// <para>Best-effort like every other post-Hello atom push: a failure here must never turn a
    /// successful equip write into a 500 for the caller, so it is caught and logged, not surfaced.</para>
    /// </summary>
    static async Task SyncLawnRuntimeAsync(RpgStore store, UniqueActorService uniqueActorService, string specimenId, long playerId)
    {
        try
        {
            var actor = store.GetUniqueActor(specimenId);
            if (actor is null) return;
            store.MaterializeRolledEquipRuntime(specimenId, checked((int)actor.Level));
            await uniqueActorService.PushAtomUnionAsync(playerId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[item-equip] Lawn runtime sync failed: " + ex.Message);
        }
    }

    /// <summary>
    /// A refused operation is <b>409 with the named rule</b>, never a 200 carrying a sad face and never
    /// a bare 400 — the request was well formed and the answer is "the rules say no". Identical to
    /// <c>WorkbenchEndpoints.Render</c> on purpose: one refusal shape across the item program's whole
    /// write surface, so `httpErrorMessage` lifts `reason` out of either without a special case.
    /// </summary>
    static IResult Render(ItemEquipOutcomeDto outcome) =>
        outcome.Ok ? Results.Ok(outcome) : Results.Json(outcome, statusCode: StatusCodes.Status409Conflict);
}
