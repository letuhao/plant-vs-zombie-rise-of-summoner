using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Rungs;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Data.Sqlite;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>aura-skill T19 (audit D3 part two): "the correctness half of the owner's 'both' answer —
/// equipped actions resolve properly rather than being silently dropped by T3's degrade path."
/// `RpgStore.BuildActionCatalog` is the FIRST production caller of `ActionCompiler.Compile` at bulk
/// scale — before this task, `BattleEngine.Resolve` was NEVER given a non-null `ActionCatalog`
/// anywhere in production, so any non-empty `EquippedActionIds` always hit T3's degrade path,
/// regardless of whether the equipped action was perfectly valid. `RungPolicy` is configured globally
/// for this assembly (`ContractTuningTestBootstrap`'s `[ModuleInitializer]`), matching production's
/// own `Program.cs` startup sequence.</summary>
public class ActionCatalogBuilderTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public ActionCatalogBuilderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-actioncatalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    string SeedSkillAction(string actionId)
    {
        var containerId = "skill." + actionId.Replace('.', '-') + "-container";
        var atomId = AtomRow.DeriveId("atom." + actionId.Replace('.', '-'), "", 1);
        var atomResult = _store.UpsertAtom(new AtomRow
        {
            AtomId = atomId,
            KindId = "stat.modify",
            FamilyId = "atom." + actionId.Replace('.', '-'),
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

    [Fact]
    public void An_empty_store_builds_an_empty_catalog_never_throws()
    {
        var catalog = _store.BuildActionCatalog(RungPolicy.Table);
        Assert.Equal(0, catalog.Count);
    }

    [Fact]
    public void A_real_authored_skill_compiles_and_resolves_from_the_built_catalog()
    {
        // The exact D3 fix, proven at the level this task actually builds: before T19, NOTHING ever
        // called ActionCompiler.Compile against a real ActionRow in production -- a valid skill would
        // compile in isolation (ActionCompilerTests.cs) but never reach a real ActionCatalog.
        var actionId = SeedSkillAction("skill.catalog-real-resolve");

        var catalog = _store.BuildActionCatalog(RungPolicy.Table);

        var compiled = catalog.Get(actionId);
        Assert.NotNull(compiled);
        Assert.Equal(actionId, compiled!.ActionId);
        Assert.Equal(ActionKind.Skill, compiled.Kind);
    }

    [Fact]
    public void A_rejected_row_is_skipped_not_fatal_to_the_whole_catalog()
    {
        // "Skipped, not fatal" -- one bad row (here: a container that no longer exists, simulating an
        // authoring mistake) must not take down every OTHER action's ability to resolve.
        var good = SeedSkillAction("skill.catalog-good");
        var badResult = _store.UpsertAction(new ActionRow
        {
            ActionId = "skill.catalog-bad",
            Name = "skill.catalog-bad",
            Kind = ActionKind.Skill,
            Rung = 1,
            ContainerId = "skill.catalog-bad-container", // never created -- UnknownContainer at compile time
            Grantable = true,
            Tags = new[] { ActionTag.Offensive },
        });
        // UpsertAction itself may or may not reject an unknown container at write time depending on
        // its own validation scope -- either is fine for this test, which only needs the ROW to exist
        // (or not) and the GOOD row to still compile regardless.
        _ = badResult;

        List<(string ActionId, ActionRejection Rejection)> rejected = new();
        var catalog = _store.BuildActionCatalog(RungPolicy.Table, (id, r) => rejected.Add((id, r)));

        Assert.NotNull(catalog.Get(good));
    }
}
