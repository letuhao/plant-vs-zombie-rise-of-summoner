namespace FusionRpg.Core.Demons;

/// <summary>
/// The closed source grammar shared by every demon progression writer and reader. The source is
/// explicit data, never inferred from a PvZ type id: the same type can be a general spawn in one
/// mechanism and a unique specimen in another.
/// </summary>
public abstract record DemonProgressionSource(string Kind, string Id, string ScopeKey)
{
    public const string ContractKind = "demon.progression.v1";
    // Named aliases keep call sites readable; the versioned source_kind is shared by all variants.
    public const string EmpireGeneralKind = ContractKind;
    public const string UniqueSpecimenKind = ContractKind;
    public const string CommanderKind = ContractKind;

    public static DemonProgressionSource EmpireGeneral(string speciesId)
    {
        var value = ValidatePart(speciesId, nameof(speciesId));
        return new EmpireGeneralSource(value);
    }

    public static DemonProgressionSource UniqueSpecimen(string instanceId, string occurrenceId)
    {
        var instance = ValidatePart(instanceId, nameof(instanceId));
        var occurrence = ValidatePart(occurrenceId, nameof(occurrenceId));
        return new UniqueSpecimenSource(instance, occurrence);
    }

    public static DemonProgressionSource Commander(string commanderId)
    {
        var value = ValidatePart(commanderId, nameof(commanderId));
        return new CommanderSource(value);
    }

    public static DemonProgressionSource Parse(string kind, string id)
    {
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(id))
            throw new FormatException("demon progression source kind and id are required");

        var parts = id.Trim().Split(':', StringSplitOptions.None);
        if (!string.Equals(kind.Trim(), ContractKind, StringComparison.Ordinal))
            throw new FormatException($"invalid demon progression source '{kind}:{id}'");
        try
        {
            return parts.Length switch
            {
                2 when parts[0] == "general" => EmpireGeneral(parts[1]),
                3 when parts[0] == "unique" => UniqueSpecimen(parts[1], parts[2]),
                2 when parts[0] == "commander" => Commander(parts[1]),
                _ => throw new FormatException($"invalid demon progression source '{kind}:{id}'")
            };
        }
        catch (ArgumentException ex)
        {
            throw new FormatException($"invalid demon progression source '{kind}:{id}'", ex);
        }
    }

    static string ValidatePart(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("source identity is required", parameterName);
        var trimmed = value.Trim();
        if (trimmed.Contains(':', StringComparison.Ordinal))
            throw new ArgumentException("source identity cannot contain ':'", parameterName);
        return trimmed;
    }

    public sealed record EmpireGeneralSource(string SpeciesId)
        : DemonProgressionSource(ContractKind, $"general:{SpeciesId}", SpeciesId);

    public sealed record UniqueSpecimenSource(string InstanceId, string OccurrenceId)
        : DemonProgressionSource(ContractKind, $"unique:{InstanceId}:{OccurrenceId}", InstanceId);

    public sealed record CommanderSource(string CommanderId)
        : DemonProgressionSource(ContractKind, $"commander:{CommanderId}", CommanderId);
}
