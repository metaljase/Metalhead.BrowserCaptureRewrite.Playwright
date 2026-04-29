using Metalhead.BrowserCaptureRewrite.Abstractions.Models;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;

/// <summary>
/// Defines a factory for creating <see cref="RewriteSpec"/> instances based on target URLs.
/// </summary>
/// <remarks>
/// <para>
/// Implementations provide methods to construct response rewrite specifications that use URL-based logic to determine when and
/// how to rewrite HTTP responses.
/// </para>
/// <para>
/// Cancellation is supported for all asynchronous operations within the created <see cref="RewriteSpec"/> delegates via
/// <see cref="CancellationToken"/>.  If cancellation is requested, in-flight work is stopped and an
/// <see cref="OperationCanceledException"/> is thrown.
/// </para>
/// </remarks>
public interface IRewriteSpecFactoryByUrls
{
    /// <summary>
    /// Creates a <see cref="RewriteSpec"/> for rewriting responses at the specified URL by adding the given albums.
    /// </summary>
    /// <param name="rewriteUrl">
    /// The absolute URL to match for rewriting.  Must not be <see langword="null"/>.
    /// </param>
    /// <param name="addAlbums">
    /// The list of album names to add to the albums response content.  Must not be <see langword="null"/> or empty.
    /// </param>
    /// <returns>
    /// A <see cref="RewriteSpec"/> configured to rewrite responses at the specified URL by adding the specified albums.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The returned specification uses URL-based logic to determine when to rewrite a response and how to modify its content.
    /// If <paramref name="addAlbums"/> is empty, no albums will be added.
    /// </para>
    /// </remarks>
    RewriteSpec CreateRewriteSpecForAddingAlbumsByUrl(Uri rewriteUrl, IReadOnlyList<string> addAlbums);
}