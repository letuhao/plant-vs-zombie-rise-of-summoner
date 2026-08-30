using System.Text.Json.Serialization;

namespace FusionRpg.Contracts;

public sealed class CommanderListResponse
{
    [JsonPropertyName("defaultLawnCommanderId")] public string DefaultLawnCommanderId { get; set; } = "";
    [JsonPropertyName("commanders")] public List<CommanderListRowDto> Commanders { get; set; } = new();
}

public sealed class CommanderListRowDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("isDefault")] public bool IsDefault { get; set; }
    [JsonPropertyName("activeAuraId")] public string? ActiveAuraId { get; set; }
    [JsonPropertyName("activeAuraName")] public string? ActiveAuraName { get; set; }
    [JsonPropertyName("locationStub")] public string? LocationStub { get; set; }
    [JsonPropertyName("legionStub")] public string? LegionStub { get; set; }
}

public sealed class SetDefaultLawnCommanderRequest
{
    [JsonPropertyName("playerId")] public long? PlayerId { get; set; }
    [JsonPropertyName("commanderId")] public string? CommanderId { get; set; }
}

public sealed class DefaultLawnCommanderResponse
{
    [JsonPropertyName("defaultLawnCommanderId")] public string DefaultLawnCommanderId { get; set; } = "";
}
