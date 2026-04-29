using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Enums;
using Metalhead.BrowserCaptureRewrite.Abstractions.Exceptions;
using Metalhead.BrowserCaptureRewrite.Abstractions.Factories;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Services;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Services;

/// <summary>
/// Provides the default implementation of <see cref="IBrowserSessionService"/> for creating browser sessions, supporting sign-in
/// and standard session flows.
/// </summary>
/// <remarks>
/// Implements <see cref="IBrowserSessionService"/>.
/// <para>
/// Cancellation is supported via <see cref="CancellationToken"/> for all asynchronous operations.  If cancellation is requested
/// before or during session creation, an <see cref="OperationCanceledException"/> is thrown.  If the browser or page is externally
/// closed, an <see cref="OperationCanceledException"/> is also thrown.
/// </para>
/// <para>
/// <see cref="SignInException"/> and <see cref="OperationCanceledException"/> are rethrown directly.  All other exceptions are
/// wrapped in <see cref="BrowserSessionInitializationException"/>; when the browser engine is unavailable, the exception is thrown
/// with <see cref="BrowserSessionInitializationFailureReason.EngineNotAvailable"/>.
/// </para>
/// </remarks>
public sealed class DefaultBrowserSessionService(
    BrowserOptions browserOptions,
    ISignInBrowserSessionFactory signInSessionFactory,
    IBrowserSessionFactory sessionFactory)
    : IBrowserSessionService
{
    /// <inheritdoc/>
    public Task<IBrowserSession> CreateBrowserSessionOrThrowAsync(CancellationToken cancellationToken)
        => CreateBrowserSessionOrThrowAsync(null, null, new SignInOptions(), cancellationToken);

    /// <inheritdoc/>
    public async Task<IBrowserSession> CreateBrowserSessionOrThrowAsync(
        Uri? signInUrl,
        Uri? signedInUrl,
        SignInOptions signInOptions,
        CancellationToken cancellationToken)
    {
        // Create a sign-in session if a SignInUrl is provided, so the user can sign-in first, otherwise create a standard session.
        try
        {
            var sessionOptions = new SessionOptions(
                BrowserOptions: browserOptions,
                AssumeSignedInWhenNavigatedToUrl: signedInUrl,
                AssumeSignedInAfter: signInOptions.AssumeSignedInAfter(),
                SignInPageLoadTimeout: signInOptions.PageLoadTimeout(),
                UseResilienceForSignIn: signInUrl is not null);

            return signInUrl is not null
                ? await signInSessionFactory.CreateSignInSessionAsync(signInUrl, sessionOptions, cancellationToken).ConfigureAwait(false)
                : await sessionFactory.CreateSessionAsync(sessionOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SignInException or OperationCanceledException)
        {
            throw;
        }
        catch (BrowserAutomationEngineNotAvailableException ex)
        {
            throw new BrowserSessionInitializationException(
                BrowserSessionInitializationFailureReason.EngineNotAvailable, signInUrl is not null, ex.ResolutionHint, innerException: ex);
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is DllNotFoundException || ex is FileNotFoundException)
        {
            throw new BrowserSessionInitializationException(
                BrowserSessionInitializationFailureReason.General, signInUrl is not null, innerException: ex);
        }
        catch (Exception ex)
        {
            throw new BrowserSessionInitializationException(
                BrowserSessionInitializationFailureReason.General, signInUrl is not null, innerException: ex);
        }
    }
}
