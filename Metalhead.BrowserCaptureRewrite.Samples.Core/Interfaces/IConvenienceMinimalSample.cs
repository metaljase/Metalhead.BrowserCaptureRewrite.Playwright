namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;

public interface IConvenienceMinimalSample
{
    Task CaptureResponsesAndRenderedHtmlAsync(CancellationToken cancellationToken = default);
}