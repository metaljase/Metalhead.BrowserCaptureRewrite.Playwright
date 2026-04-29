using System.Text.Json;

using Metalhead.BrowserCaptureRewrite.Abstractions.Transport;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Helpers;

/// <summary>
/// Provides helper methods for content type detection and safe deserialization in sample scenarios.
/// </summary>
/// <remarks>
/// <para>
/// This sealed class is intended for use in sample and demonstration code to assist with content-based resource capture and
/// model deserialization.
/// </para>
/// <para>
/// All methods are <see langword="static"/> and thread-safe.  Null handling and exception safety are explicitly managed.
/// </para>
/// </remarks>
public sealed class SamplesHelper
{
    /// <summary>
    /// Determines whether the specified request is likely to be a JSON resource based on its content type or URL.
    /// </summary>
    /// <param name="req">
    /// The <see cref="IRequestInfo"/> representing the HTTP request.  Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the request's content type header or URL indicates JSON content; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Returns <see langword="true"/> if the <c>content-type</c> header contains "application/json", "text/json", or "application/vnd.api+json"
    /// (case-insensitive), or if the URL ends with ".json".
    /// </para>
    /// </remarks>
    public static bool IsLikelyJson(IRequestInfo req)
    {
        if (req.Headers.TryGetValue("content-type", out var contentTypeHeader))
        {
            if (contentTypeHeader.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
                contentTypeHeader.Contains("text/json", StringComparison.OrdinalIgnoreCase) ||
                contentTypeHeader.Contains("application/vnd.api+json", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return Uri.TryCreate(req.Url, UriKind.Absolute, out var parsed)
            && parsed.AbsolutePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Attempts to deserialize the specified JSON string into a model of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of model to deserialize to.
    /// </typeparam>
    /// <param name="json">
    /// The JSON string to deserialize.  Must not be <see langword="null"/>.
    /// </param>
    /// <param name="model">
    /// When this method returns, contains the deserialized model if successful; otherwise, the default value for <typeparamref name="T"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if deserialization was successful and the model is not <see langword="null"/>; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Any exceptions during deserialization are caught and suppressed.  If deserialization fails, <paramref name="model"/> is set to
    /// <see langword="default"/>.
    /// </para>
    /// </remarks>
    public static bool TryDeserializeModel<T>(string json, out T? model)
    {
        try
        {
            model = JsonSerializer.Deserialize<T>(json);
            return model != null;
        }
        catch
        {
            model = default;
            return false;
        }
    }
}
