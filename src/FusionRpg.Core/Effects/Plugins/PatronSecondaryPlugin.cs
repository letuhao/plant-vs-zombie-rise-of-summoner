using FusionRpg.Contracts;
using FusionRpg.Core.Demons.Patron;

namespace FusionRpg.Core.Effects.Plugins;

/// <summary>
/// Patron aura (spec-patron-demon.md): grants the match-scoped marker at match start and
/// freezes the aura for the running match. Grant-only Secondary discipline — the aura's combat
/// math is a pure read overlay at compose time, never a Unity write. The grant itself carries
/// no overlay (it is the session-visible lifecycle marker; magnitudes live in the frozen
/// PatronRuntimeState.MatchAura).
/// </summary>
public sealed class PatronSecondaryPlugin : IEffectGrantPlugin
{
    public string PluginId => "sec.patron.aura";

    public void OnMatchStart(EffectPluginContext ctx)
    {
        if (!PatronRuntimeState.TryGet(ctx.PlayerId, out var aura))
            return;

        PatronRuntimeState.BeginMatch(aura);
        ctx.Funnel.EnqueueModifier(new EffectGrantDto
        {
            GrantId = "patron:aura",
            EffectId = "fx.patron_aura",
            OwnerKey = EffectOwnerKeys.Match,
            PluginId = PluginId
        });
    }

    public void OnLoadoutChanged(EffectPluginContext ctx) { }

    public void OnOwnerChanged(EffectPluginContext ctx) { }

    public void OnRemoved(EffectPluginContext ctx)
    {
        PatronRuntimeState.EndMatch();
        EffectPluginGrantOps.WithdrawByPluginId(ctx, PluginId);
    }
}
