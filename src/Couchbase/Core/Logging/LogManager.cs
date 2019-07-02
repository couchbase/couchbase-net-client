using System;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Logging
{
    internal static class LogManager
    {
        public static RedactionLevel RedactionLevel = RedactionLevel.None;

        public static ILoggerFactory LoggerFactory { get; set; } = new LoggerFactory();

        public static ILogger CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

        public static ILogger CreateLogger(Type type) => LoggerFactory.CreateLogger(type);
    }
}
