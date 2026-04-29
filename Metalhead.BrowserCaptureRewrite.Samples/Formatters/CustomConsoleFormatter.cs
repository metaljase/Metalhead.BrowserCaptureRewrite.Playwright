using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Metalhead.BrowserCaptureRewrite.Samples.Formatters;

/// <summary>
/// Provides a custom console log formatter for sample applications, supporting timestamp, log level truncation, and colour customisation.
/// </summary>
/// <remarks>
/// <para>
/// Inherits from <see cref="ConsoleFormatter"/> and is intended for use with Microsoft.Extensions.Logging in sample or console applications.
/// </para>
/// <para>
/// Implements log level truncation, UTC/local timestamp formatting, and background/foreground colour customisation for warning, error, and
/// critical log levels.
/// </para>
/// <para>
/// All formatting options are configured via <see cref="CustomConsoleFormatterOptions"/>.
/// </para>
/// </remarks>
internal sealed class CustomConsoleFormatter(IOptions<CustomConsoleFormatterOptions> options) : ConsoleFormatter("Custom")
{
    private readonly CustomConsoleFormatterOptions _options = options.Value;

    /// <summary>
    /// Writes a formatted log entry to the console, applying timestamp, log level truncation, and colour customisation.
    /// </summary>
    /// <typeparam name="TState">The type of the log entry state.</typeparam>
    /// <param name="logEntry">The log entry to write.</param>
    /// <param name="scopeProvider">The scope provider, or <see langword="null"/>.</param>
    /// <param name="textWriter">The text writer to write to.</param>
    /// <remarks>
    /// <para>
    /// The log level is truncated according to <see cref="CustomConsoleFormatterOptions.LogLevelTruncationLength"/> and coloured
    /// according to log severity.
    /// </para>
    /// <para>
    /// The timestamp is formatted using <see cref="CustomConsoleFormatterOptions.TimestampFormat"/> and may be UTC or local time.
    /// </para>
    /// </remarks>
    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var logLevel = TruncateLogLevel(logEntry.LogLevel, _options.LogLevelTruncationLength).ToUpper();
        var timestamp = _options.UseUtcTimestamp
            ? DateTime.UtcNow.ToString(_options.TimestampFormat)
            : DateTime.Now.ToString(_options.TimestampFormat);
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);

        if (!string.IsNullOrEmpty(message))
        {
            Console.Write($"{timestamp} ");
            SetConsoleColor(logEntry.LogLevel, _options.BackgroudColourForWarningErrorCritical);
            Console.Write($"{logLevel}");
            Console.ResetColor();
            Console.Write(": ");
            Console.WriteLine(message);
        }
    }

    /// <summary>
    /// Truncates the log level string to the specified length, using standard abbreviations for common log levels.
    /// </summary>
    /// <param name="logLevel">The log level to truncate.</param>
    /// <param name="length">The desired truncation length (typically 3 or 4).</param>
    /// <returns>
    /// The truncated log level string.
    /// </returns>
    private static string TruncateLogLevel(LogLevel logLevel, int length)
    {
        return (logLevel, length) switch
        {
            (LogLevel.Trace, 3) => "Trc",
            (LogLevel.Trace, 4) => "Trce",
            (LogLevel.Debug, 3) => "Dbg",
            (LogLevel.Debug, 4) => "Debg",
            (LogLevel.Information, 3) => "Inf",
            (LogLevel.Information, 4) => "Info",
            (LogLevel.Warning, 3) => "Wrn",
            (LogLevel.Warning, 4) => "Warn",
            (LogLevel.Error, 3) => "Err",
            (LogLevel.Error, 4) => "Eror",
            (LogLevel.Critical, 3) => "Crt",
            (LogLevel.Critical, 4) => "Crit",
            _ => logLevel.ToString().Substring(0, length)
        };
    }

    /// <summary>
    /// Sets the console foreground and background colours based on log level and options.
    /// </summary>
    /// <param name="logLevel">The log level to determine colour for.</param>
    /// <param name="backgroundColor">
    /// <see langword="true"/> to use background colour for warning, error, and critical; otherwise, <see langword="false"/>.
    /// </param>
    private static void SetConsoleColor(LogLevel logLevel, bool backgroundColor)
    {
        Console.ForegroundColor = logLevel switch
        {
            LogLevel.Trace => ConsoleColor.DarkGray,
            LogLevel.Debug => ConsoleColor.Blue,
            LogLevel.Information => ConsoleColor.DarkGreen,
            LogLevel.Warning => backgroundColor ? ConsoleColor.Black : ConsoleColor.DarkYellow,
            LogLevel.Error => backgroundColor ? ConsoleColor.White : ConsoleColor.Red,
            LogLevel.Critical => backgroundColor ? ConsoleColor.White : ConsoleColor.Magenta,
            _ => Console.ForegroundColor
        };

        if (backgroundColor)
        {
            Console.BackgroundColor = logLevel switch
            {
                LogLevel.Warning => ConsoleColor.DarkYellow,
                LogLevel.Error => ConsoleColor.DarkRed,
                LogLevel.Critical => ConsoleColor.DarkMagenta,
                _ => Console.BackgroundColor
            };
        }
    }
}

/// <summary>
/// Provides configuration options for <see cref="CustomConsoleFormatter"/>.
/// </summary>
/// <remarks>
/// <para>
/// All properties are mutable and may be configured via dependency injection or app settings.
/// </para>
/// </remarks>
public class CustomConsoleFormatterOptions
{
    /// <summary>
    /// Gets or sets the timestamp format string.  Defaults to "yyyy-MM-dd HH:mm:ss".
    /// </summary>
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// Gets or sets a value indicating whether to use UTC timestamps.  Defaults to <see langword="true"/>.
    /// </summary>
    public bool UseUtcTimestamp { get; set; } = true;

    /// <summary>
    /// Gets or sets the log level truncation length (typically 3 or 4).  Defaults to 3.
    /// </summary>
    public int LogLevelTruncationLength { get; set; } = 3;

    /// <summary>
    /// Gets or sets a value indicating whether to use background colour for warning, error, and critical log levels.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool BackgroudColourForWarningErrorCritical { get; set; } = true;
}
