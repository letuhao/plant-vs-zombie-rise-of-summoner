using FusionRpg.Contracts;
using FusionRpg.Core.Demons.Patron;

namespace FusionRpg.Core.Effects.Plugins;

/// <summary>
/// Patron aura (spec-patron-demon.md): grants the match-scoped marker at match start, gated on the
/// player having any patron designated at all. Grant-only Secondary discipline — no overlay, ever.
///
/// <para>The grant's magnitude (patron-absorption, `seed-to-concrete` T6.2, 2026-09-06) now comes
/// from `fx.patron_aura`'s own compiled atom action rows, resolved fresh per push from the player's
/// live patron row — never a value frozen here. This plugin's only remaining job is the gate: without
/// it, every player (even one with no patron) would carry the grant as a standing +0 no-op for the
/// whole match, which is harmless but pointless.</para>
/// </summary>
public sealed class PatronSecondaryPlugin : IEffectGrantPlugin
{
    public string PluginId => "sec.patron.aura";

    public void OnMatchStart(EffectPluginContext ctx)
    {
        if (!PatronRuntimeState.TryGet(ctx.PlayerId, out _))
            return;

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
        EffectPluginGrantOps.WithdrawByPluginId(ctx, PluginId);
    }
}
