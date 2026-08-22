using System.Text.Json;

namespace FusionRpg.Core.Effects.Atoms;

/// <summary>
/// Reads the two authored shapes — a value spec and a predicate tree — from their canonical JSON
/// (definitions.md §2 and §3). <b>One canonical form; no other shape parses.</b>
///
/// <para>Every failure comes back as a typed rejection rather than a default, because a value spec
/// that silently becomes zero and a predicate that silently becomes "always" are the two most
/// expensive ways this layer could fail.</para>
/// </summary>
public static class AtomJson
{
    // ---- value specs ---------------------------------------------------------------------------
    //
    //   42                                            -> Fixed(42)
    //   { "min": 100, "max": 200, "roll": "onApply", "curve": "curve.atk.level" }

    public static AtomRejection TryReadValueSpec(JsonElement el, out ValueSpec spec)
    {
        spec = default;

        if (el.ValueKind == JsonValueKind.Number)
        {
            if (!el.TryGetInt32(out var n))
                return AtomRejection.Fail(AtomRejectionReason.BadValueSpec,
                    "magnitudes are integers — see definitions §2 on units");
            spec = ValueSpec.Of(n);
            return AtomRejection.Ok;
        }

        if (el.ValueKind != JsonValueKind.Object)
            return AtomRejection.Fail(AtomRejectionReason.BadValueSpec,
                $"expected a number or a value-spec object, got {el.ValueKind}");

        if (!TryInt(el, "min", out var min))
            return AtomRejection.Fail(AtomRejectionReason.BadValueSpec, "value spec needs an integer 'min'");
        if (!TryInt(el, "max", out var max))
            return AtomRejection.Fail(AtomRejectionReason.BadValueSpec, "value spec needs an integer 'max'");

        var roll = RollPolicy.Fixed;
        if (el.TryGetProperty("roll", out var rollEl))
        {
            var name = rollEl.ValueKind == JsonValueKind.String ? rollEl.GetString() : null;
            if (!TryParseRoll(name, out roll))
                return AtomRejection.Fail(AtomRejectionReason.BadValueSpec,
                    $"unknown roll policy '{name}' — one of fixed | onInstantiate | onApply");
        }

        string? curve = null;
        if (el.TryGetProperty("curve", out var curveEl) && curveEl.ValueKind == JsonValueKind.String)
            curve = curveEl.GetString();

        spec = new ValueSpec(min, max, roll, curve);
        return spec.Validate();
    }

    /// <summary>Case-insensitive so authored JSON may read naturally; the set itself is closed.</summary>
    static bool TryParseRoll(string? name, out RollPolicy roll)
    {
        switch (name?.ToLowerInvariant())
        {
            case "fixed": roll = RollPolicy.Fixed; return true;
            case "oninstantiate": roll = RollPolicy.OnInstantiate; return true;
            case "onapply": roll = RollPolicy.OnApply; return true;
            default: roll = RollPolicy.Fixed; return false;
        }
    }

    static bool TryInt(JsonElement obj, string name, out int value)
    {
        value = 0;
        return obj.TryGetProperty(name, out var el)
               && el.ValueKind == JsonValueKind.Number
               && el.TryGetInt32(out value);
    }

    // ---- predicates ----------------------------------------------------------------------------
    //
    //   { "op": "and", "children": [ { "leaf": "sideIs", "subject": "target", "value": "zombie" } ] }

    public static AtomRejection TryReadPredicate(JsonElement el, out PredicateNode? node)
    {
        node = null;

        if (el.ValueKind == JsonValueKind.Null) return AtomRejection.Ok; // absent means "always"

        if (el.ValueKind != JsonValueKind.Object)
            return AtomRejection.Fail(AtomRejectionReason.UnknownLeaf,
                $"a predicate node is an object, got {el.ValueKind}");

        // Internal node: op + children.
        if (el.TryGetProperty("op", out var opEl))
        {
            var op = opEl.ValueKind == JsonValueKind.String ? opEl.GetString()?.ToLowerInvariant() : null;

            var children = new List<PredicateNode>();
            if (el.TryGetProperty("children", out var kids) && kids.ValueKind == JsonValueKind.Array)
            {
                foreach (var kid in kids.EnumerateArray())
                {
                    var r = TryReadPredicate(kid, out var childNode);
                    if (!r.IsOk) return r;
                    if (childNode is not null) children.Add(childNode);
                }
            }

            switch (op)
            {
                case "and":
                    node = new PredicateNode.And(children);
                    return AtomRejection.Ok;
                case "or":
                    node = new PredicateNode.Or(children);
                    return AtomRejection.Ok;
                case "not":
                    // The compiler rejects a Not with anything but exactly one child; keep that in
                    // one place rather than duplicating the rule here.
                    if (children.Count != 1)
                        return AtomRejection.Fail(AtomRejectionReason.EmptyNode,
                            $"not takes exactly one child, got {children.Count}");
                    node = new PredicateNode.Not(children[0]);
                    return AtomRejection.Ok;
                default:
                    return AtomRejection.Fail(AtomRejectionReason.UnknownLeaf, $"unknown op '{op}'");
            }
        }

        // Leaf: leaf + subject + value.
        if (!el.TryGetProperty("leaf", out var leafEl) || leafEl.ValueKind != JsonValueKind.String)
            return AtomRejection.Fail(AtomRejectionReason.UnknownLeaf,
                "a predicate node carries either 'op' or 'leaf'");

        var leafName = leafEl.GetString();
        if (!Enum.TryParse<LeafId>(leafName, ignoreCase: true, out var leafId)
            || !Enum.IsDefined(typeof(LeafId), leafId))
            return AtomRejection.Fail(AtomRejectionReason.UnknownLeaf, $"unknown leaf '{leafName}'");

        // No default: an omitted subject is AmbiguousSubject, because OnDamageDealt inverts side.
        if (!el.TryGetProperty("subject", out var subjEl) || subjEl.ValueKind != JsonValueKind.String)
            return AtomRejection.Fail(AtomRejectionReason.AmbiguousSubject,
                $"leaf '{leafName}' has no subject");

        if (!Enum.TryParse<Subject>(subjEl.GetString(), ignoreCase: true, out var subject)
            || !Enum.IsDefined(typeof(Subject), subject))
            return AtomRejection.Fail(AtomRejectionReason.AmbiguousSubject,
                $"leaf '{leafName}' has subject '{subjEl.GetString()}' — one of self | target");

        var value = 0;
        string? text = null;
        List<int>? values = null;

        if (el.TryGetProperty("value", out var valEl))
        {
            switch (valEl.ValueKind)
            {
                case JsonValueKind.Number when valEl.TryGetInt32(out var n): value = n; break;
                case JsonValueKind.String: text = valEl.GetString(); break;
                case JsonValueKind.True: value = 1; break;
                case JsonValueKind.False: value = 0; break;
                case JsonValueKind.Array:
                    values = new List<int>();
                    foreach (var item in valEl.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var iv))
                            values.Add(iv);
                    break;
            }
        }

        node = new PredicateNode.Leaf(leafId, subject, value, text, values);
        return AtomRejection.Ok;
    }
}
