using FusionRpg.Core.Battle;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items.Drops;

namespace FusionRpg.Core.Items.Sockets;

/// <summary>
/// Where sockets come from (spec-sockets.md §5). Pure: one derived count, no stored column, so
/// nothing can drift — <c>RpgStore.Loot.cs</c>'s <c>item_generation</c> deliberately has no
/// <c>socket_count</c>, and D2 §6 makes <c>item_socket</c> the SSOT for the current state.
///
/// <code>
/// socketsAtDrop = min( baseType.socketMax, roll(rarity.socket_min .. rarity.socket_max, socketSeed) )
/// socketsNow    = socketsAtDrop + socket-add operations, capped at baseType.socketMax
/// </code>
///
/// <para><c>socketSeed = SeededRng.DeriveStream(roll_seed, "item.socket")</c> — the same stream
/// <see cref="LootStreams.Sockets"/> already derives and advances at step 10, so landing the real
/// count here moves no other draw. <b>Never <c>System.Random</c></b>: definitions §13 D5 is explicit
/// that a seeded <c>System.Random</c> sequence is not stable across .NET versions and would move
/// goldens with no content change.</para>
/// </summary>
public static class SocketGeometry
{
    /// <summary>
    /// The socket count an item is dropped with. <paramref name="entrySocketMax"/> is the base type's
    /// own declared value, which module 6 validates against its role's ceiling; this method clamps to
    /// it rather than re-reading the registry, so the two can never disagree at runtime.
    /// </summary>
    public static int SocketsAtDrop(int entrySocketMax, string rarityRungId, ulong rollSeed, SocketTuning tuning)
    {
        if (tuning is null) throw new ArgumentNullException(nameof(tuning));
        if (entrySocketMax < 0)
            throw new ArgumentOutOfRangeException(nameof(entrySocketMax), entrySocketMax,
                "a base type's socketMax is a count, never negative");

        if (!tuning.RarityGrant.TryGetValue(rarityRungId, out var window))
            throw new ArgumentOutOfRangeException(nameof(rarityRungId), rarityRungId,
                "no socket grant window for this rung — the tuning parser refuses a table with a missing rung, so " +
                "reaching here means the caller invented a rung id");

        // The draw runs even when the window is a single value, so a later widening of a rung's
        // window cannot shift a different rung's stream. Same reasoning as step 10's own advance.
        var rng = SeededRng.DeriveStream(rollSeed, LootStreams.Sockets);
        var grant = window.Min + rng.NextInt(window.Max - window.Min + 1);

        return Math.Min(grant, entrySocketMax);
    }

    /// <summary>
    /// The count after crafting. D23: <c>socket-add</c> is available at <b>every</b> rarity and the
    /// MATERIAL COST scales with the target's rung (module 14 prices it) — so a bad socket roll is a
    /// cost, not a discard, which is what removes most of §8.1's pressure.
    /// </summary>
    public static int SocketsNow(int socketsAtDrop, int socketAddOperations, int entrySocketMax)
    {
        if (socketAddOperations < 0)
            throw new ArgumentOutOfRangeException(nameof(socketAddOperations), socketAddOperations,
                "an operation count is never negative");

        return Math.Min(socketsAtDrop + socketAddOperations, entrySocketMax);
    }

    /// <summary>
    /// Module 6's per-entry value against this module's per-role ceiling. ⛔ The check is
    /// <b>"never exceeds its role's ceiling"</b>, not spec-sockets.md §3's stronger "fixed per role,
    /// never varied per base type" — the shipped 740-entry corpus varies within a role by design
    /// (module 6 measured <c>armament-primary</c> at <c>{0:18, 1:26, 2:4}</c>), so the stronger
    /// invariant is contradicted by real data and enforcing it would refuse the corpus.
    /// </summary>
    public static AtomRejection ValidateEntry(ItemRole role, int entrySocketMax, SocketTuning tuning)
    {
        if (tuning is null) throw new ArgumentNullException(nameof(tuning));

        var ceiling = tuning.CeilingFor(role);
        if (entrySocketMax < 0 || entrySocketMax > ceiling)
            return SocketRules.Violated(SocketRules.EntryExceedsRoleCeiling,
                $"role '{ItemRoles.Id(role)}' allows [0..{ceiling}] sockets, the base type declares {entrySocketMax}");

        return AtomRejection.Ok;
    }

    /// <summary>
    /// spec-sockets.md §4's geometric ceiling, stated rather than assumed: only a role whose ceiling
    /// reaches D20's four-ingredient count can host a Strain or a Splice at all.
    /// </summary>
    public static IReadOnlyList<ItemRole> RolesThatCanHostAStrain(SocketTuning tuning) =>
        tuning.SocketCeiling
            .Where(kv => kv.Value >= tuning.StrainSpliceIngredientCount)
            .Select(kv => kv.Key)
            .OrderBy(r => (int)r)
            .ToList();
}
