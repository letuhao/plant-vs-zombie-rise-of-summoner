using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Battle;

/// <summary>
/// Where a specimen's equipped items' static channel mods come from (item-ideal.md, `equip-runtime`
/// — module 5, the payoff). The same shape <see cref="TraitAtomSource"/> already ships (E12): bound
/// `stat.derived` atoms merge at COMPOSE time, a path battle already runs. Equipment differs only in
/// where the bindings come from — the durable assignment projection (module 4), resolved through
/// <c>ResolveBindings</c> at <c>unique-actor:</c> scope, rather than a trait catalog. Nothing new in
/// the pipeline.
///
/// <para><b>Closes the write-only half of a live production defect.</b>
/// <c>ProduceAndBind</c> already binds a `UniqueActor`'s items (`RpgStore.UniqueActors.cs`); this is
/// the first read of them. Before this, <c>UniqueActor</c> bindings existed only to be written.</para>
/// </summary>
public sealed class EquipAtomSource
{
    readonly Func<string, IReadOnlyList<AtomRow>> _resolveEquippedAtoms;

    EquipAtomSource(Func<string, IReadOnlyList<AtomRow>> resolveEquippedAtoms) => _resolveEquippedAtoms = resolveEquippedAtoms;

    /// <summary>Nothing wired — every specimen resolves to no equipment mods. The pre-module-5 state.</summary>
    public static readonly EquipAtomSource None = new(_ => Array.Empty<AtomRow>());

    /// <summary>
    /// Production shape: <paramref name="resolveEquippedAtoms"/> is
    /// <c>specimenId => store.ResolveBindings(OwnerScope.UniqueActor(specimenId), ctx).AtomsByBinding</c>
    /// flattened — the caller supplies it so this class stays free of `FusionRpg.Data` (Core does not
    /// depend on Data), matching every other atom-source seam in this program.
    /// </summary>
    public static EquipAtomSource FromResolver(Func<string, IReadOnlyList<AtomRow>> resolveEquippedAtoms) =>
        new(resolveEquippedAtoms ?? throw new ArgumentNullException(nameof(resolveEquippedAtoms)));

    /// <summary>
    /// This specimen's equipped `stat.derived` channel mods. Only `stat.derived` contributes — the
    /// one kind whose consumer battle has, exactly as <see cref="TraitAtomSource"/> already restricts
    /// itself.
    /// </summary>
    public IReadOnlyList<BattleChannelMod> ModsFor(string specimenId)
    {
        var mods = new List<BattleChannelMod>();
        foreach (var atom in _resolveEquippedAtoms(specimenId))
        {
            if (!string.Equals(atom.KindId, "stat.derived", StringComparison.Ordinal)) continue;

            var pars = Effects.Atoms.Power.CostFunction.Read(atom.ParamsJson);
            if (!pars.TryGetValue("channel", out var chEl)
                || chEl.ValueKind != System.Text.Json.JsonValueKind.String) continue;
            if (!pars.TryGetValue("amount", out var amtEl)
                || !amtEl.TryGetInt32(out var amount)) continue;

            mods.Add(new BattleChannelMod(chEl.GetString()!, amount));
        }
        return mods;
    }
}
