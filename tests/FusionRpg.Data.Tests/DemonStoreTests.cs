using FusionRpg.Contracts;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

public class DemonStoreTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public DemonStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-demon-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    // Mint enforces catalog discipline — tests must use a real generated species.
    static readonly FusionRpg.Core.Demons.DemonSpeciesDef CatalogSpecies =
        FusionRpg.Core.Demons.DemonSpeciesCatalog.All.First(s => s.Side == "zombie");

    static DemonMintSpec Spec(string? species = null) => new()
    {
        SpeciesId = species ?? CatalogSpecies.SpeciesId,
        Side = "zombie",
        GameTypeId = CatalogSpecies.GameTypeId,
        Rarity = "chaff",
        Variant = "normal",
        ElementPrimary = "fire",
        TraitIds = new List<string> { "swift" },
        Origin = "summon"
    };

    [Fact]
    public void Mint_creates_actor_profile_and_codex_atomically()
    {
        var (specimen, newlyDiscovered) = _store.MintDemon(1, Spec());
        Assert.True(newlyDiscovered);
        Assert.Equal("Roster", specimen.Actor.Phase);
        Assert.Equal(CatalogSpecies.SpeciesId, specimen.Profile.SpeciesId);
        Assert.Equal(specimen.Actor.InstanceId, specimen.Profile.InstanceId);

        var roster = _store.ListDemonRoster(1);
        Assert.Single(roster.Items);
        var codex = _store.ListDemonCodex(1);
        Assert.Single(codex.Entries);
        Assert.Equal(DemonCodexStates.Discovered, codex.Entries[0].State);
    }

    [Fact]
    public void Mint_for_unknown_player_leaves_nothing()
    {
        Assert.Throws<InvalidOperationException>(() => _store.MintDemon(999, Spec()));
        Assert.Empty(_store.ListDemonRoster(999).Items);
        Assert.Empty(_store.ListDemonCodex(999).Entries);
    }

    [Fact]
    public void Second_mint_of_same_species_is_not_newly_discovered()
    {
        var (_, first) = _store.MintDemon(1, Spec());
        var (_, second) = _store.MintDemon(1, Spec());
        Assert.True(first);
        Assert.False(second);
        Assert.Equal(2, _store.ListDemonRoster(1).Items.Count);
        Assert.Single(_store.ListDemonCodex(1).Entries);
    }

    [Fact]
    public void Codex_lattice_never_downgrades()
    {
        _store.UpsertDemonCodex(1, "hell-hound", DemonCodexStates.Discovered);
        _store.UpsertDemonCodex(1, "hell-hound", DemonCodexStates.Seen); // must be a no-op downgrade
        var codex = _store.ListDemonCodex(1);
        Assert.Equal(DemonCodexStates.Discovered, Assert.Single(codex.Entries).State);

        _store.UpsertDemonCodex(1, "wisp", DemonCodexStates.Seen);
        Assert.Equal(DemonCodexStates.Seen, _store.ListDemonCodex(1).Entries.Single(e => e.SpeciesId == "wisp").State);
        var newly = _store.UpsertDemonCodex(1, "wisp", DemonCodexStates.Discovered);
        Assert.True(newly);
        Assert.Equal(DemonCodexStates.Discovered, _store.ListDemonCodex(1).Entries.Single(e => e.SpeciesId == "wisp").State);
    }

    [Fact]
    public void Nickname_and_lock_bump_revision()
    {
        var (specimen, _) = _store.MintDemon(1, Spec());
        var id = specimen.Profile.InstanceId;
        var (ok1, p1) = _store.SetDemonNickname(id, "Ragnar");
        Assert.True(ok1);
        Assert.Equal("Ragnar", p1!.Nickname);
        Assert.Equal(1, p1.Revision);
        var (ok2, p2) = _store.SetDemonLocked(id, true);
        Assert.True(ok2);
        Assert.True(p2!.Locked);
        Assert.Equal(2, p2.Revision);
    }

    [Fact]
    public void Mint_rejects_bad_args()
    {
        Assert.Throws<ArgumentException>(() =>
            _store.MintDemon(1, new DemonMintSpec { SpeciesId = "", Side = "zombie" }));
        Assert.Throws<ArgumentException>(() =>
            _store.MintDemon(1, new DemonMintSpec { SpeciesId = "x", Side = "mower" }));
    }

    [Fact]
    public void Mint_enforces_catalog_discipline()
    {
        // Review I3: unknown ids reject at the write gate; whitespace species trims to ONE codex key.
        Assert.Throws<ArgumentException>(() => _store.MintDemon(1, Spec("no-such-species")));
        var badTrait = Spec();
        badTrait.TraitIds = new List<string> { "nonsense-trait" };
        Assert.Throws<ArgumentException>(() => _store.MintDemon(1, badTrait));
        var badVariant = Spec();
        badVariant.Variant = "holographic";
        Assert.Throws<ArgumentException>(() => _store.MintDemon(1, badVariant));
        var badRarity = Spec();
        badRarity.Rarity = "mythic";
        Assert.Throws<ArgumentException>(() => _store.MintDemon(1, badRarity));
        Assert.Empty(_store.ListDemonRoster(1).Items); // nothing leaked through

        _store.MintDemon(1, Spec("  " + CatalogSpecies.SpeciesId + "  "));
        _store.MintDemon(1, Spec());
        Assert.Single(_store.ListDemonCodex(1).Entries); // trimmed — no split codex keys
    }
}
