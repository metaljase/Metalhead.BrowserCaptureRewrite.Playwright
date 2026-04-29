using Microsoft.Playwright;
using Moq;

using Metalhead.BrowserCaptureRewrite.Playwright.Sessions;

namespace Metalhead.BrowserCaptureRewrite.Playwright.Tests.Sessions;

public class PlaywrightSessionHelperTests
{
    [Fact]
    public async Task DisposeAsync_WhenBrowserCloseExceedsTimeout_WaitsForCloseCompletion()
    {
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var closeTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        context.Setup(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions?>())).Returns(Task.CompletedTask);
        browser.Setup(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>())).Returns(closeTcs.Task);

        var disposeTask = PlaywrightSessionHelper.DisposeAsync(
            context.Object,
            browser.Object,
            browserCloseTimeout: TimeSpan.FromMilliseconds(1));

        await Task.Delay(25, CancellationToken.None);
        Assert.False(disposeTask.IsCompleted);

        closeTcs.SetResult();
        await disposeTask;

        context.Verify(c => c.CloseAsync(It.IsAny<BrowserContextCloseOptions?>()), Times.Once);
        browser.Verify(b => b.CloseAsync(It.IsAny<BrowserCloseOptions?>()), Times.Once);
    }
}
