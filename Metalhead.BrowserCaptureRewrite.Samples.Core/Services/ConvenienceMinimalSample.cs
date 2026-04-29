using System.Text.Json;

using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Services;
using Metalhead.BrowserCaptureRewrite.Abstractions.Transport;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Services;

public class ConvenienceMinimalSample(
    NavigationTimingOptions navigationTimingOptions,
    CaptureTimingOptions captureTimingOptions,
    IBrowserSessionService browserSessionService,
    IBrowserSessionResilienceWrapper resilienceWrapper,
    IBrowserDomCaptureService domCaptureService)
    : IConvenienceMinimalSample
{
    public async Task CaptureResponsesAndRenderedHtmlAsync(CancellationToken cancellationToken = default)
    {
        // Create a browser session, wrapped in resiliency to handle transient errors and retries.
        await using var resilientSession = resilienceWrapper.Wrap(
            await browserSessionService.CreateBrowserSessionOrThrowAsync(cancellationToken)
            .ConfigureAwait(false));

        Uri pageUrl = new("https://metaljase.github.io/browsercapturerewrite/index.html?albumsDelay=3");
        Uri bandsUrl = new("https://metaljase.github.io/browsercapturerewrite/bands_a-m.json");
        List<Uri> urlsToCapture = [
            bandsUrl,
            new("https://metaljase.github.io/browsercapturerewrite/bands_n-z.json"),
            new("https://metaljase.github.io/browsercapturerewrite/albums.json")];
        
        IReadOnlyList<string> addBands = ["A", "AA", "AAA", "AAAA"];

        // Create a CaptureSpec that captures the contents of in-flight HTTP responses for all
        // three JSON files, and only completes capture when all three JSON files have been captured.
        CaptureSpec captureSpec = new(
            shouldCapture: req => urlsToCapture.Contains(new Uri(req.Url)),
            tryCreateCapturedResourceAsync: TryCreateCapturedResourceAsync,
            shouldCompleteCapture: (navOptions, capturedResources, lastCapturedTime) =>
                urlsToCapture.All(url => capturedResources.Any(r => r.Url.Equals(url))));

        // Create a RewriteSpec that rewrites the in-flight HTTP response body of bands_a-m.json
        // by adding more bands.
        RewriteSpec rewriteSpec = new(
            shouldRewrite: req => Uri.TryCreate(req.Url, UriKind.Absolute, out var requestUri) && requestUri.Equals(bandsUrl),
            tryRewriteResponseAsync: async (req, resp) =>
                await RewriteAsync(req, resp, addBands).ConfigureAwait(false));

        NavigationOptions navigationOptions = new(
            pageUrl, RefererUrl: null, PageLoadTimeout: navigationTimingOptions.PageLoadTimeout());

        // Capture contents of in-flight HTTP responses for all three JSON files, including albums.json
        // that's fetched after a 3 seconds delay.  The CaptureSpec has a capture-completion
        // predicate that only completes once all three URLs have been captured.
        PageCaptureResult result = await domCaptureService.NavigateAndCaptureHtmlAndResourcesResultAsync(
            resilientSession,
            navigationOptions,
            captureSpec,
            rewriteSpec,
            cancellationToken,
            captureTimingOptions)
            .ConfigureAwait(false);

        Console.WriteLine(result.RenderedHtml);
        foreach (var resource in result.Resources)
            Console.Write(resource.TextContent);
    }

    private record Bands(List<string> BandNames);

    // This method is supplied as the tryCreateCapturedResourceAsync delegate, and is invoked for each
    // response that matches the shouldCapture predicate.  The response should be examined to determine
    // if it contains data you want to keep; if it does, return it as a CapturedResource.
    // It's OK to return a CapturedResource containing a response you're NOT interested in, as this can
    // be filtered out later, so your logic can be relatively loose, but this will use more memory than
    // necessary, and may affect performance for large responses.  For this example, the shouldCapture
    // predicate ensures only bands_a-m.json is intercepted, so examining the content isn't necessary.
    private static async Task<CapturedResource?> TryCreateCapturedResourceAsync(
        IRequestInfo req, IResponseInfo resp)
    {
        resp.Headers.TryGetValue("content-type", out var contentType);
        if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var requestUri))
            return null;

        var body = await resp.GetBodyAsStringAsync().ConfigureAwait(false);
        return new CapturedResource(requestUri, body, null, contentType, resp.StatusCode, resp.Headers);
    }

    // This method is supplied as the rewriteResponse delegate, and is invoked for each response that
    // matches the shouldRewrite predicate, i.e. the URL for bands_a-m.json.  The response body is
    // deserialized, modified by adding more bands, and then serialized back to JSON and returned.  The
    // browser will recieve the modified response body.
    private static async Task<ResponseRewriteResult> RewriteAsync(
        IRequestInfo req, IResponseInfo resp, IReadOnlyList<string> addBands)
    {
        if (!Uri.TryCreate(req.Url, UriKind.Absolute, out _))
            return ResponseRewriteResult.NotRewritten;

        var body = await resp.GetBodyAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body) 
            || !TryDeserialize<Bands>(body, out var bands)
            || bands?.BandNames is not { Count: > 0 })
            return ResponseRewriteResult.NotRewritten;

        bands.BandNames.InsertRange(0, addBands);
        return new ResponseRewriteResult(true, JsonSerializer.Serialize(bands), null);
    }

    private static bool TryDeserialize<T>(string json, out T? model)
    {
        try
        {
            model = JsonSerializer.Deserialize<T>(json);
            return model != null;
        }
        catch
        {
            model = default;
            return false;
        }
    }
}