using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.Logging
{
    /// <summary>
    /// Writes a warning to stderr, once per process, when the application has not given the SDK a logger
    /// that writes anything. Without SDK logs, Couchbase Support cannot easily diagnose problems (SDK-53).
    /// </summary>
    /// <remarks>
    /// The warning goes to <see cref="Console.Error"/> and not through the SDK logger, because the SDK logger is the
    /// thing that is not enabled. The check runs when the first cluster in the process is created by
    /// <see cref="Cluster.ConnectAsync(ClusterOptions, CancellationToken)"/>, before it connects to a server, so the
    /// options of the first cluster decide.
    /// </remarks>
    internal sealed class LoggingNotEnabledWarning
    {
        // The text and the detection settings are all here, so they are easy to find and change.

        /// <summary>
        /// The log category that is checked. Logger filters match by prefix, so a rule for "Couchbase" also applies
        /// to every SDK logger ("Couchbase.Core.ClusterContext", and so on).
        /// </summary>
        internal const string LoggerCategory = "Couchbase";

        /// <summary>
        /// Logging counts as enabled when the <see cref="LoggerCategory"/> logger is enabled at this level.
        /// </summary>
        internal const LogLevel MinimumLevel = LogLevel.Warning;

        internal const string DocsUrl =
            "https://docs.couchbase.com/dotnet-sdk/current/howtos/collecting-information-and-logging.html";

        internal const string Message =
            "WARNING: Couchbase .NET SDK logging is not enabled. Couchbase Support can diagnose problems much more easily with SDK logs.\n" +
            "To enable them, pass your ILoggerFactory to ClusterOptions.WithLogging(...), or use Couchbase.Extensions.DependencyInjection.\n" +
            "See " + DocsUrl;

        /// <summary>
        /// The process-wide instance. It is the default <see cref="LoggingNotEnabledWarning"/> cluster service, which
        /// the cluster constructors use. Tests register their own instance instead.
        /// </summary>
        internal static LoggingNotEnabledWarning Instance { get; } = new(static () => Console.Error);

        private readonly Func<TextWriter> _getWriter;
        private int _checked;

        internal LoggingNotEnabledWarning(Func<TextWriter> getWriter)
        {
            _getWriter = getWriter;
        }

        /// <summary>
        /// On the first call only, writes <see cref="Message"/> if <paramref name="loggerFactory"/> does not log at
        /// <see cref="MinimumLevel"/>. Later calls do nothing. Never throws.
        /// </summary>
        /// <returns><c>true</c> if this call wrote the warning.</returns>
        internal bool WarnOnce(ILoggerFactory? loggerFactory)
        {
            if (Interlocked.Exchange(ref _checked, 1) != 0)
            {
                return false;
            }

            if (IsLoggingEnabled(loggerFactory))
            {
                return false;
            }

            try
            {
                // Console.Error is unavailable or closed in some hosts (services, GUI apps). The warning must never
                // stop the application from connecting.
                _getWriter().WriteLine(Message);
            }
            catch
            {
                // Ignore.
            }

            return true;
        }

        /// <summary>
        /// Returns <c>true</c> if a logger from <paramref name="loggerFactory"/> for <see cref="LoggerCategory"/> is
        /// enabled at <see cref="MinimumLevel"/>. This is false for a null factory, <c>NullLoggerFactory</c>, a factory
        /// with no providers, and a filter that turns the category off.
        /// </summary>
        internal static bool IsLoggingEnabled(ILoggerFactory? loggerFactory)
        {
            if (loggerFactory is null)
            {
                return false;
            }

            try
            {
                return loggerFactory.CreateLogger(LoggerCategory).IsEnabled(MinimumLevel);
            }
            catch
            {
                // A factory that throws is a problem for the application, not one this check can report. Do not warn.
                return true;
            }
        }
    }
}
