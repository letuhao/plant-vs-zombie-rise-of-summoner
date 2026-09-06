using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Board;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Delve.Battle;

/// <summary>
/// D2.12 — the one place a delve battle resolves. `BattleEngine.Resolve` is called **explicitly**
/// with the `delve` profile pinned — never `WaveCatalog.ProfileForExpedition`/`.ProfileFor`, both of
/// which resolve a profile by `waveId` (a wave-catalog concept a delve encounter has none of; its own
/// profile choice is always `BattleModeProfileCatalog.Delve`, never a per-encounter pick). Setup
/// construction is `encounter-generator`'s (`Θ_room` + the party half); the intent source is
/// `RaidIntentSource`'s (D2.13) — both are parameters here, not built by this file.
/// </summary>
public static class DelveBattle
{
    public static BattleReport Run(
        BattleSetup setup, ulong seed, BattleTrace? trace = null,
        Action<BattleEffectHost>? onEffectHostReady = null, ActionCatalog? actionCatalog = null,
        IContainerEffectResolver? containerResolver = null, IIntentSource? intentSource = null,
        BoardState? board = null)
        => BattleEngine.Resolve(
            setup, seed, trace, onEffectHostReady,
            profile: BattleModeProfileCatalog.Delve,
            actionCatalog, containerResolver, intentSource, board);
}
