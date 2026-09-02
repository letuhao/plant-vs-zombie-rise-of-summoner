using System.Text.Json.Serialization;

namespace FusionRpg.Contracts;

/// <summary>Demon profile — the identity superset over a UniqueActor specimen (spec-demon-core.md).</summary>
public sealed class DemonProfileDto
{
    [JsonPropertyName("instanceId")] public string InstanceId { get; set; } = "";
    [JsonPropertyName("speciesId")] public string SpeciesId { get; set; } = "";
    [JsonPropertyName("rarity")] public string Rarity { get; set; } = "chaff";
    [JsonPropertyName("variant")] public string Variant { get; set; } = "normal";
    [JsonPropertyName("elementPrimary")] public string ElementPrimary { get; set; } = "";
    [JsonPropertyName("elementSecondary")] public string? ElementSecondary { get; set; }
    [JsonPropertyName("traitIds")] public List<string> TraitIds { get; set; } = new();
    [JsonPropertyName("origin")] public string Origin { get; set; } = "";
    [JsonPropertyName("nickname")] public string? Nickname { get; set; }
    [JsonPropertyName("locked")] public bool Locked { get; set; }
    [JsonPropertyName("star")] public int Star { get; set; }
    [JsonPropertyName("promoted")] public bool Promoted { get; set; }
    [JsonPropertyName("createdUtc")] public string CreatedUtc { get; set; } = "";
    [JsonPropertyName("revision")] public long Revision { get; set; }
}

/// <summary>Roster row: the specimen (actor) with its demon profile.</summary>
public sealed class DemonSpecimenDto
{
    [JsonPropertyName("actor")] public UniqueActorDto Actor { get; set; } = new();
    [JsonPropertyName("profile")] public DemonProfileDto Profile { get; set; } = new();
}

public sealed class DemonRosterDto
{
    [JsonPropertyName("playerId")] public long PlayerId { get; set; }
    [JsonPropertyName("items")] public List<DemonSpecimenDto> Items { get; set; } = new();
}

public static class DemonCodexStates
{
    public const string Seen = "seen";
    public const string Discovered = "discovered";
}

public sealed class DemonCodexEntryDto
{
    [JsonPropertyName("speciesId")] public string SpeciesId { get; set; } = "";
    [JsonPropertyName("state")] public string State { get; set; } = DemonCodexStates.Seen;
    [JsonPropertyName("firstUtc")] public string FirstUtc { get; set; } = "";
    [JsonPropertyName("updatedUtc")] public string UpdatedUtc { get; set; } = "";
}

public sealed class DemonCodexDto
{
    [JsonPropertyName("playerId")] public long PlayerId { get; set; }
    [JsonPropertyName("entries")] public List<DemonCodexEntryDto> Entries { get; set; } = new();
}

/// <summary>Mint request fields (server-internal callers: summoning, capture, fusion, seed).</summary>
public sealed class DemonMintSpec
{
    public string SpeciesId { get; set; } = "";
    public string Side { get; set; } = "";
    public int GameTypeId { get; set; }
    public string Rarity { get; set; } = "chaff";
    public string Variant { get; set; } = "normal";
    public string ElementPrimary { get; set; } = "";
    public string? ElementSecondary { get; set; }
    public List<string> TraitIds { get; set; } = new();
    public string Origin { get; set; } = "";
    public string? Nickname { get; set; }
}
