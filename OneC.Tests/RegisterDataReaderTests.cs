using Microsoft.Extensions.Logging.Abstractions;
using OneC.Domain.Register;
using OneC.Infrastructure.Com;
using OneC.Infrastructure.Readers;
using Xunit;

namespace OneC.Tests;

/// <summary>
///     Tests for <see cref="RegisterDataReader" /> price/stock building logic.
///     These methods are pure (no COM round-trips), so they can be tested directly.
/// </summary>
public class RegisterDataReaderTests
{
    private static RegisterDataReader CreateReader()
    {
        // BuildPrices/BuildStock do not touch _session.Connection, so a
        // non-connected session is fine for these tests.
        var connector = new ComConnector(NullLogger<ComConnector>.Instance);
        var session = new ComSession(connector, NullLogger<ComSession>.Instance);
        var resolver = new ReferenceResolver(session, NullLogger.Instance);
        var refCacheBuilder = new RefCacheBuilder(session, NullLogger.Instance);
        var priceTypeLoader = new PriceTypeLoader(session, NullLogger.Instance);
        var priceLoader = new PriceLoader(session, resolver, NullLogger.Instance);
        var stockLoader = new StockLoader(session, resolver, NullLogger.Instance);
        var lastMovementLoader = new LastMovementLoader(session, resolver, NullLogger.Instance);
        var priceCalculator = new PriceCalculator(NullLogger.Instance);
        var stockBuilder = new StockBuilder(NullLogger.Instance);
        return new RegisterDataReader(
            priceTypeLoader,
            refCacheBuilder,
            priceLoader,
            stockLoader,
            lastMovementLoader,
            priceCalculator,
            stockBuilder,
            NullLogger<RegisterDataReader>.Instance);
    }

    private static PriceTypeInfo PriceType(
        string guid,
        string name,
        bool isCalculated = false,
        decimal markupPct = 0m)
    {
        return new PriceTypeInfo(guid, name, null, isCalculated, markupPct);
    }

    private static Dictionary<string, Dictionary<string, PriceRow>> PricesByItem(
        string itemGuid,
        params (string TypeGuid, decimal Price, decimal MarkupPct, string Unit, string Period)[] rows)
    {
        var result = new Dictionary<string, Dictionary<string, PriceRow>>(StringComparer.OrdinalIgnoreCase);
        var types = new Dictionary<string, PriceRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            types[row.TypeGuid] = new PriceRow(row.Price, row.MarkupPct, row.Unit, row.Period);
        }

        result[itemGuid] = types;
        return result;
    }

    [Fact]
    public void BuildPrices_ItemWithoutPrices_ReturnsEmpty()
    {
        var reader = CreateReader();
        var priceTypes = new Dictionary<string, PriceTypeInfo>(StringComparer.OrdinalIgnoreCase);
        var prices = PricesByItem("other-item");

        var result = reader.BuildPrices("item-1", priceTypes, prices);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildPrices_CostPriceType_ReturnsBaseCostAsPrice()
    {
        var reader = CreateReader();
        var costType = PriceType("guid-cost", "Цена закупочная");
        var priceTypes = new Dictionary<string, PriceTypeInfo>(StringComparer.OrdinalIgnoreCase)
        {
            [costType.Guid] = costType,
        };
        var prices = PricesByItem(
            "item-1",
            ("guid-cost", 150m, 0m, "шт", "2026-08-01 00:00:00"));

        var result = reader.BuildPrices("item-1", priceTypes, prices);

        var entry = Assert.Single(result);
        Assert.Equal("Цена закупочная", entry.PriceType);
        Assert.Equal(150m, entry.Price);
        Assert.Equal(150m, entry.BaseCost); // base_cost = price for cost type
        Assert.Equal(0m, entry.MarkupPct);
        Assert.Equal("шт", entry.Unit);
    }

    [Fact]
    public void BuildPrices_CalculatedType_ComputesPriceWithMarkup()
    {
        var reader = CreateReader();
        var costType = PriceType("guid-cost", "Цена закупочная");
        var retailType = PriceType("guid-retail", "Розничная", isCalculated: true, markupPct: 30m);
        var priceTypes = new Dictionary<string, PriceTypeInfo>(StringComparer.OrdinalIgnoreCase)
        {
            [costType.Guid] = costType,
            [retailType.Guid] = retailType,
        };
        var prices = PricesByItem(
            "item-1",
            ("guid-cost", 100m, 0m, "шт", "2026-08-01 00:00:00"),
            ("guid-retail", 0m, 30m, "шт", "2026-08-01 00:00:00"));

        var result = reader.BuildPrices("item-1", priceTypes, prices);

        Assert.Collection(result,
            entry => Assert.Equal("Цена закупочная", entry.PriceType),
            entry =>
            {
                Assert.Equal("Розничная", entry.PriceType);
                Assert.Equal(130m, entry.Price); // 100 * (1 + 30/100)
                Assert.Equal(0m, entry.BaseCost);
                Assert.Equal(30m, entry.MarkupPct);
            });
    }

    [Fact]
    public void BuildPrices_CalculatedTypeWithoutCost_SkipsEntry()
    {
        var reader = CreateReader();
        var retailType = PriceType("guid-retail", "Розничная", isCalculated: true, markupPct: 30m);
        var priceTypes = new Dictionary<string, PriceTypeInfo>(StringComparer.OrdinalIgnoreCase)
        {
            [retailType.Guid] = retailType,
        };
        var prices = PricesByItem(
            "item-1",
            ("guid-retail", 0m, 30m, "шт", "2026-08-01 00:00:00"));

        var result = reader.BuildPrices("item-1", priceTypes, prices);

        // No cost price → calculated types are skipped.
        Assert.Empty(result);
    }

    [Fact]
    public void BuildStock_ItemWithRows_ReturnsEntriesWithLastMovement()
    {
        var reader = CreateReader();
        var stockByItem = new Dictionary<string, List<StockRow>>(StringComparer.OrdinalIgnoreCase)
        {
            ["item-1"] = new()
            {
                new StockRow("wh-1", "Склад 1", 10m),
                new StockRow("wh-2", "Склад 2", 5m),
            },
        };
        var lastMovements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["item-1_wh-1"] = "2026-08-01 10:00:00",
        };

        var result = reader.BuildStock("item-1", stockByItem, lastMovements);

        Assert.Collection(result,
            entry =>
            {
                Assert.Equal("Склад 1", entry.Warehouse);
                Assert.Equal(10m, entry.Quantity);
                Assert.Equal("2026-08-01 10:00:00", entry.LastMovement);
            },
            entry =>
            {
                Assert.Equal("Склад 2", entry.Warehouse);
                Assert.Equal(5m, entry.Quantity);
                Assert.Equal(string.Empty, entry.LastMovement); // no movement for wh-2
            });
    }

    [Fact]
    public void BuildStock_ItemWithoutRows_ReturnsEmpty()
    {
        var reader = CreateReader();
        var stockByItem = new Dictionary<string, List<StockRow>>(StringComparer.OrdinalIgnoreCase);
        var lastMovements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var result = reader.BuildStock("item-1", stockByItem, lastMovements);

        Assert.Empty(result);
    }
}