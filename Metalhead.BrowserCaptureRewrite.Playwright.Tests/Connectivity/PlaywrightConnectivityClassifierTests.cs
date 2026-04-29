using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using Microsoft.Playwright;

using Metalhead.BrowserCaptureRewrite.Abstractions.Connectivity;
using Metalhead.BrowserCaptureRewrite.Playwright.Connectivity;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Tests.Connectivity;

public class PlaywrightConnectivityClassifierTests
{
    private readonly PlaywrightConnectivityClassifier _classifier = new();

    [Fact]
    public void ClassifyException_TimeoutException_ReturnsUnknownConnectivityRelated()
    {
        var ex = new TimeoutException();
        var result = _classifier.ClassifyException(ex, CancellationToken.None);
        Assert.True(result.IsConnectivityRelated);
        Assert.Equal(ConnectivityScope.Unknown, result.Scope);
    }

    [Fact]
    public void ClassifyException_HttpRequestExceptionWithStatusCode_ReturnsRemoteSiteConnectivityRelated()
    {
        var ex = new HttpRequestException("fail", null, HttpStatusCode.BadGateway);
        var result = _classifier.ClassifyException(ex, CancellationToken.None);
        Assert.True(result.IsConnectivityRelated);
        Assert.Equal(ConnectivityScope.RemoteSite, result.Scope);
    }

    [Fact]
    public void ClassifyException_HttpRequestExceptionWithSocketException_NetworkDown_ReturnsLocalEnvironment()
    {
        var socketEx = new SocketException((int)SocketError.NetworkDown);
        var ex = new HttpRequestException("fail", socketEx);
        var result = _classifier.ClassifyException(ex, CancellationToken.None);
        Assert.True(result.IsConnectivityRelated);
        Assert.Equal(ConnectivityScope.LocalEnvironment, result.Scope);
    }

    [Fact]
    public void ClassifyException_HttpRequestExceptionWithSocketException_HostNotFound_ReturnsLocalEnvironment()
    {
        var socketEx = new SocketException((int)SocketError.HostNotFound);
        var ex = new HttpRequestException("fail", socketEx);
        var result = _classifier.ClassifyException(ex, CancellationToken.None);
        Assert.True(result.IsConnectivityRelated);
        Assert.Equal(ConnectivityScope.HostnameResolution, result.Scope);
    }

    [Fact]
    public void ClassifyException_HttpRequestExceptionWithSocketException_ConnectionRefused_ReturnsRemoteSite()
    {
        var socketEx = new SocketException((int)SocketError.ConnectionRefused);
        var ex = new HttpRequestException("fail", socketEx);
        var result = _classifier.ClassifyException(ex, CancellationToken.None);
        Assert.True(result.IsConnectivityRelated);
        Assert.Equal(ConnectivityScope.RemoteSite, result.Scope);
    }

    [Fact]
    public void ClassifyException_HttpRequestExceptionWithAuthenticationException_ReturnsRemoteSite()
    {
        var authEx = new AuthenticationException();
        var ex = new HttpRequestException("fail", authEx);
        var result = _classifier.ClassifyException(ex, CancellationToken.None);
        Assert.True(result.IsConnectivityRelated);
        Assert.Equal(ConnectivityScope.RemoteSite, result.Scope);
    }

    [Fact]
    public void ClassifyException_HttpRequestExceptionWithNoInner_ReturnsNotConnectivityRelated()
    {
        var ex = new HttpRequestException("fail");
        var result = _classifier.ClassifyException(ex, CancellationToken.None);
        Assert.False(result.IsConnectivityRelated);
        Assert.Equal(ConnectivityScope.Unknown, result.Scope);
    }

    [Theory]
    [InlineData("net::ERR_INTERNET_DISCONNECTED")]
    [InlineData("net::ERR_NETWORK_CHANGED")]
    [InlineData("net::ERR_PROXY_CONNECTION_FAILED")]
    public void ClassifyException_PlaywrightException_LocalHints_ReturnsLocalEnvironment(string message)
    {
        var ex = new PlaywrightException(message);
        var result = _classifier.ClassifyException(ex, CancellationToken.None);
        Assert.True(result.IsConnectivityRelated);
        Assert.Equal(ConnectivityScope.LocalEnvironment, result.Scope);
    }

    [Theory]
    [InlineData("net::ERR_CONNECTION_REFUSED")]
    [InlineData("net::ERR_ADDRESS_UNREACHABLE")]
    [InlineData("NS_ERROR_CONNECTION_REFUSED")]
    [InlineData("NS_ERROR_NET_RESET")]
    public void ClassifyException_PlaywrightException_RemoteHints_ReturnsRemoteSite(string message)
    {
        var ex = new PlaywrightException(message);
        var result = _classifier.ClassifyException(ex, CancellationToken.None);
        Assert.True(result.IsConnectivityRelated);
        Assert.Equal(ConnectivityScope.RemoteSite, result.Scope);
    }

    [Theory]
    [InlineData("net::ERR_NAME_NOT_RESOLVED")]
    [InlineData("NS_ERROR_UNKNOWN_HOST")]
    [InlineData("Could not resolve hostname")]
    public void ClassifyException_PlaywrightException_NetworkHints_ReturnsHostnameResolution(string message)
    {
        var ex = new PlaywrightException(message);
        var result = _classifier.ClassifyException(ex, CancellationToken.None);
        Assert.True(result.IsConnectivityRelated);
        Assert.Equal(ConnectivityScope.HostnameResolution, result.Scope);
    }

    [Fact]
    public void ClassifyException_PlaywrightException_UnknownMessage_ReturnsUnknownConnectivityRelated()
    {
        var ex = new PlaywrightException("some unrelated error");
        var result = _classifier.ClassifyException(ex, CancellationToken.None);
        Assert.True(result.IsConnectivityRelated);
        Assert.Equal(ConnectivityScope.Unknown, result.Scope);
    }

    [Fact]
    public void ClassifyException_NotConnectivityRelatedException_ReturnsNotConnectivityRelated()
    {
        var ex = new InvalidOperationException("not connectivity");
        var result = _classifier.ClassifyException(ex, CancellationToken.None);
        Assert.False(result.IsConnectivityRelated);
        Assert.Equal(ConnectivityScope.Unknown, result.Scope);
    }
}
