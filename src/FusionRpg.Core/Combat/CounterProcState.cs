using FusionRpg.Contracts;

namespace FusionRpg.Core.Combat;

/// <summary>Hit-streak meters for Counter delivery. In-memory session only.</summary>
public sealed class CounterProcState
{
    readonly Dictionary<string, int> _hits = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, int> Snapshot() =>
        new Dictionary<string, int>(_hits, StringComparer.OrdinalIgnoreCase);

    public void Clear() => _hits.Clear();

    public void ClearGrant(string grantId)
    {
        if (string.IsNullOrEmpty(grantId)) return;
        var prefix = grantId + "|";
        foreach (var k in _hits.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
            _hits.Remove(k);
    }

    /// <returns>True when this increment reached <paramref name="everyHits"/>.</returns>
    public bool TryBurst(string grantId, string scopeKey, int everyHits, bool resetOnBurst)
    {
        if (everyHits <= 0 || string.IsNullOrWhiteSpace(grantId)) return false;
        var key = grantId + "|" + (scopeKey ?? "");
        _hits.TryGetValue(key, out var n);
        n++;
        if (n >= everyHits)
        {
            if (resetOnBurst) _hits[key] = 0;
            else _hits[key] = n;
            return true;
        }

        _hits[key] = n;
        return false;
    }
}
