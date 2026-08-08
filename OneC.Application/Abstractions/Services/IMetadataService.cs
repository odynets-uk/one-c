using OneC.Domain.Metadata;

namespace OneC.Application.Abstractions.Services;

/// <summary>
///     Represents a service that provides 1C metadata (catalogs, enums) from the XSD schema.
/// </summary>
public interface IMetadataService
{
    /// <summary>
    ///     Loads the metadata model (parses XSD or loads from cache).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata model.</returns>
    Task<MetadataModel> GetMetadataAsync(CancellationToken cancellationToken = default);
}