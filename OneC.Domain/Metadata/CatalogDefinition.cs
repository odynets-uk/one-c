namespace OneC.Domain.Metadata;

/// <summary>
///     Represents a 1C catalog (довідник) definition.
/// </summary>
public sealed record CatalogDefinition
{
    /// <summary>
    ///     Gets the catalog name (e.g. "Номенклатура").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Gets the full XSD type name (e.g. "CatalogObject.Номенклатура").
    /// </summary>
    public required string XsdTypeName { get; init; }

    /// <summary>
    ///     Gets the reference type name (e.g. "CatalogRef.Номенклатура").
    /// </summary>
    public required string RefTypeName { get; init; }

    /// <summary>
    ///     Gets the list of field definitions.
    /// </summary>
    public IReadOnlyList<FieldDefinition> Fields { get; init; } = [];

    /// <summary>
    ///     Gets a value indicating whether the catalog has an IsFolder field (hierarchical).
    /// </summary>
    public bool IsHierarchical => Fields.Any(f => f.Name == "IsFolder");
}