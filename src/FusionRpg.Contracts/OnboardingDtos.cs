using System.Text.Json.Serialization;

namespace FusionRpg.Contracts;

public sealed class OnboardingCheckpointDto
{
    [JsonPropertyName("checkpointId")] public string CheckpointId { get; set; } = "";
    [JsonPropertyName("state")] public string State { get; set; } = "";
    [JsonPropertyName("earnedRunId")] public long? EarnedRunId { get; set; }
    [JsonPropertyName("rewardRef")] public string? RewardRef { get; set; }
    [JsonPropertyName("payloadJson")] public string? PayloadJson { get; set; }
    [JsonPropertyName("earnedUtc")] public string EarnedUtc { get; set; } = "";
    [JsonPropertyName("claimedUtc")] public string? ClaimedUtc { get; set; }
    [JsonPropertyName("revision")] public long Revision { get; set; }
}

public sealed class OnboardingStateDto
{
    [JsonPropertyName("playerId")] public long PlayerId { get; set; }
    [JsonPropertyName("playerLevel")] public long PlayerLevel { get; set; }
    [JsonPropertyName("revision")] public long Revision { get; set; }
    [JsonPropertyName("checkpoints")] public List<OnboardingCheckpointDto> Checkpoints { get; set; } = new();
}

public sealed class OnboardingClaimDto
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("reason")] public string Reason { get; set; } = "";
    [JsonPropertyName("checkpoint")] public OnboardingCheckpointDto? Checkpoint { get; set; }
}
