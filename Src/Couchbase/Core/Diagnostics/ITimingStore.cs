namespace Couchbase.Core.Diagnostics
{
    public interface ITimingStore
    {
        void Write(string format, params object[] args);

        bool Enabled { get; }
    }
}
