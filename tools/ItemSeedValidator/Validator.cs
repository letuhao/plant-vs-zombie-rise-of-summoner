using FusionRpg.Tools.ItemSeedValidator.Checks;
using FusionRpg.Tools.ItemSeedValidator.Model;
using FusionRpg.Tools.ItemSeedValidator.Naming;
using FusionRpg.Tools.ItemSeedValidator.Registries;

namespace FusionRpg.Tools.ItemSeedValidator;

public sealed record ValidationResult(
    IReadOnlyList<Finding> Findings,
    int FilesScanned,
    int EntriesScanned,
    RegistrySet Registries,
    NamespaceAllocation Allocation)
{
    public int ErrorCount => Findings.Count(f => f.Severity == Severity.Error);
    public int WarningCount => Findings.Count(f => f.Severity == Severity.Warning);

    /// <summary>
    /// A validator that passes because it found nothing is worse than no validator. Zero files
    /// scanned is never success.
    /// </summary>
    public bool ScannedNothing => FilesScanned == 0;
}

public static class Validator
{
    /// <summary>Directory under the seed root holding the wave-0 registries.</summary>
    public const string RegistryDirName = "_registry";

    public static ValidationResult Run(string seedRoot)
    {
        var registries = RegistrySet.Load(Path.Combine(seedRoot, RegistryDirName));
        var files = Discover(seedRoot);
        return Run(registries, files);
    }

    public static ValidationResult Run(RegistrySet registries, IReadOnlyList<SeedFile> files)
    {
        var allocation = NamespaceAllocation.Build(registries);
        var ctx = new ValidationContext
        {
            Registries = registries,
            Allocation = allocation,
            Files = files,
            Normalizer = new NameNormalizer(registries),
        };

        // Registry health first. A namespace nothing validates, or a registry that is not frozen,
        // is a fleet-level problem and the report should lead with it.
        foreach (var missing in KindCatalog.MissingNamespaces(registries))
            ctx.CorpusError("NamespaceUncovered", "naming.v1.json idNamespaces",
                $"idNamespaces.{missing} is allocated but this validator has no kind for it; "
                + "ids under it would be validated against nothing");
        foreach (var problem in allocation.Problems)
            ctx.CorpusError("NamespaceUnexpandable", "naming.v1.json idNamespaces", problem);
        foreach (var unfrozen in registries.Unfrozen)
            ctx.CorpusWarn("RegistryNotFrozen", "authoring-fleet-plan.md §7.1",
                $"{unfrozen} still carries frozen:false; the freeze gate is not closed");
        if (registries.Words is null)
            ctx.CorpusWarn("WordPoolAbsent", "naming.v1.json collisionNormalization",
                "words.v1.json (F1's reserved word pools) is absent, so surface forms cannot be "
                + "collapsed onto canonical ids and fusions cannot be decomposed — the collision "
                + "check runs on raw tokens and will miss Ashen/Ash");

        StructuralCheck.Run(ctx);
        IdentityCheck.Run(ctx);
        OwnershipCheck.Run(ctx);
        NamingCheck.Run(ctx);
        ReferenceCheck.Run(ctx);
        UniqueRuleCheck.Run(ctx);
        SetRuleCheck.Run(ctx);
        DropTableCheck.Run(ctx);
        FrameDirectionCheck.Run(ctx);
        SocketMaxCheck.Run(ctx);
        GemAffinityCheck.Run(ctx);
        RoleFamilyCheck.Run(ctx);
        NameWordCheck.Run(ctx);
        LintCheck.Run(ctx);

        // Partitions are assigned by IdentityCheck, after some findings were already recorded.
        // Backfill so every finding lands in the right group.
        var byFile = files.ToDictionary(f => f.RelativePath, StringComparer.Ordinal);
        var findings = ctx.Findings.Select(f =>
        {
            if (f.Partition != Finding.NoPartition) return f;
            if (!byFile.TryGetValue(f.File, out var file)) return f;
            var partition = f.EntryId is not null
                ? file.Entries.FirstOrDefault(e => e.Label == f.EntryId)?.Partition ?? Finding.NoPartition
                : ValidationContext.FilePartition(file);
            return partition == Finding.NoPartition ? f : f with { Partition = partition };
        }).ToList();

        return new ValidationResult(findings, files.Count, files.Sum(f => f.Entries.Count),
            registries, allocation);
    }

    /// <summary>Every .json under the seed root except the registry directory itself.</summary>
    public static List<SeedFile> Discover(string seedRoot)
    {
        var files = new List<SeedFile>();
        if (!Directory.Exists(seedRoot)) return files;

        foreach (var path in Directory.EnumerateFiles(seedRoot, "*.json", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(seedRoot, path).Replace('\\', '/');
            var segments = relative.Split('/');
            if (segments[0] == RegistryDirName) continue;
            var directory = segments.Length > 1 ? segments[0] : "";
            files.Add(SeedFile.Load(path, relative, directory));
        }

        return files;
    }
}
