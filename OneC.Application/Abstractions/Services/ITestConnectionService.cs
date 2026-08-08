namespace OneC.Application.Abstractions.Services;

/// <summary>
///     Represents a service that tests the 1C COM connection.
/// </summary>
public interface ITestConnectionService
{
    /// <summary>
    ///     Tests the connection to the 1C base and returns platform/configuration versions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Connection test result.</returns>
    Task<TestConnectionResult> TestAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Result of a 1C connection test.
/// </summary>
/// <param name="PlatformVersion">1C platform version.</param>
/// <param name="ConfigurationVersion">1C configuration version.</param>
public sealed record TestConnectionResult(string PlatformVersion, string ConfigurationVersion);