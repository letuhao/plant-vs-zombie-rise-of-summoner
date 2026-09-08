namespace FusionRpg.Core.Items;

/// <summary>
/// The two <c>ref_kind</c> values <c>rpg_item_assignment</c> actually carries, in one place.
///
/// <para>⛔ <b>This is NOT the same vocabulary as <c>rpg_item_loadout_entry</c>'s.</b> That table —
/// module 2's saved presets — spells its instance-pinned kind <c>"item"</c>
/// (<see cref="LoadoutReport.InstanceRefKind"/>, and <c>GetLoadoutEntriesValidated</c>'s own SQL
/// switches on it). Two tables, two vocabularies, one word apart, and nothing named the difference
/// until a reader in a third module tested an assignment row against the <i>preset</i> table's
/// literal and silently saw nothing (defect R2, 2026-09-06). Both constants live here so that
/// mistake has to be made deliberately.</para>
/// </summary>
public static class EquipRefKinds
{
    /// <summary>One rolled copy: <see cref="EquipAssignment.RefId"/> is an
    /// <c>effect_instance.instance_id</c>. Written by <c>POST /api/items/equip</c>, and the only kind
    /// <c>RpgStore.ApplyEquipProjection</c> turns into a binding.</summary>
    public const string Rolled = "rolled";

    /// <summary>A catalog id: <see cref="EquipAssignment.RefId"/> is a <c>container_id</c>, so it
    /// never pins one specific copy. Written by the relic wire
    /// (<c>PUT /api/unique/actors/{id}/equipment/{slot}</c>) and by D1 §10 M1's row migration.</summary>
    public const string Stock = "stock";
}

/// <summary>One durable equip decision: this player put this item in this role on this specimen.
/// <paramref name="SpecimenId"/> is the `rpg_unique_actor`'s own stable `instance_id` — a kebab-case
/// string, matching `OwnerScope.UniqueActor`'s key exactly (`OwnerScope.cs`: "keyed on the actor's own
/// stable instance_id" — never a numeric id). <c>RefKind</c> is <c>"rolled"</c> (<paramref name="RefId"/>
/// an `effect_instance.instance_id`) or <c>"stock"</c> (<paramref name="RefId"/> a `container_id` into
/// module 2's counter).</summary>
public sealed record EquipAssignment(string SpecimenId, ItemRole Role, string RefKind, string RefId, string AssignedUtc);

/// <summary>What an assignment's item actually asks of the specimen wearing it — supplied by the
/// caller (module 6 does not exist yet), matching how <c>BindGate.Check</c> already takes
/// <c>levelReq</c> as a parameter rather than reading it off a not-yet-existing type.</summary>
public readonly record struct EquipItemFacts(string? Frame, int? LevelReq, string? FactionReq);

public sealed record ProjectionResult(
    IReadOnlyList<EquipAssignment> Bindings,
    IReadOnlyList<(EquipAssignment Assignment, EquipRefusal Reason)> Shortfalls,
    IReadOnlyList<(EquipAssignment Assignment, EquipRefusal Reason)> Skipped);

/// <summary>
/// Assignments → bindings, a full rebuild every time — never a delta. `UpsertUniqueEquipment` already
/// works this way and `UniqueOwnerBinder.ToEntityKey` already discards the instance id at deploy, so
/// this is the shipped shape, not a simplification; it is also what makes unequip atomic (one
/// assignment row deleted, and the next projection simply does not produce that binding).
///
/// <para><b>Two moments, two tests.</b> <see cref="EquipGate.Admits"/> is the ASSIGN gate and is hard.
/// <see cref="EquipGate.Projectable"/> is the DEPLOY test and is deliberately weaker — a standing
/// assignment whose `level_req` lapsed still projects, because filtering it here would be
/// force-unequip wearing a projection's clothes (I11 §2.6). A lapse produces a reported shortfall,
/// never a missing binding.</para>
/// </summary>
public sealed class EquipProjector
{
    readonly EquipGate _gate;
    readonly Func<string, SpecimenActor> _actorOf;
    readonly Func<EquipAssignment, EquipItemFacts> _itemFactsOf;

    public EquipProjector(EquipGate gate, Func<string, SpecimenActor> actorOf, Func<EquipAssignment, EquipItemFacts> itemFactsOf)
    {
        _gate = gate;
        _actorOf = actorOf;
        _itemFactsOf = itemFactsOf;
    }

    public ProjectionResult Project(string specimenId, IReadOnlyList<EquipAssignment> assignments)
    {
        var actor = _actorOf(specimenId);

        // (assignment, item facts, admit refusal or null) computed once per row -- Admits and
        // Projectable would otherwise each re-derive the same facts independently.
        var judged = assignments.Select(a =>
        {
            var facts = _itemFactsOf(a);
            return (Assignment: a, Facts: facts,
                    AdmitRefusal: _gate.Explain(a.Role, actor, facts.Frame, facts.LevelReq, facts.FactionReq),
                    Projectable: _gate.Projectable(a.Role, actor, facts.Frame, facts.FactionReq));
        }).ToList();

        return new ProjectionResult(
            Bindings: judged.Where(j => j.Projectable).Select(j => j.Assignment).ToList(),
            Shortfalls: judged.Where(j => j.AdmitRefusal is not null)
                .Select(j => (j.Assignment, j.AdmitRefusal!.Value)).ToList(),
            // Invariant, asserted rather than assumed: today Projectable is strictly weaker than
            // Admits (it drops the level check only), so "not Projectable" always implies Explain
            // found SOME reason. A future Gate change that lets Projectable fail independently of
            // Explain must update this, not silently null-deref here.
            Skipped: judged.Where(j => !j.Projectable)
                .Select(j => (j.Assignment, j.AdmitRefusal ?? throw new InvalidOperationException(
                    $"role {j.Assignment.Role}: Projectable() refused but Explain() found no reason — " +
                    "the two must agree on every row Projectable rejects.")))
                .ToList());
    }
}
