using Metalhead.BrowserCaptureRewrite.Abstractions.Models;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;

/// <summary>
/// Defines a factory for creating <see cref="CaptureSpec"/> instances based on target URLs.
/// </summary>
/// <remarks>
/// <para>
/// Implementations provide methods to construct resource capture specifications that use URL-based logic to filter and
/// complete resource capture.
/// </para>
/// <para>
/// Cancellation is supported for all asynchronous operations within the created <see cref="CaptureSpec"/> delegates
/// via <see cref="CancellationToken"/>.  If cancellation is requested, in-flight work is stopped and an
/// <see cref="OperationCanceledException"/> is thrown.
/// </para>
/// </remarks>
public interface ICaptureSpecFactoryByUrls
{
    /// <summary>
    /// Creates a <see cref="CaptureSpec"/> for capturing resources at the specified URLs.
    /// </summary>
    /// <param name="urlsToCapture">
    /// The list of absolute URLs to capture.  Must not be <see langword="null"/> or empty.
    /// </param>
    /// <returns>
    /// A <see cref="CaptureSpec"/> configured to capture and complete based on the specified URLs.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The returned specification uses URL-based logic to filter and complete resource capture.  If
    /// <paramref name="urlsToCapture"/> is empty, the capture may never complete.
    /// </para>
    /// </remarks>
    CaptureSpec CreateSpecForBandsAndAlbumsByUrl(IReadOnlyList<Uri> urlsToCapture);
}