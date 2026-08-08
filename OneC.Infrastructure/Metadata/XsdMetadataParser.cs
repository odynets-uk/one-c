using System.Xml;
using OneC.Domain.Metadata;

namespace OneC.Infrastructure.Metadata;

/// <summary>
///     Parses the 1C data-enterprise.xsd schema into a <see cref="MetadataModel" />.
///     Uses streaming XmlReader to avoid loading the whole file into memory.
/// </summary>
public static class XsdMetadataParser
{
    private const string XsNamespace = "http://www.w3.org/2001/XMLSchema";
    private const string TnsNamespace = "http://v8.1c.ru/8.1/data/enterprise/current-config";

    /// <summary>
    ///     Parses the XSD file at the given path.
    /// </summary>
    /// <param name="xsdPath">Path to the XSD file.</param>
    /// <returns>Parsed metadata model.</returns>
    public static MetadataModel Parse(string xsdPath)
    {
        var catalogs = new List<CatalogDefinition>();
        var enums = new List<EnumDefinition>();

        using var reader = XmlReader.Create(xsdPath, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreWhitespace = true,
        });

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.LocalName == "complexType" && reader.NamespaceURI == XsNamespace)
            {
                var name = reader.GetAttribute("name");
                if (name is null)
                {
                    continue;
                }

                if (name.StartsWith("CatalogObject.", StringComparison.Ordinal))
                {
                    var catalog = ParseCatalog(reader, name);
                    catalogs.Add(catalog);
                }
                else if (name.StartsWith("CatalogTabularSectionRow.", StringComparison.Ordinal))
                {
                    // Tabular sections are handled as part of catalog fields.
                    // We skip them here; they are referenced by CatalogObject fields.
                }
            }
            else if (reader.LocalName == "simpleType" && reader.NamespaceURI == XsNamespace)
            {
                var name = reader.GetAttribute("name");
                if (name is null)
                {
                    continue;
                }

                if (name.StartsWith("EnumRef.", StringComparison.Ordinal))
                {
                    var enumDef = ParseEnum(reader, name);
                    enums.Add(enumDef);
                }
            }
        }

        return new MetadataModel
        {
            Catalogs = catalogs,
            Enums = enums,
        };
    }

    private static CatalogDefinition ParseCatalog(XmlReader reader, string xsdTypeName)
    {
        var catalogName = xsdTypeName["CatalogObject.".Length..];
        var refTypeName = $"CatalogRef.{catalogName}";
        var fields = new List<FieldDefinition>();

        // Read the complexType content.
        var depth = reader.Depth;
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
            {
                break;
            }

            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "element" || reader.NamespaceURI != XsNamespace)
            {
                continue;
            }

            var fieldName = reader.GetAttribute("name");
            if (fieldName is null)
            {
                continue;
            }

            var xsdType = reader.GetAttribute("type") ?? string.Empty;
            var minOccurs = reader.GetAttribute("minOccurs");
            var nillable = reader.GetAttribute("nillable");

            fields.Add(new FieldDefinition
            {
                Name = fieldName,
                Type = MapFieldType(xsdType),
                XsdType = xsdType,
                IsOptional = minOccurs == "0",
                IsNillable = nillable == "true",
                ReferencedType = GetReferencedType(xsdType),
            });
        }

        return new CatalogDefinition
        {
            Name = catalogName,
            XsdTypeName = xsdTypeName,
            RefTypeName = refTypeName,
            Fields = fields,
        };
    }

    private static EnumDefinition ParseEnum(XmlReader reader, string xsdTypeName)
    {
        var enumName = xsdTypeName["EnumRef.".Length..];
        var values = new List<string>();

        var depth = reader.Depth;
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
            {
                break;
            }

            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "enumeration" && reader.NamespaceURI == XsNamespace)
            {
                var value = reader.GetAttribute("value");
                if (value is not null)
                {
                    values.Add(value);
                }
            }
        }

        return new EnumDefinition
        {
            Name = enumName,
            XsdTypeName = xsdTypeName,
            Values = values,
        };
    }

    private static FieldType MapFieldType(string xsdType)
    {
        return xsdType switch
        {
            "xs:string" => FieldType.String,
            "xs:boolean" => FieldType.Boolean,
            "xs:decimal" => FieldType.Decimal,
            "xs:dateTime" => FieldType.DateTime,
            _ when xsdType.StartsWith("tns:CatalogRef.", StringComparison.Ordinal) => FieldType.Reference,
            _ when xsdType.StartsWith("tns:EnumRef.", StringComparison.Ordinal) => FieldType.Enum,
            _ when xsdType.StartsWith("tns:CatalogTabularSectionRow.", StringComparison.Ordinal) => FieldType.TabularSection,
            _ => FieldType.Unknown,
        };
    }

    private static string? GetReferencedType(string xsdType)
    {
        if (xsdType.StartsWith("tns:", StringComparison.Ordinal))
        {
            return xsdType[4..];
        }

        return null;
    }
}