using System.ComponentModel;

namespace Couchbase.Services
{
    public enum ServiceType
    {
        [Description("kv")]
        KeyValue,

        [Description("n1ql")]
        Query,

        [Description("fts")]
        Search,

        [Description("cbas")]
        Analytics
    }
}
