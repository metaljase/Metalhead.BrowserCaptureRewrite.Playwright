using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Enums;
using Metalhead.BrowserCaptureRewrite.Abstractions.Exceptions;
using Metalhead.BrowserCaptureRewrite.Playwright.Factories;
using Metalhead.BrowserCaptureRewrite.Playwright.Sessions;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Services;

/// <summary>
/// Provides the default implementation of <see cref="IPlaywrightSessionService"/> for creating Playwright session handles, supporting sign-in and
/// standard session flows.
/// </summary>
/// <remarks>
/// Implements <see cref="IPlaywrightSessionService"/>.
/// <para>
/// Cancellation is supported via <see cref="CancellationToken"/> for all asynchronous operations.  If cancellation is requested before or during
/// session creation, an <see cref="OperationCanceledException"/> is thrown.  If the browser or page is externally closed, an
/// <see cref="OperationCanceledException"/> is also thrown.
/// </para>
/// <para>
/// All non-cancellation exceptions are caught and wrapped in <see cref="BrowserSessionInitializationException"/>, with
/// <see cref="BrowserSessionInitializationFailureReason.EngineNotAvailable"/> used when the underlying cause is a
/// <see cref="BrowserAutomationEngineNotAvailableException"/>.
/// </para>
/// </remarks>
public sealed class DefaultPlaywrightSessionService(
    BrowserOptions browserOptions,
    IPlaywrightSignInSessionHandleFactory signInSessionHandleFactory,
    IPlaywrightSessionHandleFactory sessionHandleFactory)
    : IPlaywrightSessionService
{
    /// <inheritdoc/>
    public async Task<IPlaywrightSessionHandle> CreatePlaywrightSessionOrThrowAsync(
        Uri? signInUrl,
        Uri? signedInUrl,
        TimeSpan? signedInAfter,
        TimeSpan? signInPageLoadTimeout,
        CancellationToken cancellationToken)
    {
        // Create a sign-in session if a SignInUrl is provided, so the user can sign-in first, otherwise create a standard session.
        try
        {
            var sessionOptions = new SessionOptions(
                BrowserOptions: browserOptions,
                AssumeSignedInWhenNavigatedToUrl: signedInUrl,
                AssumeSignedInAfter: signedInAfter,
                SignInPageLoadTimeout: signInPageLoadTimeout,
                UseResilienceForSignIn: signInUrl is not null);

            return signInUrl is not null
                ? await signInSessionHandleFactory.CreateSignInSessionHandleAsync(signInUrl, sessionOptions, cancellationToken).ConfigureAwait(false)
                : await sessionHandleFactory.CreateSessionHandleAsync(sessionOptions, cancellationToken).ConfigureAwait(false);
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
