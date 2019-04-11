using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Logging
{
    internal static class LogManager
    {
        public static RedactionLevel RedactionLevel = RedactionLevel.None;

        public static ILoggerFactory LoggerFactory { get; } = new LoggerFactory();

        public static ILogger CreateLogger<T>() => LoggerFactory.CreateLogger<T>();
    }
}
