using FusionRpg.Contracts;
using FusionRpg.Core.Status;

namespace FusionRpg.Core.Vfx;

/// <summary>
/// Maps status applies to presentation cues — SPEC W5, vfx-ssot.md §4.1. Pure; the injector
/// subscribes <see cref="StatusRuntime.OnApplied"/> and forwards the cue to its sink.
/// </summary>
public static class StatusVfxCues
{
    public static string CueId(string statusId) =>
        "status." + (statusId ?? "").Trim().ToLowerInvariant() + ".apply";

    public static VfxCueDto Cue(StatusInstance instance) => new()
    {
        CueId = CueId(instance.StatusId),
        TargetPtr = instance.HostPtr
    };
}
