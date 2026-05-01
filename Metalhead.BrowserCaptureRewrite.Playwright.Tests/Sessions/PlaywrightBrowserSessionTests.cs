using Microsoft.Playwright;
using Moq;
using Polly;
using Polly.Timeout;

using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Enums;
using Metalhead.BrowserCaptureRewrite.Abstractions.Models;
using Metalhead.BrowserCaptureRewrite.Abstractions.Resilience;
using Metalhead.BrowserCaptureRewrite.Playwright.Services;
using Metalhead.BrowserCaptureRewrite.Playwright.Sessions;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Tests.Sessions;

public class PlaywrightBrowserSessionTests
{
    [Fact]
    public async Task NavigateAndCaptureAsync_NavigationReturnsNotFound_ThrowsHttpRequestException()
    {
        // Arrange
        var Url = new Uri("https://example.com/app?id=666");
        Uri? refererUrl = null;
        TimeSpan? pageLoadTimeout = null;
        TimeSpan? networkCallsTimeout = null;
        var navOptions = new NavigationOptions(Url, refererUrl, pageLoadTimeout);
        var timingOptions = BuildCaptureTimingOptions(networkCallsTimeout);

        var mockResilience = new Mock<IResiliencePolicyFactory>();
        var mockBrowser = new Mock<IBrowser>();
        var mockBrowserContext = new Mock<IBrowserContext>();
        var mockPage = new Mock<IPage>();
        var mockResponse = new Mock<IResponse>();

        mockResilience.Setup(x => x.BuildResiliencePolicy<PageCaptureResult>(Url, pageLoadTimeout, It.IsAny<CancellationToken>()))
            .Returns(Policy.WrapAsync(Policy.NoOpAsync<PageCaptureResult>(), Policy.NoOpAsync<PageCaptureResult>()));
        mockBrowserContext.Setup(x => x.NewPageAsync()).ReturnsAsync(mockPage.Object);
        mockPage.Setup(x => x.GotoAsync(Url.ToString(), It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(mockResponse.Object);
        mockResponse.Setup(x => x.Status).Returns(404);
        mockPage.Setup(p => p.WaitForLoadStateAsync(LoadState.NetworkIdle, It.IsAny<PageWaitForLoadStateOptions>()))
            .Returns(Task.CompletedTask);

        var captureSpec = new CaptureSpec(
            _ => true,
            async (r, s) => new CapturedResource(new Uri(r.Url), await s.GetBodyAsStringAsync(), null, null, null, null),
            shouldCompleteCapture: null);
        var session = CreateSession(mockResilience, mockBrowser, mockBrowserContext);

        // Act
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            session.NavigateAndCaptureResultAsync(PageCaptureParts.Resources, navOptions, captureSpec, timingOptions, CancellationToken.None));

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task NavigateAndCaptureAsync_NavigationTransientFailures_RetriesAndReturnsEmpty()
    {
        // Arrange
        var Url = new Uri("https://example.com/app?id=666");
        Uri? refererUrl = null;
        TimeSpan? pageLoadTimeout = null;
        TimeSpan? networkCallsTimeout = null;
        var navOptions = new NavigationOptions(Url, refererUrl, pageLoadTimeout);
        var timingOptions = BuildCaptureTimingOptions(networkCallsTimeout);

        var mockResilience = new Mock<IResiliencePolicyFactory>();
        var mockBrowser = new Mock<IBrowser>();
        var mockBrowserContext = new Mock<IBrowserContext>();
        var mockPage = new Mock<IPage>();
        var mockResponse = new Mock<IResponse>();

        mockResilience.Setup(x => x.BuildResiliencePolicy<PageCaptureResult>(Url, pageLoadTimeout, It.IsAny<CancellationToken>()))
            .Returns(Policy.WrapAsync(
                Policy<PageCaptureResult>.Handle<TimeoutException>().Or<TaskCanceledException>().RetryAsync(3),
                Policy<PageCaptureResult>.Handle<HttpRequestException>().Or<PlaywrightException>().RetryAsync(5)));

        mockBrowserContext.Setup(x => x.NewPageAsync()).ReturnsAsync(mockPage.Object);

        mockPage.SetupSequence(x => x.GotoAsync(Url.ToString(), It.IsAny<PageGotoOptions>()))
            .ThrowsAsync(new HttpRequestException())
            .ThrowsAsync(new HttpRequestException())
            .ThrowsAsync(new PlaywrightException())
            .ThrowsAsync(new PlaywrightException())
            .ThrowsAsync(new HttpRequestException())
            .ThrowsAsync(new TimeoutException())
            .ThrowsAsync(new TimeoutException())
            .ThrowsAsync(new TaskCanceledException())
            .ReturnsAsync(mockResponse.Object);
        mockResponse.Setup(x => x.Status).Returns(200);

        mockPage.Setup(p => p.WaitForLoadStateAsync(LoadState.NetworkIdle, It.IsAny<PageWaitForLoadStateOptions>()))
            .Returns(Task.CompletedTask);

        var captureSpec = new CaptureSpec(
            _ => false,
            async (r, s) => new CapturedResource(new Uri(r.Url), await s.GetBodyAsStringAsync(), null, null, null, null),
            shouldCompleteCapture: null);
        var session = CreateSession(mockResilience, mockBrowser, mockBrowserContext);

        // Act
        var result = await session.NavigateAndCaptureResultAsync(PageCaptureParts.Resources, navOptions, captureSpec, timingOptions, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Resources);
        mockBrowserContext.Verify(b => b.NewPageAsync(), Times.Exactly(9));
        mockPage.Verify(p => p.GotoAsync(Url.ToString(), It.IsAny<PageGotoOptions>()), Times.Exactly(9));
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(501)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    [InlineData(505)]
    [InlineData(506)]
    [InlineData(507)]
    [InlineData(508)]
    [InlineData(510)]
    [InlineData(511)]
    public async Task NavigateAndCaptureAsync_NavigationTransientStatusCodes_RetriesAndReturnsEmpty(int transientStatusCode)
    {
        // Arrange
        var Url = new Uri("https://example.com/app?id=666");
        Uri? refererUrl = null;
        TimeSpan? pageLoadTimeout = null;
        TimeSpan? networkCallsTimeout = null;
        var navOptions = new NavigationOptions(Url, refererUrl, pageLoadTimeout);
        var timingOptions = BuildCaptureTimingOptions(networkCallsTimeout);

        var mockResilience = new Mock<IResiliencePolicyFactory>();
        var mockBrowser = new Mock<IBrowser>();
        var mockBrowserContext = new Mock<IBrowserContext>();
        var mockPage = new Mock<IPage>();
        var mockResponse = new Mock<IResponse>();
        var mockTransientResponse = new Mock<IResponse>();

        mockResilience.Setup(x => x.BuildResiliencePolicy<PageCaptureResult>(Url, pageLoadTimeout, It.IsAny<CancellationToken>()))
            .Returns(Policy.WrapAsync(
                Policy.NoOpAsync<PageCaptureResult>(),
                Policy<PageCaptureResult>.Handle<HttpRequestException>().RetryAsync(5)));
        mockBrowserContext.Setup(x => x.NewPageAsync()).ReturnsAsync(mockPage.Object);

        mockPage.SetupSequence(x => x.GotoAsync(Url.ToString(), It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(mockTransientResponse.Object)
            .ReturnsAsync(mockTransientResponse.Object)
            .ReturnsAsync(mockTransientResponse.Object)
            .ReturnsAsync(mockTransientResponse.Object)
            .ReturnsAsync(mockTransientResponse.Object)
            .ReturnsAsync(mockResponse.Object);
        mockTransientResponse.Setup(x => x.Status).Returns(transientStatusCode);
        mockResponse.Setup(x => x.Status).Returns(200);

        mockPage.Setup(p => p.WaitForLoadStateAsync(LoadState.NetworkIdle, It.IsAny<PageWaitForLoadStateOptions>()))
            .Returns(Task.CompletedTask);

        var captureSpec = new CaptureSpec(
            _ => false,
            async (r, s) => new CapturedResource(new Uri(r.Url), await s.GetBodyAsStringAsync(), null, null, null, null),
            shouldCompleteCapture: null);
        var session = CreateSession(mockResilience, mockBrowser, mockBrowserContext);

        // Act
        var result = await session.NavigateAndCaptureResultAsync(PageCaptureParts.Resources, navOptions, captureSpec, timingOptions, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Resources);
        mockBrowserContext.Verify(b => b.NewPageAsync(), Times.Exactly(6));
        mockPage.Verify(p => p.GotoAsync(Url.ToString(), It.IsAny<PageGotoOptions>()), Times.Exactly(6));
    }

    [Fact]
    public async Task NavigateAndCaptureAsync_NavigationTimeout_ThrowsTimeoutRejectedException()
    {
        // Arrange
        var Url = new Uri("https://example.com/app?id=666");
        Uri? refererUrl = null;
        TimeSpan? pageLoadTimeout = null;
        TimeSpan? networkCallsTimeout = null;
        var navOptions = new NavigationOptions(Url, refererUrl, pageLoadTimeout);
        var timingOptions = BuildCaptureTimingOptions(networkCallsTimeout);
        var timeoutPolicyTimeout = TimeSpan.FromMilliseconds(20);

        var timeoutPolicy = Policy.TimeoutAsync<PageCaptureResult>(timeoutPolicyTimeout, TimeoutStrategy.Pessimistic);
        var resiliencePolicy = Policy<PageCaptureResult>.Handle<HttpRequestException>().RetryAsync(3);
        var wrappedPolicy = Policy.WrapAsync(resiliencePolicy, timeoutPolicy);

        var mockResilience = new Mock<IResiliencePolicyFactory>();
        var mockBrowser = new Mock<IBrowser>();
        var mockBrowserContext = new Mock<IBrowserContext>();
        var mockPage = new Mock<IPage>();
        var mockResponse = new Mock<IResponse>();

        mockResilience.Setup(x => x.BuildResiliencePolicy<PageCaptureResult>(Url, pageLoadTimeout, It.IsAny<CancellationToken>()))
            .Returns(wrappedPolicy);
        mockBrowserContext.Setup(x => x.NewPageAsync()).ReturnsAsync(mockPage.Object);
        mockPage.Setup(x => x.GotoAsync(Url.ToString(), It.IsAny<PageGotoOptions>()))
            .Returns(async () =>
            {
                await Task.Delay(timeoutPolicyTimeout + TimeSpan.FromMilliseconds(80));
                return mockResponse.Object;
            });
        mockResponse.Setup(x => x.Status).Returns(200);
        mockPage.Setup(p => p.WaitForLoadStateAsync(LoadState.NetworkIdle, It.IsAny<PageWaitForLoadStateOptions>()))
            .Returns(Task.CompletedTask);

        var captureSpec = new CaptureSpec(
            _ => true,
            async (r, s) => new CapturedResource(new Uri(r.Url), await s.GetBodyAsStringAsync(), null, null, null, null),
            shouldCompleteCapture: null);
        var session = CreateSession(mockResilience, mockBrowser, mockBrowserContext);

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutRejectedException>(() =>
            session.NavigateAndCaptureResultAsync(PageCaptureParts.Resources, navOptions, captureSpec, timingOptions, CancellationToken.None));
    }

    [Fact]
    public async Task NavigateAndCaptureAsync_CapturesResponseAndRenderedHtml()
    {
        // Arrange
        var Url = new Uri("https://example.com/app?id=666");
        var navOptions = new NavigationOptions(Url, null, null);
        var timingOptions = BuildCaptureTimingOptions(null);
        var responseHtml = "<html>initial</html>";
        var renderedHtml = "<html>final</html>";

        var mockResilience = new Mock<IResiliencePolicyFactory>();
        var mockBrowser = new Mock<IBrowser>();
        var mockBrowserContext = new Mock<IBrowserContext>();
        var mockPage = new Mock<IPage>();
        var mockResponse = new Mock<IResponse>();

        mockResilience.Setup(x => x.BuildResiliencePolicy<PageCaptureResult>(Url, null, It.IsAny<CancellationToken>()))
            .Returns(Policy.WrapAsync(Policy.NoOpAsync<PageCaptureResult>(), Policy.NoOpAsync<PageCaptureResult>()));
        mockBrowserContext.Setup(x => x.NewPageAsync()).ReturnsAsync(mockPage.Object);
        mockPage.Setup(x => x.GotoAsync(Url.ToString(), It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(mockResponse.Object);
        mockResponse.Setup(x => x.Status).Returns(200);
        mockResponse.Setup(x => x.TextAsync()).ReturnsAsync(responseHtml);
        mockPage.Setup(x => x.ContentAsync()).ReturnsAsync(renderedHtml);
        mockPage.Setup(p => p.WaitForLoadStateAsync(LoadState.NetworkIdle, It.IsAny<PageWaitForLoadStateOptions>()))
            .Returns(Task.CompletedTask);

        var session = CreateSession(mockResilience, mockBrowser, mockBrowserContext);

        // Act
        var result = await session.NavigateAndCaptureResultAsync(
            PageCaptureParts.ResponseHtml | PageCaptureParts.RenderedHtml,
            navOptions,
            null,
            timingOptions,
            CancellationToken.None);

        // Assert
        Assert.Equal(responseHtml, result.ResponseHtml);
        Assert.Equal(renderedHtml, result.RenderedHtml);
        Assert.Empty(result.Resources);
        mockPage.Verify(p => p.WaitForLoadStateAsync(LoadState.NetworkIdle, It.IsAny<PageWaitForLoadStateOptions>()), Times.Once);
    }

    [Fact]
    public async Task NavigateAndCaptureAsync_CapturesResourcesFromRequests()
    {
        // Arrange
        var Url = new Uri("https://example.com/app?id=666");
        var navOptions = new NavigationOptions(Url, null, null);
        var timingOptions = BuildCaptureTimingOptions(null);
        var resourceUrl = "https://example.com/resource.json";
        var resourceBody = "{\"ok\":true}";

        var mockResilience = new Mock<IResiliencePolicyFactory>();
        var mockBrowser = new Mock<IBrowser>();
        var mockBrowserContext = new Mock<IBrowserContext>();
        var mockPage = new Mock<IPage>();
        var mockNavResponse = new Mock<IResponse>();
        var mockRequest = new Mock<IRequest>();
        var mockResourceResponse = new Mock<IResponse>();

        mockResilience.Setup(x => x.BuildResiliencePolicy<PageCaptureResult>(Url, null, It.IsAny<CancellationToken>()))
            .Returns(Policy.WrapAsync(Policy.NoOpAsync<PageCaptureResult>(), Policy.NoOpAsync<PageCaptureResult>()));
        mockBrowserContext.Setup(x => x.NewPageAsync()).ReturnsAsync(mockPage.Object);
        mockNavResponse.Setup(x => x.Status).Returns(200);
        mockRequest.Setup(x => x.Url).Returns(resourceUrl);
        mockRequest.Setup(x => x.ResponseAsync()).ReturnsAsync(mockResourceResponse.Object);
        mockResourceResponse.Setup(x => x.TextAsync()).ReturnsAsync(resourceBody);
        mockPage.SetupAdd(x => x.Request += It.IsAny<EventHandler<IRequest>>())
            .Callback<EventHandler<IRequest>>(handler =>
            {
                mockPage.Setup(x => x.GotoAsync(Url.ToString(), It.IsAny<PageGotoOptions>()))
                    .Callback(() => handler.Invoke(mockPage.Object, mockRequest.Object))
                    .ReturnsAsync(mockNavResponse.Object);
            });
        mockPage.Setup(p => p.WaitForLoadStateAsync(LoadState.NetworkIdle, It.IsAny<PageWaitForLoadStateOptions>()))
            .Returns(Task.CompletedTask);

        var captureSpec = new CaptureSpec(
            _ => true,
            async (r, s) => new CapturedResource(new Uri(r.Url), await s.GetBodyAsStringAsync(), null, null, null, null),
            shouldCompleteCapture: null);
        var session = CreateSession(mockResilience, mockBrowser, mockBrowserContext);

        // Act
        var result = await session.NavigateAndCaptureResultAsync(
            PageCaptureParts.Resources,
            navOptions,
            captureSpec,
            timingOptions,
            CancellationToken.None);

        // Assert
        Assert.Single(result.Resources);
        Assert.Equal(resourceUrl, result.Resources[0].Url.AbsoluteUri);
        Assert.Equal(resourceBody, result.Resources[0].TextContent);
    }

    [Fact]
    public async Task NavigateAndCaptureAsync_CompletionConditionSatisfied_ReturnsCriteriaSatisfied()
    {
        // Arrange
        var Url = new Uri("https://example.com/app?id=666");
        var navOptions = new NavigationOptions(Url, null, null);
        var timingOptions = BuildCaptureTimingOptions(null);

        var mockResilience = new Mock<IResiliencePolicyFactory>();
        var mockBrowser = new Mock<IBrowser>();
        var mockBrowserContext = new Mock<IBrowserContext>();
        var mockPage = new Mock<IPage>();
        var mockResponse = new Mock<IResponse>();

        mockResilience.Setup(x => x.BuildResiliencePolicy<PageCaptureResult>(Url, null, It.IsAny<CancellationToken>()))
            .Returns(Policy.WrapAsync(Policy.NoOpAsync<PageCaptureResult>(), Policy.NoOpAsync<PageCaptureResult>()));
        mockBrowserContext.Setup(x => x.NewPageAsync()).ReturnsAsync(mockPage.Object);
        mockPage.Setup(x => x.GotoAsync(Url.ToString(), It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(mockResponse.Object);
        mockResponse.Setup(x => x.Status).Returns(200);
        mockPage.SetupGet(x => x.Url).Returns(Url.ToString());

        var captureSpec = new CaptureSpec(
            _ => true,
            (_, _) => Task.FromResult<CapturedResource?>(null),
            shouldCompleteCapture: (_, _, _) => true);
        var session = CreateSession(mockResilience, mockBrowser, mockBrowserContext);

        // Act
        var result = await session.NavigateAndCaptureResultAsync(
            PageCaptureParts.Resources,
            navOptions,
            captureSpec,
            timingOptions,
            CancellationToken.None);

        // Assert
        Assert.Equal(CaptureStatus.CriteriaSatisfied, result.CaptureStatus);
    }

    [Fact]
    public async Task NavigateAndCaptureAsync_CompletionConditionTimeout_ReturnsTimeoutExceeded()
    {
        // Arrange
        var Url = new Uri("https://example.com/app?id=666");
        var navOptions = new NavigationOptions(Url, null, null);
        var captureTimingOptions = new CaptureTimingOptions(null, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1));

        var mockResilience = new Mock<IResiliencePolicyFactory>();
        var mockBrowser = new Mock<IBrowser>();
        var mockBrowserContext = new Mock<IBrowserContext>();
        var mockPage = new Mock<IPage>();
        var mockResponse = new Mock<IResponse>();

        mockResilience.Setup(x => x.BuildResiliencePolicy<PageCaptureResult>(Url, null, It.IsAny<CancellationToken>()))
            .Returns(Policy.WrapAsync(Policy.NoOpAsync<PageCaptureResult>(), Policy.NoOpAsync<PageCaptureResult>()));
        mockBrowserContext.Setup(x => x.NewPageAsync()).ReturnsAsync(mockPage.Object);
        mockPage.Setup(x => x.GotoAsync(Url.ToString(), It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(mockResponse.Object);
        mockResponse.Setup(x => x.Status).Returns(200);
        mockPage.SetupGet(x => x.Url).Returns(Url.ToString());

        var captureSpec = new CaptureSpec(
            _ => true,
            (_, _) => Task.FromResult<CapturedResource?>(null),
            shouldCompleteCapture: (_, _, _) => false);
        var session = CreateSession(mockResilience, mockBrowser, mockBrowserContext);

        // Act
        var result = await session.NavigateAndCaptureResultAsync(
            PageCaptureParts.Resources,
            navOptions,
            captureSpec,
            captureTimingOptions,
            CancellationToken.None);

        // Assert
        Assert.Equal(CaptureStatus.CaptureTimeoutExceeded, result.CaptureStatus);
    }

    [Fact]
    public async Task NavigateAndCaptureAsync_CompletionConditionUrlChanged_ReturnsUrlChanged()
    {
        // Arrange
        var Url = new Uri("https://example.com/app?id=666");
        var navOptions = new NavigationOptions(Url, null, null);
        var captureTimingOptions = new CaptureTimingOptions(null, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(1));

        var mockResilience = new Mock<IResiliencePolicyFactory>();
        var mockBrowser = new Mock<IBrowser>();
        var mockBrowserContext = new Mock<IBrowserContext>();
        var mockPage = new Mock<IPage>();
        var mockResponse = new Mock<IResponse>();

        mockResilience.Setup(x => x.BuildResiliencePolicy<PageCaptureResult>(Url, null, It.IsAny<CancellationToken>()))
            .Returns(Policy.WrapAsync(Policy.NoOpAsync<PageCaptureResult>(), Policy.NoOpAsync<PageCaptureResult>()));
        mockBrowserContext.Setup(x => x.NewPageAsync()).ReturnsAsync(mockPage.Object);
        mockPage.Setup(x => x.GotoAsync(Url.ToString(), It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(mockResponse.Object);
        mockResponse.Setup(x => x.Status).Returns(200);
        mockPage.SetupGet(x => x.Url).Returns("https://example.com/other");

        var captureSpec = new CaptureSpec(
            _ => true,
            (_, _) => Task.FromResult<CapturedResource?>(null),
            shouldCompleteCapture: (_, _, _) => false);
        var session = CreateSession(mockResilience, mockBrowser, mockBrowserContext);

        // Act
        var result = await session.NavigateAndCaptureResultAsync(
            PageCaptureParts.Resources,
            navOptions,
            captureSpec,
            captureTimingOptions,
            CancellationToken.None);

        // Assert
        Assert.Equal(CaptureStatus.UrlChangedBeforeCompletion, result.CaptureStatus);
    }

    [Fact]
    public async Task NavigateAndCaptureAsync_PageClosedWhileWaitingForCaptureCompletion_ThrowsOperationCanceledExceptionWithExpectedMessage()
    {
        // Arrange
        var Url = new Uri("https://example.com/app?id=666");
        var navOptions = new NavigationOptions(Url, null, null);
        var captureTimingOptions = new CaptureTimingOptions(null, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(1));

        var mockResilience = new Mock<IResiliencePolicyFactory>();
        var mockBrowser = new Mock<IBrowser>();
        var mockBrowserContext = new Mock<IBrowserContext>();
        var mockPage = new Mock<IPage>();
        var mockResponse = new Mock<IResponse>();

        EventHandler<IPage>? closeHandler = null;
        var isClosed = false;
        var didRaiseClose = false;

        mockResilience.Setup(x => x.BuildResiliencePolicy<PageCaptureResult>(Url, null, It.IsAny<CancellationToken>()))
            .Returns(Policy.WrapAsync(Policy.NoOpAsync<PageCaptureResult>(), Policy.NoOpAsync<PageCaptureResult>()));
        mockBrowserContext.Setup(x => x.NewPageAsync()).ReturnsAsync(mockPage.Object);
        mockPage.Setup(x => x.GotoAsync(Url.ToString(), It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(mockResponse.Object);
        mockResponse.Setup(x => x.Status).Returns(200);
        mockPage.SetupAdd(x => x.Close += It.IsAny<EventHandler<IPage>>())
            .Callback<EventHandler<IPage>>(handler => closeHandler = handler);
        mockPage.SetupRemove(x => x.Close -= It.IsAny<EventHandler<IPage>>())
            .Callback<EventHandler<IPage>>(handler =>
            {
                if (closeHandler == handler)
                    closeHandler = null;
            });
        mockPage.SetupGet(x => x.IsClosed).Returns(() => isClosed);
        mockPage.SetupGet(x => x.Url).Returns(() =>
        {
            if (!didRaiseClose)
            {
                didRaiseClose = true;
                isClosed = true;
                closeHandler?.Invoke(mockPage.Object, mockPage.Object);
            }

            return Url.ToString();
        });

        var captureSpec = new CaptureSpec(
            _ => true,
            (_, _) => Task.FromResult<CapturedResource?>(null),
            shouldCompleteCapture: (_, _, _) => false);
        var session = CreateSession(mockResilience, mockBrowser, mockBrowserContext);

        // Act
        var ex = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            session.NavigateAndCaptureResultAsync(PageCaptureParts.Resources, navOptions, captureSpec, captureTimingOptions, CancellationToken.None));

        // Assert
        Assert.Equal("Browser/page was closed while waiting for capture-completion.", ex.Message);
    }

    [Fact]
    public async Task NavigateAndCaptureAsync_TargetClosedWhileReadingUrlDuringCaptureCompletion_ThrowsOperationCanceledExceptionWithExpectedMessage()
    {
        // Arrange
        var Url = new Uri("https://example.com/app?id=666");
        var navOptions = new NavigationOptions(Url, null, null);
        var captureTimingOptions = new CaptureTimingOptions(null, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(1));

        var mockResilience = new Mock<IResiliencePolicyFactory>();
        var mockBrowser = new Mock<IBrowser>();
        var mockBrowserContext = new Mock<IBrowserContext>();
        var mockPage = new Mock<IPage>();
        var mockResponse = new Mock<IResponse>();

        mockResilience.Setup(x => x.BuildResiliencePolicy<PageCaptureResult>(Url, null, It.IsAny<CancellationToken>()))
            .Returns(Policy.WrapAsync(Policy.NoOpAsync<PageCaptureResult>(), Policy.NoOpAsync<PageCaptureResult>()));
        mockBrowserContext.Setup(x => x.NewPageAsync()).ReturnsAsync(mockPage.Object);
        mockPage.Setup(x => x.GotoAsync(Url.ToString(), It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(mockResponse.Object);
        mockResponse.Setup(x => x.Status).Returns(200);
        mockPage.SetupGet(x => x.IsClosed).Returns(false);
        mockPage.SetupGet(x => x.Url).Throws(new PlaywrightException("Target page, context or browser has been closed"));

        var captureSpec = new CaptureSpec(
            _ => true,
            (_, _) => Task.FromResult<CapturedResource?>(null),
            shouldCompleteCapture: (_, _, _) => false);
        var session = CreateSession(mockResilience, mockBrowser, mockBrowserContext);

        // Act
        var ex = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            session.NavigateAndCaptureResultAsync(PageCaptureParts.Resources, navOptions, captureSpec, captureTimingOptions, CancellationToken.None));

        // Assert
        Assert.Equal("Browser/page was closed while waiting for capture-completion.", ex.Message);
        Assert.IsType<PlaywrightException>(ex.InnerException);
    }

    [Fact]
    public async Task NavigateAndCaptureAsync_TargetClosed_ThrowsOperationCanceledException()
    {
        // Arrange
        var Url = new Uri("https://example.com/app?id=666");
        var navOptions = new NavigationOptions(Url, null, null);
        var timingOptions = BuildCaptureTimingOptions(null);

        var mockResilience = new Mock<IResiliencePolicyFactory>();
        var mockBrowser = new Mock<IBrowser>();
        var mockBrowserContext = new Mock<IBrowserContext>();
        var mockPage = new Mock<IPage>();

        mockResilience.Setup(x => x.BuildResiliencePolicy<PageCaptureResult>(Url, null, It.IsAny<CancellationToken>()))
            .Returns(Policy.WrapAsync(Policy.NoOpAsync<PageCaptureResult>(), Policy.NoOpAsync<PageCaptureResult>()));
        mockBrowserContext.Setup(x => x.NewPageAsync()).ReturnsAsync(mockPage.Object);
        mockPage.Setup(x => x.GotoAsync(Url.ToString(), It.IsAny<PageGotoOptions>()))
            .ThrowsAsync(new PlaywrightException("Target page, context or browser has been closed"));

        var captureSpec = new CaptureSpec(
            _ => true,
            (_, _) => Task.FromResult<CapturedResource?>(null),
            shouldCompleteCapture: null);
        var session = CreateSession(mockResilience, mockBrowser, mockBrowserContext);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            session.NavigateAndCaptureResultAsync(PageCaptureParts.Resources, navOptions, captureSpec, timingOptions, CancellationToken.None));
    }

    [Fact]
    public async Task NavigateAndCaptureAsync_RewritesRouteResponse_WhenRewriterReturnsTrue()
    {
        // Arrange
        var Url = new Uri("https://example.com/app?id=666");
        var navOptions = new NavigationOptions(Url, null, null);
        var timingOptions = BuildCaptureTimingOptions(null);
        var rewrittenBody = "rewritten";
        RouteFulfillOptions? fulfillOptions = null;

        var mockResilience = new Mock<IResiliencePolicyFactory>();
        var mockBrowser = new Mock<IBrowser>();
        var mockBrowserContext = new Mock<IBrowserContext>();
        var mockPage = new Mock<IPage>();
        var mockResponse = new Mock<IResponse>();
        var mockRoute = new Mock<IRoute>();
        var mockRouteRequest = new Mock<IRequest>();
        var mockRouteResponse = new Mock<IAPIResponse>();

        mockResilience.Setup(x => x.BuildResiliencePolicy<PageCaptureResult>(Url, null, It.IsAny<CancellationToken>()))
            .Returns(Policy.WrapAsync(Policy.NoOpAsync<PageCaptureResult>(), Policy.NoOpAsync<PageCaptureResult>()));
        mockBrowserContext.Setup(x => x.NewPageAsync()).ReturnsAsync(mockPage.Object);
        mockPage.Setup(x => x.GotoAsync(Url.ToString(), It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(mockResponse.Object);
        mockResponse.Setup(x => x.Status).Returns(200);
        mockPage.Setup(p => p.WaitForLoadStateAsync(LoadState.NetworkIdle, It.IsAny<PageWaitForLoadStateOptions>()))
            .Returns(Task.CompletedTask);

        mockRoute.SetupGet(x => x.Request).Returns(mockRouteRequest.Object);
        mockRoute.Setup(x => x.FetchAsync(It.IsAny<RouteFetchOptions>())).ReturnsAsync(mockRouteResponse.Object);
        mockRoute.Setup(x => x.FulfillAsync(It.IsAny<RouteFulfillOptions>()))
            .Callback<RouteFulfillOptions>(options => fulfillOptions = options)
            .Returns(Task.CompletedTask);
        mockRoute.Setup(x => x.ContinueAsync(It.IsAny<RouteContinueOptions>())).Returns(Task.CompletedTask);

        mockRouteRequest.Setup(x => x.Url).Returns("https://example.com/resource.json");
        mockRouteResponse.SetupGet(x => x.Headers)
            .Returns(new Dictionary<string, string> { ["content-type"] = "application/json" });
        mockRouteResponse.Setup(x => x.TextAsync()).ReturnsAsync("original");
        mockRouteResponse.Setup(x => x.Status).Returns(200);

        mockPage.Setup(p => p.RouteAsync("**/*", It.IsAny<Func<IRoute, Task>>(), It.IsAny<PageRouteOptions>()))
            .Callback<string, Func<IRoute, Task>, PageRouteOptions>((_, handler, _) => handler(mockRoute.Object).GetAwaiter().GetResult())
            .Returns(Task.FromResult(Mock.Of<IAsyncDisposable>()));
        mockPage.Setup(p => p.UnrouteAsync("**/*", It.IsAny<Func<IRoute, Task>>()))
            .Returns(Task.CompletedTask);

        var captureSpec = new CaptureSpec(
            _ => true,
            (_, _) => Task.FromResult<CapturedResource?>(null),
            null);
        var rewriteSpec = new RewriteSpec(
            _ => true,
            (_, _) => Task.FromResult<ResponseRewriteResult>(
                new ResponseRewriteResult(true, rewrittenBody, "text/plain")));
        var session = CreateSession(mockResilience, mockBrowser, mockBrowserContext);

        // Act
        await session.NavigateAndCaptureResultAsync(
            PageCaptureParts.Resources,
            navOptions,
            captureSpec,
            rewriteSpec,
            timingOptions,
            CancellationToken.None);

        // Assert
        Assert.NotNull(fulfillOptions);
        Assert.Equal(rewrittenBody, fulfillOptions!.Body);
        Assert.Equal("text/plain", fulfillOptions.ContentType);
        mockPage.Verify(p => p.UnrouteAsync("**/*", It.IsAny<Func<IRoute, Task>>()), Times.Once);
    }

    [Fact]
    public async Task NavigateAndCaptureAsync_ResponseHtml_UsesRewrittenNavigationDocumentHtml()
    {
        // Arrange
        var Url = new Uri("https://example.com/app?id=666");
        var navOptions = new NavigationOptions(Url, null, null);
        var timingOptions = BuildCaptureTimingOptions(null);
        const string OriginalBody = "<html>original</html>";
        const string RewrittenBody = "<html>rewritten</html>";

        var mockResilience = new Mock<IResiliencePolicyFactory>();
        var mockBrowser = new Mock<IBrowser>();
        var mockBrowserContext = new Mock<IBrowserContext>();
        var mockPage = new Mock<IPage>();
        var mockResponse = new Mock<IResponse>();
        var mockRoute = new Mock<IRoute>();
        var mockRouteRequest = new Mock<IRequest>();
        var mockRouteResponse = new Mock<IAPIResponse>();

        mockResilience.Setup(x => x.BuildResiliencePolicy<PageCaptureResult>(Url, null, It.IsAny<CancellationToken>()))
            .Returns(Policy.WrapAsync(Policy.NoOpAsync<PageCaptureResult>(), Policy.NoOpAsync<PageCaptureResult>()));
        mockBrowserContext.Setup(x => x.NewPageAsync()).ReturnsAsync(mockPage.Object);
        mockPage.Setup(x => x.GotoAsync(Url.ToString(), It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(mockResponse.Object);
        mockResponse.Setup(x => x.Status).Returns(200);
        mockResponse.Setup(x => x.TextAsync()).ReturnsAsync(OriginalBody);

        mockRoute.SetupGet(x => x.Request).Returns(mockRouteRequest.Object);
        mockRoute.Setup(x => x.FetchAsync(It.IsAny<RouteFetchOptions>())).ReturnsAsync(mockRouteResponse.Object);
        mockRoute.Setup(x => x.FulfillAsync(It.IsAny<RouteFulfillOptions>())).Returns(Task.CompletedTask);
        mockRoute.Setup(x => x.ContinueAsync(It.IsAny<RouteContinueOptions>())).Returns(Task.CompletedTask);

        mockRouteRequest.Setup(x => x.Url).Returns(Url.ToString());
        mockRouteRequest.Setup(x => x.IsNavigationRequest).Returns(true);
        mockRouteResponse.SetupGet(x => x.Headers)
            .Returns(new Dictionary<string, string> { ["content-type"] = "text/html" });
        mockRouteResponse.Setup(x => x.TextAsync()).ReturnsAsync(OriginalBody);
        mockRouteResponse.Setup(x => x.Status).Returns(200);

        mockPage.Setup(p => p.RouteAsync("**/*", It.IsAny<Func<IRoute, Task>>(), It.IsAny<PageRouteOptions>()))
            .Callback<string, Func<IRoute, Task>, PageRouteOptions>((_, handler, _) => handler(mockRoute.Object).GetAwaiter().GetResult())
            .Returns(Task.FromResult(Mock.Of<IAsyncDisposable>()));
        mockPage.Setup(p => p.UnrouteAsync("**/*", It.IsAny<Func<IRoute, Task>>()))
            .Returns(Task.CompletedTask);

        var captureSpec = new CaptureSpec(
            _ => false,
            (_, _) => Task.FromResult<CapturedResource?>(null),
            null);
        var rewriteSpec = new RewriteSpec(
            _ => true,
            (_, _) => Task.FromResult<ResponseRewriteResult>(
                new ResponseRewriteResult(true, RewrittenBody, "text/html")));
        var session = CreateSession(mockResilience, mockBrowser, mockBrowserContext);

        // Act
        var result = await session.NavigateAndCaptureResultAsync(
            PageCaptureParts.ResponseHtml,
            navOptions,
            captureSpec,
            rewriteSpec,
            timingOptions,
            CancellationToken.None);

        // Assert
        Assert.Equal(RewrittenBody, result.ResponseHtml);
    }

    private static ResilientBrowserSession CreateSession(
        Mock<IResiliencePolicyFactory> resilienceFactoryMock,
        Mock<IBrowser> browserMock,
        Mock<IBrowserContext> contextMock)
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<PlaywrightBrowserSession>.Instance;
        var pageCaptureLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<PlaywrightPageCaptureService>.Instance;
        var pageCaptureService = new PlaywrightPageCaptureService(pageCaptureLogger);
        var inner = new PlaywrightBrowserSession(logger, browserMock.Object, contextMock.Object, pageCaptureService);
        return new ResilientBrowserSession(inner, resilienceFactoryMock.Object);
    }

    private static CaptureTimingOptions BuildCaptureTimingOptions(TimeSpan? networkIdleTimeout) => new(networkIdleTimeout, pollInterval: CaptureTimingOptions.DefaultPollInterval);
}
