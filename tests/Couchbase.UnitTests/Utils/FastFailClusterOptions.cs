using System;

namespace Couchbase.UnitTests.Utils;

[Flags]
internal enum FastFailServices
{
    None = 0,
    Kv = 1 << 0,
    Query = 1 << 1,
    Analytics = 1 << 2,
    Search = 1 << 3,
    View = 1 << 4,
    Management = 1 << 5,
    DisableDnsSrv = 1 << 6,
}

internal static class FastFailClusterOptions
{
    private static readonly TimeSpan FastFail = TimeSpan.FromMilliseconds(100);

    // Unit tests that deliberately point at an unreachable host need short timeouts so the
    // failing op surfaces a CouchbaseException quickly instead of waiting the default
    // per-service timeout (75s for Query/Analytics/Search/View/Management, 2.5s for KV).
    // Classic-scheme bootstrap additionally does DNS SRV resolution that adds several
    // seconds. This extension applies only the knobs the caller actually exercises so each
    // test surface is explicit.
    public static ClusterOptions WithFastFailTimeouts(this ClusterOptions options, FastFailServices services)
    {
        options.KvConnectTimeout = TimeSpan.FromMilliseconds(1);
        if ((services & FastFailServices.DisableDnsSrv) != 0) options.EnableDnsSrvResolution = false;
        if ((services & FastFailServices.Kv) != 0) options.KvTimeout = FastFail;
        if ((services & FastFailServices.Query) != 0) options.QueryTimeout = FastFail;
        if ((services & FastFailServices.Analytics) != 0) options.AnalyticsTimeout = FastFail;
        if ((services & FastFailServices.Search) != 0) options.SearchTimeout = FastFail;
        if ((services & FastFailServices.View) != 0) options.ViewTimeout = FastFail;
        if ((services & FastFailServices.Management) != 0) options.ManagementTimeout = FastFail;
        return options;
    }
}
