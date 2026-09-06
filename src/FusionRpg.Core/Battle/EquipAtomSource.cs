using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Stats.Derived.Subsystems;

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
    ///
    /// <para><b>The op is deliberately not read here, and that is a named gap, not an oversight.</b>
    /// <c>BattleStatComposer</c> folds every equip mod additively
    /// (<c>snap.Set(ch, snap.Get(ch) + mod.Amount)</c>), so an equipped atom declaring
    /// <c>op: "increased"</c> or <c>op: "replace"</c> applies as if it were <c>flat</c> on the battle
    /// side. No shipped content trips it — the only concrete `stat.derived` atom in the corpus today
    /// is `atom.critical-hunter` at <c>op: "flat"</c> — but it is a real divergence from the derived
    /// contract, and <see cref="DerivedAtomsFor"/> (the primary/derived side, below) honours the op
    /// rather than reproducing this. Widening battle to match would move battle numbers, which is a
    /// battle-program change, not this seam's to make.</para>
    /// </summary>
    public IReadOnlyList<BattleChannelMod> ModsFor(string specimenId)
    {
        var mods = new List<BattleChannelMod>();
        foreach (var parsed in EquippedDerived(specimenId))
            mods.Add(new BattleChannelMod(parsed.Channel, parsed.Amount));
        return mods;
    }

    /// <summary>
    /// The same equipped atoms, projected for the <b>primary/derived</b> pipeline
    /// (<see cref="ActorHub"/> / <see cref="AtomDerivedSubsystem"/>) instead of the battle one.
    ///
    /// <para><b>Why a second projection and not a second pipeline.</b> `class-system-map.md` §2a.0
    /// decided 2026-08-26 that *the composers stay separate* — the battle seam is `ChannelMods`, and
    /// `BattleStatComposer` runs no subsystems. That decision is about not fusing the two composers;
    /// it says nothing against each side reading the SAME equipped atoms through its own seam, which
    /// is exactly what this is. The parse is shared with <see cref="ModsFor"/> (one
    /// <see cref="EquippedDerived"/> walk), so the two sides can never drift on which atoms count,
    /// which channel they name, or what magnitude they carry.</para>
    ///
    /// <para><b>The op IS honoured here</b>, through <see cref="AtomDerivedSubsystem.TryParseOp"/> —
    /// the shipped parser whose own contract is that an unknown or `more` op is a content error to
    /// skip, never something to coerce into a wrong-but-plausible `flat`. Skipping rather than
    /// coercing is what keeps a bad row visible at the bind gate instead of shipping a silently wrong
    /// number.</para>
    /// </summary>
    public IReadOnlyList<BoundDerivedAtom> DerivedAtomsFor(string specimenId)
    {
        var atoms = new List<BoundDerivedAtom>();
        foreach (var parsed in EquippedDerived(specimenId))
        {
            if (!AtomDerivedSubsystem.TryParseOp(parsed.Op, out var op)) continue;
            atoms.Add(new BoundDerivedAtom(parsed.Channel, op, parsed.Amount, parsed.SourceId));
        }
        return atoms;
    }

    /// <summary>
    /// The ONE parse of a specimen's equipped `stat.derived` atoms, shared by both projections above.
    ///
    /// <para><b><c>amount</c> is read as <c>long</c>, not <c>int</c>.</b> The first cut of this class
    /// used <c>TryGetInt32</c>, which does not throw on a larger magnitude — it returns false, so the
    /// atom was silently DROPPED. That is the exact shape CLAUDE.md's binding numeric rule forbids
    /// ("`long` for any magnitude... overflow throws, never wraps"), and a dropped equip line is a
    /// defect with no symptom. Every value the shipped corpus carries fits either width, so no number
    /// anywhere moves; what changes is that a magnitude above <c>int.MaxValue</c> now applies instead
    /// of vanishing.</para>
    /// </summary>
    IEnumerable<(string Channel, string? Op, long Amount, string SourceId)> EquippedDerived(string specimenId)
    {
        foreach (var atom in _resolveEquippedAtoms(specimenId))
        {
            if (!string.Equals(atom.KindId, "stat.derived", StringComparison.Ordinal)) continue;

            var pars = Effects.Atoms.Power.CostFunction.Read(atom.ParamsJson);
            if (!pars.TryGetValue("channel", out var chEl)
                || chEl.ValueKind != System.Text.Json.JsonValueKind.String) continue;
            if (!pars.TryGetValue("amount", out var amtEl)
                || !amtEl.TryGetInt64(out var amount)) continue;

            var op = pars.TryGetValue("op", out var opEl) && opEl.ValueKind == System.Text.Json.JsonValueKind.String
                ? opEl.GetString()
                : null;

            // SourceId is what DerivedContributionBag reports as "why did my number change" — the
            // atom's own id, so an equipped line is attributable to the exact row that produced it.
            yield return (chEl.GetString()!, op, amount,
                string.IsNullOrWhiteSpace(atom.AtomId) ? atom.FamilyId : atom.AtomId);
        }
    }
}
