namespace OneC.Domain.Metadata;

/// <summary>
///     Represents a 1C enumeration (перелік) definition.
/// </summary>
public sealed record EnumDefinition
{
    /// <summary>
    ///     Gets the enumeration name (e.g. "СтавкиНДС").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Gets the full XSD type name (e.g. "EnumRef.СтавкиНДС").
    /// </summary>
    public required string XsdTypeName { get; init; }

    /// <summary>
    ///     Gets the list of allowed values.
    /// </summary>
    public IReadOnlyList<string> Values { get; init; } = [];
}