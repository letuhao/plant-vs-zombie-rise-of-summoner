using System.Net;
using System.Text.Json;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Effects.Atoms.Generation;
using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Display;
using FusionRpg.Core.Items.Drops;
using FusionRpg.Core.Items.Mutation;
using FusionRpg.Core.Items.Sockets;
using FusionRpg.Core.Items.Surfaces;
using FusionRpg.Core.Power;
using FusionRpg.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>
/// ⭐ item modules 10 + 20 — <b>the see and compare routes</b>, against a real in-process host, a real
/// SQLite store and a real rolled item.
///
/// <para>Module 10 shipped <c>ItemCardRenderer.Render</c>, <c>ItemCardCompare.Compare</c> and
/// <c>DominancePresentation</c> green, and module 20 recorded the same blocker module 4 and the
/// workbench modules did: <b>nothing outside <c>tests/</c> called any of them</b>, so every block of
/// the web card rendered its honest "pending" state. These tests drive both routes over HTTP and then
/// check the payload against the Core renderer's own output, which is what separates "the route
/// returned 200" from "the player can read the item".</para>
///
/// <para>⛔ <b>Nothing here is a fixture atom.</b> The catalog is the REAL
/// <c>data/seed/items/affix-families/*.json</c> corpus through the REAL <c>FamilyExpansion</c> and
/// <c>AffixLibraryGenerator</c>, the display templates are the REAL
/// <c>data/seed/items/display-templates/*.json</c> rows, the gem is the REAL
/// <c>data/seed/items/gems/*.json</c> entry, and both items are minted by the REAL
/// <c>Instantiator</c>. A route that rendered something merely plausible fails the fingerprint
/// assertions below.</para>
/// </summary>
public class ItemCardEndpointsTests : IAsyncLifetime
{
    const string Rung = "heirloom";
    const string BladeContainer = "item.card-proof-blade";
    const string HelmContainer = "item.card-proof-helm";
    const string StrandedContainer = "item.card-proof-stranded";
    const string Frame = "humanoid";
    const int ItemLevel = 24;
    const int LevelReq = 20;

    /// <summary>The one gem the socket test puts in a cell — a real <c>gems/g1.json</c> entry, so the
    /// cell's display key comes off the shipped corpus rather than a stand-in.</summary>
    const string EmberShard = "gem.g1-001";

    string _dir = "";
    RpgStore _store = null!;
    WebApplication _app = null!;
    HttpClient _http = null!;
    string _playerKey = "";
    string _specimenId = "";
    string _bladeId = "";
    string _helmId = "";
    string _strandedId = "";
    ItemCardCorpus _corpus = null!;

    // ---- the real corpus -------------------------------------------------------------------------

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

    static string Seed(params string[] parts) =>
        Path.Combine(new[] { RepoRoot(), "data", "seed" }.Concat(parts).ToArray());

    /// <summary>This assembly's own configured ladder, reused rather than rebuilt — the module
    /// initializer already hands <c>PowerTuningHub</c> exactly this, so a second inline copy here
    /// would be a private curve in a test file.</summary>
    static readonly PowerTuning Power = PowerAndAptitudeTuningTestBootstrap.DefaultPower;

    /// <summary>The ladder's own pin index, so <c>contentScale</c> is exactly ×1.000 and a frozen
    /// number is the atom's own roll rather than a scaled one. Structural, not a tunable: moving it
    /// would change what the fixture is testing, not how the game feels.</summary>
    static readonly int PinTheta = Power.Curve.PinIndex;

    static long? FlatReferenceBase(string channel) => channel switch
    {
        "maxHp" or "hp" => FusionRpg.Core.Battle.BattleRuleset.BaseHp(FamilyExpansion.ReferenceLevel),
        "atk" => FusionRpg.Core.Battle.BattleRuleset.BaseAtk(FamilyExpansion.ReferenceLevel),
        "defense" => FusionRpg.Core.Battle.BattleRuleset.BaseDefense(FamilyExpansion.ReferenceLevel),
        _ => null,
    };

    static readonly Lazy<IReadOnlyList<AtomRow>> RealAtoms = new(() =>
    {
        var itemsRoot = Seed("items");
        var tierBands = TierBandsFile.Read(
            File.ReadAllText(Path.Combine(itemsRoot, "_tuning", "tier-bands.v1.json")));

        var families = new List<FamilyEntryInput>();
        foreach (var file in Directory.GetFiles(Path.Combine(itemsRoot, "affix-families"), "*.json")
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            if (Path.GetFileName(file).StartsWith('_')) continue;
            families.AddRange(AffixFamilyFile.Read(Path.GetFileName(file), File.ReadAllText(file)));
        }

        return FamilyExpansion.Expand(families, tierBands, FlatReferenceBase).Rows;
    });

    static readonly Lazy<IReadOnlyList<DisplayTemplateRow>> Templates = new(() =>
        Directory.EnumerateFiles(Seed("items", "display-templates"), "*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .SelectMany(f => DisplayTemplates.Parse(File.ReadAllText(f)))
            .ToList());

    static SocketTuning Sockets() =>
        SocketTuning.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "data", "tuning", "sockets.v1.json")));

    static ItemSurfaceTuning Surfaces() =>
        ItemSurfaceTuning.Parse(File.ReadAllText(
            Path.Combine(RepoRoot(), "data", "tuning", "item-surfaces.v1.json")));

    static EnhancementTuning Enhancement() =>
        EnhancementTuning.Parse(File.ReadAllText(
            Path.Combine(RepoRoot(), "data", "tuning", "enhancement.v1.json")));

    // ---- fixture ---------------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-itemcard-http-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        var playerId = _store.GetCurrentPlayerId();
        _playerKey = playerId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        for (var i = 0; i < RarityLadder.RungIds.Count; i++)
            Assert.True(_store.UpsertRarity(new RarityRow(RarityLadder.RungIds[i], (i + 1) * 10, 3, 0, 1, 5)).Ok);

        // The real atom catalog, the real affix library generated from it, and the real display rows.
        var atoms = RealAtoms.Value;
        Assert.NotEmpty(atoms);
        _store.UpsertAtoms(atoms);

        var byId = atoms.ToDictionary(a => a.AtomId, StringComparer.Ordinal);
        AtomRow? LookupAtom(string id) => byId.TryGetValue(id, out var a) ? a : null;

        var affixes = AffixLibraryGenerator.Generate(atoms);
        foreach (var affix in affixes) Assert.True(_store.UpsertAffix(affix, LookupAtom).IsOk);
        var affixById = affixes.ToDictionary(a => a.AffixId, StringComparer.Ordinal);
        AffixRow? LookupAffix(string id) => affixById.TryGetValue(id, out var a) ? a : null;

        _store.SeedItemDisplayTemplates(Templates.Value);

        // Only families the display corpus actually carries may reach a rolled affix — a family with
        // no template is `DisplayRules.MissingDisplayTemplate`, which the renderer refuses by design.
        var templated = Templates.Value
            .Where(t => t.Status == "live")
            .Select(t => t.RuntimeFamily)
            .ToHashSet(StringComparer.Ordinal);

        // Two DIFFERENT base stats so the two items genuinely touch different channels and the
        // comparison has real deltas to report rather than an accident of the roll.
        var hpBase = atoms.Where(a => a.ParamsJson.Contains("\"maxHp\"", StringComparison.Ordinal))
            .OrderByDescending(a => a.Tier).First();
        var defenseBase = atoms.Where(a => a.ParamsJson.Contains("\"defense\"", StringComparison.Ordinal))
            .OrderByDescending(a => a.Tier).First();

        List<ContainerPoolRow> PoolExcept(params string[] families)
        {
            var pool = affixes
                .Where(a => a.Refs.Count == 1 && a.Refs[0].AtomId is { } id && byId.TryGetValue(id, out var at)
                            && !families.Contains(at.FamilyId, StringComparer.Ordinal)
                            && templated.Contains(at.FamilyId))
                .OrderBy(a => a.AffixId, StringComparer.Ordinal)
                .Select(a => new ContainerPoolRow(a.AffixId, 100))
                .ToList();
            Assert.NotEmpty(pool);
            return pool;
        }

        _bladeId = SeedItem(BladeContainer, ItemRole.ArmamentPrimary, hpBase, PoolExcept(hpBase.FamilyId),
            rollSeed: 0xC0FFEE, LookupAtom, LookupAffix);
        _helmId = SeedItem(HelmContainer, ItemRole.HeadGuard, defenseBase, PoolExcept(defenseBase.FamilyId),
            rollSeed: 0xBEEF, LookupAtom, LookupAffix);
        // Deliberately NOT in the base-type corpus below — the 409 case.
        _strandedId = SeedItem(StrandedContainer, ItemRole.ArmamentPrimary, hpBase, PoolExcept(hpBase.FamilyId),
            rollSeed: 0x1234, LookupAtom, LookupAffix);

        _specimenId = _store.CreateUniqueActor(playerId, "plant", 1).InstanceId;

        // ⭐ The corpus half the DAL cannot know. The gem catalog is the REAL shipped loader over the
        // REAL seed files; the base type is an explicit map because these three container ids are the
        // test's own, and module 6 has no table to register them in.
        _corpus = new ItemCardCorpus(
            ItemBaseTypeCorpus.From(new Dictionary<string, CardBaseType>(StringComparer.Ordinal)
            {
                [BladeContainer] = new("base.card-proof-blade", "class.blade", Frame, "role.armament-primary", null),
                [HelmContainer] = new("base.card-proof-helm", "class.helm", Frame, "role.head-guard", null),
            }),
            GemInsertCorpus.Load(Seed("items", "gems")),
            Sockets(), Surfaces(), Enhancement());

        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(_store);
        builder.WebHost.UseUrls(baseUrl);
        _app = builder.Build();
        _app.UseDeveloperExceptionPage();
        _app.MapItemCard(new ItemCardService(_store, _corpus));
        await _app.StartAsync();

        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    static int GetFreeTcpPort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    /// <summary>A real container, a real mint through <c>Instantiator</c>, a real ownership row and a
    /// real generation stamp — everything the card reader joins over.</summary>
    string SeedItem(
        string containerId, ItemRole role, AtomRow baseStat, IReadOnlyList<ContainerPoolRow> pool,
        long rollSeed, Func<string, AtomRow?> lookupAtom, Func<string, AffixRow?> lookupAffix)
    {
        var container = new ContainerRow
        {
            ContainerId = containerId,
            Kind = ContainerKind.Item,
            Slot = ItemRoles.Id(role),
            Rarity = Rung,
            LevelReq = LevelReq,
            PrefixRolls = 3,
            SuffixRolls = 0,
            Atoms = new[] { new ContainerAtomRow(0, baseStat.AtomId) },
            Pool = pool,
        };
        var upsert = _store.UpsertContainer(container);
        Assert.True(upsert.IsOk, upsert.ToString());

        var mint = Instantiator.TryInstantiate(
            container, lookupAtom, lookupAffix, rollSeed, thetaContent: PinTheta,
            tuning: Power, out var instance, InstanceOrigin.Drop,
            catalogRevision: _store.GetCatalogRevision());
        Assert.True(mint.IsOk, mint.ToString());

        var instanceId = _store.SaveInstance(instance!);
        Assert.True(_store.AcquireItem(new RpgItemRow
        {
            InstanceId = instanceId,
            PlayerId = _playerKey,
            AcquiredUtc = "2026-09-06T00:00:00Z",
            OriginKind = "drop",
        }).IsOk);

        _store.PersistLoot(
            _playerKey,
            new LootManifest("card-proof", "table.card", (ulong)rollSeed, ItemLevel, Array.Empty<LootGrant>(),
                Array.Empty<string>(), "{}", LootPityState.Empty, LootPityState.Empty, null, false, null),
            "test", "card", _store.GetCatalogRevision(), 1,
            new[]
            {
                new ItemGenerationRow(instanceId, 0, containerId, 70, ItemLevel, Frame,
                    ItemRoles.Id(role), "drop"),
            });

        return instanceId;
    }

    async Task<(HttpStatusCode Status, JsonElement Body)> Get(string path)
    {
        var resp = await _http.GetAsync(path);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return (resp.StatusCode, doc.RootElement.Clone());
    }

    static IReadOnlyList<JsonElement> Lines(JsonElement card, string blockKey) =>
        card.GetProperty("blocks").EnumerateArray()
            .Single(b => b.GetProperty("blockKey").GetString() == blockKey)
            .GetProperty("lines").EnumerateArray().ToList();

    // ====================================================================================================
    // The card route
    // ====================================================================================================

    /// <summary>
    /// ⭐ The whole point of the module: an instance id goes in and the eleven ordered blocks come
    /// back, with REAL rendered affix lines off the real corpus — not a placeholder, not an empty
    /// shell. The affix block is the one that was permanently pending in the browser until today.
    /// </summary>
    [Fact]
    public async Task Card_returnsTheElevenBlocksWithRealRenderedLines()
    {
        var (status, body) = await Get($"/api/items/{_bladeId}/card");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(_bladeId, body.GetProperty("instanceId").GetString());
        Assert.Equal(
            CardBlocks.Order,
            body.GetProperty("blocks").EnumerateArray().Select(b => b.GetProperty("blockKey").GetString()!).ToList());

        // Real content, from the two modules that own it.
        Assert.NotEmpty(Lines(body, CardBlocks.BaseStats));
        var affixes = Lines(body, CardBlocks.Affixes);
        Assert.NotEmpty(affixes);

        // Every affix line is a {key, args} leaf with a real template key and a real magnitude arg —
        // never a glued sentence and never an empty bag.
        foreach (var line in affixes)
        {
            Assert.False(string.IsNullOrWhiteSpace(line.GetProperty("key").GetString()));
            Assert.NotEmpty(line.GetProperty("args").EnumerateObject().ToList());
        }

        // The header is one line and carries the identity the browser draws.
        var header = Assert.Single(Lines(body, CardBlocks.Header));
        var args = header.GetProperty("args");
        Assert.Equal("base.card-proof-blade", args.GetProperty("name").GetString());
        Assert.Equal("rarity." + Rung, args.GetProperty("rungKey").GetString());
        Assert.Equal("7", args.GetProperty("pips").GetString());
        Assert.Equal(ItemLevel.ToString(), args.GetProperty("ilvl").GetString());
    }

    /// <summary>
    /// ⭐ <b>The acceptance test.</b> The route's payload must be the Core renderer's own model, so the
    /// fingerprint the wire carries has to equal the fingerprint <c>ItemCardRenderer.Render</c>
    /// produces for the same stored state. A route that reshaped, rounded or re-ordered anything fails
    /// here — which is the only assertion that pins the payload rather than the plumbing.
    /// </summary>
    [Fact]
    public async Task Card_isByteIdenticalToTheCoreRenderersOwnModel()
    {
        var expected = ItemCardRenderer.Render(_store.GetItemCardInput(_bladeId, _corpus)!);

        var (status, body) = await Get($"/api/items/{_bladeId}/card");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(expected.Fingerprint(), body.GetProperty("fingerprint").GetString());
    }

    /// <summary>
    /// ⛔ The two closed vocabularies go out as their NAMES, never as enum ordinals. A numeric
    /// <c>unit</c> would pin the client to a declaration order, which is the one thing an append-only
    /// enum is allowed to change — and both stay nullable, because a structural line carries no
    /// magnitude and must not name a unit it does not have.
    /// </summary>
    [Fact]
    public async Task Card_writesUnitAndSourceKindAsNamesAndLeavesStructuralLinesNull()
    {
        var (_, body) = await Get($"/api/items/{_bladeId}/card");

        var affix = Lines(body, CardBlocks.Affixes)[0];
        Assert.Equal(JsonValueKind.String, affix.GetProperty("sourceKind").ValueKind);
        Assert.Contains(affix.GetProperty("sourceKind").GetString(),
            new[] { nameof(SourceKind.AffixPrefix), nameof(SourceKind.AffixSuffix) });

        // The header is structural: no magnitude, so no unit and no source kind. The null is the
        // point — naming one would be the exact lie SC4 exists to prevent.
        var header = Assert.Single(Lines(body, CardBlocks.Header));
        Assert.Equal(JsonValueKind.Null, header.GetProperty("unit").ValueKind);
        Assert.Equal(JsonValueKind.Null, header.GetProperty("sourceKind").ValueKind);
    }

    /// <summary>A filled socket renders its DISPLAY KEY off the real shipped gem corpus — never the
    /// insert's container id, which §2.4 forbids showing.</summary>
    [Fact]
    public async Task Card_rendersAFilledSocketFromTheRealGemCorpus()
    {
        _store.SetSockets(_bladeId, new List<SocketSlot>
        {
            new(0, "fire", Crafted: false, EmberShard, null),
            new(1, "", Crafted: true, null, null),
        });

        var cells = Lines(await Body($"/api/items/{_bladeId}/card"), CardBlocks.Sockets)
            .Where(l => l.GetProperty("key").GetString() == "item.card.socket.cell")
            .ToList();

        Assert.Equal(2, cells.Count);
        Assert.Equal("gem.ember-shard", cells[0].GetProperty("args").GetProperty("insertKey").GetString());
        Assert.Equal("0", cells[0].GetProperty("args").GetProperty("empty").GetString());
        Assert.Equal("1", cells[1].GetProperty("args").GetProperty("empty").GetString());
        Assert.Equal("1", cells[1].GetProperty("args").GetProperty("crafted").GetString());
    }

    /// <summary>The three wearer-shaped blocks are facts about a CREATURE, so they arrive only when
    /// one is named — and the refusal reaches the card as a reason key plus a remedy, never a bare
    /// boolean.</summary>
    [Fact]
    public async Task Card_withASpecimenWearingIt_carriesTheGatesRefusal()
    {
        _store.SaveAssignment(_specimenId, ItemRole.ArmamentPrimary, EquipRefKinds.Rolled, _bladeId);
        Assert.Equal(1, _store.GetUniqueActor(_specimenId)!.Level);

        var anonymous = await Body($"/api/items/{_bladeId}/card");
        Assert.DoesNotContain(Lines(anonymous, CardBlocks.Requirements),
            l => l.GetProperty("key").GetString() == "item.card.requirement.refused");

        var worn = await Body($"/api/items/{_bladeId}/card?specimenId={_specimenId}");
        var refused = Assert.Single(Lines(worn, CardBlocks.Requirements),
            l => l.GetProperty("key").GetString() == "item.card.requirement.refused");
        Assert.Equal("item.equip.refusal.level-too-low",
            refused.GetProperty("args").GetProperty("reasonKey").GetString());
    }

    // ---- refusals: named, and never a 500 ---------------------------------------------------------

    [Fact]
    public async Task Card_forAnUnknownInstance_is404WithANamedReason()
    {
        var (status, body) = await Get("/api/items/no-such-instance/card");

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.False(body.GetProperty("ok").GetBoolean());
        Assert.Contains("item.unknown", body.GetProperty("reason").GetString());
        Assert.Equal("no-such-instance", body.GetProperty("instanceId").GetString());
    }

    /// <summary>
    /// A container the base-type corpus does not carry is a 409, not a 500: the row is there and the
    /// card cannot be assembled honestly. <c>GetItemCardInput</c> throws by name rather than rendering
    /// a nameless item, and the route turns that into the named refusal instead of an unhandled
    /// exception.
    /// </summary>
    [Fact]
    public async Task Card_whenTheBaseTypeCorpusDoesNotCarryTheContainer_is409NotA500()
    {
        var (status, body) = await Get($"/api/items/{_strandedId}/card");

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("item.card-unrenderable", body.GetProperty("reason").GetString());
        Assert.Contains(StrandedContainer, body.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Card_forAnUnknownSpecimen_isRefusedByNameRatherThanRenderedUnworn()
    {
        var (status, body) = await Get($"/api/items/{_bladeId}/card?specimenId=no-such-creature");

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("equip.specimen-unknown", body.GetProperty("reason").GetString());
    }

    // ====================================================================================================
    // The compare route
    // ====================================================================================================

    /// <summary>
    /// ⭐ The comparison the browser drew as "pending" until today: both cards, the line diff, the
    /// per-channel deltas, and <c>DominancePresentation</c>'s verdict, trade, unit grouping and
    /// permanent footnote — every one of them the Core module's own value.
    /// </summary>
    [Fact]
    public async Task Compare_returnsBothCardsTheDeltasAndTheVerdictPayload()
    {
        var (status, body) = await Get($"/api/items/{_bladeId}/compare/{_helmId}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(_helmId, body.GetProperty("incumbent").GetProperty("instanceId").GetString());
        Assert.Equal(_bladeId, body.GetProperty("candidate").GetProperty("instanceId").GetString());

        // Two different items differ on rendered lines, and the diff indexes the FLATTENED sequence.
        Assert.NotEmpty(body.GetProperty("differingLineIndexes").EnumerateArray().ToList());

        // Module 13's payload.
        var deltas = body.GetProperty("deltas").EnumerateArray().ToList();
        Assert.NotEmpty(deltas);
        foreach (var d in deltas)
            Assert.Equal(
                d.GetProperty("candidate").GetInt64() - d.GetProperty("incumbent").GetInt64(),
                d.GetProperty("delta").GetInt64());

        // Module 20's presentation half — a word AND a shape, never a colour alone.
        Assert.Contains(body.GetProperty("dominance").GetString(),
            Enum.GetNames<DominanceVerdict>());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("badge").GetProperty("shape").GetString()));
        Assert.StartsWith("item.compare.", body.GetProperty("badge").GetProperty("labelKey").GetString());

        // SC4 as a wire shape: every delta lives under a unit-class group, and the groups partition
        // the delta list exactly.
        var grouped = body.GetProperty("unitGroups").EnumerateArray()
            .SelectMany(g => g.GetProperty("deltas").EnumerateArray())
            .Count();
        Assert.Equal(deltas.Count, grouped);

        // The footnote is not optional and there is no flag that could hide it.
        Assert.Equal(DominancePresentation.NoSingleScoreFootnoteKey,
            body.GetProperty("footnoteKey").GetString());
    }

    /// <summary>
    /// ⭐ The compare payload is <c>ItemCardCompare.Compare</c>'s own model, proven the same way the
    /// single card is: both embedded cards fingerprint-match the Core renderer, and the verdict
    /// matches the Core comparison over the same stored state. Nothing is recomputed on the way out.
    /// </summary>
    [Fact]
    public async Task Compare_isTheCoreCompareModelUnchanged()
    {
        var incumbent = _store.GetItemCardInput(_helmId, _corpus)!;
        var candidate = _store.GetItemCardInput(_bladeId, _corpus)!;
        var expected = ItemCardCompare.Compare(
            ItemCardRenderer.Render(incumbent), ItemCardRenderer.Render(candidate),
            ItemCardCompare.AtomsOf(incumbent.Instance, incumbent.LookupAtom),
            ItemCardCompare.AtomsOf(candidate.Instance, candidate.LookupAtom));

        var (_, body) = await Get($"/api/items/{_bladeId}/compare/{_helmId}");

        Assert.Equal(expected.Left.Fingerprint(),
            body.GetProperty("incumbent").GetProperty("fingerprint").GetString());
        Assert.Equal(expected.Right.Fingerprint(),
            body.GetProperty("candidate").GetProperty("fingerprint").GetString());
        Assert.Equal(expected.Dominance.ToString(), body.GetProperty("dominance").GetString());
        Assert.Equal(expected.Deltas.Count, body.GetProperty("deltas").GetArrayLength());
        Assert.Equal(expected.DifferingLineIndexes,
            body.GetProperty("differingLineIndexes").EnumerateArray().Select(x => x.GetInt32()).ToList());
        Assert.Equal(expected.MeanRollQualityMilliRight,
            body.GetProperty("meanRollQualityMilliCandidate").GetInt32());
    }

    /// <summary>The trade is split from the SAME deltas the table renders, so the two halves can never
    /// disagree with the rows above them.</summary>
    [Fact]
    public async Task Compare_splitsTheTradeOutOfTheSameDeltasTheTableShows()
    {
        var (_, body) = await Get($"/api/items/{_bladeId}/compare/{_helmId}");

        var trade = body.GetProperty("trade");
        Assert.All(trade.GetProperty("youGain").EnumerateArray(),
            d => Assert.True(d.GetProperty("delta").GetInt64() > 0));
        Assert.All(trade.GetProperty("youGiveUp").EnumerateArray(),
            d => Assert.True(d.GetProperty("delta").GetInt64() < 0));
    }

    [Fact]
    public async Task Compare_anItemAgainstItself_isRefusedByName()
    {
        var (status, body) = await Get($"/api/items/{_bladeId}/compare/{_bladeId}");

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("item.compare-same-instance", body.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Compare_withAnUnknownIncumbent_is404WithANamedReason()
    {
        var (status, body) = await Get($"/api/items/{_bladeId}/compare/no-such-instance");

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Contains("item.unknown", body.GetProperty("reason").GetString());
        Assert.Contains("no-such-instance", body.GetProperty("reason").GetString());
    }

    // ====================================================================================================
    // The two boot-time corpora
    // ====================================================================================================

    /// <summary>
    /// The base-type loader reads the REAL shipped corpus, including the partitioned subdirectories —
    /// a non-recursive walk silently loses <c>footing/</c> and <c>girdle/</c>, which would make every
    /// item of those roles unrenderable with no error anywhere.
    /// </summary>
    [Fact]
    public void TheBaseTypeCorpus_loadsEveryShippedPartitionAndNamesRealEntries()
    {
        var lookup = ItemBaseTypeCorpus.Load(Seed("items", "base-types"));

        var known = lookup("item.humanoid-main-hand-a-001");
        Assert.NotNull(known);
        Assert.Equal("base.honed-hatchet", known!.Value.NameKey);
        Assert.Equal("class.blade", known.Value.ClassNounKey);
        Assert.Equal("role.armament-primary", known.Value.RoleNameKey);
        Assert.Equal("humanoid", known.Value.Frame);

        // A partitioned file, not one at the corpus root.
        var partitionCount = Directory
            .EnumerateFiles(Seed("items", "base-types"), "*.json", SearchOption.AllDirectories)
            .Count(f => Path.GetDirectoryName(f) != Seed("items", "base-types"));
        Assert.True(partitionCount > 0, "the corpus is partitioned; this test is pointless if it stops being");

        Assert.Null(lookup("item.not-in-the-corpus"));
    }

    /// <summary>The gem loader carries the ELEMENT, which is load-bearing:
    /// <c>CombinationDistance</c> matches ingredients on <c>Insert.Element</c>, so a blank one makes
    /// every element-shaped resonance unreachable.</summary>
    [Fact]
    public void TheGemCorpus_carriesTheElementAndTheDisplayKey()
    {
        var lookup = GemInsertCorpus.Load(Seed("items", "gems"));

        var ember = lookup(EmberShard);
        Assert.NotNull(ember);
        Assert.Equal("fire", ember!.Value.Def.Element);
        Assert.Equal("gem.ember-shard", ember.Value.NameKey);
        Assert.Equal(GemInsertCorpus.UnauthoredInsertTier, ember.Value.Def.Tier);

        Assert.Null(lookup("gem.not-a-gem"));
    }

    async Task<JsonElement> Body(string path)
    {
        var (status, body) = await Get(path);
        Assert.Equal(HttpStatusCode.OK, status);
        return body;
    }
}
