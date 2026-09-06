using FusionRpg.Core.Battle;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items.Drops;
using FusionRpg.Core.Power;

namespace FusionRpg.Core.Delve.Loot;

/// <summary>
/// `dungeon-loot` D3.15 (spec-dungeon-loot.md §3's own "wiring gaps" table, row 1) — PARTIALLY BUILT:
/// this file owns instantiating the boss's own first-clear grant through the REAL
/// `Instantiator.TryInstantiate`, on its own reserved stream, "never flat" (D3.15's own acceptance
/// line). `DelveLoot.RollRoom` itself (the room-drop host, D3.11's own already-named gap) is not built
/// here — it needs `RarityShift.Apply`'s own tuning-reading signature, which this session's own read
/// pass found genuinely underspecified by the spec's literal 5-argument pseudocode (see D3.11's own
/// todo entry) — building it here would mean guessing at loot-rarity-affecting wiring.
/// </summary>
public static class DelveLoot
{
    /// <summary>
    /// Spec's own "wiring gaps" table, verbatim: "host instantiates it through `TryInstantiate` at
    /// `Θ_boss` on `DeriveStream(manifest.LootSeed, LootStreams.RollSeed(grant.Index))` — the
    /// pipeline's own stream at the grant's own index."
    ///
    /// <para><b>Deliberately a NEW, standalone function — `LootPipeline.cs`'s own existing first-clear-
    /// grant code (`:225-231`, "appended flat, RollSeed 0, no Mint") is NOT edited here.</b> Spec's own
    /// text marks that exact edit "ask-first: it changes every manifest with a `FirstClearGrant`" —
    /// moving every existing golden hash for a delve boss clear, a cross-cutting change `LootPipeline
    /// .cs` shares with web-wave/expedition-tier/world-sector, not a call this task can make alone.
    /// This function is the real, tested mechanism an owner-approved wiring would call into; it never
    /// touches the shared path other source kinds already rely on.</para>
    ///
    /// <para><paramref name="origin"/> defaults to <see cref="InstanceOrigin.Drop"/> — spec's own
    /// stated v1 permission ("`InstanceOrigin` has no delve member; filed on the effect-atom program
    /// … v1 reads `Drop` with the binding source carrying scope") — never a fabricated new enum
    /// member.</para>
    /// </summary>
    public static AtomRejection InstantiateBossFirstClearGrant(
        ContainerRow container,
        Func<string, AtomRow?> lookupAtom,
        Func<string, AffixRow?> lookupAffix,
        ulong lootSeed,
        int grantIndex,
        int thetaBoss,
        PowerTuning tuning,
        long catalogRevision,
        out InstanceRow? instance,
        InstanceOrigin origin = InstanceOrigin.Drop)
    {
        if (container is null) throw new ArgumentNullException(nameof(container));
        if (tuning is null) throw new ArgumentNullException(nameof(tuning));

        var streamName = LootStreams.RollSeed(grantIndex);
        var rollSeed = unchecked((long)SeededRng.DeriveStream(lootSeed, streamName).NextULong());

        return Instantiator.TryInstantiate(
            container, lookupAtom, lookupAffix, rollSeed, thetaBoss, tuning, out instance,
            origin, catalogRevision);
    }
}
