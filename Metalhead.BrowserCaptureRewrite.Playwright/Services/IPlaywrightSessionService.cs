using Metalhead.BrowserCaptureRewrite.Playwright.Sessions;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Services;

/// <summary>
/// Defines a service for creating Playwright browser session handles, supporting both standard and sign-in flows.
/// </summary>
/// <remarks>
/// <para>
/// Implementations must provide asynchronous session handle creation, optionally supporting sign-in navigation and completion detection.
/// </para>
/// <para>
/// Cancellation is supported via <see cref="CancellationToken"/> for all asynchronous operations.  If cancellation is requested before or during
/// session creation, an <see cref="OperationCanceledException"/> is thrown.  If the browser or page is externally closed, an
/// <see cref="OperationCanceledException"/> is also thrown.
/// </para>
/// </remarks>
public interface IPlaywrightSessionService
{
    /// <summary>
    /// Creates a new Playwright session handle, optionally performing a sign-in flow if a sign-in URL is provided.
    /// </summary>
    /// <param name="signInUrl">The URL to use for sign-in.  If <see langword="null"/>, a standard session is created.</param>
    /// <param name="signedInUrl">The URL to assume sign-in is complete when navigated to.  May be <see langword="null"/>.</param>
    /// <param name="signedInAfter">The duration to wait before assuming sign-in is complete, if <paramref name="signedInUrl"/> is
    /// <see langword="null"/>.  May be <see langword="null"/>.</param>
    /// <param name="signInPageLoadTimeout">The timeout for loading the sign-in page.  May be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.  The task result is an <see cref="IPlaywrightSessionHandle"/> representing the created
    /// session handle.
    /// </returns>
    /// <exception cref="SignInException">Thrown if sign-in fails or is cancelled.</exception>
    /// <exception cref="BrowserSessionInitializationException">Thrown for general session creation failures, including when the
    /// browser engine is not available.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled before or during session creation.</exception>
    /// <remarks>
    /// <para>
    /// If <paramref name="signInUrl"/> is provided, a sign-in session handle is created.  Otherwise, a standard session handle is created.
    /// </para>
    /// </remarks>
    Task<IPlaywrightSessionHandle> CreatePlaywrightSessionOrThrowAsync(
        Uri? signInUrl,
        Uri? signedInUrl,
        TimeSpan? signedInAfter,
        TimeSpan? signInPageLoadTimeout,
        CancellationToken cancellationToken);
}
