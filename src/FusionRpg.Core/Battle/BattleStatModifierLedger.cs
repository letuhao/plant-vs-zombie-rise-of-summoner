using System.Linq;
using FusionRpg.Core.Stats;

namespace FusionRpg.Core.Battle;

/// <summary>
/// A18e (spec-battle-live-stat-modifiers.md §1): a sourced, per-actor, per-channel modifier ledger so
/// a triggered `stat.modify` grant can affect live combat, composed through the SAME
/// Flat→Increased(sum)→More(product)→Override phased math the overlay's primary stat system already
/// uses (<see cref="PhasedComposeStrategy"/>) — never a parallel percent-math implementation for
/// battle specifically, never <c>DerivedComposer</c> (wrong op vocabulary; that composer belongs to
/// `stat.derived`, a different kind).
/// </summary>
public sealed class BattleStatModifierLedger
{
    static readonly PhasedComposeStrategy Strategy = new();

    readonly Dictionary<(string ActorKey, string Channel), List<(string SourceGrantId, StatModifier Mod)>> _mods = new();

    /// <summary>Sourced by grant id, so <see cref="RemoveBySource"/> can revert exactly one source's
    /// own contribution without disturbing another source on the same channel.</summary>
    public void Add(string actorKey, string channel, string sourceGrantId, StatModifier mod)
    {
        var key = (actorKey, channel);
        if (!_mods.TryGetValue(key, out var list))
            _mods[key] = list = new List<(string, StatModifier)>();
        list.Add((sourceGrantId, mod));
    }

    /// <summary>Removes every (channel, mod) tuple this source added, across all of an actor's
    /// channels — proven directly in this module's own tests; nothing built by A17–A20 calls it in
    /// production yet (no grant is ever withdrawn), named honestly in the spec rather than hidden.</summary>
    public void RemoveBySource(string actorKey, string sourceGrantId)
    {
        foreach (var key in _mods.Keys)
        {
            if (key.ActorKey != actorKey) continue;
            _mods[key].RemoveAll(t => t.SourceGrantId == sourceGrantId);
        }
    }

    public IReadOnlyList<StatModifier> For(string actorKey, string channel) =>
        _mods.TryGetValue((actorKey, channel), out var list)
            ? list.Select(t => t.Mod).ToList()
            : Array.Empty<StatModifier>();

    /// <summary>The one entry point every live-read call site uses — <see cref="ActorState.LiveAtk"/>,
    /// the `Derived`-channel recompose, and `BattleEffectSink`'s own `ModifyStat` branch — never
    /// <see cref="PhasedComposeStrategy"/> directly, so there is exactly one place this module's own
    /// recompose math lives.</summary>
    public long Recompose(string actorKey, string channel, long baseline) =>
        (long)Math.Round(Strategy.ComposeChannel(baseline, For(actorKey, channel)));
}
