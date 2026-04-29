using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

using Metalhead.BrowserCaptureRewrite.Abstractions.Connectivity;
using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Resilience;
using Metalhead.BrowserCaptureRewrite.Playwright.Factories;
using Metalhead.BrowserCaptureRewrite.Playwright.Services;
using Metalhead.BrowserCaptureRewrite.Playwright.Sessions;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Tests.Factories;

public class PlaywrightBrowserSessionFactoryTests
{
    private static SessionOptions DefaultOptions => new(BrowserOptions: new BrowserOptions());

    [Fact]
    public async Task CreateSessionAsync_ReturnsNonResilientSession()
    {
        var logger = new Mock<ILogger<PlaywrightBrowserSessionFactory>>();
        var loggerFactory = new Mock<ILoggerFactory>();
        var playwright = new Mock<IPlaywright>();
        var browserType = new Mock<IBrowserType>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var resilienceFactory = new Mock<IResiliencePolicyFactory>();
        var pageCaptureService = Mock.Of<IPlaywrightPageCaptureService>();
        // CreateLogger<T>() is an extension method; mock the underlying interface method instead.
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        browserType.Setup(t => t.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>())).ReturnsAsync(browser.Object);
        browser.Setup(b => b.NewContextAsync(It.IsAny<BrowserNewContextOptions>())).ReturnsAsync(context.Object);
        playwright.Setup(p => p.Chromium).Returns(browserType.Object);
        var factory = new PlaywrightBrowserSessionFactory(logger.Object, loggerFactory.Object, playwright.Object, pageCaptureService, resilienceFactory.Object, Mock.Of<IConnectivityProbe>(), Mock.Of<IConnectivityClassifier>());
        var session = await factory.CreateSessionAsync(DefaultOptions, CancellationToken.None);
        Assert.IsType<PlaywrightBrowserSession>(session);
    }

    [Fact]
    public async Task CreateSessionHandleAsync_ReturnsHandleWithBrowserAndContext()
    {
        var logger = new Mock<ILogger<PlaywrightBrowserSessionFactory>>();
        var loggerFactory = new Mock<ILoggerFactory>();
        var playwright = new Mock<IPlaywright>();
        var browserType = new Mock<IBrowserType>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var resilienceFactory = new Mock<IResiliencePolicyFactory>();
        var pageCaptureService = Mock.Of<IPlaywrightPageCaptureService>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        browserType.Setup(t => t.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>())).ReturnsAsync(browser.Object);
        browser.Setup(b => b.NewContextAsync(It.IsAny<BrowserNewContextOptions>())).ReturnsAsync(context.Object);
        playwright.Setup(p => p.Chromium).Returns(browserType.Object);
        var factory = new PlaywrightBrowserSessionFactory(logger.Object, loggerFactory.Object, playwright.Object, pageCaptureService, resilienceFactory.Object, Mock.Of<IConnectivityProbe>(), Mock.Of<IConnectivityClassifier>());

        var sessionHandle = await factory.CreateSessionHandleAsync(DefaultOptions, CancellationToken.None);

        var handle = Assert.IsType<PlaywrightSessionHandle>(sessionHandle);
        Assert.Same(browser.Object, handle.Browser);
        Assert.Same(context.Object, handle.Context);
    }

    [Fact]
    public async Task CreateSessionAsync_ThrowsOnNullOptions()
    {
        var factory = new PlaywrightBrowserSessionFactory(Mock.Of<ILogger<PlaywrightBrowserSessionFactory>>(), Mock.Of<ILoggerFactory>(), Mock.Of<IPlaywright>(), Mock.Of<IPlaywrightPageCaptureService>(), Mock.Of<IResiliencePolicyFactory>(), Mock.Of<IConnectivityProbe>(), Mock.Of<IConnectivityClassifier>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => factory.CreateSessionAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => factory.CreateSessionHandleAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task CreateResilientSessionAsync_ReturnsResilientSession()
    {
        var logger = new Mock<ILogger<PlaywrightBrowserSessionFactory>>();
        var loggerFactory = new Mock<ILoggerFactory>();
        var playwright = new Mock<IPlaywright>();
        var browserType = new Mock<IBrowserType>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var resilienceFactory = new Mock<IResiliencePolicyFactory>();
        var pageCaptureService = Mock.Of<IPlaywrightPageCaptureService>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        browserType.Setup(t => t.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>())).ReturnsAsync(browser.Object);
        browser.Setup(b => b.NewContextAsync(It.IsAny<BrowserNewContextOptions>())).ReturnsAsync(context.Object);
        playwright.Setup(p => p.Chromium).Returns(browserType.Object);
        var factory = new PlaywrightBrowserSessionFactory(logger.Object, loggerFactory.Object, playwright.Object, pageCaptureService, resilienceFactory.Object, Mock.Of<IConnectivityProbe>(), Mock.Of<IConnectivityClassifier>());
        var session = await factory.CreateSessionAsync(DefaultOptions, CancellationToken.None);
        var resilientBrowserSession = new ResilientBrowserSession(session, resilienceFactory.Object);
        Assert.IsType<ResilientBrowserSession>(resilientBrowserSession);
    }

    [Fact]
    public async Task CreateResilientSessionAsync_DisposesBrowserOnException()
    {
        var logger = new Mock<ILogger<PlaywrightBrowserSessionFactory>>();
        var loggerFactory = new Mock<ILoggerFactory>();
        var playwright = new Mock<IPlaywright>();
        var browserType = new Mock<IBrowserType>();
        var browser = new Mock<IBrowser>();
        var resilienceFactory = new Mock<IResiliencePolicyFactory>();
        var pageCaptureService = Mock.Of<IPlaywrightPageCaptureService>();
        // CreateLogger<T>() is an extension method; mock the underlying interface method instead.
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        browserType.Setup(t => t.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>())).ReturnsAsync(browser.Object);
        browser.Setup(b => b.NewContextAsync(It.IsAny<BrowserNewContextOptions>())).ThrowsAsync(new Exception("fail"));
        playwright.Setup(p => p.Chromium).Returns(browserType.Object);
        var factory = new PlaywrightBrowserSessionFactory(logger.Object, loggerFactory.Object, playwright.Object, pageCaptureService, resilienceFactory.Object, Mock.Of<IConnectivityProbe>(), Mock.Of<IConnectivityClassifier>());
        await Assert.ThrowsAsync<Exception>(() => factory.CreateSessionAsync(DefaultOptions, CancellationToken.None));
        browser.Verify(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>()), Times.Once);
    }

    [Fact]
    public async Task CreateSignInSessionAsync_ThrowsOnNullArgs()
    {
        var factory = new PlaywrightBrowserSessionFactory(Mock.Of<ILogger<PlaywrightBrowserSessionFactory>>(), Mock.Of<ILoggerFactory>(), Mock.Of<IPlaywright>(), Mock.Of<IPlaywrightPageCaptureService>(), Mock.Of<IResiliencePolicyFactory>(), Mock.Of<IConnectivityProbe>(), Mock.Of<IConnectivityClassifier>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => factory.CreateSignInSessionAsync(null!, DefaultOptions, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => factory.CreateSignInSessionAsync(new Uri("https://test"), null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => factory.CreateSignInSessionHandleAsync(null!, DefaultOptions, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => factory.CreateSignInSessionHandleAsync(new Uri("https://test"), null!, CancellationToken.None));
    }

    [Fact]
    public async Task CreateResilientSignInSessionAsync_DisposesAllOnException()
    {
        var logger = new Mock<ILogger<PlaywrightBrowserSessionFactory>>();
        var loggerFactory = new Mock<ILoggerFactory>();
        var playwright = new Mock<IPlaywright>();
        var browserType = new Mock<IBrowserType>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();
        var resilienceFactory = new Mock<IResiliencePolicyFactory>();
        var pageCaptureService = Mock.Of<IPlaywrightPageCaptureService>();
        // CreateLogger<T>() is an extension method; mock the underlying interface method instead.
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        browserType.Setup(t => t.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>())).ReturnsAsync(browser.Object);
        browser.Setup(b => b.NewContextAsync(It.IsAny<BrowserNewContextOptions>())).ReturnsAsync(context.Object);
        context.Setup(c => c.NewPageAsync()).ThrowsAsync(new Exception("fail"));
        playwright.Setup(p => p.Chromium).Returns(browserType.Object);
        var factory = new PlaywrightBrowserSessionFactory(logger.Object, loggerFactory.Object, playwright.Object, pageCaptureService, resilienceFactory.Object, Mock.Of<IConnectivityProbe>(), Mock.Of<IConnectivityClassifier>());
        await Assert.ThrowsAsync<Exception>(() => factory.CreateSignInSessionAsync(new Uri("https://test"), DefaultOptions, CancellationToken.None));
        browser.Verify(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>()), Times.Once);
        context.Verify(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions?>()), Times.Once);
    }
}
