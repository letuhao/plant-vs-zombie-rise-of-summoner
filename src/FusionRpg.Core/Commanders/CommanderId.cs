namespace FusionRpg.Core.Commanders;

/// <summary>
/// aura-skill T9a: an addressable identity for a commander, distinct from every other id shape a
/// "dave"/"zomboss" string already means elsewhere in this codebase.
///
/// <para><b>Why a new id, not an existing one.</b> `WorldFaction.FactionId` (`WorldTemplateCatalog.cs`)
/// is a WORLD-MAP concept — bare `"dave"`/`"zomboss"`, meaningful only inside one `WorldState`.
/// `BattleActorSetup.Key` (`BattleModels.cs`) is a PER-MATCH concept — `"squad:0"`/`"wave:3"`, meaningful
/// only inside one battle resolve and re-minted every match. Neither survives across matches or across
/// the world map, which is exactly what an aura/allocation/resource pool needs to attach to — the
/// commander is the same identity in match N and match N+1, and on the world map between them.</para>
///
/// <para><b>Deliberately not player-scoped here.</b> There are exactly two commanders total (owner
/// decision, 2026-08-30: "for now only have 2 of them for lawn run"), not one per player — Dave is the
/// player's own commander, Zomboss is the opposing AI's. <see cref="AllocationScopeKey"/> is where
/// player-scoping actually happens, mirroring <c>AptitudeEndpoints.ScopeKey</c>'s established
/// convention without duplicating its string shape for Dave.</para>
/// </summary>
public enum CommanderId
{
    Dave,
    Zomboss,
}

public static class CommanderIds
{
    /// <summary>The stable string form — `"commander:dave"` / `"commander:zomboss"`. The `commander:`
    /// prefix is load-bearing: neither `WorldFaction.FactionId` (bare `"dave"`/`"zomboss"`) nor any
    /// `BattleActorSetup.Key` (`"squad:N"`/`"wave:N"`) ever carries this prefix, so the three id spaces
    /// can never collide even though two of them share the same bare words.</summary>
    public static string ToStableId(this CommanderId id) => id switch
    {
        CommanderId.Dave => "commander:dave",
        CommanderId.Zomboss => "commander:zomboss",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "unknown commander id"),
    };

    public static readonly IReadOnlyList<CommanderId> All = new[] { CommanderId.Dave, CommanderId.Zomboss };

    /// <summary>Inverse of <see cref="ToStableId"/> — rejects unknown strings and bare faction ids.</summary>
    public static bool TryParseStableId(string? stableId, out CommanderId id)
    {
        id = default;
        if (string.IsNullOrWhiteSpace(stableId)) return false;
        return stableId.Trim() switch
        {
            "commander:dave" => Assign(CommanderId.Dave, out id),
            "commander:zomboss" => Assign(CommanderId.Zomboss, out id),
            _ => false,
        };

        static bool Assign(CommanderId value, out CommanderId target)
        {
            target = value;
            return true;
        }
    }

    /// <summary>
    /// The <see cref="Stats.Aptitudes.AllocationScope.Commander"/> scope key for this commander,
    /// within one player's save. Reuses <c>AptitudeEndpoints.ScopeKey</c>'s exact `"player:{id}"` shape
    /// for Dave (he IS the player's own commander — no new convention needed, no data migration for
    /// existing saves). Zomboss gets a sibling key under the SAME scope enum and the SAME
    /// `RpgStore.LoadAllocation`/`SaveAllocation` mechanism — a new key prefix, not a new store, new
    /// table, or new scope value.
    /// </summary>
    public static string AllocationScopeKey(this CommanderId id, long playerId) => id switch
    {
        CommanderId.Dave => $"player:{playerId}",
        CommanderId.Zomboss => $"zomboss:{playerId}",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "unknown commander id"),
    };
}
