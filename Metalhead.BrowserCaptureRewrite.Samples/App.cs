using Microsoft.Extensions.Logging;

using Metalhead.BrowserCaptureRewrite.Abstractions.Exceptions;
using Metalhead.BrowserCaptureRewrite.Abstractions.Helpers;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Factories;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Models;

namespace Metalhead.BrowserCaptureRewrite.Samples;

/// <summary>
/// Provides the interactive menu and runs the selected browser capture and rewrite sample scenario.
/// </summary>
/// <remarks>
/// <para>
/// Implements the interactive console menu for demonstrating various browser automation, resource capture, and response rewrite scenarios using the
/// sample services and factories.
/// </para>
/// <para>
/// This type is intended for use in the sample console application and is not intended for reuse as a library API.
/// </para>
/// <para>
/// Cancellation is supported for all asynchronous operations via <see cref="CancellationToken"/>.  If cancellation is requested, in-flight work is
/// stopped and an <see cref="OperationCanceledException"/> is thrown.  If the browser or page is externally closed, an
/// <see cref="OperationCanceledException"/> is also thrown.
/// </para>
/// </remarks>
internal sealed class App(
    ILogger<App> logger,
    SignInOptions signInOptions,
    NavigationTimingOptions navigationTimingOptions,
    CaptureTimingOptions captureTimingOptions,
    IDomCaptureSampleService domCaptureService,
    IResourceCaptureSampleService resourceCaptureService,
    IDomAndResourcesCaptureSampleService domAndResourcesCaptureService,
    IRawNavigateAndCaptureSampleService rawNavigateAndCaptureService,
    IPlaywrightCaptureSampleService playwrightCaptureService,
    ICaptureSpecFactoryByUrls captureSpecFactoryByUrls,
    ICaptureSpecFactoryByContent captureSpecFactoryByContent,
    IRewriteSpecFactoryByUrls rewriteSpecFactoryByUrls,
    IRewriteSpecFactoryByContent rewriteSpecFactoryByContent,
    IExtensionMinimalSample extensionMinimalSample,
    IConvenienceMinimalSample convenienceMinimalSample)
{
    /// <summary>
    /// The URL of the sample sign-in page.
    /// </summary>
    public static readonly Uri SignInUrl = new("https://metaljase.github.io/browsercapturerewrite/sign-in.html");

    /// <summary>
    /// The URL of the sample index page.
    /// </summary>
    public static readonly Uri SampleUrl = new("https://metaljase.github.io/browsercapturerewrite/index.html?albumsDelay=3");

    /// <summary>
    /// The URL of the sample albums JSON file.
    /// </summary>
    public static readonly Uri AlbumsUrl = new("https://metaljase.github.io/browsercapturerewrite/albums.json");

    /// <summary>
    /// The URL of a non-existent resource for demonstrating error handling.
    /// </summary>
    public static readonly Uri WrongUrl = new("https://metaljase.github.io/browsercapturerewrite/wrong_url.json");

    /// <summary>
    /// The list of URLs to capture for bands and albums.
    /// </summary>
    public static readonly Uri[] UrlsToCapture = [
        new("https://metaljase.github.io/browsercapturerewrite/bands_a-m.json"),
        new("https://metaljase.github.io/browsercapturerewrite/bands_n-z.json"),
        AlbumsUrl];

    /// <summary>
    /// The required band names for content-based capture scenarios.
    /// </summary>
    public static readonly string[] RequiredBands = ["Metallica", "Parkway Drive"];

    /// <summary>
    /// The album titles to add in rewrite scenarios.
    /// </summary>
    public static readonly string[] AlbumsToAdd = ["A", "AA", "AAA", "AAAA", "AAAAA"];

    /// <summary>
    /// Runs the interactive sample application, displaying the menu and executing the selected capture scenario.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests.  Defaults to <see langword="default"/>, meaning no cancellation is requested.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.  The task completes when the user exits the application.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The menu allows the user to select from a variety of capture and rewrite scenarios, demonstrating file extension, URL, content-based, and
    /// sign-in flows.  Each scenario logs results and handles cancellation and errors gracefully.
    /// </para>
    /// </remarks>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Capture and display bands & albums JSON by...");
            Console.WriteLine();

            Console.WriteLine("a) Rendered HTML only; cannot provide capture completion predicate, so will not wait for albums to be fetched.");
            Console.WriteLine("b) File extension (.json).");
            Console.WriteLine("c) File extension (.json), and rewrites albums JSON response by adding more albums.");
            Console.WriteLine("d) Deserializing JSON files.");
            Console.WriteLine("e) Deserializing JSON files, and rewrites albums JSON response by adding more albums.");
            Console.WriteLine("f) URLs of JSON files.");
            Console.WriteLine("g) URLs of JSON files using an extension method (simplest).");
            Console.WriteLine("h) URLs of JSON files, and rewrites albums JSON response by adding more albums.");
            Console.WriteLine("i) URLs of JSON files, and rewrites albums JSON response by adding more albums, capturing rendered HTML as well.");
            Console.WriteLine("j) URL of albums JSON file, and rewrites HTML to prevent bands JSON from being fetched, capturing rendered HTML as well.");
            Console.WriteLine("k) URLs of JSON files with a sign-in step (capture starts when browser navigates away from sign-in URL).");
            Console.WriteLine("l) URLs of JSON files with a sign-in step (capture starts when browser navigates to 'signed-in URL').");
            Console.WriteLine("m) URLs of JSON files with a sign-in step (capture starts after 'assume signed-in after' duration (8 secs).");
            Console.WriteLine("n) URLs of JSON files without a capture completion predicate (will not wait for albums to be fetched).");
            Console.WriteLine("o) URLs of JSON files with an incorrect URL (completion predicate cannot complete), causing a timeout after 20 secs.");
            Console.WriteLine("p) URLs of JSON files with an incorrect URL (completion predicate cannot complete), calling directly (not a convenience method), returns whatever captured instead of throwing a PageCaptureIncompleteException after 20 secs.");
            Console.WriteLine("q) Rendered HTML only, using PlaywrightPageCaptureService without a capture completion predicate.");
            Console.WriteLine("X) Exit");
            Console.WriteLine();
            Console.WriteLine("Choose an option...");
            var key = char.ToLower(Console.ReadKey(intercept: true).KeyChar);
            Console.WriteLine();

            if (key == 'x')
                break;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            void handler(object? _, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                logger.LogWarning("Cancellation requested.  Attempting graceful cancellation...");
                cts.Cancel();
            }

            Console.CancelKeyPress += handler;

            try
            {
                switch (key)
                {
                    case 'a':
                        await CaptureAndDisplayRenderedHtmlAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    case 'b':
                        await CaptureAndDisplayBandsAndAlbumsByFileExtensionsAsync(null, cancellationToken)
                            .ConfigureAwait(false);
                        break;
                    case 'c':
                        await CaptureAndDisplayBandsAndAlbumsByFileExtensionsWithRewriteAddingAlbumsAsync(cts.Token)
                            .ConfigureAwait(false);
                        break;
                    case 'd':
                        await CaptureAndDisplayBandsAndAlbumsByDeserializationAsync(cts.Token).ConfigureAwait(false);
                        break;
                    case 'e':
                        await CaptureAndDisplayBandsAndAlbumsByDeserializationWithRewriteAddingAlbumsAsync(cts.Token)
                            .ConfigureAwait(false);
                        break;
                    case 'f':
                        await CaptureAndDisplayBandsAndAlbumsByUrlsAsync(cts.Token).ConfigureAwait(false);
                        break;
                    case 'g':
                        await CaptureAndDisplayBandsAndAlbumsByUrlsExtensionAsync(null, cancellationToken)
                            .ConfigureAwait(false);
                        break;
                    case 'h':
                        await CaptureAndDisplayBandsAndAlbumsByUrlsWithRewriteAddingAlbumsAsync(cts.Token)
                            .ConfigureAwait(false);
                        break;
                    case 'i':
                        await CaptureAndDisplayBandsAndAlbumsAndHtmlByUrlsWithRewriteAddingAlbumsAsync(cts.Token)
                            .ConfigureAwait(false);
                        break;
                    case 'j':
                        await CaptureAndDisplayAlbumsAndHtmlByUrlsWithRewriteToNotFetchBandsAsync(cts.Token)
                            .ConfigureAwait(false);
                        break;
                    case 'k':
                        await CaptureAndDisplayBandsAndAlbumsByUrlsWithSignInUrlAndWithoutSignedInUrlAsync(cts.Token)
                            .ConfigureAwait(false);
                        break;
                    case 'l':
                        await CaptureAndDisplayBandsAndAlbumsByUrlsWithSignInUrlAndSignedInUrlAsync(cts.Token)
                            .ConfigureAwait(false);
                        break;
                    case 'm':
                        await CaptureAndDisplayBandsAndAlbumsByUrlsWithSignInUrlAndSignedInDelayAsync(cts.Token)
                            .ConfigureAwait(false);
                        break;
                    case 'n':
                        await CaptureAndDisplayBandsAndAlbumsByUrlsWithoutCompletionAsync(cts.Token).ConfigureAwait(false);
                        break;
                    case 'o':
                        await CaptureAndDisplayBandsAndAlbumsByUrlsWithWrongUrlThrowsTimeoutAsync(cts.Token)
                            .ConfigureAwait(false);
                        break;
                    case 'p':
                        await CaptureAndDisplayBandsAndAlbumsAndHtmlDirectlyByCaptureSpecAsync(null, cancellationToken)
                            .ConfigureAwait(false);
                        break;
                    case 'q':
                        await FetchAndDisplayRenderedHtmlWithPlaywrightAsync(cts.Token).ConfigureAwait(false);
                        break;
                    case 'r':
                        // Minimal extension method sample from README.md
                        await extensionMinimalSample.CaptureResponsesAsync(cts.Token).ConfigureAwait(false);
                        break;
                    case 's':
                        // Minimal convenience method sample from README.md
                        await convenienceMinimalSample.CaptureResponsesAndRenderedHtmlAsync(cts.Token).ConfigureAwait(false);
                        break;
                    default:
                        Console.WriteLine("Invalid option.");
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Operation successfully cancelled by user.");
            }
            finally
            {
                Console.CancelKeyPress -= handler;
            }
        }
    }

    private async Task CaptureAndDisplayBandsAndAlbumsByUrlsAsync(CancellationToken cancellationToken)
    {
        var captureSpec = captureSpecFactoryByUrls.CreateSpecForBandsAndAlbumsByUrl(UrlsToCapture);
        await CaptureAndDisplayBandsAndAlbumsByCaptureSpecAsync(
            captureSpec, null, signInOptions, cancellationToken, null, null)
            .ConfigureAwait(false);
    }

    private async Task CaptureAndDisplayBandsAndAlbumsByUrlsWithRewriteAddingAlbumsAsync(CancellationToken cancellationToken)
    {
        var captureSpec = captureSpecFactoryByUrls.CreateSpecForBandsAndAlbumsByUrl(UrlsToCapture);
        var rewriteSpec = rewriteSpecFactoryByUrls.CreateRewriteSpecForAddingAlbumsByUrl(AlbumsUrl, AlbumsToAdd);
        await CaptureAndDisplayBandsAndAlbumsByCaptureSpecAsync(
            captureSpec, rewriteSpec, signInOptions, cancellationToken, null, null)
            .ConfigureAwait(false);
    }

    private async Task CaptureAndDisplayBandsAndAlbumsAndHtmlByUrlsWithRewriteAddingAlbumsAsync(CancellationToken cancellationToken)
    {
        var captureSpec = captureSpecFactoryByUrls.CreateSpecForBandsAndAlbumsByUrl(UrlsToCapture);
        var rewriteSpec = rewriteSpecFactoryByUrls.CreateRewriteSpecForAddingAlbumsByUrl(AlbumsUrl, AlbumsToAdd);
        await CaptureAndDisplayBandsAndAlbumsAndHtmlByCaptureSpecAsync(captureSpec, rewriteSpec, cancellationToken).ConfigureAwait(false);
    }

    private async Task CaptureAndDisplayAlbumsAndHtmlByUrlsWithRewriteToNotFetchBandsAsync(CancellationToken cancellationToken)
    {
        var sampleUrlWithoutQuery = new Uri(SampleUrl.GetLeftPart(UriPartial.Path));
        var newSampleUrl = new UriBuilder(sampleUrlWithoutQuery) { Query = "albumsDelay=15" }.Uri;
        var captureSpec = captureSpecFactoryByUrls.CreateSpecForBandsAndAlbumsByUrl([AlbumsUrl]);
        var rewriteSpec = RewriteSpecFactoryByUrls.CreateRewriteSpecForWebPage(newSampleUrl);
        await CaptureAndDisplayBandsAndAlbumsAndHtmlByCaptureSpecAsync(captureSpec, rewriteSpec, cancellationToken, newSampleUrl)
            .ConfigureAwait(false);
    }

    private async Task CaptureAndDisplayBandsAndAlbumsByUrlsWithSignInUrlAndWithoutSignedInUrlAsync(CancellationToken cancellationToken)
    {
        var captureSpec = captureSpecFactoryByUrls.CreateSpecForBandsAndAlbumsByUrl(UrlsToCapture);
        await CaptureAndDisplayBandsAndAlbumsByCaptureSpecAsync(
            captureSpec, null, signInOptions, cancellationToken, SignInUrl)
            .ConfigureAwait(false);
    }

    private async Task CaptureAndDisplayBandsAndAlbumsByUrlsWithSignInUrlAndSignedInUrlAsync(CancellationToken cancellationToken)
    {
        Uri? assumedSignedInUrl = new("https://metaljase.github.io/browsercapturerewrite/index.html");
        var captureSpec = captureSpecFactoryByUrls.CreateSpecForBandsAndAlbumsByUrl(UrlsToCapture);
        await CaptureAndDisplayBandsAndAlbumsByCaptureSpecAsync(
            captureSpec, null, signInOptions, cancellationToken, SignInUrl, assumedSignedInUrl)
            .ConfigureAwait(false);
    }

    private async Task CaptureAndDisplayBandsAndAlbumsByUrlsWithSignInUrlAndSignedInDelayAsync(CancellationToken cancellationToken)
    {
        SignInOptions signInOptionsWithDelay = new(assumeSignedInAfter: TimeSpan.FromSeconds(8));
        var captureSpec = captureSpecFactoryByUrls.CreateSpecForBandsAndAlbumsByUrl(UrlsToCapture);
        await CaptureAndDisplayBandsAndAlbumsByCaptureSpecAsync(
            captureSpec, null, signInOptionsWithDelay, cancellationToken, SignInUrl)
            .ConfigureAwait(false);
    }

    private async Task CaptureAndDisplayBandsAndAlbumsByUrlsWithoutCompletionAsync(CancellationToken cancellationToken)
    {
        CaptureSpec captureSpec = new(shouldCapture: req => UrlsToCapture.Contains(UriHelper.ParseAbsoluteUrl(req.Url)),
            tryCreateCapturedResourceAsync: (req, resp) => CaptureSpecFactoryByUrls.TryCreateCapturedResourceAsync(req, resp),
            shouldCompleteCapture: null);
        await CaptureAndDisplayBandsAndAlbumsByCaptureSpecAsync(
            captureSpec, null, signInOptions, cancellationToken, null, null)
            .ConfigureAwait(false);
    }

    private async Task CaptureAndDisplayBandsAndAlbumsByUrlsWithWrongUrlThrowsTimeoutAsync(CancellationToken cancellationToken)
    {
        var captureSpec = captureSpecFactoryByUrls.CreateSpecForBandsAndAlbumsByUrl([.. UrlsToCapture, WrongUrl]);
        await CaptureAndDisplayBandsAndAlbumsByCaptureSpecAsync(
            captureSpec, null, signInOptions, cancellationToken, null, null)
            .ConfigureAwait(false);
    }

    private async Task CaptureAndDisplayBandsAndAlbumsByFileExtensionsWithRewriteAddingAlbumsAsync(CancellationToken cancellationToken)
    {
        var rewriteSpec = rewriteSpecFactoryByUrls.CreateRewriteSpecForAddingAlbumsByUrl(AlbumsUrl, AlbumsToAdd);
        await CaptureAndDisplayBandsAndAlbumsByFileExtensionsAsync(rewriteSpec, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task CaptureAndDisplayBandsAndAlbumsByDeserializationAsync(CancellationToken cancellationToken)
    {
        var captureSpec = captureSpecFactoryByContent.CreateSpecForBandsAndAlbumsByContent(RequiredBands);
        await CaptureAndDisplayBandsAndAlbumsByCaptureSpecAsync(
            captureSpec, null, signInOptions, cancellationToken, null, null)
            .ConfigureAwait(false);
    }

    private async Task CaptureAndDisplayBandsAndAlbumsByDeserializationWithRewriteAddingAlbumsAsync(CancellationToken cancellationToken)
    {
        var captureSpec = captureSpecFactoryByContent.CreateSpecForBandsAndAlbumsByContent(RequiredBands);
        var rewriteSpec = rewriteSpecFactoryByContent.CreateRewriteSpecForAddingAlbumsByContent(AlbumsToAdd);
        await CaptureAndDisplayBandsAndAlbumsByCaptureSpecAsync(
            captureSpec, rewriteSpec, signInOptions, cancellationToken, null, null)
            .ConfigureAwait(false);
    }

    private async Task CaptureAndDisplayBandsAndAlbumsByFileExtensionsAsync(
        RewriteSpec? rewriteSpec, CancellationToken cancellationToken)
    {
        var shouldCompleteCapture = CaptureSpecFactoryByContent.CreateCompletionPredicateByContent(RequiredBands);

        (Bands Bands, Albums Albums) capturedResources;
        try
        {
            capturedResources = await resourceCaptureService.CaptureBandsAndAlbumsAsync(
                SampleUrl,
                null,
                null,
                null,
                signInOptions,
                navigationTimingOptions,
                captureTimingOptions,
                rewriteSpec,
                shouldCompleteCapture,
                [".json"],
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is BrowserSessionInitializationException
            or PageCaptureIncompleteException
            or SignInException
            or ConnectivityException
            or HttpRequestException
            or TimeoutException
            or InvalidOperationException
            or ArgumentException
            or ArgumentNullException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogCritical(ex, "Fatal error capturing files.  Exiting.");
            throw;
        }

        DisplayBandsAndAlbums(capturedResources.Bands, capturedResources.Albums);
    }

    private async Task CaptureAndDisplayBandsAndAlbumsByUrlsExtensionAsync(
        RewriteSpec? rewriteSpec, CancellationToken cancellationToken)
    {
        (Bands Bands, Albums Albums) capturedResources;
        try
        {
            capturedResources = await resourceCaptureService.CaptureBandsAndAlbumsAsync(
                SampleUrl,
                null,
                null,
                null,
                signInOptions,
                navigationTimingOptions,
                captureTimingOptions,
                rewriteSpec,
                UrlsToCapture,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is BrowserSessionInitializationException
            or PageCaptureIncompleteException
            or SignInException
            or ConnectivityException
            or HttpRequestException
            or TimeoutException
            or InvalidOperationException
            or ArgumentException
            or ArgumentNullException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogCritical(ex, "Fatal error capturing files.  Exiting.");
            throw;
        }

        DisplayBandsAndAlbums(capturedResources.Bands, capturedResources.Albums);
    }

    private async Task CaptureAndDisplayBandsAndAlbumsByCaptureSpecAsync(
        CaptureSpec captureSpec,
        RewriteSpec? rewriteSpec,
        SignInOptions signInOptions,
        CancellationToken cancellationToken,
        Uri? signInUrl = null,
        Uri? assumedSignedInUrl = null)
    {
        (Bands Bands, Albums Albums) capturedResources;
        try
        {
            capturedResources = await resourceCaptureService.CaptureBandsAndAlbumsAsync(
                SampleUrl,
                null,
                signInUrl,
                assumedSignedInUrl,
                signInOptions,
                navigationTimingOptions,
                captureTimingOptions,
                captureSpec,
                rewriteSpec,
                cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is BrowserSessionInitializationException
            or PageCaptureIncompleteException
            or SignInException
            or ConnectivityException
            or HttpRequestException
            or TimeoutException
            or InvalidOperationException
            or ArgumentException
            or ArgumentNullException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogCritical(ex, "Fatal error capturing files.  Exiting.");
            throw;
        }

        DisplayBandsAndAlbums(capturedResources.Bands, capturedResources.Albums);
    }

    private async Task CaptureAndDisplayBandsAndAlbumsAndHtmlByCaptureSpecAsync(
        CaptureSpec captureSpec, RewriteSpec? rewriteSpec, CancellationToken cancellationToken, Uri? pageUrl = null)
    {
        pageUrl ??= SampleUrl;
        (Bands Bands, Albums Albums, string? RenderedHtml) capturedResources;
        try
        {
            capturedResources = await domAndResourcesCaptureService.CaptureBandsAndAlbumsAsync(
                pageUrl,
                null,
                null,
                null,
                signInOptions,
                navigationTimingOptions,
                captureTimingOptions,
                captureSpec,
                rewriteSpec,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is BrowserSessionInitializationException
            or PageCaptureIncompleteException
            or SignInException
            or ConnectivityException
            or HttpRequestException
            or TimeoutException
            or InvalidOperationException
            or ArgumentException
            or ArgumentNullException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogCritical(ex, "Fatal error capturing files.  Exiting.");
            throw;
        }

        DisplayBandsAndAlbums(capturedResources.Bands, capturedResources.Albums);
        Console.WriteLine();
        DisplayRenderedHtml(capturedResources.RenderedHtml);
    }

    private async Task CaptureAndDisplayRenderedHtmlAsync(CancellationToken cancellationToken)
    {
        string? renderedHtml;
        try
        {
            renderedHtml = await domCaptureService.CaptureBandsAndAlbumsAsync(
                SampleUrl,
                null,
                null,
                null,
                signInOptions,
                navigationTimingOptions,
                captureTimingOptions,
                cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is BrowserSessionInitializationException
            or PageCaptureIncompleteException
            or SignInException
            or ConnectivityException
            or HttpRequestException
            or TimeoutException
            or InvalidOperationException
            or ArgumentException
            or ArgumentNullException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogCritical(ex, "Fatal error capturing files.  Exiting.");
            throw;
        }

        DisplayRenderedHtml(renderedHtml);
    }

    private async Task CaptureAndDisplayBandsAndAlbumsAndHtmlDirectlyByCaptureSpecAsync(
        RewriteSpec? rewriteSpec, CancellationToken cancellationToken)
    {
        var captureSpec = captureSpecFactoryByUrls.CreateSpecForBandsAndAlbumsByUrl([.. UrlsToCapture, WrongUrl]);
        (Bands Bands, Albums Albums, string? RenderedHtml) capturedResources;
        try
        {
            capturedResources = await rawNavigateAndCaptureService.CaptureBandsAndAlbumsAsync(
                SampleUrl,
                null,
                null,
                null,
                signInOptions,
                navigationTimingOptions,
                captureTimingOptions,
                captureSpec,
                rewriteSpec,
                cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is BrowserSessionInitializationException
            or SignInException
            or ConnectivityException
            or HttpRequestException
            or TimeoutException
            or InvalidOperationException
            or ArgumentException
            or ArgumentNullException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogCritical(ex, "Fatal error capturing files.  Exiting.");
            throw;
        }

        DisplayBandsAndAlbums(capturedResources.Bands, capturedResources.Albums);
        Console.WriteLine();
        DisplayRenderedHtml(capturedResources.RenderedHtml);
    }

    private async Task FetchAndDisplayRenderedHtmlWithPlaywrightAsync(CancellationToken cancellationToken)
    {
        string? renderedHtml;
        try
        {
            renderedHtml = await playwrightCaptureService.FetchRenderedHtmlAsync(
                SampleUrl,
                null,
                null,
                null,
                signInOptions,
                navigationTimingOptions,
                captureTimingOptions,
                cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is BrowserSessionInitializationException
            or SignInException
            or ConnectivityException
            or HttpRequestException
            or TimeoutException
            or InvalidOperationException
            or ArgumentException
            or ArgumentNullException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogCritical(ex, "Fatal error fetching page HTML.  Exiting.");
            throw;
        }
        DisplayRenderedHtml(renderedHtml);
    }

    private void DisplayBandsAndAlbums(Bands bands, Albums albums)
    {
        if (bands.BandNames.Count == 0)
            logger.LogWarning("No bands data captured.");
        else
            logger.LogInformation("Bands ({BandCount}): {Bands}.", bands.BandNames.Count, string.Join(", ", bands.BandNames));

        Console.WriteLine();

        if (albums.AlbumTitles.Count == 0)
            logger.LogWarning("No albums data captured.");
        else
            logger.LogInformation("Albums ({AlbumCount}): {Albums}.", albums.AlbumTitles.Count, string.Join(", ", albums.AlbumTitles));
    }

    private void DisplayRenderedHtml(string? renderedHtml)
    {
        if (string.IsNullOrEmpty(renderedHtml))
            logger.LogWarning("No rendered HTML captured.");
        else
            logger.LogInformation("Rendered HTML:{NewLine}{RenderedHtml}", Environment.NewLine, renderedHtml);
    }
}
