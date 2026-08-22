namespace FusionRpg.Core.World.Turn;

/// <summary>What a battle is being fought over.</summary>
public static class BattleKinds
{
    /// <summary>Two hostile forces standing in the same sector.</summary>
    public const string Sector = "sector";

    /// <summary>Two hostile forces closing on one lane, at the moment they meet.</summary>
    public const string Lane = "lane";

    /// <summary>A deliberate attack on a slot's guard — never a consequence of walking past it.</summary>
    public const string Guard = "guard";

    /// <summary>
    /// A battle's id: deterministic, unique within a turn, and readable in a report. It lives here
    /// with the kinds rather than in whichever phase happens to start the fight, so movement and
    /// sieges cannot drift into two different formats.
    /// </summary>
    public static string IdFor(int turn, string kind, string location, string attacker, string? defender) =>
        $"t{turn}:{kind}:{location}:{attacker}" + (defender is null ? "" : "|" + defender);
}

/// <summary>
/// The map's half of the combat seam (spec-world-movement.md §Contact). The world says who is
/// fighting, where, and when inside the turn; it never says who wins. Everything here is plain data
/// so the real combat module can be dropped in behind <see cref="IBattleResolver"/> without the
/// world module learning anything about rounds, decks, or damage.
/// </summary>
public sealed record BattleRequest
{
    /// <summary>Deterministic and unique within a turn — the report key and the replay anchor.</summary>
    public string BattleId { get; init; } = "";

    public string Kind { get; init; } = "";

    /// <summary>Sector id, or lane id for a crossing.</summary>
    public string LocationId { get; init; } = "";

    /// <summary>When inside the turn, in per-mille — 0 for anything settled after movement.</summary>
    public int TimeMilli { get; init; }

    public string AttackerEntityId { get; init; } = "";

    /// <summary>Null for a guard fight: a guard is slot state, not an entity.</summary>
    public string? DefenderEntityId { get; init; }

    /// <summary>
    /// True when the defender held this ground at turn start. Neither side defends when both were
    /// moving — which is the difference between meeting in the open and being met.
    /// </summary>
    public bool DefenderStationary { get; init; }

    public string? GuardWaveId { get; init; }
    public int? SlotIndex { get; init; }
}

/// <summary>What one side has left when the dust settles.</summary>
public sealed record BattleSideOutcome
{
    public string EntityId { get; init; } = "";

    /// <summary>Members still alive, in their original order, carrying their new wounds.</summary>
    public IReadOnlyList<WorldEntityMember> Survivors { get; init; } = Array.Empty<WorldEntityMember>();

    /// <summary>Beaten but alive: it keeps the field it is on and loses next turn's orders.</summary>
    public bool Routed { get; init; }

    /// <summary>Nothing walked away — the entity leaves the map.</summary>
    public bool Destroyed { get; init; }
}

public sealed record BattleOutcome
{
    public string BattleId { get; init; } = "";

    /// <summary>Null when nobody won — mutual destruction, or a guard that held.</summary>
    public string? WinnerEntityId { get; init; }

    public bool GuardCleared { get; init; }

    public IReadOnlyList<BattleSideOutcome> Sides { get; init; } = Array.Empty<BattleSideOutcome>();
}

/// <summary>
/// The seam itself. Wave 1 ships exactly one implementation — the placeholder — and the world
/// module is the only thing that constructs it, so nothing else can start depending on its numbers.
/// </summary>
public interface IBattleResolver
{
    /// <summary>
    /// Resolves one request. <paramref name="combatants"/> carries the entities named by the
    /// request, so an implementation never needs the whole world; <paramref name="seed"/> is the
    /// world seed mixed with the turn, so a roll is reproducible without a wall clock.
    /// </summary>
    BattleOutcome Resolve(BattleRequest request, IReadOnlyList<WorldEntity> combatants, ulong seed);
}
