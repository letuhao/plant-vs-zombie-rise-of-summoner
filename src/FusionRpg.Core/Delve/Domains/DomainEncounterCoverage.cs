using FusionRpg.Core.Battle;
using FusionRpg.Core.Delve.Encounter;
using FusionRpg.Core.Delve.Roll;
using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Dungeon.Tuning;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;
// The `Encounter` class lives IN the `FusionRpg.Core.Delve.Encounter` namespace -- an unqualified
// `Encounter.Build(...)` is ambiguous with the namespace segment itself, the same collision
// `EncounterSeedContentTests.cs` already names and fixes the same way.
using static FusionRpg.Core.Delve.Encounter.Encounter;

namespace FusionRpg.Core.Delve.Domains;

/// <summary>One domain's own coverage sample — spec-encounter-generator.md §8's own
/// `EncounterCoverage.Report(domain, rung, seeds)` citation, run for real (D4.31, party-dungeon-todo.md,
/// 2026-09-07). `RoomRefusals` names every `roomId` whose own `Encounter.Build` call threw
/// `EncounterRefusal` at least once across the sample — "a refusal names any domain that cannot fill a
/// slot" (D4.31's own verify line) — rather than letting one starved slot crash the whole report.</summary>
public sealed record DomainEncounterCoverageReport(
    string DomainId, int DistinctCells, bool MeetsBudget, IReadOnlyList<string> RoomRefusals);

/// <summary>
/// D4.31's own production bridge — `EncounterCoverage.cs`'s own pure counting functions (D2.7, already
/// shipped: <see cref="EncounterCoverage.DistinctCells"/>/<see cref="EncounterCoverage.MeetsBudget"/>)
/// stay UNTOUCHED, matching that file's own "this module owns none of the seeding or looping itself"
/// posture — this class OWNS the seeding/looping/domain-resolution the spec's own citation describes,
/// the same "row's own adapter, not the pure primitive" split D4.17's rows 4/6/7 already established.
///
/// <para>Samples ONLY `fight`/`elite`/`boss` archetypes (spec §8's own "a domain's fight/elite/boss
/// rooms" — `rest`/`merchant`/`curio`/`shrine`/`trap`/`wild`/`cache` never resolve through
/// `Encounter.Build` at all) drawn from the domain's own real `roomPalette`, resolved through each
/// room's own `encounterRef`. A `boss`-formation anchor has its `BossSpeciesRef` patched to the
/// domain's own real `bossSpeciesRef` first — the seed anchor itself never authors one (seed contract
/// §1.6: "not a species — the domain pins it at runtime"), the same patch
/// `EncounterSeedContentTests.cs` already applies for its own goldens.</para>
///
/// <para><b>Sibling-collision reporting (spec §8's own "StS rule") is NOT built here</b> — it needs an
/// actual rolled `DelveGraph` to know which encounters share a graph ROW (D4.17 row 4's own bridge
/// shape), a real, separate, larger piece than the distinct-cells/budget/refusal report this class
/// provides; named, not silently folded in as if already covered.</para>
/// </summary>
public static class DomainEncounterCoverage
{
    public static DomainEncounterCoverageReport Report(
        DomainRow domain,
        IReadOnlyList<string> roomPalette,
        IReadOnlyDictionary<string, RoomPaletteEntry> roomsById,
        IReadOnlyDictionary<string, string?> roomEncounterRefById,
        IReadOnlyDictionary<string, EncounterAnchor> encountersById,
        IReadOnlyList<ConcreteAnchor> corpus,
        int roomTheta,
        RaidModeTuning raid,
        DifficultyRungTuning rung,
        EncounterTuning tuning,
        DemonThreatTuning threatTuning,
        AptitudeTuning aptitudeTuning,
        PowerTuning powerTuning,
        int sampleSeeds,
        int budgetTarget,
        int budgetToleranceUnder = 0)
    {
        if (domain is null) throw new ArgumentNullException(nameof(domain));
        if (roomPalette is null) throw new ArgumentNullException(nameof(roomPalette));
        if (roomsById is null) throw new ArgumentNullException(nameof(roomsById));
        if (roomEncounterRefById is null) throw new ArgumentNullException(nameof(roomEncounterRefById));
        if (encountersById is null) throw new ArgumentNullException(nameof(encountersById));
        if (corpus is null) throw new ArgumentNullException(nameof(corpus));
        if (raid is null) throw new ArgumentNullException(nameof(raid));
        if (rung is null) throw new ArgumentNullException(nameof(rung));
        if (tuning is null) throw new ArgumentNullException(nameof(tuning));
        if (threatTuning is null) throw new ArgumentNullException(nameof(threatTuning));
        if (aptitudeTuning is null) throw new ArgumentNullException(nameof(aptitudeTuning));
        if (powerTuning is null) throw new ArgumentNullException(nameof(powerTuning));

        ElementTypeId? climate = ElementRoster.TryParse(domain.Climate, out var c) ? c : null;
        var cells = new List<EncounterCell>();
        var refusedRooms = new List<string>();

        foreach (var roomId in roomPalette)
        {
            if (!roomsById.TryGetValue(roomId, out var room)) continue;
            if (room.Kind is not ("fight" or "elite" or "boss")) continue;
            if (!roomEncounterRefById.TryGetValue(roomId, out var encounterRef) || encounterRef is null) continue;
            if (!encountersById.TryGetValue(encounterRef, out var anchor)) continue;

            var resolved = anchor.Formation == Formation.Boss ? anchor with { BossSpeciesRef = domain.BossSpeciesRef } : anchor;

            for (var i = 0; i < sampleSeeds; i++)
            {
                var streamName = $"domain-encounter-coverage:{domain.DomainId}:{roomId}:{i}";
                var seed = SeededRng.DeriveStream(0, streamName).NextULong();
                try
                {
                    var half = Build(resolved, roomTheta, climate, raid, rung, seed, corpus, tuning, threatTuning, aptitudeTuning, powerTuning);
                    cells.Add(half.Cell);
                }
                catch (EncounterRefusal)
                {
                    if (!refusedRooms.Contains(roomId, StringComparer.Ordinal)) refusedRooms.Add(roomId);
                }
            }
        }

        var distinct = EncounterCoverage.DistinctCells(cells);
        var meetsBudget = refusedRooms.Count == 0 && EncounterCoverage.MeetsBudget(distinct, budgetTarget, budgetToleranceUnder);
        return new DomainEncounterCoverageReport(domain.DomainId, distinct, meetsBudget, refusedRooms);
    }
}
