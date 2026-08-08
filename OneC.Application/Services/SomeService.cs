using OneC.Application.Abstractions.Services;

namespace OneC.Application.Services;

/// <summary>
///     Default implementation of application service.
/// </summary>
public sealed class SomeService : ISomeService
{
    /// <inheritdoc />
    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Application service executed.");
        return Task.CompletedTask;
    }
}