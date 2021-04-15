namespace Couchbase.Core.Diagnostics.Metrics
{
    /// <summary>
    /// An interface for collecting metric data and associated with a <see cref="IMeter"/>.
    /// </summary>
    public interface IValueRecorder
    {
        /// <summary>
        /// Collects metric data and forwards it to its <see cref="IMeter"/> parent.
        /// </summary>
        /// <param name="value">The value to measure.</param>
        void RecordValue(uint value);
    }
}
