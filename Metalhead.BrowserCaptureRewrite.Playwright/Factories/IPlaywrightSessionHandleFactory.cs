using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Playwright.Sessions;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Factories;

/// <summary>
/// Defines a contract for creating Playwright session handles for browser automation.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are responsible for initialising and configuring Playwright session handles, which encapsulate browser and
/// context resources for advanced scenarios.
/// </para>
/// <para>
/// Cancellation is supported via <see cref="CancellationToken"/> for all asynchronous operations.  If cancelled, operations throw
/// <see cref="OperationCanceledException"/>.
/// </para>
/// </remarks>
public interface IPlaywrightSessionHandleFactory
{
    /// <summary>
    /// Creates a new Playwright session handle using the specified session options.
    /// </summary>
    /// <param name="options">The session options to use for browser configuration.  Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.  The task result contains the created
    /// <see cref="IPlaywrightSessionHandle"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
    Task<IPlaywrightSessionHandle> CreateSessionHandleAsync(SessionOptions options, CancellationToken cancellationToken);
}
