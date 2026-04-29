using Microsoft.Playwright;

using Metalhead.BrowserCaptureRewrite.Abstractions.Resilience;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Factories;

/// <summary>
/// Provides a Playwright-specific implementation of <see cref="ResiliencePolicyFactoryBase"/> that configures resilience
/// policies for browser automation operations.
/// </summary>
/// <remarks>
/// Implements Playwright transport exception handling by treating all <see cref="PlaywrightException"/> instances as
/// transport-level failures for resilience policy purposes.
/// <para>
/// Inherits from <see cref="ResiliencePolicyFactoryBase"/> and utilises the supplied <see cref="IResiliencePolicyBuilder"/>
/// and optional <see cref="ResiliencePolicyOptions"/>.
/// </para>
/// <para>
/// Implements <see cref="IResiliencePolicyFactory"/>.
/// </para>
/// <para>
/// Cancellation is supported for all resilience policy operations via <see cref="CancellationToken"/>.
/// If cancellation is requested, in-flight work is stopped and an <see cref="OperationCanceledException"/> is thrown.
/// </para>
/// </remarks>
/// <param name="resiliencePolicyBuilder">
/// The builder used to construct resilience policies.  Must not be <see langword="null"/>.
/// </param>
/// <param name="options">
/// Optional.  The options used to configure policy behaviour.  If <see langword="null"/>, default options are used.
/// </param>
public class PlaywrightResiliencePolicyFactory(
    IResiliencePolicyBuilder resiliencePolicyBuilder, ResiliencePolicyOptions? options = null)
    : ResiliencePolicyFactoryBase(resiliencePolicyBuilder, options)
{
    /// <summary>
    /// Gets a predicate that determines whether an exception is considered a transport-level failure for Playwright operations.
    /// </summary>
    /// <remarks>
    /// Treats all <see cref="Microsoft.Playwright.PlaywrightException"/> instances as transport exceptions for
    /// resilience policy handling.
    /// <para>
    /// Overrides the base implementation to specialise for Playwright-specific transport errors.
    /// </para>
    /// </remarks>
    protected override Func<Exception, bool> TransportExceptionPredicate => static ex => ex is PlaywrightException;
}
