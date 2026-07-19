#if MACCATALYST
using System.Runtime.InteropServices;
using Foundation;
using Microsoft.Extensions.Logging;

namespace ServiceDelivery.Client.Desktop.Services;

/// <summary>
/// FE-003 (cycle 9) live-gate diagnostic. A minimal <see cref="ILoggerProvider"/> whose loggers write each
/// formatted line to the macOS unified log via Foundation's <c>NSLog</c>. Under the live Mac2/Appium Desktop
/// gate the app is launched by XCTest, which swallows stdout — so <c>AddDebug()</c> / console output never
/// surfaces. NSLog reaches the unified log under ANY launcher, so this is what makes the SignalR
/// HubConnection's internal transport/dispatch logs (now routed through <c>ILoggerFactory</c>) observable via
/// <c>log show</c> / Console.app when a fleet marker fails to render under XCTest.
/// <para>
/// MacCatalyst-only (<c>Foundation</c>); registered behind <c>#if DEBUG &amp;&amp; MACCATALYST</c> in
/// <c>MauiProgram.cs</c>. This is host bootstrapping / diagnostic code — no unit test per repo conventions
/// (hosts are bootstrapping-only). Verified only by the maccatalyst build.
/// </para>
/// </summary>
public sealed class OsLogLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new OsLogLogger(categoryName);

    public void Dispose()
    {
    }

    private sealed class OsLogLogger(string category) : ILogger
    {
        // NSLog is variadic in C ("void NSLog(NSString *format, ...)"). We pass ONLY the format and no
        // variadic arguments, so a single-parameter P/Invoke is safe on every ABI (including arm64, where
        // variadic args are passed on the stack): the already-formatted, %-escaped NSString IS the format.
        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation", EntryPoint = "NSLog")]
        private static extern void NSLog(IntPtr format);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (exception is not null)
            {
                message = $"{message}\n{exception}";
            }

            // Escape every '%' so NSLog treats the whole string as a literal format with no conversions.
            var line = $"[{logLevel}] {category}: {message}".Replace("%", "%%");
            using var formatted = new NSString(line);
            NSLog(formatted.Handle);
        }
    }
}
#endif
