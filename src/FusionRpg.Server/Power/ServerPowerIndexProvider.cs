using FusionRpg.Core.Power;
using FusionRpg.Core.Stats;
using FusionRpg.Data;

namespace FusionRpg.Server.Power;

/// <summary>
/// Server-side Θ index — hydrates <see cref="ActorIndex"/> from <c>rpg_actor_progression</c> via
/// <see cref="RpgStore"/> (T1.4; tunables-ssot.md §7.2 boundary — SQL stays inside FusionRpg.Data,
/// this class only calls an existing store method, so it never trips <c>guard-dal.ps1</c>).
///
/// <para><b>Partial hydration, documented rather than hidden:</b> only <c>daveLevel</c> has a
/// persistent column today. <c>realmsAdvanced</c> and <c>pvzRuns</c> have no column anywhere in the
/// schema — <c>empire-economy-ssot.md §4</c>, which ssot-power-scale.md §5 cites as realmsAdvanced's
/// source, does not currently define one either (searched; zero matches for "realm" in that doc).
/// World retirement/prestige is an unbuilt feature, not a wiring gap this task can close. Both
/// therefore read as 0 via <see cref="PowerIndexComposer"/>'s existing "absence, not corruption"
/// clamp — the same contract an un-hydrated actor already gets, so this is not a special case.</para>
///
/// <para><see cref="ContentIndex"/> needs no store access at all: every content-side input
/// (dangerBand/worldTier/zombossLevel/realmsAdvanced) arrives already resolved on
/// <see cref="ContentContext"/> — the caller's job (a later phase's), not this provider's.</para>
/// </summary>
public sealed class ServerPowerIndexProvider : IPowerIndexProvider
{
    readonly RpgStore _store;
    readonly PowerTuning _tuning;

    public ServerPowerIndexProvider(RpgStore store, PowerTuning tuning)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
    }

    public int ActorIndex(StatContext ctx) => PowerIndexComposer.ActorExplain(_tuning, ReadSnapshot(ctx)).Total;

    public int ContentIndex(ContentContext ctx) => PowerIndexComposer.ContentExplain(_tuning, ctx).Total;

    public PowerAxisReport Explain(StatContext ctx) => PowerIndexComposer.ActorExplain(_tuning, ReadSnapshot(ctx));

    ActorLadderSnapshot ReadSnapshot(StatContext ctx)
    {
        if (ctx.PlayerId is not { } playerId) return ActorLadderSnapshot.Empty;

        var summary = _store.GetRpgProgressionSummary(playerId);
        if (summary?.Player is not { } player) return ActorLadderSnapshot.Empty;

        int daveLevel = checked((int)player.Level);
        return new ActorLadderSnapshot(daveLevel, RealmsAdvanced: 0, PvzRuns: 0);
    }
}
