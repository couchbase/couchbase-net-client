using System.ComponentModel;

namespace Couchbase
{
    public enum ServiceType
    {
        [Description("kv")]
        KeyValue,

        [Description("views")]
        Views,

        [Description("n1ql")]
        Query,

        [Description("fts")]
        Search,

        [Description("config")]
        Config,

        [Description("cbas")]
        Analytics
    }
}
