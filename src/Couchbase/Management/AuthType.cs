using System.ComponentModel;

namespace Couchbase.Management
{
    public enum AuthType
    {
        [Description("none")]
        None,

        [Description("sasl")]
        Sasl
    }
}