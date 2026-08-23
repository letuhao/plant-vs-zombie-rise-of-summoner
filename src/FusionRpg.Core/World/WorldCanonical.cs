using System.Globalization;
using System.Text;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.World.Intel;

namespace FusionRpg.Core.World;

/// <summary>
/// One canonical text form of a world. Everything determinism-related leans on this: template
/// determinism tests, the turn engine's state hash, and replay comparison.
///
/// Two rules keep it trustworthy as a hash input. Numbers format with
/// <see cref="CultureInfo.InvariantCulture"/> — a machine whose culture writes a non-ASCII negative
/// sign must not produce a different world hash. And collections are written in their **stored**
/// order, never re-sorted here, so a mis-ordered model shows up as a diff instead of being quietly
/// normalised away.
/// </summary>
public static class WorldCanonical
{
    public static string Write(WorldState w)
    {
        var sb = new StringBuilder();

        // The world's *id* is deliberately absent: it is identity, not state. Two worlds built from
        // the same template and seed and played the same way must hash identically, or a golden
        // could never be compared across saves, machines, or test fixtures.
        Row(sb, "world", w.TemplateId, w.Seed, w.CurrentTurn);

        foreach (var f in w.Factions)
            Row(sb, "faction", f.FactionId, f.Kind, f.Name, f.PolicyId, f.UpkeepHandicapMilli);

        foreach (var s in w.Sectors)
        {
            Row(sb, "sector", s.SectorId, s.TypeId, s.Climate, s.DangerBand, s.Phase, s.OwnerFactionId,
                s.StabilityMilli, s.PressureMilli, s.DepletionMilli, s.DevelopmentLevel,
                s.AuthoredIntel, s.LastSeenTurn, s.LayoutX, s.LayoutY,
                s.LoamStock, s.FractureIntensityMilli);

            foreach (var sl in s.Slots)
                Row(sb, "slot", s.SectorId, sl.SlotIndex, sl.SlotTypeId, sl.Element, sl.State,
                    sl.OwnerFactionId, sl.GuardWaveId, sl.GuardState);
        }

        foreach (var l in w.Lanes)
            Row(sb, "lane", l.LaneId, l.FromSectorId, l.ToSectorId, l.TypeId, l.Length, l.Width,
                l.HazardMilli, l.WardLevel, l.GateKeyId, l.State);

        foreach (var e in w.Entities)
        {
            Row(sb, "entity", e.EntityId, e.Kind, e.OwnerFactionId, e.AtSectorId, e.OnLaneId,
                e.OnLaneTowardSectorId, e.LaneProgressMilli, e.Stance, e.MovementRemaining,
                e.Routed ? 1 : 0);

            for (var i = 0; i < e.Members.Count; i++)
            {
                var m = e.Members[i];
                Row(sb, "member", e.EntityId, i, m.InstanceId, m.SpeciesId, m.Level, m.Hp, m.Wounds);
            }
        }

        // Belief is state, so it is hashed like state. Written last so a world with no intel yet
        // — every wave-1 save — produces exactly the bytes it always did.
        foreach (var faction in w.Intel)
        foreach (var snapshot in faction.Sectors)
        {
            Row(sb, "intel", faction.FactionId, snapshot.SectorId, snapshot.LastSeenTurn, snapshot.Detail,
                snapshot.OwnerFactionId, snapshot.Phase, snapshot.Climate, snapshot.DangerBand,
                snapshot.DevelopmentLevel);

            foreach (var slot in snapshot.Slots)
                Row(sb, "intel-slot", faction.FactionId, snapshot.SectorId, slot.SlotIndex,
                    slot.SlotTypeId, slot.Element, slot.GuardWaveId, slot.State, slot.GuardState);

            foreach (var force in snapshot.Forces)
                Row(sb, "intel-force", faction.FactionId, snapshot.SectorId, force.EntityId,
                    force.OwnerFactionId, force.Kind, force.Exact ? 1 : 0, force.Strength, force.BandIndex);
        }

        return sb.ToString();
    }

    static void Row(StringBuilder sb, params object?[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            if (i > 0) sb.Append('\t');
            sb.Append(Cell(cells[i]));
        }

        sb.Append('\n');
    }

    /// <summary>
    /// Invariant formatting, null as a placeholder, and no cell may carry the separators — a
    /// display name with a tab in it would otherwise silently shift every field after it.
    /// </summary>
    static string Cell(object? value) => value switch
    {
        null => "-",
        string s => Escape(s),
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture),
        ulong u => u.ToString(CultureInfo.InvariantCulture),
        ElementTypeId e => e.ToString(),
        Enum e => e.ToString(),
        _ => Escape(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "-")
    };

    static string Escape(string s) =>
        s.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
}
