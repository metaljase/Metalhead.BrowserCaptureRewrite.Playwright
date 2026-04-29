using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Transport;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Helpers;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Services;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Factories;

/// <summary>
/// Provides a factory for creating <see cref="RewriteSpec"/> instances that rewrite albums responses by adding
/// specified albums based on HTTP response content.
/// </summary>
/// <remarks>
/// Implements <see cref="IRewriteSpecFactoryByContent"/>.
/// <para>
/// The created <see cref="RewriteSpec"/> uses content-based logic to determine when to rewrite a response and how to
/// modify its content, including deserialisation and modification of JSON responses.
/// </para>
/// <para>
/// Cancellation is supported for all asynchronous operations within the created <see cref="RewriteSpec"/> delegates via
/// <see cref="CancellationToken"/>.  If cancellation is requested, in-flight work is stopped and an
/// <see cref="OperationCanceledException"/> is thrown.
/// </para>
/// </remarks>
public sealed class RewriteSpecFactoryByContent : IRewriteSpecFactoryByContent
{
    /// <inheritdoc/>
    public RewriteSpec CreateRewriteSpecForAddingAlbumsByContent(IReadOnlyList<string> addAlbums) =>
        new(req => SamplesHelper.IsLikelyJson(req), CreateRewriteHandlerForAddingAlbums(addAlbums));

    // Creates the rewrite delegate for the albums response.  The shouldRewrite predicate ensures only likely JSON responses are passed to
    // this delegate, and RewriteAsync will rewrite the response if it's the albums JSON, otherwise NotRewritten is returned.
    private static Func<IRequestInfo, IResponseInfo, Task<ResponseRewriteResult>> CreateRewriteHandlerForAddingAlbums(
        IReadOnlyList<string> addAlbums) =>
        async (req, resp) =>
            await AlbumsRewriterService.RewriteAsync(req, resp, addAlbums).ConfigureAwait(false);
}
