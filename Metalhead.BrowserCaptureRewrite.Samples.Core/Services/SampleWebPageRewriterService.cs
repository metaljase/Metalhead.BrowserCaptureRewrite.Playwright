using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Transport;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Services;

/// <summary>
/// Provides logic for rewriting sample web page HTTP responses to demonstrate content modification scenarios.
/// </summary>
/// <remarks>
/// <para>
/// This service is intended for use in sample scenarios where HTTP responses containing HTML web pages are programmatically
/// modified before further processing.
/// </para>
/// <para>
/// Implements asynchronous response rewriting.  Cancellation is supported if the underlying
/// <see cref="IResponseInfo.GetBodyAsStringAsync"/> supports <see cref="CancellationToken"/>.
/// </para>
/// </remarks>
public sealed class SampleWebPageRewriterService()
{
    /// <summary>
    /// Attempts to rewrite the response body of the sample web page by removing the first <c>&lt;script&gt;</c> block and
    /// inserting a note, preventing bands from being fetched.
    /// </summary>
    /// <param name="req">
    /// The <see cref="IRequestInfo"/> representing the HTTP request.  Must not be <see langword="null"/>.  The URL must be a
    /// valid absolute URI.
    /// </param>
    /// <param name="resp">
    /// The <see cref="IResponseInfo"/> representing the HTTP response.  Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.  The result is a
    /// <see cref="ResponseRewriteResult"/> indicating whether rewriting occurred, the new response body (if rewritten), and
    /// the content type (or <see langword="null"/> if unchanged).
    /// </returns>
    /// <remarks>
    /// <para>
    /// If the request URL is not a valid absolute URI, or the response body cannot be parsed or rewritten, the method returns
    /// <see cref="ResponseRewriteResult.NotRewritten"/>.
    /// </para>
    /// <para>
    /// If rewriting occurs, the returned result contains the updated HTML with the script block removed and a note inserted.
    /// </para>
    /// </remarks>
    public static async Task<ResponseRewriteResult> RewriteAsync(IRequestInfo req, IResponseInfo resp)
    {
        if (!Uri.TryCreate(req.Url, UriKind.Absolute, out _))
            return ResponseRewriteResult.NotRewritten;

        var body = await resp.GetBodyAsStringAsync().ConfigureAwait(false);

        var rewritten = BuildRewrittenWebPage(body);
        return rewritten is null
            ? ResponseRewriteResult.NotRewritten
            : new ResponseRewriteResult(true, rewritten, null);
    }

    /// <summary>
    /// Builds a new HTML string by removing the first <c>&lt;script&gt;</c> block and inserting a note after the highlight
    /// container placeholder.
    /// </summary>
    /// <param name="body">
    /// The original HTML body.  Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The rewritten HTML string, or <see langword="null"/> if the script block or placeholder cannot be found.
    /// </returns>
    private static string? BuildRewrittenWebPage(string body)
    {
        var scriptStartIndex = body.IndexOf("<script>", StringComparison.OrdinalIgnoreCase);
        if (scriptStartIndex == -1)
            return null;
        var scriptEndIndex = body.IndexOf("</script>", scriptStartIndex, StringComparison.OrdinalIgnoreCase);
        if (scriptEndIndex == -1)
            return null;
        body = body.Remove(scriptStartIndex, scriptEndIndex - scriptStartIndex + "</script>".Length);

        var note = """
            <p class="highlight-container">
                NOTE: Normally, bands would also be fetched and displayed below.  However, the Samples app
                intercepted the HTTP response for this page and rewrote the HTML to prevent the bands from
                being fetched.  This ability to rewrite web pages can be useful for certain scenarios.
            </p>
            """;
        var placeholder = "<div id=\"highlight-container-placeholder\">";
        var placeholderIndex = body.IndexOf(placeholder, StringComparison.OrdinalIgnoreCase);
        if (placeholderIndex == -1)
            return null;

        if (placeholderIndex >= 0)
            body = body.Insert(placeholderIndex + placeholder.Length, note);
        return body;
    }
}
