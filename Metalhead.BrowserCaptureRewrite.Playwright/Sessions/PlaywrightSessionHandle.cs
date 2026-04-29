using Microsoft.Playwright;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Sessions;

/// <summary>
/// Provides a Playwright-based implementation of <see cref="IPlaywrightSessionHandle"/>, managing the lifetime of a browser and context.
/// </summary>
/// <remarks>
/// Implements <see cref="IPlaywrightSessionHandle"/>.
/// <para>
/// Disposing the handle disposes both the browser context and browser instance.  Asynchronous disposal is supported via
/// <see cref="DisposeAsync"/>; synchronous disposal blocks until disposal completes.
/// </para>
/// <para>
/// <see cref="Browser"/> and <see cref="Context"/> are initialised at construction and remain valid until disposal.
/// </para>
/// <para>
/// If the browser is externally closed during disposal, an <see cref="OperationCanceledException"/> may be thrown.
/// </para>
/// </remarks>
internal sealed class PlaywrightSessionHandle(IBrowser browser, IBrowserContext context) : IPlaywrightSessionHandle
{
    private bool _disposed;

    /// <inheritdoc/>
    public IBrowser Browser { get; } = browser;

    /// <inheritdoc/>
    public IBrowserContext Context { get; } = context;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        await PlaywrightSessionHelper.DisposeAsync(Context, Browser).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
