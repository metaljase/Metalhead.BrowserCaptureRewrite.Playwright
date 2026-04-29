using Metalhead.BrowserCaptureRewrite.Abstractions.Transport;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Transport;

/// <summary>
/// Provides a minimal, in-memory implementation of <see cref="IResponseInfo"/> for representing HTTP response details.
/// </summary>
/// <remarks>
/// Implements <see cref="IResponseInfo"/>.
/// <para>
/// Designed for scenarios where only the response body, status code, and headers are required, without dependency on a browser automation engine.
/// </para>
/// <para>
/// All properties and methods are initialised from constructor arguments.  <see cref="StatusCode"/> may be <see langword="null"/>
/// if no status was provided.  The response body is always returned as UTF-8 encoded bytes or as the original string.
/// </para>
/// </remarks>
internal sealed class MinimalResponseInfo(string body, int? status, IReadOnlyDictionary<string, string> headers) : IResponseInfo
{
    /// <inheritdoc/>
    public int? StatusCode { get; } = status;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Headers { get; } = headers;

    /// <inheritdoc/>
    public Task<string> GetBodyAsStringAsync() => Task.FromResult(body);

    /// <inheritdoc/>
    public Task<byte[]> GetBodyAsByteArrayAsync() => Task.FromResult(System.Text.Encoding.UTF8.GetBytes(body));
}
