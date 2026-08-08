using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OneC.Infrastructure.Com;

/// <summary>
///     Registers the 1C COM connector library (comcntr.dll) via regsvr32.exe.
///     The user usually registers it manually; this is an optional convenience.
/// </summary>
public sealed class ComRegistrationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ComRegistrationService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComRegistrationService" /> class.
    /// </summary>
    /// <param name="configuration">Configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public ComRegistrationService(IConfiguration configuration, ILogger<ComRegistrationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    ///     Ensures the COM connector library is registered, if enabled in configuration.
    /// </summary>
    public void EnsureRegisteredOnStartup()
    {
        var enabled = _configuration.GetValue<bool>("ComRegistration:EnsureRegisteredOnStartup");
        if (!enabled)
        {
            return;
        }

        var regsvr32Path = _configuration["ComRegistration:Regsvr32Path"];
        var libraryPath = _configuration["ComRegistration:LibraryPath"];

        if (string.IsNullOrWhiteSpace(regsvr32Path) || string.IsNullOrWhiteSpace(libraryPath))
        {
            _logger.LogWarning("ComRegistration paths are not configured. Skipping COM registration.");
            return;
        }

        // Check if the library is already registered by trying to resolve ProgID.
        if (Type.GetTypeFromProgID("V83.COMConnector") is not null)
        {
            _logger.LogInformation("COM connector is already registered.");
            return;
        }

        _logger.LogInformation("Registering COM connector from {LibraryPath}...", libraryPath);
        Register(regsvr32Path, libraryPath);
    }

    private void Register(string regsvr32Path, string libraryPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = regsvr32Path,
                Arguments = $"/s \"{libraryPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(30_000);

            if (process is null)
            {
                _logger.LogError("Failed to start regsvr32.exe.");
                return;
            }

            if (process.ExitCode != 0)
            {
                _logger.LogError("regsvr32.exe exited with code {ExitCode}.", process.ExitCode);
                return;
            }

            _logger.LogInformation("COM connector registered successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register COM connector: {ErrorMessage}", ex.Message);
        }
    }
}