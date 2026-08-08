using OneC.Domain.Metadata;
using OneC.Infrastructure.Metadata;
using Xunit;

namespace OneC.Tests;

/// <summary>
///     Tests for <see cref="XsdMetadataParser" /> using the sample XSD file.
/// </summary>
public class XsdMetadataParserTests
{
    private const string SampleXsdPath = "CatalogObject.Номенклатура.xsd";

    [Fact]
    public void Parse_LoadsCatalogs()
    {
        var model = XsdMetadataParser.Parse(SampleXsdPath);

        Assert.NotEmpty(model.Catalogs);
        Assert.Contains(model.Catalogs, c => c.Name == "Номенклатура");
    }

    [Fact]
    public void Parse_Номенклатура_HasExpectedFields()
    {
        var model = XsdMetadataParser.Parse(SampleXsdPath);
        var catalog = model.FindCatalog("Номенклатура");

        Assert.NotNull(catalog);
        Assert.Equal("CatalogObject.Номенклатура", catalog!.XsdTypeName);
        Assert.Equal("CatalogRef.Номенклатура", catalog.RefTypeName);
        Assert.True(catalog.IsHierarchical);
        Assert.Contains(catalog.Fields, f => f.Name == "Code" && f.Type == FieldType.String);
        Assert.Contains(catalog.Fields, f => f.Name == "Description" && f.Type == FieldType.String);
        Assert.Contains(catalog.Fields, f => f.Name == "IsFolder" && f.Type == FieldType.Boolean);
        Assert.Contains(catalog.Fields, f => f.Name == "Артикул" && f.Type == FieldType.String && f.IsOptional);
    }

    [Fact]
    public void Parse_Номенклатура_HasReferences()
    {
        var model = XsdMetadataParser.Parse(SampleXsdPath);
        var catalog = model.FindCatalog("Номенклатура");

        Assert.NotNull(catalog);

        var parent = catalog!.Fields.First(f => f.Name == "Parent");
        Assert.Equal(FieldType.Reference, parent.Type);
        Assert.Equal("CatalogRef.Номенклатура", parent.ReferencedType);

        var priceGroup = catalog.Fields.First(f => f.Name == "ЦеноваяГруппа");
        Assert.Equal(FieldType.Reference, priceGroup.Type);
        Assert.Equal("CatalogRef.ЦеновыеГруппы", priceGroup.ReferencedType);
    }

    [Fact]
    public void Parse_LoadsEnums()
    {
        var model = XsdMetadataParser.Parse(SampleXsdPath);

        Assert.NotEmpty(model.Enums);
        Assert.Contains(model.Enums, e => e.Name == "СтавкиНДС");
    }

    [Fact]
    public void Parse_СтавкиНДС_HasExpectedValues()
    {
        var model = XsdMetadataParser.Parse(SampleXsdPath);
        var enumDef = model.Enums.First(e => e.Name == "СтавкиНДС");

        Assert.Equal("EnumRef.СтавкиНДС", enumDef.XsdTypeName);
        Assert.Contains("НДС0", enumDef.Values);
        Assert.Contains("НДС20", enumDef.Values);
        Assert.Contains("БезНДС", enumDef.Values);
    }

    [Fact]
    public void Parse_Контрагенты_HasTabularSections()
    {
        var model = XsdMetadataParser.Parse(SampleXsdPath);
        var catalog = model.FindCatalog("Контрагенты");

        Assert.NotNull(catalog);

        var tabular = catalog!.Fields.First(f => f.Name == "ВидыДеятельности");
        Assert.Equal(FieldType.TabularSection, tabular.Type);
        Assert.Equal("CatalogTabularSectionRow.Контрагенты.ВидыДеятельности", tabular.ReferencedType);
    }
}