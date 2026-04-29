using Microsoft.Playwright;

using Metalhead.BrowserCaptureRewrite.Abstractions.Enums;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Services;

public interface IPlaywrightPageCaptureService
{
    /// <summary>
    /// Navigates the specified page to a URL and captures selected content and resources.
    /// </summary>
    /// <param name="page">The Playwright page instance to use for navigation and capture.  Must not be <see langword="null"/>.</param>
    /// <param name="captureParts">Specifies which parts of the page to capture (e.g., response HTML, rendered HTML, resources).</param>
    /// <param name="navOptions">Navigation options, including the target URL and optional referer.  Must not be <see langword="null"/>.</param>
    /// <param name="captureSpec">Resource capture specification.  Required if <paramref name="captureParts"/> includes resources;
    /// otherwise, may be <see langword="null"/>.</param>
    /// <param name="captureTimingOptions">Timing and completion options for navigation and resource capture.  Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.  Cancels navigation and capture if triggered.</param>
    /// <returns>
    /// A <see cref="PageCaptureResult"/> containing the captured response HTML, rendered HTML, captured resources, and status information.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="page"/>, <paramref name="navOptions"/>, <c>navOptions.Url</c>, or <paramref name="captureTimingOptions"/>
    /// is <see langword="null"/>,
    /// or if <paramref name="captureSpec"/> is <see langword="null"/> when resources are to be captured.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="captureParts"/> is <see cref="PageCaptureParts.None"/>.
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// Thrown if the navigation response has a retryable or 404 HTTP status code.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation is cancelled via <paramref name="cancellationToken"/> or if the browser/page is closed during navigation or capture.
    /// </exception>
    /// <remarks>
    /// <para>
    /// When capturing resources, completion can be determined by network idle, a custom predicate, or a timeout, as specified in
    /// <paramref name="captureTimingOptions"/> and <paramref name="captureSpec"/>.
    /// </para>
    /// <para>
    /// The method is resilient to transient browser closure and cancellation, and logs debug information for capture progress and errors.
    /// </para>
    /// </remarks>
    Task<PageCaptureResult> NavigateAndCaptureResultAsync(
        IPage page,
        PageCaptureParts captureParts,
        NavigationOptions navOptions,
        CaptureSpec? captureSpec,
        CaptureTimingOptions captureTimingOptions,
        CancellationToken cancellationToken);

    /// <summary>
    /// Navigates the specified page to a URL and captures selected content and resources, optionally rewriting HTTP responses.
    /// </summary>
    /// <param name="page">The Playwright page instance to use for navigation and capture.  Must not be <see langword="null"/>.</param>
    /// <param name="captureParts">Specifies which parts of the page to capture (e.g., response HTML, rendered HTML, resources).</param>
    /// <param name="navOptions">Navigation options, including the target URL and optional referer.  Must not be <see langword="null"/>.</param>
    /// <param name="captureSpec">Resource capture specification.  Required if <paramref name="captureParts"/> includes resources;
    /// otherwise, may be <see langword="null"/>.</param>
    /// <param name="rewriteSpec">Optional specification for rewriting HTTP responses during navigation and resource capture.</param>
    /// <param name="captureTimingOptions">Timing and completion options for navigation and resource capture.  Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.  Cancels navigation and capture if triggered.</param>
    /// <returns>
    /// A <see cref="PageCaptureResult"/> containing the captured response HTML, rendered HTML, captured resources, and status information.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="page"/>, <paramref name="navOptions"/>, <c>navOptions.Url</c>, or <paramref name="captureTimingOptions"/>
    /// is <see langword="null"/>,
    /// or if <paramref name="captureSpec"/> is <see langword="null"/> when resources are to be captured.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="captureParts"/> is <see cref="PageCaptureParts.None"/>.
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// Thrown if the navigation response has a retryable or 404 HTTP status code.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation is cancelled via <paramref name="cancellationToken"/> or if the browser/page is closed during navigation or capture.
    /// </exception>
    /// <remarks>
    /// <para>
    /// If <paramref name="rewriteSpec"/> is provided, HTTP responses matching the rewrite criteria will be intercepted and
    /// optionally rewritten.
    /// </para>
    /// <para>
    /// When capturing resources, completion can be determined by network idle, a custom predicate, or a timeout, as specified in
    /// <paramref name="captureTimingOptions"/> and <paramref name="captureSpec"/>.
    /// </para>
    /// <para>
    /// The method is resilient to transient browser closure and cancellation, and logs debug information for capture progress and errors.
    /// </para>
    /// </remarks>
    Task<PageCaptureResult> NavigateAndCaptureResultAsync(
        IPage page,
        PageCaptureParts captureParts,
        NavigationOptions navOptions,
        CaptureSpec? captureSpec,
        RewriteSpec? rewriteSpec,
        CaptureTimingOptions captureTimingOptions,
        CancellationToken cancellationToken);
}
