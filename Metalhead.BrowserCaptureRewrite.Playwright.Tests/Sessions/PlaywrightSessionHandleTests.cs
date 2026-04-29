using Microsoft.Playwright;
using Moq;

using Metalhead.BrowserCaptureRewrite.Playwright.Sessions;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Tests.Sessions;

public class PlaywrightSessionHandleTests
{
    [Fact]
    public async Task DisposeAsync_ClosesContextAndBrowserOnce()
    {
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var handle = new PlaywrightSessionHandle(browser.Object, context.Object);

        await handle.DisposeAsync();
        await handle.DisposeAsync();

        context.Verify(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions?>()), Times.Once);
        browser.Verify(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>()), Times.Once);
    }
}
