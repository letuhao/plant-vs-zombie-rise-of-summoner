using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Drops;
using FusionRpg.Core.Items.Materials;
using FusionRpg.Core.Items.Mutation;
using FusionRpg.Core.Items.Sockets;
using FusionRpg.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using FusionRpg.Data.Tests;

namespace FusionRpg.Server.Tests;

/// <summary>
/// ⭐ item modules 14/15/16 — <b>the workbench executor</b>, against a real in-process host, a real
/// store, the real shipped tuning and the real shipped recipe corpus.
///
/// <para>All three modules shipped their Core half green and all three recorded the same blocker:
/// <c>TrySpendRecipe</c>, <c>AppendMutationOp</c> and <c>SetSockets</c> had <b>zero production
/// callers</b>, so no path anywhere ran debit → act → persist on one stored item. These tests drive
/// that cycle through the HTTP surface for each of the three modules' own verbs and then <b>read the
/// state back</b> — a computed result that is never stored is exactly the thing this task exists to
/// stop reporting as done.</para>
///
/// <para>Every assertion about a price is made against <see cref="MaterialRecipeCatalog.Resolve"/>'s
/// own answer rather than against a number copied out of the tuning: the claim under test is "the
/// debit equals the resolved price", which a hardcoded quantity would turn into "the tuning still
/// says 4".</para>
/// </summary>
public class ItemWorkbenchEndpointsTests : IAsyncLifetime
{
    const string Rung = "cultivated";
    const string BaseTypeId = "item.workbench-base-a-001";
    const string ContainerId = "item.workbench-blade";
    const string Frame = "humanoid";
    const int ItemLevel = 24;
    const int SocketMax = 4;

    DataTestStore _testStore = null!;
    RpgStore _store = null!;
    WebApplication _app = null!;
    HttpClient _http = null!;
    long _playerId;
    string _playerKey = "";
    string _instanceId = "";

    MaterialTuning _materials = null!;
    MaterialRecipeCatalog _recipes = null!;
    EnhancementTuning _enhancement = null!;
    SocketTuning _sockets = null!;

    // ---- fixture -----------------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        _testStore = DataTestStore.Create();
        _store = _testStore.Store;
        _playerId = _store.GetCurrentPlayerId();
        _playerKey = _playerId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        _materials = MaterialTuning.Parse(File.ReadAllText(Tuning("materials.v1.json")));
        _enhancement = EnhancementTuning.Parse(File.ReadAllText(Tuning("enhancement.v1.json")));
        _sockets = SocketTuning.Parse(File.ReadAllText(Tuning("sockets.v1.json")));
        var rarity = ItemRarityTuning.Parse(File.ReadAllText(Tuning("item-rarity.v1.json")));
        _recipes = MaterialRecipeCatalog.Load(
            Directory.EnumerateFiles(Path.Combine(RepoRoot(), "data", "seed", "items", "recipes"), "*.json")
                .OrderBy(f => f, StringComparer.Ordinal).Select(File.ReadAllText),
            _materials);

        // The ladder's ROWS have to exist before a container may name a rung (ContainerValidator's
        // own check); `SeedRarityLadder` seeds the rarity_budget keys beside them, and the workbench
        // reads none of those directly — the card does.
        for (var i = 0; i < RarityLadder.RungIds.Count; i++)
            Assert.True(_store.UpsertRarity(new RarityRow(RarityLadder.RungIds[i], (i + 1) * 10, 3, 0, 1, 5)).Ok);
        _store.SeedRarityLadder(rarity);

        SeedItem();

        var bench = new ItemWorkbench(
            _store, _materials, _recipes, _enhancement, _sockets,
            BaseTypeSocketMaxCorpus.From(new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [BaseTypeId] = SocketMax,
            }),
            // item-content `item-naming` T4: the gem corpus the bench resolves an insert's element AND
            // name through, exactly as `Program.cs` hands it in. A tiny in-memory corpus rather than
            // the shipped `gems/*.json`, because the fixture's own gem is not a shipped id.
            GemInsertCorpus.From(new Dictionary<string, CardInsertLookup>(StringComparer.Ordinal)
            {
                ["gem.workbench-ember.t1"] = new CardInsertLookup(
                    new InsertDef("gem.workbench-ember.t1", "atom.elemental-power", "fire", 1),
                    "gem.ember-shard", "Ember Shard"),
            }));

        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(_store);
        builder.WebHost.UseUrls(baseUrl);
        _app = builder.Build();
        _app.UseDeveloperExceptionPage();
        _app.MapWorkbench(bench);
        await _app.StartAsync();

        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
        _testStore.Dispose();
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

    static string Tuning(string file) => Path.Combine(RepoRoot(), "data", "tuning", file);

    static int GetFreeTcpPort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    /// <summary>A real container, a real frozen instance, a real ownership row and a real generation
    /// stamp — the four things a workbench verb reads off "a stored item".</summary>
    void SeedItem()
    {
        var coreAtomId = AtomRow.DeriveId("atom.workbench-vitality", "", 1);
        Assert.True(_store.UpsertAtom(new AtomRow
        {
            AtomId = coreAtomId,
            KindId = "stat.modify",
            FamilyId = "atom.workbench-vitality",
            Variant = "",
            Tier = 1,
            Name = "workbench vitality",
            ParamsJson = """{"channel":"maxHp","op":"flat","amount":10}""",
        }).IsOk);

        var drawnAtomId = AtomRow.DeriveId("atom.workbench-ember", "fire", 2);
        Assert.True(_store.UpsertAtom(new AtomRow
        {
            AtomId = drawnAtomId,
            KindId = "stat.modify",
            FamilyId = "atom.workbench-ember",
            Variant = "fire",
            Tier = 2,
            Name = "workbench ember",
            ParamsJson = """{"channel":"atk","op":"flat","amount":5}""",
        }).IsOk);

        var upsert = _store.UpsertContainer(new ContainerRow
        {
            ContainerId = ContainerId,
            Kind = ContainerKind.Item,
            Slot = ItemRoles.Id(ItemRole.ArmamentPrimary),
            Rarity = Rung,
            Atoms = new[] { new ContainerAtomRow(0, coreAtomId) },
        });
        Assert.True(upsert.IsOk, upsert.ToString());

        _instanceId = _store.SaveInstance(new InstanceRow
        {
            ContainerId = ContainerId,
            RollSeed = 4242,
            CatalogRevision = _store.GetCatalogRevision(),
            Origin = InstanceOrigin.Drop,
            Atoms = new[]
            {
                new InstanceAtomRow(0, coreAtomId, """{"amount":10}"""),
                new InstanceAtomRow(1, drawnAtomId, """{"amount":5}"""),
            },
        });

        Assert.True(_store.AcquireItem(new RpgItemRow
        {
            InstanceId = _instanceId,
            PlayerId = _playerKey,
            AcquiredUtc = "2026-09-06T00:00:00Z",
            OriginKind = "drop",
        }).IsOk);

        _store.PersistLoot(
            _playerKey,
            new LootManifest("wb-drop", "table.workbench", 7UL, ItemLevel, Array.Empty<LootGrant>(),
                Array.Empty<string>(), "{}", LootPityState.Empty, LootPityState.Empty, null, false, null),
            "test", "workbench", _store.GetCatalogRevision(), 1,
            new[]
            {
                new ItemGenerationRow(_instanceId, 0, BaseTypeId, RungOrdinal(), ItemLevel, Frame,
                    ItemRoles.Id(ItemRole.ArmamentPrimary), "drop"),
            });
    }

    int RungOrdinal() => _store.ListRarities().First(r => r.RarityId == Rung).Ordinal;

    int RungIndex() => RarityLadder.RungIds.ToList().IndexOf(Rung);

    RecipeContext ItemContext(int enhanceLevel = 0) =>
        new(RungIndex(), IlvlTierLadder.MaxTierAt(ItemLevel), ItemLevel, Frame, enhanceLevel);

    /// <summary>Fund exactly the resolved price and not a unit more, so "sufficient" and "insufficient"
    /// are one material apart rather than separated by a comfortable float.</summary>
    void Fund(IReadOnlyList<MaterialCostLine> lines, long extraSouls = 0)
    {
        var souls = lines.Where(l => l.Class == MaterialClass.Souls).Sum(l => l.Qty) + extraSouls;
        if (souls > 0) _store.AwardSouls(_playerId, souls, "test.fund", Guid.NewGuid().ToString("N"));

        var materials = lines
            .Where(l => l.Class != MaterialClass.Souls)
            .Select(l => (l.MaterialId, l.Qty))
            .ToList();
        if (materials.Count > 0) _store.GrantMaterials(_playerId, materials);
    }

    long Balance(string materialId) => _store.GetMaterialQty(_playerId, materialId);

    async Task<(HttpStatusCode Status, JsonElement Body)> Post(string verb, object body)
    {
        var resp = await _http.PostAsJsonAsync($"/api/items/workbench/{verb}", body);
        var text = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return (resp.StatusCode, doc.RootElement.Clone());
    }

    // ---- module 15 `enhance-reroll` — the enhance verb ---------------------------------------------

    [Fact]
    public async Task Enhance_withoutTheMaterials_isRefusedAndSpendsNothing()
    {
        var price = _recipes.Resolve("recipe.012", ItemContext());
        var substrate = price.First(l => l.Class == MaterialClass.Substrate);

        // One short of the price on exactly one leg. Every other leg is fully funded, so a refusal
        // here can only be the leg that is short.
        Fund(price);
        _store.GrantMaterials(_playerId, new[] { (substrate.MaterialId, -1L) });
        var before = Balance(substrate.MaterialId);
        var soulsBefore = _store.GetSoulBalance(_playerId).Balance;

        var (status, body) = await Post("enhance", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.012",
            correlationId = "wb-enhance-short",
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("materials.insufficient", body.GetProperty("reason").GetString());

        // A refusal writes NOTHING — not the souls leg that came first in the fixed spend order,
        // not the op row, not the head.
        Assert.Equal(before, Balance(substrate.MaterialId));
        Assert.Equal(soulsBefore, _store.GetSoulBalance(_playerId).Balance);
        Assert.Empty(_store.ReadMutationOps(_instanceId));
        Assert.Equal(0, _store.GetInstanceMutationHead(_instanceId)!.EnhanceLevel);
    }

    [Fact]
    public async Task Enhance_withTheMaterials_debitsPersistsAndReadsBack()
    {
        var price = _recipes.Resolve("recipe.012", ItemContext());
        Fund(price);

        var soulsBefore = _store.GetSoulBalance(_playerId).Balance;
        var materialBefore = price
            .Where(l => l.Class != MaterialClass.Souls)
            .ToDictionary(l => l.MaterialId, l => Balance(l.MaterialId), StringComparer.Ordinal);

        var (status, body) = await Post("enhance", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.012",
            correlationId = "wb-enhance-1",
        });

        Assert.Equal(HttpStatusCode.OK, status);
        // +1 sits in the Safe band at 1000‰, so the outcome is deterministic and this asserts the
        // real decision rather than tolerating either answer.
        Assert.Equal("success", body.GetProperty("outcome").GetString());
        Assert.Equal(1, body.GetProperty("enhanceLevel").GetInt32());
        Assert.Equal(1, body.GetProperty("opSeq").GetInt32());

        // 1. the debit is real, and it is exactly the resolved price
        foreach (var line in price.Where(l => l.Class != MaterialClass.Souls))
            Assert.Equal(materialBefore[line.MaterialId] - line.Qty, Balance(line.MaterialId));
        Assert.Equal(
            soulsBefore - price.Where(l => l.Class == MaterialClass.Souls).Sum(l => l.Qty),
            _store.GetSoulBalance(_playerId).Balance);

        // 2. the operation persisted — SECOND READ, from the store rather than the response
        var head = _store.GetInstanceMutationHead(_instanceId)!;
        Assert.Equal(1, head.EnhanceLevel);
        Assert.Equal(1, head.MutationSeq);
        Assert.NotNull(head.StateHash);
        Assert.NotNull(head.OriginValuesJson);   // D2 rung 1', written lazily at the first mutation

        // 3. the op ledger carries the spend beside the result (D2 clause 11)
        var op = Assert.Single(_store.ReadMutationOps(_instanceId));
        Assert.Equal(MutationOpKind.Enhance, op.Kind);
        Assert.Equal("wb-enhance-1", op.CorrelationId);
        Assert.Equal("success", op.Result.Outcome);
        Assert.Contains("\"qty\"", op.CostJson);
        foreach (var line in price)
            Assert.Contains($"\"qty\":{line.Qty}", op.CostJson);

        // 4. the spend log has exactly one row for the whole operation
        Assert.Equal(1, _store.CountMaterialSpendLog(_playerId));
    }

    [Fact]
    public async Task Enhance_retriedOnTheSameCorrelation_spendsNothingASecondTime()
    {
        var price = _recipes.Resolve("recipe.012", ItemContext());
        Fund(price);

        var first = await Post("enhance", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.012",
            correlationId = "wb-enhance-retry",
        });
        Assert.True(first.Status == HttpStatusCode.OK, first.Body.ToString());

        var soulsAfterFirst = _store.GetSoulBalance(_playerId).Balance;
        var materialsAfterFirst = price
            .Where(l => l.Class != MaterialClass.Souls)
            .ToDictionary(l => l.MaterialId, l => Balance(l.MaterialId), StringComparer.Ordinal);

        var second = await Post("enhance", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.012",
            correlationId = "wb-enhance-retry",
        });

        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.True(second.Body.GetProperty("replayed").GetBoolean());
        Assert.Equal(soulsAfterFirst, _store.GetSoulBalance(_playerId).Balance);
        foreach (var (id, qty) in materialsAfterFirst) Assert.Equal(qty, Balance(id));

        // The level moved once, not twice, and the ledger has one row.
        Assert.Equal(1, _store.GetInstanceMutationHead(_instanceId)!.EnhanceLevel);
        Assert.Single(_store.ReadMutationOps(_instanceId));
        Assert.Equal(1, _store.CountMaterialSpendLog(_playerId));
    }

    [Fact]
    public async Task Enhance_withARecipeForAnotherVerb_isRefusedByName()
    {
        Fund(_recipes.Resolve("recipe.012", ItemContext()));

        var (status, body) = await Post("enhance", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.019",  // a `bore`
            correlationId = "wb-enhance-wrong-verb",
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("material.operation-mismatch", body.GetProperty("reason").GetString());
        Assert.Empty(_store.ReadMutationOps(_instanceId));
    }

    [Fact]
    public async Task Enhance_withoutACorrelationId_is400()
    {
        var resp = await _http.PostAsJsonAsync("/api/items/workbench/enhance", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.012",
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ---- module 16 `sockets` — the socket write ----------------------------------------------------

    [Fact]
    public async Task SocketAdd_withoutTheMaterials_isRefusedAndWritesNoSocketRow()
    {
        var (status, body) = await Post("socket-add", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.019",
            correlationId = "wb-bore-short",
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("insufficient", body.GetProperty("reason").GetString());
        Assert.Empty(_store.GetSockets(_instanceId));
        Assert.Empty(_store.ReadMutationOps(_instanceId));
    }

    [Fact]
    public async Task SocketAdd_withTheMaterials_debitsAndPersistsARealSocketRow()
    {
        var price = _recipes.Resolve("recipe.019", ItemContext());
        Fund(price);
        var soulsBefore = _store.GetSoulBalance(_playerId).Balance;
        var materialBefore = price
            .Where(l => l.Class != MaterialClass.Souls)
            .ToDictionary(l => l.MaterialId, l => Balance(l.MaterialId), StringComparer.Ordinal);

        var (status, _) = await Post("socket-add", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.019",
            correlationId = "wb-bore-1",
        });

        Assert.Equal(HttpStatusCode.OK, status);

        foreach (var line in price.Where(l => l.Class != MaterialClass.Souls))
            Assert.Equal(materialBefore[line.MaterialId] - line.Qty, Balance(line.MaterialId));
        Assert.Equal(
            soulsBefore - price.Where(l => l.Class == MaterialClass.Souls).Sum(l => l.Qty),
            _store.GetSoulBalance(_playerId).Balance);

        // SECOND READ — item_socket is the SSOT (D2 §6), so this is the state, not a projection.
        var slots = Assert.Single(_store.GetSockets(_instanceId));
        Assert.Equal(0, slots.Index);
        Assert.True(slots.Crafted);          // D24: only a crafted socket may later be imbued
        Assert.Equal("", slots.Affinity);
        Assert.True(slots.IsEmpty);

        // The op is the audit receipt beside it (D2 clause 13), never the state.
        var op = Assert.Single(_store.ReadMutationOps(_instanceId));
        Assert.Equal(MutationOpKind.SocketAdd, op.Kind);
        Assert.Equal(0, op.Result.EnhanceLevelDelta);
    }

    [Fact]
    public async Task SocketAdd_withNoBaseTypeSocketMax_refusesByNameRatherThanGuessing()
    {
        // The same executor with no base-type lookup at all — module 6's missing table, reproduced.
        var blind = new ItemWorkbench(_store, _materials, _recipes, _enhancement, _sockets);
        Fund(_recipes.Resolve("recipe.019", ItemContext()));

        var outcome = blind.SocketAdd(_playerId, _instanceId, "recipe.019", "wb-bore-blind");

        Assert.False(outcome.Ok);
        Assert.Contains("socket.base-type-socket-max-unavailable", outcome.Reason);
        Assert.Empty(_store.GetSockets(_instanceId));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task SocketInsert_consumesTheInsertFromStockAndFillsTheSocket()
    {
        Fund(_recipes.Resolve("recipe.019", ItemContext()));
        Assert.Equal(HttpStatusCode.OK, (await Post("socket-add", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.019",
            correlationId = "wb-bore-for-insert",
        })).Status);

        const string gem = "gem.workbench-ember.t1";
        _store.AdjustStock(_playerKey, gem, 1);
        var price = _recipes.Resolve("recipe.022", ItemContext());
        Fund(price);
        var soulsBefore = _store.GetSoulBalance(_playerId).Balance;

        var (status, _) = await Post("socket-insert", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.022",
            insertContainerId = gem, correlationId = "wb-insert-1",
        });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(
            soulsBefore - price.Where(l => l.Class == MaterialClass.Souls).Sum(l => l.Qty),
            _store.GetSoulBalance(_playerId).Balance);

        var slot = Assert.Single(_store.GetSockets(_instanceId));
        Assert.Equal(gem, slot.InsertContainerId);
        Assert.False(slot.IsEmpty);

        // The insert left stock in the SAME transaction — otherwise one gem fills every socket.
        Assert.DoesNotContain(_store.ListStock(_playerKey), s => s.ContainerId == gem && s.Qty > 0);
    }

    [Fact]
    public async Task SocketInsert_withoutHoldingTheInsert_isRefused()
    {
        Fund(_recipes.Resolve("recipe.019", ItemContext()));
        await Post("socket-add", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.019",
            correlationId = "wb-bore-for-missing-insert",
        });
        Fund(_recipes.Resolve("recipe.022", ItemContext()));

        var (status, body) = await Post("socket-insert", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.022",
            insertContainerId = "gem.not-held.t1", correlationId = "wb-insert-missing",
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("socket.insert-not-held", body.GetProperty("reason").GetString());
        Assert.True(_store.GetSockets(_instanceId).Single().IsEmpty);
    }

    // ---- module 14 `salvage-craft` — the upcycle and salvage verbs ---------------------------------

    [Fact]
    public async Task Upcycle_withoutTheMaterials_isRefusedAndMintsNothing()
    {
        var recipe = _recipes.Recipes["recipe.005"];
        var output = recipe.OutputRef!;
        var before = Balance(output);

        var (status, body) = await Post("upcycle", new
        {
            playerId = _playerId, recipeId = "recipe.005", correlationId = "wb-upcycle-short",
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("materials.insufficient", body.GetProperty("reason").GetString());
        Assert.Equal(before, Balance(output));
        Assert.Equal(0, _store.CountMaterialSpendLog(_playerId));
    }

    [Fact]
    public async Task Upcycle_withTheMaterials_debitsTheInputAndMintsTheOutput()
    {
        var recipe = _recipes.Recipes["recipe.005"];
        var price = _recipes.Resolve("recipe.005", UpcycleContext(recipe.Frame));
        Fund(price);

        var input = price.First(l => l.Class == MaterialClass.Substrate);
        var inputBefore = Balance(input.MaterialId);
        var outputBefore = Balance(recipe.OutputRef!);

        var (status, body) = await Post("upcycle", new
        {
            playerId = _playerId, recipeId = "recipe.005", correlationId = "wb-upcycle-1",
        });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("upcycled", body.GetProperty("outcome").GetString());

        // SECOND READ: the grinder took the input and produced the output, both persisted.
        Assert.Equal(inputBefore - input.Qty, Balance(input.MaterialId));
        Assert.Equal(outputBefore + recipe.OutputQty, Balance(recipe.OutputRef!));
        Assert.Equal(1, _store.CountMaterialSpendLog(_playerId));

        // ⭐ R2's direction, observed end to end: the upcycle spends strictly more units than it mints.
        Assert.True(input.Qty > recipe.OutputQty,
            $"upcycle minted {recipe.OutputQty} for {input.Qty} — a conversion that is not a loss is a faucet");
    }

    /// <summary>
    /// ⭐ Proves the executor's <c>MaterialHasNoRung</c> constant is not a silent assumption:
    /// <c>upcycle</c> has no rung leg in <c>materials.v1.json</c>, so the rung index it is handed
    /// cannot change the price. If a balance pass ever adds one, this goes red on the day it happens
    /// rather than the day a player notices.
    /// </summary>
    [Fact]
    public void Upcycle_cost_is_invariant_across_every_rung_index()
    {
        foreach (var recipeId in _recipes.Recipes.Values
                     .Where(r => r.Operation == CraftOperation.Upcycle)
                     .Select(r => r.RecipeId))
        {
            var frame = _recipes.Recipes[recipeId].Frame;
            var baseline = _recipes.Resolve(recipeId, UpcycleContext(frame));
            for (var rung = 0; rung < RarityLadder.RungIds.Count; rung++)
            {
                var priced = _recipes.Resolve(recipeId, UpcycleContext(frame) with { TargetRungIndex = rung });
                Assert.Equal(baseline, priced);
            }
        }
    }

    static RecipeContext UpcycleContext(string frame) => new(0, IlvlTierLadder.MinTier, 0, frame, 0);

    [Fact]
    public async Task Salvage_returnsMaterialsAndRetiresTheItem()
    {
        var expected = SalvagePolicy.Yield(
            new SalvageInput(RungIndex(), ItemLevel, Frame, 1,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["fire"] = 1 }, 0),
            _materials);
        Assert.NotEmpty(expected);

        var before = expected.ToDictionary(l => l.MaterialId, l => Balance(l.MaterialId), StringComparer.Ordinal);

        var (status, body) = await Post("salvage", new { playerId = _playerId, instanceId = _instanceId });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("salvaged", body.GetProperty("outcome").GetString());

        // SECOND READ: the yield landed and the item is gone from the armoury as an owned thing.
        foreach (var line in expected)
            Assert.Equal(before[line.MaterialId] + line.Qty, Balance(line.MaterialId));
        Assert.Equal("salvaged", _store.GetItem(_instanceId)!.Disposition);
        Assert.Contains(_store.ListItemEvents(_instanceId), e => e.Kind == "salvaged");

        // Salvage never mints currency.
        Assert.Equal(0, _store.GetSoulBalance(_playerId).Balance);
    }

    [Fact]
    public async Task Salvage_twice_isRefusedAndGrantsNothingTheSecondTime()
    {
        Assert.Equal(HttpStatusCode.OK,
            (await Post("salvage", new { playerId = _playerId, instanceId = _instanceId })).Status);

        var after = _store.GetSockets(_instanceId);   // untouched either way
        var substrate = MaterialCatalog.SubstrateId(Frame, _materials.GradeForItemLevel(ItemLevel));
        var before = Balance(substrate);

        var (status, body) = await Post("salvage", new { playerId = _playerId, instanceId = _instanceId });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("item.not-owned: '" + _instanceId + "' is 'salvaged'", body.GetProperty("reason").GetString());
        Assert.Equal(before, Balance(substrate));
        Assert.Equal(after.Count, _store.GetSockets(_instanceId).Count);
    }

    [Fact]
    public async Task Salvage_aLockedItem_isRefused()
    {
        var item = _store.GetItem(_instanceId)!;
        _store.SaveItem(item with { Locked = true });
        var substrate = MaterialCatalog.SubstrateId(Frame, _materials.GradeForItemLevel(ItemLevel));
        var before = Balance(substrate);

        var (status, body) = await Post("salvage", new { playerId = _playerId, instanceId = _instanceId });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("item.locked", body.GetProperty("reason").GetString());
        Assert.Equal(before, Balance(substrate));
        Assert.Equal("owned", _store.GetItem(_instanceId)!.Disposition);
    }

    // ---- the gate every verb shares ----------------------------------------------------------------

    [Fact]
    public async Task AnItemAnotherPlayerOwns_isRefusedForEveryVerb()
    {
        var other = _store.CreatePlayer("other").Id;
        Fund(_recipes.Resolve("recipe.012", ItemContext()));

        var (enhanceStatus, enhanceBody) = await Post("enhance", new
        {
            playerId = other, instanceId = _instanceId, recipeId = "recipe.012",
            correlationId = "wb-not-mine",
        });
        Assert.Equal(HttpStatusCode.Conflict, enhanceStatus);
        Assert.Contains("item.not-owned", enhanceBody.GetProperty("reason").GetString());

        var (salvageStatus, salvageBody) = await Post("salvage", new { playerId = other, instanceId = _instanceId });
        Assert.Equal(HttpStatusCode.Conflict, salvageStatus);
        Assert.Contains("item.not-owned", salvageBody.GetProperty("reason").GetString());

        Assert.Equal("owned", _store.GetItem(_instanceId)!.Disposition);
        Assert.Empty(_store.ReadMutationOps(_instanceId));
    }

    [Fact]
    public async Task AnUnknownInstance_isRefusedRatherThanCrashing()
    {
        var (status, body) = await Post("salvage", new { playerId = _playerId, instanceId = "no-such-instance" });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("item.unknown", body.GetProperty("reason").GetString());
    }

    /// <summary>
    /// ⛔ <b>Checkpoint 4's own criterion, on one item:</b> craft (a socket bored) → enhance → socket
    /// (an insert set) → salvage, in that order, each through the real endpoint, each debiting real
    /// balances, and every step's state read back from the store afterwards.
    /// </summary>
    [Fact]
    public async Task TheWholeLoopRunsOnOneItem_craftEnhanceSocketSalvage()
    {
        // 1 — craft: bore a socket.
        Fund(_recipes.Resolve("recipe.019", ItemContext()));
        Assert.Equal(HttpStatusCode.OK, (await Post("socket-add", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.019", correlationId = "loop-bore",
        })).Status);

        // 2 — enhance: +0 → +1.
        Fund(_recipes.Resolve("recipe.012", ItemContext()));
        Assert.Equal(HttpStatusCode.OK, (await Post("enhance", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.012", correlationId = "loop-enhance",
        })).Status);

        // 3 — socket: set an insert the player holds.
        const string gem = "gem.workbench-loop.t1";
        _store.AdjustStock(_playerKey, gem, 1);
        Fund(_recipes.Resolve("recipe.022", ItemContext(enhanceLevel: 1)));
        Assert.Equal(HttpStatusCode.OK, (await Post("socket-insert", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.022", insertContainerId = gem,
            correlationId = "loop-insert",
        })).Status);

        // The state after three operations, read back from the store.
        Assert.Equal(1, _store.GetInstanceMutationHead(_instanceId)!.EnhanceLevel);
        Assert.Equal(gem, _store.GetSockets(_instanceId).Single().InsertContainerId);
        Assert.Equal(3, _store.ReadMutationOps(_instanceId).Count);
        Assert.Equal(new[] { 1, 2, 3 }, _store.ReadMutationOps(_instanceId).Select(o => o.Seq).ToArray());
        Assert.Equal(3, _store.CountMaterialSpendLog(_playerId));

        // 4 — salvage: the same item back into materials, priced off the state the loop produced.
        var yieldBefore = SalvagePolicy.Yield(
            new SalvageInput(RungIndex(), ItemLevel, Frame, 1,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["fire"] = 1 }, EnhanceLevel: 1),
            _materials);
        var before = yieldBefore.ToDictionary(l => l.MaterialId, l => Balance(l.MaterialId), StringComparer.Ordinal);

        Assert.Equal(HttpStatusCode.OK,
            (await Post("salvage", new { playerId = _playerId, instanceId = _instanceId })).Status);

        foreach (var line in yieldBefore)
            Assert.Equal(before[line.MaterialId] + line.Qty, Balance(line.MaterialId));
        Assert.Equal("salvaged", _store.GetItem(_instanceId)!.Disposition);
    }

    // ---- item-content `item-naming` T4 — the two READ routes a picker needs -------------------------

    /// <summary>
    /// ⛔ <b>Until 2026-09-06 NO route served the recipe corpus.</b> Thirty rows shipped in
    /// <c>material_recipe</c> and both benches asked the player to TYPE <c>recipe.014</c> —
    /// <c>GET /api/recipes</c> is the PvZ fusion table and a different thing entirely.
    ///
    /// <para>Two claims, and the second is the one that makes the picker safe: every row carries the
    /// corpus's own AUTHORED name (which <c>MaterialRecipeCatalog.Load</c> also dropped until today),
    /// and the list is <see cref="ItemWorkbench.Recipes"/> itself — so a row a picker offers can never
    /// be one the very next POST refuses with <c>material.recipe-unknown</c>.</para>
    /// </summary>
    [Fact]
    public async Task Recipes_areServedWithTheirAuthoredNamesAndAreExactlyWhatTheBenchPricesAgainst()
    {
        var resp = await _http.GetAsync("/api/items/workbench/recipes");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var rows = doc.RootElement.EnumerateArray().ToList();

        // The route's list IS the executor's corpus — not a copy, not a subset.
        Assert.Equal(_recipes.Recipes.Count, rows.Count);
        Assert.Equal(
            _recipes.Recipes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray(),
            rows.Select(r => r.GetProperty("recipeId").GetString()!).OrderBy(k => k, StringComparer.Ordinal).ToArray());

        // Every shipped row authors a name, and no name is its own id wearing a different hat.
        Assert.All(rows, r =>
        {
            var name = r.GetProperty("name").GetString()!;
            Assert.NotEqual("", name);
            Assert.DoesNotContain("recipe.", name, StringComparison.Ordinal);
        });

        var temper = rows.Single(r => r.GetProperty("recipeId").GetString() == "recipe.014");
        Assert.Equal("Temper: Ultimate Enhancement", temper.GetProperty("name").GetString());
        Assert.Equal("temper", temper.GetProperty("operation").GetString());
    }

    /// <summary>The verb filter, so a temper picker never offers a bore recipe the executor would
    /// refuse for the control the player is actually looking at.</summary>
    [Fact]
    public async Task Recipes_narrowToOneVerbSoAPickerCannotOfferTheWrongOne()
    {
        var resp = await _http.GetAsync("/api/items/workbench/recipes?operation=bore");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var rows = doc.RootElement.EnumerateArray().ToList();

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal("bore", r.GetProperty("operation").GetString()));
        Assert.Equal(
            _recipes.Recipes.Values.Count(r => CraftOperations.Id(r.Operation) == "bore"),
            rows.Count);
    }

    /// <summary>
    /// The other half of T4: the socket bench's insert field was a free-text box asking for a
    /// container id, so a player had to know <c>gem.workbench-ember.t1</c> existed before they could
    /// socket it. This serves what they actually hold, named through the gem corpus.
    /// </summary>
    [Fact]
    public async Task HeldInserts_areServedWithTheirAuthoredNamesAndOnlyWhatThePlayerHolds()
    {
        var empty = await _http.GetAsync($"/api/items/workbench/inserts/{_playerKey}");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        using (var none = JsonDocument.Parse(await empty.Content.ReadAsStringAsync()))
            Assert.Empty(none.RootElement.EnumerateArray());

        _store.AdjustStock(_playerKey, "gem.workbench-ember.t1", 3);
        // Not an insert: a material in the same stock table must not reach an insert picker.
        _store.AdjustStock(_playerKey, "substrate.humanoid.crude", 9);

        var resp = await _http.GetAsync($"/api/items/workbench/inserts/{_playerKey}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var row = Assert.Single(doc.RootElement.EnumerateArray().ToList());

        Assert.Equal("gem.workbench-ember.t1", row.GetProperty("containerId").GetString());
        Assert.Equal("Ember Shard", row.GetProperty("name").GetString());
        Assert.Equal("fire", row.GetProperty("element").GetString());
        Assert.Equal(3, row.GetProperty("qty").GetInt64());
    }

    /// <summary>
    /// The socket cell in an operation's own reply carries the insert's authored name too, so the
    /// bench never prints <c>gem.workbench-ember.t1</c> where a name belongs. Driven through the real
    /// <c>socket-insert</c> verb rather than asserted on a hand-built DTO.
    /// </summary>
    [Fact]
    public async Task SocketInsert_replyNamesTheInsertItSet()
    {
        Fund(_recipes.Resolve("recipe.019", ItemContext()));
        Assert.Equal(HttpStatusCode.OK, (await Post("socket-add", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.019",
            correlationId = "wb-bore-for-name",
        })).Status);

        const string gem = "gem.workbench-ember.t1";
        _store.AdjustStock(_playerKey, gem, 1);
        Fund(_recipes.Resolve("recipe.022", ItemContext()));

        var (status, reply) = await Post("socket-insert", new
        {
            playerId = _playerId, instanceId = _instanceId, recipeId = "recipe.022",
            insertContainerId = gem, correlationId = "wb-insert-named",
        });
        Assert.Equal(HttpStatusCode.OK, status);

        var filled = reply.GetProperty("sockets").EnumerateArray()
            .Single(sck => sck.GetProperty("insert").GetString() == gem);
        Assert.Equal("Ember Shard", filled.GetProperty("insertName").GetString());
    }
}
