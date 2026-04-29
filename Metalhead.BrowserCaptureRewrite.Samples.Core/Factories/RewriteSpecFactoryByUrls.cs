using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Transport;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Services;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Factories;

/// <summary>
/// Provides a factory for creating <see cref="RewriteSpec"/> instances that rewrite responses at specified URLs by adding
/// albums or modifying web page content.
/// </summary>
/// <remarks>
/// Implements <see cref="IRewriteSpecFactoryByUrls"/>.
/// <para>
/// The created <see cref="RewriteSpec"/> uses URL-based logic to determine when to rewrite a response and how to
/// modify its content, including deserialisation and modification of JSON responses or web page content.
/// </para>
/// <para>
/// Cancellation is supported for all asynchronous operations within the created <see cref="RewriteSpec"/> delegates via
/// <see cref="CancellationToken"/>.  If cancellation is requested, in-flight work is stopped and an
/// <see cref="OperationCanceledException"/> is thrown.
/// </para>
/// </remarks>
public sealed class RewriteSpecFactoryByUrls : IRewriteSpecFactoryByUrls
{
    /// <inheritdoc/>
    public RewriteSpec CreateRewriteSpecForAddingAlbumsByUrl(Uri rewriteUrl, IReadOnlyList<string> addAlbums) =>
        new(CreateShouldRewriteByUrl(rewriteUrl), CreateRewriteHandlerForAddingAlbums(addAlbums));

    /// <summary>
    /// Creates a <see cref="RewriteSpec"/> for rewriting the web page response at the specified URL.
    /// </summary>
    /// <param name="rewriteUrl">
    /// The absolute URL to match for rewriting.  Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A <see cref="RewriteSpec"/> configured to rewrite the web page response at the specified URL.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The returned specification uses URL-based logic to determine when to rewrite a response and how to modify its content.
    /// </para>
    /// </remarks>
    public static RewriteSpec CreateRewriteSpecForWebPage(Uri rewriteUrl) =>
        new(CreateShouldRewriteByUrl(rewriteUrl), CreateRewriteHandlerForWebPage());

    /// <summary>
    /// Creates a predicate that determines whether a request should be considered for rewriting based on an exact URL match.
    /// </summary>
    /// <param name="rewriteUrl">
    /// The absolute URL to match for rewriting.  Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A function that returns <see langword="true"/> if the request URL is a valid absolute URI and equals
    /// <paramref name="rewriteUrl"/>; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The returned predicate parses the request URL and compares it to <paramref name="rewriteUrl"/> using
    /// <see cref="Uri.Equals(Uri)"/>.  If the request URL is not a valid absolute URI, the predicate returns <see langword="false"/>.
    /// </para>
    /// </remarks>
    public static Func<IRequestInfo, bool> CreateShouldRewriteByUrl(Uri rewriteUrl) =>
        req => Uri.TryCreate(req.Url, UriKind.Absolute, out var requestUri) && requestUri.Equals(rewriteUrl);

    // Creates the rewrite delegate for the albums response.  URL matching is handled by the shouldRewrite predicate.
    // This delegate attempts to add the supplied albums, otherwise returns NotRewritten.
    private static Func<IRequestInfo, IResponseInfo, Task<ResponseRewriteResult>> CreateRewriteHandlerForAddingAlbums(
        IReadOnlyList<string> addAlbums) =>
        async (req, resp) =>
            await AlbumsRewriterService.RewriteAsync(req, resp, addAlbums).ConfigureAwait(false);

    private static Func<IRequestInfo, IResponseInfo, Task<ResponseRewriteResult>> CreateRewriteHandlerForWebPage() =>
        SampleWebPageRewriterService.RewriteAsync;
}
