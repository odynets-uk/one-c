namespace OneC.Domain.Metadata;

/// <summary>
///     Represents a field definition in a 1C metadata object.
/// </summary>
public sealed record FieldDefinition
{
    /// <summary>
    ///     Gets the field name (e.g. "Code", "Description", "Артикул").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Gets the field type.
    /// </summary>
    public required FieldType Type { get; init; }

    /// <summary>
    ///     Gets the XSD type name (e.g. "xs:string", "tns:CatalogRef.Номенклатура").
    /// </summary>
    public required string XsdType { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the field is optional (minOccurs="0").
    /// </summary>
    public bool IsOptional { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the field is nillable.
    /// </summary>
    public bool IsNillable { get; init; }

    /// <summary>
    ///     Gets the referenced type name for Reference/Enum fields (e.g. "CatalogRef.Номенклатура").
    /// </summary>
    public string? ReferencedType { get; init; }
}