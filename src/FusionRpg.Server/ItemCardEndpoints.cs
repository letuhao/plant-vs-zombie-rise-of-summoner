using System.Text.Json;
using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Display;
using FusionRpg.Core.Items.Sockets;
using FusionRpg.Data;

namespace FusionRpg.Server;

/// <summary>
/// ⏸ <b>The second stopgap over module 6's missing <c>item_base_type</c> table</b>, and it says so
/// for the same reason <see cref="BaseTypeSocketMaxCorpus"/> does. Module 6 shipped the 740-entry
/// corpus as seed JSON and the Core readers, but no table and no loader — and a card with no base
/// type has no header, which is why <see cref="RpgStore.GetItemCardInput"/> throws by name rather
/// than rendering a nameless item.
///
/// <para>Every field is a display KEY or a frame/role id, never the base type's own
/// <c>container_id</c>, which spec-item-card.md §2.4 forbids showing. The day module 6 lands the
/// table, this class is deleted and the delegate reads the table.</para>
/// </summary>
public static class ItemBaseTypeCorpus
{
    /// <summary>
    /// Recursive: the corpus is partitioned into subdirectories (<c>footing/</c>, <c>girdle/</c>, …)
    /// as well as files at the root, and a non-recursive walk silently loses those partitions.
    /// Returns <c>null</c> for an id the corpus does not carry, which the card route refuses by name.
    /// </summary>
    public static Func<string, CardBaseType?> Load(string baseTypesDir)
    {
        var byId = new Dictionary<string, CardBaseType>(StringComparer.Ordinal);
        if (!Directory.Exists(baseTypesDir)) return _ => null;

        foreach (var file in Directory
                     .EnumerateFiles(baseTypesDir, "*.json", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(File.ReadAllText(file)); }
            catch (JsonException) { continue; }

            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("entries", out var entries) ||
                    entries.ValueKind != JsonValueKind.Array) continue;

                foreach (var entry in entries.EnumerateArray())
                {
                    if (Str(entry, "id") is not { Length: > 0 } id) continue;
                    if (Str(entry, "nameKey") is not { Length: > 0 } nameKey) continue;

                    byId[id] = new CardBaseType(
                        nameKey,
                        // Class and role reach the card as KEYS derived from the corpus's own ids —
                        // the same `{kind}.{id}` derivation `RpgStore.GetItemCardInput` already uses
                        // for `rarity.*` / `set.*` / `combo.*`, and whether `content/display/en.json`
                        // carries a row for one is `DisplayRules.MissingDisplayKey`'s question.
                        ClassNounKey: Prefixed("class.", Str(entry, "class")),
                        Frame: Str(entry, "frame") ?? "",
                        RoleNameKey: Prefixed("role.", Str(entry, "role")),
                        FlavourKey: Str(entry, "flavorKey"));
                }
            }
        }

        return baseTypeId => byId.TryGetValue(baseTypeId, out var value) ? value : null;
    }

    /// <summary>An explicit lookup for tests and for a host with no corpus on disk.</summary>
    public static Func<string, CardBaseType?> From(IReadOnlyDictionary<string, CardBaseType> byId) =>
        baseTypeId => byId.TryGetValue(baseTypeId, out var value) ? value : (CardBaseType?)null;

    static string? Str(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static string Prefixed(string prefix, string? id) => id is { Length: > 0 } ? prefix + id : "";
}

/// <summary>
/// The gem catalog the DAL cannot reconstruct — <c>item_socket.insert_container_id</c> names a
/// <c>gem.*</c> container and <see cref="ContainerRow"/> carries no element, tier or family, which is
/// exactly the gap <see cref="ItemCardCorpus.LookupInsert"/> documents.
///
/// <para>⚠ <b>The corpus authors no tier</b>, and this does not invent one. <c>gems/*.json</c> carries
/// a <c>powerBand</c>, which is a different axis, so every insert reports
/// <see cref="UnauthoredInsertTier"/>. That is the SAFE direction and not an arbitrary pick:
/// <c>CombinationDistance</c> filters ingredients with <c>Insert.Tier &gt;= need.MinTier</c>, so the
/// lowest rung can only ever UNDER-report a resonance — it can never promise one the evaluator would
/// not fire, which is module 16's own rule. The day the corpus authors a tier, this reads it.</para>
/// </summary>
public static class GemInsertCorpus
{
    /// <summary>Structural, not a tunable: it is the identity of "no tier was authored", and a
    /// balance pass has nothing to change here — the fix is authoring the field, not moving this
    /// number. It is also the minimum of module 16's insert ladder, which is what makes it safe.</summary>
    public const int UnauthoredInsertTier = 1;

    public static Func<string, CardInsertLookup?> Load(string gemsDir)
    {
        var byId = new Dictionary<string, CardInsertLookup>(StringComparer.Ordinal);
        if (!Directory.Exists(gemsDir)) return _ => null;

        foreach (var file in Directory
                     .EnumerateFiles(gemsDir, "*.json", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(File.ReadAllText(file)); }
            catch (JsonException) { continue; }

            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("entries", out var entries) ||
                    entries.ValueKind != JsonValueKind.Array) continue;

                foreach (var entry in entries.EnumerateArray())
                {
                    if (Str(entry, "id") is not { Length: > 0 } id) continue;

                    byId[id] = new CardInsertLookup(
                        new InsertDef(id, Str(entry, "family") ?? "", Str(entry, "element") ?? "",
                            UnauthoredInsertTier),
                        // §2.4: the CELL shows a display key, never the insert's container id.
                        Str(entry, "nameKey") ?? id);
                }
            }
        }

        return containerId => byId.TryGetValue(containerId, out var value) ? value : (CardInsertLookup?)null;
    }

    public static Func<string, CardInsertLookup?> From(IReadOnlyDictionary<string, CardInsertLookup> byId) =>
        containerId => byId.TryGetValue(containerId, out var value) ? value : (CardInsertLookup?)null;

    static string? Str(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

// ---- wire shapes ----------------------------------------------------------------------------------

/// <summary>
/// One <see cref="DisplayLine"/>, on the wire.
///
/// <para><b>A thin DTO, not a redesign, and it exists for exactly one reason:</b> the default
/// serializer writes a C# enum as a NUMBER, and <c>UnitClass</c>/<c>SourceKind</c> are two closed
/// vocabularies whose members are the contract. A route that emitted <c>"unit": 7</c> would pin the
/// web client to a declaration ORDER, which is the one thing an append-only enum is allowed to
/// change. So both go out as their enum names, the same
/// <c>.ToString()</c> convention <see cref="ItemSurfaceEndpoints"/> already uses for surface state
/// and combination state, and both stay NULLABLE because their null is load-bearing: a structural
/// line (a header, a footer, a requirement clause) carries no magnitude and names no unit.</para>
/// </summary>
public sealed record DisplayLineDto(
    string Key,
    IReadOnlyDictionary<string, string> Args,
    string? Unit,
    string? SourceKind,
    int GroupOrder,
    int? RollBarSegments,
    string? ContextRead,
    int? RollQualityPerMille);

public sealed record DisplayBlockDto(string BlockKey, IReadOnlyList<DisplayLineDto> Lines);

/// <summary>
/// One rendered card — module 10's <see cref="DisplayModel"/>, unchanged.
/// </summary>
/// <param name="Fingerprint"><see cref="DisplayModel.Fingerprint"/>, carried rather than recomputed:
/// it is the byte-identity claim spec-item-card.md makes over one
/// <c>(container_id, catalog_revision, roll_seed)</c>, and a client that caches a card wants the same
/// key the determinism test asserts on.</param>
public sealed record ItemCardDto(
    string InstanceId, IReadOnlyList<DisplayBlockDto> Blocks, string Fingerprint);

public sealed record ChannelDeltaDto(
    string Channel, string Unit, long Incumbent, long Candidate, long Delta);

public sealed record VerdictBadgeDto(string LabelKey, string Shape);

public sealed record SidegradeTradeDto(
    IReadOnlyList<ChannelDeltaDto> YouGain, IReadOnlyList<ChannelDeltaDto> YouGiveUp);

/// <summary><c>Unit</c> is nullable and its own group — an unresolvable unit is never folded into
/// <c>GameUnits</c>, which is SC4 as a wire shape rather than as a component's good intention.</summary>
public sealed record UnitClassGroupDto(string? Unit, IReadOnlyList<ChannelDeltaDto> Deltas);

/// <summary>
/// <see cref="CompareModel"/>, on the wire. <c>Incumbent</c>/<c>Candidate</c> are
/// <see cref="CompareModel.Left"/>/<see cref="CompareModel.Right"/> under the names
/// <see cref="ItemCardCompare.Compare"/>'s own parameter documentation gives them, so nothing here
/// renames a Core concept.
/// </summary>
public sealed record ItemCompareDto(
    ItemCardDto Incumbent,
    ItemCardDto Candidate,
    IReadOnlyList<int> DifferingLineIndexes,
    IReadOnlyList<ChannelDeltaDto> Deltas,
    string Dominance,
    VerdictBadgeDto Badge,
    SidegradeTradeDto Trade,
    IReadOnlyList<UnitClassGroupDto> UnitGroups,
    int MeanRollQualityMilliIncumbent,
    int MeanRollQualityMilliCandidate,
    string FootnoteKey,
    string? IncomparableReasonKey);

/// <summary>Why a card could not be rendered, in the item program's own refusal vocabulary. Same
/// shape as the workbench's and the equip route's, so <c>httpErrorMessage</c> lifts <c>reason</c> out
/// of any of the three without a special case.</summary>
public sealed record ItemCardRefusalDto(bool Ok, string Reason, string InstanceId);

// ---- the service ----------------------------------------------------------------------------------

/// <summary>
/// ⭐ <b>The production caller module 10 and module 20 both named as their last blocker.</b>
///
/// <para><c>ItemCardRenderer.Render</c>, <c>ItemCardCompare.Compare</c> and
/// <c>DominancePresentation</c> all shipped tested, and <b>nothing outside <c>tests/</c> called any
/// of them</b> — so the web card rendered an honest "pending" for every block. This class calls them
/// against a real stored item, through <see cref="RpgStore.GetItemCardInput"/>.</para>
///
/// <para>⛔ <b>It computes nothing.</b> Every line, every delta, every verdict and the footnote come
/// from the module that owns them, called once. The only thing this file decides is which HTTP
/// status a named refusal gets.</para>
///
/// <para><b>Named gaps, carried forward rather than papered over.</b> <c>SalvageYield</c> and
/// <c>Power</c> stay null (the DAL's own documented omission — both are computed reads over tuning,
/// not stored rows), <c>ItemCardCorpus.ItemName</c> stays empty so the header falls back to the base
/// type's own <c>nameKey</c> (module 8's <c>ItemNameComposer</c> needs the <c>nameWords</c> corpus and
/// the rare two-word draw, neither of which the Server loads), and per-item attribute
/// <c>Requirements</c> stay empty because no shipped table carries one.</para>
/// </summary>
public sealed class ItemCardService
{
    readonly RpgStore _store;
    readonly ItemCardCorpus _corpus;
    readonly EquipGate _gate;

    public ItemCardService(RpgStore store, ItemCardCorpus corpus, EquipGate? gate = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _corpus = corpus ?? throw new ArgumentNullException(nameof(corpus));
        _gate = gate ?? new EquipGate();
    }

    /// <summary>A rendered card, or a named refusal with the status it deserves. <c>Card</c> and
    /// <c>Reason</c> are mutually exclusive by construction.</summary>
    public sealed record CardResult(ItemCardDto? Card, string Reason, int Status);

    public sealed record CompareResultDto(ItemCompareDto? Compare, string Reason, int Status);

    // ---- one card ----------------------------------------------------------------------------------

    public CardResult Card(string instanceId, string? specimenId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return new CardResult(null, "item.instance-required: name the item to render", StatusCodes.Status400BadRequest);

        if (!TryWearer(specimenId, out var wearer, out var wearerRefusal))
            return new CardResult(null, wearerRefusal, StatusCodes.Status409Conflict);

        return Render(instanceId, wearer, out var card, out var reason, out var status)
            ? new CardResult(card, "", StatusCodes.Status200OK)
            : new CardResult(null, reason, status);
    }

    // ---- two cards ---------------------------------------------------------------------------------

    /// <summary>
    /// The comparison, over two real stored items.
    ///
    /// <para>Both sides are rendered by the SAME <c>ItemCardRenderer.Render</c> the single-card route
    /// calls, and the diff is taken over those rendered lines — spec-item-card.md's own wording:
    /// <i>"comparison diffs rendered lines, so the server must be able to call the same function the
    /// tooltip calls"</i>. A second delta pass here would be the two-implementations defect this
    /// level exists to make impossible.</para>
    /// </summary>
    public CompareResultDto Compare(string candidateId, string incumbentId, string? specimenId)
    {
        if (string.IsNullOrWhiteSpace(candidateId) || string.IsNullOrWhiteSpace(incumbentId))
            return new CompareResultDto(null,
                "item.instance-required: a comparison needs both an incumbent and a candidate",
                StatusCodes.Status400BadRequest);

        if (string.Equals(candidateId, incumbentId, StringComparison.Ordinal))
            return new CompareResultDto(null,
                $"item.compare-same-instance: '{candidateId}' cannot be weighed against itself",
                StatusCodes.Status409Conflict);

        if (!TryWearer(specimenId, out var wearer, out var wearerRefusal))
            return new CompareResultDto(null, wearerRefusal, StatusCodes.Status409Conflict);

        // The INPUTS are needed as well as the cards: the delta table reads the instances' own frozen
        // atom rows, which is what keeps the numbers under the verdict identical to the numbers on
        // the lines above it.
        if (!TryInput(incumbentId, wearer, out var incumbentInput, out var reason, out var status))
            return new CompareResultDto(null, reason, status);
        if (!TryInput(candidateId, wearer, out var candidateInput, out reason, out status))
            return new CompareResultDto(null, reason, status);

        DisplayModel left, right;
        try
        {
            left = ItemCardRenderer.Render(incumbentInput!);
            right = ItemCardRenderer.Render(candidateInput!);
        }
        catch (DisplayTemplateRejection ex)
        {
            return new CompareResultDto(null, $"item.display-template-missing: {ex.Message}",
                StatusCodes.Status409Conflict);
        }

        var model = ItemCardCompare.Compare(
            left, right,
            ItemCardCompare.AtomsOf(incumbentInput!.Instance, incumbentInput.LookupAtom),
            ItemCardCompare.AtomsOf(candidateInput!.Instance, candidateInput.LookupAtom),
            _corpus.Registry);

        return new CompareResultDto(
            new ItemCompareDto(
                Incumbent: ToDto(incumbentId, model.Left),
                Candidate: ToDto(candidateId, model.Right),
                DifferingLineIndexes: model.DifferingLineIndexes,
                Deltas: model.Deltas.Select(ToDto).ToList(),
                Dominance: model.Dominance.ToString(),
                Badge: new VerdictBadgeDto(model.Badge.LabelKey, model.Badge.Shape),
                Trade: new SidegradeTradeDto(
                    model.Trade.YouGain.Select(ToDto).ToList(),
                    model.Trade.YouGiveUp.Select(ToDto).ToList()),
                UnitGroups: model.UnitGroups
                    .Select(g => new UnitClassGroupDto(g.Unit?.ToString(), g.Deltas.Select(ToDto).ToList()))
                    .ToList(),
                MeanRollQualityMilliIncumbent: model.MeanRollQualityMilliLeft,
                MeanRollQualityMilliCandidate: model.MeanRollQualityMilliRight,
                FootnoteKey: model.FootnoteKey,
                IncomparableReasonKey: model.IncomparableReasonKey),
            "", StatusCodes.Status200OK);
    }

    // ---- shared ------------------------------------------------------------------------------------

    bool Render(string instanceId, ItemCardWearer? wearer, out ItemCardDto? card, out string reason, out int status)
    {
        card = null;
        if (!TryInput(instanceId, wearer, out var input, out reason, out status)) return false;

        try
        {
            card = ToDto(instanceId, ItemCardRenderer.Render(input!));
        }
        catch (DisplayTemplateRejection ex)
        {
            // §2.4's rule, surfaced rather than swallowed: a family with no template would put a raw
            // id on a tooltip, so the renderer refuses and so does this route.
            reason = $"item.display-template-missing: {ex.Message}";
            status = StatusCodes.Status409Conflict;
            return false;
        }

        reason = "";
        status = StatusCodes.Status200OK;
        return true;
    }

    bool TryInput(string instanceId, ItemCardWearer? wearer, out ItemCardInput? input, out string reason, out int status)
    {
        input = null;
        try
        {
            input = _store.GetItemCardInput(instanceId, _corpus, wearer);
        }
        catch (InvalidOperationException ex)
        {
            // The DAL's hard failures, each already named there: a container the base-type corpus does
            // not carry, a filled socket the gem catalog does not carry, a rarity off the stored
            // ladder. All three are "this card cannot be assembled honestly", never a 500.
            reason = $"item.card-unrenderable: {ex.Message}";
            status = StatusCodes.Status409Conflict;
            return false;
        }

        if (input is null)
        {
            // The three genuinely absent rows the DAL distinguishes by returning null: an unowned
            // instance, a missing `effect_instance`, a container the catalog does not have.
            reason = $"item.unknown: no owned item '{instanceId}'";
            status = StatusCodes.Status404NotFound;
            return false;
        }

        reason = "";
        status = StatusCodes.Status200OK;
        return true;
    }

    /// <summary>
    /// Who is asking. Absent, the card still renders — it just carries no requirement block, no
    /// refusal and no set progress, because all three are facts about a WEARER. A specimen id that
    /// names nothing is refused rather than silently treated as absent: the caller asked a question
    /// about a creature, and answering about no creature at all would be a different answer.
    /// </summary>
    bool TryWearer(string? specimenId, out ItemCardWearer? wearer, out string reason)
    {
        wearer = null;
        reason = "";
        if (specimenId is not { Length: > 0 }) return true;

        var row = _store.GetUniqueActor(specimenId);
        if (row is null)
        {
            reason = $"equip.specimen-unknown: no bound creature '{specimenId}'";
            return false;
        }

        // ⚠ `UniqueActorDto.Level` is a `long` and `SpecimenActor.Level` is an `int` — a level is a
        // ladder INDEX, not a magnitude, so `int` is the repo's shape for it. The narrowing is
        // `checked` for the same reason `ItemEquipService` checks it: a clamp would silently admit an
        // item the level gate should refuse.
        wearer = new ItemCardWearer(
            new SpecimenActor(row.InstanceId, Frame: null, checked((int)row.Level), Faction: null), _gate);
        return true;
    }

    static ItemCardDto ToDto(string instanceId, DisplayModel model) => new(
        instanceId,
        model.Blocks
            .Select(b => new DisplayBlockDto(b.BlockKey, b.Lines.Select(ToDto).ToList()))
            .ToList(),
        model.Fingerprint());

    static DisplayLineDto ToDto(DisplayLine line) => new(
        line.Key, line.Args, line.Unit?.ToString(), line.SourceKind?.ToString(), line.GroupOrder,
        line.RollBar?.Segments, line.ContextRead, line.RollQualityPerMille);

    static ChannelDeltaDto ToDto(ChannelDelta d) =>
        new(d.Channel, d.Unit, d.Incumbent, d.Candidate, d.Delta);
}

/// <summary>
/// item module 20 (<c>item-surfaces</c>) — the <b>see</b> and <b>compare</b> surfaces' own read-only
/// routes, and the last two of the six that had no server half.
///
/// <para>Its own file rather than more lines in <see cref="ItemSurfaceEndpoints"/> for the same
/// file-per-concern reason <c>WorkbenchEndpoints.cs</c> and <c>ItemEquipEndpoints.cs</c> are separate:
/// these two need a corpus and a gate that the three armoury routes do not. Both are
/// <c>MapGet</c> — module 20 carries no write path, deliberately.</para>
/// </summary>
public static class ItemCardEndpoints
{
    public static void MapItemCard(this WebApplication app, ItemCardService cards)
    {
        if (cards is null) throw new ArgumentNullException(nameof(cards));

        // The eleven blocks of one item, rendered. `specimenId` is optional and adds the three
        // wearer-shaped blocks (requirements, refusal, set progress) — an item in the bag advances no
        // set and refuses nothing, so its absence is a different card, not a poorer one.
        app.MapGet("/api/items/{instanceId}/card", (string instanceId, string? specimenId) =>
            Render(instanceId, cards.Card(instanceId, specimenId)));

        // Incumbent versus candidate. The path reads "compare THIS item against THAT one", so
        // `{instanceId}` is the candidate the player is deciding about and `{incumbentId}` is what
        // they are already wearing — the same direction `ItemCardCompare.Compare(left, right)` takes.
        app.MapGet("/api/items/{instanceId}/compare/{incumbentId}",
            (string instanceId, string incumbentId, string? specimenId) =>
                Render(instanceId, cards.Compare(instanceId, incumbentId, specimenId)));
    }

    /// <summary>
    /// A refused render is the named rule at the status it deserves — <b>404 when the row genuinely
    /// is not there</b>, 409 when it is there and cannot be rendered honestly, 400 when the request
    /// itself is malformed. Never a 500, and never a 200 carrying an empty card: an empty card and an
    /// unrenderable one look identical to a player and must not look identical on the wire.
    /// </summary>
    static IResult Render(string instanceId, ItemCardService.CardResult result) =>
        result.Card is { } card
            ? Results.Ok(card)
            : Results.Json(new ItemCardRefusalDto(false, result.Reason, instanceId), statusCode: result.Status);

    static IResult Render(string instanceId, ItemCardService.CompareResultDto result) =>
        result.Compare is { } compare
            ? Results.Ok(compare)
            : Results.Json(new ItemCardRefusalDto(false, result.Reason, instanceId), statusCode: result.Status);
}
