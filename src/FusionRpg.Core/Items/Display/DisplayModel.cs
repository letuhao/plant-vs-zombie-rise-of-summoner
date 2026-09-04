using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Items.Display;

/// <summary>G3 §4.4's closed vocabulary — where a rendered line came from.</summary>
public enum SourceKind
{
    Base, Implicit, AffixPrefix, AffixSuffix, Enhancement, SocketInsert, Resonance,
    Word, SetThreshold, GrantedAction, UniqueIdentity, UniqueVariance,
}

/// <summary>A roll-quality bar — only ever produced for `RollPolicy.OnInstantiate` (spec's own rule:
/// `Fixed` would lie about luck with a full bar; `OnApply` shows the band, not a bar).</summary>
public readonly record struct RollBar(int Segments)
{
    public const int MaxSegments = 5;
}

/// <summary>
/// One rendered magnitude. `Key`/`Args` are the ONLY human-readable leaf shape (Boundaries: "every
/// human-readable leaf is {key, args}") — never a glued string. `Args` carries the frozen numbers a
/// template's placeholders need; the renderer (module 20) formats them, this layer never emits markup.
/// </summary>
public readonly record struct DisplayLine(
    string Key, IReadOnlyDictionary<string, string> Args, UnitClass Unit, SourceKind SourceKind, int GroupOrder,
    RollBar? RollBar = null, string? ContextRead = null, int? RollQualityPerMille = null);

/// <summary>One of the card's eleven ordered sections (G3 §4.1) — order and contents unchanged here,
/// this module only produces the lines inside each.</summary>
public readonly record struct DisplayBlock(string BlockKey, IReadOnlyList<DisplayLine> Lines);

/// <summary>The whole card — an ordered tree, never markup.</summary>
public readonly record struct DisplayModel(IReadOnlyList<DisplayBlock> Blocks);

/// <summary>Two cards, diffed line by line — the same `DisplayLine`s the single-card render produces
/// (comparison diffs RENDERED lines, never a parallel computation).</summary>
public readonly record struct CompareModel(DisplayModel Left, DisplayModel Right, IReadOnlyList<int> DifferingLineIndexes);
