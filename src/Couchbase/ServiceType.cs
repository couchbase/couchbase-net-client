using System.ComponentModel;

namespace Couchbase
{
    public enum ServiceType
    {
        [Description("kv")]
        KeyValue = 0,

        [Description("views")]
        Views = 1,

        [Description("n1ql")]
        Query = 2,

        [Description("fts")]
        Search = 3,

        [Description("config")]
        Config = 4,

        [Description("cbas")]
        Analytics = 5
    }
}
