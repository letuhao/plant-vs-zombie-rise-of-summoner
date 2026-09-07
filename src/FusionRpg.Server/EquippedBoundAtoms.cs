using FusionRpg.Core.Battle;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items;
using FusionRpg.Core.Stats.Derived.Subsystems;
using FusionRpg.Data;

namespace FusionRpg.Server;

/// <summary>
/// Shared UniqueActor equip → <see cref="EquippedAtomInput"/> / <see cref="BoundDerivedAtom"/> path
/// so battle <see cref="BattleStatComposer"/> and sheet Hub fan-in mint the same
/// <c>equip:{role}:{itemRef}</c> SourceIds (GG-49).
/// </summary>
public static class EquippedBoundAtoms
{
    /// <summary>Role-tagged inputs for <see cref="EquipAtomSource.FromEquippedResolver"/>.</summary>
    public static IReadOnlyList<EquippedAtomInput> InputsFromStore(RpgStore store, string specimenId)
    {
        var roleToItem = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in store.ListAssignments(specimenId))
            roleToItem[ItemRoles.Id(a.Role)] = a.RefId;

        var resolution = store.ResolveBindings(
            new OwnerScope(OwnerKind.UniqueActor, specimenId),
            new BindContext(RuntimeId.Battle));

        if (resolution.AtomsByBinding is null || resolution.AtomsByBinding.Count == 0)
            return Array.Empty<EquippedAtomInput>();

        var inputs = new List<EquippedAtomInput>();
        foreach (var binding in resolution.Bindings)
        {
            if (!resolution.AtomsByBinding.TryGetValue(binding.BindingId, out var atoms)) continue;
            var role = string.IsNullOrWhiteSpace(binding.Slot) ? "unknown" : binding.Slot!;
            if (!roleToItem.TryGetValue(role, out var itemRef))
                itemRef = binding.InstanceId;
            foreach (var atom in atoms)
                inputs.Add(new EquippedAtomInput(role, itemRef, atom));
        }

        return inputs;
    }

    public static EquipAtomSource SourceFromStore(RpgStore store) =>
        EquipAtomSource.FromEquippedResolver(specimenId => InputsFromStore(store, specimenId));

    public static IReadOnlyList<BoundDerivedAtom> DerivedFromStore(RpgStore store, string specimenId)
    {
        var inputs = InputsFromStore(store, specimenId);
        if (inputs.Count == 0) return Array.Empty<BoundDerivedAtom>();
        return EquipAtomSource.FromEquippedResolver(_ => inputs).DerivedAtomsFor(specimenId);
    }
}
