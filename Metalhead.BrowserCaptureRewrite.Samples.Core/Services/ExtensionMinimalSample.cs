using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Services;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Services;

public class ExtensionMinimalSample(
    NavigationTimingOptions navigationTimingOptions,
    CaptureTimingOptions captureTimingOptions,
    IBrowserSessionService browserSessionService,
    IBrowserSessionResilienceWrapper resilienceWrapper,
    IBrowserCaptureService captureService)
    : IExtensionMinimalSample
{
    public async Task CaptureResponsesAsync(CancellationToken cancellationToken = default)
    {
        // Create a browser session, wrapped in resiliency to handle transient errors and retries.
        await using var resilientSession = resilienceWrapper.Wrap(
            await browserSessionService.CreateBrowserSessionOrThrowAsync(cancellationToken)
            .ConfigureAwait(false));

        Uri pageUrl = new("https://metaljase.github.io/browsercapturerewrite/index.html?albumsDelay=3");
        Uri[] urlsToCapture = [
            new("https://metaljase.github.io/browsercapturerewrite/bands_a-m.json"),
            new("https://metaljase.github.io/browsercapturerewrite/bands_n-z.json"),
            new("https://metaljase.github.io/browsercapturerewrite/albums.json")];

        // Capture contents of in-flight HTTP responses for all three JSON files, including albums.json
        // that's fetched after a 3 seconds delay.  The extension method creates a CaptureSpec
        // with a capture-completion predicate that only completes once all 3 URLs have been captured.
        IReadOnlyList<CapturedResource> resultByUrls =
            await captureService.NavigateAndCaptureResourcesAsync(
                resilientSession,
                pageUrl,
                urlsToCapture,
                cancellationToken,
                refererUrl: null,
                navigationTimingOptions.PageLoadTimeout(),
                captureTimingOptions.NetworkIdleTimeout(),
                captureTimingOptions.CaptureTimeout(),
                pollInterval: captureTimingOptions.PollInterval(),
                rewriteSpec: null)
            .ConfigureAwait(false);

        Console.WriteLine("Example 1...");
        foreach (var resource in resultByUrls)
            Console.Write(resource.TextContent);

        // Capture contents of in-flight HTTP responses with a .json extension, however, albums.json
        // will not be captured due to the fetch delay.  The extension method creates a
        // CaptureSpec that captures HTTP responses with a .json extension, but it doesn't
        // include a capture-completion predicate because it doesn't know what JSON files will be
        // fetched, thus nor what JSON files should be captured.  Therefore, capture completes when zero
        // network traffic has been observed for a duration of 500ms.
        // NOTE: A capture-completion predicate can be provided as a parameter, where custom logic can
        // control when capture should complete, e.g. after specific URLs have been captured, or a
        // duration of time has elapsed, or when the file contains certain data.
        IReadOnlyList<CapturedResource> resultByFileExt =
            await captureService.NavigateAndCaptureResourcesAsync(
                resilientSession,
                pageUrl,
                [".json"],
                cancellationToken,
                refererUrl: null,
                navigationTimingOptions.PageLoadTimeout(),
                captureTimingOptions.NetworkIdleTimeout(),
                captureTimingOptions.CaptureTimeout(),
                captureTimingOptions.PollInterval(),
                rewriteSpec: null,
                shouldCompleteCapture: null)
            .ConfigureAwait(false);

        Console.WriteLine("Example 2...");
        foreach (var resource in resultByFileExt)
            Console.Write(resource.TextContent);
    }
}