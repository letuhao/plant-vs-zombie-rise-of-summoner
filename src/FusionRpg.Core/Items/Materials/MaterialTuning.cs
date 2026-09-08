using System.Text.Json;

namespace FusionRpg.Core.Items.Materials;

/// <summary>What a cost coefficient is multiplied by. Every one of these is a property of the
/// <b>target</b> — D26's whole point. There is deliberately no member reading the player.</summary>
public enum CostVariable
{
    /// <summary>×1. A number the operation charges regardless of the target.</summary>
    Flat = 0,

    /// <summary>The substrate grade 1..4, derived from the target's item level.</summary>
    Grade,

    /// <summary>`b` — the target's rung INDEX + 1, so 1..10. ⛔ Never `rarity.ordinal`, which is
    /// 10…100 and would make every row wrong by 10×.</summary>
    Rung,

    /// <summary>`n+1` — the enhancement level the operation is buying.</summary>
    EnhanceNext,

    /// <summary>`ceil((n+1)/3)` — temper's catalyst leg.</summary>
    CeilThirdEnhanceNext,
}

/// <summary>One leg of one operation's reference cost. <see cref="Coefficient"/> is a
/// <c>long</c> because it is multiplied by a target property and the product is a magnitude.</summary>
public readonly record struct CostLeg(long Coefficient, CostVariable Variable, bool BandImmune)
{
    /// <summary>
    /// The base quantity before any <c>costBand</c> multiplier. Widened before multiplying and never
    /// divided here — the single divide-by-1000 happens once, at the end, in
    /// <see cref="MaterialTuning.ApplyBand"/>. Overflow throws (checked), it never wraps.
    /// </summary>
    public long BaseQty(int grade, int rungIndex, int enhanceLevel) => Variable switch
    {
        CostVariable.Flat => Coefficient,
        CostVariable.Grade => checked(Coefficient * grade),
        CostVariable.Rung => checked(Coefficient * (rungIndex + 1L)),
        CostVariable.EnhanceNext => checked(Coefficient * (enhanceLevel + 1L)),
        CostVariable.CeilThirdEnhanceNext => checked(Coefficient * ((enhanceLevel + 1L + 2) / 3)),
        _ => throw new ArgumentOutOfRangeException(nameof(Variable), Variable, null),
    };
}

/// <summary>One row of the reference cost table (`salvage-craft` §"The reference cost table"), ten
/// rows for ten operations. A null leg is a cell I9 §7.4 leaves as an em dash.</summary>
public sealed record MaterialOperationCost(
    CraftOperation Operation,
    string Owner,
    CostLeg? Souls,
    CostLeg? Substrate,
    CostLeg? Shard,
    CostLeg? Essence,
    string? CatalystId,
    CostLeg? Catalyst);

/// <summary>One rung's salvage coefficients (I9 §5.1, re-derived to ten rungs).</summary>
public readonly record struct SalvageCoefficient(long SubstrateBase, long EssenceCap, long ShardBack);

public sealed class MaterialTuningRejection : Exception
{
    public MaterialTuningRejection(string message) : base(message) { }
}

/// <summary>
/// Pure parser over `data/tuning/materials.v1.json` — no file I/O (tunables-ssot.md §7.2: "Core never
/// reads a file. Hosts load and inject."). <b>No key has a default.</b> A missing one throws at load
/// rather than resolving to a silently-invented price, which is the same bar module 13's
/// `SetCharmGenTuning` set: a generator running on a default is how an unreviewed number reaches
/// content.
/// </summary>
public sealed class MaterialTuning
{
    MaterialTuning(
        int itemLevelPerGrade,
        int maxGrade,
        int upcycleMaxInputGrade,
        long upcycleInputPerOutput,
        long salvageEnhanceReturnDivisor,
        IReadOnlyDictionary<string, int> bandMultipliersPerMille,
        IReadOnlyDictionary<CraftOperation, MaterialOperationCost> operations,
        IReadOnlyDictionary<string, SalvageCoefficient> salvage)
    {
        ItemLevelPerGrade = itemLevelPerGrade;
        MaxGrade = maxGrade;
        UpcycleMaxInputGrade = upcycleMaxInputGrade;
        UpcycleInputPerOutput = upcycleInputPerOutput;
        SalvageEnhanceReturnDivisor = salvageEnhanceReturnDivisor;
        BandMultipliersPerMille = bandMultipliersPerMille;
        Operations = operations;
        Salvage = salvage;
    }

    public int ItemLevelPerGrade { get; }
    public int MaxGrade { get; }

    /// <summary>⚠ BOUNDED RATIO (AGENTS.md's exempt category), not a progression ceiling: it caps a
    /// conversion between two material grades, never what a player may earn or own. Stated in the
    /// tuning file's own `capNote` too, where a balance pass reads it.</summary>
    public int UpcycleMaxInputGrade { get; }

    public long UpcycleInputPerOutput { get; }
    public long SalvageEnhanceReturnDivisor { get; }
    public IReadOnlyDictionary<string, int> BandMultipliersPerMille { get; }
    public IReadOnlyDictionary<CraftOperation, MaterialOperationCost> Operations { get; }
    public IReadOnlyDictionary<string, SalvageCoefficient> Salvage { get; }

    /// <summary>
    /// bands.v1.json's own formula, verbatim: <c>max(1, ceil(baseQty × multiplierPerMille / 1000))</c>.
    /// Widen before multiplying, divide by 1000 <b>last and exactly once</b>, ceiling always so no
    /// band can make a cost free, and <c>checked</c> so an overflow throws instead of wrapping.
    /// </summary>
    public static long ApplyBand(long baseQty, int multiplierPerMille) =>
        checked(Math.Max(1L, (baseQty * multiplierPerMille + 999) / 1000));

    /// <summary>I9 §5.1's grade function. Integer division; never reads the player.</summary>
    public int GradeForItemLevel(int itemLevel)
    {
        if (itemLevel < 0)
            throw new MaterialTuningRejection($"item level {itemLevel} is negative");
        return 1 + Math.Min(MaxGrade - 1, itemLevel / ItemLevelPerGrade);
    }

    public int BandMultiplier(string band)
    {
        if (!BandMultipliersPerMille.TryGetValue(band, out var m))
            throw new MaterialTuningRejection(
                $"cost band '{band}' is not in the frozen bands.v1.json vocabulary " +
                $"({string.Join("/", BandMultipliersPerMille.Keys.OrderBy(k => BandMultipliersPerMille[k]))})");
        return m;
    }

    public static MaterialTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new MaterialTuningRejection("materials tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new MaterialTuningRejection($"materials tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;

            var gradeEl = Obj(root, "grade");
            var itemLevelPerGrade = Int(gradeEl, "itemLevelPerGrade", "grade");
            var maxGrade = Int(gradeEl, "maxGrade", "grade");
            if (itemLevelPerGrade <= 0)
                throw new MaterialTuningRejection("materials tuning: grade.itemLevelPerGrade must be positive");
            if (maxGrade != MaterialCatalog.SubstrateGrades.Count)
                throw new MaterialTuningRejection(
                    $"materials tuning: grade.maxGrade is {maxGrade} but the substrate vocabulary ships " +
                    $"{MaterialCatalog.SubstrateGrades.Count} grades — a fifth grade is a new material id, not a tuning edit");

            var upcycleEl = Obj(root, "upcycle");
            var maxInputGrade = Int(upcycleEl, "maxInputGrade", "upcycle");
            var inputPerOutput = Int(upcycleEl, "inputPerOutput", "upcycle");
            if (maxInputGrade < 1 || maxInputGrade >= maxGrade)
                throw new MaterialTuningRejection(
                    $"materials tuning: upcycle.maxInputGrade {maxInputGrade} must be in 1..{maxGrade - 1} — " +
                    "upcycling INTO the top grade is the exact leak the cap exists to close (I9 §5.3)");
            if (inputPerOutput < 2)
                throw new MaterialTuningRejection(
                    "materials tuning: upcycle.inputPerOutput below 2 makes the conversion free or profitable");

            var divisor = Int(root, "salvageEnhanceReturnDivisor", "(root)");
            if (divisor < 1)
                throw new MaterialTuningRejection("materials tuning: salvageEnhanceReturnDivisor must be at least 1");

            var bands = ParseBands(Obj(root, "costBandMultiplierPerMille"));
            var operations = ParseOperations(Obj(root, "operations"), inputPerOutput);
            var salvage = ParseSalvage(Obj(root, "salvageCoefficient"));

            return new MaterialTuning(
                itemLevelPerGrade, maxGrade, maxInputGrade, inputPerOutput, divisor, bands, operations, salvage);
        }
    }

    static IReadOnlyDictionary<string, int> ParseBands(JsonElement el)
    {
        var bands = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Number) continue; // the two *Note strings
            if (!prop.Value.TryGetInt32(out var v) || v <= 0)
                throw new MaterialTuningRejection($"materials tuning: cost band '{prop.Name}' is not a positive integer");
            bands[prop.Name] = v;
        }

        if (bands.Count == 0)
            throw new MaterialTuningRejection("materials tuning: costBandMultiplierPerMille has no bands");

        return bands;
    }

    static IReadOnlyDictionary<CraftOperation, MaterialOperationCost> ParseOperations(
        JsonElement el, long upcycleInputPerOutput)
    {
        var result = new Dictionary<CraftOperation, MaterialOperationCost>();

        foreach (var op in CraftOperations.All)
        {
            var id = CraftOperations.Id(op);
            if (!el.TryGetProperty(id, out var rowEl) || rowEl.ValueKind != JsonValueKind.Object)
                throw new MaterialTuningRejection(
                    $"materials tuning: operations is missing row '{id}' — all {CraftOperations.All.Count} " +
                    "priced operations must have a row, including D24's `imbue`, which I9 §7.4 has none for");

            var catalystId = CostClassMatrix.CatalystFor(op);
            var catalystEl = rowEl.TryGetProperty("catalyst", out var c) && c.ValueKind == JsonValueKind.Object ? c : (JsonElement?)null;

            if (catalystId == null && catalystEl != null)
                throw new MaterialTuningRejection(
                    $"materials tuning: operation '{id}' rides no catalyst (CostClassMatrix) but authors one");
            if (catalystId != null && catalystEl == null)
                throw new MaterialTuningRejection($"materials tuning: operation '{id}' must price its '{catalystId}' leg");

            if (catalystEl is { } ce)
            {
                var authored = Str(ce, "id", id);
                if (!string.Equals(authored, catalystId, StringComparison.Ordinal))
                    throw new MaterialTuningRejection(
                        $"materials tuning: operation '{id}' prices catalyst '{authored}' but rides '{catalystId}'");
            }

            var row = new MaterialOperationCost(
                op,
                Str(rowEl, "owner", id),
                Leg(rowEl, "souls", id),
                Leg(rowEl, "substrate", id),
                Leg(rowEl, "shard", id),
                Leg(rowEl, "essence", id),
                catalystId,
                catalystEl is { } cel ? LegFrom(cel, id, "catalyst") : null);

            // Every priced leg must be a class the matrix lets this operation spend — so the cost
            // table and the vocabulary cannot disagree about what an operation is allowed to charge.
            Forbid(row.Substrate, MaterialClass.Substrate, op);
            Forbid(row.Shard, MaterialClass.Shard, op);
            Forbid(row.Essence, MaterialClass.Essence, op);

            result[op] = row;
        }

        // I9 §7.4's upcycle row and §7.3's drain valve are the same number; if they drift, a balance
        // pass has changed the conversion in one place and not the other.
        var upcycleSubstrate = result[CraftOperation.Upcycle].Substrate
            ?? throw new MaterialTuningRejection("materials tuning: upcycle must price a substrate leg");
        if (upcycleSubstrate.Variable != CostVariable.Flat || upcycleSubstrate.Coefficient != upcycleInputPerOutput)
            throw new MaterialTuningRejection(
                $"materials tuning: operations.upcycle.substrate ({upcycleSubstrate.Coefficient} {upcycleSubstrate.Variable}) " +
                $"must be the flat upcycle.inputPerOutput ({upcycleInputPerOutput}) — they are the same conversion ratio");

        // D24: imbue prices on bore's curve. Asserted at load so a balance pass that moves one and
        // forgets the other fails at boot rather than at the first crafted socket.
        var bore = result[CraftOperation.Bore];
        var imbue = result[CraftOperation.Imbue];
        if (bore.Souls != imbue.Souls || bore.Substrate != imbue.Substrate)
            throw new MaterialTuningRejection(
                "materials tuning: D24 requires `imbue`'s souls and substrate legs to be `bore`'s verbatim — " +
                $"bore souls={bore.Souls} substrate={bore.Substrate}, imbue souls={imbue.Souls} substrate={imbue.Substrate}");

        return result;
    }

    static void Forbid(CostLeg? leg, MaterialClass cls, CraftOperation op)
    {
        if (leg != null && !CostClassMatrix.Allows(op, cls))
            throw new MaterialTuningRejection(
                $"materials tuning: operation '{CraftOperations.Id(op)}' prices a {cls} leg the cost-class matrix forbids");
    }

    static IReadOnlyDictionary<string, SalvageCoefficient> ParseSalvage(JsonElement el)
    {
        var result = new Dictionary<string, SalvageCoefficient>(StringComparer.Ordinal);
        long prevSubstrate = long.MinValue, prevEssence = long.MinValue, prevShard = long.MinValue;

        foreach (var rungId in RarityLadder.RungIds)
        {
            if (!el.TryGetProperty(rungId, out var rowEl) || rowEl.ValueKind != JsonValueKind.Object)
                throw new MaterialTuningRejection($"materials tuning: salvageCoefficient is missing rung '{rungId}'");

            var row = new SalvageCoefficient(
                Int(rowEl, "substrateBase", rungId),
                Int(rowEl, "essenceCap", rungId),
                Int(rowEl, "shardBack", rungId));

            if (row.SubstrateBase < 1)
                throw new MaterialTuningRejection(
                    $"materials tuning: salvage substrateBase for '{rungId}' is {row.SubstrateBase} — " +
                    "salvaging anything must return at least one substrate, or the operation is a delete button");
            if (row.EssenceCap < 0 || row.ShardBack < 0)
                throw new MaterialTuningRejection($"materials tuning: negative salvage coefficient on rung '{rungId}'");

            // Monotone non-decreasing on all three axes: a better item must never salvage for less.
            if (row.SubstrateBase < prevSubstrate || row.EssenceCap < prevEssence || row.ShardBack < prevShard)
                throw new MaterialTuningRejection(
                    $"materials tuning: salvage coefficients fall at rung '{rungId}' — the ladder must be monotone, " +
                    "or a player is paid to keep the worse item");

            (prevSubstrate, prevEssence, prevShard) = (row.SubstrateBase, row.EssenceCap, row.ShardBack);
            result[rungId] = row;
        }

        // R1's bottom edge, enforced as data rather than trusted: chaff has no rung below it.
        if (result[RarityLadder.RungIds[0]].ShardBack != 0)
            throw new MaterialTuningRejection(
                $"materials tuning: rung '{RarityLadder.RungIds[0]}' must return no shard (R1 — there is no rung below it)");

        return result;
    }

    static CostLeg? Leg(JsonElement parent, string key, string opId) =>
        parent.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Object ? LegFrom(el, opId, key) : null;

    static CostLeg LegFrom(JsonElement el, string opId, string key)
    {
        var coefficient = Int(el, "coefficient", $"{opId}.{key}");
        if (coefficient < 1)
            throw new MaterialTuningRejection($"materials tuning: {opId}.{key}.coefficient must be at least 1");

        var variableId = Str(el, "variable", $"{opId}.{key}");
        var variable = variableId switch
        {
            "flat" => CostVariable.Flat,
            "grade" => CostVariable.Grade,
            "rung" => CostVariable.Rung,
            "enhanceNext" => CostVariable.EnhanceNext,
            "ceilThirdEnhanceNext" => CostVariable.CeilThirdEnhanceNext,
            _ => throw new MaterialTuningRejection(
                $"materials tuning: {opId}.{key}.variable '{variableId}' is not a target property " +
                "(flat/grade/rung/enhanceNext/ceilThirdEnhanceNext). D26: a cost variable reading the PLAYER has no spelling here on purpose"),
        };

        var bandImmune = el.TryGetProperty("bandImmune", out var bi) && bi.ValueKind == JsonValueKind.True;
        return new CostLeg(coefficient, variable, bandImmune);
    }

    static JsonElement Obj(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Object)
            throw new MaterialTuningRejection($"materials tuning: missing or non-object '{key}'");
        return el;
    }

    static int Int(JsonElement parent, string key, string where)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new MaterialTuningRejection($"materials tuning: '{where}' missing or non-integer '{key}'");
        return v;
    }

    static string Str(JsonElement parent, string key, string where)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.String)
            throw new MaterialTuningRejection($"materials tuning: '{where}' missing or non-string '{key}'");
        return el.GetString()!;
    }
}
