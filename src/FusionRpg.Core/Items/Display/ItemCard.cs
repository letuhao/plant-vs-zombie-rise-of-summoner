using System.Globalization;
using System.Text.Json;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items.Materials;
using FusionRpg.Core.Items.Power;
using FusionRpg.Core.Items.Sockets;
using FusionRpg.Core.Items.Surfaces;
using FusionRpg.Core.Items.Thresholds;
using FusionRpg.Core.Items.Uniques;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Items.Display;

/// <summary>
/// G3 §4.1's eleven blocks, in order. <b>Closed</b> — §9's own failure-mode note says adding a block
/// is a reviewed change, and the order being a static list rather than a configuration table is
/// deliberate (§5.3: "a reorderable card is a table nobody reorders and a bug surface everybody
/// inherits").
/// </summary>
public static class CardBlocks
{
    public const string Header = "item.card.header";
    public const string Requirements = "item.card.requirements";
    public const string BaseStats = "item.card.base-stats";
    public const string Implicit = "item.card.implicit";
    public const string Affixes = "item.card.affixes";
    public const string Enhancement = "item.card.enhancement";
    public const string Sockets = "item.card.sockets";
    public const string Set = "item.card.set";
    public const string GrantedAction = "item.card.granted-action";
    public const string Flavour = "item.card.flavour";
    public const string Footer = "item.card.footer";

    /// <summary>The eleven, in render order. Every card emits all eleven — an empty block is an empty
    /// block, not an absent one, so a consumer never has to ask "was there no set, or did the renderer
    /// forget?".</summary>
    public static readonly IReadOnlyList<string> Order = new[]
    {
        Header, Requirements, BaseStats, Implicit, Affixes, Enhancement,
        Sockets, Set, GrantedAction, Flavour, Footer,
    };

    /// <summary>The four blocks §4.1 marks as never collapsible are actually eight; these two are the
    /// only ones that may. Carried as data so module 20 does not re-derive the table.</summary>
    public static readonly IReadOnlySet<string> MayCollapse =
        new HashSet<string>(StringComparer.Ordinal) { Flavour, Footer };
}

/// <summary>Module 6's base type, as the card reads it. Every field is a KEY or a frame/role id —
/// never the base type's <c>container_id</c>, which §2.4 forbids showing.</summary>
public readonly record struct CardBaseType(
    string NameKey, string ClassNounKey, string Frame, string RoleNameKey, string? FlavourKey);

/// <summary>Module 7's rung, as the card reads it — I1's three redundant channels, all three
/// (pips, the rung name in text, and the colour), never colour alone.</summary>
public readonly record struct CardRarity(string DisplayKey, int PipCount, string ColorHex);

/// <summary>
/// One requirement clause. I11's own shape: the card must name WHICH number gates
/// (<c>Sinew 32 (29 + 3) — 29 gates</c>), so the unassisted half and the granted half are separate
/// fields rather than one total.
/// </summary>
/// <param name="Unassisted">What the specimen brings on its own — never anything an equippable
/// container grants (I11 §2.7's cycle rule, which <c>UnassistedAttributes</c> already enforces).</param>
public readonly record struct CardRequirement(
    string AttributeKey, long Needed, long Unassisted, long Bonus, bool Gates);

/// <summary>One socket cell. <see cref="InsertNameKey"/> is a display key, never the insert's
/// container or family id, and there is deliberately no tier field: §2.4 never renders one.</summary>
public readonly record struct CardSocketCell(
    int Index, string AffinityKey, bool Crafted, string? InsertNameKey, bool OmniCountsDiversityOnly = false)
{
    public bool IsEmpty => InsertNameKey is null;
}

/// <summary>
/// One combination row, joined to its display key. The state, distance and granted tier all come from
/// <see cref="CombinationDistance.Evaluate"/> — module 16's own single evaluator, called once — so the
/// card cannot promise a resonance the evaluator would not fire.
/// </summary>
public readonly record struct CardCombination(
    string NameKey, ComboShape Shape, CombinationDisplayState State, int? Distance,
    int GrantedTier, bool AllAttuned, IReadOnlyList<string> MissingKeys);

/// <summary>One rung of a set's threshold ladder. The WHOLE ladder always renders (§4.3) — an
/// inactive threshold is the goal, and hiding it removes the goal.</summary>
public readonly record struct CardSetTier(int PiecesRequired, bool Active, bool IsCapability);

/// <summary>
/// The set block. <paramref name="Redundant"/> is module 12's disclosure requirement, not a rejection:
/// equipping a second copy in another role stays legal, so the card must show "3 / 4" AND say why the
/// fourth did not count.
/// </summary>
public readonly record struct CardSet(
    string NameKey, int Count, int Total, IReadOnlyList<CardSetTier> Ladder, bool Redundant);

/// <summary>G4's granted action, as §4.1 block 9 names it.</summary>
public readonly record struct CardGrantedAction(
    string NameKey, string DescriptionKey, bool BattleOnly, bool AlreadyKnown);

/// <summary>
/// Everything the eleven blocks read, and nothing else. Each field is produced by an
/// already-shipped read path — named per field — so this is an assembly point, never a second
/// mechanism for reading an item.
/// </summary>
/// <param name="Instance">The rolled instance, exactly as <c>Instantiator.TryInstantiate</c> produced
/// it and <c>RpgStore</c> round-trips it. Its <c>values_json</c> is the authoritative magnitude
/// (Rule 3); nothing here re-rolls or re-curves.</param>
/// <param name="Container">The container the instance was minted from — the fixed core's <c>seq</c>
/// set is what tells a core atom from a drawn one, with no extra column and no second bookkeeping.</param>
/// <param name="LookupTemplate">N1's row for a family. <c>null</c> is
/// <see cref="DisplayRules.MissingDisplayTemplate"/> and throws — never a blank line, never a raw id.</param>
/// <param name="ItemName">Module 8's <c>ItemNameComposer.Compose</c> output, or module 17's authored
/// unique name. <b>Module 10 renders the name; it does not derive it</b>
/// (spec-affix-legality.md's own note), which is also why a reroll leaves the name alone for free.</param>
public sealed record ItemCardInput(
    InstanceRow Instance,
    ContainerRow Container,
    Func<string, AtomRow?> LookupAtom,
    Func<string, DisplayTemplateRow?> LookupTemplate,
    CardBaseType BaseType,
    CardRarity Rarity,
    string ItemName)
{
    public int ItemLevel { get; init; }

    /// <summary>Module 15's mutation ledger value (<c>effect_instance.enhance_level</c>). 0 when never
    /// enhanced, and at 0 the header emits no <c>+N</c> token at all.</summary>
    public int EnhanceLevel { get; init; }

    /// <summary>Module 15's <c>EnhancePolicy.GainMilli</c> at <see cref="EnhanceLevel"/>. Rendered as
    /// ONE block (I6 §5.5's suppress-and-append rule), never as stacked per-level lines.</summary>
    public long EnhanceGainMilli { get; init; }

    // ---- requirements (module 4 `equip-assign`) ----------------------------------------------------

    public int? LevelReq { get; init; }
    public int SpecimenLevel { get; init; }

    /// <summary><c>EquipGate.Explain</c>'s answer. Non-null turns the block red and names the axis.</summary>
    public EquipRefusal? Refusal { get; init; }

    public IReadOnlyList<CardRequirement> Requirements { get; init; } = Array.Empty<CardRequirement>();

    // ---- sockets (module 16) -----------------------------------------------------------------------

    public IReadOnlyList<CardSocketCell> Sockets { get; init; } = Array.Empty<CardSocketCell>();
    public IReadOnlyList<CardCombination> Combinations { get; init; } = Array.Empty<CardCombination>();

    // ---- set (module 12 `threshold-grants`) --------------------------------------------------------

    public CardSet? Set { get; init; }

    // ---- granted actions (G4) ----------------------------------------------------------------------

    public IReadOnlyList<CardGrantedAction> GrantedActions { get; init; } = Array.Empty<CardGrantedAction>();

    // ---- uniques (module 17) -----------------------------------------------------------------------

    /// <summary>Non-null makes the fixed core render as <c>unique-identity</c>/<c>unique-variance</c>
    /// (§4.4's exact rule) and unlocks the flavour block, which is uniques-only.</summary>
    public UniqueRow? Unique { get; init; }

    // ---- footer -------------------------------------------------------------------------------------

    public bool Locked { get; init; }
    public bool Stale { get; init; }
    public bool NoReassign { get; init; }

    /// <summary>Module 19's <c>SalvagePolicy.Yield</c> output. One number reaches the face; the
    /// breakdown is the expandable half.</summary>
    public IReadOnlyList<MaterialCostLine>? SalvageYield { get; init; }

    /// <summary>Module 9's <c>ItemPowerReads.CardPower</c>. Suppressed by its own tuning flag, in which
    /// case no power line is emitted at all — §10 Q7's "ask first" is answered by the tuning, not
    /// here.</summary>
    public CardPowerDisplay? Power { get; init; }

    public DerivedStatRegistry? Registry { get; init; }
}

/// <summary>
/// The <b>Card</b> level of spec-item-card.md's three (Line → Card → Compare): one real rolled
/// instance plus its container, sockets, set, enhancement and requirements, projected onto G3 §4.1's
/// eleven ordered blocks.
///
/// <para>⛔ <b>Every magnitude line goes through <see cref="ItemDisplayRenderer.Line"/>.</b> There is
/// no second path to a line here — that is the rule that stops "the tooltip says X and the list says
/// Y", and it is why this class contains no formatting of its own for any atom's number.</para>
///
/// <para>⛔ <b>§2.4's never-shown list is enforced structurally, not by convention.</b> No atom id,
/// family id, container id, instance id, tier number, name band, group id, or raw per-mille reaches an
/// arg: the group ORDER is an ordinal derived from the template's group id and the id itself is
/// dropped; every name that could have been an id is taken as a display key from the input instead.
/// <c>Nothing_the_spec_forbids_reaches_a_rendered_arg</c> asserts it over a real instance rather than
/// trusting this paragraph.</para>
/// </summary>
public static class ItemCardRenderer
{
    /// <summary>The one place the eleven blocks are assembled. Total over
    /// <see cref="CardBlocks.Order"/> by construction: the result is built by walking that list, so a
    /// block cannot be silently dropped by an early return.</summary>
    public static DisplayModel Render(ItemCardInput input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        var registry = input.Registry ?? DerivedStatRegistry.CreateDefault();
        var lines = Classify(input);
        var groupOrder = GroupOrdinals(input, lines);

        var blocks = new List<DisplayBlock>(CardBlocks.Order.Count);
        foreach (var key in CardBlocks.Order)
        {
            blocks.Add(new DisplayBlock(key, key switch
            {
                CardBlocks.Header => HeaderLines(input),
                CardBlocks.Requirements => RequirementLines(input),
                CardBlocks.BaseStats => AtomLines(input, registry, groupOrder, lines.Base),
                CardBlocks.Implicit => AtomLines(input, registry, groupOrder, lines.Implicit),
                CardBlocks.Affixes => AtomLines(input, registry, groupOrder, lines.Affixes),
                CardBlocks.Enhancement => EnhancementLines(input),
                CardBlocks.Sockets => SocketLines(input),
                CardBlocks.Set => SetLines(input),
                CardBlocks.GrantedAction => GrantedActionLines(input),
                CardBlocks.Flavour => FlavourLines(input),
                CardBlocks.Footer => FooterLines(input, registry, groupOrder, lines),
                _ => throw new InvalidOperationException($"unhandled card block '{key}'"),
            }));
        }

        return new DisplayModel(blocks);
    }

    // ---- which atom belongs to which block -----------------------------------------------------------

    /// <summary>One instance atom, already joined to its catalog row and its authored value spec.</summary>
    readonly record struct Placed(InstanceAtomRow Row, AtomRow Atom, SourceKind Kind);

    readonly record struct Placement(
        IReadOnlyList<Placed> Base, IReadOnlyList<Placed> Implicit, IReadOnlyList<Placed> Affixes);

    /// <summary>
    /// I3 §5.2's split, read off content and nothing else: the container's fixed-core <c>seq</c> set
    /// says which atoms were authored rather than drawn; <c>seq 0</c> is the base stat block and
    /// <c>seq 1</c> is the single implicit. Anything the pool drew is an affix, and its prefix/suffix
    /// side is <c>AffixValidator.AffixClassOfAtom</c>'s own derivation (trigger present ⇒ suffix) —
    /// never a second rule.
    ///
    /// <para>On a UNIQUE the fixed core past <c>seq 1</c> renders in the affix block as
    /// <c>unique-identity</c>, except the one <c>OnInstantiate</c> atom, which is
    /// <c>unique-variance</c> and is the only line on that card with a bar (§4.4).</para>
    /// </summary>
    static Placement Classify(ItemCardInput input)
    {
        var coreSeqs = input.Container.Atoms.Select(a => a.Seq).ToHashSet();
        var baseRows = new List<Placed>();
        var implicitRows = new List<Placed>();
        var affixRows = new List<Placed>();

        foreach (var row in input.Instance.Atoms.OrderBy(a => a.Seq))
        {
            var atom = input.LookupAtom(row.AtomId)
                ?? throw new DisplayTemplateRejection(
                    $"instance references atom '{row.AtomId}', which the catalog does not have");

            if (coreSeqs.Contains(row.Seq))
            {
                if (row.Seq == 0) { baseRows.Add(new Placed(row, atom, SourceKind.Base)); continue; }
                if (row.Seq == 1) { implicitRows.Add(new Placed(row, atom, SourceKind.Implicit)); continue; }

                affixRows.Add(new Placed(row, atom, input.Unique is null
                    ? SourceKind.Base
                    : RollOf(atom) == RollPolicy.OnInstantiate
                        ? SourceKind.UniqueVariance
                        : SourceKind.UniqueIdentity));
                continue;
            }

            affixRows.Add(new Placed(row, atom,
                AffixValidator.AffixClassOfAtom(atom) == AffixClass.Suffix
                    ? SourceKind.AffixSuffix
                    : SourceKind.AffixPrefix));
        }

        return new Placement(baseRows, implicitRows, affixRows);
    }

    /// <summary>
    /// §4.1's affix ordering key: <b>group order</b>, then tier DESC, then seq ASC. The group order is
    /// the ORDINAL of the template's <c>groupId</c> within this card's own sorted group set —
    /// content-derived and ordinal, the tiebreak discipline definitions §5 already forced on the
    /// effect list. The group id itself never reaches an arg (§2.4).
    /// </summary>
    static IReadOnlyDictionary<string, int> GroupOrdinals(ItemCardInput input, Placement placement)
    {
        var groups = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var p in placement.Base.Concat(placement.Implicit).Concat(placement.Affixes))
            if (input.LookupTemplate(p.Atom.FamilyId) is { } t) groups.Add(t.GroupId);

        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        var i = 0;
        foreach (var g in groups) map[g] = i++;
        return map;
    }

    // ---- blocks 3/4/5: the atom lines ------------------------------------------------------------------

    static IReadOnlyList<DisplayLine> AtomLines(
        ItemCardInput input, DerivedStatRegistry registry,
        IReadOnlyDictionary<string, int> groupOrder, IReadOnlyList<Placed> placed)
    {
        var lines = new List<(DisplayLine Line, int Group, int Tier, int Seq, int Side)>(placed.Count);

        foreach (var p in placed)
        {
            var template = input.LookupTemplate(p.Atom.FamilyId)
                ?? throw new DisplayTemplateRejection(
                    $"'{p.Atom.FamilyId}' has no display template row ({DisplayRules.MissingDisplayTemplate}) "
                    + "-- rendering it would put a raw id on a tooltip");

            var group = groupOrder.TryGetValue(template.GroupId, out var g) ? g : int.MaxValue;
            var roll = RollOf(p.Atom);
            var quality = ArmouryCompare.RollQualityMilli(p.Atom, p.Row.ValuesJson);
            var channel = ChannelOf(p.Row.ValuesJson) ?? ChannelOf(p.Atom.ParamsJson);
            var unit = channel is null ? null : ChannelUnits.ForAuthoredChannel(channel, registry);
            var (frozen, bandMax) = Magnitude(p.Row.ValuesJson);

            lines.Add((
                ItemDisplayRenderer.Line(
                    template, p.Atom, input.BaseType.Frame, frozen, p.Kind, group,
                    unit, ElementVariantOf(p.Atom), roll, quality, contextRead: null, bandMax: bandMax),
                group,
                p.Atom.Tier,
                p.Row.Seq,
                // Prefixes then suffixes -- §4.1's first ordering key, before the group.
                p.Kind == SourceKind.AffixSuffix ? 1 : 0));
        }

        return lines
            .OrderBy(x => x.Side)
            .ThenBy(x => x.Group)
            .ThenByDescending(x => x.Tier)
            .ThenBy(x => x.Seq)
            .Select(x => x.Line)
            .ToList();
    }

    // ---- block 1: header ---------------------------------------------------------------------------------

    /// <summary>
    /// One line, because the header is one thing a player reads left to right. §10 Q2's collision is
    /// resolved in the ARG ORDER the key documents — <b>pips → <c>+12</c> → name</b> — because the pips
    /// are the rarity ladder's accessibility channel and must not be displaced by an optional token.
    /// Reversible by moving one entry; the owner's call.
    /// </summary>
    static IReadOnlyList<DisplayLine> HeaderLines(ItemCardInput input)
    {
        var args = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pips"] = input.Rarity.PipCount.ToString(CultureInfo.InvariantCulture),
            ["rungKey"] = input.Rarity.DisplayKey,
            ["colorHex"] = input.Rarity.ColorHex,
            ["name"] = input.ItemName,
            ["baseNameKey"] = input.BaseType.NameKey,
            ["classNounKey"] = input.BaseType.ClassNounKey,
            ["roleNameKey"] = input.BaseType.RoleNameKey,
            ["frame"] = input.BaseType.Frame,
            ["ilvl"] = input.ItemLevel.ToString(CultureInfo.InvariantCulture),
        };

        // Absent at +0 rather than present as "+0": a zero enhancement is not a thing the item has.
        if (input.EnhanceLevel > 0)
            args["enhance"] = "+" + input.EnhanceLevel.ToString(CultureInfo.InvariantCulture);

        // Module 9's read, rendered under Rule P (two significant figures with its band). Suppressed
        // entirely when the tuning says so -- no row, rather than an empty one.
        if (input.Power is { Shown: true } power) args["power"] = power.Render();

        return new[] { new DisplayLine(CardBlocks.Header, args, null, null, 0) };
    }

    // ---- block 2: requirements ---------------------------------------------------------------------------

    static IReadOnlyList<DisplayLine> RequirementLines(ItemCardInput input)
    {
        var lines = new List<DisplayLine>();

        if (input.LevelReq is { } req)
        {
            var met = input.SpecimenLevel >= req;
            lines.Add(new DisplayLine("item.card.requirement.level", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["need"] = req.ToString(CultureInfo.InvariantCulture),
                ["have"] = input.SpecimenLevel.ToString(CultureInfo.InvariantCulture),
                ["met"] = met ? "1" : "0",
            }, UnitClass.Count, null, 0));
        }

        foreach (var r in input.Requirements)
            lines.Add(new DisplayLine("item.card.requirement.attribute", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["attrKey"] = r.AttributeKey,
                ["need"] = r.Needed.ToString(CultureInfo.InvariantCulture),
                ["unassisted"] = r.Unassisted.ToString(CultureInfo.InvariantCulture),
                ["bonus"] = r.Bonus.ToString(CultureInfo.InvariantCulture),
                // I11's own wording: the card names WHICH number gates, not just that something does.
                ["gates"] = r.Gates ? "1" : "0",
            }, UnitClass.Count, null, 0));

        if (input.Refusal is { } refusal)
            lines.Add(new DisplayLine("item.card.requirement.refused", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["reasonKey"] = "item.equip.refusal." + RefusalSlug(refusal.Reason),
                ["remedy"] = refusal.Remedy,
            }, null, null, 0));

        return lines;
    }

    static string RefusalSlug(EquipRefusalReason reason) => reason switch
    {
        EquipRefusalReason.RoleLocked => "role-locked",
        EquipRefusalReason.RoleNotOnFrame => "role-not-on-frame",
        EquipRefusalReason.LevelTooLow => "level-too-low",
        EquipRefusalReason.FactionMismatch => "faction-mismatch",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };

    // ---- block 6: enhancement -----------------------------------------------------------------------------

    /// <summary>I6 §5.5's suppress-and-append rule, taken at its word: ONE line at the current level,
    /// never one per level. Absent entirely at +0.</summary>
    static IReadOnlyList<DisplayLine> EnhancementLines(ItemCardInput input)
    {
        if (input.EnhanceLevel <= 0) return Array.Empty<DisplayLine>();

        return new[]
        {
            new DisplayLine("item.card.enhancement.level", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["level"] = input.EnhanceLevel.ToString(CultureInfo.InvariantCulture),
                // Per-mille NEVER renders as per-mille (§2.4) -- the shared conversion, not a second one.
                ["gain"] = ItemDisplayRenderer.FormatPerMille(checked((int)input.EnhanceGainMilli)),
            }, UnitClass.PerMilleRatio, SourceKind.Enhancement, 0),
        };
    }

    // ---- block 7: sockets ---------------------------------------------------------------------------------

    /// <summary>
    /// Cells first (empty ones shown as empty), then <c>active</c> resonances, then the word, then
    /// <c>one-away</c>. <c>undiscovered</c> is <b>never rendered at all</b> and <c>known-inactive</c>
    /// renders name-only — §4.3's four closed states, applied to
    /// <see cref="CombinationDistance"/>'s own output rather than re-decided here.
    /// </summary>
    static IReadOnlyList<DisplayLine> SocketLines(ItemCardInput input)
    {
        var lines = new List<DisplayLine>();

        foreach (var cell in input.Sockets.OrderBy(c => c.Index))
            lines.Add(new DisplayLine("item.card.socket.cell", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["index"] = cell.Index.ToString(CultureInfo.InvariantCulture),
                ["affinityKey"] = cell.AffinityKey,
                ["crafted"] = cell.Crafted ? "1" : "0",
                ["empty"] = cell.IsEmpty ? "1" : "0",
                ["insertKey"] = cell.InsertNameKey ?? "",
                // §9.4: an omni insert sitting in a three-fire fill that is not firing Pure looks
                // broken unless its own line says why. So the line says why.
                ["omniDiversityOnly"] = cell.OmniCountsDiversityOnly ? "1" : "0",
            }, null, SourceKind.SocketInsert, cell.Index));

        // Active first, then one-away, then known-inactive; within a state, the catalog's own order.
        foreach (var state in new[]
                 {
                     CombinationDisplayState.Active,
                     CombinationDisplayState.OneAway,
                     CombinationDisplayState.KnownInactive,
                 })
            foreach (var combo in input.Combinations.Where(c => c.State == state))
            {
                var args = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["nameKey"] = combo.NameKey,
                    ["state"] = StateSlug(combo.State),
                    ["attuned"] = combo.AllAttuned ? "1" : "0",
                };
                // A one-away line names the EXACT missing ingredient -- "needs something" is not a hint.
                if (combo.State == CombinationDisplayState.OneAway)
                {
                    args["distance"] = (combo.Distance ?? 0).ToString(CultureInfo.InvariantCulture);
                    args["missingKeys"] = string.Join(",", combo.MissingKeys);
                }

                lines.Add(new DisplayLine("item.card.socket.combination", args, null,
                    combo.Shape == ComboShape.Strain || combo.Shape == ComboShape.Splice
                        ? SourceKind.Word
                        : SourceKind.Resonance,
                    0));
            }

        return lines;
    }

    static string StateSlug(CombinationDisplayState state) => state switch
    {
        CombinationDisplayState.Active => "active",
        CombinationDisplayState.OneAway => "one-away",
        CombinationDisplayState.KnownInactive => "known-inactive",
        CombinationDisplayState.Undiscovered => throw new InvalidOperationException(
            "an undiscovered combination is never rendered at all (ssot-presentation.md §4.3)"),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    // ---- block 8: set --------------------------------------------------------------------------------------

    /// <summary>
    /// The whole ladder, always. A set has at most four thresholds, and an inactive one is the GOAL —
    /// hiding it removes the goal. The wording rule that prevents a real lie is enforced by the shape:
    /// a tier line carries its <c>pieces</c> count and no "next" flag, so a screenshot taken an hour ago
    /// is still true.
    /// </summary>
    static IReadOnlyList<DisplayLine> SetLines(ItemCardInput input)
    {
        if (input.Set is not { } set) return Array.Empty<DisplayLine>();

        var lines = new List<DisplayLine>
        {
            new("item.card.set.header", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["nameKey"] = set.NameKey,
                ["count"] = set.Count.ToString(CultureInfo.InvariantCulture),
                ["total"] = set.Total.ToString(CultureInfo.InvariantCulture),
                // Module 12's disclosure requirement: legal, uncounted, and said out loud.
                ["redundant"] = set.Redundant ? "1" : "0",
            }, UnitClass.Count, SourceKind.SetThreshold, 0),
        };

        foreach (var tier in set.Ladder.OrderBy(t => t.PiecesRequired))
            lines.Add(new DisplayLine("item.card.set.threshold", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["pieces"] = tier.PiecesRequired.ToString(CultureInfo.InvariantCulture),
                ["active"] = tier.Active ? "1" : "0",
                ["capability"] = tier.IsCapability ? "1" : "0",
            }, UnitClass.Count, SourceKind.SetThreshold, tier.PiecesRequired));

        return lines;
    }

    // ---- block 9: granted action ------------------------------------------------------------------------------

    static IReadOnlyList<DisplayLine> GrantedActionLines(ItemCardInput input) =>
        input.GrantedActions.Select(a => new DisplayLine(
            "item.card.granted-action", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["nameKey"] = a.NameKey,
                ["descriptionKey"] = a.DescriptionKey,
                ["battleOnly"] = a.BattleOnly ? "1" : "0",
                ["alreadyKnown"] = a.AlreadyKnown ? "1" : "0",
            }, null, SourceKind.GrantedAction, 0)).ToList();

    // ---- block 10: flavour -------------------------------------------------------------------------------------

    /// <summary>Uniques only (§4.1). A base type's own <c>flavorKey</c> is authored and real, but the
    /// card does not carry it: G1's <c>flavour_key</c> is the unique's identity line, and rendering
    /// every item's flavour would make block 10 the tallest thing on a common drop.</summary>
    static IReadOnlyList<DisplayLine> FlavourLines(ItemCardInput input)
    {
        if (input.Unique?.FlavourKey is not { Length: > 0 } key) return Array.Empty<DisplayLine>();

        return new[]
        {
            new DisplayLine("item.card.flavour", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["flavourKey"] = key,
            }, null, SourceKind.UniqueIdentity, 0),
        };
    }

    // ---- block 11: footer ---------------------------------------------------------------------------------------

    /// <summary>
    /// The mean roll quality is COMPUTED from the same per-atom read the bars above it use
    /// (<c>ArmouryCompare.RollQualityMilli</c>), never passed in — a footer that could disagree with
    /// the bars on the same card is the two-implementations defect in miniature.
    /// </summary>
    static IReadOnlyList<DisplayLine> FooterLines(
        ItemCardInput input, DerivedStatRegistry registry,
        IReadOnlyDictionary<string, int> groupOrder, Placement placement)
    {
        var rolled = placement.Affixes
            .Where(p => RollOf(p.Atom) == RollPolicy.OnInstantiate)
            .Select(p => ArmouryCompare.RollQualityMilli(p.Atom, p.Row.ValuesJson))
            .ToList();

        var args = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["meanRollQuality"] = rolled.Count == 0
                ? ""
                // Integer mean, rounded away from zero exactly once, through the engine's own divide.
                : ItemDisplayRenderer.FormatPerMille(
                    checked((int)ItemDisplayRenderer.RoundAwayFromZero(rolled.Sum(r => (long)r), rolled.Count))),
            ["stale"] = input.Stale ? "1" : "0",
            ["locked"] = input.Locked ? "1" : "0",
            ["noReassign"] = input.NoReassign ? "1" : "0",
        };

        if (input.SalvageYield is { Count: > 0 } yield)
        {
            long total = 0;
            foreach (var line in yield) total = checked(total + line.Qty);
            args["salvageUnits"] = total.ToString(CultureInfo.InvariantCulture);
            args["salvageLines"] = yield.Count.ToString(CultureInfo.InvariantCulture);
        }

        return new[] { new DisplayLine(CardBlocks.Footer, args, null, null, 0) };
    }

    // ---- reads off the frozen instance / the authored atom ----------------------------------------------------------

    /// <summary>The atom's own authored roll policy, read from its <c>params.amount</c> value spec —
    /// the same reader <c>Instantiator.Freeze</c> used to freeze it, so the bar rule and the freeze rule
    /// can never disagree about what rolled.</summary>
    static RollPolicy RollOf(AtomRow atom)
    {
        if (!TryAmount(atom.ParamsJson, out var raw)) return RollPolicy.Fixed;
        return AtomJson.TryReadValueSpec(raw, out var spec).IsOk ? spec.Roll : RollPolicy.Fixed;
    }

    /// <summary>
    /// What the line has to show: a frozen integer, or an <c>OnApply</c> BAND.
    ///
    /// <para><c>Instantiator.Freeze</c> writes a plain number for a <c>Fixed</c> or
    /// <c>OnInstantiate</c> spec and deliberately copies an <c>OnApply</c> spec through <b>as
    /// authored</b> — the hit rolls it, not the item — so an <c>OnApply</c> row's <c>values_json</c>
    /// still carries <c>{min, max, roll}</c>. That is not a missing magnitude: it is the band §3.4
    /// requires the card to show instead of a value, and it is what the shipped corpus authors for
    /// every generated affix (<c>ssot-affixes.md:422</c>). Returning both bounds is how the renderer
    /// tells the two apart without a second parse.</para>
    /// </summary>
    static (long? Value, long? BandMax) Magnitude(string valuesJson)
    {
        if (!TryAmount(valuesJson, out var raw)) return (null, null);
        if (raw.ValueKind == JsonValueKind.Number) return (raw.GetInt64(), null);
        if (raw.ValueKind != JsonValueKind.Object) return (null, null);

        return AtomJson.TryReadValueSpec(raw, out var spec).IsOk
            ? (spec.Min, spec.Max)
            : (null, null);
    }

    static bool TryAmount(string? json, out JsonElement amount)
    {
        amount = default;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json!);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            if (!doc.RootElement.TryGetProperty("amount", out var a)) return false;
            amount = a.Clone();
            return true;
        }
        catch (JsonException) { return false; }
    }

    static string? ChannelOf(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json!);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            return doc.RootElement.TryGetProperty("channel", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString()
                : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>An element-typed atom's variant, for a template that names <c>{element}</c>. Empty is
    /// <c>null</c>: <c>AtomRow.Variant</c> is "" and never NULL by design, and passing "" would render
    /// an empty word into the sentence.</summary>
    static string? ElementVariantOf(AtomRow atom) =>
        atom.Variant.Length == 0 ? null : atom.Variant;
}
