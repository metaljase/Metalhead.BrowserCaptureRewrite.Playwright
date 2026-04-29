using Metalhead.BrowserCaptureRewrite.Abstractions.Models;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;

/// <summary>
/// Defines a factory for creating <see cref="RewriteSpec"/> instances based on HTTP response content.
/// </summary>
/// <remarks>
/// <para>
/// Implementations provide methods to construct response rewrite specifications that use content-based logic to determine when
/// and how to rewrite HTTP responses.
/// </para>
/// <para>
/// Cancellation is supported for all asynchronous operations within the created <see cref="RewriteSpec"/> delegates via
/// <see cref="CancellationToken"/>.  If cancellation is requested, in-flight work is stopped and an
/// <see cref="OperationCanceledException"/> is thrown.
/// </para>
/// </remarks>
public interface IRewriteSpecFactoryByContent
{
    /// <summary>
    /// Creates a <see cref="RewriteSpec"/> for rewriting albums responses by adding the specified albums to the content.
    /// </summary>
    /// <param name="addAlbums">
    /// The list of album names to add to the albums response content.  Must not be <see langword="null"/> or empty.
    /// </param>
    /// <returns>
    /// A <see cref="RewriteSpec"/> configured to rewrite albums responses by adding the specified albums.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The returned specification uses content-based logic to determine when to rewrite a response and how to modify its content.
    /// If <paramref name="addAlbums"/> is empty, no albums will be added.
    /// </para>
    /// </remarks>
    RewriteSpec CreateRewriteSpecForAddingAlbumsByContent(IReadOnlyList<string> addAlbums);
}