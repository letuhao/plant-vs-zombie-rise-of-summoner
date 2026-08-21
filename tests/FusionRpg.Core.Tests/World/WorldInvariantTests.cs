using System.Globalization;
using FusionRpg.Core.World;
using Xunit;

namespace FusionRpg.Core.Tests.World;

/// <summary>
/// Review findings (2026-08-21), each proven by a failing test before the fix — the three
/// invariants wave 1 leans on that nothing was enforcing.
/// </summary>
public class WorldInvariantTests
{
    [Theory]
    [InlineData(" seat ")]
    [InlineData("seat ")]
    [InlineData(" seat")]
    public void Ids_with_surrounding_whitespace_are_rejected(string id)
    {
        // Trimming before the check lets a padded id through; catalog lookups are ordinal, so the
        // stored id would then never match anything.
        Assert.Throws<InvalidOperationException>(() => WorldIds.RequireKebab(id, "Slot type id"));
    }

    [Fact]
    public void Canonical_form_is_culture_invariant()
    {
        // The canonical text is the hash input for replay. A culture whose negative sign is not
        // ASCII '-' must not change it — first-light has negative layout coordinates.
        var world = WorldTemplateCatalog.Build(WorldTemplateCatalog.FirstLightId, seed: 7);

        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var invariant = WorldCanonical.Write(world);

            CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
            var swedish = WorldCanonical.Write(world);

            Assert.Equal(invariant, swedish);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void An_intact_guard_without_an_encounter_is_rejected()
    {
        // Otherwise the slot can never be cleared, and Rule "every slot cleared" makes its sector
        // permanently unclaimable — a soft-locked map that validates fine.
        var world = WorldTemplateCatalog.Build(WorldTemplateCatalog.FirstLightId, seed: 1);
        var sectorIndex = world.Sectors.ToList().FindIndex(s => s.SectorId == "ember-hollow");
        var sector = world.Sectors[sectorIndex];

        var broken = world with
        {
            Sectors = world.Sectors
                .Select((s, i) => i != sectorIndex
                    ? s
                    : s with
                    {
                        Slots = s.Slots
                            .Select(sl => sl.SlotIndex != 1
                                ? sl
                                : sl with { GuardState = GuardState.Intact, GuardWaveId = null })
                            .ToList()
                    })
                .ToList()
        };

        var ex = Assert.Throws<InvalidOperationException>(() => WorldValidation.Validate(broken));
        Assert.Contains("ember-hollow", ex.Message);
    }

    [Fact]
    public void A_cleared_guard_may_keep_its_encounter_id_as_history()
    {
        var world = WorldTemplateCatalog.Build(WorldTemplateCatalog.FirstLightId, seed: 1);
        var sectorIndex = world.Sectors.ToList().FindIndex(s => s.SectorId == "ember-hollow");

        var cleared = world with
        {
            Sectors = world.Sectors
                .Select((s, i) => i != sectorIndex
                    ? s
                    : s with
                    {
                        Slots = s.Slots.Select(sl => sl with { GuardState = GuardState.Cleared }).ToList()
                    })
                .ToList()
        };

        WorldValidation.Validate(cleared); // must not throw — "what was here" is worth remembering
    }
}
