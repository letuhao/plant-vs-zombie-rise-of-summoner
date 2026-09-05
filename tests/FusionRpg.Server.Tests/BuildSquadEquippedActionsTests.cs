using FusionRpg.Contracts;
using FusionRpg.Core.Actions;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>
/// T22 (action-todo.md): "the auto-equipped set appears in the battle report — otherwise a dominant
/// auto-loadout is invisible to a matrix that compares allocations, not loadouts." Closed 2026-08-28
/// by wiring `WebMatchService.BuildSquad` to `RpgStore.GetLoadoutOrAutoEquip` — this proves the wiring
/// through the REAL production seam (a real summoned specimen, a real action grant, the real
/// `BuildSquad` entry point), not a re-implementation of `AutoEquip`'s own already-exhaustively-tested
/// ranking logic (that's `AutoEquipTests.cs`'s job).
/// </summary>
public class BuildSquadEquippedActionsTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;
    readonly WebMatchService _service;

    public BuildSquadEquippedActionsTests()
    {
        // ExecuteSummon's real mint path reaches SummonBannerCatalog/SummonRoller (SummoningTuningHub)
        // AND ContractPolicy (auto-bind on mint) -- neither covered by this assembly's
        // [ModuleInitializer] bootstrap, so configured here exactly like AptitudeChannelModsTests'
        // own RealBattle test configures the policies IT needs beyond that bootstrap.
        var tuningDir = Path.Combine(FindRepoRoot(), "data", "tuning");
        string Read(string name) => File.ReadAllText(Path.Combine(tuningDir, name));
        SummoningTuningHub.Configure(SummoningTuningLoader.Parse(Read("summoning.v1.json")));
        FusionRpg.Core.Demons.Contracts.ContractPolicy.Configure(
            FusionRpg.Core.Demons.Contracts.ContractTuningLoader.Parse(Read("contracts.v1.json")));
        SoulEarnPolicy.Configure(SoulEarnTuningLoader.Parse(Read("souls.v1.json")));
        // RollTraits -> FusionRoller.SlotsFor now reads StarPolicy.Tuning.SlotsByRarity
        // (seed-to-concrete T4.1 moved the old hardcoded switch into fusion.v1.json), so the mint
        // path this test drives needs StarPolicy configured too, exactly like AptitudeChannelModsTests.
        FusionRpg.Core.Demons.Fusion.StarPolicy.Configure(
            FusionRpg.Core.Demons.Fusion.FusionTuningLoader.Parse(Read("fusion.v1.json")));

        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-buildsquad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR();
        var provider = services.BuildServiceProvider();
        var hub = provider.GetRequiredService<IHubContext<RpgHub>>();
        _service = new WebMatchService(_store, hub);
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    string SeedSkillAction(string actionId)
    {
        var containerId = actionId + "-container";
        var atomId = FusionRpg.Core.Effects.Atoms.AtomRow.DeriveId(
            "atom." + actionId.Replace('.', '-'), "", 1);
        var atomResult = _store.UpsertAtom(new FusionRpg.Core.Effects.Atoms.AtomRow
        {
            AtomId = atomId,
            KindId = "stat.modify",
            FamilyId = "atom." + actionId.Replace('.', '-'),
            Variant = "",
            Tier = 1,
            Name = actionId,
            ParamsJson = "{\"channel\":\"maxHp\",\"op\":\"flat\",\"amount\":1}",
        });
        Assert.True(atomResult.IsOk, atomResult.ToString());

        var containerResult = _store.UpsertContainer(new FusionRpg.Core.Effects.Atoms.ContainerRow
        {
            ContainerId = containerId,
            Kind = FusionRpg.Core.Effects.Atoms.ContainerKind.Skill,
            Atoms = new[] { new FusionRpg.Core.Effects.Atoms.ContainerAtomRow(0, atomId) },
        });
        Assert.True(containerResult.IsOk, containerResult.ToString());

        var actionResult = _store.UpsertAction(new ActionRow
        {
            ActionId = actionId,
            Name = actionId,
            Kind = ActionKind.Skill,
            Rung = 1,
            ContainerId = containerId,
            Grantable = true,
            Tags = new[] { ActionTag.Offensive },
        });
        Assert.True(actionResult.IsOk, actionResult.ToString());
        return actionId;
    }

    static (long PlayerId, string InstanceId) SummonOneSpecimen(RpgStore store, string correlationSuffix, ulong rngSeed)
    {
        var playerId = store.CreatePlayer("buildsquad-test-" + correlationSuffix).Id;
        store.AwardSouls(playerId, 10_000, SoulEarnPolicy.Reasons.Seed, "test-bankroll");
        var (ok, reason, outcome) = store.ExecuteSummon(
            playerId, SummonBannerCatalog.StandardRift, 1, "c-buildsquad-" + correlationSuffix, rngSeed, focusElementId: null);
        Assert.True(ok, reason);
        return (playerId, Assert.Single(outcome!.Specimens).Profile.InstanceId);
    }

    [Fact]
    public void A_specimen_with_no_grant_and_no_loadout_row_auto_equips_from_nothing_but_basics()
    {
        // No SeedSkillAction call at all -- this specimen holds zero skills, exactly the "no
        // candidates" state most demons are in today (T22's own honest admission: nothing in
        // production grants an action to a demon instance yet). BuildSquad must still succeed and
        // must not throw reaching for a loadout/rung table that was never configured.
        var (playerId, instanceId) = SummonOneSpecimen(_store, "no-grant", rngSeed: 1);

        var (ok, reason, squad, _) = _service.BuildSquad(playerId, new[] { instanceId });

        Assert.True(ok, reason);
        var actor = Assert.Single(squad!);
        Assert.NotNull(actor.EquippedActionIds);
    }

    [Fact]
    public void A_real_skill_grant_reaches_the_built_squads_EquippedActionIds()
    {
        // The real point of the wire: a skill actually GRANTED to this specimen (via UpsertGrant,
        // the same table T23's ActionSetAssembler and T21's loadout both read) must appear on the
        // BattleActorSetup BuildSquad produces -- not just "the code compiles and BuildSquad still
        // returns a squad."
        var (playerId, instanceId) = SummonOneSpecimen(_store, "real-grant", rngSeed: 2);
        var actionId = SeedSkillAction("skill.buildsquad-fireball");

        var grantResult = _store.UpsertGrant(
            new ActionGrantRow(OwnerKind.Entity, instanceId, actionId, Source: "test"));
        Assert.True(grantResult.IsOk, grantResult.ToString());

        var (ok, reason, squad, _) = _service.BuildSquad(playerId, new[] { instanceId });

        Assert.True(ok, reason);
        var actor = Assert.Single(squad!);
        Assert.NotNull(actor.EquippedActionIds);
        Assert.Contains(actionId, actor.EquippedActionIds!);
    }

    [Fact]
    public void A_real_loadout_row_wins_over_auto_equip_exactly_as_GetLoadoutOrAutoEquip_documents()
    {
        var (playerId, instanceId) = SummonOneSpecimen(_store, "loadout-wins", rngSeed: 3);
        var granted = SeedSkillAction("skill.buildsquad-granted");
        var chosen = SeedSkillAction("skill.buildsquad-chosen");
        _store.UpsertGrant(new ActionGrantRow(OwnerKind.Entity, instanceId, granted, Source: "test"));
        _store.UpsertGrant(new ActionGrantRow(OwnerKind.Entity, instanceId, chosen, Source: "test"));

        var scope = new OwnerScope(OwnerKind.Entity, instanceId);
        var held = new HashSet<string> { granted, chosen };
        var setResult = _store.SetLoadout(scope, new[] { chosen }, held.Contains, () => false);
        Assert.True(setResult.Ok, setResult.ToString());

        var (ok, reason, squad, _) = _service.BuildSquad(playerId, new[] { instanceId });

        Assert.True(ok, reason);
        var actor = Assert.Single(squad!);
        Assert.Equal(new[] { chosen }, actor.EquippedActionIds);
    }

    [Fact]
    public void Two_specimens_of_the_same_species_carry_independent_loadouts()
    {
        // Keyed on the specimen's own instance id, never the player -- two demons one player owns
        // must not share a loadout just because they share a summon.
        var (playerId, a) = SummonOneSpecimen(_store, "independent-a", rngSeed: 4);
        _store.AwardSouls(playerId, 10_000, SoulEarnPolicy.Reasons.Seed, "test-bankroll-2");
        var (_, _, secondOutcome) = _store.ExecuteSummon(
            playerId, SummonBannerCatalog.StandardRift, 1, "c-buildsquad-independent-b", rngSeed: 5, focusElementId: null);
        var b = Assert.Single(secondOutcome!.Specimens).Profile.InstanceId;
        var skillA = SeedSkillAction("skill.buildsquad-a");
        _store.UpsertGrant(new ActionGrantRow(OwnerKind.Entity, a, skillA, Source: "test"));

        var (ok, reason, squad, _) = _service.BuildSquad(playerId, new[] { a, b });

        Assert.True(ok, reason);
        Assert.Equal(2, squad!.Count);
        var setupA = squad.Single(s => s.Key.EndsWith("0", StringComparison.Ordinal));
        var setupB = squad.Single(s => s.Key.EndsWith("1", StringComparison.Ordinal));
        Assert.Contains(skillA, setupA.EquippedActionIds!);
        Assert.DoesNotContain(skillA, setupB.EquippedActionIds ?? Array.Empty<string>());
    }
}
