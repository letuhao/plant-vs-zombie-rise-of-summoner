namespace FusionRpg.Core.Battle.Timeline;

/// <summary>
/// Turn-domain derived channel ids. **Flat and non-elemental** — speed is not an element, so these
/// must stay out of the generated combat roster (12 families × 7 elements); adding them there
/// would invent 14 meaningless channels and change the roster count from 84.
///
/// The precedent already exists: `status.power.*` and `progression.*` are plain consts outside
/// that generation. Names inherited from the Chaos combat-core stat families.
/// </summary>
public static class DerivedTurnChannels
{
    /// <summary>Higher acts more often. Default 100 — the resolver's scale-100 "nominal".</summary>
    public const string Speed = "turn.speed";

    /// <summary>Per-mille action-time multiplier: 1000 = normal, 500 = twice as fast.</summary>
    public const string Haste = "turn.haste";

    /// <summary>Reserved movement vocabulary (Chaos combat-core). Not build scope here.</summary>
    public const string MoveSpeed = "turn.moveSpeed";

    public const int BaseSpeed = 100;
    public const int NominalHasteMilli = 1000;
}
