using OneC.Application.Abstractions.Services;

namespace OneC.Cli;

/// <summary>
///     Represents CLI application entry service.
/// </summary>
public sealed class CliApp
{
    private readonly ISomeService _someService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CliApp" /> class.
    /// </summary>
    /// <param name="someService">Application service.</param>
    public CliApp(ISomeService someService)
    {
        _someService = someService;
    }

    /// <summary>
    ///     Runs the CLI application.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        await _someService.ExecuteAsync(cancellationToken);
    }
}