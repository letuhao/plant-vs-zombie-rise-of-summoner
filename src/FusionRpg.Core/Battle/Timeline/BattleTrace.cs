using FusionRpg.Core.Combat;

namespace FusionRpg.Core.Battle.Timeline;

/// <summary>
/// Observation of a battle's internals — RNG draws, intra-round phase order, per-round state.
/// Opt-in and inert: <see cref="BattleEngine.Resolve(BattleSetup, ulong, BattleTrace?)"/> takes
/// null in production and every record site is a null-conditional call, so tracing cannot change
/// an outcome.
///
/// It exists for the kernel-adoption parity ladder (spec-kernel-adoption.md): a golden hash diff
/// says only "something moved", which is a poor debugging instrument. A trace localizes drift to
/// a stream, a phase, or a round. Captured from the pre-adoption engine — once the kernel lands
/// there is nothing left to capture, which is why this ships before any engine change.
/// </summary>
public sealed class BattleTrace
{
    readonly List<string> _lines = new();
    readonly Dictionary<string, List<int>> _draws = new(StringComparer.Ordinal);
    readonly List<string> _phases = new();

    /// <summary>
    /// Intra-round phase markers in the order they actually ran. A snapshot, not a live view:
    /// handing out the backing list lets a caller cast it back and append, so the fixture the
    /// byte-identity gate compares could disagree with <see cref="Digest"/> about the same battle.
    /// </summary>
    public IReadOnlyList<string> Phases => _phases.ToArray();

    /// <summary>
    /// One RNG draw. Records the returned VALUE, never a count — <c>SeededRng.NextUInt</c> uses
    /// rejection sampling, so one logical draw consumes a variable number of generator steps and
    /// a count assertion over the underlying generator would be meaningless.
    /// </summary>
    public void Draw(string stream, int value)
    {
        if (!_draws.TryGetValue(stream, out var list))
        {
            list = new List<int>();
            _draws[stream] = list;
        }

        list.Add(value);
        _lines.Add($"draw {stream} {value}");
    }

    /// <summary>
    /// Draws seen on one stream, in order. Empty for a stream that never drew. A snapshot, for the
    /// same reason as <see cref="Phases"/> — and so a caller holding a result mid-battle does not
    /// watch it grow underneath them.
    /// </summary>
    public IReadOnlyList<int> Draws(string stream) =>
        _draws.TryGetValue(stream, out var list) ? list.ToArray() : Array.Empty<int>();

    public void Phase(int round, string phase)
    {
        _phases.Add($"{round}:{phase}");
        _lines.Add($"phase {round} {phase}");
    }

    public void State(int round, string actorKey, int hp, long shieldAbsorbed) =>
        _lines.Add($"state {round} {actorKey} hp={hp} abs={shieldAbsorbed}");

    /// <summary>
    /// Decorates the combat RNG so crit-stream draws are recorded without touching
    /// <c>OverlayCombatCalculator</c> — that path is shared with the overlay hot path.
    /// </summary>
    public ICombatRng WrapCombat(string stream, ICombatRng inner) =>
        new TracingCombatRng(this, stream, inner);

    /// <summary>The whole trace as stable text — the fixture the parity ladder compares.</summary>
    public string Digest => string.Join("\n", _lines);

    sealed class TracingCombatRng : ICombatRng
    {
        readonly BattleTrace _trace;
        readonly string _stream;
        readonly ICombatRng _inner;

        public TracingCombatRng(BattleTrace trace, string stream, ICombatRng inner)
        {
            _trace = trace;
            _stream = stream;
            _inner = inner;
        }

        public int Next(int exclusiveMax)
        {
            var v = _inner.Next(exclusiveMax);
            _trace.Draw(_stream, v);
            return v;
        }
    }
}
