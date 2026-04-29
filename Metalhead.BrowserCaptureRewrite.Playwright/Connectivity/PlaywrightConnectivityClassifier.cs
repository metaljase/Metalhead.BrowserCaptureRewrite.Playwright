using Microsoft.Playwright;

using Metalhead.BrowserCaptureRewrite.Abstractions.Connectivity;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Connectivity;

/// <summary>
/// Classifies exceptions thrown by Playwright and related network operations as connectivity-related or not, and determines their scope.
/// </summary>
/// <remarks>
/// <para>
/// Implements <see cref="IConnectivityClassifier"/>.  Analyses <see cref="PlaywrightException"/>,
/// <see cref="HttpRequestException"/>, and other exceptions for known connectivity-related error messages and patterns.
/// </para>
/// <para>
/// Message fragments are used to distinguish between local environment issues, remote site failures, and hostname resolution
/// problems for Chromium, Firefox, and WebKit.
/// </para>
/// </remarks>
public sealed class PlaywrightConnectivityClassifier : IConnectivityClassifier
{
    /// <summary>
    /// Message fragments that suggest the failure is caused by the local environment.
    /// </summary>
    private static readonly string[] s_localHints =
    [
        "net::ERR_INTERNET_DISCONNECTED",   // Chromium
        "net::ERR_NETWORK_CHANGED",         // Chromium
        "net::ERR_PROXY_CONNECTION_FAILED"  // Chromium
        ];

    /// <summary>
    /// Message fragments that suggest the failure is caused by the remote site.
    /// </summary>
    private static readonly string[] s_remoteHints =
    [
        "net::ERR_CONNECTION_REFUSED",      // Chromium
        "net::ERR_ADDRESS_UNREACHABLE",     // Chromium
        "NS_ERROR_CONNECTION_REFUSED",      // Firefox
        "NS_ERROR_NET_RESET",               // Firefox
        "Could not connect to server"       // WebKit
        ];

    /// <summary>
    /// Message fragments that suggest the failure is caused by hostname resolution issues.
    /// </summary>
    private static readonly string[] s_hostnameHints =
        [
        "net::ERR_NAME_NOT_RESOLVED",       // Chromium
        "NS_ERROR_UNKNOWN_HOST",            // Firefox
        "Could not resolve hostname"        // WebKit
        ];

    /// <inheritdoc/>
    public ConnectivityClassificationResult ClassifyException(Exception ex, CancellationToken cancellationToken)
    {
        var timeoutResult = ConnectivityExceptionHelper.ClassifyTimeout(ex, cancellationToken);
        return timeoutResult.IsConnectivityRelated
            ? timeoutResult
            : ex is HttpRequestException http
                ? ConnectivityExceptionHelper.ClassifyHttpException(http)
                : ex is PlaywrightException pw
                    ? ClassifyPlaywrightException(pw)
                    : ConnectivityClassificationResult.NotConnectivityRelated;
    }

    private static ConnectivityClassificationResult ClassifyPlaywrightException(PlaywrightException ex)
    {
        var msg = ex.Message ?? string.Empty;

        if (s_localHints.Any(h => msg.Contains(h, StringComparison.OrdinalIgnoreCase)))
            return ConnectivityClassificationResult.ConnectivityRelated(ConnectivityScope.LocalEnvironment);

        if (s_remoteHints.Any(h => msg.Contains(h, StringComparison.OrdinalIgnoreCase)))
            return ConnectivityClassificationResult.ConnectivityRelated(ConnectivityScope.RemoteSite);

        if (s_hostnameHints.Any(h => msg.Contains(h, StringComparison.OrdinalIgnoreCase)))
            return ConnectivityClassificationResult.ConnectivityRelated(ConnectivityScope.HostnameResolution);

        // Default to "connectivity-related but ambiguous" so the caller can disambiguate by probing.
        return ConnectivityClassificationResult.ConnectivityRelated(ConnectivityScope.Unknown);
    }
}
