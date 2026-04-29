# BrowserCaptureRewrite.Playwright
`BrowserCaptureRewrite.Playwright` is a .NET library that implements the abstractions in [`BrowserCaptureRewrite.Abstractions`](https://github.com/metaljase/Metalhead.BrowserCaptureRewrite.Abstractions) using [Playwright](https://playwright.dev/dotnet/).  It can intercept HTTP requests from a web page and capture the corresponding HTTP responses in-flight by targeting specific requests and optionally rewriting those responses - including the response for the web page itself.

Because intercepting and modifying HTTP responses happens in-flight before they reach the browser's rendering engine, you can fundamentally alter the page's behaviour.  For example, the initial web page HTML can be rewritten to manipulate the subsequent HTTP requests it makes.  Another example: a web page may fetch a JSON file and render its UI based on that data; by modifying the JSON response before the client-side code processes it, you can change the behaviour of the page.

Optionally, resiliency features such as retry logic and timeout handling can be configured, and the ability to manually sign-in is supported, for when the target web page requires authentication.

A key part for capturing in-flight HTTP responses is creating a `CaptureSpec` instance, which specifies what HTTP responses should be captured.  Similarly, rewriting in-flight HTTP responses relies on a `RewriteSpec` instance, which specifies which responses should be rewritten and how.

With a browser instance, an overload of `NavigateAndCaptureResultAsync` in `PlaywrightPageCaptureService` can be called to perform the navigation, capture, and optional rewrite, by providing a `CaptureSpec` and optionally a `RewriteSpec` instance.  However, it's usually more convenient to call an overload in one of the [convenience classes or extension methods](#capturerewrite-methods) instead.

# BrowserCaptureRewrite.Samples
`BrowserCaptureRewrite.Samples` is a .NET console application that uses the sample code in `BrowserCaptureRewrite.Samples.Core` to demonstrate how `BrowserCaptureRewrite.Abstractions` and `BrowserCaptureRewrite.Playwright` can capture or rewrite in-flight HTTP responses from web page URLs.

# Setup instructions
## Installing `BrowserCaptureRewrite.Playwright`
Add the `BrowserCaptureRewrite.Playwright` NuGet package to your project via your IDE, or by running the following command:
```bash
dotnet add package Metalhead.BrowserCaptureRewrite.Playwright
```

## Installing Playwright browsers
`BrowserCaptureRewrite.Playwright` relies on Playwright to drive the browser, so Playwright and its dependencies must be installed.  Before doing so, build your project to generate the Playwright installation script:
```bash
dotnet build
```

Run the following command from your project directory to install Playwright and the supported browsers.  NOTE: If your project is not using .NET 8.0, replace `net8.0` in the path with your target framework:
```bash
pwsh bin/Debug/net8.0/playwright.ps1 install
```

## Configuration
The Playwright implementation of `BrowserCaptureRewrite.Abstractions` must be added to your project's dependency injection container, which registers the necessary services for capturing and rewriting in-flight HTTP responses:
```csharp
builder.Services.AddPlaywrightCaptureRewrite();
```
See the [Configuration section on the `BrowserCaptureRewrite.Abstractions` repository](https://github.com/metaljase/Metalhead.BrowserCaptureRewrite.Abstractions#configuration) for details on the available options that can be added to `appsettings.json` or supplied through any other .NET configuration provider (e.g. environment variables, user secrets, command‑line arguments) for configuring various aspects of the library, such as navigation timing, capture timing, browser settings, resiliency policies, and connectivity probes.

# Examples
See the [Examples section on the `BrowserCaptureRewrite.Abstractions` repository](https://github.com/metaljase/Metalhead.BrowserCaptureRewrite.Abstractions#examples) for code examples that demonstrate how to capture and rewrite in-flight HTTP responses.

# Capture/Rewrite methods
See the [Capture/Rewrite methods section on the `BrowserCaptureRewrite.Abstractions` repository](https://github.com/metaljase/Metalhead.BrowserCaptureRewrite.Abstractions#capturerewrite-methods) for details on the available extension methods and convenience methods for capturing and rewriting in-flight HTTP responses.

## Playwright service
Ultimately, the extension methods and convenience methods call through to `PlaywrightPageCaptureService` (for this implementation) to perform the actual work of navigating to the page URL, capturing the page's response HTML, rendered HTML, in-flight HTTP responses, and optionally rewriting in-flight HTTP responses.  It works directly with Playwright's `IPage`, so it can be used for more custom scenarios where you need direct access to the `IPage` or want to use Playwright features that aren't abstracted by the other methods.  However, unlike the extension methods and convenience methods, [`PageCaptureIncompleteException`](https://github.com/metaljase/Metalhead.BrowserCaptureRewrite.Abstractions/blob/master/Metalhead.BrowserCaptureRewrite.Abstractions/Exceptions/PageCaptureIncompleteException.cs) is not thrown when capture does not complete successfully; therefore, it's recommended `PlaywrightPageCaptureService` is only used when the other capture methods aren't sufficient.

XML documentation for [`IPlaywrightPageCaptureService`](https://github.com/metaljase/Metalhead.BrowserCaptureRewrite.Playwright/blob/master/Metalhead.BrowserCaptureRewrite.Playwright/Services/IPlaywrightPageCaptureService.cs) is available in the source code.

[`IPlaywrightPageCaptureService`](https://github.com/metaljase/Metalhead.BrowserCaptureRewrite.Playwright/blob/master/Metalhead.BrowserCaptureRewrite.Playwright/Services/IPlaywrightPageCaptureService.cs): Implementations (`PlaywrightPageCaptureService`) return the page's response HTML, rendered HTML, and in-flight HTTP responses.

```csharp
Task<PageCaptureResult> NavigateAndCaptureResultAsync(
    IPage page,
    PageCaptureParts captureParts,
    NavigationOptions navOptions,
    CaptureSpec? captureSpec,
    CaptureTimingOptions timingOptions,
    CancellationToken cancellationToken);

Task<PageCaptureResult> NavigateAndCaptureResultAsync(
    IPage page,
    PageCaptureParts captureParts,
    NavigationOptions navOptions,
    CaptureSpec? captureSpec,
    RewriteSpec? rewriteSpec,
    CaptureTimingOptions timingOptions,
    CancellationToken cancellationToken);
```
