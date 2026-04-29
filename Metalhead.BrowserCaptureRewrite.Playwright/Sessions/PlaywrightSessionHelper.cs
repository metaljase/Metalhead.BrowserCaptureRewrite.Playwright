using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

using Metalhead.BrowserCaptureRewrite.Abstractions.Helpers;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Sessions;

/// <summary>
/// Provides helper methods for managing the lifecycle and disposal of Playwright browser and context instances.
/// </summary>
/// <remarks>
/// <para>
/// This internal static class is used to ensure proper and robust disposal of <see cref="IBrowserContext"/> and
/// <see cref="IBrowser"/> instances, including logging and timeout handling.
/// </para>
/// <para>
/// Implements logic to handle timeouts and exceptions during browser shutdown, and logs clean-up progress if a logger is provided.
/// </para>
/// <para>
/// Cancellation is not directly supported; disposal will always attempt to complete, even if the browser takes longer than the
/// default timeout.
/// </para>
/// </remarks>
internal static class PlaywrightSessionHelper
{
    private static readonly TimeSpan s_defaultBrowserCloseTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Asynchronously disposes the specified Playwright browser context and browser, with optional logging and a configurable close timeout.
    /// </summary>
    /// <param name="context">
    /// The <see cref="IBrowserContext"/> to close.  May be <see langword="null"/>; if so, no action is taken for the context.
    /// </param>
    /// <param name="browser">
    /// The <see cref="IBrowser"/> to close.  May be <see langword="null"/>; if so, no action is taken for the browser.
    /// </param>
    /// <param name="logger">
    /// Optional.  The <see cref="ILogger"/> to use for logging clean-up progress and errors.  May be <see langword="null"/>.
    /// </param>
    /// <param name="browserCloseTimeout">
    /// Optional.  The maximum duration to wait for the browser to close before logging a warning and continuing clean-up.  If
    /// <see langword="null"/>, a default of 5 seconds is used.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous disposal operation.  The task completes when both the context and
    /// browser have been disposed, regardless of exceptions.
    /// </returns>
    /// <remarks>
    /// <para>
    /// If the browser does not close within the specified timeout, a warning is logged and the method will continue to wait for the
    /// browser to finish closing.  All exceptions during disposal are caught and suppressed to ensure clean-up always completes.
    /// </para>
    /// </remarks>
    public static async Task DisposeAsync(IBrowserContext? context, IBrowser? browser, ILogger? logger = null, TimeSpan? browserCloseTimeout = null)
    {
        if (context is not null)
        {
            try { await context.CloseAsync().ConfigureAwait(false); }
            catch { }
        }

        if (browser is not null)
        {
            var sw = Stopwatch.StartNew();
            var delayClosing = false;
            try
            {
                logger?.LogDebug("Closing web browser...");
                var closeTask = browser.CloseAsync();
                try
                {
                    await closeTask.WaitAsync(browserCloseTimeout ?? s_defaultBrowserCloseTimeout).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    delayClosing = true;
                    logger?.LogInformation("Finishing up web browser clean-up...");
                    await closeTask.ConfigureAwait(false);
                }

                logger?.LogDebug("Web browser closed.");
            }
            catch { }

            if (delayClosing)
                logger?.LogInformation("Finished web browser clean-up in {ElapsedSecs}.", HumanizeHelper.FormatDuration(sw.Elapsed));
        }
    }
}
