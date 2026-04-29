using Microsoft.Playwright;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Sessions;

/// <summary>
/// Represents a handle to a Playwright browser session, providing access to the underlying <see cref="IBrowser"/> and <see cref="IBrowserContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// Extends <see cref="IAsyncDisposable"/> and <see cref="IDisposable"/>.  Disposing the handle disposes both the browser context
/// and browser instance.
/// </para>
/// <para>
/// <see cref="Browser"/> and <see cref="Context"/> are guaranteed to be non-<see langword="null"/> for the lifetime of the handle.
/// </para>
/// <para>
/// If the browser is externally closed during disposal, an <see cref="OperationCanceledException"/> may be thrown.
/// </para>
/// </remarks>
public interface IPlaywrightSessionHandle : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the underlying Playwright browser instance for this session.
    /// </summary>
    /// <value>
    /// A non-<see langword="null"/> <see cref="IBrowser"/> instance.
    /// </value>
    IBrowser Browser { get; }

    /// <summary>
    /// Gets the Playwright browser context for this session.
    /// </summary>
    /// <value>
    /// A non-<see langword="null"/> <see cref="IBrowserContext"/> instance.
    /// </value>
    IBrowserContext Context { get; }
}
