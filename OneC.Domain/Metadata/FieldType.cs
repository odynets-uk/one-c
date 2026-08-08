namespace OneC.Domain.Metadata;

/// <summary>
///     Represents the type of a 1C metadata field.
/// </summary>
public enum FieldType
{
    /// <summary>String field (xs:string).</summary>
    String,

    /// <summary>Boolean field (xs:boolean).</summary>
    Boolean,

    /// <summary>Decimal field (xs:decimal).</summary>
    Decimal,

    /// <summary>DateTime field (xs:dateTime).</summary>
    DateTime,

    /// <summary>Reference to another catalog (CatalogRef.*).</summary>
    Reference,

    /// <summary>Enumeration reference (EnumRef.*).</summary>
    Enum,

    /// <summary>Tabular section row (CatalogTabularSectionRow.*).</summary>
    TabularSection,

    /// <summary>Unknown/untyped field.</summary>
    Unknown,
}