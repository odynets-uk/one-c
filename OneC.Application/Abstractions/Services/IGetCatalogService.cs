using OneC.Domain.Profiles;

namespace OneC.Application.Abstractions.Services;

/// <summary>
///     Represents a service that reads catalog data from 1C using a profile.
/// </summary>
public interface IGetCatalogService
{
    /// <summary>
    ///     Reads catalog data according to the profile and outputs to JSON/SQLite.
    /// </summary>
    /// <param name="profile">Extraction profile.</param>
    /// <param name="batchSize">Batch size (-1 = all records).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of records read.</returns>
    Task<int> ExecuteAsync(
        ExtractionProfile profile,
        int batchSize = -1,
        CancellationToken cancellationToken = default);
}