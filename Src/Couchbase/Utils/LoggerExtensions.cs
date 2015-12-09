using System;
using Microsoft.Extensions.Logging;

namespace Couchbase.Utils
{
    public static class LoggerExtensions
    {
        public static void Error(this ILogger logger, string message)
        {
            logger.LogError(message);
        }

        public static void Error(this ILogger logger, Exception exception)
        {
            logger.LogError(exception.ToString(), exception);
        }

        public static void Error(this ILogger logger, string message, Exception exception)
        {
            logger.LogError(message, exception);
        }

        public static void Debug(this ILogger logger, string message)
        {
            logger.LogDebug(message);
        }

        public static void Debug(this ILogger logger, Exception exception)
        {
            logger.LogDebug(exception.ToString(), exception);
        }

        public static void Debug(this ILogger logger, string message, Exception exception)
        {
            logger.LogDebug(message, exception);
        }

        public static void Info(this ILogger logger, string message)
        {
            logger.LogInformation(message);
        }

        public static void Info(this ILogger logger, Exception exception)
        {
            logger.LogInformation(exception.ToString(), exception);
        }

        public static void Info(this ILogger logger, string message, Exception exception)
        {
            logger.LogInformation(message, exception);
        }

        public static void Warn(this ILogger logger, string message)
        {
            logger.LogWarning(message);
        }

        public static void Warn(this ILogger logger, Exception exception)
        {
            logger.LogWarning(exception.ToString(), exception);
        }

        public static void Warn(this ILogger logger, string message, Exception exception)
        {
            logger.LogWarning(message, exception);
        }
        
        public static void Trace(this ILogger logger, string message)
        {
            logger.LogInformation(message);
        }

        public static bool IsDebugEnabled(this ILogger logger)
        {
            return logger.IsEnabled(LogLevel.Debug);
        }
    }
}