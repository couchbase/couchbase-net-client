namespace Couchbase.Core.Diagnostics.Metrics
{
    /// <summary>
    /// A NOOP value recorder which records nothing.
    /// </summary>
    internal class NoopValueRecorder : IValueRecorder
    {
        public static IValueRecorder Instance { get; } = new NoopValueRecorder();

        public void RecordValue(uint value)
        {
        }
    }
}
