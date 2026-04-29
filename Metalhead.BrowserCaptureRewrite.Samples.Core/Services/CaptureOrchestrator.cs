using Microsoft.Extensions.Logging;

using Metalhead.BrowserCaptureRewrite.Abstractions.Connectivity;
using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Enums;
using Metalhead.BrowserCaptureRewrite.Abstractions.Exceptions;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Services;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Helpers;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Models;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Services;

/// <summary>
/// Coordinates browser session creation, resource capture, and result parsing for bands and albums in sample scenarios.
/// </summary>
/// <remarks>
/// <para>
/// Implements the orchestration logic for capturing bands and albums from a web page using browser automation, resilience, and connectivity
/// abstractions.
/// </para>
/// <para>
/// Implements cancellation support for all asynchronous operations via <see cref="CancellationToken"/>.  If cancellation is requested, in-flight
/// work is stopped and an <see cref="OperationCanceledException"/> is thrown.  If the browser or page is externally closed, an
/// <see cref="OperationCanceledException"/> is also thrown.
/// </para>
/// <para>
/// Exceptions during session creation, sign-in, or capture are logged and rethrown.  Connectivity errors are classified and logged using
/// <see cref="BaseOrchestrator"/> logic.
/// </para>
/// </remarks>
internal sealed class CaptureOrchestrator(
    ILogger logger,
    IBrowserSessionService browserSessionService,
    IBrowserSessionResilienceWrapper resilienceWrapper,
    IConnectivityClassifier classifier,
    IConnectivityProbe probe)
    : BaseOrchestrator(logger, classifier, probe)
{
    /// <summary>
    /// Captures bands and albums from the specified URL, performing sign-in if required, and returns the parsed results and capture details.
    /// </summary>
    /// <param name="url">
    /// The target URL to capture resources from.  Must not be <see langword="null"/>.
    /// </param>
    /// <param name="referrerUrl">
    /// The referer URL to use for navigation, or <see langword="null"/> to omit the referer.
    /// </param>
    /// <param name="signInUrl">
    /// The URL to use for sign-in, or <see langword="null"/> to skip sign-in.
    /// </param>
    /// <param name="assumeSignedInWhenNavigatedToUrl">
    /// The URL to assume sign-in is complete when navigated to, or <see langword="null"/> to use timing-based completion.
    /// </param>
    /// <param name="signInOptions">
    /// The sign-in timing options controlling how long to wait before assuming sign-in is complete and the maximum page
    /// load timeout.  Must not be <see langword="null"/>.
    /// </param>
    /// <param name="navigationTimingOptions">
    /// The timing options for navigation.  Must not be <see langword="null"/>.
    /// </param>
    /// <param name="captureAsync">
    /// The delegate to perform the actual resource capture, given a browser session, navigation options, timing options, and cancellation token.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests.
    /// </param>
    /// <returns>
    /// A tuple containing the parsed <see cref="Bands"/>, <see cref="Albums"/>, and the full <see cref="PageCaptureResult"/> for the capture
    /// operation.
    /// </returns>
    /// <exception cref="BrowserSessionInitializationException">Thrown if browser or session initialisation fails.</exception>
    /// <exception cref="SignInException">Thrown if sign-in fails or is cancelled.</exception>
    /// <exception cref="PageCaptureIncompleteException">Thrown if the page capture does not complete successfully.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled before or during capture.</exception>
    /// <remarks>
    /// <para>
    /// This method creates a browser session (optionally wrapped for resilience), navigates to the target URL, and invokes the provided
    /// <paramref name="captureAsync"/> delegate to perform resource capture.  It parses the captured resources to extract bands and albums, and
    /// returns the results along with the full capture details.
    /// </para>
    /// <para>
    /// Exceptions are logged and rethrown.  Connectivity errors are classified and logged using <see cref="BaseOrchestrator.IsLocalConnectivityErrorAsync"/>.
    /// </para>
    /// </remarks>
    public async Task<(Bands Bands, Albums Albums, PageCaptureResult PageCaptureResult)> CaptureBandsAndAlbumsAsync(
        Uri url,
        Uri? referrerUrl,
        Uri? signInUrl,
        Uri? assumeSignedInWhenNavigatedToUrl,
        SignInOptions signInOptions,
        NavigationTimingOptions navigationTimingOptions,
        Func<IBrowserSession, NavigationOptions, CancellationToken, Task<PageCaptureResult>> captureAsync,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PageCaptureResult pageCaptureResult;
        try
        {
            // Create a browser session, then optionally wrapped in a resilience wrapper to handle transient errors and retries.
            await using var resilientSession = resilienceWrapper.Wrap(
                await browserSessionService.CreateBrowserSessionOrThrowAsync(
                    signInUrl,
                    assumeSignedInWhenNavigatedToUrl,
                    signInOptions,
                    cancellationToken).ConfigureAwait(false));

            cancellationToken.ThrowIfCancellationRequested();

            var navigationOptions = new NavigationOptions(url, referrerUrl, navigationTimingOptions.PageLoadTimeout());

            pageCaptureResult = await captureAsync(resilientSession, navigationOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (BrowserSessionInitializationException ex)
        {
            _logger.LogError(ex, "Browser/session initialization failed.  Details: {Details}", ex.Message);
            throw;
        }
        catch (SignInException ex)
        {
            // The result of IsLocalConnectivityErrorAsync can be used in batch processing to decide whether to abort on local connectivity errors.
            await IsLocalConnectivityErrorAsync(ex, action: "navigating to sign-in URL", ex.SignInUrl.ToString(), cancellationToken)
                .ConfigureAwait(false);
            throw;
        }
        catch (PageCaptureIncompleteException ex)
        {
            if (ex.CaptureStatus == CaptureStatus.UrlChangedBeforeCompletion)
                _logger.LogWarning(ex, "Browser navigated away from URL before capture completed.");
            else if (ex.CaptureStatus == CaptureStatus.CaptureTimeoutExceeded)
                _logger.LogWarning(ex, "Timeout exceeded before capture completed.");
            throw;
        }
        catch (OperationCanceledException ex)
        {
            if (!cancellationToken.IsCancellationRequested)
                _logger.LogWarning("{Message}", ex.Message);
            throw;
        }
        catch (Exception ex) when (
            ex is ConnectivityException
            or HttpRequestException
            or TimeoutException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // The result of IsLocalConnectivityErrorAsync can be used in batch processing to decide whether to abort on local connectivity errors.
            await IsLocalConnectivityErrorAsync(ex, action: "capturing resources", url.ToString(), cancellationToken)
                .ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing files.");
            throw;
        }

        var (Bands, Albums) = ParseBandsAndAlbums(pageCaptureResult.Resources);
        return (Bands, Albums, pageCaptureResult);
    }

    /// <summary>
    /// Parses the captured resources to extract bands and albums models.
    /// </summary>
    /// <param name="capturedResources">
    /// The list of captured resources to parse.  Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A tuple containing the parsed <see cref="Bands"/> and <see cref="Albums"/> models.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method deserialises each resource's text content as <see cref="Bands"/> and <see cref="Albums"/> models, aggregating all band names and
    /// album titles found.
    /// </para>
    /// </remarks>
    private static (Bands Bands, Albums Albums) ParseBandsAndAlbums(IReadOnlyList<CapturedResource> capturedResources)
    {
        Bands bands = new();
        Albums albums = new();
        foreach (var resource in capturedResources)
        {
            if (resource.TextContent is not string text)
                continue;

            if (SamplesHelper.TryDeserializeModel<Bands>(text, out var bandsAToM) && bandsAToM is { BandNames.Count: > 0 })
                bands.BandNames.AddRange(bandsAToM.BandNames);

            if (SamplesHelper.TryDeserializeModel<Albums>(text, out var albumsAToZ) && albumsAToZ is { AlbumTitles.Count: > 0 })
                albums.AlbumTitles.AddRange(albumsAToZ.AlbumTitles);
        }
        return (bands, albums);
    }
}
