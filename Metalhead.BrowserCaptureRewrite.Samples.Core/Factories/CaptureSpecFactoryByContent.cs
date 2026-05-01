using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Transport;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Helpers;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Models;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Factories;

/// <summary>
/// Provides a factory for creating <see cref="CaptureSpec"/> instances that capture bands and albums based on HTTP response content.
/// </summary>
/// <remarks>
/// Implements <see cref="ICaptureSpecFactoryByContent"/>.
/// <para>
/// The created <see cref="CaptureSpec"/> uses content-based logic to filter and complete resource capture, including
/// deserialisation of JSON responses and matching required band names.
/// </para>
/// <para>
/// Cancellation is supported for all asynchronous operations within the created <see cref="CaptureSpec"/> delegates
/// via <see cref="CancellationToken"/>.  If cancellation is requested, in-flight work is stopped and an
/// <see cref="OperationCanceledException"/> is thrown.
/// </para>
/// </remarks>
public sealed class CaptureSpecFactoryByContent() : ICaptureSpecFactoryByContent
{
    /// <inheritdoc/>
    public CaptureSpec CreateSpecForBandsAndAlbumsByContent(IReadOnlyList<string> requiredBands) =>
        new(shouldCapture: req => SamplesHelper.IsLikelyJson(req),
            tryCreateCapturedResourceAsync: (req, resp) => TryCreateCapturedResourceByContentAsync(req, resp, requiredBands),
            shouldCompleteCapture: CreateCompletionPredicateByContent(requiredBands));

    // This method is supplied as the tryCreateCapturedResourceAsync delegate, and is invoked for each response that matches the shouldCapture
    // predicate.  The response should be examined to determine if it contains data you want to keep; if it does, return it as a CapturedResource.
    //
    // It's OK to return a CapturedResource containing a response you're NOT interested in, as this can be filtered out later, so your
    // logic can be relatively loose, but this will use more memory than necessary, and my affect performance for large responses.
    // For this example, deserialization is included as a demonstration, but it would still work without it because the completion predicate
    // also performs deserialization to ensure the capture process only completes once all responses we're interested in have been captured.
    private static async Task<CapturedResource?> TryCreateCapturedResourceByContentAsync(
        IRequestInfo req, IResponseInfo resp, IReadOnlyList<string> requiredBands)
    {
        resp.Headers.TryGetValue("content-type", out var contentType);
        if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var requestUri))
            return null;

        // If the response is likely NOT JSON, then return null because we're only interested in capturing the bands and albums JSON responses.
        if (!SamplesHelper.IsLikelyJson(req))
            return null;

        // Try getting the response body as string, so we can determine if it's the bands or albums JSON response.
        string text;
        try
        {
            text = await resp.GetBodyAsStringAsync().ConfigureAwait(false);
        }
        catch { return null; }

        // Try deserializing the response using the albums model, then check if it contains at least one album.
        if (SamplesHelper.TryDeserializeModel<Albums>(text, out var albums) && albums is { AlbumTitles.Count: > 0 })
            return new CapturedResource(requestUri, text, null, contentType, resp.StatusCode, resp.Headers);

        // Try deserializing the response using the bands model, and ensure it contains any of the interested bands, otherwise return null.
        if (SamplesHelper.TryDeserializeModel<Bands>(text, out var bands)
            && bands is { BandNames.Count: > 0 }
            && requiredBands.Any(b => bands.BandNames.Contains(b)))
            return (CapturedResource?)new CapturedResource(requestUri, text, null, contentType, resp.StatusCode, resp.Headers);

        // Responses we want to keep should have been returned above as a CapturedResource, so return null.
        return null;
    }

    /// <summary>
    /// Creates a capture-completion predicate that determines when all required bands and at least one album have been captured
    /// based on response content.
    /// </summary>
    /// <param name="requiredBands">
    /// The list of band names that must be present in the captured content for the capture to be considered complete.  Must not be
    /// <see langword="null"/> or empty.
    /// </param>
    /// <returns>
    /// A delegate that evaluates the current navigation options, the list of captured resources, and the capture start time,
    /// returning <see langword="true"/> if all required bands and at least one album have been captured; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The returned function is invoked regularly during the capture process and examines all captured resources so far.  It
    /// deserialises each resource's text content to check for the presence of required bands and albums.
    /// </para>
    /// <para>
    /// If all <paramref name="requiredBands"/> are found in the captured content and at least one album is present, the predicate
    /// returns <see langword="true"/> to signal completion.  Otherwise, it returns <see langword="false"/> to continue capturing.
    /// </para>
    /// <para>
    /// If a completion predicate is not provided, the capture process will complete after 500 ms of network inactivity.  Use this
    /// predicate when you need to ensure specific content is captured, even if it arrives after the default idle period.
    /// </para>
    /// </remarks>
    public static Func<NavigationOptions, IReadOnlyList<CapturedResource>, DateTime, bool> CreateCompletionPredicateByContent(
        IReadOnlyList<string> requiredBands) =>
        (navOptions, resources, startTime) =>
        {
            // Creates the capture-completion predicate delegate.  The returned function is invoked regularly during the capture process, and is
            // passed the resources captured so far, allowing you to determine whether or not ALL the resources you're interested in have been
            // captured.  If a completion predicate is not provided, then the capture process will complete once a duration of 500ms without any
            // network taffic has been observed.  Therefore, for many web pages, a completion predicate will not be necessary.  If the web page
            // fetches resources you want to capture after 500ms of zero network has been observed, a capture predicate will be necessary; allowing
            // you to control when the capture process should complete.
            // Return true if capturing is complete, or false if the capture process should continue because not all resources have been captured yet.

            var capturedAlbums = false;
            List<string> capturedRequiredBands = [];

            foreach (var resource in resources)
            {
                if (resource.TextContent is not string text)
                    continue;

                if (SamplesHelper.TryDeserializeModel<Bands>(text, out var bands) && bands?.BandNames is not null)
                    foreach (var band in bands.BandNames)
                        if (requiredBands.Contains(band) && !capturedRequiredBands.Contains(band))
                            capturedRequiredBands.Add(band);

                if (SamplesHelper.TryDeserializeModel<Albums>(text, out var albums) && albums is { AlbumTitles.Count: > 0 })
                    capturedAlbums = true;
            }
            return capturedRequiredBands.Count == requiredBands.Count && capturedAlbums;
        };
}
