using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

using Metalhead.BrowserCaptureRewrite.Abstractions.Connectivity;
using Metalhead.BrowserCaptureRewrite.Abstractions.Enums;
using Metalhead.BrowserCaptureRewrite.Abstractions.Exceptions;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Playwright.Services;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IPlaywrightCaptureSampleService"/> for capturing the rendered HTML of a
/// web page using Playwright.
/// </summary>
/// <remarks>
/// Implements <see cref="IPlaywrightCaptureSampleService"/>.
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
public sealed class PlaywrightCaptureSampleService(
    ILogger<PlaywrightCaptureSampleService> logger,
    IPlaywrightSessionService playwrightSessionService,
    IPlaywrightPageCaptureService playwrightCaptureService,
    IConnectivityClassifier classifier,
    IConnectivityProbe probe)
    : BaseOrchestrator(logger, classifier, probe), IPlaywrightCaptureSampleService
{
    /// <inheritdoc/>
    public async Task<string> FetchRenderedHtmlAsync(
        Uri url,
        Uri? referrerUrl,
        Uri? signInUrl,
        Uri? assumeSignedInWhenNavigatedToUrl,
        SignInOptions signInOptions,
        NavigationTimingOptions navigationTimingOptions,
        CaptureTimingOptions captureTimingOptions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Create a Playwright session, which includes browser and context initialization, and optionally signing-in if signInUrl is provided.
            await using var sessionHandle = await playwrightSessionService.CreatePlaywrightSessionOrThrowAsync(
                signInUrl,
                assumeSignedInWhenNavigatedToUrl,
                signInOptions.AssumeSignedInAfter(),
                signInOptions.PageLoadTimeout(),
                cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            IPage? page = null;
            try
            {
                page = await sessionHandle.Context.NewPageAsync().ConfigureAwait(false);

                // Playwright IPage operations (e.g. navigation) can be performed using the 'page' instance.
                await page.GotoAsync("https://www.google.com/");
                await Task.Delay(2000, cancellationToken);

                var navOptions = new NavigationOptions(url, referrerUrl, navigationTimingOptions.PageLoadTimeout());

                // Capture the rendered HTML and response HTML (using PageCaptureParts flags) with the Playwright page capture service.
                var pageCaptureResult = await playwrightCaptureService.NavigateAndCaptureResultAsync(
                    page,
                    PageCaptureParts.RenderedHtml | PageCaptureParts.ResponseHtml,
                    navOptions,
                    null,
                    captureTimingOptions,
                    cancellationToken)
                    .ConfigureAwait(false);

                return pageCaptureResult.RenderedHtml ?? string.Empty;
            }
            finally
            {
                if (page is not null)
                {
                    try { await page.CloseAsync().ConfigureAwait(false); }
                    catch { }
                }
            }
        }
        catch (BrowserSessionInitializationException ex)
        {
            _logger.LogError(ex, "Browser/session initialization failed.  Details: {Details}", ex.Message);
            throw;
        }
        catch (SignInException ex)
        {
            _logger.LogError(ex, "Sign-in failed.  Details: {Details}", ex.Message);
            throw;
        }
    }
}
