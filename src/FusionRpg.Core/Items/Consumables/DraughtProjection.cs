using FusionRpg.Core.Battle;

namespace FusionRpg.Core.Items.Consumables;

/// <summary>
/// One resolved draught line: a derived channel and the signed amount it contributes for the run.
///
/// <para>⏸ <b>The amount arrives from the caller, and that is the seam, not a shortcut.</b> The 60-row
/// corpus holds <b>seeds</b> — a family and a <c>powerBand</c>, never a magnitude (seed-contract.md §3)
/// — so rolling a seed into a concrete container with real atom rows is the runtime generator's job
/// under the binding seed-to-concrete rule. This type takes what that generator produces; module 11
/// used the same injected-delegate shape for step 9's mint.</para>
/// </summary>
/// <param name="ContainerId">For the audit line and for the manifest's own ordering.</param>
/// <param name="Channel">A derived-stat channel id, e.g. <c>combat.power.fire</c>.</param>
/// <param name="Amount">
/// <b><c>long</c>, because it is a magnitude</b> (AGENTS.md), and signed only so the type can carry a
/// drawback later; <see cref="Apply"/> refuses a negative one today — see there.
/// </param>
public readonly record struct DraughtMod(string ContainerId, string Channel, long Amount);

/// <summary>
/// ssot-consumables.md §5.4 — how a draught reaches an actor, and why it is a <b>projection</b> rather
/// than a binding in v1.
///
/// <para>The scopes are seven and <b>there is no <c>actor:{instanceId}</c></b> (definitions §6), so a
/// per-specimen draught cannot be a binding. It does not need to be: <c>BattleActorSetup.ChannelMods</c>
/// is documented as "additive derived-channel adjustments (trait stat mods, equipment later)" and the
/// expedition resolver already drives exactly this road for injuries —
/// <c>ExpeditionResolver.ApplyInjuries</c> appends a <see cref="BattleChannelMod"/> to each victim
/// before the battles resolve.</para>
///
/// <para><b>A draught is the same transform with the opposite sign. That is the whole v1 runtime.</b></para>
///
/// <para>⭐ v1 is <b>per-squad</b>, which is §10.4's own answer: every member receives every mod. Making
/// it per-specimen is expressible on this same road (the mods are already per-actor) and would turn the
/// manifest into a targeting decision — recorded as the owner's, not decided here.</para>
/// </summary>
public static class DraughtProjection
{
    /// <summary>
    /// Append every manifest draught to every squad member, returning a new list. Pure: the input
    /// setups are not mutated, matching <c>ApplyInjuries</c> exactly.
    ///
    /// <para>⛔ <b>A non-positive amount is refused rather than applied.</b> A draught that lowers a
    /// channel is an injury wearing a potion's name, and the resolver already has a road for those
    /// with its own sign; silently accepting one here would let a content bug read as a mechanic. The
    /// refusal throws rather than clamping, per AGENTS.md — a clamp would turn "your draught did
    /// nothing" into a bug with no symptom.</para>
    /// </summary>
    public static IReadOnlyList<BattleActorSetup> Apply(
        IReadOnlyList<BattleActorSetup> squad,
        IReadOnlyList<DraughtMod> draughts)
    {
        if (squad is null) throw new ArgumentNullException(nameof(squad));
        draughts ??= Array.Empty<DraughtMod>();
        if (draughts.Count == 0) return squad;

        foreach (var d in draughts)
        {
            if (string.IsNullOrWhiteSpace(d.Channel))
                throw new ArgumentException(
                    $"draught '{d.ContainerId}' names no channel", nameof(draughts));
            if (d.Amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(draughts),
                    $"draught '{d.ContainerId}' contributes {d.Amount} to '{d.Channel}'; a draught is " +
                    "ApplyInjuries with the OPPOSITE sign (§5.4), so a non-positive amount is a content " +
                    "defect and throws rather than being clamped to nothing");
        }

        return squad.Select(s =>
        {
            var mods = s.ChannelMods.ToList();
            foreach (var d in draughts) mods.Add(new BattleChannelMod(d.Channel, d.Amount));
            return s with { ChannelMods = mods };
        }).ToList();
    }

    /// <summary>
    /// The run-start binding shape §4.3 / §9 item 10 fixes for the player-wide half — <b>one snapshot
    /// mechanism, two sources.</b> Charms bind at <c>player:{id}</c> with <c>source = 'charm'</c>;
    /// draughts do the same with <c>source = 'draught'</c>, the same <c>slot = NULL</c> and the same
    /// priority. "Whoever builds the run-start snapshot first owns it and the other adopts it" — module
    /// 22 <c>charm-carry</c> is unbuilt, so this module owns it and the charm side adopts this shape.
    /// </summary>
    public const string BindingSource = "draught";

    /// <summary>The owner scope a run-scoped draught binds at. There is no actor scope (§5.4).</summary>
    public const string BindingOwnerKind = "player";

    /// <summary>
    /// Withdrawal is <b>by source</b>, at run end — the index for which already exists on
    /// <c>effect_binding</c>. ⛔ It is NOT a clock: <c>effect_binding</c> carries no expiry, no
    /// duration and no until-tick (verified in the shipped DDL), so a timed buff must be a status and a
    /// run-scoped buff is a lifecycle. v1 uses the second, because it needs nothing new.
    /// </summary>
    public static string WithdrawalKey => BindingSource;
}
