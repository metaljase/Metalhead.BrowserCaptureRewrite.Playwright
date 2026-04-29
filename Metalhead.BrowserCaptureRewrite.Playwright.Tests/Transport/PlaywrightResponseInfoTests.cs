using System.Text;
using Microsoft.Playwright;
using Moq;

using Metalhead.BrowserCaptureRewrite.Playwright.Transport;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Tests.Transport;

public class PlaywrightResponseInfoTests
{
    [Fact]
    public void Properties_AreForwardedFromResponse()
    {
        var mockResponse = new Mock<IResponse>();
        mockResponse.Setup(r => r.Status).Returns(201);
        var headers = new Dictionary<string, string> { ["X-Header"] = "abc" };
        mockResponse.Setup(r => r.Headers).Returns(headers);
        var info = new PlaywrightResponseInfo(mockResponse.Object);
        Assert.Equal(201, info.StatusCode);
        Assert.Equal(headers, info.Headers);
    }

    [Fact]
    public async Task GetBodyAsStringAsync_ReturnsTextAsyncResult()
    {
        var mockResponse = new Mock<IResponse>();
        mockResponse.Setup(r => r.TextAsync()).ReturnsAsync("body-text");
        var info = new PlaywrightResponseInfo(mockResponse.Object);
        var result = await info.GetBodyAsStringAsync();
        Assert.Equal("body-text", result);
    }

    [Fact]
    public async Task GetBodyAsByteArrayAsync_ReturnsBodyAsyncResult()
    {
        var bytes = Encoding.UTF8.GetBytes("abc123");
        var mockResponse = new Mock<IResponse>();
        mockResponse.Setup(r => r.BodyAsync()).ReturnsAsync(bytes);
        var info = new PlaywrightResponseInfo(mockResponse.Object);
        var result = await info.GetBodyAsByteArrayAsync();
        Assert.Equal(bytes, result);
    }

    [Fact]
    public async Task GetBodyAsStringAsync_RespectsCancellationToken()
    {
        var mockResponse = new Mock<IResponse>();
        mockResponse.Setup(r => r.TextAsync()).Returns(async () => { await Task.Delay(50); return "cancel"; });
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var info = new PlaywrightResponseInfo(mockResponse.Object, cts.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => info.GetBodyAsStringAsync());
    }

    [Fact]
    public async Task GetBodyAsByteArrayAsync_RespectsCancellationToken()
    {
        var mockResponse = new Mock<IResponse>();
        mockResponse.Setup(r => r.BodyAsync()).Returns(async () => { await Task.Delay(50); return [1, 2, 3]; });
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var info = new PlaywrightResponseInfo(mockResponse.Object, cts.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => info.GetBodyAsByteArrayAsync());
    }
}
