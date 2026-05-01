using Metalhead.BrowserCaptureRewrite.Abstractions.Helpers;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Transport;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Factories;

/// <summary>
/// Provides a factory for creating <see cref="CaptureSpec"/> instances that capture resources based on specified URLs.
/// </summary>
/// <remarks>
/// Implements <see cref="ICaptureSpecFactoryByUrls"/>.
/// <para>
/// The created <see cref="CaptureSpec"/> uses URL-based logic to filter and complete resource capture, ensuring only
/// resources at the specified URLs are captured and that capture completes when all expected URLs are present.
/// </para>
/// <para>
/// Cancellation is supported for all asynchronous operations within the created <see cref="CaptureSpec"/> delegates
/// via <see cref="CancellationToken"/>.  If cancellation is requested, in-flight work is stopped and an
/// <see cref="OperationCanceledException"/> is thrown.
/// </para>
/// </remarks>
public sealed class CaptureSpecFactoryByUrls() : ICaptureSpecFactoryByUrls
{
    /// <inheritdoc/>
    public CaptureSpec CreateSpecForBandsAndAlbumsByUrl(IReadOnlyList<Uri> urlsToCapture) =>
        new(shouldCapture: req => urlsToCapture.Contains(UriHelper.ParseAbsoluteUrl(req.Url)),
            tryCreateCapturedResourceAsync: (req, resp) => TryCreateCapturedResourceAsync(req, resp),
            shouldCompleteCapture: CreateCompletionPredicateByUrl(urlsToCapture));

    /// <summary>
    /// Attempts to create a <see cref="CapturedResource"/> from the specified request and response.
    /// </summary>
    /// <param name="req">
    /// The <see cref="IRequestInfo"/> representing the HTTP request.  Must not be <see langword="null"/>.
    /// The URL must be absolute.
    /// </param>
    /// <param name="resp">
    /// The <see cref="IResponseInfo"/> representing the HTTP response.  Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.  The result is a <see cref="CapturedResource"/>
    /// if the response can be processed, or <see langword="null"/> if the request URL is invalid or the response body
    /// cannot be read.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is intended to be used as the <c>tryCreateCapturedResourceAsync</c> delegate for a
    /// <see cref="CaptureSpec"/>.  It attempts to read the response body as a string and constructs a
    /// <see cref="CapturedResource"/> with the request URL, response body, content type, status code, and headers.
    /// </para>
    /// <para>
    /// If the request URL is not a valid absolute URI, or if the response body cannot be read, the method returns
    /// <see langword="null"/>.  The <c>shouldCapture</c> predicate is expected to ensure only relevant URLs are processed.
    /// </para>
    /// <para>
    /// Cancellation is supported if the underlying <see cref="IResponseInfo.GetBodyAsStringAsync"/> implementation supports
    /// <see cref="CancellationToken"/>.
    /// </para>
    /// </remarks>
    public static async Task<CapturedResource?> TryCreateCapturedResourceAsync(IRequestInfo req, IResponseInfo resp)
    {
        // This method is supplied as the tryCreateCapturedResourceAsync delegate, and is invoked for each response that matches the shouldCapture
        // predicate.  The response should be examined to determine if it contains data you want to keep; if it does, return it as a CapturedResource.
        //
        // It's OK to return a CapturedResource containing a response you're NOT interested in, as this can be filtered out later, so your
        // logic can be relatively loose, but this will use more memory than necessary, and may affect performance for large responses.
        // For this example, the shouldCapture predicate ensures only the relevant URLs are intercepted, so examining the content isn't necessary.

        resp.Headers.TryGetValue("content-type", out var contentType);
        if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var requestUri))
            return null;

        string text;
        try
        {
            text = await resp.GetBodyAsStringAsync().ConfigureAwait(false);
        }
        catch { return null; }

        // The shouldCapture predicate ensures this is a URL we want to capture, so no need to examine the response body.
        return new CapturedResource(requestUri, text, null, contentType, resp.StatusCode, resp.Headers);
    }

    // Creates the capture-completion predicate delegate.  The returned function is invoked regularly during the capture process, and is passed
    // all the resources captured so far, allowing you to determine whether or not ALL the resources you're interested in have been captured.
    // If a completion predicate is not provided, then the capture process will complete once a duration of 500ms without any network taffic has
    // been observed.  Therefore, for many web pages, a completion predicate will not be necessary.  If the web page fetches resources you want
    // to capture after 500ms of zero network has been observed, a capture predicate will be necessary; allowing you to control when the capture
    // process should complete.
    // Return true if capturing is complete, or false if the capture process should continue because not all resources have been captured yet.
    private static Func<NavigationOptions, IReadOnlyList<CapturedResource>, DateTime, bool> CreateCompletionPredicateByUrl(
        IReadOnlyList<Uri> urlsToCapture) =>
        (navOptions, resources, startTime) =>
        {
            // The shouldCapture predicate ensures only the bands and albums URLs are captured, so 'urlsToCapture.Count == resources.Count'
            // would be sufficient, however explicitly checking that all expected URLs are present is recommended.
            return urlsToCapture.All(url => resources.Any(r => r.Url.Equals(url)));
        };
}
