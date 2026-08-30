using System.Linq;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Battle;

/// <summary>
/// aura-skill T4: the recompose seam for `Derived` channels (`combat.*`) — the
/// <see cref="ActorDerivedSnapshot"/> counterpart to <see cref="BattleStatModifierLedger"/>, which
/// already recomposes primary stat channels (atk/defense) the same sourced way. Sourced so a
/// toggled-off aura withdraws exactly its own contribution, never another source's.
///
/// <para><b>Idempotent by construction.</b> `Recompose` never accumulates onto the live value — for
/// every channel this ledger tracks, it writes <c>baseDerived.Get(channel) + Σ(active sources)</c>
/// into `live`, always computed from the frozen base, never from `live`'s own prior value. Calling it
/// twice in a row, or after a source withdrew, produces the exact value for the CURRENT set of active
/// sources (audit D2) — never a value that depends on how many times it happened to run before.</para>
///
/// <para><b>Deliberately not routed through `DerivedComposer`.</b> That composer folds a
/// `DerivedModifier` list whose "base" entry does not exist — `BattleStatComposer.Compose` seeds
/// `Derived` directly from actor setup fields, never from a modifier list — so mixing the frozen base
/// value with dynamic contributions is a plain sum here, the same reasoning
/// <see cref="ActorDerivedSnapshot.OverlayAdd"/> already established (T1). This is not a shortcut:
/// every channel this program's content actually targets (spec-aura-content.md) registers `FlatSum`
/// in production (`combat.*` channels, `DerivedStatRegistry.RegisterCombatDefaults`), and `FlatSum`
/// composing is exactly "sum every contribution" — plain addition IS that channel family's real
/// compose kind, not an approximation of it.</para>
/// </summary>
public sealed class BattleDerivedModifierLedger
{
    readonly Dictionary<(string ActorKey, string Channel), List<(string SourceId, double Value)>> _contributions = new();

    public void Add(string actorKey, string channel, string sourceId, double value)
    {
        var key = (actorKey, channel);
        if (!_contributions.TryGetValue(key, out var list))
            _contributions[key] = list = new List<(string, double)>();
        list.Add((sourceId, value));
    }

    /// <summary>Removes every (channel, value) tuple this source added, across all of an actor's
    /// channels — mirrors <see cref="BattleStatModifierLedger.RemoveBySource"/> exactly. The emptied
    /// channel entry is kept, not deleted: `Recompose` still needs to visit it once more to fall the
    /// live value back to the base (a channel with zero active sources still needs exactly one more
    /// write, or it keeps stale contributions on read).</summary>
    public void RemoveBySource(string actorKey, string sourceId)
    {
        foreach (var key in _contributions.Keys.Where(k => k.ActorKey == actorKey).ToList())
            _contributions[key].RemoveAll(t => t.SourceId == sourceId);
    }

    double Total(string actorKey, string channel) =>
        _contributions.TryGetValue((actorKey, channel), out var list) ? list.Sum(t => t.Value) : 0.0;

    /// <summary>Writes the recomposed value into every (actor, channel) pair this ledger has ever
    /// tracked for `actorKey` — including a channel every source has since withdrawn from, which
    /// still needs the fall-back-to-base write. A channel this ledger has never seen is never touched,
    /// which is what makes an EMPTY ledger a hard no-op: nothing is tracked, so nothing is visited,
    /// so `live` is byte-identical to before the call (proven in
    /// <c>BattleDerivedModifierLedgerTests.An_empty_ledger_recomposes_nothing</c>).</summary>
    public void Recompose(string actorKey, ActorDerivedSnapshot baseDerived, ActorDerivedSnapshot live)
    {
        foreach (var key in _contributions.Keys.Where(k => k.ActorKey == actorKey))
            live.Set(key.Channel, baseDerived.Get(key.Channel) + Total(actorKey, key.Channel));
    }
}
