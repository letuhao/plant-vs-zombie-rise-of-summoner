using FusionRpg.Core.Actions.Loadout;
using FusionRpg.Core.Actions.Unlock;

namespace FusionRpg.Core.Actions.Grants;

/// <summary>
/// T24 (spec-grant-seam.md §5, item 8): the cap question, answered by NAMING which existing cap
/// governs rather than inventing a new one on the assembled/granted set.
///
/// <para><b>"The number is not 8, and it is not one number"</b> — the item lane's original ask
/// conflated three different scarcities, each already owned and already built:</para>
///
/// <list type="bullet">
/// <item><see cref="HeldCap"/> — levelling unlocks HELD (`A11`/T19, tunable, the free faucet,
/// capped because it is free).</item>
/// <item><see cref="EquippedSkillCap"/> — equipped AT ONCE (`A16`/T21, the real bottleneck; the
/// innate and three basics are intrinsic and never count against it).</item>
/// <item><b>Granted by paid sources: uncapped, on purpose</b> (spec §5.1 — "an uncapped pool grows
/// the choice, never the power"). There is no third field here for that: this class HAS no
/// "grantedCap" member, which is the answer, not an omission.</item>
/// </list>
///
/// <para><b>"Exceeding an actual cap rejects at equip time"</b> (spec §5) — that is
/// <see cref="LoadoutSet.Validate"/>'s existing <c>LoadoutFull</c> rejection (T21), already built and
/// already tested. This class does not re-implement it; it names it.</para>
/// </summary>
public static class CapPolicy
{
    /// <summary>Levelling unlocks held — `UnlockTuning.Cap` (A11/T19).</summary>
    public static int HeldCap(UnlockTuning unlockTuning)
    {
        if (unlockTuning is null) throw new ArgumentNullException(nameof(unlockTuning));
        return unlockTuning.Cap;
    }

    /// <summary>Equipped skills at once — `LoadoutSet.MaxSize` (A16/T21). The innate and three
    /// basics are intrinsic and never counted against it.</summary>
    public const int EquippedSkillCap = LoadoutSet.MaxSize;
}
