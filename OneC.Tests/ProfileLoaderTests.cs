using System.Text.Json;
using OneC.Domain.Profiles;
using OneC.Infrastructure.Profiles;
using Xunit;

namespace OneC.Tests;

/// <summary>
///     Tests for <see cref="ProfileLoader" />.
/// </summary>
public class ProfileLoaderTests
{
    [Fact]
    public void Parse_ValidProfile_LoadsAllSections()
    {
        const string json = """
                            {
                              "name": "products",
                              "rootType": "CatalogObject.Номенклатура",
                              "output": { "path": "export_{date}.json", "pretty": true },
                              "filters": {
                                "prices": { "changedSince": null, "priceTypes": [], "excludeZeroPrice": true },
                                "stock": { "changedSince": null, "warehouses": [], "statusCodes": [], "onlyPositive": true },
                                "items": { "codes": [], "artikuls": [], "guids": [], "nameContains": "" }
                              },
                              "includeFields": {
                                "item": ["item_code", "item_artikul", "item_guid", "item_name"],
                                "price": ["price_type", "price", "base_cost", "markup_pct", "unit", "timestamp"],
                                "stock": ["warehouse", "status_code", "quantity", "last_movement"]
                              },
                              "skipItemsWithoutPrices": false,
                              "skipItemsWithoutStock": false
                            }
                            """;

        var profile = ProfileLoader.Parse(json, "products");

        Assert.Equal("products", profile.Name);
        Assert.Equal("CatalogObject.Номенклатура", profile.RootType);
        Assert.NotNull(profile.Output);
        Assert.Equal("export_{date}.json", profile.Output!.Path);
        Assert.True(profile.Output.Pretty);
        Assert.NotNull(profile.Filters);
        Assert.True(profile.Filters!.Prices!.ExcludeZeroPrice);
        Assert.True(profile.Filters.Stock!.OnlyPositive);
        Assert.Equal(4, profile.IncludeFields!.Item.Count);
        Assert.Equal(6, profile.IncludeFields.Price.Count);
        Assert.Equal(4, profile.IncludeFields.Stock.Count);
    }

    [Fact]
    public void Parse_WithoutName_UsesFileName()
    {
        const string json = """{ "rootType": "CatalogObject.Номенклатура" }""";

        var profile = ProfileLoader.Parse(json, "catalog");

        Assert.Equal("catalog", profile.Name);
    }

    [Fact]
    public void Parse_WithoutRootType_Throws()
    {
        const string json = """{ "name": "test" }""";

        Assert.Throws<JsonException>(() => ProfileLoader.Parse(json, "test"));
    }

    [Fact]
    public void Parse_EmptyJson_Throws()
    {
        Assert.Throws<JsonException>(() => ProfileLoader.Parse("{}", "test"));
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => ProfileLoader.Load("nonexistent-profile.json"));
    }
}