using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Models;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;

/// <summary>
/// Defines a service for capturing bands and albums from a web page using resource capture strategies, with support for sign-in,
/// referer, timing, resource capture, and optional response rewriting.
/// </summary>
/// <remarks>
/// <para>
/// Implementations must provide asynchronous capture of bands and albums, supporting sign-in, referer, timing, resource capture,
/// and optional response rewriting.
/// </para>
/// <para>
/// Cancellation is supported for all asynchronous operations via <see cref="CancellationToken"/>.  If cancellation is requested
/// before or during capture, an <see cref="OperationCanceledException"/> is thrown.  If the browser or page is externally closed,
/// an <see cref="OperationCanceledException"/> is also thrown.
/// </para>
/// </remarks>
public interface IResourceCaptureSampleService
{
    /// <summary>
    /// Captures bands and albums from the specified URL, performing sign-in if required, and applying resource and response
    /// rewrite specifications.
    /// </summary>
    /// <param name="url">The target URL to capture resources from.  Must not be <see langword="null"/>.</param>
    /// <param name="referrerUrl">The referer URL to use for navigation, or <see langword="null"/> to omit the referer.</param>
    /// <param name="signInUrl">The URL to use for sign-in, or <see langword="null"/> to skip sign-in.</param>
    /// <param name="assumeSignedInWhenNavigatedToUrl">
    /// The URL to assume sign-in is complete when navigated to, or <see langword="null"/> to use timing-based completion.
    /// </param>
    /// <param name="navigationTimingOptions">
    /// The timing options for navigation.  Must not be <see langword="null"/>.
    /// </param>
    /// <param name="captureTimingOptions">
    /// The timing and completion options for resource capture, controlling network idle detection, overall capture timeout,
    /// and polling interval.  Must not be <see langword="null"/>.
    /// </param>
    /// <param name="captureSpec">The resource capture specification to use.  Must not be <see langword="null"/>.</param>
    /// <param name="rewriteSpec">
    /// Optional.  The response rewrite specification to use, or <see langword="null"/> for no rewriting.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A tuple containing the parsed <see cref="Bands"/> and <see cref="Albums"/> models.
    /// </returns>
    /// <exception cref="BrowserSessionInitializationException">Thrown if browser or session initialisation fails.</exception>
    /// <exception cref="SignInException">Thrown if sign-in fails or is cancelled.</exception>
    /// <exception cref="PageCaptureIncompleteException">Thrown if the page capture does not complete successfully.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled before or during capture.</exception>
    Task<(Bands Bands, Albums Albums)> CaptureBandsAndAlbumsAsync(
        Uri url,
        Uri? referrerUrl,
        Uri? signInUrl,
        Uri? assumeSignedInWhenNavigatedToUrl,
        SignInOptions signInOptions,
        NavigationTimingOptions navigationTimingOptions,
        CaptureTimingOptions captureTimingOptions,
        CaptureSpec captureSpec,
        RewriteSpec? rewriteSpec,
        CancellationToken cancellationToken);

    /// <summary>
    /// Captures bands and albums from the specified URL using file extensions, with optional completion predicate and response
    /// rewriting.
    /// </summary>
    /// <param name="url">The target URL to capture resources from.  Must not be <see langword="null"/>.</param>
    /// <param name="referrerUrl">The referer URL to use for navigation, or <see langword="null"/> to omit the referer.</param>
    /// <param name="signInUrl">The URL to use for sign-in, or <see langword="null"/> to skip sign-in.</param>
    /// <param name="assumeSignedInWhenNavigatedToUrl">
    /// The URL to assume sign-in is complete when navigated to, or <see langword="null"/> to use timing-based completion.
    /// </param>
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
    /// <param name="rewriteSpec">
    /// Optional.  The response rewrite specification to use, or <see langword="null"/> for no rewriting.
    /// </param>
    /// <param name="shouldCompleteCapture">
    /// Optional.  A predicate to determine when capture is complete, or <see langword="null"/> to use default completion logic.
    /// </param>
    /// <param name="extensions">
    /// The file extensions to capture (e.g., ".json", ".m3u8").  Must not be <see langword="null"/> or empty.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A tuple containing the parsed <see cref="Bands"/> and <see cref="Albums"/> models.
    /// </returns>
    /// <exception cref="BrowserSessionInitializationException">Thrown if browser or session initialisation fails.</exception>
    /// <exception cref="SignInException">Thrown if sign-in fails or is cancelled.</exception>
    /// <exception cref="PageCaptureIncompleteException">Thrown if the page capture does not complete successfully.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled before or during capture.</exception>
    Task<(Bands Bands, Albums Albums)> CaptureBandsAndAlbumsAsync(
        Uri url,
        Uri? referrerUrl,
        Uri? signInUrl,
        Uri? assumeSignedInWhenNavigatedToUrl,
        SignInOptions signInOptions,
        NavigationTimingOptions navigationTimingOptions,
        CaptureTimingOptions captureTimingOptions,
        RewriteSpec? rewriteSpec,
        Func<NavigationOptions, IReadOnlyList<CapturedResource>, DateTime, bool>? shouldCompleteCapture,
        string[] extensions,
        CancellationToken cancellationToken);

    /// <summary>
    /// Captures bands and albums from the specified URL using a list of URLs to capture, with optional response rewriting.
    /// </summary>
    /// <param name="url">The target URL to capture resources from.  Must not be <see langword="null"/>.</param>
    /// <param name="referrerUrl">The referer URL to use for navigation, or <see langword="null"/> to omit the referer.</param>
    /// <param name="signInUrl">The URL to use for sign-in, or <see langword="null"/> to skip sign-in.</param>
    /// <param name="assumeSignedInWhenNavigatedToUrl">
    /// The URL to assume sign-in is complete when navigated to, or <see langword="null"/> to use timing-based completion.
    /// </param>
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
    /// <param name="rewriteSpec">
    /// Optional.  The response rewrite specification to use, or <see langword="null"/> for no rewriting.
    /// </param>
    /// <param name="urlsToCapture">The list of URLs to capture.  Must not be <see langword="null"/> or empty.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A tuple containing the parsed <see cref="Bands"/> and <see cref="Albums"/> models.
    /// </returns>
    /// <exception cref="BrowserSessionInitializationException">Thrown if browser or session initialisation fails.</exception>
    /// <exception cref="SignInException">Thrown if sign-in fails or is cancelled.</exception>
    /// <exception cref="PageCaptureIncompleteException">Thrown if the page capture does not complete successfully.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled before or during capture.</exception>
    Task<(Bands Bands, Albums Albums)> CaptureBandsAndAlbumsAsync(
        Uri url,
        Uri? referrerUrl,
        Uri? signInUrl,
        Uri? assumeSignedInWhenNavigatedToUrl,
        SignInOptions signInOptions,
        NavigationTimingOptions navigationTimingOptions,
        CaptureTimingOptions captureTimingOptions,
        RewriteSpec? rewriteSpec,
        Uri[] urlsToCapture,
        CancellationToken cancellationToken);
}
