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
        Register(catalog, "leech", StatusKind.OverTime, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.PulseHp);
        Register(catalog, "expose", StatusKind.Debuff, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.ModifyStat);
        Register(catalog, "command", StatusKind.Meter, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.ModifyStat);
        Register(catalog, "shatter", StatusKind.Debuff, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.ModifyStat);
        Register(catalog, "charm_pulse", StatusKind.CrowdControl, "overlay", StatusL2bCategory.Cc, StatusStacking.Replace, StatusPayloadKind.UnityCc);

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
}
