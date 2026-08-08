using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace OneC.Infrastructure.Com;

/// <summary>
///     Wraps the 1C V83.COMConnector COM object (dynamic, IDispatch).
///     Handles COM object lifecycle and HRESULT errors.
/// </summary>
public sealed class ComConnector : IDisposable
{
    private const string ProgId = "V83.COMConnector";

    private readonly ILogger<ComConnector> _logger;
    private dynamic? _connector;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComConnector" /> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ComConnector(ILogger<ComConnector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Creates the COM connector instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">When COM type cannot be created.</exception>
    public void Initialize()
    {
        try
        {
            var type = Type.GetTypeFromProgID(ProgId)
                       ?? throw new InvalidOperationException($"COM type '{ProgId}' is not registered.");

            _connector = Activator.CreateInstance(type);
            _logger.LogInformation("COM connector '{ProgId}' initialized.", ProgId);
        }
        catch (COMException ex)
        {
            _logger.LogError(ex, "Failed to create COM connector '{ProgId}'. HRESULT: {HResult}", ProgId, ex.HResult);
            throw new InvalidOperationException($"Failed to create COM connector '{ProgId}'. Is comcntr.dll registered?", ex);
        }
    }

    /// <summary>
    ///     Connects to a 1C database using the given connection string.
    /// </summary>
    /// <param name="connectionString">1C connection string.</param>
    /// <returns>Dynamic COM object representing the connected 1C base.</returns>
    /// <exception cref="InvalidOperationException">When connector is not initialized or connection fails.</exception>
    public dynamic Connect(string connectionString)
    {
        if (_connector is null)
        {
            throw new InvalidOperationException("COM connector is not initialized. Call Initialize() first.");
        }

        try
        {
            var connection = _connector.Connect(connectionString);
            _logger.LogInformation("Connected to 1C base.");
            return connection;
        }
        catch (COMException ex)
        {
            _logger.LogError(ex, "Failed to connect to 1C base. HRESULT: {HResult}", ex.HResult);
            throw new InvalidOperationException("Failed to connect to 1C base. Check connection string and base availability.", ex);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_connector is not null)
        {
            Marshal.ReleaseComObject(_connector);
            _connector = null;
            _logger.LogInformation("COM connector released.");
        }
    }
}