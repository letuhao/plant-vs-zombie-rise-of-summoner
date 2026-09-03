using System.Text.Json;
using FusionRpg.Core.Actions.Seeding;

namespace FusionRpg.Core.Actions.Eligibility;

/// <summary>
/// A-E1 (spec-eligibility-axis.md §3.2): the one query the whole action-corpus program needs — "who
/// may hold this action." Pure, free of I/O, exactly like <see cref="ActionValidator"/> and
/// <see cref="EnablerPayoffPairings"/> stay free of it: the caller supplies every row and every
/// lookup, so nothing here can silently read a stale catalog.
/// </summary>
public static class ActionEligibility
{
    /// <summary>
    /// <c>candidates(actor) = { general } ∪ { family : scopeKey = familyOf(actor) } ∪
    /// { species : scopeKey = actor.speciesKey }</c> — §3.2, sorted by <c>actionId</c> ordinal so
    /// every downstream roll sees a deterministic order.
    ///
    /// <para><b>The failure mode this guards against:</b> a <c>family</c>/<c>species</c> row whose
    /// <see cref="ActionRow.ScopeKey"/> is <c>null</c> or empty never matches, even when the actor's
    /// own key is also <c>null</c> or empty — two nulls comparing equal is exactly the accident that
    /// would make a mis-authored row universal (§4's worst-case).</para>
    /// </summary>
    /// <param name="actions">Every candidate row to filter — the caller decides scope (a whole
    /// catalog, one container, etc.).</param>
    /// <param name="actorSpeciesKey">The acting actor's opaque species key, or <c>null</c> for an
    /// actor with none.</param>
    /// <param name="familyOf">A load-time <c>speciesKey → familyId</c> lookup (§3.2's decided
    /// mapping — <see cref="FamilyMap"/> parses its committed source). A miss (actor absent, or the
    /// actor's family unassigned) must be treated as "no family" by the caller, never guessed.</param>
    public static IReadOnlyList<ActionRow> Candidates(
        IEnumerable<ActionRow> actions,
        string? actorSpeciesKey,
        IReadOnlyDictionary<string, string> familyOf)
    {
        string? familyId = null;
        if (!string.IsNullOrEmpty(actorSpeciesKey))
            familyOf.TryGetValue(actorSpeciesKey, out familyId);

        var matches = new List<ActionRow>();
        foreach (var a in actions)
        {
            var isMatch = a.Scope switch
            {
                EligibilityScope.General => true,
                EligibilityScope.Family =>
                    !string.IsNullOrEmpty(a.ScopeKey) && !string.IsNullOrEmpty(familyId) &&
                    string.Equals(a.ScopeKey, familyId, StringComparison.Ordinal),
                EligibilityScope.Species =>
                    !string.IsNullOrEmpty(a.ScopeKey) && !string.IsNullOrEmpty(actorSpeciesKey) &&
                    string.Equals(a.ScopeKey, actorSpeciesKey, StringComparison.Ordinal),
                _ => false,
            };
            if (isMatch) matches.Add(a);
        }

        matches.Sort((x, y) => string.CompareOrdinal(x.ActionId, y.ActionId));
        return matches;
    }
}

/// <summary>
/// A-E1 (spec-eligibility-axis.md §3.2, decided 2026-09-03): the <c>speciesKey → familyId</c> mapping
/// <see cref="ActionEligibility.Candidates"/> needs, parsed from the committed projection
/// (<c>data/seed/actions/_generated/family-map.json</c>) of <c>family-assignments.json</c> — a flat
/// object because the source relation is a function (no species carries two families; the projection
/// that emitted this file refused any list of length other than 1). This type stays free of I/O, same
/// discipline as <see cref="EnablerPayoffPairings.Parse"/>: the caller reads the file, this parses the
/// string.
/// </summary>
public static class FamilyMap
{
    public static IReadOnlyDictionary<string, string> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("family-map: root must be an object of speciesKey -> familyId");

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException($"family-map: '{prop.Name}' must be a single family id string");
            map[prop.Name] = prop.Value.GetString()!;
        }
        return map;
    }
}
