using Microsoft.Extensions.Logging;

using Metalhead.BrowserCaptureRewrite.Abstractions.Connectivity;
using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Services;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Models;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IResourceCaptureSampleService"/> for capturing bands and albums
/// using resource capture strategies.
/// </summary>
/// <remarks>
/// Implements <see cref="IResourceCaptureSampleService"/>.
/// <para>
/// Cancellation is supported for all asynchronous operations via <see cref="CancellationToken"/>.  If cancellation is requested
/// before or during capture, an <see cref="OperationCanceledException"/> is thrown.  If the browser or page is externally
/// closed, an <see cref="OperationCanceledException"/> is also thrown.
/// </para>
/// <para>
/// Exceptions during session creation, sign-in, or capture are logged and rethrown.  Connectivity errors are classified and
/// logged using orchestrator logic.
/// </para>
/// </remarks>
public sealed class ResourceCaptureSampleService(
    ILogger<ResourceCaptureSampleService> logger,
    IBrowserSessionService browserSessionService,
    IBrowserSessionResilienceWrapper resilienceWrapper,
    IBrowserCaptureService captureService,
    IConnectivityClassifier classifier,
    IConnectivityProbe probe)
    : IResourceCaptureSampleService
{
    /// <inheritdoc/>
    public async Task<(Bands Bands, Albums Albums)> CaptureBandsAndAlbumsAsync(
        Uri url,
        Uri? referrerUrl,
        Uri? signInUrl,
        Uri? assumeSignedInWhenNavigatedToUrl,
        SignInOptions signInOptions,
        NavigationTimingOptions navigationTimingOptions,
        CaptureTimingOptions captureTimingOptions,
        CaptureSpec captureSpec,
        RewriteSpec? rewriteSpec,
        CancellationToken cancellationToken)
    {
        var orchestrator = new CaptureOrchestrator(logger, browserSessionService, resilienceWrapper, classifier, probe);

        var result = await orchestrator.CaptureBandsAndAlbumsAsync(
            url,
            referrerUrl,
            signInUrl,
            assumeSignedInWhenNavigatedToUrl,
            signInOptions,
            navigationTimingOptions,
            captureAsync: (session, navigationOptions, ct) =>
                captureService.NavigateAndCaptureResourcesResultAsync(
                    session, navigationOptions, captureSpec, rewriteSpec, ct, captureTimingOptions),
            cancellationToken)
            .ConfigureAwait(false);

        return (result.Bands, result.Albums);
    }

    /// <inheritdoc/>
    public async Task<(Bands Bands, Albums Albums)> CaptureBandsAndAlbumsAsync(
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
        CancellationToken cancellationToken)
    {
        var orchestrator = new CaptureOrchestrator(logger, browserSessionService, resilienceWrapper, classifier, probe);

        var result = await orchestrator.CaptureBandsAndAlbumsAsync(
            url,
            referrerUrl,
            signInUrl,
            assumeSignedInWhenNavigatedToUrl,
            signInOptions,
            navigationTimingOptions,
            captureAsync: (session, navigationOptions, ct) =>
                captureService.NavigateAndCaptureResourcesResultAsync(
                    session, navigationOptions, extensions, ct, rewriteSpec, shouldCompleteCapture, captureTimingOptions),
            cancellationToken)
            .ConfigureAwait(false);

        return (result.Bands, result.Albums);
    }

    /// <inheritdoc/>
    public async Task<(Bands Bands, Albums Albums)> CaptureBandsAndAlbumsAsync(
        Uri url,
        Uri? referrerUrl,
        Uri? signInUrl,
        Uri? assumeSignedInWhenNavigatedToUrl,
        SignInOptions signInOptions,
        NavigationTimingOptions navigationTimingOptions,
        CaptureTimingOptions captureTimingOptions,
        RewriteSpec? rewriteSpec,
        Uri[] urlsToCapture,
        CancellationToken cancellationToken)
    {
        var orchestrator = new CaptureOrchestrator(logger, browserSessionService, resilienceWrapper, classifier, probe);

        var result = await orchestrator.CaptureBandsAndAlbumsAsync(
            url,
            referrerUrl,
            signInUrl,
            assumeSignedInWhenNavigatedToUrl,
            signInOptions,
            navigationTimingOptions,
            captureAsync: async (session, navigationOptions, ct) =>
            {
                // Extension method.
                var result = await captureService.NavigateAndCaptureResourcesAsync(
                    session,
                    url,
                    urlsToCapture,
                    ct,
                    referrerUrl,
                    navigationTimingOptions.PageLoadTimeout(),
                    captureTimingOptions.NetworkIdleTimeout(),
                    captureTimingOptions.CaptureTimeout(),
                    captureTimingOptions.PollInterval(),
                    rewriteSpec)
                .ConfigureAwait(false);
                return new PageCaptureResult(null, null, result, null, null);
            },
            cancellationToken).ConfigureAwait(false);

        return (result.Bands, result.Albums);
    }
}
