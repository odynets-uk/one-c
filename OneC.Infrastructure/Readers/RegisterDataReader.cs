using Microsoft.Extensions.Logging;
using OneC.Application.Abstractions;
using OneC.Domain.Register;

namespace OneC.Infrastructure.Readers;

/// <summary>
///     Facade for reading price and stock data from 1C registers via COM.
///     Composes specialized loaders and business logic components.
/// </summary>
public sealed class RegisterDataReader : IRegisterDataReader
{
    private readonly PriceTypeLoader _priceTypeLoader;
    private readonly RefCacheBuilder _refCacheBuilder;
    private readonly PriceLoader _priceLoader;
    private readonly StockLoader _stockLoader;
    private readonly LastMovementLoader _lastMovementLoader;
    private readonly PriceCalculator _priceCalculator;
    private readonly StockBuilder _stockBuilder;
    private readonly ILogger _logger;

    public RegisterDataReader(
        PriceTypeLoader priceTypeLoader,
        RefCacheBuilder refCacheBuilder,
        PriceLoader priceLoader,
        StockLoader stockLoader,
        LastMovementLoader lastMovementLoader,
        PriceCalculator priceCalculator,
        StockBuilder stockBuilder,
        ILogger<RegisterDataReader> logger)
    {
        _priceTypeLoader = priceTypeLoader;
        _refCacheBuilder = refCacheBuilder;
        _priceLoader = priceLoader;
        _stockLoader = stockLoader;
        _lastMovementLoader = lastMovementLoader;
        _priceCalculator = priceCalculator;
        _stockBuilder = stockBuilder;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<PriceTypeInfo> LoadPriceTypes()
    {
        return _priceTypeLoader.LoadPriceTypes();
    }

    /// <inheritdoc />
    public RefCache BuildRefCache(IReadOnlyCollection<string> itemGuids, string catalogName)
    {
        return _refCacheBuilder.BuildRefCache(itemGuids, catalogName);
    }

    /// <inheritdoc />
    public Dictionary<string, Dictionary<string, PriceRow>> LoadPrices(
        IReadOnlyCollection<string> itemGuids,
        RefCache refCache,
        string? changedSince = null)
    {
        return _priceLoader.LoadPrices(itemGuids, refCache, changedSince);
    }

    /// <inheritdoc />
    public Dictionary<string, List<StockRow>> LoadStock(
        IReadOnlyCollection<string> itemGuids,
        RefCache refCache)
    {
        return _stockLoader.LoadStock(itemGuids, refCache);
    }

    /// <inheritdoc />
    public Dictionary<string, string> LoadLastMovements(
        IReadOnlyCollection<string> itemGuids,
        RefCache refCache,
        string? changedSince = null)
    {
        return _lastMovementLoader.LoadLastMovements(itemGuids, refCache, changedSince);
    }

    /// <inheritdoc />
    public List<PriceEntry> BuildPrices(
        string itemGuid,
        IReadOnlyDictionary<string, PriceTypeInfo> priceTypesByGuid,
        Dictionary<string, Dictionary<string, PriceRow>> pricesByItem)
    {
        return _priceCalculator.BuildPrices(itemGuid, priceTypesByGuid, pricesByItem);
    }

    /// <inheritdoc />
    public List<StockEntry> BuildStock(
        string itemGuid,
        Dictionary<string, List<StockRow>> stockByItem,
        Dictionary<string, string> lastMovements)
    {
        return _stockBuilder.BuildStock(itemGuid, stockByItem, lastMovements);
    }
}