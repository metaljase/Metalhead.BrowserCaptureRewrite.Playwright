using Metalhead.BrowserCaptureRewrite.Abstractions.Models;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;

/// <summary>
/// Defines a factory for creating <see cref="CaptureSpec"/> instances based on HTTP response content.
/// </summary>
/// <remarks>
/// <para>
/// Implementations provide methods to construct resource capture specifications that use content-based logic to filter
/// and complete resource capture.
/// </para>
/// <para>
/// Cancellation is supported for all asynchronous operations within the created <see cref="CaptureSpec"/> delegates
/// via <see cref="CancellationToken"/>.  If cancellation is requested, in-flight work is stopped and an
/// <see cref="OperationCanceledException"/> is thrown.
/// </para>
/// </remarks>
public interface ICaptureSpecFactoryByContent
{
    /// <summary>
    /// Creates a <see cref="CaptureSpec"/> for capturing bands and albums based on response content, using the
    /// specified required band names.
    /// </summary>
    /// <param name="requiredBands">
    /// The list of band names that must be present in the captured content for the capture to be considered complete.
    /// Must not be <see langword="null"/> or empty.
    /// </param>
    /// <returns>
    /// A <see cref="CaptureSpec"/> configured to capture and complete based on the specified band names.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The returned specification uses content-based logic to filter and complete resource capture.  If
    /// <paramref name="requiredBands"/> is empty, the capture may never complete.
    /// </para>
    /// </remarks>
    CaptureSpec CreateSpecForBandsAndAlbumsByContent(IReadOnlyList<string> requiredBands);
}
