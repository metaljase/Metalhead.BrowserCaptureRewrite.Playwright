using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

using Metalhead.BrowserCaptureRewrite.Abstractions.Connectivity;
using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Exceptions;
using Metalhead.BrowserCaptureRewrite.Abstractions.Factories;
using Metalhead.BrowserCaptureRewrite.Abstractions.Helpers;
using Metalhead.BrowserCaptureRewrite.Abstractions.Resilience;
using Metalhead.BrowserCaptureRewrite.Playwright.Services;
using Metalhead.BrowserCaptureRewrite.Playwright.Sessions;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Factories;

/// <summary>
/// Provides a factory for creating Playwright-based browser sessions and session handles, supporting both standard and
/// sign-in scenarios.
/// </summary>
/// <remarks>
/// Implements <see cref="IBrowserSessionFactory"/>, <see cref="ISignInBrowserSessionFactory"/>,
/// <see cref="IPlaywrightSessionHandleFactory"/>, and <see cref="IPlaywrightSignInSessionHandleFactory"/>.
/// <para>
/// All session creation methods support cancellation via <see cref="CancellationToken"/>.  If cancellation is requested before or
/// during browser/context creation, or during sign-in navigation, an <see cref="OperationCanceledException"/> is thrown.  If the
/// browser or page is externally closed, an <see cref="OperationCanceledException"/> is also thrown.
/// </para>
/// <para>
/// This factory launches browsers in headful (non-headless) mode by default, enabling user interaction with the browser window
/// during automation.  This is intentional to support scenarios such as video playback scrubbing and retry UX.
/// </para>
/// </remarks>
public sealed class PlaywrightBrowserSessionFactory(
    ILogger<PlaywrightBrowserSessionFactory> logger,
    ILoggerFactory loggerFactory,
    IPlaywright playwright,
    IPlaywrightPageCaptureService pageCaptureService,
    IResiliencePolicyFactory resilienceFactory,
    IConnectivityProbe probe,
    IConnectivityClassifier classifier)
    : IBrowserSessionFactory, ISignInBrowserSessionFactory, IPlaywrightSessionHandleFactory, IPlaywrightSignInSessionHandleFactory
{
    /// <inheritdoc/>
    public Task<IBrowserSession> CreateSessionAsync(SessionOptions options, CancellationToken cancellationToken)
        => CreateBrowserSessionAsync(options, signInUrl: null, cancellationToken);

    /// <inheritdoc/>
    public Task<IBrowserSession> CreateSignInSessionAsync(Uri signInUrl, SessionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signInUrl);
        return CreateBrowserSessionAsync(options, signInUrl, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IPlaywrightSessionHandle> CreateSessionHandleAsync(SessionOptions options, CancellationToken cancellationToken)
        => CreateSessionHandleCoreAsync(options, signInUrl: null, cancellationToken: cancellationToken);

    /// <inheritdoc/>
    public Task<IPlaywrightSessionHandle> CreateSignInSessionHandleAsync(Uri signInUrl, SessionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signInUrl);
        return CreateSessionHandleCoreAsync(options, signInUrl, cancellationToken: cancellationToken);
    }

    private async Task<IBrowserSession> CreateBrowserSessionAsync(SessionOptions options, Uri? signInUrl, CancellationToken cancellationToken)
    {
        var (browser, context) = await CreateSessionComponentsAsync(options, signInUrl, cancellationToken).ConfigureAwait(false);
        var sessionLogger = loggerFactory.CreateLogger<PlaywrightBrowserSession>();
        return new PlaywrightBrowserSession(sessionLogger, browser, context, pageCaptureService);
    }

    private async Task<IPlaywrightSessionHandle> CreateSessionHandleCoreAsync(
        SessionOptions options, Uri? signInUrl, CancellationToken cancellationToken)
    {
        var (browser, context) = await CreateSessionComponentsAsync(options, signInUrl, cancellationToken).ConfigureAwait(false);
        return new PlaywrightSessionHandle(browser, context);
    }

    private async Task<(IBrowser Browser, IBrowserContext Context)> CreateSessionComponentsAsync(
        SessionOptions options, Uri? signInUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        IBrowser? browser = null;
        IBrowserContext? context = null;
        try
        {
            browser = await LaunchBrowserAsync(options, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            context = await CreateContextAsync(browser, options).ConfigureAwait(false);

            if (signInUrl is not null)
                await SignInAsync(context, signInUrl, options, cancellationToken).ConfigureAwait(false);

            return (browser, context);
        }
        catch
        {
            await PlaywrightSessionHelper.DisposeAsync(context, browser, logger).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<IBrowser> LaunchBrowserAsync(SessionOptions options, CancellationToken cancellationToken)
    {
        var launch = new BrowserTypeLaunchOptions { Headless = options.BrowserOptions.Headless };
        IBrowserType browserType = options.BrowserOptions.Browser switch
        {
            BrowserEngine.WebKit => playwright.Webkit,
            BrowserEngine.Firefox => playwright.Firefox,
            _ => playwright.Chromium
        };

        launch.Channel = options.BrowserOptions.Browser switch
        {
            BrowserEngine.Chrome => "chrome",
            BrowserEngine.ChromeDev => "chrome-dev",
            BrowserEngine.Edge => "msedge",
            _ => null
        };

        // Args are compatible with Chromium based browsers, e.g, Firefox will open an additional window and navigate to http://automationcontrolled.
        if (options.BrowserOptions.Browser == BrowserEngine.Chromium
            || options.BrowserOptions.Browser == BrowserEngine.Chrome
            || options.BrowserOptions.Browser == BrowserEngine.ChromeDev
            || options.BrowserOptions.Browser == BrowserEngine.Edge)
            launch.Args = ["--disable-blink-features=AutomationControlled", "--window-position=0,0"];

        if (browserType == playwright.Chromium)
            launch.ExecutablePath = options.BrowserOptions.ExecutablePath;

        try
        {
            return await browserType.LaunchAsync(launch).ConfigureAwait(false);
        }
        catch (PlaywrightException ex) when (IsPlaywrightBrowserInstallMissing(ex))
        {
            throw new BrowserAutomationEngineNotAvailableException(ex.Message, null, ex);
        }
        catch (PlaywrightException ex) when (IsTargetClosed(ex))
        {
            throw new OperationCanceledException("Browser/page was closed during launch.", ex, cancellationToken);
        }
    }

    private static Task<IBrowserContext> CreateContextAsync(IBrowser browser, SessionOptions options)
        => browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = options.BrowserOptions.UserAgent,
            ViewportSize = options.BrowserOptions.ViewportWidth.HasValue && options.BrowserOptions.ViewportWidth > 0 && options.BrowserOptions.ViewportHeight.HasValue && options.BrowserOptions.ViewportHeight > 0
                ? new ViewportSize { Width = options.BrowserOptions.ViewportWidth.Value, Height = options.BrowserOptions.ViewportHeight.Value }
                : null
        });

    private async Task SignInAsync(IBrowserContext context, Uri signInUrl, SessionOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IPage? page = null;
        try
        {
            page = await context.NewPageAsync().ConfigureAwait(false);

            var navigationOptions = new PageGotoOptions();
            if (options.SignInPageLoadTimeout.HasValue)
                navigationOptions.Timeout = (float)options.SignInPageLoadTimeout.Value.TotalMilliseconds;

            try
            {
                // Attempt to navigate to the sign-in URL without resiliency to determine if the page is reachable, e.g. not a 404 Not Found.
                IResponse? initialResponse = null;
                try
                {
                    initialResponse = await page.GotoAsync(signInUrl.ToString(), navigationOptions).ConfigureAwait(false);
                }
                catch {}

                // If the response returned 404 Not Found, throw an exception which will be caught and wrapped as a SignInException.
                if (initialResponse is not null && initialResponse.Status == 404)
                    throw new HttpRequestException(
                        $"Server responded with status {HttpStatusCode.NotFound} (404) for URL: {signInUrl}",
                        null,
                        HttpStatusCode.NotFound);

                if (options.UseResilienceForSignIn.GetValueOrDefault())
                {
                    var policy = resilienceFactory.BuildResiliencePolicy<bool>(signInUrl, null, cancellationToken);
                    await policy.ExecuteAsync(
                        async ct =>
                        {
                            ct.ThrowIfCancellationRequested();

                            // If navigating to the sign-in URL was successful use that response, otherwise attempt navigation with resilience.
                            IResponse? response = initialResponse is not null && initialResponse.Status == 200
                                ? (IResponse?)initialResponse
                                : await page.GotoAsync(signInUrl.ToString(), navigationOptions).ConfigureAwait(false);

                            if (response is not null)
                            {
                                // Throw an exception for transient HTTP status codes to enable external resilience/retry policy handling.
                                var status = response.Status;
                                if (HttpHelper.IsRetryableHttpRequestException((HttpStatusCode)status))
                                    throw new HttpRequestException(
                                        $"Server responded with status {(HttpStatusCode?)status} ({status}) for URL: {signInUrl}",
                                        null,
                                        (HttpStatusCode?)status);
                            }

                            return true;
                        }, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        // If navigating to the sign-in URL was successful use that response, otherwise attempt navigation with resilience.
                        IResponse? response = initialResponse is not null && initialResponse.Status == 200
                            ? (IResponse?)initialResponse
                            : await page.GotoAsync(signInUrl.ToString(), navigationOptions).ConfigureAwait(false);

                        if (response is not null)
                        {
                            var status = response.Status;
                            if (HttpHelper.IsRetryableHttpRequestException((HttpStatusCode)status))
                                throw new HttpRequestException(
                                    $"Server responded with status {(HttpStatusCode?)status} ({status}) for URL: {signInUrl}",
                                    null,
                                    (HttpStatusCode?)status);
                        }
                    }
                    catch (Exception ex)
                    {
                        await ConnectivityExceptionHelper.ThrowIfConnectivityFailureAsync(
                            ex, classifier, probe, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex) when (
                ex is PlaywrightException
                or ConnectivityException
                or HttpRequestException
                or TimeoutException
                || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                if ((ex is TimeoutException or TaskCanceledException) && options.SignInPageLoadTimeout.HasValue)
                    ex.Data["TimeoutMs"] = options.SignInPageLoadTimeout.Value.TotalMilliseconds;
                int? statusCode = ex is HttpRequestException hre && hre.StatusCode.HasValue ? (int)hre.StatusCode.Value : null;
                throw new SignInException(signInUrl, statusCode, innerException: ex);
            }

            // Only wait for the sign-in period specified in AssumeSignedInAfter if AssumeSignedInWhenNavigatedToUrl is null.
            if (options.AssumeSignedInAfter.HasValue && options.AssumeSignedInWhenNavigatedToUrl is null)
            {
                // Wait for the specified period, supporting cancellation.
                var delayTask = Task.Delay(options.AssumeSignedInAfter.Value, cancellationToken);
                try
                {
                    await delayTask.ConfigureAwait(false);
                    logger.LogInformation(
                        "Assuming sign-in completed after waiting for {SignInDelay}.",
                        HumanizeHelper.FormatDuration(options.AssumeSignedInAfter.Value));
                }
                catch (TaskCanceledException)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }
            else
            {
                // Wait until the browser navigates away from the sign-in URL, or to the specified signed-in URL.
                await WaitForSignInNavigationAsync(page, signInUrl, options, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (page is not null)
            {
                try { await page.CloseAsync().ConfigureAwait(false); } catch { }
            }
        }
    }

    private async Task WaitForSignInNavigationAsync(IPage page, Uri signInUrl, SessionOptions options, CancellationToken cancellationToken)
    {
        Uri? signedInUrl = options.AssumeSignedInWhenNavigatedToUrl;
        var waitOptions = new PageWaitForURLOptions { Timeout = 0 };
        var waitTask = page.WaitForURLAsync(
            url => signedInUrl is not null
                ? url.Equals(signedInUrl.ToString(), StringComparison.OrdinalIgnoreCase)
                : !url.Equals(signInUrl.ToString(), StringComparison.OrdinalIgnoreCase),
            waitOptions);

        var cancelTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => cancelTcs.TrySetCanceled(cancellationToken));

        var firstCompletedTask = await Task.WhenAny(waitTask, cancelTcs.Task).ConfigureAwait(false);
        if (firstCompletedTask != waitTask)
            throw new OperationCanceledException(cancellationToken);

        try
        {
            await waitTask.ConfigureAwait(false);
            if (signedInUrl is null)
                logger.LogInformation("Assuming sign-in completed as browser navigated away from sign-in URL to: {NewUrl}", page.Url);
            else
                logger.LogInformation("Assuming sign-in completed as browser navigated to provided signed-in URL.");
        }
        catch (PlaywrightException ex) when (
            (ex.Message ?? string.Empty).Contains("has been closed", StringComparison.OrdinalIgnoreCase)
            || (ex.Message ?? string.Empty).Contains("Target page, context or browser has been closed", StringComparison.OrdinalIgnoreCase))
        {
            throw new OperationCanceledException("Sign-in window was closed before sign-in completed.", ex, cancellationToken);
        }
    }

    private static bool IsPlaywrightBrowserInstallMissing(PlaywrightException ex)
    {
        var message = ex.Message ?? string.Empty;

        // Playwright typically includes an install instruction, e.g. pwsh bin/Debug/netX/playwright.ps1 install
        return message.Contains("install", StringComparison.OrdinalIgnoreCase)
            && (message.Contains("playwright.ps1", StringComparison.OrdinalIgnoreCase)
                || message.Contains("playwright.cmd", StringComparison.OrdinalIgnoreCase)
                || message.Contains("playwright install", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTargetClosed(PlaywrightException ex)
    {
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("net::ERR_ABORTED; maybe frame was detached?", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Target page, context or browser has been closed", StringComparison.OrdinalIgnoreCase);
    }
}
