using System.Net;
using Microsoft.Extensions.Logging;

using Metalhead.BrowserCaptureRewrite.Abstractions.Connectivity;
using Metalhead.BrowserCaptureRewrite.Abstractions.Exceptions;
using Metalhead.BrowserCaptureRewrite.Abstractions.Helpers;

namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Services;

/// <summary>
/// Provides a base class for orchestrators that coordinate browser automation, connectivity classification, and error handling in sample scenarios.
/// </summary>
/// <remarks>
/// <para>
/// Implements core logic for connectivity error detection, logging, and classification using <see cref="IConnectivityClassifier"/> and
/// <see cref="IConnectivityProbe"/>.
/// </para>
/// <para>
/// Derived types should use <see cref="IsLocalConnectivityErrorAsync"/> to determine whether an exception is due to local connectivity issues, and
/// to log errors with appropriate context.
/// </para>
/// <para>
/// Cancellation is supported for all asynchronous operations via <see cref="CancellationToken"/>.  If cancellation is requested, in-flight work is
/// stopped and an <see cref="OperationCanceledException"/> is thrown.
/// </para>
/// </remarks>
public abstract class BaseOrchestrator(ILogger logger, IConnectivityClassifier classifier, IConnectivityProbe probe)
{
    protected readonly ILogger _logger = logger;

    /// <summary>
    /// Determines whether the specified exception is due to a local connectivity error and logs the error with context.
    /// </summary>
    /// <param name="ex">
    /// The exception to evaluate.  Must not be <see langword="null"/>.
    /// </param>
    /// <param name="action">
    /// A description of the action being performed when the error occurred.  Used for logging context.
    /// </param>
    /// <param name="urlForLog">
    /// The URL associated with the action, or <see langword="null"/> if not applicable.  Used for logging context.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.  The result is <see langword="true"/> if the error is due to local
    /// connectivity; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method inspects the exception and its inner exceptions, classifies connectivity errors, and logs detailed error messages.  It handles
    /// <see cref="SignInException"/>, <see cref="ConnectivityException"/>, <see cref="HttpRequestException"/>, <see cref="TimeoutException"/>, and
    /// <see cref="TaskCanceledException"/>.  For HTTP errors, it logs the status code and reason.
    /// </para>
    /// <para>
    /// If the exception is not recognised as a connectivity error, the method returns <see langword="false"/>.
    /// </para>
    /// <para>
    /// Throws <see cref="InvalidOperationException"/> if an unexpected <see cref="ConnectivityScope"/> is encountered in a
    /// <see cref="ConnectivityException"/>.
    /// </para>
    /// </remarks>
    protected async Task<bool> IsLocalConnectivityErrorAsync(
        Exception ex, string action, string? urlForLog, CancellationToken cancellationToken)
    {
        if (ex is SignInException signInEx)
        {
            if (string.IsNullOrWhiteSpace(urlForLog))
                urlForLog = signInEx.SignInUrl.ToString();

            if (signInEx.InnerException is not null)
                ex = signInEx.InnerException;
        }

        ConnectivityScope scope;
        if (ex is ConnectivityException connectivityEx)
        {
            scope = connectivityEx.Scope;

            if (scope is ConnectivityScope.LocalEnvironment)
            {
                _logger.LogError(ex, "Local internet connectivity issue while {Action}.  URL: {Url}", action, urlForLog);
                return true;
            }
            if (scope is ConnectivityScope.HostnameResolution)
            {
                _logger.LogError(ex, "Hostname resolution error (check URL) while {Action}.  URL: {Url}", action, urlForLog);
                return false;
            }

            throw new InvalidOperationException($"Unexpected ConnectivityScope '{scope}' in ConnectivityException.");
        }

        scope = ex is HttpRequestException httpException && httpException.StatusCode == HttpStatusCode.NotFound
            ? ConnectivityScope.RemoteSite
            : ex.Data["ConnectivityScope"] is ConnectivityScope cs
                ? cs
                : await ConnectivityExceptionHelper.AnnotateExceptionWithConnectivityScopeAsync(ex, classifier, probe, cancellationToken)
                .ConfigureAwait(false);

        if (ex is HttpRequestException httpEx)
        {
            var statusForLog = httpEx.StatusCode.HasValue
                ? $"{(int)httpEx.StatusCode} {HumanizeHelper.HumanizeEnum(httpEx.StatusCode.Value)}"
                : "Unknown";
            var reason = scope switch
            {
                ConnectivityScope.LocalEnvironment => "due to local internet connectivity issue",
                ConnectivityScope.RemoteSite => "from remote site",
                ConnectivityScope.HostnameResolution => "due to hostname resolution issue",
                _ => ""
            };

            if (scope is ConnectivityScope.LocalEnvironment or ConnectivityScope.RemoteSite or ConnectivityScope.HostnameResolution)
            {
                _logger.LogError(ex, "HTTP {StatusCode} error {Reason} while {Action}. URL: {Url}", statusForLog, reason, action, urlForLog);
                return scope is ConnectivityScope.LocalEnvironment;
            }

            _logger.LogError(ex, "HTTP {StatusCode} error while {Action}. URL: {Url}", statusForLog, action, urlForLog);
            return false;
        }

        var isTimeoutLike = ex is TimeoutException || ex is TaskCanceledException;
        if (isTimeoutLike)
        {
            var hasTimeoutMs = ex.Data.Contains("TimeoutMs");
            if (scope is ConnectivityScope.LocalEnvironment)
            {
                if (hasTimeoutMs)
                    _logger.LogError(ex,
                        "Timeout ({TimeoutMs}) due to local internet connectivity issue while {Action}.  URL: {Url}",
                        HumanizeHelper.FormatDuration(TimeSpan.FromMilliseconds((double)(ex.Data["TimeoutMs"] ?? 0))),
                        action,
                        urlForLog);
                else
                    _logger.LogError(
                        ex, "Timeout due to local internet connectivity issue while {Action}.  URL: {Url}", action, urlForLog);
                return true;
            }

            if (hasTimeoutMs)
                _logger.LogError(ex,
                    "Timeout ({TimeoutMs}) while {Action}.  URL: {Url}",
                    HumanizeHelper.FormatDuration(TimeSpan.FromMilliseconds((double)(ex.Data["TimeoutMs"] ?? 0))),
                    action,
                    urlForLog);
            else
                _logger.LogError(ex, "Timeout while {Action}.  URL: {Url}", action, urlForLog);
            return false;
        }
        return false;
    }
}
