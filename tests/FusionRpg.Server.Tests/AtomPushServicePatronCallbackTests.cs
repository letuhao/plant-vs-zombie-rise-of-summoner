using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Contracts;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>
/// T6.2b (`patron-absorption`, 2026-09-06) — the spec's own "Revised testing strategy" table names
/// three tests at exactly this level (<see cref="AtomPushService"/> against a real <see cref="RpgStore"/>,
/// not <c>PatronAbsorptionGridEqualityTests</c>' isolated <c>AtomCompiler.Compile</c> call) that were
/// never actually written when the grid test shipped.
///
/// <para><b>A real, found-not-hypothetical gap.</b> Re-reading the spec's own table after the grid
/// test passed surfaced that <see cref="AtomPushService.Build"/>'s <c>ResolveBindings</c> loop can
/// NEVER discover <c>patron.aura</c>'s atoms for any owner — nothing ever creates a
/// <c>BindingRow</c> for it (a patron is designated via <c>RpgStore.SetPatron</c>, which writes
/// <c>rpg_patron</c>, never a binding, unlike gear/traits). Proving the compile MATH is right (the
/// grid test) never proved the DEF reaches a real push. Fixed the same session by adding
/// <c>AtomPushService.PatronAuraAtoms()</c> plus an isolated-compile, defs-only merge in
/// <c>Build</c> — these tests prove that wiring end to end, against a real store.</para>
/// </summary>
public class AtomPushServicePatronCallbackTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;
    readonly AtomPushService _push;

    public AtomPushServicePatronCallbackTests()
    {
        // MintDemon's real call chain auto-binds a contract for the new specimen, which needs these
        // three policies configured — not covered by this assembly's [ModuleInitializer] bootstrap,
        // same combination BuildSquadEquippedActionsTests.cs already needs for the same reason.
        var tuningDir = Path.Combine(RepoRoot(), "data", "tuning");
        string Read(string name) => File.ReadAllText(Path.Combine(tuningDir, name));
        SummoningTuningHub.Configure(SummoningTuningLoader.Parse(Read("summoning.v1.json")));
        ContractPolicy.Configure(ContractTuningLoader.Parse(Read("contracts.v1.json")));
        SoulEarnPolicy.Configure(SoulEarnTuningLoader.Parse(Read("souls.v1.json")));
        // PatronEndpoints.Compute -> ServerPowerIndexProvider.ActorIndex -> GetRpgProgressionSummary
        // needs this too — the player's own Θ read is part of the real live lookup being proven here.
        Core.Progression.ProgressionTuningHub.Configure(
            Core.Progression.ProgressionTuningLoader.Parse(Read("progression.v1.json")));
        // The real AuraMilli formula itself — the whole point of this suite.
        Core.Demons.Patron.PatronPolicy.Configure(
            Core.Demons.Patron.PatronTuningLoader.Parse(Read("patron.v1.json")));

        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-patron-push-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        SeedRealPatronAuraContent();
        _push = new AtomPushService(_store);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    /// <summary>The REAL committed `data/seed/atoms/patron-aura.json` + the real `patron.aura` shape,
    /// loaded from disk rather than hand-typed — this suite is worthless if it silently drifts from
    /// what the game actually ships. Mirrors `PatronAbsorptionGridEqualityTests`' own load pattern.</summary>
    void SeedRealPatronAuraContent()
    {
        var root = RepoRoot();
        var atomsPath = Path.Combine(root, "data", "seed", "atoms", "patron-aura.json");
        var collected = AtomSeedFile.Collect(new[] { (atomsPath, File.ReadAllText(atomsPath)) });
        Assert.True(collected.IsOk, string.Join("; ", collected.Errors));
        Assert.Equal(12, collected.Content.Atoms.Count);

        foreach (var atom in collected.Content.Atoms)
            Assert.True(_store.UpsertAtom(atom).IsOk);

        Assert.True(_store.UpsertContainer(new ContainerRow
        {
            ContainerId = "patron.aura",
            Kind = ContainerKind.Patron,
            Atoms = collected.Content.Atoms
                .Select((a, i) => new ContainerAtomRow(i + 1, a.AtomId))
                .ToList(),
        }).IsOk);
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }

    static readonly DemonSpeciesDef FireSpecies = DemonSpeciesCatalog.All
        .First(s => s.ElementPrimary.ToElementId() == "fire" && s.Acquisition != DemonAcquisition.CaptureOnly);
    static readonly DemonSpeciesDef DarkSpecies = DemonSpeciesCatalog.All
        .First(s => s.ElementPrimary.ToElementId() == "dark" && s.Acquisition != DemonAcquisition.CaptureOnly);

    string Mint(DemonSpeciesDef species)
    {
        var (specimen, _) = _store.MintDemon(1, new DemonMintSpec
        {
            SpeciesId = species.SpeciesId,
            Side = species.Side,
            GameTypeId = species.GameTypeId,
            Rarity = species.BaseRarity.ToId(),
            Variant = "normal",
            ElementPrimary = species.ElementPrimary.ToElementId(),
            ElementSecondary = species.ElementSecondary?.ToElementId(),
            TraitIds = new List<string> { species.TraitPool[0] },
            Origin = "summon",
        });
        return specimen.Actor.InstanceId;
    }

    static OwnerScope PlayerOwner(long id) =>
        new(OwnerKind.Player, id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    static BindContext Lawn() => new(RuntimeId.Lawn);

    static EffectDefDto PatronDef(AtomPushDto payload) =>
        Assert.Single(payload.Defs, d => d.EffectId == "fx.patron_aura");

    static long Flat(EffectDefDto def, string channel) =>
        Convert.ToInt64(def.Actions.Single(a => Equals(a.Params.GetValueOrDefault("channel"), channel)).Params["flat"]);

    [Fact]
    public void A_player_with_a_patron_set_gets_the_real_live_aura_baked_into_the_push()
    {
        var demon = Mint(FireSpecies);
        Assert.True(_store.SetPatron(1, demon, "corr-1").Ok);
        var expected = PatronEndpoints.Compute(_store, 1);
        Assert.NotNull(expected);

        var payload = _push.Build(PlayerOwner(1), Lawn(), matchSeed: 1);

        var def = PatronDef(payload);
        Assert.Equal(expected!.Value.Aura.PowerMilli, Flat(def, "combat.power.fire"));
        Assert.Equal(expected.Value.Aura.DefenseMilli, Flat(def, "combat.defense.fire"));
    }

    [Fact]
    public void A_player_with_no_patron_set_still_gets_the_def_but_every_channel_is_zero_not_a_throw()
    {
        var payload = _push.Build(PlayerOwner(1), Lawn(), matchSeed: 1);

        var def = PatronDef(payload);
        Assert.All(def.Actions, a => Assert.Equal(0L, Convert.ToInt64(a.Params["flat"])));
    }

    [Fact]
    public void The_auto_generated_compile_grant_is_never_pushed_only_the_def_the_plugin_grants_separately()
    {
        // AtomCompiler.Compile always emits at least a match-scoped grant per compiled group (its own
        // "grantOwnerKeys" doc: "Null is the shipped behaviour verbatim: one grant per ICD group at
        // Match"). AtomPushService.Build deliberately discards that auto-grant for patron.aura and
        // merges only its Defs — PatronSecondaryPlugin's own existing grant (GrantId "patron:aura")
        // must stay the SOLE grant, or the actor would carry two grants naming the same EffectId and
        // double the aura (GrantedDerivedAtomReader has no de-dup across grants).
        var demon = Mint(FireSpecies);
        Assert.True(_store.SetPatron(1, demon, "corr-2").Ok);

        var payload = _push.Build(PlayerOwner(1), Lawn(), matchSeed: 1);

        Assert.DoesNotContain(payload.Grants, g => g.EffectId == "fx.patron_aura");
        PatronDef(payload); // still asserts the def itself IS present, just ungranted by this path
    }

    [Fact]
    public void Two_pushes_after_a_patron_switch_reflect_the_new_designations_aura_immediately()
    {
        // "A promotion" (the spec's own named scenario) needs a real, rarity-matched, soul- and
        // material-funded fusion — a switch to a DIFFERENT designated patron proves the identical
        // underlying property this test exists for (nothing about the aura's rarity/star/level/Θ
        // inputs is cached or frozen between pushes) far more reliably than depending on fusion RNG.
        _store.AwardSouls(1, 500, "seed", "patron-push-bank");
        var first = Mint(FireSpecies);
        Assert.True(_store.SetPatron(1, first, "corr-3").Ok);
        var firstExpected = PatronEndpoints.Compute(_store, 1)!.Value.Aura;

        var firstPayload = _push.Build(PlayerOwner(1), Lawn(), matchSeed: 1);
        Assert.Equal(firstExpected.PowerMilli, Flat(PatronDef(firstPayload), "combat.power.fire"));

        var second = Mint(DarkSpecies);
        Assert.True(_store.SetPatron(1, second, "corr-4").Ok); // funded switch
        var secondExpected = PatronEndpoints.Compute(_store, 1)!.Value.Aura;
        Assert.NotEqual(firstExpected.ElementPrimary, secondExpected.ElementPrimary);

        // No receiverRevision echoed — a fresh cold rebuild, exactly like a reconnect after the switch.
        var secondPayload = _push.Build(PlayerOwner(1), Lawn(), matchSeed: 2);
        var secondDef = PatronDef(secondPayload);
        Assert.Equal(secondExpected.PowerMilli, Flat(secondDef, "combat.power.dark"));
        // And fire (the OLD patron's element) is back to zero — no leftover from the first push.
        Assert.Equal(0L, Flat(secondDef, "combat.power.fire"));
    }
}
