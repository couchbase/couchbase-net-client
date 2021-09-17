using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#nullable enable

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    internal class LoggingConfigurator : IConfigureOptions<ClusterOptions>
    {
        private readonly ILoggerFactory? _loggerFactory;

        public LoggingConfigurator(ILoggerFactory? loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public void Configure(ClusterOptions options)
        {
            options.WithLogging(_loggerFactory);
        }
    }
}
