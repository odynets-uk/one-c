using Microsoft.Extensions.Logging;

namespace OneC.Infrastructure;

/// <summary>
///     Provides a factory for creating loggers outside the DI container
///     (e.g. for short-lived COM sessions).
/// </summary>
public static class Logging
{
    private static ILoggerFactory? _factory;

    /// <summary>
    ///     Creates a logger of the specified type.
    /// </summary>
    /// <typeparam name="T">Type for which the logger is created.</typeparam>
    /// <returns>Logger instance.</returns>
    public static ILogger<T> CreateLogger<T>()
    {
        _factory ??= LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });

        return _factory.CreateLogger<T>();
    }
}
