using Microsoft.Playwright;

using Metalhead.BrowserCaptureRewrite.Abstractions.Transport;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Transport;

/// <summary>
/// Provides a Playwright-based implementation of <see cref="IResponseInfo"/> for exposing HTTP response details.
/// </summary>
/// <remarks>
/// Implements <see cref="IResponseInfo"/>.
/// <para>
/// All properties and methods are populated from the underlying Playwright <see cref="IResponse"/> instance.
/// Asynchronous methods support cancellation via the provided <see cref="CancellationToken"/> if specified at construction, and
/// throw <see cref="OperationCanceledException"/> if cancellation is requested.
/// </para>
/// </remarks>
internal sealed class PlaywrightResponseInfo(IResponse response, CancellationToken token) : IResponseInfo
{
    /// <summary>Initialises a new instance using <see cref="CancellationToken.None"/>.</summary>
    public PlaywrightResponseInfo(IResponse response) : this(response, CancellationToken.None) { }

    /// <inheritdoc/>
    public int? StatusCode => response.Status;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Headers => response.Headers;

    /// <inheritdoc/>
    public Task<string> GetBodyAsStringAsync() =>
        response.TextAsync().WaitAsync(token);

    /// <inheritdoc/>
    public async Task<byte[]> GetBodyAsByteArrayAsync()
    {
        var buf = await response.BodyAsync().WaitAsync(token).ConfigureAwait(false);
        return [.. buf];
    }
}
