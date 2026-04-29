using Microsoft.Extensions.Logging;

using Metalhead.BrowserCaptureRewrite.Abstractions.Connectivity;
using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Services;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Models;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IDomAndResourcesCaptureSampleService"/> for capturing bands, albums,
/// and rendered HTML using DOM and resource capture strategies.
/// </summary>
/// <remarks>
/// Implements <see cref="IDomAndResourcesCaptureSampleService"/>.
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
public sealed class DomAndResourcesCaptureSampleService(
    ILogger<DomAndResourcesCaptureSampleService> logger,
    IBrowserSessionService browserSessionService,
    IBrowserSessionResilienceWrapper resilienceWrapper,
    IBrowserDomCaptureService domCaptureService,
    IConnectivityClassifier classifier,
    IConnectivityProbe probe)
    : IDomAndResourcesCaptureSampleService
{
    /// <inheritdoc/>
    public async Task<(Bands Bands, Albums Albums, string? RenderedHtml)> CaptureBandsAndAlbumsAsync(
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
                domCaptureService.NavigateAndCaptureHtmlAndResourcesResultAsync(
                    session, navigationOptions, captureSpec, rewriteSpec, ct, captureTimingOptions),
            cancellationToken).ConfigureAwait(false);

        return (result.Bands, result.Albums, result.PageCaptureResult.RenderedHtml);
    }
}
