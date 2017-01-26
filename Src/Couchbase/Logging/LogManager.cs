using System;
#if NETSTANDARD
using Microsoft.Extensions.Logging;
#endif

namespace Couchbase.Logging
{
    public static class LogManager
    {
        public static ILog GetLogger<T>()
        {
            return GetLogger(typeof(T));
        }

#if NET45
        public static ILog GetLogger(Type type)
        {
            return new CommonLoggingLogger(Common.Logging.LogManager.GetLogger(type));
        }
#endif

#if NETSTANDARD
        private static ILoggerFactory _factory;

        public static void ConfigureLoggerFactory(ILoggerFactory factory)
        {
            _factory = factory;
        }

        public static ILog GetLogger(Type type)
        {
            if (_factory == null)
            {
                _factory = new LoggerFactory();
            }
            return new MicrosoftLoggingLogger(_factory.CreateLogger(type));
        }
#endif
    }
}
