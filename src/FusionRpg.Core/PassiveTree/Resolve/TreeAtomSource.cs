using FusionRpg.Core.PassiveTree.Catalog;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Stats.Derived.Subsystems;

namespace FusionRpg.Core.PassiveTree.Resolve;

/// <summary>
/// The third source of the shape `TraitAtomSource`/`EquipAtomSource` already ship (spec-tree-resolve.md
/// §2.1-2.2, task B6). Fans owned, gate-open nodes' `stat.derived` atoms into
/// <see cref="BoundDerivedAtom"/>s — the SAME shape `AtomDerivedSubsystem` already reads via its
/// `boundFor` delegate. No new subsystem, no new order band, no eviction of the existing three: this
/// is a PRODUCER composed into the existing fan-in, never a fourth registration.
///
/// <para>Source id convention: `tree.{treeId}.{nodeId}` — one row per node, so attribution reaches
/// the player through the existing `DerivedContributionBag` path with no second component.</para>
/// </summary>
public static class TreeAtomSource
{
    /// <summary>Produces one `BoundDerivedAtom` per live, `stat.derived`-kind atom on every node the
    /// actor owns AND whose tier is at or below the reached gate. `tree-resolve` decides only
    /// whether a node's atoms are LIVE right now (gate open, node enabled) — it does not dispatch,
    /// trigger, or execute anything (§2.3); mechanism-class atoms are `mechanism-wiring`'s to
    /// execute and are skipped here by construction (their kind is never `stat.derived`).
    ///
    /// <para><b>D7 — `fMilli` applies D4/D5/D8's concentration multiplier here, and ONLY here.</b>
    /// This is the seam spec-tree-resolve.md §5.3 names: "`F` multiplies every tree-derived
    /// contribution — magnitude and contest alike" (every <see cref="ScaleAxis"/> branch below), "and
    /// nothing else" — a caller passes `fMilli` computed once, upstream, from
    /// <see cref="Concentration.FmaxAppliedMilli"/> over THIS actor's whole allocation; nothing not
    /// produced by this function ever sees it, so traits, equipment and base stats are structurally
    /// unaffected. `fMilli` is bounded below by `1000` (`F &gt;= 1.000`, §5.1's proof) — the caller is
    /// expected to pass `Concentration.FmaxAppliedMilli`'s own output, never a raw, unvalidated
    /// `H`.</para>
    ///
    /// <para>The multiplier is computed ONCE, upstream of the per-atom loop
    /// (`fMilli / 1000.0`), and applied as the LAST step inside <see cref="ResolveAmount"/> (CLAUDE.md
    /// rule 4: divide once, last) — so `fMilli = 1000` yields a multiplier of EXACTLY `1.0`, and
    /// multiplying any finite `double` by exactly `1.0` is an IEEE 754 identity operation: `Fmax =
    /// 1000‰` therefore removes `F` byte-identically (§5.4, test 9) without deleting the multiply
    /// itself — the code path still runs, it just multiplies by a no-op.</para></summary>
    public static IReadOnlyList<BoundDerivedAtom> BoundAtomsFor(
        LoadedTree tree, IReadOnlySet<string> ownedNodeIds, int tierReached, long thetaNode, PowerTuning powerTuning,
        long fMilli)
    {
        if (fMilli < 1000)
            throw new ArgumentOutOfRangeException(nameof(fMilli), fMilli,
                "F is bounded below by 1.000 (1000 per-mille) -- see spec-tree-resolve.md §5.1's proof");

        var result = new List<BoundDerivedAtom>();
        var ladder = new PowerLadder(powerTuning);
        var fMultiplier = fMilli / 1000.0; // computed ONCE, upstream of the loop -- see the class doc

        foreach (var node in tree.Nodes)
        {
            if (!node.Enabled) continue; // R2: a retired node is disabled, never live again
            if (!ownedNodeIds.Contains(node.NodeId)) continue;
            if (node.Tier > tierReached) continue; // the gate this actor has not opened yet (D11/D12:
                                                    // a closed gate invalidates, never repairs)
            if (ExclusionResolver.Resolve(tree, node, ownedNodeIds) is not null) continue; // D14/D40:
                                                    // an excluded node contributes zero -- reroute,
                                                    // precedence and nullification alike. The report
                                                    // builder (TreeResolveReport.Build) calls the SAME
                                                    // resolver so contribution and report can never drift.

            foreach (var atom in node.Atoms)
            {
                if (atom.KindId != "stat.derived") continue; // magnitude-class only; mechanism atoms
                                                              // are never this kind (§2.3)
                if (!AtomDerivedSubsystem.TryParseOp(NodeAtomOpToWireString(atom.Op), out var composerOp))
                    continue; // a 'more' or unknown op on the derived side is refused at bind (B4/B2), never reached here

                var amount = ResolveAmount(atom, ladder, thetaNode, fMultiplier);
                var sourceId = ContributionSourceIds.Tree(tree.Tree.TreeId, node.NodeId);
                result.Add(new BoundDerivedAtom(atom.ChannelId, composerOp, amount, sourceId));
            }
        }
        return result;
    }

    /// <summary><paramref name="fMultiplier"/> (`fMilli / 1000.0`, computed once by the caller) is
    /// applied as the LAST step on every branch alike — "magnitude and contest alike" (§5.3) — never
    /// folded into an earlier term, so it can never change WHICH axis a channel reads, only scale the
    /// already-axis-correct result.</summary>
    static double ResolveAmount(NodeAtom atom, PowerLadder ladder, long thetaNode, double fMultiplier) => atom.ScaleAxis switch
    {
        // PS-3: magnitudes read P(Theta), contests read Theta, LINEARLY. Getting this branch wrong
        // is a SILENT failure -- the sheet number rises either way, only the multiplier's behavior
        // differs (spec-tree-resolve.md §5, PS-3).
        // No clamping: PowerLadder.Value itself throws PowerIndexOverflow past MaxIndex, and that
        // throw-never-wrap behavior is the correct one to let propagate (CLAUDE.md rule 5).
        ScaleAxis.PTheta => (double)atom.KMicro * ladder.Value(checked((int)thetaNode)) / 1_000_000.0 * fMultiplier,
        ScaleAxis.Theta => (double)atom.KMicro * thetaNode / 1_000_000.0 * fMultiplier,
        ScaleAxis.FlatPermille => (double)atom.KMicro / 1_000_000.0 * fMultiplier,
        _ => throw new ArgumentOutOfRangeException(nameof(atom), atom.ScaleAxis, "unknown scaleAxis"),
    };

    static string NodeAtomOpToWireString(NodeAtomOp op) => op switch
    {
        NodeAtomOp.Flat => "flat",
        NodeAtomOp.Increased => "increased",
        NodeAtomOp.Replace => "replace",
        NodeAtomOp.Flag => "flag",
        _ => "",
    };
}
