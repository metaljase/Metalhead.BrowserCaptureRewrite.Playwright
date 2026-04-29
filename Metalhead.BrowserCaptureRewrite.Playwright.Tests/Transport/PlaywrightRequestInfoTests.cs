using Microsoft.Playwright;
using Moq;

using Metalhead.BrowserCaptureRewrite.Playwright.Transport;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Tests.Transport;

public class PlaywrightRequestInfoTests
{
    [Fact]
    public void Properties_AreForwardedFromRequest()
    {
        var url = "https://example.com";
        var method = "POST";
        var headers = new Dictionary<string, string> { ["X-Test"] = "1" };
        var mockRequest = new Mock<IRequest>();
        mockRequest.Setup(r => r.Url).Returns(url);
        mockRequest.Setup(r => r.Method).Returns(method);
        mockRequest.Setup(r => r.Headers).Returns(headers);

        var info = new PlaywrightRequestInfo(mockRequest.Object);
        Assert.Equal(url, info.Url);
        Assert.Equal(method, info.Method);
        Assert.Equal(headers, info.Headers);
    }

    [Fact]
    public void Headers_EmptyDictionary_ReturnsEmpty()
    {
        var mockRequest = new Mock<IRequest>();
        mockRequest.Setup(r => r.Url).Returns("u");
        mockRequest.Setup(r => r.Method).Returns("GET");
        mockRequest.Setup(r => r.Headers).Returns([]);
        var info = new PlaywrightRequestInfo(mockRequest.Object);
        Assert.Empty(info.Headers);
    }
}
