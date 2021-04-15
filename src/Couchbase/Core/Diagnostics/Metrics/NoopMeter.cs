using System.Collections.Generic;

namespace Couchbase.Core.Diagnostics.Metrics
{
    /// <summary>
    /// A NOOP meter that does nothing.
    /// </summary>
    internal class NoopMeter : IMeter
    {
        private readonly IValueRecorder _valueRecorder = new NoopValueRecorder();

        public static IMeter Instance { get; } = new NoopMeter();

        public IValueRecorder ValueRecorder(string name, IDictionary<string, string> tags)
        {
            return _valueRecorder;
        }

        public void Dispose()
        {
        }
    }
}
