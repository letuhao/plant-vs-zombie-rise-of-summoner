namespace FusionRpg.Core.Stats.Aptitudes;

/// <summary>Closed auto-assign rule ids (spec-aptitude-auto-assign).</summary>
public static class AptitudeAutoAssignRules
{
    public const string Even = "even";
    public const string PostureForce = "posture-force";
    public const string PostureFinesse = "posture-finesse";
    public const string PostureBastion = "posture-bastion";
    public const string ActivePreset = "active-preset";
    public const string SpeciesFavour = "species-favour";
}

public sealed record AptitudeAutoAssignResult(
    bool Ok,
    string Reason,
    IReadOnlyDictionary<string, long> Shares,
    long Leftover);

/// <summary>
/// Draft-only fills (AS-3.3). Never POSTs. Leftover after fill is legal (E2).
/// Magnitudes are <c>long</c>; widen before multiply; /1000 last (S6).
/// </summary>
public static class AptitudeAutoAssign
{
    /// <summary>Even split of budget across twelve aptitudes; remainder left as leftover.</summary>
    public static AptitudeAutoAssignResult FillEven(long budget)
    {
        if (budget < 0)
            return Fail("presets.budget.negative");

        var shares = new Dictionary<string, long>(StringComparer.Ordinal);
        var n = AptitudeCatalog.Count;
        if (n <= 0) return Fail("aptitudes.emptyCatalog");

        long each;
        checked { each = budget / n; }
        long spent = 0;
        foreach (var apt in AptitudeCatalog.All)
        {
            shares[apt.Id] = each;
            checked { spent += each; }
        }

        return new AptitudeAutoAssignResult(true, "", shares, budget - spent);
    }

    /// <summary>
    /// Lean budget into one posture (4 aptitudes get equal share of budget; others 0).
    /// Remainder is leftover.
    /// </summary>
    public static AptitudeAutoAssignResult FillPosture(long budget, Posture posture)
    {
        if (budget < 0)
            return Fail("presets.budget.negative");

        var inPosture = AptitudeCatalog.All.Where(a => a.Posture == posture).ToList();
        if (inPosture.Count == 0)
            return Fail("autoAssign.posture.empty");

        var shares = AptitudeCatalog.All.ToDictionary(a => a.Id, _ => 0L, StringComparer.Ordinal);
        long each;
        checked { each = budget / inPosture.Count; }
        long spent = 0;
        foreach (var apt in inPosture)
        {
            shares[apt.Id] = each;
            checked { spent += each; }
        }

        return new AptitudeAutoAssignResult(true, "", shares, budget - spent);
    }

    /// <summary>
    /// Scale favour permille (S1) to budget. Empty favour → refuse with named reason (S7).
    /// </summary>
    public static AptitudeAutoAssignResult FillFromPermille(
        long budget,
        IReadOnlyDictionary<string, long> sharesPermille)
    {
        if (budget < 0)
            return Fail("presets.budget.negative");
        if (sharesPermille is null || sharesPermille.Count == 0)
            return Fail("autoAssign.favour.empty");

        var rows = new List<AptitudePresetRowSpec>(AptitudeCatalog.Count);
        foreach (var apt in AptitudeCatalog.All)
        {
            if (!sharesPermille.TryGetValue(apt.Id, out var pm))
                return Fail("autoAssign.favour.incomplete");
            rows.Add(new AptitudePresetRowSpec(apt.Id, pm));
        }

        var materialized = AptitudePresetMaterialize.Materialize(rows, budget);
        if (!materialized.Ok)
            return new AptitudeAutoAssignResult(false, materialized.Reason, materialized.Shares, materialized.Leftover);

        return new AptitudeAutoAssignResult(true, "", materialized.Shares, materialized.Leftover);
    }

    /// <summary>Active-preset path — shared D13 materialize (G11).</summary>
    public static AptitudeAutoAssignResult FillFromPresetRows(
        long budget,
        IReadOnlyList<AptitudePresetRowSpec> rows)
        => ToAuto(AptitudePresetMaterialize.Materialize(rows, budget));

    /// <param name="favourAllowed">
    /// Mode A/B only. Mode C must pass <c>false</c> — species-favour is refused with a named reason.
    /// </param>
    public static AptitudeAutoAssignResult Fill(
        string ruleId,
        long budget,
        IReadOnlyDictionary<string, long>? favourPermille = null,
        IReadOnlyList<AptitudePresetRowSpec>? activePresetRows = null,
        bool favourAllowed = true)
    {
        return ruleId switch
        {
            AptitudeAutoAssignRules.Even => FillEven(budget),
            AptitudeAutoAssignRules.PostureForce => FillPosture(budget, Posture.Force),
            AptitudeAutoAssignRules.PostureFinesse => FillPosture(budget, Posture.Finesse),
            AptitudeAutoAssignRules.PostureBastion => FillPosture(budget, Posture.Bastion),
            AptitudeAutoAssignRules.ActivePreset =>
                activePresetRows is null
                    ? Fail("autoAssign.activePreset.missing")
                    : FillFromPresetRows(budget, activePresetRows),
            AptitudeAutoAssignRules.SpeciesFavour =>
                !favourAllowed
                    ? Fail("autoAssign.favour.modeC")
                    : FillFromPermille(budget, favourPermille ?? new Dictionary<string, long>()),
            _ => Fail("autoAssign.rule.unknown")
        };
    }

    static AptitudeAutoAssignResult ToAuto(AptitudePresetMaterializeResult m) =>
        new(m.Ok, m.Reason, m.Shares, m.Leftover);

    static AptitudeAutoAssignResult Fail(string reason) =>
        new(false, reason, new Dictionary<string, long>(StringComparer.Ordinal), 0);
}
