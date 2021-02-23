using System;
using System.Diagnostics;

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// An abstract trace listener that raises trace start/stop trace events when implemented in a concrete class.
    /// </summary>
    public abstract class TraceListener : IDisposable
    {
        /// <summary>
        /// The <see cref="ActivityListener"/> used for listening to trace events.
        /// </summary>
        public ActivityListener Listener { get; } = new();

        /// <summary>
        /// Starts the underlying <see cref="ActivityListener"/> so that trace events can be captured.
        /// </summary>
        public abstract void Start();

        /// <summary>
        /// Disposes of the <see cref="ActivityListener"/> instance.
        /// </summary>
        public void Dispose()
        {
            Listener?.Dispose();
        }
    }
}
