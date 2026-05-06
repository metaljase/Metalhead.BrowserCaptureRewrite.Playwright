using Metalhead.BrowserCaptureRewrite.Abstractions.Connectivity;
using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Resilience;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides delegate-based configuration actions for all Playwright browser capture options types, used when registering
/// services via
/// <see cref="ServiceCollectionExtensions.AddPlaywrightCaptureRewrite(IServiceCollection, Action{PlaywrightCaptureRewriteBuilder})"/>.
/// </summary>
/// <remarks>
/// Each property holds an <see cref="Action{T}"/> delegate that is applied to the corresponding options type during service
/// registration.  Leaving a property at its default value results in no changes to that options type's defaults.
/// </remarks>
public sealed class PlaywrightCaptureRewriteBuilder
{
    /// <summary>Gets or sets the delegate used to configure <see cref="SignInOptions"/>.</summary>
    public Action<SignInOptions> ConfigureSignIn { get; set; } = _ => { };

    /// <summary>Gets or sets the delegate used to configure <see cref="NavigationTimingOptions"/>.</summary>
    public Action<NavigationTimingOptions> ConfigureNavigationTiming { get; set; } = _ => { };

    /// <summary>Gets or sets the delegate used to configure <see cref="CaptureTimingOptions"/>.</summary>
    public Action<CaptureTimingOptions> ConfigureCaptureTiming { get; set; } = _ => { };

    /// <summary>Gets or sets the delegate used to configure <see cref="BrowserOptions"/>.</summary>
    public Action<BrowserOptions> ConfigureBrowser { get; set; } = _ => { };

    /// <summary>Gets or sets the delegate used to configure <see cref="ResiliencePolicyOptions"/>.</summary>
    public Action<ResiliencePolicyOptions> ConfigureResiliencePolicy { get; set; } = _ => { };

    /// <summary>Gets or sets the delegate used to configure <see cref="ConnectivityProbeOptions"/>.</summary>
    public Action<ConnectivityProbeOptions> ConfigureConnectivityProbe { get; set; } = _ => { };
}
