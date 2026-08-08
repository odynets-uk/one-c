namespace OneC.Domain.Metadata;

/// <summary>
///     Represents the full metadata model parsed from the 1C XSD schema.
/// </summary>
public sealed record MetadataModel
{
    /// <summary>
    ///     Gets the list of catalog definitions.
    /// </summary>
    public IReadOnlyList<CatalogDefinition> Catalogs { get; init; } = [];

    /// <summary>
    ///     Gets the list of enumeration definitions.
    /// </summary>
    public IReadOnlyList<EnumDefinition> Enums { get; init; } = [];

    /// <summary>
    ///     Gets a catalog by name (case-insensitive).
    /// </summary>
    /// <param name="name">Catalog name.</param>
    /// <returns>Catalog definition or null.</returns>
    public CatalogDefinition? FindCatalog(string name) =>
        Catalogs.FirstOrDefault(c =>
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            c.XsdTypeName.Equals(name, StringComparison.OrdinalIgnoreCase));
}