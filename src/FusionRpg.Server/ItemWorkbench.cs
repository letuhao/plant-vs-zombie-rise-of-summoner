using System.Globalization;
using System.Text;
using System.Text.Json;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Materials;
using FusionRpg.Core.Items.Mutation;
using FusionRpg.Core.Items.Sockets;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Data;

namespace FusionRpg.Server;

/// <summary>One resolved cost or yield line, as a caller renders it.</summary>
public sealed record WorkbenchCostDto(string Class, string MaterialId, long Qty);

/// <summary>One socket after the operation.</summary>
public sealed record WorkbenchSocketDto(int Index, string Affinity, bool Crafted, string? Insert);

/// <summary>
/// What one workbench operation did, in the shape a surface can draw without a second read: what was
/// spent, what was returned, what the item's persisted state now is, and — when the answer is no —
/// which named rule refused it.
/// </summary>
public sealed record WorkbenchOutcomeDto(
    bool Ok,
    string Verb,
    string Reason,
    string InstanceId,
    string RecipeId,
    int OpSeq,
    bool Replayed,
    string Outcome,
    int EnhanceLevel,
    int PityCounter,
    int SuccessMilli,
    IReadOnlyList<WorkbenchCostDto> Spent,
    IReadOnlyList<WorkbenchCostDto> Granted,
    IReadOnlyList<WorkbenchSocketDto> Sockets);

/// <summary>
/// ⭐ <b>The workbench executor</b> — the production caller item modules 14, 15 and 16 each named as
/// their one shared blocker.
///
/// <para>All three shipped a real, tested half of one loop and none shipped the joint:
/// <c>MaterialRecipeCatalog</c>/<c>SalvagePolicy</c> price and convert but nothing debits;
/// <c>EnhancePolicy</c>/<c>RerollPolicy</c>/<c>TransferPolicy</c> decide but nothing records;
/// <c>SocketOperations</c> transitions but nothing writes. <c>TrySpendRecipe</c>,
/// <c>AppendMutationOp</c> and <c>SetSockets</c> all had zero production callers. This class calls
/// them, against a real stored item, and <see cref="RpgStore.TrySpendAndApply"/> commits the debit and
/// the write together.</para>
///
/// <para><b>Shape: ONE executor, per-verb methods.</b> That is what the three specs describe rather
/// than a choice made here. <c>spec-salvage-craft.md</c> §"The spend transaction" is a single
/// six-step transaction (<i>replay → resolve → gate → spend → <b>perform</b> → log</i>) whose step 5
/// is explicitly <i>"the owning module's mutation or mint"</i> — one pattern, a pluggable body per
/// verb. Its SC7 line says the same from the other end: <i>"adding an operation verb is code, because
/// a verb needs <b>an executor</b> and a module that owns it."</i> Parallel per-verb executors would
/// have to re-derive debit-then-act-then-persist three times, and the day the two copies disagreed
/// one of them would be spending without recording.</para>
///
/// <para>⛔ <b>The gate order is the same for every verb</b>, so a refusal costs the same nothing
/// whichever verb asked: resolve the target and its ownership → let Core decide → resolve the price →
/// spend and apply atomically. Nothing is debited before Core has said yes.</para>
///
/// <para>⚠ <b>D26 holds through this class.</b> Every cost input handed to
/// <see cref="MaterialRecipeCatalog.Resolve"/> is read off the TARGET — its rung, its item level, its
/// frame, its own <c>+n</c>. The player id reaches the store only as a balance to debit and an
/// ownership check; it never reaches a price.</para>
/// </summary>
public sealed class ItemWorkbench
{
    /// <summary>
    /// A material has no rarity rung, and <c>materials.v1.json</c> gives <c>upcycle</c> no rung leg —
    /// proven by <c>Upcycle_cost_is_invariant_across_every_rung_index</c> rather than assumed. Index 0
    /// is the ladder's own first slot, used because <see cref="RecipeContext"/> requires a value in
    /// range; it is <b>not</b> a claim that upcycling is a <c>chaff</c> operation.
    /// </summary>
    const int MaterialHasNoRung = 0;

    /// <summary>Neither the item lane nor <c>enhancement.v1.json</c> declares a rules version today, so
    /// D2 clause 5's column ships stamped with this until one exists. Structural placeholder, not a
    /// balance number.</summary>
    const int ItemRulesVersionUnset = 0;

    readonly RpgStore _store;
    readonly MaterialTuning _materials;
    readonly MaterialRecipeCatalog _recipes;
    readonly EnhancementTuning _enhancement;
    readonly SocketTuning _sockets;
    readonly Func<string, int?>? _baseTypeSocketMax;
    readonly Func<string, CardInsertLookup?>? _lookupInsert;

    /// <param name="baseTypeSocketMax">
    /// The base type's own declared <c>socketMax</c>, by base-type id. ⚠ <b>Module 6 shipped the
    /// 740-entry corpus but no <c>item_base_type</c> table</b>, so this arrives as a delegate — the
    /// same seam <c>LootContentView.SocketMaxFor</c> already uses for step 10. <c>null</c> refuses
    /// every <c>socket-add</c> by name rather than guessing a ceiling, which is
    /// <c>LootPipeline.Sockets</c>'s own stated rule: <i>"half a socket rule would grant the wrong
    /// count, which is worse than granting none."</i>
    /// </param>
    /// <param name="lookupInsert">
    /// ⭐ <b>The gem catalog, by container id</b> — the same <see cref="GemInsertCorpus"/> delegate
    /// <see cref="ItemCardEndpoints"/> and <see cref="ItemSurfaceEndpoints"/> read. <c>socket-insert</c>
    /// used to describe the insert by its container id alone with a hardcoded <c>Element: ""</c>;
    /// the corpus carries the real element (<c>data/seed/items/gems/*.json</c>), so it is read rather
    /// than assumed. A container the corpus does not carry keeps <c>""</c> — the honest "no element",
    /// and also what a genuinely element-free insert authors (<c>SocketModel.cs:72</c>).
    /// </param>
    public ItemWorkbench(
        RpgStore store,
        MaterialTuning materials,
        MaterialRecipeCatalog recipes,
        EnhancementTuning enhancement,
        SocketTuning sockets,
        Func<string, int?>? baseTypeSocketMax = null,
        Func<string, CardInsertLookup?>? lookupInsert = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _materials = materials ?? throw new ArgumentNullException(nameof(materials));
        _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        _enhancement = enhancement ?? throw new ArgumentNullException(nameof(enhancement));
        _sockets = sockets ?? throw new ArgumentNullException(nameof(sockets));
        _baseTypeSocketMax = baseTypeSocketMax;
        _lookupInsert = lookupInsert;
    }

    // ---- module 14 `salvage-craft` -----------------------------------------------------------------

    /// <summary>
    /// <b>salvage</b> — the converter, committed. An item goes in; materials come out and the item's
    /// disposition becomes <c>salvaged</c>, in one transaction.
    ///
    /// <para>No debit and no recipe: <c>SalvagePolicy.Yield</c> is a credit, and R1's rung−1 rule and
    /// the "no souls, ever" rule are its, not this method's. Idempotency is the disposition itself —
    /// see <see cref="RpgStore.TrySalvageItem"/>.</para>
    /// </summary>
    public WorkbenchOutcomeDto Salvage(long playerId, string instanceId)
    {
        if (!TryResolve(playerId, instanceId, out var target, out var refusal))
            return Refused("salvage", instanceId, "", refusal);

        var yield = SalvagePolicy.Yield(
            new SalvageInput(
                target.RungIndex, target.Generation.ItemLevel, target.Generation.Frame,
                target.DrawnAffixCount, target.ElementalAffixCounts, target.Head.EnhanceLevel),
            _materials);

        var now = DateTime.UtcNow.ToString("O");
        var salvaged = _store.TrySalvageItem(
            playerId, PlayerKey(playerId), instanceId, yield, now, Guid.NewGuid().ToString("N"));

        return salvaged.Ok
            ? new WorkbenchOutcomeDto(true, "salvage", "", instanceId, "", 0, false, "salvaged",
                target.Head.EnhanceLevel, target.Head.PityCounter, 0,
                Array.Empty<WorkbenchCostDto>(), Lines(salvaged.Granted), Array.Empty<WorkbenchSocketDto>())
            : Refused("salvage", instanceId, "", salvaged.Reason);
    }

    /// <summary>
    /// <b>upcycle</b> — module 14's material-to-material verb, and the one operation in this class with
    /// no item instance at all: five of grade <c>g</c> become one of grade <c>g+1</c>. It is here
    /// because it is the shortest complete debit→act→persist cycle the corpus can already run, and
    /// because <c>forge</c> — module 14's other owned verb — <b>cannot</b> run: its recipes name
    /// <c>item.*</c> containers and no module has authored an <c>effect_container</c> for a base type
    /// (module 14's own P4.1 note, unchanged).
    /// </summary>
    public WorkbenchOutcomeDto Upcycle(long playerId, string recipeId, string correlationId)
    {
        if (Replay(playerId, correlationId, "upcycle", instanceId: null) is { } replayed) return replayed;

        if (!TryRecipe(recipeId, CraftOperation.Upcycle, out var recipe, out var recipeRefusal))
            return Refused("upcycle", "", recipeId, recipeRefusal);

        if (recipe.OutputKind != "material" || recipe.OutputRef is not { Length: > 0 } outputMaterial)
            return Refused("upcycle", "", recipeId,
                $"recipe '{recipeId}' has no material output — an upcycle that mints nothing is a spend with no product");

        var ctx = new RecipeContext(MaterialHasNoRung, IlvlTierLadder.MinTier, 0, recipe.Frame, 0);
        var lines = _recipes.Resolve(recipeId, ctx);

        var applied = _store.TrySpendAndApply(
            playerId, recipeId, lines, correlationId,
            grants: new[] { new WorkbenchGrant(outputMaterial, recipe.OutputQty) },
            playerKey: PlayerKey(playerId));

        return applied.Ok
            ? new WorkbenchOutcomeDto(true, "upcycle", applied.Reason, "", recipeId, 0, applied.Replayed,
                "upcycled", 0, 0, 0, Lines(lines),
                new[] { new WorkbenchCostDto(nameof(MaterialClass.Substrate), outputMaterial, recipe.OutputQty) },
                Array.Empty<WorkbenchSocketDto>())
            : Refused("upcycle", "", recipeId, applied.Reason);
    }

    // ---- module 15 `enhance-reroll` ----------------------------------------------------------------

    /// <summary>
    /// <b>enhance</b> — <c>+n → +n+1</c> through <see cref="EnhancePolicy.Resolve"/>, priced by module
    /// 14's <c>temper</c> rows, recorded by <c>AppendMutationOp</c>.
    ///
    /// <para><b>A failed attempt still spends</b> (spec §4: <i>"materials spent, level unchanged, pity
    /// counter +1"</i>), so both outcomes take the same path and both append an op — the failure is a
    /// result, not an error, and D2 clause 4 records it as one.</para>
    ///
    /// <para>⚠ <b><c>MutationResult.Values</c> is deliberately empty here, and that is a finding rather
    /// than a shortcut.</b> D2 clause 1 says the head's <c>values_json</c> is the SSOT and enhancement
    /// rewrites in place — but <c>AppendMutationOp</c> never applies <c>Result.Values</c> to
    /// <c>effect_instance_atom</c>, and the shipped card composes the gain from the persisted
    /// <c>enhance_level</c> over the rung's <c>enhance_cap</c> instead
    /// (<c>RpgStore.ItemCard.cs:256-261</c>). Writing values here as well would double-count the gain
    /// on every surface that reads the card. The two models are reconciled by the level being the one
    /// stored fact; the divergence is recorded in tasks/item-todo.md rather than papered over.</para>
    /// </summary>
    public WorkbenchOutcomeDto Enhance(
        long playerId, string instanceId, string recipeId, string correlationId, bool wardLoaded = false)
    {
        if (Replay(playerId, correlationId, "enhance", instanceId) is { } replayed) return replayed;

        if (!TryResolve(playerId, instanceId, out var target, out var refusal))
            return Refused("enhance", instanceId, recipeId, refusal);

        if (!TryRecipe(recipeId, CraftOperation.Temper, out _, out var recipeRefusal))
            return Refused("enhance", instanceId, recipeId, recipeRefusal);

        var ctx = new EnhanceContext(
            target.RungIndex, target.Generation.ItemLevel, target.Head.EnhanceLevel,
            target.Head.PityCounter, wardLoaded);

        // The op's own named stream, seeded from (instance, correlation) so a retry decides the same
        // thing — and domain-separated per op kind, so adding a roll to reroll never shifts this one.
        var rng = SeededRng.DeriveStream(
            unchecked((ulong)RpgStore.DeriveOpSeed(instanceId, correlationId)),
            MutationOpKinds.StreamName(MutationOpKind.Enhance));

        var attempt = EnhancePolicy.Resolve(ctx, rng, _enhancement, out var policyRefusal);
        if (!policyRefusal.IsOk)
            return Refused("enhance", instanceId, recipeId, policyRefusal.ToString());

        var lines = _recipes.Resolve(recipeId, RecipeContextFor(target));
        var levelAfter = attempt.LevelAfter;
        var head = HeadOf(target.Instance, levelAfter);

        var mutation = new WorkbenchMutation(
            instanceId, MutationOpKind.Enhance,
            new MutationResult(OutcomeId(attempt.Outcome), levelAfter - target.Head.EnhanceLevel,
                Array.Empty<AtomValueSet>(), Array.Empty<int>(), Array.Empty<AtomAppend>()),
            MutationCanonical.StateHash(head),
            target.Head.OriginValuesJson ?? OriginValuesJson(target.Instance),
            DateTime.UtcNow.ToString("O"),
            target.Instance.CatalogRevision, ItemRulesVersionUnset,
            PityCounter: attempt.PityCounterAfter);

        var applied = _store.TrySpendAndApply(
            playerId, recipeId, lines, correlationId, mutation, playerKey: PlayerKey(playerId));

        return applied.Ok
            ? new WorkbenchOutcomeDto(true, "enhance", applied.Reason, instanceId, recipeId,
                applied.OpSeq, applied.Replayed, OutcomeId(attempt.Outcome),
                levelAfter, attempt.PityCounterAfter, attempt.SuccessMilli,
                Lines(lines), Array.Empty<WorkbenchCostDto>(), Sockets(instanceId))
            : Refused("enhance", instanceId, recipeId, applied.Reason);
    }

    // ---- module 16 `sockets` -----------------------------------------------------------------------

    /// <summary><b>socket-add</b> (module 14's <c>bore</c> price) — open one empty, crafted socket.</summary>
    public WorkbenchOutcomeDto SocketAdd(long playerId, string instanceId, string recipeId, string correlationId)
    {
        if (Replay(playerId, correlationId, "socket-add", instanceId) is { } replayed) return replayed;

        if (!TryResolve(playerId, instanceId, out var target, out var refusal))
            return Refused("socket-add", instanceId, recipeId, refusal);

        if (!TryRecipe(recipeId, CraftOperation.Bore, out _, out var recipeRefusal))
            return Refused("socket-add", instanceId, recipeId, recipeRefusal);

        if (_baseTypeSocketMax?.Invoke(target.Generation.BaseTypeId) is not { } entrySocketMax)
            return Refused("socket-add", instanceId, recipeId,
                $"ContentRuleViolated{{socket.base-type-socket-max-unavailable}}: no socketMax for base type " +
                $"'{target.Generation.BaseTypeId}' — module 6 shipped the corpus but no item_base_type table, and " +
                "half a socket rule would grant the wrong count rather than none");

        var current = _store.GetSockets(instanceId);
        var rejection = SocketOperations.TryAdd(current, entrySocketMax, out var next);
        if (!rejection.IsOk) return Refused("socket-add", instanceId, recipeId, rejection.ToString());

        return ApplySocketWrite(playerId, target, recipeId, correlationId, "socket-add",
            MutationOpKind.SocketAdd, next, stock: null);
    }

    /// <summary>
    /// <b>socket-insert</b> (module 14's flat-ten-souls <c>socket</c> price) — put an insert the player
    /// already holds into an open socket, and take it out of stock in the same transaction.
    ///
    /// <para>⭐ <b>The element is real as of 2026-09-06</b> — it comes from module 16's shipped gem
    /// corpus through <c>lookupInsert</c>, the same delegate the card and surface routes read, rather
    /// than the hardcoded <c>""</c> this method used to pass. <c>""</c> survives only as the fallback
    /// for a container the corpus does not carry, which is also what a genuinely element-free insert
    /// authors.</para>
    ///
    /// <para>⚠ Still approximate in one respect: <c>ContainerKind.Gem</c> has not landed (X7), so no
    /// <c>gem.*</c> container row and no insert <i>instance</i> exist — the insert is identified by
    /// its catalog id, and the day X7 lands it gets its own bound instance. Tier is likewise
    /// <see cref="GemInsertCorpus.UnauthoredInsertTier"/> because <c>gems/*.json</c> authors none.</para>
    /// </summary>
    public WorkbenchOutcomeDto SocketInsert(
        long playerId, string instanceId, string recipeId, string insertContainerId, int? socketIndex,
        string correlationId)
    {
        if (Replay(playerId, correlationId, "socket-insert", instanceId) is { } replayed) return replayed;

        if (!TryResolve(playerId, instanceId, out var target, out var refusal))
            return Refused("socket-insert", instanceId, recipeId, refusal);

        if (!TryRecipe(recipeId, CraftOperation.Socket, out _, out var recipeRefusal))
            return Refused("socket-insert", instanceId, recipeId, recipeRefusal);

        var held = _store.ListStock(PlayerKey(playerId))
            .FirstOrDefault(s => string.Equals(s.ContainerId, insertContainerId, StringComparison.Ordinal));
        if (held is null || held.Qty <= 0)
            return Refused("socket-insert", instanceId, recipeId,
                $"ContentRuleViolated{{socket.insert-not-held}}: '{insertContainerId}' is not in this player's stock");

        var insert = _lookupInsert?.Invoke(insertContainerId)?.Def
                     ?? new InsertDef(insertContainerId, insertContainerId, Element: "",
                         Tier: GemInsertCorpus.UnauthoredInsertTier);
        var current = _store.GetSockets(instanceId);
        var rejection = SocketOperations.TryInsert(current, socketIndex, insert, "", out var next);
        if (!rejection.IsOk) return Refused("socket-insert", instanceId, recipeId, rejection.ToString());

        return ApplySocketWrite(playerId, target, recipeId, correlationId, "socket-insert",
            MutationOpKind.SocketInsert, next,
            stock: new[] { new WorkbenchStockDelta(insertContainerId, -1) });
    }

    /// <summary>
    /// <b>socket-imbue</b> (D24, module 14's <c>imbue</c> price) — declare a crafted, empty socket's
    /// element affinity.
    ///
    /// <para>⏸ <b>Reachable but not yet payable:</b> the reference cost table prices <c>imbue</c> on
    /// <c>bore</c>'s curve and the check runs at boot, but <b>no recipe row authors the verb</b>
    /// (module 14's own deferred corpus item). Until one exists this refuses with
    /// <c>material.recipe-unknown</c> — the verb is wired, the content is not, and the distinction is
    /// worth keeping visible.</para>
    /// </summary>
    public WorkbenchOutcomeDto SocketImbue(
        long playerId, string instanceId, string recipeId, int socketIndex, string element, string correlationId)
    {
        if (Replay(playerId, correlationId, "socket-imbue", instanceId) is { } replayed) return replayed;

        if (!TryResolve(playerId, instanceId, out var target, out var refusal))
            return Refused("socket-imbue", instanceId, recipeId, refusal);

        if (!TryRecipe(recipeId, CraftOperation.Imbue, out _, out var recipeRefusal))
            return Refused("socket-imbue", instanceId, recipeId, recipeRefusal);

        var current = _store.GetSockets(instanceId);
        var rejection = SocketOperations.TryImbue(current, socketIndex, element, out var next);
        if (!rejection.IsOk) return Refused("socket-imbue", instanceId, recipeId, rejection.ToString());

        return ApplySocketWrite(playerId, target, recipeId, correlationId, "socket-imbue",
            MutationOpKind.SocketImbue, next, stock: null);
    }

    /// <summary>The one socket persistence path, shared by all three socket verbs — <c>item_socket</c>
    /// is the SSOT and the <c>socket-*</c> op is the audit receipt beside it (D2 clause 13), and both
    /// commit with the debit.</summary>
    WorkbenchOutcomeDto ApplySocketWrite(
        long playerId, WorkbenchTarget target, string recipeId, string correlationId, string verb,
        MutationOpKind kind, IReadOnlyList<SocketSlot> next, IReadOnlyList<WorkbenchStockDelta>? stock)
    {
        var lines = _recipes.Resolve(recipeId, RecipeContextFor(target));

        // Sockets never touch the host's atoms, so the host's state hash is unchanged by construction
        // — carried forward rather than recomputed, which is the claim `socketing_never_writes_a_host
        // _atom_row` already makes at the Core level.
        var mutation = new WorkbenchMutation(
            target.Item.InstanceId, kind, MutationResult.Nothing(verb),
            target.Head.StateHash, target.Head.OriginValuesJson,
            DateTime.UtcNow.ToString("O"),
            target.Instance.CatalogRevision, ItemRulesVersionUnset,
            Sockets: next);

        var applied = _store.TrySpendAndApply(
            playerId, recipeId, lines, correlationId, mutation, stock: stock, playerKey: PlayerKey(playerId));

        return applied.Ok
            ? new WorkbenchOutcomeDto(true, verb, applied.Reason, target.Item.InstanceId, recipeId,
                applied.OpSeq, applied.Replayed, verb, target.Head.EnhanceLevel, target.Head.PityCounter, 0,
                Lines(lines), Array.Empty<WorkbenchCostDto>(), Sockets(target.Item.InstanceId))
            : Refused(verb, target.Item.InstanceId, recipeId, applied.Reason);
    }

    // ---- replay ------------------------------------------------------------------------------------

    /// <summary>
    /// ⭐ <b>D2 §9 clause 8, and it has to run BEFORE the price is re-derived.</b> Found by the retry
    /// test, not by reading: a successful operation moves the very state its price is derived from —
    /// <c>temper</c> costs <c>15 × (n+1)</c>, so re-pricing a retried enhance against the item's new
    /// <c>+n</c> resolves a different cost, and <see cref="RpgStore.TrySpendRecipe"/> then correctly
    /// refuses it as <c>correlation.mismatch</c>. The mismatch rule is right and stays; what was wrong
    /// was asking it a question about a cost the caller had already been charged a different one for.
    ///
    /// <para>So a correlation that already has a spend-log row short-circuits here and returns the
    /// <b>recorded</b> cost and the item's <b>current persisted</b> state — nothing is re-decided, nothing
    /// is re-priced and nothing is re-rolled, which is the same discipline clause 4 puts on replay.</para>
    /// </summary>
    WorkbenchOutcomeDto? Replay(long playerId, string correlationId, string verb, string? instanceId)
    {
        if (_store.FindMaterialSpend(playerId, correlationId) is not { } prior) return null;

        var op = instanceId is { Length: > 0 }
            ? _store.ReadMutationOps(instanceId)
                .FirstOrDefault(o => string.Equals(o.CorrelationId, correlationId, StringComparison.Ordinal))
            : null;
        var head = instanceId is { Length: > 0 } ? _store.GetInstanceMutationHead(instanceId) : null;

        return new WorkbenchOutcomeDto(
            true, verb, "replay", instanceId ?? "", prior.RecipeId,
            op?.Seq ?? 0, Replayed: true, op?.Result.Outcome ?? "replay",
            head?.EnhanceLevel ?? 0, head?.PityCounter ?? 0, 0,
            ParseCostJson(prior.CostJson), Array.Empty<WorkbenchCostDto>(),
            instanceId is { Length: > 0 } ? Sockets(instanceId) : Array.Empty<WorkbenchSocketDto>());
    }

    /// <summary>Read back <c>rpg_material_spend_log.cost_json</c> — what the operation actually paid,
    /// rather than what it would cost to run again now.</summary>
    static IReadOnlyList<WorkbenchCostDto> ParseCostJson(string costJson)
    {
        var lines = new List<WorkbenchCostDto>();
        try
        {
            using var doc = JsonDocument.Parse(costJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return lines;
            foreach (var line in doc.RootElement.EnumerateArray())
                lines.Add(new WorkbenchCostDto(
                    line.GetProperty("class").GetString() ?? "",
                    line.GetProperty("id").GetString() ?? "",
                    line.GetProperty("qty").GetInt64()));
        }
        catch (JsonException)
        {
            // A log row we cannot parse is still a real spend — reporting no lines is honest, and
            // inventing them from today's tuning would be the re-pricing this method exists to avoid.
        }

        return lines;
    }

    // ---- target resolution -------------------------------------------------------------------------

    /// <summary>Everything a workbench verb reads off one stored item, resolved once.</summary>
    sealed record WorkbenchTarget(
        RpgItemRow Item, InstanceRow Instance, ContainerRow Container, ItemGenerationRow Generation,
        InstanceMutationHead Head, int RungIndex, int DrawnAffixCount,
        IReadOnlyDictionary<string, int> ElementalAffixCounts);

    bool TryResolve(long playerId, string instanceId, out WorkbenchTarget target, out string reason)
    {
        target = null!;

        var item = _store.GetItem(instanceId);
        if (item is null) { reason = $"item.unknown: no owned item '{instanceId}'"; return false; }

        if (!string.Equals(item.PlayerId, PlayerKey(playerId), StringComparison.Ordinal))
        {
            reason = $"item.not-owned: '{instanceId}' belongs to player '{item.PlayerId}'";
            return false;
        }

        // `RpgItemRow.Locked`'s own contract: "refuse salvage/transfer while true. Never enforced by
        // this row alone — callers that spend an item check it." This is that caller.
        if (item.Locked) { reason = $"item.locked: '{instanceId}' is locked by its owner"; return false; }
        if (!string.Equals(item.Disposition, "owned", StringComparison.Ordinal))
        {
            reason = $"item.not-owned: '{instanceId}' is '{item.Disposition}'";
            return false;
        }

        var instance = _store.GetInstance(instanceId);
        if (instance is null) { reason = $"item.instance-missing: no effect_instance '{instanceId}'"; return false; }

        var container = _store.GetContainer(instance.ContainerId);
        if (container is null)
        {
            reason = $"item.container-missing: '{instance.ContainerId}' is not in the catalog";
            return false;
        }

        var generation = _store.GetItemGeneration(instanceId);
        if (generation is null)
        {
            // Every cost input is a property of the target (D26), and two of them — item level and
            // frame — live only here. Guessing either would price the item wrong in silence.
            reason = $"item.generation-missing: '{instanceId}' has no item_generation stamp, so its item " +
                     "level and frame are unknown and no cost can be priced against it";
            return false;
        }

        var rungIndex = container.Rarity is { Length: > 0 } rarity
            ? RarityLadder.RungIds.ToList().IndexOf(rarity)
            : -1;
        if (rungIndex < 0)
        {
            reason = $"item.rarity-unknown: container '{container.ContainerId}' carries rarity " +
                     $"'{container.Rarity}', which is not one of the ten rungs";
            return false;
        }

        var head = _store.GetInstanceMutationHead(instanceId)
                   ?? new InstanceMutationHead(instanceId, 0, 0, 0, null, null);

        var (drawn, elemental) = Affixes(instance, container);

        target = new WorkbenchTarget(item, instance, container, generation, head, rungIndex, drawn, elemental);
        reason = "";
        return true;
    }

    /// <summary>
    /// The cost context, all five fields read off the TARGET (D26). <c>TargetTier</c> comes from the
    /// item's own level through D29's ladder rather than from a stored column, because no shipped
    /// recipe authors a <c>qty_curve_id</c> and the tier axis therefore has no priced leg yet —
    /// module 14's own carried gap, unchanged here.
    /// </summary>
    static RecipeContext RecipeContextFor(WorkbenchTarget t) => new(
        t.RungIndex, IlvlTierLadder.MaxTierAt(t.Generation.ItemLevel), t.Generation.ItemLevel,
        t.Generation.Frame, t.Head.EnhanceLevel);

    /// <summary>
    /// The instance's drawn affixes and their elements. <b>Drawn</b> is "not in the container's fixed
    /// core": <c>Instantiator</c> freezes the core and the pool draw into one atom list with no flag
    /// separating them, so the container's own <c>Atoms</c> list is the only thing that can tell them
    /// apart. The element comes from the atom row's variant, which is where a concrete element lives.
    /// </summary>
    (int Drawn, IReadOnlyDictionary<string, int> Elemental) Affixes(InstanceRow instance, ContainerRow container)
    {
        var core = container.Atoms.Select(a => a.AtomId).ToHashSet(StringComparer.Ordinal);
        var elemental = new Dictionary<string, int>(StringComparer.Ordinal);
        var drawn = 0;

        foreach (var atom in instance.Atoms)
        {
            if (core.Contains(atom.AtomId)) continue;
            drawn++;
            var variant = _store.GetAtom(atom.AtomId)?.Variant;
            if (variant is { Length: > 0 } && ElementRoster.TryParse(variant, out _))
                elemental[variant] = elemental.TryGetValue(variant, out var n) ? n + 1 : 1;
        }

        return (drawn, elemental);
    }

    bool TryRecipe(string recipeId, CraftOperation expected, out MaterialRecipe recipe, out string reason)
    {
        if (!_recipes.Recipes.TryGetValue(recipeId, out recipe!))
        {
            var refused = _recipes.Refusals.FirstOrDefault(r =>
                string.Equals(r.RecipeId, recipeId, StringComparison.Ordinal));
            reason = refused is null
                ? $"material.recipe-unknown: '{recipeId}' is not in the loaded catalog"
                : $"material.recipe-unknown: '{recipeId}' was refused at load — {refused.Rule}: {refused.Detail}";
            return false;
        }

        if (recipe.Operation != expected)
        {
            reason = $"material.operation-mismatch: recipe '{recipeId}' is a " +
                     $"'{CraftOperations.Id(recipe.Operation)}', not a '{CraftOperations.Id(expected)}'";
            return false;
        }

        reason = "";
        return true;
    }

    // ---- rendering ---------------------------------------------------------------------------------

    IReadOnlyList<WorkbenchSocketDto> Sockets(string instanceId) =>
        _store.GetSockets(instanceId)
            .Select(s => new WorkbenchSocketDto(s.Index, s.Affinity, s.Crafted, s.InsertContainerId))
            .ToList();

    static IReadOnlyList<WorkbenchCostDto> Lines(IReadOnlyList<MaterialCostLine> lines) =>
        lines.Select(l => new WorkbenchCostDto(l.Class.ToString(), l.MaterialId, l.Qty)).ToList();

    static WorkbenchOutcomeDto Refused(string verb, string instanceId, string recipeId, string reason) =>
        new(false, verb, reason, instanceId, recipeId, 0, false, "refused", 0, 0, 0,
            Array.Empty<WorkbenchCostDto>(), Array.Empty<WorkbenchCostDto>(), Array.Empty<WorkbenchSocketDto>());

    static string PlayerKey(long playerId) => playerId.ToString(CultureInfo.InvariantCulture);

    static string OutcomeId(EnhanceOutcome outcome) => outcome switch
    {
        EnhanceOutcome.Success => "success",
        EnhanceOutcome.Failure => "failure",
        EnhanceOutcome.FailureWithDowngrade => "failure-downgrade",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };

    static InstanceHead HeadOf(InstanceRow instance, int enhanceLevel) => new(
        enhanceLevel,
        instance.Atoms
            .OrderBy(a => a.Seq)
            .Select(a => new InstanceAtomHead(a.Seq, a.AtomId, ReadValues(a.ValuesJson)))
            .ToList());

    static IReadOnlyDictionary<string, long> ReadValues(string valuesJson)
    {
        var values = new Dictionary<string, long>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(valuesJson)) return values;

        try
        {
            using var doc = JsonDocument.Parse(valuesJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return values;
            foreach (var property in doc.RootElement.EnumerateObject())
                if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt64(out var v))
                    values[property.Name] = v;
        }
        catch (JsonException)
        {
            // An OnApply spec is an object, not a number — it contributes no scalar to the head hash
            // and is not an error. The atom id and seq are still hashed, so two instances that differ
            // only in an unresolved spec still differ.
        }

        return values;
    }

    /// <summary>
    /// D2 rung 1′, written lazily at the FIRST mutation (§11.3's lean): the instance's frozen numbers,
    /// keyed by <c>seq</c>. An item nobody ever crafts never pays for a second copy of its own values.
    /// </summary>
    static string OriginValuesJson(InstanceRow instance)
    {
        var sb = new StringBuilder("{");
        var first = true;
        foreach (var atom in instance.Atoms.OrderBy(a => a.Seq))
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(atom.Seq.ToString(CultureInfo.InvariantCulture)).Append("\":")
              .Append(string.IsNullOrWhiteSpace(atom.ValuesJson) ? "{}" : atom.ValuesJson);
        }

        return sb.Append('}').ToString();
    }
}
