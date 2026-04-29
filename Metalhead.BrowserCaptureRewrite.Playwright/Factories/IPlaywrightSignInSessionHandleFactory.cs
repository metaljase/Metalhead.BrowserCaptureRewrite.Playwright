using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Playwright.Sessions;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Factories;

/// <summary>
/// Defines a contract for creating Playwright session handles initialised for sign-in workflows.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are responsible for initialising and configuring Playwright session handles that begin at a sign-in URL,
/// supporting engine selection, sign-in detection, and resilience.
/// </para>
/// <para>
/// Cancellation is supported via <see cref="CancellationToken"/> for all asynchronous operations.  If cancelled, operations throw
/// <see cref="OperationCanceledException"/>.
/// </para>
/// </remarks>
public interface IPlaywrightSignInSessionHandleFactory
{
    /// <summary>
    /// Creates a new Playwright session handle initialised for sign-in using the specified sign-in URL and session options.
    /// </summary>
    /// <param name="signInUrl">The URL to use for the sign-in navigation.  Must not be <see langword="null"/>.</param>
    /// <param name="options">The session options to use for browser configuration.  Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.  The task result contains the created
    /// <see cref="IPlaywrightSessionHandle"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="signInUrl"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
    Task<IPlaywrightSessionHandle> CreateSignInSessionHandleAsync(Uri signInUrl, SessionOptions options, CancellationToken cancellationToken);
}
