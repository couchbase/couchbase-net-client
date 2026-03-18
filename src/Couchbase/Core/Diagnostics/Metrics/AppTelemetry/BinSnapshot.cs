using Couchbase.Core.Compatibility;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

[InterfaceStability(Level.Volatile)]
internal readonly struct BinSnapshot(double boundary, uint count, double sumMilliseconds)
{
    public readonly double Boundary = boundary;
    public readonly uint Count = count;
    public readonly double SumMilliseconds = sumMilliseconds;
}
