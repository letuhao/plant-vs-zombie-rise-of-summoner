using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Drops;
using FusionRpg.Core.Items.Grants;
using FusionRpg.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>
/// ⭐ item module 4 (<c>equip-assign</c>) — <b>the equip executor</b>, against a real in-process host,
/// a real store, a real bound specimen and a real stored item.
///
/// <para>Module 4 shipped its table, its two writes, its gate and its projector green in September
/// and recorded the same blocker the workbench modules did: <c>SaveAssignment</c> and
/// <c>RemoveAssignment</c> had <b>zero callers outside <c>tests/</c></b>. These tests drive both
/// through the HTTP surface and then <b>read the state back from the store</b>, which is the only
/// thing that separates "the route returned 200" from "the player is wearing it".</para>
///
/// <para>⛔ Every refusal case asserts <b>two</b> things: the named rule on the wire, and that the
/// table is unchanged. A gate that answers 409 and writes anyway is the failure mode a status-code
/// assertion alone cannot see.</para>
/// </summary>
public class ItemEquipEndpointsTests : IAsyncLifetime
{
    const string Rung = "cultivated";
    const string BaseTypeId = "item.equip-base-a-001";
    const string BladeContainer = "item.equip-blade";
    const string HelmContainer = "item.equip-helm";
    const string LordlyContainer = "item.equip-lordly-blade";
    const string Frame = "humanoid";
    const int ItemLevel = 24;

    /// <summary>Above the level a freshly bound specimen starts at (<c>rpg_unique_actors.level</c>
    /// defaults to 1), so the level arm refuses for a real reason rather than a contrived one.</summary>
    const int OutOfReachLevelReq = 50;

    string _dir = "";
    RpgStore _store = null!;
    WebApplication _app = null!;
    HttpClient _http = null!;
    long _playerId;
    string _playerKey = "";
    string _specimenId = "";
    string _bladeId = "";
    string _helmId = "";
    string _lordlyId = "";

    static readonly string ArmamentPrimary = ItemRoles.Id(ItemRole.ArmamentPrimary);
    static readonly string HeadGuard = ItemRoles.Id(ItemRole.HeadGuard);

    // ---- fixture -----------------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-equip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        _playerId = _store.GetCurrentPlayerId();
        _playerKey = _playerId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        for (var i = 0; i < RarityLadder.RungIds.Count; i++)
            Assert.True(_store.UpsertRarity(new RarityRow(RarityLadder.RungIds[i], (i + 1) * 10, 3, 0, 1, 5)).Ok);

        _specimenId = _store.CreateUniqueActor(_playerId, "plant", 1).InstanceId;

        _bladeId = SeedItem(BladeContainer, ItemRole.ArmamentPrimary);
        _helmId = SeedItem(HelmContainer, ItemRole.HeadGuard);
        _lordlyId = SeedItem(LordlyContainer, ItemRole.ArmamentPrimary, levelReq: OutOfReachLevelReq);

        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(_store);
        // The OTHER flow that writes `rpg_item_assignment`, mapped here on purpose: defect R1 was
        // that these two routes were asymmetric, and an asymmetry between two routes can only be
        // proven with both of them reachable from one host.
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<InjectorCommandInbox>();
        builder.Services.AddSingleton<UniqueActorService>();
        builder.WebHost.UseUrls(baseUrl);
        _app = builder.Build();
        _app.UseDeveloperExceptionPage();
        _app.MapItemEquip(new ItemEquipService(_store));
        _app.MapUniqueActors();
        // Module 20's read-only armoury, on its REAL shipped tuning files (defect R4's surface).
        var tuningDir = Path.Combine(RepoRoot(), "data", "tuning");
        _app.MapItemSurfaces(
            FusionRpg.Core.Items.Surfaces.ItemSurfaceTuning.Parse(
                File.ReadAllText(Path.Combine(tuningDir, "item-surfaces.v1.json"))),
            FusionRpg.Core.Items.Sockets.SocketTuning.Parse(
                File.ReadAllText(Path.Combine(tuningDir, "sockets.v1.json"))),
            lookupInsert: null,
            // item-content `item-naming` T3: the SAME base-type delegate the card route reads, handed
            // in exactly as `Program.cs` hands it in. These three test containers are not in the
            // shipped 740-entry corpus, so a real authored name is supplied for one of them here and
            // the other two exercise the honest "" the route sends for an unknown container.
            lookupBaseType: FusionRpg.Server.ItemBaseTypeCorpus.From(
                new Dictionary<string, FusionRpg.Core.Items.Display.CardBaseType>(StringComparer.Ordinal)
                {
                    [BladeContainer] = new FusionRpg.Core.Items.Display.CardBaseType(
                        "base.equip-blade", "class.blade", "humanoid", "role.armament-primary", null,
                        "Honed Hatchet"),
                }));
        await _app.StartAsync();

        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
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

    static int GetFreeTcpPort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    /// <summary>A real container, a real frozen instance, a real ownership row and a real generation
    /// stamp — the four things the equip gate reads off "a stored item".</summary>
    string SeedItem(string containerId, ItemRole role, int? levelReq = null, string? playerKey = null)
    {
        var atomId = AtomRow.DeriveId("atom.equip-vitality", "", 1);
        Assert.True(_store.UpsertAtom(new AtomRow
        {
            AtomId = atomId,
            KindId = "stat.modify",
            FamilyId = "atom.equip-vitality",
            Variant = "",
            Tier = 1,
            Name = "equip vitality",
            ParamsJson = """{"channel":"maxHp","op":"flat","amount":10}""",
        }).IsOk);

        var upsert = _store.UpsertContainer(new ContainerRow
        {
            ContainerId = containerId,
            Kind = ContainerKind.Item,
            Slot = ItemRoles.Id(role),
            Rarity = Rung,
            LevelReq = levelReq,
            Atoms = new[] { new ContainerAtomRow(0, atomId) },
        });
        Assert.True(upsert.IsOk, upsert.ToString());

        var instanceId = _store.SaveInstance(new InstanceRow
        {
            ContainerId = containerId,
            RollSeed = 4242,
            CatalogRevision = _store.GetCatalogRevision(),
            Origin = InstanceOrigin.Drop,
            Atoms = new[] { new InstanceAtomRow(0, atomId, """{"amount":10}""") },
        });

        var owner = playerKey ?? _playerKey;
        Assert.True(_store.AcquireItem(new RpgItemRow
        {
            InstanceId = instanceId,
            PlayerId = owner,
            AcquiredUtc = "2026-09-06T00:00:00Z",
            OriginKind = "drop",
        }).IsOk);

        _store.PersistLoot(
            owner,
            new LootManifest("eq-drop", "table.equip", 7UL, ItemLevel, Array.Empty<LootGrant>(),
                Array.Empty<string>(), "{}", LootPityState.Empty, LootPityState.Empty, null, false, null),
            "test", "equip", _store.GetCatalogRevision(), 1,
            new[]
            {
                new ItemGenerationRow(instanceId, 0, BaseTypeId, RungOrdinal(), ItemLevel, Frame,
                    ItemRoles.Id(role), "drop"),
            });

        return instanceId;
    }

    int RungOrdinal() => _store.ListRarities().First(r => r.RarityId == Rung).Ordinal;

    async Task<(HttpStatusCode Status, JsonElement Body)> Post(string verb, object body)
    {
        var resp = await _http.PostAsJsonAsync($"/api/items/{verb}", body);
        var text = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return (resp.StatusCode, doc.RootElement.Clone());
    }

    /// <summary>An INDEPENDENT read of what the specimen wears — over HTTP, not off the write's own
    /// reply. A response echoing back what it was handed proves nothing about what was stored.</summary>
    async Task<JsonElement> ReadAssignments()
    {
        var resp = await _http.GetAsync($"/api/items/assignments/{_specimenId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    // ---- equip: the happy path, proven from the store ---------------------------------------------

    /// <summary>
    /// ⭐ The whole point of the module: an item goes into a role over HTTP, and the row is there
    /// afterwards. Read back twice, both times from somewhere other than the reply — once through the
    /// store directly (which is what <c>SaveAssignment</c> actually wrote) and once through a second
    /// HTTP call on its own connection.
    /// </summary>
    [Fact]
    public async Task Equip_writesTheAssignment_andTwoIndependentReadsSeeIt()
    {
        Assert.Empty(_store.ListAssignments(_specimenId));

        var (status, body) = await Post("equip", new
        {
            playerId = _playerId, specimenId = _specimenId, instanceId = _bladeId, role = ArmamentPrimary,
        });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal("equip", body.GetProperty("verb").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("replaced").ValueKind);

        // 1 — SECOND READ, straight off the store. `rpg_item_assignment` is the SSOT.
        var row = Assert.Single(_store.ListAssignments(_specimenId));
        Assert.Equal(ItemRole.ArmamentPrimary, row.Role);
        Assert.Equal(ItemEquipService.RolledRefKind, row.RefKind);
        Assert.Equal(_bladeId, row.RefId);
        Assert.False(string.IsNullOrWhiteSpace(row.AssignedUtc));

        // 2 — THIRD READ, over a separate request.
        var listed = Assert.Single((await ReadAssignments()).EnumerateArray().ToList());
        Assert.Equal(ArmamentPrimary, listed.GetProperty("role").GetString());
        Assert.Equal(_bladeId, listed.GetProperty("refId").GetString());
    }

    /// <summary>
    /// ⭐ <c>ref_kind</c> is <c>"rolled"</c>, and that is load-bearing rather than cosmetic:
    /// <c>RpgStore.ApplyEquipProjection</c> only turns a <c>"rolled"</c> assignment into a runtime
    /// binding, so an equip that stored any other kind would persist a decision module 5 could never
    /// project. Pinned here so a future rename has to come through this test.
    /// </summary>
    [Fact]
    public async Task Equip_storesTheRolledRefKind_theOnlyKindTheProjectorBinds()
    {
        await Post("equip", new { playerId = _playerId, specimenId = _specimenId, instanceId = _bladeId, role = ArmamentPrimary });

        Assert.Equal("rolled", Assert.Single(_store.ListAssignments(_specimenId)).RefKind);
    }

    /// <summary>Retrying an equip is safe without a correlation id, which is why the route asks for
    /// none: the upsert lands the same single row.</summary>
    [Fact]
    public async Task Equip_repeated_isIdempotentAndStillOneRow()
    {
        var first = await Post("equip", new { playerId = _playerId, specimenId = _specimenId, instanceId = _bladeId, role = ArmamentPrimary });
        Assert.Equal(HttpStatusCode.OK, first.Status);

        var second = await Post("equip", new { playerId = _playerId, specimenId = _specimenId, instanceId = _bladeId, role = ArmamentPrimary });
        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal("equip.already-in-this-role", second.Body.GetProperty("reason").GetString());

        Assert.Single(_store.ListAssignments(_specimenId));
    }

    /// <summary>Two items for one role is a swap. It is allowed, and the reply names the piece that
    /// came off so the surface never has to work it out.</summary>
    [Fact]
    public async Task Equip_overAWornPiece_swapsAndNamesWhatItDisplaced()
    {
        var second = SeedItem("item.equip-blade-b", ItemRole.ArmamentPrimary);
        await Post("equip", new { playerId = _playerId, specimenId = _specimenId, instanceId = _bladeId, role = ArmamentPrimary });

        var (status, body) = await Post("equip", new
        {
            playerId = _playerId, specimenId = _specimenId, instanceId = second, role = ArmamentPrimary,
        });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(_bladeId, body.GetProperty("replaced").GetProperty("refId").GetString());

        var row = Assert.Single(_store.ListAssignments(_specimenId));
        Assert.Equal(second, row.RefId);

        // The displaced item is still owned — coming off a role never destroys a piece (module 1's R1).
        Assert.Equal("owned", _store.GetItem(_bladeId)!.Disposition);
    }

    // ---- unequip -----------------------------------------------------------------------------------

    /// <summary>⭐ <c>RemoveAssignment</c>'s first production caller: one row deleted, no second
    /// writer (§6.4's atomicity claim), and the item survives it.</summary>
    [Fact]
    public async Task Unequip_deletesTheRow_andLeavesTheItemOwned()
    {
        await Post("equip", new { playerId = _playerId, specimenId = _specimenId, instanceId = _bladeId, role = ArmamentPrimary });
        Assert.Single(_store.ListAssignments(_specimenId));

        var (status, body) = await Post("unequip", new
        {
            playerId = _playerId, specimenId = _specimenId, role = ArmamentPrimary,
        });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(_bladeId, body.GetProperty("replaced").GetProperty("refId").GetString());

        // SECOND READ: gone from the table, still in the armoury.
        Assert.Empty(_store.ListAssignments(_specimenId));
        Assert.Empty((await ReadAssignments()).EnumerateArray());
        Assert.Equal("owned", _store.GetItem(_bladeId)!.Disposition);
    }

    [Fact]
    public async Task Unequip_anEmptyRole_isRefusedByName()
    {
        var (status, body) = await Post("unequip", new
        {
            playerId = _playerId, specimenId = _specimenId, role = ArmamentPrimary,
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("equip.role-empty", body.GetProperty("reason").GetString());
    }

    // ---- refusals: each one asserts the table is untouched -----------------------------------------

    /// <summary>⭐ The gate refuses a piece aimed at the wrong role, the same way the relic flow's own
    /// <c>SlotMatchesItem</c> does for its three legacy slots.</summary>
    [Fact]
    public async Task Equip_intoTheWrongRole_isRefusedAndWritesNothing()
    {
        var (status, body) = await Post("equip", new
        {
            playerId = _playerId, specimenId = _specimenId, instanceId = _helmId, role = ArmamentPrimary,
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("equip.role-mismatch", body.GetProperty("reason").GetString());
        Assert.Contains(HeadGuard, body.GetProperty("reason").GetString());
        Assert.Empty(_store.ListAssignments(_specimenId));
    }

    /// <summary>⭐ A4, end to end rather than as a branch: <c>level_req</c> is compared against the
    /// SPECIMEN's level (`spec-equip-assign.md`'s recommendation) and actually refuses.</summary>
    [Fact]
    public async Task Equip_whenLevelReqIsAboveTheSpecimen_isRefusedAndWritesNothing()
    {
        Assert.Equal(1, _store.GetUniqueActor(_specimenId)!.Level);

        var (status, body) = await Post("equip", new
        {
            playerId = _playerId, specimenId = _specimenId, instanceId = _lordlyId, role = ArmamentPrimary,
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("equip.level-too-low", body.GetProperty("reason").GetString());
        Assert.Contains($"needs level {OutOfReachLevelReq}", body.GetProperty("reason").GetString());
        Assert.Empty(_store.ListAssignments(_specimenId));
    }

    [Fact]
    public async Task Equip_anItemAnotherPlayerOwns_isRefusedAndWritesNothing()
    {
        var other = _store.CreatePlayer("other").Id;
        var theirs = SeedItem("item.equip-theirs", ItemRole.ArmamentPrimary,
            playerKey: other.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var (status, body) = await Post("equip", new
        {
            playerId = _playerId, specimenId = _specimenId, instanceId = theirs, role = ArmamentPrimary,
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("item.not-owned", body.GetProperty("reason").GetString());
        Assert.Empty(_store.ListAssignments(_specimenId));
    }

    [Fact]
    public async Task Equip_toASpecimenAnotherPlayerOwns_isRefused()
    {
        var other = _store.CreatePlayer("other-2").Id;
        var theirSpecimen = _store.CreateUniqueActor(other, "plant", 2).InstanceId;

        var (status, body) = await Post("equip", new
        {
            playerId = _playerId, specimenId = theirSpecimen, instanceId = _bladeId, role = ArmamentPrimary,
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("equip.specimen-not-owned", body.GetProperty("reason").GetString());
        Assert.Empty(_store.ListAssignments(theirSpecimen));
    }

    [Fact]
    public async Task Equip_toAnUnknownSpecimen_isRefusedRatherThanCrashing()
    {
        var (status, body) = await Post("equip", new
        {
            playerId = _playerId, specimenId = "no-such-specimen", instanceId = _bladeId, role = ArmamentPrimary,
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("equip.specimen-unknown", body.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Equip_aSalvagedItem_isRefused()
    {
        var item = _store.GetItem(_bladeId)!;
        _store.SaveItem(item with { Disposition = "salvaged" });

        var (status, body) = await Post("equip", new
        {
            playerId = _playerId, specimenId = _specimenId, instanceId = _bladeId, role = ArmamentPrimary,
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("is 'salvaged'", body.GetProperty("reason").GetString());
        Assert.Empty(_store.ListAssignments(_specimenId));
    }

    /// <summary>One physical copy cannot be worn twice. The primary key stops two items sharing a
    /// role; this stops one item filling the same role on two specimens.</summary>
    [Fact]
    public async Task Equip_theSameCopyOnASecondSpecimen_isRefused()
    {
        var secondSpecimen = _store.CreateUniqueActor(_playerId, "plant", 3).InstanceId;
        Assert.Equal(HttpStatusCode.OK, (await Post("equip", new
        {
            playerId = _playerId, specimenId = _specimenId, instanceId = _bladeId, role = ArmamentPrimary,
        })).Status);

        var (status, body) = await Post("equip", new
        {
            playerId = _playerId, specimenId = secondSpecimen, instanceId = _bladeId, role = ArmamentPrimary,
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("equip.already-worn", body.GetProperty("reason").GetString());
        Assert.Empty(_store.ListAssignments(secondSpecimen));
        Assert.Single(_store.ListAssignments(_specimenId));
    }

    /// <summary>
    /// ⛔ <b>Two flows, one table, no shared writes.</b> Since D1 §10 M1 the four relics live in
    /// <c>rpg_item_assignment</c> as <c>ref_kind='stock'</c>, written by
    /// <c>PUT /api/unique/actors/{id}/equipment/{slot}</c> — which also rebuilds <c>mods_json</c> and
    /// the <c>unique-equip</c> atom bindings in the same call. Clobbering that cell from here would
    /// delete the row and leave both derived states standing, so it is refused by name and the
    /// player is pointed at the flow that owns it.
    /// </summary>
    [Fact]
    public async Task Equip_intoARoleARelicHolds_isRefusedRatherThanClobberingTheRelicFlow()
    {
        _store.SaveAssignment(_specimenId, ItemRole.ArmamentPrimary, "stock", "relic.ashen_reliquary");

        var (status, body) = await Post("equip", new
        {
            playerId = _playerId, specimenId = _specimenId, instanceId = _bladeId, role = ArmamentPrimary,
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("equip.role-held-by-relic", body.GetProperty("reason").GetString());

        var row = Assert.Single(_store.ListAssignments(_specimenId));
        Assert.Equal("stock", row.RefKind);
        Assert.Equal("relic.ashen_reliquary", row.RefId);
    }

    /// <summary>
    /// ⭐ <b>The missing half, measured as defect R1 and closed 2026-09-06.</b> The test above proves
    /// this route refuses a relic's role. Until today the relic route refused <i>nothing</i>: with a
    /// real blade in <c>armament-primary</c>, <c>PUT /api/unique/actors/{id}/equipment/weapon</c>
    /// answered <b>200</b> and its upsert replaced the row, so the player's item came off with no
    /// refusal and no notice.
    ///
    /// <para>Driven end to end through <b>both real routes</b> on one host — the item goes in through
    /// <c>POST /api/items/equip</c>, not through a seeded row — because the defect was an asymmetry
    /// between the two, and only both of them together can show it is gone.</para>
    /// </summary>
    [Fact]
    public async Task Equip_thenTheRelicRouteOverTheSameRole_isRefusedAndTheItemSurvives()
    {
        var (equipStatus, _) = await Post("equip", new
        {
            playerId = _playerId, specimenId = _specimenId, instanceId = _bladeId, role = ArmamentPrimary,
        });
        Assert.Equal(HttpStatusCode.OK, equipStatus);

        var resp = await _http.PutAsJsonAsync(
            $"/api/unique/actors/{_specimenId}/equipment/weapon", new { itemId = "relic.ashen_reliquary" });

        // 409, not 200 — a well-formed request the rules say no to, the same shape and the same
        // status this route's mirror refusal already answered with.
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("slot.claimed_by_item", doc.RootElement.GetProperty("reason").GetString());

        // And the item's own row is untouched — the assertion a status-code check cannot make.
        var row = Assert.Single(_store.ListAssignments(_specimenId));
        Assert.Equal(ItemRole.ArmamentPrimary, row.Role);
        Assert.Equal(ItemEquipService.RolledRefKind, row.RefKind);
        Assert.Equal(_bladeId, row.RefId);
    }

    /// <summary>The <c>DELETE</c> half of R1, which was the worse one: clearing the legacy slot runs
    /// an unqualified delete on <c>(specimen_id, role)</c> and would have unequipped an item the relic
    /// flow never put there.</summary>
    [Fact]
    public async Task Equip_thenTheRelicRoutesDelete_isRefusedAndTheItemSurvives()
    {
        await Post("equip", new
        {
            playerId = _playerId, specimenId = _specimenId, instanceId = _bladeId, role = ArmamentPrimary,
        });

        var resp = await _http.DeleteAsync($"/api/unique/actors/{_specimenId}/equipment/weapon");

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("slot.claimed_by_item", doc.RootElement.GetProperty("reason").GetString());
        Assert.Equal(_bladeId, Assert.Single(_store.ListAssignments(_specimenId)).RefId);
    }

    /// <summary>⚠ The boundary: only a <c>rolled</c> occupant is refused. A relic replacing a relic is
    /// the relic wire's own job and must stay a 200 — a guard that protected the item flow by breaking
    /// the relic flow would be a worse defect than R1.</summary>
    [Fact]
    public async Task TheRelicRoute_stillReplacesItsOwnStockRow()
    {
        _store.SaveAssignment(_specimenId, ItemRole.ArmamentPrimary, "stock", "relic.ashen_reliquary");

        var resp = await _http.PutAsJsonAsync(
            $"/api/unique/actors/{_specimenId}/equipment/weapon", new { itemId = "relic.sunworn_charm" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var row = Assert.Single(_store.ListAssignments(_specimenId));
        Assert.Equal("stock", row.RefKind);
        Assert.Equal("relic.sunworn_charm", row.RefId);
    }

    // ---- R4: the armoury's `assigned` flag --------------------------------------------------------

    /// <summary>
    /// ⛔ <b>Defect R4, fixed 2026-09-06.</b> <c>ArmouryRowDto.Assigned</c> was hard-coded
    /// <c>false</c> — harmless while nothing could be assigned, and wrong from the day this equip
    /// route shipped. The web armoury's <c>hideAssigned</c> filter (<c>ArmouryList.tsx:60</c>) and the
    /// <c>assigned</c> sort key were both already built and reading it, so both were inert.
    ///
    /// <para>Driven through the real equip route, then read back off the real armoury route: one item
    /// equipped, two not, and the flag has to separate them.</para>
    /// </summary>
    [Fact]
    public async Task Armoury_reportsAssignedForAnEquippedItemAndNotForTheRest()
    {
        var before = await ArmouryRows();
        Assert.Equal(3, before.Count);
        Assert.All(before.Values, assigned => Assert.False(assigned));

        var (status, _) = await Post("equip", new
        {
            playerId = _playerId, specimenId = _specimenId, instanceId = _bladeId, role = ArmamentPrimary,
        });
        Assert.Equal(HttpStatusCode.OK, status);

        var after = await ArmouryRows();
        Assert.True(after[_bladeId]);
        Assert.False(after[_helmId]);
        Assert.False(after[_lordlyId]);
    }

    /// <summary>Unequipping puts the flag back. The item is still owned (module 1's R1), so it must
    /// reappear as un-assigned rather than vanish from the armoury.</summary>
    [Fact]
    public async Task Armoury_dropsAssignedAgainWhenTheItemComesOff()
    {
        await Post("equip", new
        {
            playerId = _playerId, specimenId = _specimenId, instanceId = _bladeId, role = ArmamentPrimary,
        });
        Assert.True((await ArmouryRows())[_bladeId]);

        await Post("unequip", new { playerId = _playerId, specimenId = _specimenId, role = ArmamentPrimary });

        var after = await ArmouryRows();
        Assert.Equal(3, after.Count);
        Assert.False(after[_bladeId]);
    }

    /// <summary>⚠ A <c>stock</c> row is the relic wire's catalog id and never pins one of the
    /// player's instances, so it must not light up an armoury row. Pinned because the naive join —
    /// "any assignment row whose ref_id matches" — would, the day a relic id ever collided with an
    /// instance id.</summary>
    [Fact]
    public async Task Armoury_ignoresAStockAssignmentWhenDecidingAssigned()
    {
        _store.SaveAssignment(_specimenId, ItemRole.ArmamentPrimary, "stock", _bladeId);

        Assert.False((await ArmouryRows())[_bladeId]);
    }

    /// <summary>
    /// ⭐ item-content <c>granted-action-text</c> (T15): <c>ssot-presentation.md</c> §9.14's own ask —
    /// <i>"the battle-only tag needs to be visible in the compact list line too, not only the card — a
    /// player scanning an armoury should not have to open each item to learn that half of them are
    /// inert on the lawn."</i>
    ///
    /// <para>It was NOT true before this task: <c>ArmouryRowDto</c> carried no such field, so the only
    /// way to learn an item's action was battle-only was to open its card.</para>
    /// </summary>
    [Fact]
    public async Task Armoury_carriesTheBattleOnlyTagOnTheCompactLine()
    {
        // Nothing grants anything yet, so no row claims to be battle-only.
        Assert.All((await ArmouryBattleOnly()).Values, Assert.False);

        // A DefaultAttack grant replaces the species' basic attack, which only exists in a battle.
        _store.UpsertItemGrantedAction(new ItemGrantedActionRow(
            BladeContainer, 0, "action.general.0003", ItemGrantRole.DefaultAttack));
        // A plain `Granted` entry is an extra selectable and is NOT inert on the lawn — the negative
        // arm, so the tag is proven to discriminate rather than to light up for any grant at all.
        _store.UpsertItemGrantedAction(new ItemGrantedActionRow(
            HelmContainer, 0, "action.general.0001", ItemGrantRole.Granted));

        var rows = await ArmouryBattleOnly();
        Assert.True(rows[_bladeId]);
        Assert.False(rows[_helmId]);
    }

    /// <summary>instanceId → `battleOnly`, off the same real module 20 route.</summary>
    async Task<Dictionary<string, bool>> ArmouryBattleOnly()
    {
        var resp = await _http.GetAsync($"/api/items/armoury/{_playerKey}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("rows").EnumerateArray()
            .ToDictionary(r => r.GetProperty("instanceId").GetString()!,
                          r => r.GetProperty("battleOnly").GetBoolean(),
                          StringComparer.Ordinal);
    }

    /// <summary>instanceId → `containerName`, off the real module 20 route (item-content T3).</summary>
    async Task<Dictionary<string, string>> ArmouryNames()
    {
        var resp = await _http.GetAsync($"/api/items/armoury/{_playerKey}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("rows").EnumerateArray()
            .ToDictionary(r => r.GetProperty("instanceId").GetString()!,
                          r => r.GetProperty("containerName").GetString()!,
                          StringComparer.Ordinal);
    }

    /// <summary>
    /// ⛔ <b>item-content `item-naming` T3.</b> The armoury row carried no name at all, so
    /// `ArmouryList.tsx` fell back to `adapt.ts`'s `?? containerId` and printed
    /// <c>item.equip-blade</c> where a name belongs. T2 had already carried the base type's authored
    /// `name` through to the CARD; this puts the same string on the row.
    ///
    /// <para>⚠ And the absent case is asserted beside it: a container the corpus does not carry sends
    /// <c>""</c>, never the id. The client turns that into a sentence — a shortened or prettified id
    /// would still be an id.</para>
    /// </summary>
    [Fact]
    public async Task Armoury_carriesTheBaseTypesAuthoredNameAndNeverTheContainerId()
    {
        var names = await ArmouryNames();
        Assert.Equal(3, names.Count);
        Assert.Equal("Honed Hatchet", names[_bladeId]);
        Assert.Equal("", names[_helmId]);
        Assert.Equal("", names[_lordlyId]);
        Assert.DoesNotContain(names.Values, v => v.Contains('.', StringComparison.Ordinal));
    }

    /// <summary>instanceId → `assigned`, off the real module 20 route.</summary>
    async Task<Dictionary<string, bool>> ArmouryRows()
    {
        var resp = await _http.GetAsync($"/api/items/armoury/{_playerKey}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("rows").EnumerateArray()
            .ToDictionary(r => r.GetProperty("instanceId").GetString()!,
                          r => r.GetProperty("assigned").GetBoolean(),
                          StringComparer.Ordinal);
    }

    [Fact]
    public async Task Unequip_aRelicsRole_isRefusedRatherThanTakingItOffBehindTheRelicFlowsBack()
    {
        _store.SaveAssignment(_specimenId, ItemRole.ArmamentPrimary, "stock", "relic.ashen_reliquary");

        var (status, body) = await Post("unequip", new
        {
            playerId = _playerId, specimenId = _specimenId, role = ArmamentPrimary,
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("equip.role-held-by-relic", body.GetProperty("reason").GetString());
        Assert.Single(_store.ListAssignments(_specimenId));
    }

    [Fact]
    public async Task Equip_anUnknownRole_isRefusedByName()
    {
        var (status, body) = await Post("equip", new
        {
            playerId = _playerId, specimenId = _specimenId, instanceId = _bladeId, role = "left-antenna",
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("equip.role-unknown", body.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Equip_withoutARole_is400()
    {
        var resp = await _http.PostAsJsonAsync("/api/items/equip", new
        {
            playerId = _playerId, specimenId = _specimenId, instanceId = _bladeId,
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    /// <summary>
    /// ⭐ <b>Module 3's unlock predicate is consulted on the real equip path</b>, not merely built —
    /// the wiring gap `spec-equip-assign.md` calls out by name. Driven in-process because closing a
    /// slot needs an <c>ISlotUnlockRule</c>, and nothing configures one at boot today (D2: every slot
    /// ships open, and the predicate exists so that stays reversible).
    /// </summary>
    [Fact]
    public void Equip_intoASlotTheUnlockPredicateCloses_isRefusedWithItsOwnReason()
    {
        var closed = new ItemEquipService(_store, new EquipGate(new SlotUnlock(new ClosesEverything())));

        var outcome = closed.Equip(_playerId, _specimenId, _bladeId, ArmamentPrimary);

        Assert.False(outcome.Ok);
        Assert.Contains("equip.role-locked", outcome.Reason);
        Assert.Empty(_store.ListAssignments(_specimenId));
    }

    sealed class ClosesEverything : ISlotUnlockRule
    {
        public bool Evaluate(ItemRole role, ActorContext actor) => false;
    }
}
