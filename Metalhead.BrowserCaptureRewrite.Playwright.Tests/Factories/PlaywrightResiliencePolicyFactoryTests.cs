using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Moq;
using Polly.Wrap;

using Metalhead.BrowserCaptureRewrite.Abstractions.Connectivity;
using Metalhead.BrowserCaptureRewrite.Abstractions.Exceptions;
using Metalhead.BrowserCaptureRewrite.Abstractions.Resilience;
using Metalhead.BrowserCaptureRewrite.Playwright.Factories;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Tests.Factories;

public class PlaywrightResiliencePolicyFactoryTests
{
    private static Uri TestUrl => new("https://example.com");
    private static TimeSpan TestTimeout => TimeSpan.FromSeconds(1);

    private static readonly int[] s_transportRetryDelays = [0, 0];
    private static readonly int[] s_timeoutRetryDelays = [0, 0];

    [Fact]
    public void BuildResiliencePolicy_ReturnsPolicyWrap()
    {
        var logger = new Mock<ILogger<ResiliencePolicyBuilder>>();
        var classifier = new Mock<IConnectivityClassifier>();
        var probe = new Mock<IConnectivityProbe>();
        var resiliencePolicyBuilder = new ResiliencePolicyBuilder(logger.Object, classifier.Object, probe.Object);
        var factory = new PlaywrightResiliencePolicyFactory(resiliencePolicyBuilder);
        var policy = factory.BuildResiliencePolicy<object>(TestUrl, TestTimeout, CancellationToken.None);
        Assert.NotNull(policy);
        Assert.IsType<AsyncPolicyWrap<object>>(policy);
    }

    [Fact]
    public void BuildResiliencePolicy_ThrowsOnNullUrl()
    {
        var resiliencePolicyBuilder = new Mock<IResiliencePolicyBuilder>();
        var factory = new PlaywrightResiliencePolicyFactory(resiliencePolicyBuilder.Object);
        Assert.Throws<ArgumentNullException>(() => factory.BuildResiliencePolicy<object>(null!, TestTimeout, CancellationToken.None));
    }

    [Fact]
    public async Task TransportRetryPolicy_RetriesOnHttpRequestException()
    {
        var logger = new Mock<ILogger<ResiliencePolicyBuilder>>();
        var classifier = new Mock<IConnectivityClassifier>();
        classifier.Setup(c => c.ClassifyException(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
            .Returns(ConnectivityClassificationResult.NotConnectivityRelated);
        var probe = new Mock<IConnectivityProbe>();
        probe.Setup(p => p.HasGeneralConnectivityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var resiliencePolicyBuilder = new ResiliencePolicyBuilder(logger.Object, classifier.Object, probe.Object);
        var factory = new PlaywrightResiliencePolicyFactory(resiliencePolicyBuilder, new ResiliencePolicyOptions(
            transportRetryDelays: s_transportRetryDelays,
            timeoutRetryDelays: []
        ));

        var policy = factory.BuildResiliencePolicy<object>(TestUrl, TestTimeout, CancellationToken.None);

        int attempts = 0;

        await Assert.ThrowsAsync<HttpRequestException>(() => policy.ExecuteAsync(ct =>
        {
            attempts++;
            return Task.FromException<object>(new HttpRequestException());
        }, CancellationToken.None));

        Assert.Equal(3, attempts); // 1 initial + 2 retries
    }

    [Fact]
    public async Task TimeoutRetryPolicy_RetriesOnTimeoutException()
    {
        var logger = new Mock<ILogger<ResiliencePolicyBuilder>>();
        var classifier = new Mock<IConnectivityClassifier>();
        classifier.Setup(c => c.ClassifyException(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
            .Returns(ConnectivityClassificationResult.NotConnectivityRelated);
        var probe = new Mock<IConnectivityProbe>();
        probe.Setup(p => p.HasGeneralConnectivityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var resiliencePolicyBuilder = new ResiliencePolicyBuilder(logger.Object, classifier.Object, probe.Object);
        var factory = new PlaywrightResiliencePolicyFactory(resiliencePolicyBuilder, new ResiliencePolicyOptions(
            transportRetryDelays: [],
            timeoutRetryDelays: s_timeoutRetryDelays));

        var policy = factory.BuildResiliencePolicy<object>(TestUrl, TestTimeout, CancellationToken.None);

        int attempts = 0;

        await Assert.ThrowsAsync<TimeoutException>(() => policy.ExecuteAsync(ct =>
        {
            attempts++;
            return Task.FromException<object>(new TimeoutException());
        }, CancellationToken.None));

        Assert.Equal(3, attempts); // 1 initial + 2 retries
    }

    [Fact]
    public async Task TransportRetryPolicy_RetriesOnPlaywrightException()
    {
        var logger = new Mock<ILogger<ResiliencePolicyBuilder>>();
        var classifier = new Mock<IConnectivityClassifier>();
        classifier.Setup(c => c.ClassifyException(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
            .Returns(ConnectivityClassificationResult.NotConnectivityRelated);
        var probe = new Mock<IConnectivityProbe>();
        probe.Setup(p => p.HasGeneralConnectivityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var resiliencePolicyBuilder = new ResiliencePolicyBuilder(logger.Object, classifier.Object, probe.Object);
        var factory = new PlaywrightResiliencePolicyFactory(resiliencePolicyBuilder, new ResiliencePolicyOptions(
            transportRetryDelays: s_transportRetryDelays,
            timeoutRetryDelays: []
        ));

        var policy = factory.BuildResiliencePolicy<object>(TestUrl, TestTimeout, CancellationToken.None);

        int attempts = 0;

        await Assert.ThrowsAsync<PlaywrightException>(() => policy.ExecuteAsync(ct =>
        {
            attempts++;
            return Task.FromException<object>(new PlaywrightException("fail"));
        }, CancellationToken.None));

        Assert.Equal(3, attempts); // 1 initial + 2 retries
    }

    [Fact]
    public async Task BuildResiliencePolicy_UsesFallback_ToSurfaceConnectivityException()
    {
        var logger = new Mock<ILogger<ResiliencePolicyBuilder>>();
        var classifier = new Mock<IConnectivityClassifier>();
        classifier.Setup(c => c.ClassifyException(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
            .Returns(ConnectivityClassificationResult.ConnectivityRelated(ConnectivityScope.LocalEnvironment));
        var probe = new Mock<IConnectivityProbe>();

        var resiliencePolicyBuilder = new ResiliencePolicyBuilder(logger.Object, classifier.Object, probe.Object);
        var factory = new PlaywrightResiliencePolicyFactory(resiliencePolicyBuilder,
            new ResiliencePolicyOptions(transportRetryDelays: [], timeoutRetryDelays: Array.Empty<int>()));

        var policy = factory.BuildResiliencePolicy<object>(TestUrl, TestTimeout, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConnectivityException>(() =>
            policy.ExecuteAsync(() => Task.FromException<object>(new PlaywrightException("fail"))));
        Assert.Equal(ConnectivityScope.LocalEnvironment, ex.Scope);
    }
}
