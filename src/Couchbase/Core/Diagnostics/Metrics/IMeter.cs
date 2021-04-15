using System;
using System.Collections.Generic;

namespace Couchbase.Core.Diagnostics.Metrics
{
    /// <summary>
    /// An interface for developing classes for collecting and measuring metrics.
    /// </summary>
    public interface IMeter : IDisposable
    {
        /// <summary>
        /// Creates an <see cref="IValueRecorder"/> implementation for collecting metrics.
        /// </summary>
        /// <param name="name">The name of the <see cref="IValueRecorder"/> usually a service name or similar.</param>
        /// <param name="tags">Any tags that are to be associated with the metrics being captured.</param>
        /// <returns></returns>
        IValueRecorder ValueRecorder(string name, IDictionary<string, string> tags = null);
    }
}
