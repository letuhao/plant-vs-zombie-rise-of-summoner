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
    [JsonPropertyName("stories")] public List<OnboardingStoryDto> Stories { get; set; } = new();
}

public sealed class OnboardingClaimDto
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("reason")] public string Reason { get; set; } = "";
    [JsonPropertyName("checkpoint")] public OnboardingCheckpointDto? Checkpoint { get; set; }
}

public sealed class OnboardingStoryDto
{
    [JsonPropertyName("storyId")] public string StoryId { get; set; } = "";
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("state")] public string State { get; set; } = "";
    [JsonPropertyName("outcome")] public string? Outcome { get; set; }
    [JsonPropertyName("eligible")] public bool Eligible { get; set; }
    [JsonPropertyName("acknowledgedUtc")] public string? AcknowledgedUtc { get; set; }
    [JsonPropertyName("revision")] public long Revision { get; set; }
}

public sealed class OnboardingStoryAckRequest
{
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("outcome")] public string Outcome { get; set; } = "";
}

public sealed class OnboardingStoryAckDto
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("reason")] public string Reason { get; set; } = "";
    [JsonPropertyName("story")] public OnboardingStoryDto? Story { get; set; }
}
