using FusionRpg.Core.Stats.Aptitudes;

namespace FusionRpg.Core.Aura;

/// <summary>
/// aura-skill T10 (`spec-aura-magnitude.md` §1): how strong an aura is —
/// <c>k(rung) · share^γ · P(Θ)</c>, through the SHARED <see cref="AptitudeReadFunctions.Magnitude"/>,
/// never a second copy of that arithmetic. Two axes, per the owner's own decision (Q10, 2026-08-30):
/// the aura's own level (`rung`, via the declared <see cref="AuraTuning.RungMapping"/>) and the
/// commander's specialization in that aura's aptitude (`share`) — a commander built for offense
/// cannot buff defence well, because `share` for a defensive aptitude is near zero for them, and at
/// exactly zero the product is exactly zero (base-independence: the result depends only on this
/// aura's own two axes, never on what else contributes to the target channel).
///
/// <para><b>Not "the rung is the level."</b> `ActionRow.Rung` is an authored column nobody advances —
/// the mapping from rung to `k` is declared at registration (`spec-rung-table.md:137`), which is
/// exactly what <see cref="AuraTuning"/> is.</para>
///
/// <para><b>Lives outside `Core/Actions/` on purpose.</b> An earlier placement under `Actions/Aura/`
/// (the spec's own suggested path) tripped `ActionsPurityGuardTests` — that directory bans any bare
/// `double` declaration with no exceptions, and `share` (bounded [0,1], the same shape
/// `AptitudeReadFunctions.Magnitude` itself already takes) needs one. `AptitudeReadFunctions` lives in
/// `Stats/Aptitudes/` for the exact same reason; this type sits beside it rather than repeating the
/// mistake the purity guard exists to catch.</para>
/// </summary>
public static class AuraMagnitude
{
    /// <summary>
    /// The aura's contribution to one channel. `γ` is deliberately NOT a parameter here — it is
    /// `tuning.Read.Magnitude.ShareExponentMilli`, the SAME share→effect curve every other magnitude
    /// edge in `aptitudes.v2.json` uses (spec §6's own rule: a third, aura-local exponent would be a
    /// new power-shaped curve `guard-power.ps1` forbids, or a duplicate of an existing one — neither
    /// is acceptable). Emits the same `long` a `DerivedModifier`'s `Flat` value needs directly.
    /// </summary>
    public static long Compute(int rung, double share, long pTheta, AuraTuning auraTuning, AptitudeTuning aptitudeTuning)
    {
        if (aptitudeTuning is null) throw new ArgumentNullException(nameof(aptitudeTuning));
        var kMilli = (auraTuning ?? throw new ArgumentNullException(nameof(auraTuning))).KMilliFor(rung);
        return AptitudeReadFunctions.Magnitude(kMilli, share, aptitudeTuning.Read.Magnitude.ShareExponentMilli, pTheta);
    }
}
