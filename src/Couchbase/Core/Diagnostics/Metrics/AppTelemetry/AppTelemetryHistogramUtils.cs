using Couchbase.Utils;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

public static class AppTelemetryHistogramUtils
{
    private static readonly double[] KvBuckets = [1, 10, 100, 500, 1_000, 2_500, double.PositiveInfinity];
    private static readonly double[] KvBucketsDurable = [10, 100, 500, 1_000, 2_000, 10_000, double.PositiveInfinity];
    private static readonly double[] ServicesBuckets = [100, 1_000, 10_000, 30_000, 75_000, double.PositiveInfinity];

    public static double[] GetPredefinedBucket(AppTelemetryRequestType name)
    {
        switch (name)
        {
            case AppTelemetryRequestType.KvMutationDurable:
                return KvBucketsDurable;
            case AppTelemetryRequestType.KvRetrieval:
            case AppTelemetryRequestType.KvMutationNonDurable:
                return KvBuckets;
            case AppTelemetryRequestType.Query:
            case AppTelemetryRequestType.Search:
            case AppTelemetryRequestType.Analytics:
            case AppTelemetryRequestType.Management:
            case AppTelemetryRequestType.Eventing:
                return ServicesBuckets;
        }

        ThrowHelper.ThrowInvalidArgumentException("Invalid AppTelemetryRequestType");
        return null; //unreachable
    }
}
