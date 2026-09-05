namespace FusionRpg.Core.World.Turn;

/// <summary>
/// What a commander may order. Kinds are added by the module that implements them — movement adds
/// its own in W9 — so this list is the contract between the store, the wire, and the engine.
/// </summary>
public static class WorldCommandKinds
{
    /// <summary>Do nothing this turn. The default an absent or idle commander submits.</summary>
    public const string StandFast = "stand-fast";

    /// <summary>March a legion along an ordered lane path (world-movement).</summary>
    public const string Move = "move";

    /// <summary>Attack the guard holding one slot of the sector the legion stands in.</summary>
    public const string Clear = "clear";

    /// <summary>Take the sector the legion stands in, once nothing is left defending it.</summary>
    public const string Claim = "claim";

    /// <summary>Change a legion's posture — march, scout or hold (world-movement).</summary>
    public const string Stance = "stance";

    /// <summary>
    /// Spend a legion's own carried loam into the sector it stands on, 1:1 (spec-loam-legions.md,
    /// G1's bootstrap spend) — a player-issued choice, not an automatic stance effect.
    /// </summary>
    public const string Sustain = "sustain";

    /// <summary>
    /// Found a structure on a compatible, empty slot in the sector a legion stands on
    /// (spec-loam-structures.md), spending the issuing legion's own `CarriedLoam`.
    /// </summary>
    public const string Build = "build";

    /// <summary>
    /// Give up a sector this faction holds, on purpose — the player's own deliberate release,
    /// distinct from `LoamPhases.Pressure`'s automatic pick of the weakest link when upkeep cannot
    /// be paid (world-stage W24). Needs no entity: a faction cedes ground, not a legion.
    /// </summary>
    public const string Cede = "cede";

    /// <summary>
    /// Bind a warden onto a sector this faction owns, exempting it from `LoamPhases.Pressure`'s fade
    /// (and recovery) while the binding holds (spec-loam-texture.md's Wardens; world-stage W28). Named
    /// `bind-warden`, not `ward` — `ward` names the still-unbuilt *lane* action that raises
    /// `WorldLaneDto.WardLevel`, a different mechanic entirely; the collision was repaired once
    /// already and must not return through this kind's name. Needs no entity: a faction binds a
    /// warden to ground, not a legion.
    /// </summary>
    public const string BindWarden = "bind-warden";

    /// <summary>
    /// Found a legion at the sector's Seat, spending its `RecruitStock` (world-map W51,
    /// spec-sector-development.md §1). Needs no entity — `raise` founds a *new* legion, it does not
    /// command an existing one. Resolves in `Snapshot`, right after `Build`: ownership is only
    /// decided once the rest of the turn has run, so every other legality check (whose ground, a
    /// Seat, no hostile entity standing in it, enough stock) is resolution-time in
    /// `RaiseResolver`, not admission-time, the same discipline `BuildResolver` already applies.
    /// </summary>
    public const string Raise = "raise";

    /// <summary>
    /// Start a sector-wide project on a sector this faction holds (world-map W52,
    /// spec-sector-development.md §3), spending the sector's own `LoamStock` — a project raises the
    /// whole sector (development, defense, capacity), never one slot's output, which is what `build`
    /// is for. Needs no entity, the same shape `raise` already uses: a project belongs to the
    /// sector, not a legion. Resolves in `Snapshot`, right after `raise`, for the identical
    /// ownership-race reason `BuildResolver`/`RaiseResolver` both already state.
    /// </summary>
    public const string Develop = "develop";

    /// <summary>
    /// Attack the district around a hostile sector's Seat (base-defense-ideal.md decision 26;
    /// spec-siege-seam.md §5). Distinct from <see cref="Clear"/>: `clear` defends one slot's guard,
    /// `assault` fights for the legions standing in the district's core. Needs an entity (who is
    /// attacking) and a sector (which district) — the legality that can change between filing and
    /// resolving (is the legion still there, is the sector still hostile) is reveal-time, the same
    /// discipline `clear`'s own admission rule already states.
    /// </summary>
    public const string Assault = "assault";

    public static readonly IReadOnlyList<string> All =
        new[] { StandFast, Move, Clear, Claim, Stance, Sustain, Build, Cede, BindWarden, Raise, Develop, Assault };

    public static bool IsKnown(string? kind) =>
        kind != null && All.Contains(kind, StringComparer.Ordinal);
}

/// <summary>
/// One order, as plain data. Every commander — the human, Zomboss, a clan — submits this same
/// shape through the same path, which is what keeps the AI honest and the engine ignorant of who
/// is playing.
///
/// Payload fields are optional and typed rather than a JSON blob: the set is small, the store
/// serializes it, and a typo becomes a compile error instead of a runtime surprise.
/// </summary>
public sealed record WorldCommand
{
    /// <summary>The faction issuing the order.</summary>
    public string CommanderId { get; init; } = "";

    /// <summary>Unique per commander per turn — the idempotency key for submission.</summary>
    public string CommandId { get; init; } = "";

    public string Kind { get; init; } = "";

    /// <summary>Subject of the order, when it has one.</summary>
    public string? EntityId { get; init; }

    /// <summary>Required by `clear`: the sector the order is about, so a stale client is caught.</summary>
    public string? SectorId { get; init; }

    public int? SlotIndex { get; init; }

    /// <summary>The posture a `stance` order is asking for.</summary>
    public string? Stance { get; init; }

    /// <summary>Ordered lane ids for a march (W9).</summary>
    public IReadOnlyList<string> LanePath { get; init; } = Array.Empty<string>();

    /// <summary>How much carried loam a `sustain` order spends (spec-loam-legions.md).</summary>
    public long? Amount { get; init; }

    /// <summary>Which structure a `build` order names (spec-loam-structures.md).</summary>
    public string? StructureId { get; init; }

    /// <summary>
    /// The value a `bind-warden` order writes into `WorldSector.WardenBindingId` — the bound demon
    /// contract's own instance id, unchanged (spec-loam-texture.md, world-stage W28/W29's two-step
    /// contract-then-order flow). Opaque to Core: nothing here validates it against a demon roster,
    /// the same way `StructureId` is validated only inside the `build` admission arm, not generically.
    /// </summary>
    public string? WardenId { get; init; }

    /// <summary>Which project a `develop` order names (spec-sector-development.md §3, world-map W52).</summary>
    public string? ProjectId { get; init; }
}
