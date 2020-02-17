using System;

namespace Couchbase.Core.Bootstrapping
{
    /// <summary>
    /// Monitors the client to see if its bootstrapped or not and initiates bootstrapping if its not bootstrapped
    /// </summary>
    internal interface IBootstrapper : IDisposable
    {
        /// <summary>
        /// Interval between checking the bootstrapped state.
        /// </summary>
        TimeSpan SleepDuration { get; set; }

        /// <summary>
        /// Starts the monitoring process.
        /// </summary>
        /// <param name="subject"></param>
        void Start(IBootstrappable subject);
    }
}
