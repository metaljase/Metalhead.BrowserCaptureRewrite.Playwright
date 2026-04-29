using Metalhead.BrowserCaptureRewrite.Abstractions.Models;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;

/// <summary>
/// Defines a service for capturing the rendered HTML of a web page using Playwright, supporting sign-in, referer, and timing
/// options.
/// </summary>
/// <remarks>
/// <para>
/// Cancellation is supported for all asynchronous operations via <see cref="CancellationToken"/>.  If cancellation is requested
/// before or during capture, an <see cref="OperationCanceledException"/> is thrown.  If the browser or page is externally
/// closed, an <see cref="OperationCanceledException"/> is also thrown.
/// </para>
/// </remarks>
public interface IPlaywrightCaptureSampleService
{
    /// <summary>
    /// Captures the rendered HTML from the specified URL, performing sign-in if required.
    /// </summary>
    /// <param name="url">The target URL to capture.  Must not be <see langword="null"/>.</param>
    /// <param name="referrerUrl">The referer URL to use for navigation, or <see langword="null"/> to omit the referer.</param>
    /// <param name="signInUrl">The URL to use for sign-in, or <see langword="null"/> to skip sign-in.</param>
    /// <param name="assumeSignedInWhenNavigatedToUrl">The URL to assume sign-in is complete when navigated to, or
    /// <see langword="null"/> to use timing-based completion.</param>
    /// <param name="signInOptions">
    /// The sign-in timing options controlling how long to wait before assuming sign-in is complete and the page load
    /// timeout.  Must not be <see langword="null"/>.
    /// </param>
    /// <param name="navigationTimingOptions">
    /// The timing options for navigation.  Must not be <see langword="null"/>.
    /// </param>
    /// <param name="captureTimingOptions">
    /// The timing and completion options for resource capture, controlling network idle detection, overall capture timeout,
    /// and polling interval.  Must not be <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.  The result is the rendered HTML string, or
    /// <see cref="string.Empty"/> if not available.
    /// </returns>
    /// <exception cref="BrowserSessionInitializationException">Thrown if browser or session initialisation fails.</exception>
    /// <exception cref="SignInException">Thrown if sign-in fails or is cancelled.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled before or during capture.</exception>
    Task<string> FetchRenderedHtmlAsync(
        Uri url,
        Uri? referrerUrl,
        Uri? signInUrl,
        Uri? assumeSignedInWhenNavigatedToUrl,
        SignInOptions signInOptions,
        NavigationTimingOptions navigationTimingOptions,
        CaptureTimingOptions captureTimingOptions,
        CancellationToken cancellationToken);
}