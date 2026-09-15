using FusionRpg.Contracts;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Combat;

/// <summary>
/// lawn-combat-wire T10 (spec-basic-attack-grant.md): the one construction site for the per-actor
/// basic-attack effect grant that makes <c>EffectRuntime.HasOnDamageDealtGrant()</c> true on the lawn.
///
/// <para>The def is already shipped, purpose-built (`data/seed/atoms/fx-core.json:33`, compiled into
/// <c>EffectAtomCatalog.CreateAll()</c> as <see cref="EffectId"/>): <c>kind: resource.delta</c>,
/// <c>trigger: OnDamageDealt</c>. Nothing binds it to any actor today — this is that bind, built the
/// SAME shape <c>AtomCompiler.EmitDefAndGrant</c> already bakes for a compiled owner (Phase 7 F3.1):
/// an <c>elementPayload</c> built by <see cref="HybridPayload.BuildOverlay"/> straight from the owner's
/// OWN species element, never authored on the action row (`action-ideal.md:158-160`) — that is why one
/// shared <c>act.attack</c> row still yields per-species elemental damage.</para>
///
/// <para>Pure and Unity-free on purpose: the injector (`LawnBasicAttackGrantBinder`) resolves the
/// owner's element and calls this once per spawn; this class only builds the DTO.</para>
/// </summary>
public static class BasicAttackGrantBuilder
{
    /// <summary>The compiled def this grant points at — see `EffectAtomCatalog.Generated.cs`.</summary>
    public const string EffectId = "fx.overlay_damage";

    const string PluginId = "lawn-basic-attack";
    const string GrantPrefix = "lawn-basic-attack";

    /// <summary>
    /// One grant for <paramref name="ptr"/>, scoped to `entity:{ptr}` — never `plant:{typeId}` /
    /// `zombie:{typeId}` (the spec's own boundary: a type-scoped grant cannot express one specimen's
    /// own element, which is the whole point). <paramref name="ptr"/> travels verbatim into the owner
    /// key and the grant id; the caller is responsible for handing it the same spelling it will later
    /// use to withdraw (already true today — `GameHooks.ForgetEntity`'s withdraw path compares owner
    /// keys via `StatApplyScope.Normalize`, so an exact-casing mismatch cannot leak a stale grant).
    ///
    /// <para>Deterministic <c>GrantId</c> — a repeat bind for the same ptr (e.g. a re-resolve after a
    /// side change, or two coalesced spawn records racing at drain time) is an idempotent upsert, never
    /// a duplicate grant.</para>
    /// </summary>
    public static EffectGrantDto Build(
        string ptr, ElementTypeId? primary, ElementTypeId? secondary = null, int secondaryWeightMilli = 0)
    {
        if (string.IsNullOrWhiteSpace(ptr))
            throw new ArgumentException("ptr is required.", nameof(ptr));

        var normalized = ptr.Trim();
        var overlayElementPayload = HybridPayload.BuildOverlay(primary, secondary, secondaryWeightMilli);

        return new EffectGrantDto
        {
            GrantId = GrantIdFor(normalized),
            EffectId = EffectId,
            OwnerKind = "entity",
            OwnerKey = EffectOwnerKeys.Entity(normalized),
            PluginId = PluginId,
            Priority = 0,
            // Null for a Neutral owner (no primary element) — OverlayCombatMath.Finalize passes the
            // amount through unchanged with no payload, the documented degenerate case; never an
            // empty-list payload riding the overlay.
            Overlay = overlayElementPayload == null
                ? null
                : new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["elementPayload"] = overlayElementPayload,
                },
        };
    }

    /// <summary>Keyed on the canonical ptr (<see cref="CombatPtr.Normalize"/>): "1A2B", "1a2b" and
    /// "0x1a2b" are one entity and must upsert one grant, never hold two that both fire.</summary>
    public static string GrantIdFor(string ptr) => GrantPrefix + "@" + CombatPtr.Normalize(ptr);
}
