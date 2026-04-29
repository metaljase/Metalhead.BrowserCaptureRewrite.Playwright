using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

using Metalhead.BrowserCaptureRewrite.Abstractions.Enums;
using Metalhead.BrowserCaptureRewrite.Abstractions.Helpers;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Playwright.Transport;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Services;

/// <summary>
/// Provides Playwright-based page capture functionality, supporting navigation, resource capture, and optional response rewriting.
/// </summary>
/// <remarks>
/// <para>
/// This service enables capturing various aspects of a web page, including the original HTTP response HTML, rendered HTML,
/// and matching resources.  It supports response rewriting via route interception and allows for custom completion conditions
/// when capturing resources.
/// </para>
/// <para>
/// Implements <see cref="IPlaywrightPageCaptureService"/>.
/// </para>
/// <para>
/// Cancellation is supported via <see cref="CancellationToken"/> for all asynchronous operations.  The service is designed for browser
/// automation scenarios.
/// </para>
/// </remarks>
public sealed class PlaywrightPageCaptureService(ILogger<PlaywrightPageCaptureService> logger) : IPlaywrightPageCaptureService
{
    private const string s_rewriteRoutePattern = "**/*";

    /// <inheritdoc />
    public Task<PageCaptureResult> NavigateAndCaptureResultAsync(
        IPage page,
        PageCaptureParts captureParts,
        NavigationOptions navOptions,
        CaptureSpec? captureSpec,
        CaptureTimingOptions captureTimingOptions,
        CancellationToken cancellationToken) =>
        NavigateAndCaptureResultAsync(page, captureParts, navOptions, captureSpec, null, captureTimingOptions, cancellationToken);

    /// <inheritdoc />
    public async Task<PageCaptureResult> NavigateAndCaptureResultAsync(
        IPage page,
        PageCaptureParts captureParts,
        NavigationOptions navOptions,
        CaptureSpec? captureSpec,
        RewriteSpec? rewriteSpec,
        CaptureTimingOptions captureTimingOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(navOptions);
        ArgumentNullException.ThrowIfNull(navOptions.Url);
        ArgumentNullException.ThrowIfNull(captureTimingOptions);

        cancellationToken.ThrowIfCancellationRequested();

        bool includeResponseHtml = captureParts.HasFlag(PageCaptureParts.ResponseHtml);
        bool includeRenderedHtml = captureParts.HasFlag(PageCaptureParts.RenderedHtml);
        bool includeResources = captureParts.HasFlag(PageCaptureParts.Resources);

        if (captureParts == PageCaptureParts.None)
            throw new ArgumentException("No capture parts specified.", nameof(captureParts));
        if (includeResources && captureSpec is null)
            throw new ArgumentNullException(nameof(captureSpec), "Resource capture spec required when capturing resources.");

        // Linked CTS that can be cancelled once completion criteria is satisfied.
        using var captureCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Install route interception for optional response rewriting.
        Func<IRoute, Task>? rewriteRouteHandler = null;
        string? rewrittenNavigationResponseHtml = null;
        if (rewriteSpec is not null)
        {
            rewriteRouteHandler = route => TryRewriteRouteResponseAsync(
                route,
                rewriteSpec,
                includeResponseHtml ? html => rewrittenNavigationResponseHtml = html : null,
                cancellationToken,
                captureCts.Token);
            await page.RouteAsync(s_rewriteRoutePattern, rewriteRouteHandler).ConfigureAwait(false);
        }

        // Event handler to capture matching requests, then store the URL and content.
        var captured = includeResources ? new ConcurrentBag<CapturedResource>() : null;
        var captureTasks = includeResources ? new ConcurrentBag<Task>() : null;
        var requestHandler = AttachResourceCaptureHandler(
            page, includeResources, captureSpec, captured, captureTasks, captureCts, cancellationToken);

        string? responseHtml = null;
        string? originalResponseHtml = null;
        string? renderedHtml = null;
        PageLoadStatus? pageLoadStatus = null;
        CaptureStatus? completionStatus = null;
        DateTime navigationStart = DateTime.UtcNow;

        try
        {
            try
            {
                var response = await NavigateAsync(
                    page,
                    navOptions,
                    includeResponseHtml,
                    html => originalResponseHtml = html,
                    cancellationToken).ConfigureAwait(false);

                if (includeResponseHtml)
                    responseHtml = rewrittenNavigationResponseHtml ?? originalResponseHtml;

                if (response?.Status == 404)
                    throw new HttpRequestException(
                        $"Server responded with status {HttpStatusCode.NotFound} (404) for URL: {navOptions.Url}",
                        null,
                        HttpStatusCode.NotFound);
            }
            catch (PlaywrightException ex) when (IsTargetClosed(ex))
            {
                throw CreateBrowserClosedOperationCanceledException("Browser/page was closed during navigation.", ex);
            }

            // Wait for network idle only when capturing resources or rendered HTML, and no completion condition was provided.
            if (captureSpec?.ShouldCompleteCapture is null && (includeResources || includeRenderedHtml))
                pageLoadStatus = await WaitForNetworkIdleAsync(page, navOptions.Url, captureTimingOptions, cancellationToken).ConfigureAwait(false);

            // Just because Playwright has detected zero network traffic over a 500ms duration (or timeout), that doesn't necessarily
            // mean the page has finished loading all the content; there could be delays between requests/responses of over 500ms...
            // If a completion predicate/condition was provided, wait until it returns true before closing the browser.
            // e.g. Wait for a certain amount of time to elapse or for certain content to be captured before closing the browser.
            if (includeResources && captureSpec!.ShouldCompleteCapture is not null)
            {
                try
                {
                    completionStatus = await WaitForCaptureCompletionAsync(
                        page, captureSpec, captured!, captureTimingOptions, navigationStart, navOptions, cancellationToken);
                }
                finally
                {
                    // Stop accepting new tasks and cancel in-flight ones.
                    captureCts.Cancel();
                    if (requestHandler is not null)
                        page.Request -= requestHandler;
                }

                // If not cancelled, await capture tasks to complete.
                if (!cancellationToken.IsCancellationRequested)
                {
                    var tasksSnapshot = captureTasks!.ToArray();
                    logger.LogDebug("Waiting for {Count} capture tasks to complete...", tasksSnapshot.Length);
                    try
                    {
                        await Task.WhenAll(tasksSnapshot).WaitAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
                        logger.LogDebug("All capture tasks complete.");
                    }
                    catch (TimeoutException)
                    {
                        logger.LogWarning("Timeout waiting for capture tasks to complete for: {Url}", navOptions.Url);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "One or more capture tasks failed to complete for: {Url}", navOptions.Url);
                    }
                }
            }

            if (includeRenderedHtml)
                try { renderedHtml = await page.ContentAsync().ConfigureAwait(false); } catch { }
        }
        finally
        {
            if (requestHandler is not null)
                page.Request -= requestHandler;

            if (rewriteRouteHandler is not null)
            {
                try
                {
                    await page.UnrouteAsync(s_rewriteRoutePattern, rewriteRouteHandler).ConfigureAwait(false);
                }
                catch (PlaywrightException ex) when (IsTargetClosed(ex))
                {
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to remove route interception for: {Url}", navOptions.Url);
                }
            }
        }

        var resourcesList = captured is null ? [] : captured.ToList();
        logger.LogDebug("Captured {Count} resources for: {Url}", resourcesList.Count, navOptions.Url);
        return new PageCaptureResult(responseHtml, renderedHtml, resourcesList, pageLoadStatus, completionStatus);
    }

    private async Task TryRewriteRouteResponseAsync(
        IRoute route,
        RewriteSpec rewriteSpec,
        Action<string?>? setRewrittenNavigationResponseHtml,
        CancellationToken cancellationToken,
        CancellationToken captureToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested || captureToken.IsCancellationRequested)
            {
                await route.ContinueAsync().ConfigureAwait(false);
                return;
            }

            var req = route.Request;
            var requestInfo = new PlaywrightRequestInfo(req);

            // Only attempt rewrite for requests the spec wants to rewrite.
            if (!rewriteSpec.ShouldRewrite(requestInfo))
            {
                await route.ContinueAsync().ConfigureAwait(false);
                return;
            }

            // Fetch response.
            var response = await route.FetchAsync().ConfigureAwait(false);
            var bodyText = string.Empty;
            int? statusCode = null;
            IReadOnlyDictionary<string, string> headers = response.Headers;
            try
            {
                bodyText = await response.TextAsync().ConfigureAwait(false);
                statusCode = response.Status;
            }
            catch { }

            // Attempt response rewrite.
            var responseInfo = new MinimalResponseInfo(bodyText, statusCode, headers);
            var rewriteResult = await rewriteSpec.TryRewriteResponseAsync(requestInfo, responseInfo)
                .ConfigureAwait(false);
            if (!rewriteResult.IsRewritten)
            {
                // Continue without rewrite.
                await route.ContinueAsync().ConfigureAwait(false);
                return;
            }

            // Fulfill with rewritten response.
            var contentType = rewriteResult.ContentTypeOverride ?? (response.Headers.TryGetValue("content-type", out var ct) ? ct : null);
            if (req.IsNavigationRequest)
                setRewrittenNavigationResponseHtml?.Invoke(rewriteResult.NewBody);

            await route.FulfillAsync(new()
            {
                Status = response.Status,
                ContentType = contentType,
                Body = rewriteResult.NewBody ?? string.Empty
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Route interception failed for: {Url}", route.Request.Url);
            try { await route.ContinueAsync().ConfigureAwait(false); } catch { }
        }
    }

    private EventHandler<IRequest>? AttachResourceCaptureHandler(
        IPage page,
        bool captureResources,
        CaptureSpec? captureSpec,
        ConcurrentBag<CapturedResource>? captured,
        ConcurrentBag<Task>? captureTasks,
        CancellationTokenSource captureCts,
        CancellationToken cancellationToken)
    {
        if (!captureResources)
            return null;

        var handler = new EventHandler<IRequest>((_, request) =>
        {
            try
            {
                if (cancellationToken.IsCancellationRequested || captureCts.IsCancellationRequested)
                    return;
                var requestInfo = new PlaywrightRequestInfo(request);
                if (!captureSpec!.ShouldCapture(requestInfo))
                    return;
                var t = TryCaptureResourceAsync(request, captureSpec, captured!, captureCts.Token);
                captureTasks!.Add(t);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to enqueue capture for: {Url}", request.Url);
            }
        });

        page.Request += handler;
        return handler;
    }

    private static async Task TryCaptureResourceAsync(
        IRequest request, CaptureSpec captureSpec, ConcurrentBag<CapturedResource> target, CancellationToken ct)
    {
        IResponse? response;
        try
        {
            response = await request.ResponseAsync().WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (PlaywrightException ex) when (IsTargetClosed(ex))
        {
            // Page/browser closed mid-flight; ignore this request.
            return;
        }
        if (response is null)
            return;

        CapturedResource? capturedResource = null;
        try
        {
            capturedResource = await captureSpec
                .TryCreateCapturedResourceAsync(new PlaywrightRequestInfo(request), new PlaywrightResponseInfo(response, ct))
                .WaitAsync(ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (capturedResource is not null)
            target.Add(capturedResource);
    }

    private static async Task<IResponse?> NavigateAsync(
        IPage page,
        NavigationOptions navOptions,
        bool captureResponseHtml,
        Action<string?> setResponseHtml,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var gotoOptions = new PageGotoOptions();
        if (navOptions.PageLoadTimeout.HasValue)
            gotoOptions.Timeout = (float)navOptions.PageLoadTimeout.Value.TotalMilliseconds;
        if (navOptions.RefererUrl is not null)
            gotoOptions.Referer = navOptions.RefererUrl.ToString();

        // Navigate to the URL.
        var response = await page.GotoAsync(navOptions.Url.ToString(), gotoOptions).ConfigureAwait(false);

        if (response is not null)
        {
            // Throw an exception for transient HTTP status codes to enable external resilience/retry policy handling.
            var status = response.Status;
            if (HttpHelper.IsRetryableHttpRequestException((HttpStatusCode)status))
                throw new HttpRequestException(
                    $"Server responded with status {(HttpStatusCode?)status} ({status}) for URL: {navOptions.Url}",
                    null,
                    (HttpStatusCode?)status);

            if (captureResponseHtml)
                try { setResponseHtml(await response.TextAsync().ConfigureAwait(false)); } catch { }
        }

        return response;
    }

    private async Task<PageLoadStatus> WaitForNetworkIdleAsync(
        IPage page, Uri url, CaptureTimingOptions captureTimingOptions, CancellationToken cancellationToken)
    {
        try
        {
            // Using LoadState.NetworkIdle will cause WaitForLoadStateAsync to wait for zero network traffic over a 500ms duration.
            // If LoadState.NetworkIdle is used without a timeout specified in PageWaitForLoadStateOptions, a TimeoutException will be
            // thrown after 30 seconds (default) if there hasn't been zero network traffic over a 500ms duration.
            // If a timeout is provided, a TimeoutException will be thrown after the specified timeout if there hasn't been zero
            // network traffic over a 500ms duration.  A timeout of 0 will wait indefinitely for a 500ms duration of zero network traffic.
            var networkIdleTimeout = captureTimingOptions.NetworkIdleTimeout();
            if (networkIdleTimeout.HasValue)
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
                    {
                        Timeout = (float)networkIdleTimeout.Value.TotalMilliseconds
                    }).ConfigureAwait(false);
            else
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
        }
        catch (PlaywrightException ex) when (IsTargetClosed(ex))
        {
            throw CreateBrowserClosedOperationCanceledException("Browser/page was closed while waiting for network idle.", ex);
        }
        catch (TimeoutException)
        {
            logger.LogDebug("Timeout waiting for network idle after navigating to: {Url}", url);
            return PageLoadStatus.NetworkIdleTimeoutExceeded;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(
                "Task cancelled (likely due to timeout or network issue) waiting for network idle after navigating to: {Url}", url);
            return PageLoadStatus.NetworkIdleTimeoutExceeded;
        }

        return PageLoadStatus.Completed;
    }

    private async Task<CaptureStatus?> WaitForCaptureCompletionAsync(
        IPage page,
        CaptureSpec captureSpec,
        ConcurrentBag<CapturedResource> captured,
        CaptureTimingOptions captureTimingOptions,
        DateTime navigationStart,
        NavigationOptions navOptions,
        CancellationToken cancellationToken)
    {
        if (captureSpec?.ShouldCompleteCapture is null)
            return null;

        logger.LogDebug("Polling capture completion condition for: {Url}", navOptions.Url);

        var snapshot = new List<CapturedResource>();
        var pollInterval = captureTimingOptions.PollInterval();
        var resourceTimeout = captureTimingOptions.CaptureTimeout();
        CaptureStatus? completionStatus = null;

        using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        void pageClosedHandler(object? _, IPage __) => waitCts.Cancel();
        page.Close += pageClosedHandler;

        try
        {
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    cancellationToken.ThrowIfCancellationRequested();

                // Throw if the linked token was cancelled by the page-close handler.
                if (waitCts.IsCancellationRequested)
                    throw CreateBrowserClosedOperationCanceledException(
                        "Browser/page was closed while waiting for capture completion.", waitCts.Token);

                if (page.IsClosed)
                    throw CreateBrowserClosedOperationCanceledException(
                        "Browser/page was closed while waiting for capture completion.", waitCts.Token);

                snapshot.Clear();
                snapshot.AddRange(captured);

                var completionResult = captureSpec.ShouldCompleteCapture(navOptions, snapshot, navigationStart)
                    ? new CaptureCompletionResult(CaptureStatus.CriteriaSatisfied)
                    : new CaptureCompletionResult(CaptureStatus.CriteriaNotSatisfied);
                if (completionResult.IsComplete)
                {
                    completionStatus = completionResult.Status;
                    logger.LogDebug("Resource completion status '{Status}' for: {Url}", completionStatus, navOptions.Url);
                    break;
                }
                if (resourceTimeout.HasValue && resourceTimeout.Value > TimeSpan.Zero && DateTime.UtcNow - navigationStart >= resourceTimeout.Value)
                {
                    completionStatus = CaptureStatus.CaptureTimeoutExceeded;
                    logger.LogDebug(
                        "Resource capture timeout exceeded after {Timeout} for: {Url}",
                        HumanizeHelper.FormatDuration(resourceTimeout.Value),
                        navOptions.Url);
                    break;
                }

                try
                {
                    if (!page.Url.Equals(navOptions.Url.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        completionStatus = CaptureStatus.UrlChangedBeforeCompletion;
                        logger.LogDebug(
                            "Page navigated away from capture URL before completion criteria was satisfied for: {Url}", navOptions.Url);
                        break;
                    }
                }
                catch (PlaywrightException ex) when (IsTargetClosed(ex))
                {
                    // `page.Url` can race with window closure; when Playwright reports the target closed here.
                    throw CreateBrowserClosedOperationCanceledException(
                        "Browser/page was closed while waiting for capture completion.", waitCts.Token, ex);
                }

                try
                {
                    await Task.Delay(pollInterval, waitCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    throw CreateBrowserClosedOperationCanceledException(
                        "Browser/page was closed while waiting for capture completion.", waitCts.Token);
                }
            }
        }
        finally
        {
            page.Close -= pageClosedHandler;
        }

        return completionStatus;
    }

    private static bool IsTargetClosed(PlaywrightException ex)
    {
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("net::ERR_ABORTED; maybe frame was detached?", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Target page, context or browser has been closed", StringComparison.OrdinalIgnoreCase);
    }

    private static OperationCanceledException CreateBrowserClosedOperationCanceledException(
        string message, Exception? innerException = null) => innerException is null
            ? new OperationCanceledException(message)
            : new OperationCanceledException(message, innerException);

    private static OperationCanceledException CreateBrowserClosedOperationCanceledException(
        string message, CancellationToken cancellationToken, Exception? innerException = null) => innerException is null
            ? new OperationCanceledException(message, cancellationToken)
            : new OperationCanceledException(message, innerException, cancellationToken);
}
