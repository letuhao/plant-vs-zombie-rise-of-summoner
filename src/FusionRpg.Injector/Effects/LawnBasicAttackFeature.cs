namespace FusionRpg.Injector.Effects;

/// <summary>
/// lawn-combat-wire T10/T12's shared kill switch (lawn-combat-wire-plan.md "Kill-switch env var name":
/// fixed here so both tasks agree). Disables grant-binding (T10) AND cost-charging (T12) together —
/// with it off, the lawn is byte-identical to today: no actor ever holds an `OnDamageDealt` grant, so
/// `EffectRuntime.HasOnDamageDealtGrant()` stays false exactly as it is without this feature at all.
///
/// <para>Default ON (mirrors <see cref="OverlayCombatFeature"/>'s own default-enabled toggle) — the
/// risk this exists for is a MEASURED perf breach (T13's live re-measurement), not an a-priori doubt,
/// so shipping default-off would just be reintroducing the wiring gap this program exists to close.
/// The env var is a true kill switch: "0" forces off regardless of any debug override, for a host with
/// no cheat document loaded (e.g. an automated probe).</para>
///
/// <para><b>2026-09-14 correction (lawn-combat-wire T10/T12 live-inert investigation):</b> this used to
/// read <c>CheatState.On(CheatToggleId)</c> directly, borrowing <c>CheatSchema.EffectiveToggle</c>'s
/// own default-true fallback as this flag's ONLY source of "on by default." That is a boundary defect,
/// not a bug in <c>CheatState</c> — that class's own doc comment names its real, narrower job:
/// "Session cheat registry keyed by coverage ids from cheat-menu-coverage.md," an ephemeral debug/QA
/// toggle store. It never promised to be a durable, always-correct production feature-flag source, and
/// nothing about its contract guarantees a fresh/rehydrated/reset session reaches combat still reading
/// the fallback the way a real flag must. A real always-on-by-default production switch cannot borrow
/// its default from a system whose own contract does not promise one — the SOLID-boundary hard rule
/// this repo's CLAUDE.md states ("depend on registered abstractions, not a mode-local concrete
/// composer," the identical shape one level down). <see cref="DefaultOn"/> below is now this module's
/// OWN default, read first; <c>CheatState</c> is consulted only as an OPTIONAL, EXPLICIT debug/QA
/// override — never as the thing that decides what "default" means. <c>OVERLAY-COMBAT</c> (this
/// class's own sibling, referenced above) has the identical shape and is left alone here — same-class
/// finding, separate task, not fixed in this pass.</para>
/// </summary>
public static class LawnBasicAttackFeature
{
    public const string CheatToggleId = "LAWN-BASIC-ATTACK";
    public const string EnvVar = "FUSIONRPG_LAWN_BASIC_ATTACK";

    /// <summary>This module's own default — true, independent of CheatState/CheatSchema/CheatRegistry.
    /// The single source of truth for "on unless something explicitly overrides it."</summary>
    public const bool DefaultOn = true;

    // Env is read once — this gate sits on the spawn/die path, same discipline as OverlayCombatFeature.
    static readonly bool EnvForcedOff =
        string.Equals(Environment.GetEnvironmentVariable(EnvVar), "0", StringComparison.Ordinal);

    /// <summary>An explicit debug/QA override ONLY — null (meaning "defer to <see cref="DefaultOn"/>")
    /// whenever nobody has ever toggled <see cref="CheatToggleId"/> this session
    /// (<c>CheatState.IsUserSet</c> false). Deliberately never reads <c>CheatState.On</c>'s own
    /// schema-fallback value as a default candidate — only a REAL, explicit user/script toggle counts
    /// as an override, exactly so a debug endpoint or the (currently nonexistent) web checkbox can
    /// still force this off for a controlled A/B measurement without this flag's default ever again
    /// depending on CheatState's rehydrate/reset timing.</summary>
    static bool? DebugOverride => CheatState.IsUserSet(CheatToggleId) ? CheatState.On(CheatToggleId) : null;

    public static bool Enabled => !EnvForcedOff && (DebugOverride ?? DefaultOn);
}
