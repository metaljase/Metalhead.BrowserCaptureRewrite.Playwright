using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

using Metalhead.BrowserCaptureRewrite.Abstractions.Connectivity;
using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Factories;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Resilience;
using Metalhead.BrowserCaptureRewrite.Abstractions.Services;
using Metalhead.BrowserCaptureRewrite.Abstractions.Validators;
using Metalhead.BrowserCaptureRewrite.Playwright.Connectivity;
using Metalhead.BrowserCaptureRewrite.Playwright.Factories;
using Metalhead.BrowserCaptureRewrite.Playwright.Services;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods for registering Playwright-based browser capture and session services with a dependency injection
/// container.
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
    /// Registers all Playwright-based browser capture and session services with the dependency injection container, using all
    /// default options values.
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to add services to.  Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The same <see cref="IServiceCollection"/> instance for chaining.
    /// </returns>
    /// <remarks>
    /// <para>
    /// All options types use their default values.  To load options from configuration, use the overload accepting
    /// <see cref="IConfiguration"/>.  To supply hardcoded values for any options type, use the overload accepting
    /// <see cref="Action{PlaywrightCaptureRewriteBuilder}"/>.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddPlaywrightCaptureRewrite(this IServiceCollection services)
    {
        return services.AddPlaywrightCaptureRewrite((PlaywrightCaptureRewriteBuilder _) => { });
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
    /// This overload is obsolete.  Use the overload accepting <see cref="Action{PlaywrightCaptureRewriteBuilder}"/> instead,
    /// which supports hardcoded configuration of all options types.
    /// </para>
    /// </remarks>
    [Obsolete("Use AddPlaywrightCaptureRewrite(Action<PlaywrightCaptureRewriteBuilder>) instead, which supports configuring all options types.")]
    public static IServiceCollection AddPlaywrightCaptureRewrite(this IServiceCollection services, Action<ResiliencePolicyOptions> configure)
    {
        return services.AddPlaywrightCaptureRewrite(b => b.ConfigureResiliencePolicy = configure);
    }

    /// <summary>
    /// Registers all Playwright-based browser capture and session services with the dependency injection container, loading all
    /// options from the supplied <see cref="IConfiguration"/>.
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to add services to.  Must not be <see langword="null"/>.
    /// </param>
    /// <param name="configuration">
    /// The <see cref="IConfiguration"/> instance from which all options sections are bound.  Must not be
    /// <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The same <see cref="IServiceCollection"/> instance for chaining.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Binds <see cref="SignInOptions"/>, <see cref="NavigationTimingOptions"/>, <see cref="CaptureTimingOptions"/>,
    /// <see cref="BrowserOptions"/>, <see cref="ResiliencePolicyOptions"/>, and <see cref="ConnectivityProbeOptions"/> from
    /// their respective configuration sections, and registers validation for each.
    /// </para>
    /// <para>
    /// To supply hardcoded values for any options type instead, use the overload accepting
    /// <see cref="Action{PlaywrightCaptureRewriteBuilder}"/>.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddPlaywrightCaptureRewrite(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SignInOptions>().Bind(configuration.GetSection(SignInOptions.SectionName));
        services.AddSingleton<IValidateOptions<SignInOptions>, SignInOptionsValidation>();

        services.AddOptions<NavigationTimingOptions>().Bind(configuration.GetSection(NavigationTimingOptions.SectionName));
        services.AddSingleton<IValidateOptions<NavigationTimingOptions>, NavigationTimingOptionsValidation>();

        services.AddOptions<CaptureTimingOptions>().Bind(configuration.GetSection(CaptureTimingOptions.SectionName));
        services.AddSingleton<IValidateOptions<CaptureTimingOptions>, CaptureTimingOptionsValidation>();

        services.AddOptions<BrowserOptions>().Bind(configuration.GetSection(BrowserOptions.SectionName));
        services.AddSingleton<IValidateOptions<BrowserOptions>, BrowserOptionsValidation>();

        services.AddOptions<ResiliencePolicyOptions>().Bind(configuration.GetSection(ResiliencePolicyOptions.SectionName));
        services.AddSingleton<IValidateOptions<ResiliencePolicyOptions>, ResiliencePolicyOptionsValidation>();

        services.AddOptions<ConnectivityProbeOptions>().Bind(configuration.GetSection(ConnectivityProbeOptions.SectionName));
        services.AddSingleton<IValidateOptions<ConnectivityProbeOptions>, ConnectivityProbeOptionsValidation>();

        return services.AddPlaywrightCaptureRewriteCore();
    }

    /// <summary>
    /// Registers all Playwright-based browser capture and session services with the dependency injection container, allowing
    /// hardcoded configuration of any combination of options types via <see cref="PlaywrightCaptureRewriteBuilder"/>.
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to add services to.  Must not be <see langword="null"/>.
    /// </param>
    /// <param name="configure">
    /// A delegate to configure a <see cref="PlaywrightCaptureRewriteBuilder"/>, which exposes individual configure delegates for
    /// each options type.  Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The same <see cref="IServiceCollection"/> instance for chaining.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you want to supply hardcoded values for any combination of options types, rather than loading
    /// them from an <see cref="IConfiguration"/> source.  Any property left at its default on the
    /// <see cref="PlaywrightCaptureRewriteBuilder"/> results in no changes to that options type's defaults.
    /// </para>
    /// <para>
    /// Registers validation for all options types and all core Playwright, browser session, capture, DOM, resilience, and
    /// connectivity services as singletons.  The Playwright instance is created synchronously at registration time.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddPlaywrightCaptureRewrite(
        this IServiceCollection services, Action<PlaywrightCaptureRewriteBuilder> configure)
    {
        var builder = new PlaywrightCaptureRewriteBuilder();
        configure(builder);

        services.Configure(builder.ConfigureSignIn);
        services.AddSingleton<IValidateOptions<SignInOptions>, SignInOptionsValidation>();

        services.Configure(builder.ConfigureNavigationTiming);
        services.AddSingleton<IValidateOptions<NavigationTimingOptions>, NavigationTimingOptionsValidation>();

        services.Configure(builder.ConfigureCaptureTiming);
        services.AddSingleton<IValidateOptions<CaptureTimingOptions>, CaptureTimingOptionsValidation>();

        services.Configure(builder.ConfigureBrowser);
        services.AddSingleton<IValidateOptions<BrowserOptions>, BrowserOptionsValidation>();

        services.Configure(builder.ConfigureResiliencePolicy);
        services.AddSingleton<IValidateOptions<ResiliencePolicyOptions>, ResiliencePolicyOptionsValidation>();

        services.Configure(builder.ConfigureConnectivityProbe);
        services.AddSingleton<IValidateOptions<ConnectivityProbeOptions>, ConnectivityProbeOptionsValidation>();

        return services.AddPlaywrightCaptureRewriteCore();
    }

    private static IServiceCollection AddPlaywrightCaptureRewriteCore(this IServiceCollection services)
    {
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<SignInOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<NavigationTimingOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<CaptureTimingOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<BrowserOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ResiliencePolicyOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ConnectivityProbeOptions>>().Value);
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