namespace Metalhead.BrowserCaptureRewrite.Samples.Core.Interfaces;

public interface IExtensionMinimalSample
{
    Task CaptureResponsesAsync(CancellationToken ct = default);
}