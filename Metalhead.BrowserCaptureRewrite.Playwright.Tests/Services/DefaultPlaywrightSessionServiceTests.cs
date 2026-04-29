using Moq;

using Metalhead.BrowserCaptureRewrite.Abstractions.Engine;
using Metalhead.BrowserCaptureRewrite.Abstractions.Enums;
using Metalhead.BrowserCaptureRewrite.Abstractions.Exceptions;
using Metalhead.BrowserCaptureRewrite.Playwright.Factories;
using Metalhead.BrowserCaptureRewrite.Playwright.Services;
using Metalhead.BrowserCaptureRewrite.Playwright.Sessions;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Tests.Services;

public class DefaultPlaywrightSessionServiceTests
{
    private static readonly BrowserOptions s_browserOptions = new();

    [Fact]
    public async Task CreatePlaywrightSessionOrThrowAsync_WithoutSignIn_UsesStandardFactory()
    {
        var sessionHandle = Mock.Of<IPlaywrightSessionHandle>();
        var sessionFactory = new Mock<IPlaywrightSessionHandleFactory>();
        var signInSessionFactory = new Mock<IPlaywrightSignInSessionHandleFactory>();
        sessionFactory.Setup(f => f.CreateSessionHandleAsync(It.IsAny<SessionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionHandle);

        var service = new DefaultPlaywrightSessionService(s_browserOptions, signInSessionFactory.Object, sessionFactory.Object);

        var result = await service.CreatePlaywrightSessionOrThrowAsync(null, null, null, null, CancellationToken.None);

        Assert.Same(sessionHandle, result);
        sessionFactory.Verify(f => f.CreateSessionHandleAsync(It.Is<SessionOptions>(o => o.BrowserOptions == s_browserOptions), It.IsAny<CancellationToken>()), Times.Once);
        signInSessionFactory.Verify(f => f.CreateSignInSessionHandleAsync(It.IsAny<Uri>(), It.IsAny<SessionOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePlaywrightSessionOrThrowAsync_WithSignIn_UsesSignInFactory()
    {
        var signInUrl = new Uri("https://example.com/sign-in");
        var signedInUrl = new Uri("https://example.com/app");
        var sessionHandle = Mock.Of<IPlaywrightSessionHandle>();
        var sessionFactory = new Mock<IPlaywrightSessionHandleFactory>();
        var signInSessionFactory = new Mock<IPlaywrightSignInSessionHandleFactory>();
        signInSessionFactory.Setup(f => f.CreateSignInSessionHandleAsync(signInUrl, It.IsAny<SessionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionHandle);

        var service = new DefaultPlaywrightSessionService(s_browserOptions, signInSessionFactory.Object, sessionFactory.Object);

        var result = await service.CreatePlaywrightSessionOrThrowAsync(signInUrl, signedInUrl, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.Same(sessionHandle, result);
        signInSessionFactory.Verify(f => f.CreateSignInSessionHandleAsync(
            signInUrl,
            It.Is<SessionOptions>(o =>
                o.BrowserOptions == s_browserOptions
                && o.AssumeSignedInWhenNavigatedToUrl == signedInUrl
                && o.AssumeSignedInAfter == TimeSpan.FromSeconds(2)
                && o.SignInPageLoadTimeout == TimeSpan.FromSeconds(10)
                && o.UseResilienceForSignIn == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePlaywrightSessionOrThrowAsync_SignInException_Rethrows()
    {
        var signInUrl = new Uri("https://example.com/sign-in");
        var expected = new SignInException(signInUrl);
        var sessionFactory = new Mock<IPlaywrightSessionHandleFactory>();
        var signInSessionFactory = new Mock<IPlaywrightSignInSessionHandleFactory>();
        signInSessionFactory.Setup(f => f.CreateSignInSessionHandleAsync(signInUrl, It.IsAny<SessionOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        var service = new DefaultPlaywrightSessionService(s_browserOptions, signInSessionFactory.Object, sessionFactory.Object);

        var ex = await Assert.ThrowsAsync<SignInException>(() => service.CreatePlaywrightSessionOrThrowAsync(signInUrl, null, null, null, CancellationToken.None));

        Assert.Same(expected, ex);
    }

    [Fact]
    public async Task CreatePlaywrightSessionOrThrowAsync_EngineMissing_WrapsException()
    {
        var expected = new BrowserAutomationEngineNotAvailableException("Run Playwright install.");
        var sessionFactory = new Mock<IPlaywrightSessionHandleFactory>();
        var signInSessionFactory = new Mock<IPlaywrightSignInSessionHandleFactory>();
        sessionFactory.Setup(f => f.CreateSessionHandleAsync(It.IsAny<SessionOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        var service = new DefaultPlaywrightSessionService(s_browserOptions, signInSessionFactory.Object, sessionFactory.Object);

        var ex = await Assert.ThrowsAsync<BrowserSessionInitializationException>(() => service.CreatePlaywrightSessionOrThrowAsync(null, null, null, null, CancellationToken.None));

        Assert.Equal(BrowserSessionInitializationFailureReason.EngineNotAvailable, ex.Reason);
        Assert.False(ex.IsSignInSession);
        Assert.Equal(expected.ResolutionHint, ex.ResolutionHint);
        Assert.Same(expected, ex.InnerException);
    }

    [Fact]
    public async Task CreatePlaywrightSessionOrThrowAsync_GeneralFailure_WrapsException()
    {
        var expected = new InvalidOperationException("boom");
        var sessionFactory = new Mock<IPlaywrightSessionHandleFactory>();
        var signInSessionFactory = new Mock<IPlaywrightSignInSessionHandleFactory>();
        sessionFactory.Setup(f => f.CreateSessionHandleAsync(It.IsAny<SessionOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        var service = new DefaultPlaywrightSessionService(s_browserOptions, signInSessionFactory.Object, sessionFactory.Object);

        var ex = await Assert.ThrowsAsync<BrowserSessionInitializationException>(() => service.CreatePlaywrightSessionOrThrowAsync(null, null, null, null, CancellationToken.None));

        Assert.Equal(BrowserSessionInitializationFailureReason.General, ex.Reason);
        Assert.False(ex.IsSignInSession);
        Assert.Same(expected, ex.InnerException);
    }
}
