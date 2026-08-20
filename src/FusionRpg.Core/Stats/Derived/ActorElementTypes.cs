namespace FusionRpg.Core.Stats.Derived;

public enum ElementTypeId
{
    Fire,
    Ice,
    Air,
    Earth
}

public sealed record ActorElementTypes
{
    public static ActorElementTypes Neutral { get; } = new();

    public ElementTypeId? Primary { get; init; }
    public ElementTypeId? Secondary { get; init; }

    public bool IsNeutral => Primary is null && Secondary is null;

    public static ActorElementTypes Create(ElementTypeId? primary = null, ElementTypeId? secondary = null)
    {
        if (primary is null && secondary is not null)
            throw new ArgumentException("Secondary type requires a primary type.");
        if (primary is not null && primary == secondary)
            throw new ArgumentException("Primary and secondary types must differ.");
        return new ActorElementTypes
        {
            Primary = primary,
            Secondary = secondary
        };
    }

    public static ActorElementTypes Parse(string? primary, string? secondary)
    {
        var p = ParseSlot(primary, "primary");
        var s = ParseSlot(secondary, "secondary");
        return Create(p, s);
    }

    static ElementTypeId? ParseSlot(string? value, string slotName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (string.Equals(value, "omni", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Element slot '{slotName}' cannot use omni.");
        if (Enum.TryParse<ElementTypeId>(value, ignoreCase: true, out var parsed))
            return parsed;
        throw new ArgumentException($"Unknown element type '{value}' for slot '{slotName}'.");
    }
}

public static class ElementTypeIdExtensions
{
    public static string ToElementId(this ElementTypeId id) => id switch
    {
        ElementTypeId.Fire => "fire",
        ElementTypeId.Ice => "ice",
        ElementTypeId.Air => "air",
        ElementTypeId.Earth => "earth",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown element type.")
    };
}
