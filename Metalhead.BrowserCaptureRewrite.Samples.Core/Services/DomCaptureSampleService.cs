using Microsoft.Extensions.Logging;

using Metalhead.BrowserCaptureRewrite.Abstractions.Connectivity;
using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Services;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IDomCaptureSampleService"/> for capturing the rendered HTML of a web page.
/// </summary>
/// <remarks>
/// Implements <see cref="IDomCaptureSampleService"/>.
/// <para>
/// Cancellation is supported via <see cref="CancellationToken"/>.  If cancellation is requested before or during capture, an
/// <see cref="OperationCanceledException"/> is thrown.  If the browser or page is externally closed, an
/// <see cref="OperationCanceledException"/> is also thrown.
/// </para>
/// <para>
/// Exceptions during session creation, sign-in, or capture are logged and rethrown.  Connectivity errors are classified and
/// logged using orchestrator logic.
/// </para>
/// </remarks>
public sealed class DomCaptureSampleService(
    ILogger<DomCaptureSampleService> logger,
    IBrowserSessionService browserSessionService,
    IBrowserSessionResilienceWrapper resilienceWrapper,
    IBrowserDomService domService,
    IConnectivityClassifier classifier,
    IConnectivityProbe probe)
    : IDomCaptureSampleService
{
    /// <inheritdoc/>
    public async Task<string?> CaptureBandsAndAlbumsAsync(
        Uri url,
        Uri? referrerUrl,
        Uri? signInUrl,
        Uri? assumeSignedInWhenNavigatedToUrl,
        SignInOptions signInOptions,
        NavigationTimingOptions navigationTimingOptions,
        CaptureTimingOptions captureTimingOptions,
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
                domService.NavigateAndCaptureRenderedHtmlResultAsync(session, navigationOptions, captureTimingOptions.NetworkIdleTimeout(), ct),
            cancellationToken).ConfigureAwait(false);

        return result.PageCaptureResult.RenderedHtml;
    }
}
