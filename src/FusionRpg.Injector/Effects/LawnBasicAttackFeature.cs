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
/// The env var is a true kill switch: "0" forces off regardless of the cheat toggle, for a host with no
/// cheat document loaded (e.g. an automated probe).</para>
/// </summary>
public static class LawnBasicAttackFeature
{
    public const string CheatToggleId = "LAWN-BASIC-ATTACK";
    public const string EnvVar = "FUSIONRPG_LAWN_BASIC_ATTACK";

    // Env is read once — this gate sits on the spawn/die path, same discipline as OverlayCombatFeature.
    static readonly bool EnvForcedOff =
        string.Equals(Environment.GetEnvironmentVariable(EnvVar), "0", StringComparison.Ordinal);

    public static bool Enabled => !EnvForcedOff && CheatState.On(CheatToggleId);
}
