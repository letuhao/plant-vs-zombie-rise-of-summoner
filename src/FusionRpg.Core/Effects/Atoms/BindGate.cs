using System.Text.Json;

namespace FusionRpg.Core.Effects.Atoms;

/// <summary>What the host binding into can actually do. Supplied by the caller, never guessed.</summary>
/// <param name="Runtime">Lawn, battle or sim.</param>
/// <param name="IsPlanner">
/// True when the host produces a plan rather than applying it. <see cref="RuntimeState.PlanOnly"/>
/// kinds bind only here — anywhere else they would be accepted and then apply nothing.
/// </param>
/// <param name="HasWorldHost">Whether `sector:` / `slot:` scopes can resolve.</param>
/// <param name="OwnerLevel">The owner's level, for `level_req`. Null when the host has no notion of one.</param>
public readonly record struct BindContext(
    RuntimeId Runtime,
    bool IsPlanner = false,
    bool HasWorldHost = false,
    int? OwnerLevel = null);

/// <summary>
/// The bind-time gate. Load-time validation (E4/E5) proves a row is <b>well-formed</b>; this proves
/// it is <b>executable here</b>. Both reject; neither ignores.
///
/// <para>The same container may bind on the lawn and be rejected in battle. That is correct and
/// expected — battle consumes one opcode today, and the runtime matrix is a living audited table.</para>
/// </summary>
public static class BindGate
{
    /// <summary>
    /// Judge one binding. <paramref name="atoms"/> is the instance's atom set; every one must be
    /// executable, because a partially-bound instance is the silent no-op this layer refuses.
    /// </summary>
    public static AtomRejection Check(
        IReadOnlyList<AtomRow> atoms,
        OwnerScope owner,
        BindContext ctx,
        int? levelReq = null,
        Func<string, bool>? atomIsLive = null)
    {
        if (atoms is null) return AtomRejection.Fail(AtomRejectionReason.BadParamValue, "no atoms");

        // World scopes need a world host. Accepting them elsewhere would bind a building buff to
        // nothing at all.
        if ((owner.Kind == OwnerKind.Sector || owner.Kind == OwnerKind.Slot) && !ctx.HasWorldHost)
            return AtomRejection.Fail(AtomRejectionReason.ScopeUnsupported,
                $"{owner} needs a world host; this runtime has none");

        if (levelReq is { } req && ctx.OwnerLevel is { } level && level < req)
            return AtomRejection.Fail(AtomRejectionReason.LevelTooLow,
                $"level_req {req}, owner is level {level}");

        foreach (var atom in atoms)
        {
            if (atomIsLive is not null && !atomIsLive(atom.AtomId))
                return AtomRejection.Fail(AtomRejectionReason.StaleInstance,
                    $"{atom.AtomId} is withdrawn or disabled");

            if (!atom.Enabled)
                return AtomRejection.Fail(AtomRejectionReason.StaleInstance, $"{atom.AtomId} is disabled");

            var kind = AtomKindRegistry.Get(atom.KindId);
            if (kind is null)
                return AtomRejection.Fail(AtomRejectionReason.UnknownKind, atom.KindId);

            var support = kind.SupportIn(ctx.Runtime);
            switch (support)
            {
                case RuntimeState.None:
                    return AtomRejection.Fail(AtomRejectionReason.RuntimeUnsupported,
                        $"{atom.AtomId}: {atom.KindId} has no consumer in {ctx.Runtime}");

                case RuntimeState.PlanOnly when !ctx.IsPlanner:
                    // Collapsing PlanOnly into Full is how sim silently accepts bindings it cannot
                    // execute — the exact no-op the four-state matrix exists to prevent.
                    return AtomRejection.Fail(AtomRejectionReason.RuntimeUnsupported,
                        $"{atom.AtomId}: {atom.KindId} is plan-only in {ctx.Runtime} and this host is not a planner");
            }

            var scope = CheckScope(atom, kind, owner);
            if (!scope.IsOk) return scope;
        }

        return AtomRejection.Ok;
    }

    /// <summary>
    /// G8, corrected. The `TakeDamage` prefix reads <b>one side-wide cached value</b>, so
    /// <c>stat.modify</c> on <c>defense</c> does nothing for <i>any</i> per-entity or per-type
    /// binding — not only <c>entity:</c>. An earlier rule rejected `entity:` alone and left `plant:N`
    /// and `zombie:N` silently dead, which is worse than rejecting all three.
    ///
    /// <para>Per-actor mitigation is <c>stat.derived</c> on <c>combat.defense.*</c>; per-entity
    /// primary defense waits for perf O5.</para>
    /// </summary>
    static AtomRejection CheckScope(AtomRow atom, AtomKind kind, OwnerScope owner)
    {
        if (!string.Equals(atom.KindId, "stat.modify", StringComparison.Ordinal))
            return AtomRejection.Ok;

        if (owner.Kind == OwnerKind.Match) return AtomRejection.Ok;

        return ChannelOf(atom) == "defense"
            ? AtomRejection.Fail(AtomRejectionReason.ScopeUnsupported,
                $"{atom.AtomId}: primary defense is a single side-wide value, so it is legal only at " +
                $"`match` scope — '{owner}' would bind something that never applies. Use stat.derived " +
                "on combat.defense.* for a per-actor effect.")
            : AtomRejection.Ok;
    }

    static string? ChannelOf(AtomRow atom)
    {
        if (string.IsNullOrWhiteSpace(atom.ParamsJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(atom.ParamsJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("channel", out var c)
                   && c.ValueKind == JsonValueKind.String
                ? c.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null; // E4 already refused this row; never throw on a bind path
        }
    }
}
