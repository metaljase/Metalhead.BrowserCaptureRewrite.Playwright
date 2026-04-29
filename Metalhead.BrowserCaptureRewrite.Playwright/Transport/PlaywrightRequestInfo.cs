using Microsoft.Playwright;

using Metalhead.BrowserCaptureRewrite.Abstractions.Transport;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Transport;

/// <summary>
/// Provides a Playwright-based implementation of <see cref="IRequestInfo"/> for exposing HTTP request details.
/// </summary>
/// <remarks>
/// Implements <see cref="IRequestInfo"/>.
/// <para>
/// All properties are populated from the underlying Playwright <see cref="IRequest"/> instance.  Properties are non-null
/// and reflect the state of the request at the time of construction.
/// </para>
/// </remarks>
internal sealed class PlaywrightRequestInfo(IRequest request) : IRequestInfo
{
    /// <inheritdoc/>
    public string Url => request.Url;

    /// <inheritdoc/>
    public string Method => request.Method;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Headers => request.Headers;
}