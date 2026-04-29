using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Enums;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Playwright.Services;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Sessions;

/// <summary>
/// Represents a Playwright-based browser session for navigating to web pages and capturing content and resources, with optional
/// HTTP response rewriting.
/// </summary>
/// <remarks>
/// <para>
/// This class manages the lifecycle of a Playwright browser context and page, providing navigation and capture operations that
/// are cancellable and resilient to transient browser closure.
/// </para>
/// <para>
/// Implements <see cref="IBrowserSession"/>.  Use <see cref="Dispose"/> or <see cref="DisposeAsync"/> to release browser
/// resources when finished.
/// </para>
/// <para>
/// Cancellation is supported via <see cref="CancellationToken"/> for all asynchronous operations.
/// </para>
/// </remarks>
internal sealed class PlaywrightBrowserSession(
    ILogger<PlaywrightBrowserSession> logger,
    IBrowser browser,
    IBrowserContext context,
    IPlaywrightPageCaptureService pageCaptureService)
    : IBrowserSession
{
    private bool _disposed;

    /// <inheritdoc />
    public Task<PageCaptureResult> NavigateAndCaptureResultAsync(
        PageCaptureParts captureParts,
        NavigationOptions navOptions,
        CaptureSpec? captureSpec,
        CaptureTimingOptions captureTimingOptions,
        CancellationToken cancellationToken) =>
        NavigateAndCaptureResultAsync(captureParts, navOptions, captureSpec, null, captureTimingOptions, cancellationToken);

    /// <inheritdoc />
    public async Task<PageCaptureResult> NavigateAndCaptureResultAsync(
        PageCaptureParts captureParts,
        NavigationOptions navOptions,
        CaptureSpec? captureSpec,
        RewriteSpec? rewriteSpec,
        CaptureTimingOptions captureTimingOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(navOptions);
        ArgumentNullException.ThrowIfNull(navOptions.Url);
        ArgumentNullException.ThrowIfNull(captureTimingOptions);
        ObjectDisposedException.ThrowIf(_disposed, this);

        cancellationToken.ThrowIfCancellationRequested();

        var page = await context.NewPageAsync().ConfigureAwait(false);
        try
        {
            return await pageCaptureService.NavigateAndCaptureResultAsync(
                page,
                captureParts,
                navOptions,
                captureSpec,
                rewriteSpec,
                captureTimingOptions,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            logger.LogDebug("Closing page for: {Url}", navOptions.Url);
            try { await page.CloseAsync().ConfigureAwait(false); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to close page for: {Url}", navOptions.Url);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        await PlaywrightSessionHelper.DisposeAsync(context, browser, logger).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}