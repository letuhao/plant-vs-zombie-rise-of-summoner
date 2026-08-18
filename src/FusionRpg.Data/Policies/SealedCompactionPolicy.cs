namespace FusionRpg.Data.Policies;

/// <summary>Sealed hot-path retain limits and snapshot schema versions (archive/trim = Slice D).</summary>
public static class SealedCompactionPolicy
{
    public const int ActivityRetainTail = 10_000;
    public const int XpRetainTailPerActor = 5_000;
    public const int KeepLastNFullCaptureRuns = 50;
    public const int ActivitySnapshotSchemaVersion = 1;
    public const int XpSnapshotSchemaVersion = 1;
}
