using FusionRpg.Contracts;
using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Corpus;
using FusionRpg.Core.Actions.Eligibility;
using FusionRpg.Core.Actions.Rungs;
using FusionRpg.Core.Actions.Unlock;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Timeline;
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

    /// <summary>T59.8: process-wide statics (`RungPolicy`/`DemonSpeciesCatalog`'s own established
    /// shape), configured once for this whole test class -- an `AlwaysAccepts` tuning (no chance
    /// decay) so a controlled XP award lands an unlock deterministically, never a real random wait.</summary>
    static BuildSquadEquippedActionsTests()
    {
        UnlockTuningPolicy.Configure(new UnlockTuning(
            P1Milli: 1000, DeltaMilli: 1000, FloorMilli: 1000, HeldCap: 10, RungCap: 10, DiscardTaxCoeffMilli: 100));
        ActionFamilyMapPolicy.Configure(new Dictionary<string, string>());
    }

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
        // T59.8: AwardUniqueActorXp's own XpToNext call reads RpgXpCurve.Tuning -- not covered by
        // this assembly's [ModuleInitializer] bootstrap either (no prior test in this file awarded
        // specimen XP through a real level-up).
        FusionRpg.Core.Progression.ProgressionTuningHub.Configure(
            FusionRpg.Core.Progression.ProgressionTuningLoader.Parse(Read("progression.v1.json")));
        // T59.8: the real BattleEngine.Resolve call needs the same three tunables
        // ContractTuningTestBootstrap configures for Core.Tests -- none covered by this assembly's
        // own [ModuleInitializer] bootstrap. battle.v3.json, not v2 (v2 is stale -- missing
        // speciesTempo, the same drift AptitudeChannelModsTests is separately failing on).
        FusionRpg.Core.Battle.BattleTuningHub.Configure(
            FusionRpg.Core.Battle.BattleTuningLoader.Parse(Read("battle.v3.json")));
        FusionRpg.Core.Battle.BattleRuleset.ConfigureResources(
            FusionRpg.Core.Battle.BattleResourceTuningLoader.Parse(Read("battle-resources.v1.json")));
        FusionRpg.Core.Actions.ActionTimingPolicy.Configure(
            FusionRpg.Core.Actions.ActionTimingTuningLoader.Parse(Read("action-timing.v1.json")));

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

    static ActionCorpusCostTemplate CostTemplate() => new(new Dictionary<ActionCategory, ActionCorpusCostTemplateRow>
    {
        [ActionCategory.Attack] = new("qi", 20, ActionCostTiming.OnCommit),
        [ActionCategory.Defense] = new("qi", 30, ActionCostTiming.OnCommit),
        [ActionCategory.Support] = new("qi", 40, ActionCostTiming.OnCommit),
        [ActionCategory.Movement] = new("qi", 15, ActionCostTiming.OnCommit),
        [ActionCategory.Status] = new("qi", 35, ActionCostTiming.OnCommit),
    });

    /// <summary>
    /// T59.8 (spec-action-instance-and-grant.md criterion 4): summon → real corpus import (T59.3/
    /// T59.4's real pipeline, a hand-built brief — the REAL committed corpus mostly can't resolve
    /// today, T59.5's own honest finding, not a reason to weaken THIS proof) → award XP until the
    /// unlock ladder grants it (a controlled, `AlwaysAccepts`-tuned roll, never a real random wait) →
    /// `BuildSquad`. Proves the "equips" half of criterion 4 in full, the same shape T22's own
    /// `A_real_skill_grant_reaches_the_built_squads_EquippedActionIds` already proved, now for a
    /// GENERATED, IMPORTED action rather than a hand-authored one.
    ///
    /// <para>⛔ The "can activate" half is proven FALSE, honestly, not silently skipped: attempting a
    /// real `BattleEngine.Resolve` with this squad throws — no `IContainerEffectResolver` is wired
    /// into any real production battle path today (`WebMatchService.cs`'s own 3 call sites all pass
    /// none), and every real imported action has a non-empty container (T59.3's composer refuses to
    /// draw zero atoms), so this is not a corner case, it is EVERY real imported action, every time.
    /// A real, general, RpgStore-backed resolver is a genuinely separate module's worth of work
    /// (integrating `AtomCompiler`'s whole-catalog compile pass with `BattleEffectHost`'s effect
    /// registry) — named in `action-plan.md` §5's deferred table, not attempted unreviewed here.</para>
    /// </summary>
    [Fact]
    public void A_generated_imported_unlock_ladder_grant_reaches_BuildSquad_but_a_real_battle_cannot_yet_activate_it()
    {
        var atom = new AtomRow
        {
            AtomId = AtomRow.DeriveId("atom.e2e-test", "", 1), KindId = "stat.modify",
            FamilyId = "atom.e2e-test", Variant = "", Tier = 1, Name = "e2e test",
            ParamsJson = "{\"channel\":\"maxHp\",\"op\":\"flat\",\"amount\":1}",
        };
        Assert.Empty(_store.UpsertAtoms(new[] { atom }).Rejected);

        var brief = new ActionCorpusBrief(
            Id: "action.e2e.only", Name: "E2E Only", Category: "attack", Scope: "general", ScopeKey: null,
            RungFloor: 1, RungCeiling: 1, AtomFamilies: new[] { "atom.e2e-test" },
            TargetMode: "single", Relation: "enemy");
        var importResult = ActionCorpusImporter.Import(_store, new[] { brief }, CostTemplate(), RungPolicy.Table);
        Assert.Equal(1, importResult.ImportedCount);

        var (playerId, instanceId) = SummonOneSpecimen(_store, "e2e", rngSeed: 6);

        var (awardOk, awardReason, actor) = _store.AwardUniqueActorXp(instanceId, delta: 1_000_000);
        Assert.True(awardOk, awardReason);
        Assert.True(actor!.Level > 1); // liveness -- the level gain that should have triggered a roll actually happened

        var grants = _store.ListGrants(new OwnerScope(OwnerKind.Entity, instanceId));
        Assert.Contains(grants, g => g.ActionId == "action.e2e.only"); // "equips" half of criterion 4

        var (squadOk, squadReason, squad, _) = _service.BuildSquad(playerId, new[] { instanceId });
        Assert.True(squadOk, squadReason);
        var mySetup = Assert.Single(squad!);
        Assert.Contains("action.e2e.only", mySetup.EquippedActionIds!);

        // ⛔ Real, severe, NEWLY-discovered gap found here, not assumed -- the "can activate" half of
        // criterion 4 is NOT true yet, for any real imported action, and this is proven directly
        // below rather than silently worked around. `WebMatchService`'s own three real
        // `BattleEngine.Resolve` call sites (`WebMatchService.cs:134,186,315`) supply NO
        // `containerResolver` argument at all -- confirmed by direct read, not assumed. Every real
        // action T59.3's own composer produces has a NON-EMPTY container (it refuses to draw zero
        // atoms), so `BattleRunState.BindContainers` (`BattleRunState.cs:534-549`) throws for EVERY
        // real imported action, immediately, at battle setup, before any round runs. The only real
        // production resolver anywhere (`ConstructionActions.ContainerResolver`, siege-only, four
        // hand-compiled containers) is not general-purpose and is not wired into `WebMatchService`.
        // Building a real, general, RpgStore-backed resolver means integrating `AtomCompiler`'s
        // whole-catalog compile pass with `BattleEffectHost`'s own effect registry for every
        // action-holding actor -- a genuinely separate module's worth of work, never named anywhere
        // in this reopening's own A21/A22/A23 scope, not something to improvise unreviewed here.
        var catalog = _store.BuildActionCatalog(RungPolicy.Table);
        var setup = new BattleSetup
        {
            Squad = new[] { mySetup with { Key = "squad:0" } },
            Wave = new[] { new BattleActorSetup { Key = "wave:0", Side = "wave", MaxHp = 1_000_000, Level = 1 } },
        };

        var ex = Assert.Throws<ArgumentException>(() => BattleEngine.Resolve(setup, seed: 9001, actionCatalog: catalog));
        Assert.Contains("no IContainerEffectResolver was supplied", ex.Message, StringComparison.Ordinal);
    }
}
