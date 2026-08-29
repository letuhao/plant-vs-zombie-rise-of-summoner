using FusionRpg.Core.Stats.Aptitudes;

namespace FusionRpg.Core.Combat.Guard;

/// <summary>
/// class-system-todo.md P7.1-P7.3 — the `poise` pool, drain, regen, and riposte conversion
/// (spec-guard-economy.md, read in full this session). "Shaped after `ShieldRuntime`, deliberately"
/// (§7) — same per-owner pool-tracking shape (`Combat/Shield/ShieldRuntime.cs`, read in full this
/// session) — but NOT wired into the live combat pipeline the way shields are: shields absorb on
/// EVERY hit automatically, while poise pays for a deliberate "raise guard" ACTION that the action
/// layer (spec-action-costs.md) does not exist to trigger yet. This type is a complete, independently
/// testable mechanism, ready for that layer to call — mirroring how `AptitudeSubsystem` (P2.4) shipped
/// correct and inert before `battle-adoption` had a caller for it.
///
/// <para><b>Reading C</b> (§3, owner decision 2026-08-26) — both halves, not one: <see cref="Commit"/>
/// is the flat commit cost, paid on every guard raise REGARDLESS of outcome (spec-action-costs.md §3:
/// "committing is what costs, not landing" — the same rule shields are exempt from, §7, because
/// "nothing ever pays a shield to act"). <see cref="Absorb"/> is the proportional drain, charged only
/// against what was actually stopped.</para>
///
/// <para><b>Zero poise means exhaustion, never death</b> (§2's table — poise is NOT exempt from
/// exhaustion the way `hp` is exempt from it). Structurally guaranteed here: neither <see cref="Commit"/>
/// nor <see cref="Absorb"/> can drive the pool below zero (both floor at 0, never throw on
/// insufficient poise), and neither touches HP or any other resource — <see cref="IsExhausted"/> is
/// the guard-broken signal a caller reads, nothing here can trigger anything HP-shaped.</para>
/// </summary>
public sealed class PoiseRuntime
{
    readonly Dictionary<string, long> _poise = new(StringComparer.Ordinal);

    public long PoiseOf(string ownerKey) => _poise.GetValueOrDefault(ownerKey);

    /// <summary>Guard broken — spec §2's table: every resource except `hp` gets exhaustion on empty,
    /// never death.</summary>
    public bool IsExhausted(string ownerKey) => PoiseOf(ownerKey) <= 0;

    /// <summary>Sets a pool directly — encounter start, or a test fixture. Not part of the cost/drain
    /// contract; those only ever subtract.</summary>
    public void SetPoise(string ownerKey, long value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "poise cannot be set negative");
        _poise[ownerKey] = value;
    }

    /// <summary>Reading C's flat half (§3 test 1): raising a guard costs EVEN WHEN NOTHING LANDS.
    /// Floors at zero rather than throwing on insufficient poise — an actor with 10 poise can still
    /// commit a 50-cost guard; it simply exhausts rather than refusing the action (§2: exhaustion, not
    /// a hard block — this program refuses hard caps, PS-8, and a "cannot afford to guard" refusal
    /// would be exactly that in a different shape).</summary>
    public void Commit(string ownerKey, long flatCost)
    {
        if (flatCost < 0) throw new ArgumentOutOfRangeException(nameof(flatCost), flatCost, "flatCost cannot be negative");
        var current = PoiseOf(ownerKey);
        _poise[ownerKey] = Math.Max(0, current - flatCost);
    }

    /// <summary>Reading C's proportional half (§3 test 2): drains against what was actually stopped,
    /// never the incoming hit's full size. Returns the amount ACTUALLY drained — less than the "ideal"
    /// share once the pool runs dry, mirroring `ShieldRuntime.Absorb`'s own "never spend more than is
    /// there" contract. `long`: fed by `P(Θ)` through Vigor/Bulwark-shaped edges once wired, so this
    /// widens before multiplying and divides by 1000 last (CLAUDE.md's overflow discipline).</summary>
    public long Absorb(string ownerKey, long damageStopped, long absorbDrainSharePermille)
    {
        if (damageStopped < 0) throw new ArgumentOutOfRangeException(nameof(damageStopped), damageStopped, "damageStopped cannot be negative");

        long ideal;
        checked { ideal = damageStopped * absorbDrainSharePermille / 1000; }
        var current = PoiseOf(ownerKey);
        var actual = Math.Min(ideal, current);
        _poise[ownerKey] = current - actual;
        return actual;
    }

    /// <summary>Per-tick regen, sized by the caller against peer pressure (§4: `r = poiseRegen /
    /// peerPressure` must stay under 1 — that is `PhaseModel`'s own concern at prediction time, not
    /// this runtime's; this method only ever applies whatever rate it is given, capped at
    /// <paramref name="maxPoise"/>, never spilling over the way `ShieldRuntime.Tick`'s own carry
    /// does not spill between shields).</summary>
    public void Regen(string ownerKey, long regenPerTick, long maxPoise)
    {
        if (regenPerTick < 0) throw new ArgumentOutOfRangeException(nameof(regenPerTick), regenPerTick, "regenPerTick cannot be negative");
        if (maxPoise < 0) throw new ArgumentOutOfRangeException(nameof(maxPoise), maxPoise, "maxPoise cannot be negative");
        var current = PoiseOf(ownerKey);
        checked { _poise[ownerKey] = Math.Min(maxPoise, current + regenPerTick); }
    }

    /// <summary>§5: spent `poise` converts to damage — BASTION's missing offence, the reason Reading C
    /// is necessary rather than merely tidy (a guard that costs nothing when it stops nothing would
    /// also produce nothing). BOUNDED RATIO (PS-8 exempt, §8's own required comment): a fraction of
    /// poise spent, in [0,1] via <paramref name="riposteShareCapPermille"/> — not a cap on damage, the
    /// poise it converts is uncapped (<see cref="AptitudeCatalog"/>'s own no-aptitude-cap guarantee
    /// feeds it), so the output is too.</summary>
    public static long Riposte(long spentPoise, long riposteShareCapPermille)
    {
        if (spentPoise < 0) throw new ArgumentOutOfRangeException(nameof(spentPoise), spentPoise, "spentPoise cannot be negative");
        checked { return spentPoise * riposteShareCapPermille / 1000; }
    }
}
