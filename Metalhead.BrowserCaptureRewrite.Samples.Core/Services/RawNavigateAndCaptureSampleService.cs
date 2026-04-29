using Microsoft.Extensions.Logging;

using Metalhead.BrowserCaptureRewrite.Abstractions.Connectivity;
using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Enums;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Services;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Models;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Services;

/// <summary>
/// Provides a sample service that captures bands, albums, and rendered HTML from a web page using raw navigation
/// and resource capture strategies.
/// </summary>
/// <param name="logger">The logger used for diagnostic output.  Must not be <see langword="null"/>.</param>
/// <param name="browserSessionService">The service used to create and manage browser sessions.  Must not be
/// <see langword="null"/>.</param>
/// <param name="resilienceWrapper">The resilience wrapper applied to browser session operations.  Must not be
/// <see langword="null"/>.</param>
/// <param name="classifier">The connectivity classifier used to assess network conditions.  Must not be
/// <see langword="null"/>.</param>
/// <param name="probe">The connectivity probe used to test network availability.  Must not be <see langword="null"/>.</param>
/// <remarks>
/// <para>
/// Implements <see cref="IRawNavigateAndCaptureSampleService"/>.
/// </para>
/// <para>
/// Cancellation is supported via <see cref="CancellationToken"/> on all asynchronous operations.  If cancellation is requested
/// before or during capture, an <see cref="OperationCanceledException"/> is thrown.  If the browser or page is externally
/// closed, an <see cref="OperationCanceledException"/> is also thrown.
/// </para>
/// </remarks>
public sealed class RawNavigateAndCaptureSampleService(
    ILogger<RawNavigateAndCaptureSampleService> logger,
    IBrowserSessionService browserSessionService,
    IBrowserSessionResilienceWrapper resilienceWrapper,
    IConnectivityClassifier classifier,
    IConnectivityProbe probe)
    : IRawNavigateAndCaptureSampleService
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
        PageCaptureParts captureParts = PageCaptureParts.RenderedHtml | PageCaptureParts.Resources;

        var result = await orchestrator.CaptureBandsAndAlbumsAsync(
            url,
            referrerUrl,
            signInUrl,
            assumeSignedInWhenNavigatedToUrl,
            signInOptions,
            navigationTimingOptions,
            captureAsync: (session, navigationOptions, ct) =>
                session.NavigateAndCaptureResultAsync(
                    captureParts, navigationOptions, captureSpec, rewriteSpec, captureTimingOptions, ct),
            cancellationToken).ConfigureAwait(false);

        return (result.Bands, result.Albums, result.PageCaptureResult.RenderedHtml);
    }
}
