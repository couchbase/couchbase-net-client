#if NETSTANDARD
using System;
using Microsoft.Extensions.Logging;

namespace Couchbase.Logging
{
    internal class MicrosoftLoggingLogger : ILog
    {
        private readonly ILogger _logger;

        public bool IsDebugEnabled { get { return _logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug); } }

        public MicrosoftLoggingLogger(ILogger logger)
        {
            _logger = logger;
        }

        public void Trace(string message, params object[] args)
        {
            _logger.LogTrace(message, args);
        }

        public void Trace(Exception exception)
        {
            _logger.LogTrace(default(EventId), exception, string.Empty);
        }

        public void Trace(string message, Exception exception)
        {
            _logger.LogTrace(default(EventId), exception, message);
        }

        public void Debug(string format, params object[] args)
        {
            _logger.LogDebug(format, args);
        }

        public void Debug(Exception exception)
        {
            _logger.LogDebug(default(EventId), exception, string.Empty);
        }

        public void Debug(string message, Exception exception)
        {
            _logger.LogDebug(default(EventId), exception, message);
        }

        public void Info(string format, params object[] args)
        {
            _logger.LogInformation(format, args);
        }

        public void Info(Exception exception)
        {
            _logger.LogInformation(default(EventId), exception, string.Empty);
        }

        public void Info(string message, Exception exception)
        {
            _logger.LogInformation(default(EventId), exception, message);
        }

        public void Warn(string message, Exception exception)
        {
            _logger.LogWarning(default(EventId), message, exception);
        }

        public void Warn(Exception exception)
        {
            _logger.LogWarning(default(EventId), exception, string.Empty);
        }

        public void Warn(string message, params object[] args)
        {
            _logger.LogWarning(message, args);
        }

        public void Error(string message, params object[] args)
        {
            _logger.LogError(message, args);
        }

        public void Error(Exception exception)
        {
            _logger.LogError(default(EventId), exception, string.Empty);
        }

        public void Error(string message, Exception exception)
        {
            _logger.LogError(default(EventId), message, exception);
        }
    }
}
#endif
