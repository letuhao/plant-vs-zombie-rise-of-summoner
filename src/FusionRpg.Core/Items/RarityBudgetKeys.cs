namespace FusionRpg.Core.Items;

/// <summary>
/// One `rarity_budget` key: which module owns reading it, and whether that module has a decided
/// shape for it. SC7: "a row no code consumes is a lie in a table" — <see cref="IsRegistered"/>
/// gates on a key having a **named, spec-assigned consumer**, the same bar item-ideal.md's own
/// `ContentRuleViolated` namespace registry uses (a namespace registers once a lane starts raising
/// rule ids under it, not once its full runtime ships). `promote_from`, `enhance_cap` and
/// `power_ceiling` are seeded now with that bar met, even though modules 11/15 are not yet built —
/// `ssot-rarity.md` §5 marks all five "seeded now" for exactly this reason. `set_eligible` and
/// `charm_potency` fail the bar outright: no spec reads either, so they are absent from
/// <see cref="All"/> entirely, not merely unshipped.
/// </summary>
public readonly record struct RarityBudgetKeyDef(string Key, string ConsumerModule, bool HasDecidedShape);

public sealed class RarityBudgetKeyRejection : Exception
{
    public RarityBudgetKeyRejection(string message) : base(message) { }
}

/// <summary>
/// The closed key registry (item-ideal.md, `rarity-bands`, module 7). Never grown ad hoc — a new key
/// is a reviewed addition here, naming its consumer, exactly like every other closed vocabulary in
/// this program.
/// </summary>
public static class RarityBudgetKeys
{
    public static readonly IReadOnlyList<RarityBudgetKeyDef> All = new[]
    {
        new RarityBudgetKeyDef("promote_from", "enhance-reroll (15)", HasDecidedShape: true),
        new RarityBudgetKeyDef("pity_guarded", "drop-volume (11)", HasDecidedShape: true),
        new RarityBudgetKeyDef("drop_weight_default", "drop-volume (11)", HasDecidedShape: true),
        new RarityBudgetKeyDef("enhance_cap", "enhance-reroll (15)", HasDecidedShape: true),
        new RarityBudgetKeyDef("power_ceiling", "item-power-reads (9)", HasDecidedShape: true),

        // ⭐ UNBLOCKED 2026-09-04 by module 14 (salvage-craft), which decided the shape ssot-rarity.md
        // §5 recorded as "awaiting I9": one integer per rung, the SUBSTRATE quantity a salvage of that
        // rung returns before the affix bonus (`salvageCoefficient.{rung}.substrateBase` in
        // data/tuning/materials.v1.json, seeded by RpgStore.SeedSalvageYield). It satisfies §9.8's one
        // constraint on this key — it must NOT reuse `shard.{DemonRarity}` ids — by naming no shard id
        // at all: the shard leg is R1's rung−1 rule, which is derived, not a per-rung budget row.
        new RarityBudgetKeyDef("salvage_yield", "salvage-craft (14)", HasDecidedShape: true),

        // Awaiting a decided shape — named in ssot-rarity.md §5 as "awaiting", not seeded here.
        new RarityBudgetKeyDef("socket_min", "sockets (16)", HasDecidedShape: false),
        new RarityBudgetKeyDef("socket_max", "sockets (16)", HasDecidedShape: false),
        new RarityBudgetKeyDef("reroll_cost_mult", "enhance-reroll (15)", HasDecidedShape: false),

        // set_eligible and charm_potency are deliberately ABSENT, not merely undecided: D15 makes the
        // former vacuous (a set has no rarity) and spec-set-charm-gen.md never reads the latter. Never
        // re-add either without a spec that actually consumes it.
    };

    /// <summary>
    /// True only when <paramref name="key"/> is in the closed list AND its named module has a
    /// decided shape for it. A key present but not yet decided (e.g. `socket_min`) is registered as a
    /// future obligation but still refuses a seed row today — the same "not decided ≠ safe default"
    /// rule this whole program applies everywhere else.
    /// </summary>
    public static bool IsRegistered(string key) =>
        All.Any(k => string.Equals(k.Key, key, StringComparison.Ordinal) && k.HasDecidedShape);

    public static void Validate(string key)
    {
        if (!IsRegistered(key))
            throw new RarityBudgetKeyRejection(
                $"rarity_budget key '{key}' is not registered with a decided consumer shape — " +
                "SC7 refuses a row no code consumes rather than letting it sit inert");
    }
}
