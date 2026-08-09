using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace OneC.Infrastructure.Com;

/// <summary>
///     Manages the lifecycle of a 1C COM session: connect, query, dispose.
///     Uses dynamic binding with Latin method names (NewObject, Query, etc.).
/// </summary>
public sealed class ComSession : IDisposable
{
    private readonly ComConnector _connector;
    private readonly ILogger<ComSession> _logger;
    private dynamic? _connection;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComSession" /> class.
    /// </summary>
    /// <param name="connector">COM connector.</param>
    /// <param name="logger">Logger instance.</param>
    public ComSession(ComConnector connector, ILogger<ComSession> logger)
    {
        _connector = connector;
        _logger = logger;
    }

    /// <summary>
    ///     Gets the underlying dynamic connection object.
    /// </summary>
    public dynamic Connection => _connection
                                 ?? throw new InvalidOperationException("Session is not connected. Call Connect() first.");

    /// <summary>
    ///     Connects to a 1C database.
    /// </summary>
    /// <param name="connectionString">1C connection string.</param>
    public void Connect(string connectionString)
    {
        _connector.Initialize();
        _connection = _connector.Connect(connectionString);
        _logger.LogInformation("COM session established.");
    }

    /// <summary>
    ///     Converts a COM value (e.g. Ref/GUID) to a string using the 1C String() function.
    /// </summary>
    /// <param name="value">COM value.</param>
    /// <returns>String representation.</returns>
    public string String(object value)
    {
        var result = Connection.String(value);
        return result?.ToString() ?? string.Empty;
    }

    /// <summary>
    ///     Tests the connection by executing a simple query (SELECT 1).
    ///     This verifies the COM connection and query execution mechanism.
    /// </summary>
    /// <returns>True if the query executed successfully.</returns>
    public bool TestConnection()
    {
        try
        {
            // Create a Query object inside the COM connection.
            var query = Connection.NewObject("Query");
            query.Text = "SELECT 1 AS Test";

            // Execute the query.
            var result = query.Execute();

            if (result.IsEmpty())
            {
                _logger.LogWarning("Test query returned empty result.");
                return false;
            }

            var selection = result.Choose();
            selection.Next();

            var testValue = selection.Test;
            _logger.LogInformation("Test query executed successfully. Result: {TestValue}", (object?)testValue);

            return true;
        }
        catch (COMException ex)
        {
            _logger.LogError(ex, "Test query failed. HRESULT: {HResult}", ex.HResult);
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_connection is not null)
        {
            Marshal.ReleaseComObject(_connection);
            _connection = null;
            _logger.LogInformation("COM session disposed.");
        }

        _connector.Dispose();
    }
}