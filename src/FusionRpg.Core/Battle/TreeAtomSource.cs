using FusionRpg.Core.PassiveTree.Catalog;
using FusionRpg.Core.Power;

namespace FusionRpg.Core.Battle;

/// <summary>
/// The third source of the shape <see cref="TraitAtomSource"/>/<see cref="EquipAtomSource"/> already
/// ship (task D6, spec-tree-resolve.md §2.1, §2.2, §12 tests 15-16), emitting
/// <see cref="BattleChannelMod"/> for the battle composer's additive-only pipeline.
///
/// <para><b>ONE resolve, not two.</b> Unlike <see cref="EquipAtomSource"/> (which parses its own atom
/// rows twice, once per projection, because the SOURCE format is the same on both sides), the lawn
/// projection for trees already exists and is already the source of truth:
/// <see cref="PassiveTree.Resolve.TreeAtomSource.BoundAtomsFor"/> (task B6) — gate-open, owned,
/// enabled, `stat.derived`-kind filtering, and the PS-3 `P(Θ)`/`Θ` axis split, all already correct and
/// already tested. This class calls that function and re-shapes its output for battle; it never
/// re-implements gating, ownership or axis selection, so lawn and battle can never drift on WHICH
/// atoms contribute or by HOW MUCH before the final `double`→`long` step below.</para>
///
/// <para><b>The op is deliberately not read here</b> — the same named gap
/// <see cref="EquipAtomSource.ModsFor"/> documents: `BattleStatComposer` folds every mod additively
/// (<c>snap.Set(ch, snap.Get(ch) + mod.Amount)</c>), so a node atom declaring `increased` or `replace`
/// applies as if it were `flat` on the battle side. No shipped content trips it today.</para>
///
/// <para><b>No new subsystem, no new order band.</b> `BattleStatComposer.Compose` already folds
/// `Traits`/`Equipment` as two producer slots over the same additive snapshot; this is a third slot
/// composed the identical way — no `IActorStatSubsystem`, no `AtomDerivedSubsystem` registration, and
/// the existing three registrations (Traits, Equipment, and the lawn's own `AtomDerivedSubsystem`
/// producer) are untouched.</para>
/// </summary>
public static class TreeAtomSource
{
    /// <summary>Every live tree-node contribution, as additive battle channel mods. Attribution
    /// (`SourceId = tree.{treeId}.{nodeId}`) is carried by the underlying
    /// <see cref="PassiveTree.Resolve.TreeAtomSource.BoundAtomsFor"/> call and reaches the lawn's
    /// `DerivedContributionBag` unchanged (B6) — <see cref="BattleChannelMod"/> itself carries no
    /// SourceId, matching every other battle-side producer (`TraitAtomSource`/`EquipAtomSource`), so
    /// this projection intentionally does not retrofit one.
    ///
    /// <para><b>D7 — `fMilli` is forwarded, never re-applied.</b> `F` is multiplied into the amount
    /// exactly once, inside the lawn's <see cref="PassiveTree.Resolve.TreeAtomSource.BoundAtomsFor"/>
    /// (spec-tree-resolve.md §5.3: "in both read modes" means the SAME multiply reaches both outputs,
    /// not that battle repeats it). This function's only job is the existing lawn-`double`-to-
    /// battle-`long` rounding step below, applied to an amount that is already `F`-scaled.</para></summary>
    public static IReadOnlyList<BattleChannelMod> ModsFor(
        LoadedTree tree, IReadOnlySet<string> ownedNodeIds, int tierReached, long thetaNode, PowerTuning powerTuning,
        long fMilli)
    {
        var bound = PassiveTree.Resolve.TreeAtomSource.BoundAtomsFor(tree, ownedNodeIds, tierReached, thetaNode, powerTuning, fMilli);
        var mods = new List<BattleChannelMod>(bound.Count);
        foreach (var atom in bound)
            mods.Add(new BattleChannelMod(atom.Channel, RoundHalfAwayFromZero(atom.Amount)));
        return mods;
    }

    /// <summary>The one rounding rule where a lawn `double` magnitude becomes a battle `long` one —
    /// `RoundHalfAwayFromZero`, the same convention `PowerLadder`/`ContentScale.Apply`/`ChannelLadder`/
    /// `Concentration` already share (spec-tree-catalog.md's code-style note), never a silent
    /// truncation toward zero.</summary>
    static long RoundHalfAwayFromZero(double value) =>
        (long)Math.Round(value, MidpointRounding.AwayFromZero);
}
