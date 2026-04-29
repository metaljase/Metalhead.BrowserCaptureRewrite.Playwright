using Microsoft.Extensions.Options;
using Microsoft.Playwright;

using Metalhead.BrowserCaptureRewrite.Abstractions.Connectivity;
using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Factories;
using Metalhead.BrowserCaptureRewrite.Abstractions.Resilience;
using Metalhead.BrowserCaptureRewrite.Abstractions.Services;
using Metalhead.BrowserCaptureRewrite.Playwright.Connectivity;
using Metalhead.BrowserCaptureRewrite.Playwright.Factories;
using Metalhead.BrowserCaptureRewrite.Playwright.Services;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods for registering Playwright-based browser capture and session services with a dependency injection container.
/// </summary>
/// <remarks>
/// <para>
/// Registers implementations for browser session, capture, DOM, resilience, and connectivity abstractions, as well as
/// Playwright-specific factories and services.
/// </para>
/// <para>
/// All registered services are added as singletons.  The Playwright instance is created synchronously at registration time.
/// </para>
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Playwright-based browser capture and session services with the dependency injection container, using default
    /// <see cref="ResiliencePolicyOptions"/>.
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to add services to.  Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The same <see cref="IServiceCollection"/> instance for chaining.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This overload uses default <see cref="ResiliencePolicyOptions"/>.  To customise resilience policy behaviour, use the
    /// overload accepting a configuration delegate.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddPlaywrightCaptureRewrite(
    this IServiceCollection services)
    {
        return services.AddPlaywrightCaptureRewrite(_ => { });
    }

    /// <summary>
    /// Registers all Playwright-based browser capture and session services with the dependency injection container, allowing
    /// customisation of <see cref="ResiliencePolicyOptions"/>.
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to add services to.  Must not be <see langword="null"/>.
    /// </param>
    /// <param name="configure">
    /// A delegate to configure <see cref="ResiliencePolicyOptions"/>.  Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The same <see cref="IServiceCollection"/> instance for chaining.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Registers all required Playwright, browser session, capture, DOM, resilience, and connectivity services as singletons.
    /// The Playwright instance is created synchronously at registration time.
    /// </para>
    /// <para>
    /// The <paramref name="configure"/> delegate is used to customise <see cref="ResiliencePolicyOptions"/> before registration.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddPlaywrightCaptureRewrite(
        this IServiceCollection services, Action<ResiliencePolicyOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ResiliencePolicyOptions>>().Value);
        services.AddSingleton<IResiliencePolicyFactory, PlaywrightResiliencePolicyFactory>();
        services.AddSingleton<IResiliencePolicyBuilder, ResiliencePolicyBuilder>();
        services.AddSingleton<IBrowserSessionResilienceWrapper, DefaultBrowserSessionResilienceWrapper>();
        services.AddSingleton<IPlaywrightPageCaptureService, PlaywrightPageCaptureService>();
        services.AddSingleton<IPlaywrightSessionHandleFactory, PlaywrightBrowserSessionFactory>();
        services.AddSingleton<IPlaywrightSignInSessionHandleFactory, PlaywrightBrowserSessionFactory>();
        services.AddSingleton<IPlaywrightSessionService, DefaultPlaywrightSessionService>();
        services.AddSingleton<IBrowserSessionFactory, PlaywrightBrowserSessionFactory>();
        services.AddSingleton<ISignInBrowserSessionFactory, PlaywrightBrowserSessionFactory>();
        services.AddSingleton<IBrowserSessionService, DefaultBrowserSessionService>();
        services.AddSingleton<IBrowserDomCaptureService, DefaultBrowserDomCaptureService>();
        services.AddSingleton<IBrowserCaptureService, DefaultBrowserCaptureService>();
        services.AddSingleton<IBrowserDomService, DefaultBrowserDomService>();
        services.AddSingleton<IPlaywright>(_ => Playwright.Playwright.CreateAsync().GetAwaiter().GetResult());
        services.AddSingleton<IConnectivityClassifier, PlaywrightConnectivityClassifier>();
        services.AddHttpClient<IConnectivityProbe, DefaultConnectivityProbe>();

        return services;
    }
}