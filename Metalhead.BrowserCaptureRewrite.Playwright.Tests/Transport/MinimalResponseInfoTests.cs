using System.Text;

using Metalhead.BrowserCaptureRewrite.Playwright.Transport;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Tests.Transport;

public class MinimalResponseInfoTests
{
    [Fact]
    public async Task Properties_AreSetCorrectly()
    {
        var body = "test-body";
        int? status = 200;
        var headers = new Dictionary<string, string> { ["Content-Type"] = "text/plain" };
        var info = new MinimalResponseInfo(body, status, headers);

        Assert.Equal(status, info.StatusCode);
        Assert.Equal(headers, info.Headers);
        Assert.Equal(body, await info.GetBodyAsStringAsync());
        Assert.Equal(Encoding.UTF8.GetBytes(body), await info.GetBodyAsByteArrayAsync());
    }

    [Fact]
    public async Task NullStatusAndEmptyHeaders_AreHandled()
    {
        var body = "abc";
        int? status = null;
        var headers = new Dictionary<string, string>();
        var info = new MinimalResponseInfo(body, status, headers);

        Assert.Null(info.StatusCode);
        Assert.Empty(info.Headers);
        Assert.Equal(body, await info.GetBodyAsStringAsync());
        Assert.Equal(Encoding.UTF8.GetBytes(body), await info.GetBodyAsByteArrayAsync());
    }

    [Fact]
    public async Task GetBodyAsByteArrayAsync_EmptyBody_ReturnsEmptyArray()
    {
        var info = new MinimalResponseInfo(string.Empty, 404, new Dictionary<string, string>());
        var bytes = await info.GetBodyAsByteArrayAsync();
        Assert.NotNull(bytes);
        Assert.Empty(bytes);
    }
}
