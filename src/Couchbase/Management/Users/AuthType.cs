using System.ComponentModel;

namespace Couchbase.Management.Users
{
    public enum AuthType
    {
        [Description("none")]
        None,

        [Description("sasl")]
        Sasl
    }
}
