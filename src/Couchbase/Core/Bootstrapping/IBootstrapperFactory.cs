using System;

namespace Couchbase.Core.Bootstrapping
{
    /// <summary>
    /// Factory for creating <see cref="IBootstrapper"/> implementations.
    /// </summary>
    internal interface IBootstrapperFactory
    {
        /// <summary>
        /// The interval between checks to see if the subject is bootstrapped.
        /// </summary>
        /// <param name="sleepDuration">The <see cref="TimeSpan"/>duration</param> to wait between checks.
        /// <returns></returns>
        IBootstrapper Create(TimeSpan sleepDuration);
    }
}
