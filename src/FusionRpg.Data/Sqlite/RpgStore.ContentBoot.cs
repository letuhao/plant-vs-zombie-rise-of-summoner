using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Effects.Atoms.Power;
using FusionRpg.Core.Stats;

namespace FusionRpg.Data;

/// <summary>
/// Loads the store's content tables into the Core statics that actually get read (E20,
/// completeness-audit.md finding A2).
///
/// <para><b>Every table the atom layer writes had a reader in its own tests and none in production.</b>
/// <c>ElementTable.Current</c> and <c>PowerTables.Current</c> default to a shipped code copy and stay
/// on it forever unless a host calls <c>Use</c> — and until this method existed, nothing did. Editing
/// an imported roster row or coefficient moved the content hash and changed no composed number.</para>
///
/// <para>Call once, after <see cref="RpgStore.Init"/>, from a process that wants live content — today
/// that is the server. A process that never calls this runs on the shipped fallback, exactly as it
/// always has; this method only ever <i>adds</i> a source of truth, never removes the default one.</para>
/// </summary>
public sealed partial class RpgStore
{
    public void LoadContentIntoRuntime()
    {
        ElementTable.Use(GetElementTable());
        PowerTables.Use(GetPowerTables());

        // E22: only the direction column has a live consumer (StatChannels.IsLowerBetter) — see
        // ChannelPolicyTable's doc comment for why default/cap/composeKind stay unread.
        var directions = GetChannelPolicies()
            .ToDictionary(r => r.ChannelId, r => r.Direction, StringComparer.Ordinal);
        ChannelPolicyTable.Use(new ChannelPolicyTable(directions));
    }
}
