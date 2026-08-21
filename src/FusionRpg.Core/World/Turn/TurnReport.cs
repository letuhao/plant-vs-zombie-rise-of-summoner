namespace FusionRpg.Core.World.Turn;

public static class TurnReportKinds
{
    public const string CommandAccepted = "command.accepted";
    public const string CommandDropped = "command.dropped";
    public const string Calendar = "calendar";
    public const string Event = "event";
}

/// <summary>One line of what happened, in the order it happened.</summary>
public readonly record struct TurnReportEntry(string Phase, string Kind, string Subject, string Detail);

/// <summary>
/// What a turn did — the presentation feed, the "while you were away" screen, and the record a
/// replay reproduces. The report IS the log: nothing in the engine writes anywhere else.
/// </summary>
public sealed class TurnReport
{
    readonly List<TurnReportEntry> _entries = new();
    readonly List<string> _phases = new();

    public IReadOnlyList<TurnReportEntry> Entries => _entries;

    /// <summary>Phases in the order they ran — the locked order, made observable.</summary>
    public IReadOnlyList<string> Phases => _phases;

    public IEnumerable<TurnReportEntry> Accepted =>
        _entries.Where(e => e.Kind == TurnReportKinds.CommandAccepted);

    public IEnumerable<TurnReportEntry> Dropped =>
        _entries.Where(e => e.Kind == TurnReportKinds.CommandDropped);

    /// <summary>Rebuilds a report from stored entries — the store's read path for the hot tail.</summary>
    public static TurnReport FromEntries(IEnumerable<TurnReportEntry> entries)
    {
        var report = new TurnReport();
        foreach (var entry in entries)
        {
            report._entries.Add(entry);
            if (report._phases.LastOrDefault() != entry.Phase)
                report._phases.Add(entry.Phase);
        }

        return report;
    }

    internal void BeginPhase(string phase) => _phases.Add(phase);

    internal void Add(string phase, string kind, string subject, string detail) =>
        _entries.Add(new TurnReportEntry(phase, kind, subject, detail));
}
