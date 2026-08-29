namespace FusionRpg.Core.Status;

/// <summary>Register all 21 locked status ids — status-ssot.md §9.</summary>
public static class StatusCatalogBootstrap
{
    public static StatusCatalog CreateDefault()
    {
        var catalog = new StatusCatalog();
        RegisterAll(catalog);
        return catalog;
    }

    public static void RegisterAll(StatusCatalog catalog)
    {
        // 9.2 Engine wraps (UnityCc)
        Register(catalog, "butter", StatusKind.UnityCc, "cc", StatusL2bCategory.Cc, StatusStacking.Replace, StatusPayloadKind.UnityCc);
        Register(catalog, "freeze", StatusKind.UnityCc, "elemental", StatusL2bCategory.Cc, StatusStacking.Replace, StatusPayloadKind.UnityCc);
        Register(catalog, "cold", StatusKind.UnityCc, "elemental", StatusL2bCategory.Cc, StatusStacking.Replace, StatusPayloadKind.UnityCc);
        Register(catalog, "poison", StatusKind.UnityCc, "elemental", StatusL2bCategory.Dot, StatusStacking.Replace, StatusPayloadKind.UnityCc);
        Register(catalog, "hypno", StatusKind.UnityCc, "cc", StatusL2bCategory.Cc, StatusStacking.Replace, StatusPayloadKind.UnityCc);
        Register(catalog, "ember", StatusKind.UnityCc, "mixer", StatusL2bCategory.Cc, StatusStacking.Coexist, StatusPayloadKind.UnityCc);
        Register(catalog, "jala", StatusKind.UnityCc, "elemental", StatusL2bCategory.Cc, StatusStacking.Replace, StatusPayloadKind.UnityCc);
        Register(catalog, "kelp", StatusKind.UnityCc, "slow", StatusL2bCategory.Cc, StatusStacking.Replace, StatusPayloadKind.UnityCc);

        // 9.3 Overlay-authored
        Register(catalog, "wither", StatusKind.OverTime, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.PulseHp);
        Register(catalog, "bond", StatusKind.Counter, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.PulseHp);
        Register(catalog, "rally", StatusKind.Buff, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.ModifyStat);
        // pulseHealsAttacker: true -- spec-healing-pair.md §3, finishing the half the catalog shipped
        // half-built ("damage half only — the heal half was never built").
        RegisterWithOptions(catalog, "leech", StatusKind.OverTime, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh,
            new[] { StatusPayloadKind.PulseHp }, pulseHealsAttacker: true);
        Register(catalog, "expose", StatusKind.Debuff, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.ModifyStat);
        Register(catalog, "command", StatusKind.Meter, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.ModifyStat);
        Register(catalog, "shatter", StatusKind.Debuff, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.ModifyStat);
        // E17: corrected from UnityCc. NO vanilla method exists for it — an assembly-metadata sweep
        // of Assembly-CSharp found SetEmbered / SetJalaed / SetKelped but no SetCharm*, only
        // SetZombieWithMindControl / SetZombieMindControlledNode. Declaring UnityCc named an
        // execution path the game does not have, which is a DEF ERROR and not missing wiring.
        //
        // What that cost, concretely: FA2 is emitted only for UnityCc statuses
        // (StatusEffectBridge.cs:315), so every application queued an ApplyStatus action that
        // reached the injector's status switch, matched no case, and did nothing. An inert plan item
        // that looked like a working effect in every trace.
        //
        // ModifyStat is what an overlay-authored status can actually do now that the payload has a
        // consumer. Deliberately NOT faked with a float write — that is the applyFloatSlow path,
        // documented as weak and VFX-less, and it would make the status look implemented while doing
        // something else. It still CC-locks in battle: that reads the `cc` CATEGORY, which is
        // unchanged and is what the status means rather than how it is delivered.
        Register(catalog, "charm_pulse", StatusKind.CrowdControl, "overlay", StatusL2bCategory.Cc, StatusStacking.Replace, StatusPayloadKind.ModifyStat);

        // 9.4 Contagion
        Register(catalog, "blight", StatusKind.Contagion, "overlay", StatusL2bCategory.Contagion, StatusStacking.Refresh, StatusPayloadKind.Spread, StatusPayloadKind.PulseHp);
        Register(catalog, "rot", StatusKind.Contagion, "overlay", StatusL2bCategory.Contagion, StatusStacking.Refresh, StatusPayloadKind.Spread, StatusPayloadKind.PulseHp);
        Register(catalog, "spark", StatusKind.Contagion, "overlay", StatusL2bCategory.Contagion, StatusStacking.Refresh, StatusPayloadKind.Spread, StatusPayloadKind.PulseHp);
        Register(catalog, "pact_mark", StatusKind.Contagion, "overlay", StatusL2bCategory.Contagion, StatusStacking.Refresh, StatusPayloadKind.Spread, StatusPayloadKind.PulseHp);
        Register(catalog, "spore", StatusKind.Contagion, "overlay", StatusL2bCategory.Contagion, StatusStacking.Refresh, StatusPayloadKind.Spread, StatusPayloadKind.PulseHp);
    }

    static void Register(
        StatusCatalog catalog,
        string statusId,
        StatusKind kind,
        string family,
        string primaryCategory,
        StatusStacking stacking,
        params StatusPayloadKind[] payloadKinds)
    {
        catalog.Register(new StatusDef(
            statusId,
            kind,
            family,
            new[] { primaryCategory },
            Array.Empty<string>(),
            stacking,
            payloadKinds));
    }

    /// <summary>
    /// A separate name, not an overload of <see cref="Register"/> — <c>params</c> must be a method's
    /// last parameter, so it cannot sit before trailing optional ones, and giving both methods the
    /// same name at the same arity would make every existing 7-argument call site ambiguous between
    /// them. Only <c>leech</c> uses this one.
    /// </summary>
    static void RegisterWithOptions(
        StatusCatalog catalog,
        string statusId,
        StatusKind kind,
        string family,
        string primaryCategory,
        StatusStacking stacking,
        StatusPayloadKind[] payloadKinds,
        string? element = null,
        bool pulseHealsAttacker = false)
    {
        catalog.Register(new StatusDef(
            statusId,
            kind,
            family,
            new[] { primaryCategory },
            Array.Empty<string>(),
            stacking,
            payloadKinds,
            Element: element,
            PulseHealsAttacker: pulseHealsAttacker));
    }
}
