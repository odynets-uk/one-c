namespace OneC.Application.Abstractions.Services;

/// <summary>
///     Represents example application service contract.
/// </summary>
public interface ISomeService
{
    /// <summary>
    ///     Executes application use case.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}