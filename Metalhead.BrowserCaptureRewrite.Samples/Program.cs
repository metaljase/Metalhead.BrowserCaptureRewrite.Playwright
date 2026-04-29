using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

using Metalhead.BrowserCaptureRewrite.Abstractions.Connectivity;
using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Resilience;
using Metalhead.BrowserCaptureRewrite.Abstractions.Validators;
using Metalhead.BrowserCaptureRewrite.Samples;
using Metalhead.BrowserCaptureRewrite.Samples.Formatters;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Factories;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;
using Metalhead.BrowserCaptureRewrite.Samples.Core.Services;

var builder = Host.CreateApplicationBuilder(args);
ILogger<Program>? logger = null;

try
{
    builder.Services.AddOptions<SignInOptions>().Bind(builder.Configuration.GetSection(SignInOptions.SectionName));
    builder.Services.AddSingleton<IValidateOptions<SignInOptions>, SignInOptionsValidation>();
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<SignInOptions>>().Value);

    builder.Services.AddOptions<NavigationTimingOptions>().Bind(builder.Configuration.GetSection(NavigationTimingOptions.SectionName));
    builder.Services.AddSingleton<IValidateOptions<NavigationTimingOptions>, NavigationTimingOptionsValidation>();
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<NavigationTimingOptions>>().Value);

    builder.Services.AddOptions<CaptureTimingOptions>().Bind(builder.Configuration.GetSection(CaptureTimingOptions.SectionName));
    builder.Services.AddSingleton<IValidateOptions<CaptureTimingOptions>, CaptureTimingOptionsValidation>();
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<CaptureTimingOptions>>().Value);

    builder.Services.AddOptions<BrowserOptions>().Bind(builder.Configuration.GetSection(BrowserOptions.SectionName));
    builder.Services.AddSingleton<IValidateOptions<BrowserOptions>, BrowserOptionsValidation>();
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<BrowserOptions>>().Value);

    builder.Services.AddOptions<ResiliencePolicyOptions>().Bind(builder.Configuration.GetSection(ResiliencePolicyOptions.SectionName));
    builder.Services.AddSingleton<IValidateOptions<ResiliencePolicyOptions>, ResiliencePolicyOptionsValidation>();
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ResiliencePolicyOptions>>().Value);

    builder.Services.AddOptions<ConnectivityProbeOptions>().Bind(builder.Configuration.GetSection(ConnectivityProbeOptions.SectionName));
    builder.Services.AddSingleton<IValidateOptions<ConnectivityProbeOptions>, ConnectivityProbeOptionsValidation>();
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ConnectivityProbeOptions>>().Value);

    builder.Services.AddSingleton<App>();

    // Core services.
    builder.Services.AddSingleton<ICaptureSpecFactoryByUrls, CaptureSpecFactoryByUrls>();
    builder.Services.AddSingleton<ICaptureSpecFactoryByContent, CaptureSpecFactoryByContent>();
    builder.Services.AddSingleton<IRewriteSpecFactoryByContent, RewriteSpecFactoryByContent>();
    builder.Services.AddSingleton<IRewriteSpecFactoryByUrls, RewriteSpecFactoryByUrls>();
    builder.Services.AddSingleton<IDomCaptureSampleService, DomCaptureSampleService>();
    builder.Services.AddSingleton<IResourceCaptureSampleService, ResourceCaptureSampleService>();
    builder.Services.AddSingleton<IDomAndResourcesCaptureSampleService, DomAndResourcesCaptureSampleService>();
    builder.Services.AddSingleton<IRawNavigateAndCaptureSampleService, RawNavigateAndCaptureSampleService>();
    builder.Services.AddSingleton<IPlaywrightCaptureSampleService, PlaywrightCaptureSampleService>();
    builder.Services.AddSingleton<IExtensionMinimalSample, ExtensionMinimalSample>();
    builder.Services.AddSingleton<IConvenienceMinimalSample, ConvenienceMinimalSample>();

    // Playwright & browser capture.
    builder.Services.AddPlaywrightCaptureRewrite();
    // Alternatively, can hardcode resilience policy retry delays...
    //builder.Services.AddPlaywrightCaptureRewrite(options =>
    //{
    //    options.TransportRetryDelaysSeconds = [7, 8, 15, 30, 60, 300];
    //    options.TimeoutRetryDelaysSeconds = [1, 3, 5];
    //});

    // Register the custom console formatter.
    builder.Services.AddSingleton<ConsoleFormatter, CustomConsoleFormatter>();
    builder.Services.Configure<CustomConsoleFormatterOptions>(builder.Configuration.GetSection("Logging:Console:FormatterOptions"));
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(options => options.FormatterName = "Custom");

    using var host = builder.Build();

    using var serviceScope = host.Services.CreateScope();
    var serviceProvider = serviceScope.ServiceProvider;
    logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Application starting...");

    await serviceProvider.GetRequiredService<App>().RunAsync();
}
catch (Exception ex)
{
    if (logger == null)
    {
        // If logger isn't available yet, create a minimal one for error output.
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        logger = loggerFactory.CreateLogger<Program>();
    }

    if (ex is OptionsValidationException optionsValidationException)
    {
        logger.LogCritical(ex, """
            Application exited due to invalid app settings:
            {ValidationErrors}
            """,
            ex.Message.Replace("; ", Environment.NewLine));
    }
    else
        logger.LogCritical(ex, """
            Application exited unexpectedly: {Message}
            {StackTrace}
            """,
            ex.Message,
            ex.StackTrace);

    Environment.Exit(1);
}