using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Loadout;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Data.Sqlite;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// T21 (action-todo.md, spec-loadout.md §2). <see cref="LoadoutSet"/> has its own tests in Core;
/// this covers what only exists once a database is involved — a real reject-leaves-existing-rows-
/// untouched round trip, `kindOf` resolved from the real `rpg_action` table (not a mock), and "no
/// loadout row at all" reading back as `null`.
/// </summary>
public class LoadoutStoreTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public LoadoutStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-loadouts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    string SeedContainerAndAtom(string containerId, string family)
    {
        var atomId = AtomRow.DeriveId(family, "", 1);
        var atomResult = _store.UpsertAtom(new AtomRow
        {
            AtomId = atomId,
            KindId = "stat.modify",
            FamilyId = family,
            Variant = "",
            Tier = 1,
            ParamsJson = "{\"channel\":\"maxHp\",\"op\":\"flat\",\"amount\":1}",
        });
        Assert.True(atomResult.IsOk, atomResult.ToString());

        var containerResult = _store.UpsertContainer(new ContainerRow
        {
            ContainerId = containerId,
            Kind = ContainerKind.Skill,
            Atoms = new[] { new ContainerAtomRow(0, atomId) },
        });
        Assert.True(containerResult.IsOk, containerResult.ToString());
        return atomId;
    }

    void SeedAction(string actionId, ActionKind kind, string containerId)
    {
        SeedContainerAndAtom(containerId, "atom." + actionId.Replace('.', '-'));
        var write = _store.UpsertAction(new ActionRow
        {
            ActionId = actionId,
            Name = actionId,
            Kind = kind,
            ContainerId = containerId,
            Grantable = kind == ActionKind.Skill,
            Tags = kind == ActionKind.Skill ? new[] { ActionTag.Offensive } : Array.Empty<ActionTag>(),
        });
        Assert.True(write.IsOk, write.ToString());
    }

    static readonly OwnerScope Actor = new(OwnerKind.Entity, "abc123");

    [Fact]
    public void NoLoadoutRowAtAllReadsBackAsNull()
    {
        Assert.Null(_store.GetLoadout(Actor));
    }

    [Fact]
    public void AValidSetOfFiveHeldSkillsPersistsInOrdinalOrder()
    {
        SeedAction("skill.a", ActionKind.Skill, "skill.a-container");
        SeedAction("skill.b", ActionKind.Skill, "skill.b-container");

        var result = _store.SetLoadout(
            Actor, new[] { "skill.a", "skill.b" },
            isHeld: _ => true, isMidRun: () => false);

        Assert.True(result.Ok);
        var loaded = _store.GetLoadout(Actor);
        Assert.NotNull(loaded);
        Assert.Equal(new[] { "skill.a", "skill.b" }, loaded);
    }

    [Fact]
    public void ASixthEntryRejectsAndLeavesTheExistingLoadoutUntouched()
    {
        SeedAction("skill.a", ActionKind.Skill, "skill.a-container");
        SeedAction("skill.b", ActionKind.Skill, "skill.b-container");
        _store.SetLoadout(Actor, new[] { "skill.a" }, isHeld: _ => true, isMidRun: () => false);

        var attempt = new[] { "skill.a", "skill.b", "s3", "s4", "s5", "s6" };
        var result = _store.SetLoadout(Actor, attempt, isHeld: _ => true, isMidRun: () => false);

        Assert.False(result.Ok);
        Assert.Equal(LoadoutRejectionReason.LoadoutFull, result.Reason);
        // The rejection must not have touched the PREVIOUS, already-persisted loadout.
        Assert.Equal(new[] { "skill.a" }, _store.GetLoadout(Actor));
    }

    [Fact]
    public void KindOfIsResolvedFromTheRealActionTableNotAMock()
    {
        // "kindOf IS wired for real here" -- proven by seeding a genuine `basic` action and letting
        // SetLoadout discover its kind from `rpg_action` itself, with no kindOf override to fake it.
        SeedAction("act.attack", ActionKind.Basic, "skill.act-attack-container");

        var result = _store.SetLoadout(Actor, new[] { "act.attack" }, isHeld: _ => true, isMidRun: () => false);

        Assert.False(result.Ok);
        Assert.Equal(LoadoutRejectionReason.IntrinsicNotEquippable, result.Reason);
        Assert.Equal("act.attack", result.ActionId);
    }

    [Fact]
    public void MidRunRejectsAndPersistsNothing()
    {
        SeedAction("skill.a", ActionKind.Skill, "skill.a-container");

        var result = _store.SetLoadout(Actor, new[] { "skill.a" }, isHeld: _ => true, isMidRun: () => true);

        Assert.False(result.Ok);
        Assert.Equal(LoadoutRejectionReason.MidRun, result.Reason);
        Assert.Null(_store.GetLoadout(Actor));
    }

    [Fact]
    public void DifferentOwnersAreIsolated()
    {
        SeedAction("skill.a", ActionKind.Skill, "skill.a-container");
        var other = new OwnerScope(OwnerKind.Entity, "def456");

        _store.SetLoadout(Actor, new[] { "skill.a" }, isHeld: _ => true, isMidRun: () => false);

        Assert.Equal(new[] { "skill.a" }, _store.GetLoadout(Actor));
        Assert.Null(_store.GetLoadout(other));
    }

    [Fact]
    public void SettingAnEmptyLoadoutIsLegalAndClearsAnyPreviousSet()
    {
        SeedAction("skill.a", ActionKind.Skill, "skill.a-container");
        _store.SetLoadout(Actor, new[] { "skill.a" }, isHeld: _ => true, isMidRun: () => false);

        var result = _store.SetLoadout(Actor, Array.Empty<string>(), isHeld: _ => true, isMidRun: () => false);

        Assert.True(result.Ok);
        Assert.Null(_store.GetLoadout(Actor)); // zero rows reads back as null, same as never-set
    }

    [Fact]
    public void NoLoadoutRowAutoEquipsFromTheHeldSkillCandidates()
    {
        var candidates = new[]
        {
            new AutoEquipCandidate("skill.strong", Rung: 5),
            new AutoEquipCandidate("skill.weak", Rung: 1),
        };

        var result = _store.GetLoadoutOrAutoEquip(Actor, candidates);

        Assert.Equal(new[] { "skill.strong", "skill.weak" }, result);
    }

    [Fact]
    public void ARealLoadoutRowTakesPriorityOverAutoEquip()
    {
        SeedAction("skill.chosen", ActionKind.Skill, "skill.chosen-container");
        _store.SetLoadout(Actor, new[] { "skill.chosen" }, isHeld: _ => true, isMidRun: () => false);

        var candidates = new[] { new AutoEquipCandidate("skill.would-be-auto-equipped", Rung: 9) };
        var result = _store.GetLoadoutOrAutoEquip(Actor, candidates);

        Assert.Equal(new[] { "skill.chosen" }, result);
    }
}
