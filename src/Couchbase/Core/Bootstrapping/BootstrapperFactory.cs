using System;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Bootstrapping
{
    /// <inheritdoc />
    internal class BootstrapperFactory : IBootstrapperFactory
    {
        private readonly ILogger<Bootstrapper> _logger;

        public BootstrapperFactory(ILogger<Bootstrapper> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public IBootstrapper Create(TimeSpan sleepDuration)
        {
            return new Bootstrapper(_logger)
            {
                SleepDuration = sleepDuration
            };
        }
    }
}
