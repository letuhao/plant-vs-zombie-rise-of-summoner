using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Display;
using FusionRpg.Core.Items.Grants;
using FusionRpg.Core.Items.Mutation;
using FusionRpg.Core.Items.Sockets;
using FusionRpg.Core.Items.Surfaces;
using FusionRpg.Core.Items.Thresholds;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Data;

/// <summary>
/// One socketed insert, joined to the display key its cell renders. <see cref="Def"/> is module 16's
/// own evaluator input; <see cref="NameKey"/> is what §2.4 lets the cell show instead of the insert's
/// container id.
/// </summary>
public readonly record struct CardInsertLookup(InsertDef Def, string NameKey);

/// <summary>
/// The facts an item card needs that <b>no shipped table carries</b>, supplied by the caller.
///
/// <para>This record exists because the honest answer to "can the DAL assemble a whole card from
/// SQL?" is <i>almost</i>. Three things genuinely are not in the database and saying so is cheaper
/// than inventing them:</para>
///
/// <list type="number">
/// <item><b>The base type.</b> There is no <c>item_base_type</c> table — module 6 shipped the 740-row
/// JSON corpus and the Core readers, not a table, and <c>RpgStore.ItemUniques.cs</c> already records
/// that absence by name where §5.2 wanted an FK. So <c>nameKey</c>, the class noun, the role name and
/// the flavour key have to arrive from the corpus.</item>
/// <item><b>The gem catalog.</b> <c>item_socket.insert_container_id</c> names a <c>gem.*</c> container,
/// and <see cref="ContainerRow"/> carries no element, tier or family — so an <see cref="InsertDef"/>
/// cannot be reconstructed from <c>effect_container</c> alone.</item>
/// <item><b>Two tuning objects.</b> <see cref="SocketTuning"/>/<see cref="ItemSurfaceTuning"/> come
/// from <c>data/tuning/*.json</c>, which the Server owns loading.</item>
/// </list>
///
/// <para>⛔ <b>Nothing here is optional-with-a-guess.</b> Where a piece is absent the card renders
/// that block EMPTY and the reason is documented on the field, never filled in with a plausible
/// default — an invented gem element would put a resonance on a card the evaluator would not fire.</para>
/// </summary>
/// <param name="LookupBaseType">
/// <c>container_id</c> → module 6's base-type display facts. <b>Required</b>: a card with no base type
/// has no header. Return <c>null</c> and <see cref="RpgStore.GetItemCardInput"/> throws by name rather
/// than rendering a nameless item.
/// </param>
/// <param name="LookupInsert">
/// A <c>gem.*</c> container id → its evaluator def and display key. <c>null</c> is legal only while
/// every socket on the item is empty; a FILLED socket with no lookup throws, because rendering it as
/// empty would be a lie about the item's own state.
/// </param>
/// <param name="Palette">
/// The ten rarity hexes in ordinal order. Defaults to <see cref="RarityPalette.Dark"/> — the shipped
/// dark-theme ladder, not a preference: a caller rendering the light theme passes
/// <see cref="RarityPalette.Light"/>.
/// </param>
/// <param name="ItemName">
/// Module 8's <c>ItemNameComposer.Compose</c> output, or module 17's authored unique name.
/// <b>Module 10 renders the name; it does not derive it</b> (spec-affix-legality.md's own note), and
/// neither does this method — the composer needs the <c>nameWords</c> corpus and the rare two-word
/// draw, both of which live outside SQL. Empty falls back to the base type's own name key.
/// </param>
public sealed record ItemCardCorpus(
    Func<string, CardBaseType?> LookupBaseType,
    Func<string, CardInsertLookup?>? LookupInsert = null,
    SocketTuning? SocketTuning = null,
    ItemSurfaceTuning? SurfaceTuning = null,
    EnhancementTuning? Enhancement = null,
    IReadOnlyList<string>? Palette = null,
    string ItemName = "",
    DerivedStatRegistry? Registry = null);

/// <summary>
/// Who is asking. Absent, the card still renders — it just carries no requirement block, no refusal
/// and no set progress, because all three are facts about a <i>wearer</i> and not about the item.
/// </summary>
/// <param name="Actor">Module 4's own gate input. Its <c>SpecimenId</c> is the
/// <c>rpg_item_assignment.specimen_id</c> this method reads assignments and set membership under.</param>
/// <param name="Gate">Module 4's <c>EquipGate</c>. Defaults to a fresh one over the default
/// <c>SlotUnlock</c>, which is what <c>EquipGate</c>'s own parameterless path already does.</param>
/// <param name="Requirements">
/// I11's attribute clauses. Supplied rather than read: <b>no shipped table carries a per-item
/// attribute requirement</b> — <c>EquipGate</c> refuses on role/frame/level/faction and on nothing
/// else — so reading one out of SQL would mean inventing the column first.
/// </param>
/// <param name="FactionReq">Content-restricted faction clause (I11 §2.3), inert until modules 13/17
/// ship content that sets it — passed through rather than assumed absent.</param>
public sealed record ItemCardWearer(
    SpecimenActor Actor,
    EquipGate? Gate = null,
    IReadOnlyList<CardRequirement>? Requirements = null,
    string? FactionReq = null);

public sealed partial class RpgStore
{
    /// <summary>
    /// ⭐ <b>The DAL read path P2.5 deferred</b>: an <c>instance_id</c> in, a real
    /// <see cref="ItemCardInput"/> out, assembled from the rows the shipped stores already hold — so a
    /// caller goes from "an item id" to a rendered card without hand-building the input.
    ///
    /// <para>⛔ <b>It owns no logic.</b> Every number and every state on the result is produced by the
    /// module that owns it, called once:</para>
    ///
    /// <list type="table">
    /// <item><term>module 1 <c>durable-ownership</c></term><description><c>rpg_item</c> —
    /// <see cref="GetItem"/> for <c>locked</c>/<c>stale</c>. Its absence is what makes this method
    /// return <c>null</c>: an <c>effect_instance</c> nobody owns is not an item card.</description></item>
    /// <item><term>the instance itself</term><description><see cref="GetInstance"/> +
    /// <see cref="GetContainer"/> — the frozen atoms and the fixed-core <c>seq</c> set the card splits
    /// base / implicit / affix by.</description></item>
    /// <item><term>module 10</term><description><see cref="GetAtom"/> and
    /// <see cref="GetDisplayTemplate"/>, handed over as the two memoised lookups
    /// <see cref="ItemCardInput"/> already takes — the <c>item_display_template</c> rows
    /// <c>Program.cs</c> seeds at boot finally have a production reader.</description></item>
    /// <item><term>module 15 <c>enhance-reroll</c></term><description>
    /// <see cref="GetInstanceMutationHead"/>'s <c>enhance_level</c>, and
    /// <c>EnhancePolicy.GainMilli</c> over the rung's own stored <c>enhance_cap</c>
    /// (<see cref="GetRarityBudget"/>) — never a second curve.</description></item>
    /// <item><term>module 16 <c>sockets</c></term><description><see cref="GetSockets"/> for the cells
    /// and <see cref="GetComboRecipes"/> + <c>CombinationDistance.Evaluate</c> for the combinations —
    /// one evaluation, the same one the bench previews with.</description></item>
    /// <item><term>module 12 <c>threshold-grants</c></term><description><see cref="ListSets"/> +
    /// <see cref="ListAssignments"/> → <c>SetEvaluator.Progress</c> and <c>SetDisclosure.ForWearer</c>,
    /// so the "3 / 4" and the redundancy note come off the same wearer read.</description></item>
    /// <item><term>module 4 <c>equip-assign</c></term><description><c>EquipGate.Explain</c> against the
    /// role the item is actually assigned to.</description></item>
    /// <item><term>modules 17 / 19</term><description><see cref="GetItemUnique"/> and
    /// <see cref="ListItemGrantedActions"/>.</description></item>
    /// </list>
    ///
    /// <para><b>Named gaps, not silent ones.</b> <c>SalvageYield</c> and <c>Power</c> are left null:
    /// module 14's yield needs the materials tuning and module 9's <c>CardPower</c> needs the power
    /// tuning, and both are computed reads rather than stored rows — a caller that has the tuning adds
    /// them with a <c>with</c> expression. <c>NoReassign</c> is always <c>false</c> because the
    /// <c>no_reassign</c> content flag is <b>reserved and deliberately not added</b>
    /// (<c>ssot-inventory.md:208</c>), so there is no column to read and inventing one would be worse
    /// than the false.</para>
    ///
    /// <para>⚠ <b>The display KEYS this derives are structural, not content.</b> <c>rarity.{rung}</c>,
    /// <c>set.{setId}</c>, <c>combo.{shape}</c>, <c>role.{role}</c> and friends are id→key derivations;
    /// whether <c>content/display/en.json</c> actually has a row for one is
    /// <c>DisplayRules.MissingDisplayKey</c>'s question, and today it has none of them — a named
    /// wiring gap in the string corpus, reported by the existing rule rather than papered over here.</para>
    /// </summary>
    /// <returns><c>null</c> when the instance is not an owned item, its <c>effect_instance</c> row is
    /// gone, or its container is not in the catalog — three genuinely absent rows, distinguished from
    /// a mis-assembled card by never throwing for them.</returns>
    public ItemCardInput? GetItemCardInput(
        string instanceId, ItemCardCorpus corpus, ItemCardWearer? wearer = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("instance id", nameof(instanceId));
        if (corpus is null) throw new ArgumentNullException(nameof(corpus));
        if (corpus.LookupBaseType is null) throw new ArgumentNullException(nameof(corpus.LookupBaseType));

        var item = GetItem(instanceId);
        if (item is null) return null;

        var instance = GetInstance(instanceId);
        if (instance is null) return null;

        var container = GetContainer(instance.ContainerId);
        if (container is null) return null;

        var baseType = corpus.LookupBaseType(container.ContainerId)
            ?? throw new InvalidOperationException(
                $"no base type for container '{container.ContainerId}' — module 6's corpus is the only " +
                "source for a base type's nameKey/class/role/flavour and there is no item_base_type table");

        // Memoised so a card with twelve atoms across four families does four template queries, not
        // twelve. Both delegates outlive this call by design: `ItemCardInput` holds them and the
        // renderer calls them, which is the shape the record already documents.
        var atoms = new Dictionary<string, AtomRow?>(StringComparer.Ordinal);
        var templates = new Dictionary<string, DisplayTemplateRow?>(StringComparer.Ordinal);

        AtomRow? LookupAtom(string id)
        {
            if (atoms.TryGetValue(id, out var cached)) return cached;
            return atoms[id] = GetAtom(id);
        }

        DisplayTemplateRow? LookupTemplate(string family)
        {
            if (templates.TryGetValue(family, out var cached)) return cached;
            return templates[family] = GetDisplayTemplate(family);
        }

        var generation = GetItemGeneration(instanceId);
        var head = GetInstanceMutationHead(instanceId);
        var enhanceLevel = head?.EnhanceLevel ?? 0;

        // ONE set-corpus read for the whole card: the socket block needs it for D21's set-piece
        // exclusivity and the set block needs it for the ladder, and two reads of the same table
        // inside one card is two chances for them to disagree as well as twice the query.
        var sets = ListSets();

        var (sockets, combinations) = ReadSocketBlock(instanceId, container, baseType.Frame, corpus, sets);
        var (set, wornRole) = ReadSetBlock(instanceId, wearer, sets);

        return new ItemCardInput(
            instance, container, LookupAtom, LookupTemplate, baseType,
            ReadRarity(container.Rarity, corpus.Palette),
            corpus.ItemName.Length > 0 ? corpus.ItemName : baseType.NameKey)
        {
            ItemLevel = generation?.ItemLevel ?? 0,
            EnhanceLevel = enhanceLevel,
            EnhanceGainMilli = ReadEnhanceGain(container.Rarity, enhanceLevel, corpus.Enhancement),
            LevelReq = container.LevelReq,
            SpecimenLevel = wearer?.Actor.Level ?? 0,
            Refusal = ReadRefusal(wearer, wornRole, generation?.Frame ?? baseType.Frame, container.LevelReq),
            Requirements = wearer?.Requirements ?? Array.Empty<CardRequirement>(),
            Sockets = sockets,
            Combinations = combinations,
            Set = set,
            GrantedActions = ReadGrantedActions(container.ContainerId),
            Unique = GetItemUnique(container.ContainerId),
            Locked = item.Locked,
            Stale = item.Stale,
            // `no_reassign` is reserved and deliberately unadded (ssot-inventory.md:208). No column,
            // no read, no guess.
            NoReassign = false,
            Registry = corpus.Registry,
        };
    }

    // ---- rarity ---------------------------------------------------------------------------------------

    /// <summary>
    /// I1's three redundant channels, all three read off the STORED ladder: the pip count is the rung's
    /// 1-based position in <c>rarity</c>'s own append-only ordinal order (never <c>ordinal / 10</c>,
    /// which would bake the spacing in), and the hex is that same position into the palette.
    /// </summary>
    CardRarity ReadRarity(string? rarityId, IReadOnlyList<string>? palette)
    {
        var hexes = palette ?? RarityPalette.Dark;
        if (string.IsNullOrEmpty(rarityId)) return new CardRarity("", 0, "");

        var ladder = ListRarities().OrderBy(r => r.Ordinal).ToList();
        var index = ladder.FindIndex(r => string.Equals(r.RarityId, rarityId, StringComparison.Ordinal));
        if (index < 0)
            throw new InvalidOperationException(
                $"rarity '{rarityId}' is not on the stored ladder — the container references a rung the " +
                "`rarity` table does not have, which UpsertContainer should already have refused");

        if (index >= hexes.Count)
            throw new InvalidOperationException(
                $"rarity '{rarityId}' is rung {index + 1} but the palette has {hexes.Count} entries — " +
                "the palette must cover the whole ladder or the top rungs render colourless");

        return new CardRarity("rarity." + rarityId, index + 1, hexes[index]);
    }

    /// <summary>Module 15's own curve at the stored level, over the rung's own stored cap. Returns 0
    /// when either the tuning or the <c>enhance_cap</c> row is absent — a missing gain is honest; a
    /// gain computed against a guessed cap is not.</summary>
    long ReadEnhanceGain(string? rarityId, int enhanceLevel, EnhancementTuning? tuning)
    {
        if (enhanceLevel <= 0 || tuning is null || string.IsNullOrEmpty(rarityId)) return 0;
        var cap = GetRarityBudget(rarityId, "enhance_cap");
        return cap is null ? 0 : EnhancePolicy.GainMilli(enhanceLevel, cap.Value, tuning);
    }

    // ---- sockets (module 16) --------------------------------------------------------------------------

    (IReadOnlyList<CardSocketCell> Cells, IReadOnlyList<CardCombination> Combos) ReadSocketBlock(
        string instanceId, ContainerRow container, string frame, ItemCardCorpus corpus,
        IReadOnlyList<SetDef> sets)
    {
        var slots = GetSockets(instanceId);
        if (slots.Count == 0)
            return (Array.Empty<CardSocketCell>(), Array.Empty<CardCombination>());

        var cells = new List<CardSocketCell>(slots.Count);
        var fill = new List<SocketFill>(slots.Count);

        foreach (var slot in slots)
        {
            CardInsertLookup? insert = null;
            if (!slot.IsEmpty)
            {
                if (corpus.LookupInsert is null)
                    throw new InvalidOperationException(
                        $"socket {slot.Index} on '{instanceId}' holds '{slot.InsertContainerId}' but no insert " +
                        "lookup was supplied — no shipped table carries a gem's element/tier/family, and " +
                        "rendering a filled socket as empty would be a lie about the item");

                insert = corpus.LookupInsert(slot.InsertContainerId!)
                    ?? throw new InvalidOperationException(
                        $"socket {slot.Index} on '{instanceId}' holds '{slot.InsertContainerId}', which the " +
                        "gem catalog does not have");
            }

            cells.Add(new CardSocketCell(
                slot.Index,
                slot.Affinity.Length == 0 ? "" : "element." + slot.Affinity,
                slot.Crafted,
                insert?.NameKey,
                // §9.4: an omni insert that counts only toward Diversity says so on its own line.
                OmniCountsDiversityOnly: insert is { } i
                    && string.Equals(i.Def.Element, ElementRoster.OmniId, StringComparison.Ordinal)));

            if (insert is { } filled) fill.Add(new SocketFill(slot.Index, slot.Affinity, filled.Def));
        }

        // Both tunings or neither: `CombinationDistance.Evaluate` refuses a null, and half an
        // evaluation is not a partial answer, it is a wrong one.
        if (corpus.SocketTuning is null || corpus.SurfaceTuning is null)
            return (cells, Array.Empty<CardCombination>());

        var catalog = GetComboRecipes();
        if (catalog.Count == 0) return (cells, Array.Empty<CardCombination>());

        // A container with no parseable item role is not an equippable item, and every combination
        // recipe is gated on the host's role — so there is nothing to evaluate rather than something
        // to guess. The CELLS still render: `item_socket` is the SSOT and its rows are true either way.
        if (!ItemRoles.TryParse(container.Slot ?? "", out var role))
            return (cells, Array.Empty<CardCombination>());

        var host = new SocketHost(
            container.ContainerId, role, frame, slots.Count,
            // D21's own question -- is this base type a member of any shipped set -- answered off
            // `item_set_member` rather than re-derived from the corpus.
            IsSetPiece: sets.Any(s => s.Members.Any(
                m => string.Equals(m.ContainerId, container.ContainerId, StringComparison.Ordinal))));

        var rows = CombinationDistance.Evaluate(
            host, fill, catalog, corpus.SocketTuning, corpus.SurfaceTuning, out _);

        // §4.3: `undiscovered` is never rendered at all, so it never reaches the card.
        var combos = rows
            .Where(r => r.State != CombinationDisplayState.Undiscovered)
            .Select(r => new CardCombination(
                "combo." + r.Shape.ToString().ToLowerInvariant(),
                r.Shape, r.State, r.Distance, r.GrantedTier, r.AllAttuned,
                r.MissingElements.Select(e => "element." + e).ToList()))
            .ToList();

        return (cells, combos);
    }

    // ---- set (module 12) ------------------------------------------------------------------------------

    /// <summary>
    /// The wearer's whole equipped list, once, through module 12's own evaluator — never a per-item
    /// count. Returns the set THIS item advances (or is redundant in), plus the role it occupies, which
    /// the equip gate then re-uses.
    /// </summary>
    (CardSet? Set, ItemRole? WornRole) ReadSetBlock(
        string instanceId, ItemCardWearer? wearer, IReadOnlyList<SetDef> sets)
    {
        if (wearer is null) return (null, null);

        var assignments = ListAssignments(wearer.Actor.SpecimenId);
        if (assignments.Count == 0) return (null, null);

        ItemRole? wornRole = null;
        var worn = new List<EquippedPiece>(assignments.Count);

        foreach (var a in assignments)
        {
            // `item` points at an rpg_item instance; `stock` points straight at a container id — a
            // stock-backed preset entry never pins one specific copy (RpgItemLoadoutEntryRow's own rule).
            var containerId = string.Equals(a.RefKind, "item", StringComparison.Ordinal)
                ? GetInstance(a.RefId)?.ContainerId
                : a.RefId;
            if (containerId is null) continue;

            worn.Add(new EquippedPiece(a.Role, containerId));
            if (string.Equals(a.RefKind, "item", StringComparison.Ordinal)
                && string.Equals(a.RefId, instanceId, StringComparison.Ordinal))
                wornRole = a.Role;
        }

        if (wornRole is null) return (null, null);
        if (sets.Count == 0) return (null, wornRole);

        var progress = SetEvaluator.Progress(worn, sets);
        if (progress.Count == 0) return (null, wornRole);

        // Which set THIS piece touches. Order is the caller's (SetDisclosure's own rule), and `worn`
        // was built in assignment order, so the disclosure below and the count above agree by
        // construction rather than by coincidence.
        var disclosure = SetDisclosure.ForWearer(worn, sets);
        PieceSetDisclosure? mineOrNull = null;
        foreach (var d in disclosure)
            if (d.Piece.Role == wornRole && (d.AdvancesSetIds.Count > 0 || d.RedundantSetIds.Count > 0))
            {
                mineOrNull = d;
                break;
            }

        if (mineOrNull is not { } mine) return (null, wornRole);

        var setId = mine.AdvancesSetIds.FirstOrDefault() ?? mine.RedundantSetIds.FirstOrDefault();
        if (setId is null) return (null, wornRole);

        var def = sets.FirstOrDefault(s => string.Equals(s.SetId, setId, StringComparison.Ordinal));
        if (def is null) return (null, wornRole);

        var p = progress.First(x => string.Equals(x.SetId, setId, StringComparison.Ordinal));

        return (new CardSet(
            "set." + def.SetId, p.Count, p.Total,
            // The WHOLE ladder, always — an inactive threshold is the goal (§4.3).
            def.Tiers.OrderBy(t => t.PiecesRequired)
                .Select(t => new CardSetTier(t.PiecesRequired, t.PiecesRequired <= p.Count, t.IsCapability))
                .ToList(),
            Redundant: mine.RedundantSetIds.Count > 0), wornRole);
    }

    // ---- requirements (module 4) ----------------------------------------------------------------------

    /// <summary>
    /// <c>EquipGate.Explain</c> against the role the item is <b>actually assigned to</b>. No assignment
    /// means no refusal to explain: a card for an item sitting in the bag is not refusing anything, and
    /// picking a plausible role to test it against would invent a rejection.
    /// </summary>
    static EquipRefusal? ReadRefusal(
        ItemCardWearer? wearer, ItemRole? wornRole, string itemFrame, int? levelReq)
    {
        if (wearer is null || wornRole is null) return null;
        var gate = wearer.Gate ?? new EquipGate();
        return gate.Explain(wornRole.Value, wearer.Actor, itemFrame, levelReq, wearer.FactionReq);
    }

    // ---- granted actions (module 19 / G4) --------------------------------------------------------------

    /// <summary>
    /// <c>AlreadyKnown</c> is always <c>false</c>: whether a specimen already knows an action is the
    /// action layer's own state, not the item store's, and a card that guessed it would tell a player
    /// they already have something they do not.
    /// </summary>
    IReadOnlyList<CardGrantedAction> ReadGrantedActions(string containerId) =>
        ListItemGrantedActions(containerId)
            .Where(g => g.Enabled)
            .OrderBy(g => g.Seq)
            .Select(g => new CardGrantedAction(
                "action." + g.ActionId,
                "action." + g.ActionId + ".desc",
                // A DefaultAttack grant replaces the species' basic attack, which only exists in a
                // battle; a plain Granted entry is an extra selectable and is not battle-only.
                BattleOnly: g.Role == ItemGrantRole.DefaultAttack,
                AlreadyKnown: false))
            .ToList();
}
