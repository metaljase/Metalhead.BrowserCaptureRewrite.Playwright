using System.Text.Json;

using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Transport;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Helpers;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Models;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Services;

/// <summary>
/// Provides logic for rewriting albums HTTP responses by adding additional album titles to the response content.
/// </summary>
/// <remarks>
/// <para>
/// This service is intended for use in sample scenarios where HTTP responses containing albums data (as JSON) need to be programmatically modified
/// before further processing.
/// </para>
/// <para>
/// Implements asynchronous response rewriting.  Cancellation is supported if the underlying <see cref="IResponseInfo.GetBodyAsStringAsync"/>
/// supports <see cref="CancellationToken"/>.
/// </para>
/// </remarks>
public sealed class AlbumsRewriterService()
{
    /// <summary>
    /// Attempts to rewrite the response body by adding the specified albums to the albums JSON response.
    /// </summary>
    /// <param name="req">
    /// The <see cref="IRequestInfo"/> representing the HTTP request.  Must not be <see langword="null"/>.  The URL must be a valid absolute URI.
    /// </param>
    /// <param name="resp">
    /// The <see cref="IResponseInfo"/> representing the HTTP response.  Must not be <see langword="null"/>.
    /// </param>
    /// <param name="addAlbums">
    /// The list of album titles to add to the albums response.  Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.  The result is a <see cref="ResponseRewriteResult"/> indicating
    /// whether rewriting occurred, the new response body (if rewritten), and the content type.
    /// </returns>
    /// <remarks>
    /// <para>
    /// If the request URL is not a valid absolute URI, the response body is not valid JSON, or the albums model cannot be deserialised or is empty,
    /// the method returns <see cref="ResponseRewriteResult.NotRewritten"/>.
    /// </para>
    /// <para>
    /// If rewriting occurs, the returned result contains the updated albums JSON and content type "application/json".
    /// </para>
    /// </remarks>
    public static async Task<ResponseRewriteResult> RewriteAsync(IRequestInfo req, IResponseInfo resp, IReadOnlyList<string> addAlbums)
    {
        if (!Uri.TryCreate(req.Url, UriKind.Absolute, out _))
            return ResponseRewriteResult.NotRewritten;

        var body = await resp.GetBodyAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body) || !SamplesHelper.TryDeserializeModel<Albums>(body, out var albums) || albums is null || albums.AlbumTitles.Count == 0)
            return ResponseRewriteResult.NotRewritten;

        var rewritten = BuildRewrittenAlbumsJson(albums, addAlbums);
        return new ResponseRewriteResult(true, rewritten, "application/json");
    }

    /// <summary>
    /// Builds a new albums JSON string by adding the specified albums to the existing albums model.
    /// </summary>
    /// <param name="albums">
    /// The existing <see cref="Albums"/> model.  Must not be <see langword="null"/>.
    /// </param>
    /// <param name="addAlbums">
    /// The list of album titles to add.  Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A JSON string representing the updated albums model.
    /// </returns>
    private static string BuildRewrittenAlbumsJson(Albums albums, IReadOnlyList<string> addAlbums)
    {
        albums.AlbumTitles.InsertRange(0, addAlbums);
        return JsonSerializer.Serialize(albums);
    }
}
