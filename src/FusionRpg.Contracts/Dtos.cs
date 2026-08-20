using System.Text.Json.Serialization;

namespace FusionRpg.Contracts;

public sealed class StatMod
{
    [JsonPropertyName("hpPercent")] public float HpPercent { get; set; } = 1f;
    [JsonPropertyName("hpFlat")] public int HpFlat { get; set; }
    [JsonPropertyName("attackPercent")] public float AttackPercent { get; set; } = 1f;
    [JsonPropertyName("attackFlat")] public int AttackFlat { get; set; }
    [JsonPropertyName("defensePercent")] public float DefensePercent { get; set; } = 1f;
    [JsonPropertyName("defenseFlat")] public int DefenseFlat { get; set; }
}

public sealed class StatsConfig
{
    [JsonPropertyName("plants")] public StatMod Plants { get; set; } = new();
    [JsonPropertyName("zombies")] public StatMod Zombies { get; set; } = new();
    // Per-hit telemetry is opt-in: the old default=true made every player pay per-hit
    // *.damage + combat.hit emission unless a server pull happened to say otherwise.
    [JsonPropertyName("logDamage")] public bool LogDamage { get; set; }
    [JsonPropertyName("applyStats")] public bool ApplyStats { get; set; } = true;
}

public sealed class EventEnvelope
{
    [JsonPropertyName("id")] public long? Id { get; set; }
    [JsonPropertyName("t")] public string T { get; set; } = "";
    [JsonPropertyName("game")] public string Game { get; set; } = "pvzrh-3.8.1";
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("matchKey")] public string? MatchKey { get; set; }
    [JsonPropertyName("playerId")] public long? PlayerId { get; set; }
    [JsonPropertyName("runId")] public long? RunId { get; set; }
    [JsonPropertyName("payload")] public object? Payload { get; set; }
}

public sealed class EventBatch
{
    [JsonPropertyName("events")] public List<EventEnvelope>? Events { get; set; }
}

public sealed class HealthDto
{
    [JsonPropertyName("ok")] public bool Ok { get; set; } = true;
    [JsonPropertyName("injectorConnected")] public bool InjectorConnected { get; set; }
    [JsonPropertyName("lastHeartbeatUtc")] public string? LastHeartbeatUtc { get; set; }
    [JsonPropertyName("simEnabled")] public bool SimEnabled { get; set; }
    [JsonPropertyName("source")] public string Source { get; set; } = "none";
    [JsonPropertyName("currentPlayerId")] public long CurrentPlayerId { get; set; }
    [JsonPropertyName("ingestQueued")] public int IngestQueued { get; set; }
    [JsonPropertyName("lastFlushMs")] public double LastFlushMs { get; set; }
}

public sealed class HeartbeatDto
{
    [JsonPropertyName("source")] public string? Source { get; set; }
    [JsonPropertyName("game")] public string Game { get; set; } = "pvzrh-3.8.1";
}

public sealed class ProbeRequest
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("scenario")] public string? Scenario { get; set; }
    [JsonPropertyName("data")] public object? Data { get; set; }
}

public sealed class MetricItem
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("value")] public double Value { get; set; }
    [JsonPropertyName("ts")] public string Ts { get; set; } = "";
}

public sealed class RunItem
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("playerId")] public long PlayerId { get; set; }
    [JsonPropertyName("matchKey")] public string? MatchKey { get; set; }
    [JsonPropertyName("startedUtc")] public string StartedUtc { get; set; } = "";
    [JsonPropertyName("endedUtc")] public string? EndedUtc { get; set; }
    [JsonPropertyName("levelName")] public string? LevelName { get; set; }
    [JsonPropertyName("result")] public string? Result { get; set; }
    [JsonPropertyName("mowersUsed")] public int MowersUsed { get; set; }
    [JsonPropertyName("plantsPlanted")] public int PlantsPlanted { get; set; }
    [JsonPropertyName("plantsDied")] public int PlantsDied { get; set; }
    [JsonPropertyName("zombiesKilled")] public int ZombiesKilled { get; set; }
    [JsonPropertyName("summary")] public object? Summary { get; set; }
    [JsonPropertyName("levelType")] public string? LevelType { get; set; }
    [JsonPropertyName("boardLevel")] public int? BoardLevel { get; set; }
    [JsonPropertyName("modifiers")] public object? Modifiers { get; set; }
    [JsonPropertyName("archiveUri")] public string? ArchiveUri { get; set; }
}

public sealed class RecipeItem
{
    [JsonPropertyName("parentA")] public int ParentA { get; set; }
    [JsonPropertyName("parentAName")] public string? ParentAName { get; set; }
    [JsonPropertyName("parentB")] public int ParentB { get; set; }
    [JsonPropertyName("parentBName")] public string? ParentBName { get; set; }
    [JsonPropertyName("result")] public int Result { get; set; }
    [JsonPropertyName("resultName")] public string? ResultName { get; set; }
}

public sealed class SpawnStatItem
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("runId")] public long RunId { get; set; }
    [JsonPropertyName("ptr")] public string Ptr { get; set; } = "";
    [JsonPropertyName("side")] public string Side { get; set; } = "";
    [JsonPropertyName("type")] public int Type { get; set; }
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("capturedUtc")] public string CapturedUtc { get; set; } = "";
    [JsonPropertyName("stats")] public object? Stats { get; set; }
}

public sealed class PlayerDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("createdUtc")] public string CreatedUtc { get; set; } = "";
}

public sealed class CreatePlayerRequest
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public sealed class SelectPlayerRequest
{
    [JsonPropertyName("id")] public long Id { get; set; }
}

public sealed class PlayersListDto
{
    [JsonPropertyName("items")] public List<PlayerDto> Items { get; set; } = new();
    [JsonPropertyName("currentPlayerId")] public long CurrentPlayerId { get; set; }
}

public sealed class TypeItem
{
    [JsonPropertyName("game")] public string Game { get; set; } = RpgConstants.GameId;
    [JsonPropertyName("side")] public string Side { get; set; } = "";
    [JsonPropertyName("type")] public int Type { get; set; }
    [JsonPropertyName("typeName")] public string? TypeName { get; set; }
    [JsonPropertyName("displayName")] public string? DisplayName { get; set; }
    [JsonPropertyName("sampleJson")] public string? SampleJson { get; set; }
    [JsonPropertyName("hpBase")] public long? HpBase { get; set; }
    [JsonPropertyName("maxHpBase")] public long? MaxHpBase { get; set; }
    [JsonPropertyName("attackBase")] public int? AttackBase { get; set; }
    [JsonPropertyName("armorBase")] public int? ArmorBase { get; set; }
    [JsonPropertyName("armorMaxBase")] public int? ArmorMaxBase { get; set; }
    [JsonPropertyName("seenCount")] public int SeenCount { get; set; }
    [JsonPropertyName("killedCount")] public int KilledCount { get; set; }
    [JsonPropertyName("firstSeenUtc")] public string? FirstSeenUtc { get; set; }
    [JsonPropertyName("lastSeenUtc")] public string? LastSeenUtc { get; set; }
}

public sealed class HelloDto
{
    [JsonPropertyName("game")] public string Game { get; set; } = "pvzrh-3.8.1";
    [JsonPropertyName("version")] public string Version { get; set; } = "1.0.0";
}

public sealed class CommandDto
{
    /// <summary>Dedupes SignalR + HTTP inbox delivery.</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("payload")] public object? Payload { get; set; }
}

public sealed class StorageSummaryDto
{
    [JsonPropertyName("archiveCount")] public long ArchiveCount { get; set; }
    [JsonPropertyName("closedRunsStillHot")] public long ClosedRunsStillHot { get; set; }
    [JsonPropertyName("openRuns")] public long OpenRuns { get; set; }
    [JsonPropertyName("activityOverTail")] public bool ActivityOverTail { get; set; }
    [JsonPropertyName("xpOverTail")] public bool XpOverTail { get; set; }
}

public sealed class StorageArchiveItemDto
{
    [JsonPropertyName("uri")] public string Uri { get; set; } = "";
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("runId")] public long? RunId { get; set; }
    [JsonPropertyName("createdUtc")] public string CreatedUtc { get; set; } = "";
}

public sealed class StorageUrisRequest
{
    [JsonPropertyName("uris")] public List<string> Uris { get; set; } = new();
}

public sealed class StorageRunIdsRequest
{
    [JsonPropertyName("runIds")] public List<long> RunIds { get; set; } = new();
}

public sealed class StoragePurgeResultDto
{
    [JsonPropertyName("deleted")] public int Deleted { get; set; }
    [JsonPropertyName("refused")] public int Refused { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }
}

public static class RpgConstants
{
    /// <summary>Legacy default / 3.8.1 profile id. Prefer <see cref="GameId381"/> or injector runtime profile.</summary>
    public const string GameId = GameId381;
    public const string GameId381 = "pvzrh-3.8.1";
    public const string GameId39 = "pvzrh-3.9";
    public const string InjectorGroup = "injector";
    public const string WebGroup = "web";
    public const string SourceNone = "none";
    public const string SourceSim = "sim";
    public const string SourceInjector = "injector";
    public const string SimEnvVar = "FUSIONRPG_SIM";

    public static bool IsNoisyKind(string? kind) =>
        kind is "plant.damage" or "zombie.damage" or "bullet.init" or "bullet.place" or "item.drop" or "pet.xp";

    public static bool IsDroppableWhenFull(string? kind) => IsNoisyKind(kind);
}
